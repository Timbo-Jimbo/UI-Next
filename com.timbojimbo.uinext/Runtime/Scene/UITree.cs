using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// One root's nodes as flat arrays in pre-order, with the two computations that work on them: layout (which reads
    /// every node's layout values as they are going, solves, and gives each node its frame) and compositing (which
    /// reads every node's frame and drawn values as they are drawn, and works out where each is drawn in the root's
    /// space, how opaque, and what clips it). Each runs again only when something it read changes.
    /// </summary>
    internal sealed class UITree : IDisposable
    {
        private static readonly List<UITree> s_trees = new();

        public readonly UIRoot Root;
        public readonly UINode RootNode;

        // Every node under the root, in pre-order, out of layout ones included, and where each one's subtree ends.
        public readonly List<UINode> Nodes = new();
        public readonly List<int> SubtreeEnd = new();

        // The solver's nodes for those in layout, and which node each is.
        private SolverNode[] _solver = new SolverNode[32];
        private int[] _solverToNode = new int[32];
        private int[] _lastChild = new int[32];
        private int[] _solverIndex = new int[32];

        // Per node, as compositing last found it: where it is drawn, how opaque, whether shown, its clips.
        public NodeSlot[] Slots = new NodeSlot[32];
        public Affine2[] World = new Affine2[32];
        public bool[] Shown = new bool[32];
        public readonly List<ClipShape> ClipShapes = new();
        private Vector4[] _clipChains = new Vector4[32];

        private readonly LayoutComputation _layout;
        private readonly CompositeComputation _composite;
        private bool _structureDirty = true;
        private DrivenRectTransformTracker _tracker;

        public UITree(UIRoot root)
        {
            Root = root;
            RootNode = root.Node;
            _layout = new LayoutComputation(this);
            _composite = new CompositeComputation(this);
        }

        public static IReadOnlyList<UITree> Trees => s_trees;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_trees.Clear();

        public static void Register(UITree tree)
        {
            if (s_trees.Contains(tree)) return;
            s_trees.Add(tree);
            SortTrees();
            tree.InvalidateStructure();
        }

        public static void Unregister(UITree tree)
        {
            s_trees.Remove(tree);
            Compositor.MarkOrderDirty();
        }

        public static void SortTrees()
        {
            s_trees.Sort((a, b) => a.Root.SortOrder.CompareTo(b.Root.SortOrder));
            Compositor.MarkOrderDirty();
        }

        /// <summary>A node was enabled, disabled or moved: every tree collects its nodes again.</summary>
        public static void NodeChanged(UINode node)
        {
            foreach (var tree in s_trees)
                tree.InvalidateStructure();
            UI.RequestRepaint();
        }

        public void InvalidateStructure()
        {
            _structureDirty = true;
            _layout.Invalidate();
            _composite.Invalidate();
            Compositor.MarkOrderDirty();
        }

        public void InvalidateLayout() => _layout.Invalidate();

        public void Remove(UINode node)
        {
            if (node.Tree != this) return;
            node.Tree = null;
            node.ParentNode = null;
            node.Index = -1;
            InvalidateStructure();
        }

        public void Dispose()
        {
            _layout.Dispose();
            _composite.Dispose();
            _tracker.Clear();
            foreach (var node in Nodes)
            {
                if (node == null || node.Tree != this) continue;
                node.Tree = null;
                node.ParentNode = null;
                node.Index = -1;
            }
            Nodes.Clear();
        }

        // ── Structure ────────────────────────────────────────────────────────────

        // Collects the nodes under the root in pre-order: enabled nodes, through plain objects, not into disabled nodes
        // or other roots.
        private void Rebuild()
        {
            _structureDirty = false;
            foreach (var node in Nodes)
            {
                if (node != null && node.Tree == this)
                {
                    node.Tree = null;
                    node.ParentNode = null;
                    node.Index = -1;
                }
            }
            Nodes.Clear();
            SubtreeEnd.Clear();
            _tracker.Clear();
            if (RootNode == null || !RootNode.isActiveAndEnabled) return;
            Add(RootNode, null);
            Collect(RootNode.transform, RootNode);
            SubtreeEnd[0] = Nodes.Count;
            Compositor.MarkOrderDirty();
        }

        private void Collect(Transform parent, UINode parentNode)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.TryGetComponent(out UINode node))
                {
                    if (!node.isActiveAndEnabled || child.TryGetComponent(out UIRoot _)) continue;
                    Add(node, parentNode);
                    Collect(child, node);
                    SubtreeEnd[node.Index] = Nodes.Count;
                }
                else
                {
                    Collect(child, parentNode);
                }
            }
        }

        private void Add(UINode node, UINode parent)
        {
            node.Tree = this;
            node.ParentNode = parent;
            node.Index = Nodes.Count;
            Nodes.Add(node);
            SubtreeEnd.Add(Nodes.Count);
            if (parent != null)
                _tracker.Add(Root, node.RectTransform, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta
                    | DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot);
        }

        // ── Layout ───────────────────────────────────────────────────────────────

        private void LayOut()
        {
            if (_structureDirty)
                Rebuild();
            int nodeCount = Nodes.Count;
            if (nodeCount == 0) return;
            EnsureCapacity(nodeCount);

            var rootSize = Root.SizeObservable.Value;
            var safeArea = Root.SafeAreaObservable.Value;

            int count = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                var node = Nodes[i];
                _solverIndex[i] = -1;
                var parent = node.ParentNode;
                int parentIndex = parent != null ? _solverIndex[parent.Index] : -1;
                if (parent != null && parentIndex < 0) continue;
                if (parent != null && node.Display.Value == DisplayMode.None) continue;

                _solverIndex[i] = count;
                _solverToNode[count] = i;
                _lastChild[count] = -1;
                ref var s = ref _solver[count];
                s = new SolverNode
                {
                    Parent = parentIndex,
                    FirstChild = -1,
                    NextSibling = -1,
                    Width = node.Width.Value,
                    Height = node.Height.Value,
                    Padding = node.Padding.Value,
                    ChildGap = node.Gap.Value,
                    Direction = node.Direction.Value,
                    Wrap = node.Wrap.Value,
                    AlignX = node.AlignX.Value,
                    AlignY = node.AlignY.Value,
                    AspectRatio = node.AspectRatio.Value,
                    Floating = node.Floating.Value,
                    Element = -1,
                    Scroll = node.Scroller != null && node.Scroller.isActiveAndEnabled ? node.Scroller.Axis.Value : ScrollAxis.None,
                    Content = node.Content,
                };
                if (parentIndex >= 0)
                {
                    int last = _lastChild[parentIndex];
                    if (last < 0) _solver[parentIndex].FirstChild = count;
                    else _solver[last].NextSibling = count;
                    _lastChild[parentIndex] = count;
                }
                count++;
            }

            // Floating against an element: the element's solver index, or against the parent when it is not laid out here.
            for (int k = 1; k < count; k++)
            {
                ref var s = ref _solver[k];
                if (s.Floating.AttachTo != FloatingAttach.Element) continue;
                var element = s.Floating.Element;
                if (element != null && element.Tree == this && element.Index >= 0 && _solverIndex[element.Index] >= 0)
                    s.Element = _solverIndex[element.Index];
                else
                    s.Floating.AttachTo = FloatingAttach.Parent;
            }

            LayoutSolver.Solve(_solver, count, rootSize, safeArea);

            for (int k = 0; k < count; k++)
            {
                var node = Nodes[_solverToNode[k]];
                ref var s = ref _solver[k];
                var rect = k == 0 ? new Rect(0f, 0f, rootSize.x, rootSize.y) : s.Rect;
                node.Frame.SetModel(rect, null);
                if (k > 0)
                    WriteRectTransform(node.RectTransform, rect);
                if (node.Scroller != null)
                    node.Scroller.SetExtent(s.Rect.size, s.ContentSize, s.Reach);
            }
        }

        // The model, for the scene view and for code reading transforms: anchored and pivoted at the parent's top-left.
        private static void WriteRectTransform(RectTransform rt, Rect rect)
        {
            var corner = new Vector2(0f, 1f);
            if (rt.anchorMin != corner) rt.anchorMin = corner;
            if (rt.anchorMax != corner) rt.anchorMax = corner;
            if (rt.pivot != corner) rt.pivot = corner;
            var position = new Vector2(rect.x, -rect.y);
            if (rt.anchoredPosition != position) rt.anchoredPosition = position;
            if (rt.sizeDelta != rect.size) rt.sizeDelta = rect.size;
        }

        // ── Compositing ──────────────────────────────────────────────────────────

        private void Composite()
        {
            int nodeCount = Nodes.Count;
            EnsureCapacity(nodeCount);
            ClipShapes.Clear();
            var rootSize = Root.SizeObservable.Value;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = Nodes[i];
                var parent = node.ParentNode;
                int p = parent != null ? parent.Index : -1;

                var frame = p < 0 ? new Rect(0f, 0f, rootSize.x, rootSize.y) : node.Frame.Value;
                var offset = node.Offset.Value;
                float scale = node.Scale.Value;
                float rotation = node.Rotation.Value;
                float opacity = Mathf.Clamp01(node.Opacity.Value);
                bool shown = node.Display.Value == DisplayMode.Visible && (p < 0 || Shown[p]);
                bool topLayer = node.TopLayer.Value;
                _ = node.ZIndex.Value;

                var local = Affine2.Translate(frame.position + offset)
                            * Affine2.AroundPivot(frame.size * 0.5f, scale, rotation);
                Affine2 world;
                if (p < 0)
                {
                    world = local;
                }
                else
                {
                    var parentWorld = World[p];
                    if (parent.Scroller != null && parent.Scroller.isActiveAndEnabled)
                        parentWorld = parentWorld * Affine2.Translate(-parent.Scroller.Offset.Value);
                    world = parentWorld * local;
                }
                World[i] = world;
                Shown[i] = shown;
                float effectiveOpacity = (p < 0 ? 1f : Slots[p].SizeOpacity.z) * opacity;

                // Its clips: its parent's, and its parent's own clip; none for a node in the top layer.
                var chain = new Vector4(-1f, -1f, -1f, -1f);
                if (p >= 0 && !topLayer)
                {
                    chain = _clipChains[p];
                    if (parent.Clips.Value)
                        chain = Append(chain, OwnClip(parent, p));
                }
                _clipChains[i] = chain;
                Slots[i] = new NodeSlot
                {
                    Row0 = new Vector4(world.A, world.B, world.Tx, 0f),
                    Row1 = new Vector4(world.C, world.D, world.Ty, 0f),
                    SizeOpacity = new Vector4(frame.width, frame.height, shown ? effectiveOpacity : 0f,
                        1f / Mathf.Max(1e-4f, Root.PixelsPerPoint * world.ScaleFactor)),
                    Clips = chain,
                };
            }
            Compositor.MarkSlotsDirty();
            Compositor.MarkOrderDirty();
        }

        // The clip a node gives what is inside it: its own rect and clip radii, in its own space.
        private int OwnClip(UINode node, int index)
        {
            var inverse = World[index].Inverse;
            var size = new Vector2(Slots[index].SizeOpacity.x, Slots[index].SizeOpacity.y);
            ClipShapes.Add(new ClipShape
            {
                Inverse0 = new Vector4(inverse.A, inverse.B, inverse.Tx, 0f),
                Inverse1 = new Vector4(inverse.C, inverse.D, inverse.Ty, 0f),
                Size = new Vector4(size.x, size.y, 0f, 0f),
                Radii = node.ClipRadii.Value.ToVector(),
            });
            return ClipShapes.Count - 1;
        }

        // A chain with one more clip, keeping the innermost four.
        private static Vector4 Append(Vector4 chain, int clip)
        {
            for (int c = 0; c < 4; c++)
            {
                if (chain[c] >= 0f) continue;
                chain[c] = clip;
                return chain;
            }
            chain = new Vector4(chain.y, chain.z, chain.w, clip);
            return chain;
        }

        private void EnsureCapacity(int count)
        {
            if (_solver.Length >= count && Slots.Length >= count) return;
            int size = Mathf.NextPowerOfTwo(Mathf.Max(count, 32));
            Array.Resize(ref _solver, size);
            Array.Resize(ref _solverToNode, size);
            Array.Resize(ref _lastChild, size);
            Array.Resize(ref _solverIndex, size);
            Array.Resize(ref Slots, size);
            Array.Resize(ref World, size);
            Array.Resize(ref Shown, size);
            Array.Resize(ref _clipChains, size);
        }

        private sealed class LayoutComputation : Computation
        {
            private readonly UITree _tree;

            public LayoutComputation(UITree tree) : base(Phase.Layout) => _tree = tree;

            protected override void Execute() => _tree.LayOut();
        }

        private sealed class CompositeComputation : Computation
        {
            private readonly UITree _tree;

            public CompositeComputation(UITree tree) : base(Phase.Draw) => _tree = tree;

            protected override void Execute() => _tree.Composite();
        }
    }
}
