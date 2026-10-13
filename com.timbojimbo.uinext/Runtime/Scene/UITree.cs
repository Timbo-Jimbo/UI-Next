using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// One root's nodes as flat arrays in pre-order, with the two computations that work on them: layout (which reads
    /// every node's layout values as they are going, solves, and gives each node its frame) and compositing (which works
    /// out where each node is drawn in the root's space, how opaque, and what clips it). Layout runs again when anything
    /// it read changes. Compositing works from each node's inputs, read by a computation of the node's own
    /// (<see cref="NodeComposite"/>), and composites again only the nodes whose inputs changed, with everything inside
    /// them: a scroll view scrolling composites what it holds, not the rest of the tree.
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

        // The clips nodes give what is inside them, each clipping node's at the index it keeps (-1 for none) until the
        // tree's nodes change; and each node's chain of the clips that cut it.
        public readonly List<ClipShape> ClipShapes = new();
        private int[] _clipIndex = new int[32];
        private Vector4[] _clipChains = new Vector4[32];

        // What compositing changed since the compositor last took it, as ranges of indices: slots and clip shapes. And where
        // the compositor last put them in its buffers, and how many there were then.
        internal int ChangedSlotsFrom = int.MaxValue, ChangedSlotsTo;
        internal int ChangedClipsFrom = int.MaxValue, ChangedClipsTo;
        internal int CopiedSlotBase = -1, CopiedClipBase = -1, CopiedSlots = -1, CopiedClips = -1;

        // Nodes whose compositing inputs changed since compositing last ran; per node, whether they did; whether every node
        // is composited next (the tree's nodes changed); and the root's size and scale as last composited.
        private readonly List<UINode> _changed = new();
        private bool[] _inputsChanged = new bool[32];
        private bool _compositeAll = true;
        private Vector2 _compositedSize;
        private float _compositedPixels;

        // Per node, how far what it holds is moved in its own space by its scrolling: drawn from under its top and left
        // clear insets, less its offset (nothing for a node that does not scroll).
        private Vector2[] _scrolled = new Vector2[32];

        // Per node, as compositing last found them, what else the draw order hangs on: whether it is inside a group, the
        // mask its group is cut by, its z-index, and whether it is lifted into the top layer.
        private bool[] _inGroup = new bool[32];
        private Texture[] _masks = new Texture[32];
        private int[] _zIndices = new int[32];
        private bool[] _lifted = new bool[32];

        // The nodes shown, in the order they are drawn (the compositor's), which hit testing walks backwards; and each
        // presentation level drawn (its root's node, then each presented node, lowest first), where it starts in it, and
        // whether it takes pointers.
        public readonly List<int> DrawOrder = new();
        public readonly List<UINode> Levels = new();
        public readonly List<int> LevelStarts = new();
        public readonly List<bool> LevelInteractive = new();

        // Every presentation level as the compositor last made the draw order (drawn or not), which of them it drew from
        // (the topmost cover in place, which hides what is below it), and whether a presented screen or what is on it has
        // moved or faded since: whether that cover is in place may have changed.
        internal readonly List<UINode> AllLevels = new();
        internal int CoverFrom;
        internal bool CoverCheck;

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
            _compositeAll = true;
            _layout.Invalidate();
            _composite.Invalidate();
            Compositor.MarkOrderDirty();
        }

        public void InvalidateLayout() => _layout.Invalidate();

        /// <summary>Composites every node again.</summary>
        public void InvalidateComposite()
        {
            _compositeAll = true;
            _composite.Invalidate();
        }

        /// <summary>
        /// <paramref name="node"/>'s compositing inputs may have changed (a value it reads for them did): it is composited
        /// again, with everything inside it, the next time compositing runs.
        /// </summary>
        public void CompositeChanged(UINode node)
        {
            _changed.Add(node);
            _composite.Invalidate();
        }

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
            Release(ref _tracker);
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
            Release(ref _tracker);
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
                Drive(ref _tracker, Root, node.RectTransform, DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta
                    | DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot);
        }

        /// <summary>
        /// Tells a tracker it drives a RectTransform's <paramref name="properties"/>, recording nothing for undo. In the
        /// editor a tracker records what it is told it drives, so the values layout then writes there would mark its
        /// scene as changed (a script reload laying a scene out again would leave it unsaved); they are layout's, not
        /// changes of the user's.
        /// </summary>
        internal static void Drive(ref DrivenRectTransformTracker tracker, UnityEngine.Object driver, RectTransform rect,
            DrivenTransformProperties properties)
        {
            RecordUndo(false);
            try { tracker.Add(driver, rect, properties); }
            finally { RecordUndo(true); }
        }

        /// <summary>Lets go of all a tracker drives, recording nothing for undo (see <see cref="Drive"/>).</summary>
        internal static void Release(ref DrivenRectTransformTracker tracker)
        {
            RecordUndo(false);
            try { tracker.Clear(); }
            finally { RecordUndo(true); }
        }

        // Whether trackers record what they drive for undo; undo is the editor's, so in a player there is nothing to stop.
        private static void RecordUndo(bool record)
        {
#if UNITY_EDITOR
            if (record)
                DrivenRectTransformTracker.StartRecordingUndo();
            else
                DrivenRectTransformTracker.StopRecordingUndo();
#endif
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
            var gather = UIProfiling.Gather.Auto();
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
            gather.Dispose();

            using (UIProfiling.Solve.Auto())
                LayoutSolver.Solve(_solver, count, rootSize, safeArea);

            using var apply = UIProfiling.Apply.Auto();
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

        // Composites what changed since it last ran: each node whose inputs changed is worked out again, with everything
        // inside it, from its inputs (read again first, as its own computation is run here, in tree order) and its parent's.
        // Once the tree's nodes changed, or its root's size or scale, every node is, and its clips are numbered afresh;
        // still, only nodes whose values changed read them again.
        private void Composite()
        {
            int nodeCount = Nodes.Count;
            EnsureCapacity(nodeCount);
            var rootSize = Root.SizeObservable.Value;
            float pixels = Root.PixelsPerPoint;
            bool all = _compositeAll || rootSize != _compositedSize || pixels != _compositedPixels;
            if (all)
            {
                // A pass that composites every node: the nodes it drew carry its number until the tree's nodes change.
                _compositeAll = false;
                _compositedSize = rootSize;
                _compositedPixels = pixels;
                CompositePass = ++s_compositePasses;
                ClipShapes.Clear();
                for (int i = 0; i < nodeCount; i++)
                    _clipIndex[i] = -1;
            }
            foreach (var changed in _changed)
            {
                if (changed != null && changed.Tree == this && changed.Index >= 0 && changed.Index < nodeCount)
                    _inputsChanged[changed.Index] = true;
            }
            _changed.Clear();

            // Whether what changed changes what is drawn or in what order, not only where nodes are drawn and how opaque.
            bool order = false;
            int composited = 0, read = 0;
            // Nodes before this one are inside a node composited again, so they are too.
            int until = all ? nodeCount : 0;
            for (int i = 0; i < nodeCount; i++)
            {
                if (_inputsChanged[i])
                {
                    _inputsChanged[i] = false;
                    until = Mathf.Max(until, SubtreeEnd[i]);
                }
                if (i >= until) continue;
                var node = Nodes[i];
                var inputs = node.CompositeComputation;
                if (inputs != null && inputs.Dirty)
                {
                    inputs.Run();
                    read++;
                }
                order |= CompositeNode(i, node, rootSize, pixels);
                composited++;
            }
            UIProfiling.Composited.Value += composited;
            UIProfiling.InputsRead.Value += read;
            if (composited > 0)
                Compositor.MarkSlotsDirty();
            if (order)
                Compositor.MarkOrderDirty();
        }

        // Works out node `i` from its inputs and its parent's: where it is drawn in the root's space, how opaque, whether it
        // is shown, whether it is drawn as a group, what clips it, and its own clip, which cuts what is inside it. True if
        // what changed changes what is drawn or in what order, so the draw order must be made again.
        private bool CompositeNode(int i, UINode node, Vector2 rootSize, float pixels)
        {
            ref readonly var inputs = ref node.Composited;
            // A presented node hangs off the root, whatever its place in the hierarchy.
            bool presented = i > 0 && inputs.Presented;
            var parent = presented ? RootNode : node.ParentNode;
            int p = parent != null ? parent.Index : -1;

            var frame = p < 0 ? new Rect(0f, 0f, rootSize.x, rootSize.y) : inputs.Frame;
            float opacity = inputs.Opacity;
            bool shown = inputs.Visible && (p < 0 || Shown[p]);
            // Lifted into the top layer or presented: out of its ancestors' clips.
            bool topLayer = inputs.Lifted || presented;

            var local = Affine2.Translate(frame.position + inputs.Offset)
                        * Affine2.AroundPivot(frame.size * 0.5f, inputs.Scale, inputs.Rotation);
            var world = p < 0 ? local : World[p] * Affine2.Translate(_scrolled[p]) * local;
            _scrolled[i] = inputs.Scrolled;
            float effectiveOpacity = (p < 0 ? 1f : Slots[p].SizeOpacity.z) * opacity;
            // Colour filters multiply down the tree, as opacity does, and every pixel drawn applies them.
            float saturation = (p < 0 ? 1f : Slots[p].Row0.w) * inputs.Saturation;
            float brightness = (p < 0 ? 1f : Slots[p].Row1.w) * inputs.Brightness;
            float opacities = (p < 0 ? 1f : Opacities[p]) * opacity;
            // A group not fully opaque: what is in it is drawn at its own opacities into the group's image, which is drawn
            // at the group's. A composite group is drawn as one image while not fully opaque, blurred or masked: its image
            // is what is faded, blurred and masked, however opaque it is; so is a node a transition is fading. A node drawn
            // at no opacity at all (faded out, a fade's first frame, a bouncy fade past nothing) is no group: nothing in it
            // is seen, so its image is not made, and what is in it is drawn at nothing (not at the group's 1).
            bool group = shown && effectiveOpacity > 0f
                         && (((inputs.Composite || inputs.FadingIn) && opacity < 0.999f) || inputs.Blur > 0.01f || inputs.Mask != null);
            float groupOpacity = group ? effectiveOpacity : 0f;
            float blur = group ? inputs.Blur : 0f;
            if (group) effectiveOpacity = 1f;

            // The draw order is made again for a change in whether it is seen, whether it is a group or how its group is
            // drawn, its z-index or lift, and as a group or what is in one is drawn somewhere new or fades (a group's image
            // covers what is drawn in it). As a presented screen or what is on it does, the compositor looks again at
            // whether a cover is in place (one hides what is below it only while drawn in place, opaque).
            bool inGroup = p >= 0 && (_inGroup[p] || GroupOpacities[p] > 0f);
            bool moved = !Same(World[i], world) || Slots[i].SizeOpacity.x != frame.width || Slots[i].SizeOpacity.y != frame.height
                         || Opacities[i] != opacities;
            bool order = Shown[i] != shown || (Opacities[i] > 0f) != (opacities > 0f) || GroupOpacities[i] != groupOpacity
                         || Blurs[i] != blur || _masks[i] != inputs.Mask || _zIndices[i] != inputs.ZIndex || _lifted[i] != inputs.Lifted
                         || (moved && (group || inGroup));
            if (moved && (presented || (p > 0 && Nodes[p].Composited.Presented)))
                CoverCheck = true;
            _inGroup[i] = inGroup;
            _masks[i] = inputs.Mask;
            _zIndices[i] = inputs.ZIndex;
            _lifted[i] = inputs.Lifted;

            World[i] = world;
            Shown[i] = shown;
            Opacities[i] = opacities;
            GroupOpacities[i] = groupOpacity;
            Blurs[i] = blur;
            // Seen, with its drawing waiting for that: it is drawn now, before the compositor builds.
            if (node.DrawWaiting && shown && opacities > 0f)
                node.DrawComputation?.Invalidate();

            // Its clips: its parent's, and its parent's own clip; none for a node in the top layer.
            var chain = new Vector4(-1f, -1f, -1f, -1f);
            if (p >= 0 && !topLayer)
            {
                chain = _clipChains[p];
                if (parent.Composited.Clips && _clipIndex[p] >= 0)
                    chain = Append(chain, _clipIndex[p]);
            }
            _clipChains[i] = chain;
            Slots[i] = new NodeSlot
            {
                Row0 = new Vector4(world.A, world.B, world.Tx, saturation),
                Row1 = new Vector4(world.C, world.D, world.Ty, brightness),
                SizeOpacity = new Vector4(frame.width, frame.height, shown ? effectiveOpacity : 0f,
                    1f / Mathf.Max(1e-4f, pixels * world.ScaleFactor)),
                Clips = chain,
            };
            ChangedSlotsFrom = Mathf.Min(ChangedSlotsFrom, i);
            ChangedSlotsTo = Mathf.Max(ChangedSlotsTo, i + 1);

            // Its own clip, which cuts what is inside it: its rect and clip radii, in its own space.
            if (inputs.Clips)
            {
                int clip = _clipIndex[i];
                if (clip < 0)
                {
                    clip = _clipIndex[i] = ClipShapes.Count;
                    ClipShapes.Add(default);
                }
                var inverse = world.Inverse;
                ClipShapes[clip] = new ClipShape
                {
                    Inverse0 = new Vector4(inverse.A, inverse.B, inverse.Tx, 0f),
                    Inverse1 = new Vector4(inverse.C, inverse.D, inverse.Ty, 0f),
                    Size = new Vector4(frame.width, frame.height, 0f, 0f),
                    Radii = inputs.ClipRadii,
                };
                ChangedClipsFrom = Mathf.Min(ChangedClipsFrom, clip);
                ChangedClipsTo = Mathf.Max(ChangedClipsTo, clip + 1);
            }

            LocalOpacities[i] = opacity;
            node.DrawnSlot = Slots[i];
            node.DrawnOpacity = opacity;
            node.DrawnShown = shown;
            node.DrawnPass = CompositePass;
            return order;
        }

        private static bool Same(in Affine2 a, in Affine2 b) =>
            a.A == b.A && a.B == b.B && a.C == b.C && a.D == b.D && a.Tx == b.Tx && a.Ty == b.Ty;

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
            Array.Resize(ref _inGroup, size);
            Array.Resize(ref _masks, size);
            Array.Resize(ref _zIndices, size);
            Array.Resize(ref _lifted, size);
            Array.Resize(ref _clipIndex, size);
            Array.Resize(ref _inputsChanged, size);
        }

        private sealed class LayoutComputation : Computation
        {
            private readonly UITree _tree;

            public LayoutComputation(UITree tree) : base(Phase.Layout) => _tree = tree;

            protected override string ProfilerName => "Tree.LayOut";

            protected override void Execute() => _tree.LayOut();
        }

        private sealed class CompositeComputation : Computation
        {
            private readonly UITree _tree;

            public CompositeComputation(UITree tree) : base(Phase.Draw) => _tree = tree;

            protected override string ProfilerName => "Tree.Composite";

            protected override void Execute() => _tree.Composite();
        }
    }
}
