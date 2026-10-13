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

        // How opaque each node is drawn, all told; and, for a node drawn as a group (a composite group not fully opaque,
        // blurred or masked, or a node a transition is fading), the opacity its image is drawn at (0 for the rest).
        // Inside a group, slots' opacities are the group's own.
        public float[] Opacities = new float[32];
        public float[] GroupOpacities = new float[32];

        // How opaque each node is on its own (its opacity and its transition's), for drawing it apart from what is above
        // it (in a matched flight).
        public float[] LocalOpacities = new float[32];

        // The compositing pass that last ran, numbered across every tree: the nodes it drew carry its number.
        public int CompositePass { get; private set; } = -1;
        private static int s_compositePasses;

        // How far each node drawn as a group is blurred, in points (0 for none, and for nodes not drawn as groups).
        public float[] Blurs = new float[32];
        public readonly List<ClipShape> ClipShapes = new();
        private Vector4[] _clipChains = new Vector4[32];

        // Per node, how far what it holds is moved in its own space by its scrolling: drawn from under its top and left
        // clear insets, less its offset (nothing for a node that does not scroll).
        private Vector2[] _scrolled = new Vector2[32];

        // The nodes shown, in the order they are drawn (the compositor's), which hit testing walks backwards; and each
        // presentation level drawn (its root's node, then each presented node, lowest first), where it starts in it, and
        // whether it takes pointers.
        public readonly List<int> DrawOrder = new();
        public readonly List<UINode> Levels = new();
        public readonly List<int> LevelStarts = new();
        public readonly List<bool> LevelInteractive = new();

        private readonly LayoutComputation _layout;
        private readonly CompositeComputation _composite;
        private int _pass;
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

        public void InvalidateComposite() => _composite.Invalidate();

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
            if (node.LastParent != parent)
            {
                node.LastParent = parent;
                node.HierarchyChanged();
            }
            node.Index = Nodes.Count;
            Nodes.Add(node);
            SubtreeEnd.Add(Nodes.Count);
            if (parent != null)
                _tracker.Add(Root, node.RectTransform, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta
                    | DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot);
        }

        // ── Hit testing ──────────────────────────────────────────────────────────

        /// <summary>
        /// The node drawn topmost at <paramref name="point"/> (its root's points) that takes the pointer: shown, not
        /// see-through, hit testable, with something drawn or something listening, and inside its clips. -1 for none.
        /// </summary>
        public int HitTest(Vector2 point)
        {
            int level = LevelStarts.Count - 1;
            for (int k = DrawOrder.Count - 1; k >= 0; k--)
            {
                while (level > 0 && k < LevelStarts[level]) level--;
                // A presentation on its way out takes no pointers: what it uncovers does.
                if (level >= 0 && level < LevelInteractive.Count && !LevelInteractive[level]) continue;
                int i = DrawOrder[k];
                if (i >= Nodes.Count) continue;
                var node = Nodes[i];
                if (node == null || !Shown[i] || Opacities[i] <= 0.001f) continue;
                if (!node.HitTestable.Peek || node.Leaving.Peek) continue;
                if (node.Drawables.Count == 0 && !node.TryGetComponent<IPointerHandler>(out _)) continue;
                if (Contains(i, point, 0f) && InsideClips(i, point))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// The node drawn topmost at <paramref name="point"/> (its root's points) that <paramref name="accepts"/>, inside
        /// its clips, whether it takes the pointer or not: what a click in the Scene view selects. -1 for none.
        /// </summary>
        internal int PickTopmost(Vector2 point, Predicate<UINode> accepts)
        {
            for (int k = DrawOrder.Count - 1; k >= 0; k--)
            {
                int i = DrawOrder[k];
                if (i >= Nodes.Count) continue;
                var node = Nodes[i];
                if (node == null || !Shown[i] || Opacities[i] <= 0.001f || !accepts(node)) continue;
                if (Contains(i, point, 0f) && InsideClips(i, point))
                    return i;
            }
            return -1;
        }

        /// <summary>Whether <paramref name="point"/> is over node <paramref name="index"/> as drawn, give or take <paramref name="slop"/> points.</summary>
        public bool Contains(int index, Vector2 point, float slop)
        {
            if (index < 0 || index >= Nodes.Count) return false;
            var local = World[index].Inverse.Apply(point);
            var size = Slots[index].SizeOpacity;
            return local.x >= -slop && local.y >= -slop && local.x <= size.x + slop && local.y <= size.y + slop;
        }

        private bool InsideClips(int index, Vector2 point)
        {
            var chain = Slots[index].Clips;
            for (int c = 0; c < 4; c++)
            {
                int clip = (int)chain[c];
                if (clip < 0) break;
                if (clip >= ClipShapes.Count) continue;
                var shape = ClipShapes[clip];
                float x = shape.Inverse0.x * point.x + shape.Inverse0.y * point.y + shape.Inverse0.z;
                float y = shape.Inverse1.x * point.x + shape.Inverse1.y * point.y + shape.Inverse1.z;
                if (x < 0f || y < 0f || x > shape.Size.x || y > shape.Size.y) return false;
            }
            return true;
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
            _keyboard = UIRoot.KeyboardObservable.Value / Root.PixelsPerPoint;
            _pass++;

            // The tree, then every presented node in it on its own: a presented screen is laid out as a root is,
            // covering the root whatever its place in the hierarchy, its padding kept clear of the safe area.
            Solve(0, rootSize, safeArea);
            for (int i = 1; i < nodeCount; i++)
            {
                var node = Nodes[i];
                if (node.PresentationLevel.Value != 0 && node.Display.Value != DisplayMode.None)
                    Solve(i, rootSize, safeArea);
            }
        }

        // How far up the root the software keyboard reaches, in its points, for this layout.
        private float _keyboard;

        // A node avoiding the keyboard keeps what is inside it clear of it: its bottom padding is at least how far the
        // keyboard reaches into it, by where its bottom was drawn last frame.
        private Insets AvoidKeyboard(UINode node, Insets padding)
        {
            if (!node.AvoidsKeyboard.Value || _keyboard <= 0f) return padding;
            float below = Mathf.Max(0f, Root.Size.y - node.DrawnRect.yMax);
            padding.Bottom = Mathf.Max(padding.Bottom, _keyboard - below);
            return padding;
        }

        // Lays out the subtree under node `start` as a tree of its own over the root's rect, leaving out what is not
        // laid out (Display None) and the presented nodes in it (laid out on their own).
        private void Solve(int start, Vector2 rootSize, Insets safeArea)
        {
            int end = SubtreeEnd[start];
            int count = 0;
            for (int i = start; i < end; i++)
            {
                var node = Nodes[i];
                _solverIndex[i] = -1;
                int parentIndex = -1;
                if (i != start)
                {
                    var parent = node.ParentNode;
                    parentIndex = parent != null ? _solverIndex[parent.Index] : -1;
                    if (parentIndex < 0) continue;
                    if (node.Display.Value == DisplayMode.None || node.PresentationLevel.Value != 0) continue;
                }

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
                    Padding = AvoidKeyboard(node, node.Padding.Value),
                    ChildGap = node.Gap.Value,
                    Direction = node.Direction.Value,
                    Wrap = node.Wrap.Value,
                    AlignX = node.AlignX.Value,
                    AlignY = node.AlignY.Value,
                    AspectRatio = node.AspectRatio.Value,
                    Floating = i == start ? default : node.Floating.Value,
                    Element = -1,
                    Ignores = node.IgnoresSafeArea.Value,
                    Scroll = node.Scroller != null && node.Scroller.isActiveAndEnabled ? node.Scroller.Axis.Value : ScrollAxis.None,
                    Content = node.Content,
                    Node = node,
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

            // Floating against an element: the element's solver index, or against the parent when it is not laid out
            // in this solve.
            for (int k = 1; k < count; k++)
            {
                ref var s = ref _solver[k];
                if (s.Floating.AttachTo != FloatingAttach.Element) continue;
                var element = s.Floating.Element;
                if (element != null && element.Tree == this && element.Index >= start && element.Index < end && _solverIndex[element.Index] >= 0)
                    s.Element = _solverIndex[element.Index];
                else
                    s.Floating.AttachTo = FloatingAttach.Parent;
            }

            using (UIProfiling.Solve.Auto())
                LayoutSolver.Solve(_solver, count, rootSize, safeArea);

            for (int k = 0; k < count; k++)
            {
                var node = Nodes[_solverToNode[k]];
                ref var s = ref _solver[k];
                var rect = k == 0 ? new Rect(0f, 0f, rootSize.x, rootSize.y) : s.Rect;
                // Laid out in the pass before, it carries on from where it is drawn (springing inside a change), unless it
                // lands at once; coming back into layout, it is where it goes at once, and whatever brings it in starts
                // from there.
                bool carriesOn = node.LayoutPass == _pass - 1 || node.LayoutPass == _pass;
                node.LayoutPass = _pass;
                if (carriesOn && !node.LandsAtOnce)
                {
                    node.Frame.SetModel(rect, null);
                }
                else
                {
                    node.Frame.Jump(rect);
                    if (!carriesOn)
                        node.EnteredLayout?.Invoke(rect);
                }
                if (k > 0)
                    WriteRectTransform(node.RectTransform, rect);
                else if (start > 0)
                    WriteRectTransform(node.RectTransform, new Rect(-RootPosition(node.ParentNode), rootSize));
                if (node.Scroller != null)
                    node.Scroller.SetExtent(s.Rect.size, s.ContentSize, s.Reach);
            }
        }

        // Where a node is laid out in its root's space, by its frames up the tree.
        private static Vector2 RootPosition(UINode node)
        {
            var position = Vector2.zero;
            for (; node != null && node.ParentNode != null; node = node.ParentNode)
            {
                if (node.PresentationLevel.Peek != 0) break;
                position += node.Frame.Peek.position;
            }
            return position;
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
            int pass = CompositePass = ++s_compositePasses;

            for (int i = 0; i < nodeCount; i++)
            {
                var node = Nodes[i];
                // A presented node hangs off the root, whatever its place in the hierarchy.
                var parent = i > 0 && node.PresentationLevel.Value != 0 ? RootNode : node.ParentNode;
                int p = parent != null ? parent.Index : -1;

                var frame = p < 0 ? new Rect(0f, 0f, rootSize.x, rootSize.y) : node.Frame.Value;
                var offset = node.Offset.Value;
                float scale = node.Scale.Value;
                float rotation = node.Rotation.Value;
                float opacity = Mathf.Clamp01(node.Opacity.Value);
                var transition = node.Transition;
                if (transition != null)
                {
                    offset += transition.Offset.Value;
                    scale *= transition.Scale.Value;
                    opacity *= Mathf.Clamp01(transition.Opacity.Value);
                }
                bool shown = (node.Display.Value == DisplayMode.Visible || node.Leaving.Value) && (p < 0 || Shown[p]);
                // Lifted into the top layer or presented: out of its ancestors' clips.
                bool topLayer = node.TopLayer.Value || node.PresentationLevel.Value != 0;
                _ = node.ZIndex.Value;

                var local = Affine2.Translate(frame.position + offset)
                            * Affine2.AroundPivot(frame.size * 0.5f, scale, rotation);
                var world = p < 0 ? local : World[p] * Affine2.Translate(_scrolled[p]) * local;
                var scroller = node.Scroller;
                _scrolled[i] = scroller != null && scroller.isActiveAndEnabled ? scroller.ClearShift - scroller.Offset.Value : Vector2.zero;
                World[i] = world;
                Shown[i] = shown;
                float effectiveOpacity = (p < 0 ? 1f : Slots[p].SizeOpacity.z) * opacity;
                // Colour filters multiply down the tree, as opacity does, and every pixel drawn applies them.
                float saturation = (p < 0 ? 1f : Slots[p].Row0.w) * Mathf.Max(0f, node.Saturation.Value);
                float brightness = (p < 0 ? 1f : Slots[p].Row1.w) * Mathf.Max(0f, node.Brightness.Value);
                Opacities[i] = (p < 0 ? 1f : Opacities[p]) * opacity;
                // A group not fully opaque: what is in it is drawn at its own opacities into the group's image, which
                // is drawn at the group's. A node a transition is fading (a page, an alert, an item going away) fades
                // as a group too, so its parts never show through each other on the way.
                bool fadingIn = transition != null && transition.Opacity.Value < 0.999f;
                // A composite group is drawn as one image while not fully opaque, blurred or masked: its image is what is
                // faded, blurred and masked, however opaque it is. Blur and masks apply to nothing else. A node drawn at
                // no opacity at all (faded out, a fade's first frame, a bouncy fade past nothing) is no group: nothing in
                // it is seen, so its image is not made, and what is in it is drawn at nothing (not at the group's 1).
                bool composite = node.CompositeGroup.Value;
                float blur = composite ? node.Blur.Value : 0f;
                bool masked = composite && node.Mask.Value != null;
                if (!composite && !node.WarnedOutsideGroup && (node.Blur.Value > 0.01f || node.Mask.Value != null))
                {
                    node.WarnedOutsideGroup = true;
                    Debug.LogWarning($"UI Next: {node.name} has a blur or a mask, which apply only to a composite group; turn on its Composite Group.", node);
                }
                bool group = shown && effectiveOpacity > 0f
                             && (((composite || fadingIn) && opacity < 0.999f) || blur > 0.01f || masked);
                GroupOpacities[i] = group ? effectiveOpacity : 0f;
                Blurs[i] = group ? Mathf.Max(0f, blur) : 0f;
                if (group) effectiveOpacity = 1f;

                // Its clips: its parent's, and its parent's own clip; none for a node in the top layer.
                var chain = new Vector4(-1f, -1f, -1f, -1f);
                if (p >= 0 && !topLayer)
                {
                    chain = _clipChains[p];
                    if (parent.Clips.Value || (parent.Scroller != null && parent.Scroller.isActiveAndEnabled))
                        chain = Append(chain, OwnClip(parent, p));
                }
                _clipChains[i] = chain;
                Slots[i] = new NodeSlot
                {
                    Row0 = new Vector4(world.A, world.B, world.Tx, saturation),
                    Row1 = new Vector4(world.C, world.D, world.Ty, brightness),
                    SizeOpacity = new Vector4(frame.width, frame.height, shown ? effectiveOpacity : 0f,
                        1f / Mathf.Max(1e-4f, Root.PixelsPerPoint * world.ScaleFactor)),
                    Clips = chain,
                };
                LocalOpacities[i] = opacity;
                node.DrawnSlot = Slots[i];
                node.DrawnOpacity = opacity;
                node.DrawnShown = shown;
                node.DrawnPass = pass;
            }
            Compositor.MarkSlotsDirty();
            Compositor.MarkOrderDirty();
        }

        /// <summary>
        /// Where <paramref name="node"/> is drawn <paramref name="ahead"/> seconds from now if everything that moves it keeps
        /// the speed it has (its frame, offset, scale, turn and transition, and those of everything above it, and the scroll
        /// views it is in), worked out as compositing works it out: its transform from its own space to its root's, and
        /// its size. At no time ahead, where it is drawn now.
        /// </summary>
        internal Affine2 WorldAhead(UINode node, float ahead, out Vector2 size)
        {
            // A presented node hangs off the root, whatever its place in the hierarchy.
            var parent = node == RootNode || node.ParentNode == null ? null
                : node.PresentationLevel.Peek != 0 ? RootNode : node.ParentNode;
            var frame = parent == null ? new Rect(Vector2.zero, Root.Size) : node.Frame.DrawnAhead(ahead);
            var offset = node.Offset.DrawnAhead(ahead);
            float scale = node.Scale.DrawnAhead(ahead);
            float rotation = node.Rotation.DrawnAhead(ahead);
            var transition = node.Transition;
            if (transition != null)
            {
                offset += transition.Offset.DrawnAhead(ahead);
                scale *= transition.Scale.DrawnAhead(ahead);
            }
            size = frame.size;
            var local = Affine2.Translate(frame.position + offset) * Affine2.AroundPivot(frame.size * 0.5f, scale, rotation);
            if (parent == null) return local;
            var parentWorld = WorldAhead(parent, ahead, out _);
            var scroller = parent.Scroller;
            if (scroller != null && scroller.isActiveAndEnabled)
                parentWorld = parentWorld * Affine2.Translate(scroller.ClearShift - scroller.Offset.DrawnAhead(ahead));
            return parentWorld * local;
        }

        /// <summary>
        /// Where <paramref name="node"/> is drawn now, in its root's points, as compositing works it out from the values it
        /// is drawn with now (not as the last frame drew it, so wherever it comes in the tree), and whether it is shown:
        /// for measuring one node against another while they are composited (a scroll view keeping clear of a bar).
        /// </summary>
        internal bool DrawnNow(UINode node, out Rect bounds)
        {
            bounds = MatchedGeometry.Bounds(WorldAhead(node, 0f, out var size), size);
            for (var n = node; n != null; n = n == RootNode ? null : n.PresentationLevel.Peek != 0 ? RootNode : n.ParentNode)
            {
                if (n.Display.Peek != DisplayMode.Visible && !n.Leaving.Peek) return false;
            }
            return true;
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
            Array.Resize(ref Opacities, size);
            Array.Resize(ref GroupOpacities, size);
            Array.Resize(ref LocalOpacities, size);
            Array.Resize(ref Blurs, size);
            Array.Resize(ref _clipChains, size);
            Array.Resize(ref _scrolled, size);
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
