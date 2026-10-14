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
        // Per node, whether it is scrolled out from under a clip over it (IsOffscreen): none of what it paints is in view, so
        // it is not drawn, not drawn again until it comes back, and not hit.
        public bool[] Offscreen = new bool[32];

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

        // Nodes whose layout inputs changed since layout last ran, and nodes whose content (what they measure) did; whether
        // layout was asked to run again for another reason (the tree's nodes changed, or something it read did), when every
        // node is solved again; and whether a node it was asked by is asking now, telling it why.
        private readonly List<UINode> _layoutChanged = new();
        private readonly List<UINode> _contentChanged = new();
        private bool _layoutAll = true;
        private bool _telling;

        // The root's size and safe area as last laid out; and per node, the layout pass that found it (or something
        // inside it) changed, and one that found its fit unusable at the width it was given (see Gather).
        private Vector2 _laidOutSize;
        private Insets _laidOutSafeArea;
        private int[] _holdsChangePass = new int[32];
        private int[] _expandPass = new int[32];

        // Per node, whether it is, or holds, a node whose place hangs on something outside it, or something outside on it,
        // which the solver cannot take whole; the presented nodes (each solved on its own) and those avoiding the keyboard;
        // and whether these need finding again (FindReaching).
        private bool[] _reaching = new bool[32];
        private readonly List<int> _presented = new();
        private readonly List<int> _avoiding = new();
        private bool _reachingDirty = true;

        // The nodes a solve took whole that came out a different size, whose insides are laid out again on their own.
        private readonly List<int> _within = new();

        // How many nodes the last layout pass gave the solver (one taken whole counting as one).
        internal int LaidOutLastPass;

        // Every node before the tree's nodes were collected again, to tell which went out of it.
        private readonly List<UINode> _collected = new();

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

        /// <summary>Lays every node out again, the next time layout runs.</summary>
        public void InvalidateLayout()
        {
            _layoutAll = true;
            _layout.Invalidate();
        }

        /// <summary>Composites every node again.</summary>
        public void InvalidateComposite()
        {
            _compositeAll = true;
            _composite.Invalidate();
        }

        /// <summary>
        /// <paramref name="node"/>'s layout inputs may have changed (a value it reads for them did): the tree is laid out
        /// again, reading them again first.
        /// </summary>
        public void LayoutChanged(UINode node)
        {
            _layoutChanged.Add(node);
            Tell();
        }

        /// <summary>
        /// <paramref name="node"/>'s content (what it measures) changed: the tree is laid out again, the node measured
        /// again.
        /// </summary>
        public void ContentChanged(UINode node)
        {
            _contentChanged.Add(node);
            Tell();
        }

        // Asks for layout to run, saying why (LayoutComputation.Invalidated).
        private void Tell()
        {
            _telling = true;
            _layout.Invalidate();
            _telling = false;
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
                node.InLayout = false;
                node.LayoutFitValid = false;
            }
            Nodes.Clear();
        }

        // ── Structure ────────────────────────────────────────────────────────────

        // Collects the nodes under the root in pre-order: enabled nodes, through plain objects, not into disabled nodes
        // or other roots.
        private void Rebuild()
        {
            _structureDirty = false;
            _collected.Clear();
            _collected.AddRange(Nodes);
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
            if (RootNode != null && RootNode.isActiveAndEnabled)
            {
                Add(RootNode, null);
                Collect(RootNode.transform, RootNode);
                SubtreeEnd[0] = Nodes.Count;
                Compositor.MarkOrderDirty();
            }
            // A node no longer in any tree is out of layout: back in, it lands where it goes at once.
            foreach (var node in _collected)
            {
                if (node == null || node.Tree != null) continue;
                node.InLayout = false;
                node.LayoutFitValid = false;
            }
            _collected.Clear();
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
                if (node == null || !Shown[i] || Offscreen[i] || Opacities[i] <= 0.001f) continue;
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
                if (node == null || !Shown[i] || Offscreen[i] || Opacities[i] <= 0.001f || !accepts(node)) continue;
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

        // Lays the tree out: the layout inputs of the nodes whose values changed read again first (once the tree's nodes
        // changed, of any not read yet), then the tree solved, and every presented node in it on its own.
        //
        // Only what can have moved is solved again. A node whose inputs or content changed is solved with every node on
        // the way up to its root (or to the presented node it is in) and their children; each of those children that holds
        // no change is taken whole, fitted as the last solve that had what is inside it found it, and what is inside it
        // stays as it was unless it comes out a different size (then that is laid out again at its new size, on its own).
        // So a label counting up in a header solves the header and the nodes up from it, each with its children, not the
        // feed beside it. Every node is solved again when the tree's nodes, the root's size or safe area, or a node's
        // presentation level changed, or layout was asked to run again by something it read rather than told why.
        private void LayOut()
        {
            bool rebuilt = _structureDirty;
            if (_structureDirty)
                Rebuild();
            int nodeCount = Nodes.Count;
            EnsureCapacity(nodeCount);
            _pass++;
            LaidOutLastPass = 0;
            bool all = _layoutAll || rebuilt;
            _layoutAll = false;
            all |= ReadLayoutInputs(rebuilt);
            for (int i = 0; i < _contentChanged.Count; i++)
            {
                var node = _contentChanged[i];
                if (node != null && node.Tree == this)
                    MarkChanged(node.Index);
            }
            _contentChanged.Clear();
            if (nodeCount == 0) return;

            var rootSize = Root.SizeObservable.Value;
            var safeArea = Root.SafeAreaObservable.Value;
            float keyboard = UIRoot.KeyboardObservable.Value / Root.PixelsPerPoint;
            all |= rootSize != _laidOutSize || safeArea != _laidOutSafeArea;
            _laidOutSize = rootSize;
            _laidOutSafeArea = safeArea;
            bool keyboardMoved = keyboard != _keyboard;
            _keyboard = keyboard;

            // The rest of what layout works from it is told of (a node's inputs, its content), and what it reads as it
            // solves (what it measures, what laying a node out sets off) it does not track: a pass that does not reach a
            // node would let go of what that node's measuring read.
            var tracking = Computation.Current;
            Computation.Current = null;
            try
            {
                if (all || _reachingDirty)
                    FindReaching();
                // A node avoiding the keyboard pads itself by where it was drawn against it, which moves as the keyboard
                // does.
                if (keyboardMoved || _keyboard > 0f)
                {
                    foreach (int i in _avoiding)
                        MarkChanged(i);
                }

                // The tree, then every presented node in it on its own: a presented screen is laid out as a root is,
                // covering the root whatever its place in the hierarchy, its padding kept clear of the safe area.
                LayOutRegion(0, rootSize, safeArea, all);
                foreach (int i in _presented)
                {
                    if (Nodes[i].LaidOutFrom.Display != DisplayMode.None)
                        LayOutRegion(i, rootSize, safeArea, all);
                    else
                        LeaveLayout(i);
                }
                if (UIDebug.VerifyLayout && !all)
                    Verify(rootSize, safeArea);
            }
            finally
            {
                Computation.Current = tracking;
            }
        }

        // Reads again the layout inputs of the nodes whose values changed, which said so (LayoutChanged); or, once the
        // tree's nodes changed, of every node whose inputs are not read since they changed (one just come into the tree).
        // Whether a node's presentation level changed, which moves what is in it between the solves.
        private bool ReadLayoutInputs(bool all)
        {
            using var read = UIProfiling.LayoutInputs.Auto();
            int count = 0;
            bool presentation = false;
            if (all)
            {
                for (int i = 0; i < Nodes.Count; i++)
                    count += Read(Nodes[i], ref presentation);
            }
            else
            {
                for (int i = 0; i < _layoutChanged.Count; i++)
                {
                    var node = _layoutChanged[i];
                    if (node != null && node.Tree == this)
                        count += Read(node, ref presentation);
                }
            }
            _layoutChanged.Clear();
            UIProfiling.LayoutInputsRead.Value += count;
            return presentation;
        }

        // Reads a node's layout inputs again if they may have changed, marking it changed if they did; 1 if it read them.
        private int Read(UINode node, ref bool presentation)
        {
            var computation = node.LayoutComputation;
            if (computation == null || !computation.Dirty) return 0;
            var before = node.LaidOutFrom;
            computation.Run();
            ref readonly var after = ref node.LaidOutFrom;
            if (!LayoutInputs.Same(in before, in after))
            {
                MarkChanged(node.Index);
                presentation |= before.PresentationLevel != after.PresentationLevel;
                _reachingDirty |= !LayoutInputs.SameReach(in before, in after);
            }
            return 1;
        }

        // Marks node `index` and every node up from it to its solve's root (the root, or the presented node it is in) as
        // holding a change this pass, so the solver goes down to it.
        private void MarkChanged(int index)
        {
            for (int i = index; _holdsChangePass[i] != _pass;)
            {
                _holdsChangePass[i] = _pass;
                var node = Nodes[i];
                if (i == 0 || node.LaidOutFrom.PresentationLevel != 0) break;
                i = node.ParentNode.Index;
            }
        }

        // Which nodes the solver cannot take whole (_reaching): any whose place, or the place of something inside it,
        // hangs on something outside it, or the place of something outside on it: floating against the root or an
        // element, the elements floated against, reaching out past the safe area, or avoiding the keyboard (its padding
        // follows where it was drawn). And which nodes are presented (each solved on its own) and which avoid the keyboard.
        private void FindReaching()
        {
            _reachingDirty = false;
            _presented.Clear();
            _avoiding.Clear();
            int count = Nodes.Count;
            Array.Clear(_reaching, 0, count);
            for (int i = 0; i < count; i++)
            {
                ref readonly var inputs = ref Nodes[i].LaidOutFrom;
                var floating = inputs.Floating;
                if (i > 0 && inputs.PresentationLevel != 0)
                    _presented.Add(i);
                if (inputs.AvoidsKeyboard)
                    _avoiding.Add(i);
                if (floating.AttachTo == FloatingAttach.Element && floating.Element != null && floating.Element.Tree == this)
                    _reaching[floating.Element.Index] = true;
                if (inputs.Ignores != Edges.None || inputs.AvoidsKeyboard || floating.AttachTo == FloatingAttach.Root
                    || floating.AttachTo == FloatingAttach.Element)
                    _reaching[i] = true;
            }
            // Up the tree: pre-order puts every node after its parent, so going backwards reaches each before its parent.
            for (int i = count - 1; i > 0; i--)
            {
                if (_reaching[i])
                    _reaching[Nodes[i].ParentNode.Index] = true;
            }
        }

        // How far up the root the software keyboard reaches, in its points, for this layout.
        private float _keyboard;

        // A node avoiding the keyboard keeps what is inside it clear of it: its bottom padding is at least how far the
        // keyboard reaches into it, by where its bottom was drawn last frame.
        private Insets AvoidKeyboard(UINode node, Insets padding)
        {
            if (!node.LaidOutFrom.AvoidsKeyboard || _keyboard <= 0f) return padding;
            float below = Mathf.Max(0f, Root.Size.y - node.DrawnRect.yMax);
            padding.Bottom = Mathf.Max(padding.Bottom, _keyboard - below);
            return padding;
        }

        // Lays out node `start` (the root, or a presented node) and what is inside it over the root's rect, as a tree of
        // its own: every node in it, or only what can have moved (nothing at all when it holds no change), then what is
        // inside each node taken whole that came out a different size, at its new size.
        private void LayOutRegion(int start, Vector2 rootSize, Insets safeArea, bool all)
        {
            if (!all && _holdsChangePass[start] != _pass)
            {
                // As it was; a presented node's RectTransform follows where it is in the hierarchy all the same.
                if (start > 0)
                    WriteRect(Nodes[start], new Rect(-RootPosition(Nodes[start].ParentNode), rootSize));
                return;
            }
            Solve(start, rootSize, safeArea, all, own: true);
            for (int w = 0; w < _within.Count; w++)
            {
                int index = _within[w];
                Solve(index, Nodes[index].Frame.Model.size, default, all: false, own: false);
            }
            _within.Clear();
        }

        // Solves node `start` and what is inside it as a tree of its own `size` big, keeping its content clear of
        // `safeArea`, and gives each node the solve laid out its rect: the region's root too when it is the solve's own
        // (`own`), while a node laid out within has its rect from the solve that took it whole.
        private void Solve(int start, Vector2 size, Insets safeArea, bool all, bool own)
        {
            int count;
            while (true)
            {
                count = Gather(start, all);
                using (UIProfiling.Solve.Auto())
                    LayoutSolver.SolveWidths(_solver, count, size, safeArea);
                // A node taken whole that came out a different width would be fitted in height at a width it was not
                // found at: it is solved from inside instead, the widths again with it.
                if (!ExpandResized(count)) break;
            }
            using (UIProfiling.Solve.Auto())
                LayoutSolver.SolveHeights(_solver, count, size, safeArea);
            Apply(start, count, size, own);
        }

        // Gathers node `start` and what is inside it into the solver's array, pre-order, leaving out what is not laid out
        // (Display None, with everything inside it, which goes out of layout) and the presented nodes in it (laid out on
        // their own). Unless every node is solved, a node with children that holds no change, whose fit is known (at the
        // width it had, which ExpandResized holds it to), and whose place, and that of what is inside it, hangs on nothing
        // outside it, goes in whole (Collapsed), without what is inside it. Returns how many went in.
        private int Gather(int start, bool all)
        {
            using var gather = UIProfiling.Gather.Auto();
            int end = SubtreeEnd[start];
            int count = 0;
            for (int i = start; i < end; i++)
            {
                var node = Nodes[i];
                ref readonly var inputs = ref node.LaidOutFrom;
                int parentIndex = -1;
                if (i != start)
                {
                    // What is skipped is skipped whole, so the parent of every node reached went in.
                    parentIndex = _solverIndex[node.ParentNode.Index];
                    if (inputs.PresentationLevel != 0)
                    {
                        i = SubtreeEnd[i] - 1;
                        continue;
                    }
                    if (inputs.Display == DisplayMode.None)
                    {
                        LeaveLayout(i);
                        i = SubtreeEnd[i] - 1;
                        continue;
                    }
                }

                bool whole = !all && i != start && SubtreeEnd[i] > i + 1 && node.LayoutFitValid && _holdsChangePass[i] != _pass
                    && _expandPass[i] != _pass && !_reaching[i];
                _solverIndex[i] = count;
                _solverToNode[count] = i;
                _lastChild[count] = -1;
                ref var s = ref _solver[count];
                s = new SolverNode
                {
                    Parent = parentIndex,
                    FirstChild = -1,
                    NextSibling = -1,
                    Width = inputs.Width,
                    Height = inputs.Height,
                    Padding = AvoidKeyboard(node, inputs.Padding),
                    ChildGap = inputs.Gap,
                    Direction = inputs.Direction,
                    Wrap = inputs.Wrap,
                    AlignX = inputs.AlignX,
                    AlignY = inputs.AlignY,
                    AspectRatio = inputs.AspectRatio,
                    Floating = i == start ? default : inputs.Floating,
                    Element = -1,
                    Ignores = inputs.Ignores,
                    Scroll = inputs.Scroll,
                    Content = node.Content,
                    Node = node,
                    Collapsed = whole,
                    Fitted = whole ? node.LayoutFit : default,
                };
                if (parentIndex >= 0)
                {
                    int last = _lastChild[parentIndex];
                    if (last < 0) _solver[parentIndex].FirstChild = count;
                    else _solver[last].NextSibling = count;
                    _lastChild[parentIndex] = count;
                }
                count++;
                if (whole)
                    i = SubtreeEnd[i] - 1;
            }

            // Floating against an element: the element's solver index, or against the parent when it is not laid out in
            // this solve.
            for (int k = 1; k < count; k++)
            {
                ref var s = ref _solver[k];
                if (s.Floating.AttachTo != FloatingAttach.Element) continue;
                int element = InSolve(s.Floating.Element, start, count);
                if (element >= 0)
                    s.Element = element;
                else
                    s.Floating.AttachTo = FloatingAttach.Parent;
            }
            return count;
        }

        // Where a node is in the solve of the `count` nodes from `start` just gathered, or -1 when it is not in it (a
        // skipped node's index in the solver is a solve before's).
        private int InSolve(UINode node, int start, int count)
        {
            if (node == null || node.Tree != this || node.Index < start || node.Index >= SubtreeEnd[start]) return -1;
            int k = _solverIndex[node.Index];
            return k >= 0 && k < count && _solverToNode[k] == node.Index ? k : -1;
        }

        // Node `i` went out of layout (Display None), with everything inside it but what is presented (in layout on its
        // own): back in, each lands where it goes at once. Once it is out, so is all that.
        private void LeaveLayout(int i)
        {
            if (!Nodes[i].InLayout) return;
            int end = SubtreeEnd[i];
            for (int j = i; j < end; j++)
            {
                var node = Nodes[j];
                if (j > i && node.LaidOutFrom.PresentationLevel != 0)
                {
                    j = SubtreeEnd[j] - 1;
                    continue;
                }
                node.InLayout = false;
                node.LayoutFitValid = false;
            }
        }

        // Marks each node taken whole that came out a different width from the one its fit was found at to be solved from
        // inside: whether any was.
        private bool ExpandResized(int count)
        {
            bool any = false;
            for (int k = 1; k < count; k++)
            {
                ref var s = ref _solver[k];
                if (!s.Collapsed || s.Size.x == s.Node.LayoutFitWidth) continue;
                _expandPass[_solverToNode[k]] = _pass;
                any = true;
            }
            return any;
        }

        // Gives each node the solve laid out its rect, and keeps its fit for a solve that takes it whole. A node taken
        // whole keeps what is inside it as it was, unless it came out a different size (_within).
        private void Apply(int start, int count, Vector2 size, bool own)
        {
            using var apply = UIProfiling.Apply.Auto();
            UIProfiling.LaidOut.Value += count;
            LaidOutLastPass += count;
            for (int k = 0; k < count; k++)
            {
                var node = Nodes[_solverToNode[k]];
                ref var s = ref _solver[k];
                if (k == 0 && !own)
                {
                    // Laid out within: what it holds runs as far as this solve says.
                    if (node.Scroller != null)
                        node.Scroller.SetExtent(size, s.ContentSize, default);
                    continue;
                }
                var rect = k == 0 ? new Rect(0f, 0f, size.x, size.y) : s.Rect;
                var was = node.Frame.Model.size;
                // In layout before, it carries on from where it is drawn (springing inside a change), unless it lands at
                // once; coming into layout, it is where it goes at once, and whatever brings it in starts from there.
                bool carriesOn = node.InLayout;
                node.InLayout = true;
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
                    WriteRect(node, rect);
                else if (start > 0)
                    WriteRect(node, new Rect(-RootPosition(node.ParentNode), size));
                if (s.Collapsed)
                {
                    if (rect.size != was)
                        _within.Add(_solverToNode[k]);
                    continue;
                }
                if (k > 0)
                {
                    node.LayoutFit = s.Fitted;
                    node.LayoutFitWidth = s.Size.x - s.Reach.Horizontal;
                    node.LayoutFitValid = true;
                }
                if (node.Scroller != null)
                    node.Scroller.SetExtent(s.Rect.size, s.ContentSize, s.Reach);
            }
        }

        /// <summary>
        /// Nodes a pass that solved only what can have moved laid out somewhere other than solving every node puts them,
        /// since the tree was made (counted while <see cref="UIDebug.VerifyLayout"/> is on).
        /// </summary>
        internal static int LayoutMismatches;

        // Checks the pass against solving every node again, giving no node anything: each node in layout must already be
        // where that puts it.
        private void Verify(Vector2 rootSize, Insets safeArea)
        {
            VerifyRegion(0, rootSize, safeArea);
            foreach (int i in _presented)
            {
                if (Nodes[i].LaidOutFrom.Display != DisplayMode.None)
                    VerifyRegion(i, rootSize, safeArea);
            }
        }

        private void VerifyRegion(int start, Vector2 rootSize, Insets safeArea)
        {
            int count = Gather(start, all: true);
            LayoutSolver.Solve(_solver, count, rootSize, safeArea);
            for (int k = 1; k < count; k++)
            {
                var node = Nodes[_solverToNode[k]];
                Rect laidOut = node.Frame.Model, solved = _solver[k].Rect;
                if (node.InLayout && Mathf.Abs(laidOut.x - solved.x) < 1e-3f && Mathf.Abs(laidOut.y - solved.y) < 1e-3f
                    && Mathf.Abs(laidOut.width - solved.width) < 1e-3f && Mathf.Abs(laidOut.height - solved.height) < 1e-3f)
                    continue;
                if (++LayoutMismatches <= 20)
                    Debug.LogError($"UI Next: {node.name} is laid out at {laidOut}{(node.InLayout ? "" : ", out of layout")}, where solving every node puts it at {solved}.", node);
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

        // The model on a node's RectTransform, unless it has it already: asking a RectTransform is a call into the engine.
        private static void WriteRect(UINode node, Rect rect)
        {
            if (node.RectWritten && node.WrittenRect == rect) return;
            node.RectWritten = true;
            node.WrittenRect = rect;
            WriteRectTransform(node.RectTransform, rect);
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
            // Its clips: its parent's, and its parent's own clip; none for a node in the top layer.
            var chain = new Vector4(-1f, -1f, -1f, -1f);
            if (p >= 0 && !topLayer)
            {
                chain = _clipChains[p];
                if (parent.Composited.Clips && _clipIndex[p] >= 0)
                    chain = Append(chain, _clipIndex[p]);
            }
            _clipChains[i] = chain;
            bool offscreen = IsOffscreen(node, in world, frame.size, chain);
            Offscreen[i] = offscreen;
            // Seen and in view, with its drawing waiting for that: it is drawn now, before the compositor builds.
            if (node.DrawWaiting && shown && opacities > 0f && !offscreen)
                node.DrawComputation?.Invalidate();
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
                ClipShapes[clip] = ClipShape.Of(world.Inverse, frame.size, inputs.ClipRadii);
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

        // Whether a node is scrolled out from under an upright clip over it: what it paints, taken into its root's space, lies
        // wholly outside the clip's rect. What it paints is its rect and what its own drawing reaches past it, as its draw
        // computation last found it (its rect alone until it has drawn); a turned node is taken by the box round its corners,
        // so this errs on the side of drawing. A clip that is not upright never culls, and what is inside a node is tested
        // on its own, as it may overflow it. Nodes in the top layer or presented have no clips, so are never offscreen.
        private bool IsOffscreen(UINode node, in Affine2 world, Vector2 size, Vector4 chain)
        {
            if (chain.x < 0f) return false;
            var reach = node.PaintReach;
            float x0 = -reach.x, y0 = -reach.y, x1 = size.x + reach.z, y1 = size.y + reach.w;
            if (node.PaintsOutright)
            {
                var union = node.PaintUnion;
                x0 = Mathf.Min(x0, union.xMin);
                y0 = Mathf.Min(y0, union.yMin);
                x1 = Mathf.Max(x1, union.xMax);
                y1 = Mathf.Max(y1, union.yMax);
            }
            var a = world.Apply(new Vector2(x0, y0));
            var b = world.Apply(new Vector2(x1, y0));
            var c = world.Apply(new Vector2(x1, y1));
            var d = world.Apply(new Vector2(x0, y1));
            float rx0 = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)), rx1 = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float ry0 = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)), ry1 = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            for (int k = 0; k < 4; k++)
            {
                int index = (int)chain[k];
                if (index < 0) break;
                if (index >= ClipShapes.Count) continue;
                var clip = ClipShapes[index];
                if (Mathf.Abs(clip.Inverse0.y) > 1e-6f || Mathf.Abs(clip.Inverse1.x) > 1e-6f
                    || Mathf.Abs(clip.Inverse0.x) < 1e-6f || Mathf.Abs(clip.Inverse1.y) < 1e-6f) continue;
                // The clip's rect in the root's space: its own runs from 0 to its size, and its inverse takes the root's
                // into it, so the root's runs from -shift / scale to (size - shift) / scale.
                float cx0 = -clip.Inverse0.z / clip.Inverse0.x, cx1 = (clip.Size.x - clip.Inverse0.z) / clip.Inverse0.x;
                float cy0 = -clip.Inverse1.z / clip.Inverse1.y, cy1 = (clip.Size.y - clip.Inverse1.z) / clip.Inverse1.y;
                if (rx1 < Mathf.Min(cx0, cx1) || rx0 > Mathf.Max(cx0, cx1) || ry1 < Mathf.Min(cy0, cy1) || ry0 > Mathf.Max(cy0, cy1))
                    return true;
            }
            return false;
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
            Array.Resize(ref Offscreen, size);
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
            Array.Resize(ref _holdsChangePass, size);
            Array.Resize(ref _expandPass, size);
            Array.Resize(ref _reaching, size);
        }

        private sealed class LayoutComputation : Computation
        {
            private readonly UITree _tree;

            public LayoutComputation(UITree tree) : base(Phase.Layout) => _tree = tree;

            protected override string ProfilerName => "Tree.LayOut";

            // Asked to run again by something it read (the root's size or safe area, the keyboard, anything else laying
            // out read), rather than told why by a node (Tell): every node is laid out.
            private protected override void Invalidated()
            {
                if (!_tree._telling)
                    _tree._layoutAll = true;
                base.Invalidated();
            }

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
