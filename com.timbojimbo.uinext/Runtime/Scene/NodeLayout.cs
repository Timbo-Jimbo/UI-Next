namespace TimboJimbo.UINext
{
    /// <summary>
    /// What layout works from for one node, on its own: how it is sized, padded and aligned, how it lays out its children,
    /// whether it floats, scrolls or is laid out at all, and its presentation level (a presented node is laid out over its
    /// root, on its own).
    /// </summary>
    internal struct LayoutInputs
    {
        public Sizing Width;
        public Sizing Height;
        public Insets Padding;
        public float Gap;
        public LayoutDirection Direction;
        public Wrap Wrap;
        public AlignX AlignX;
        public AlignY AlignY;
        public float AspectRatio;
        public Floating Floating;
        public DisplayMode Display;
        public Edges Ignores;
        public bool AvoidsKeyboard;
        public ScrollAxis Scroll;
        public int PresentationLevel;

        /// <summary>Whether two read the same: nothing a solve works from differs.</summary>
        public static bool Same(in LayoutInputs a, in LayoutInputs b) =>
            Same(a.Width, b.Width) && Same(a.Height, b.Height) && a.Padding == b.Padding && a.Gap == b.Gap
            && a.Direction == b.Direction && a.Wrap.Mode == b.Wrap.Mode && a.Wrap.Count == b.Wrap.Count
            && a.Wrap.MinSize == b.Wrap.MinSize && a.AlignX == b.AlignX && a.AlignY == b.AlignY && a.AspectRatio == b.AspectRatio
            && SameFloating(a.Floating, b.Floating) && a.Display == b.Display && a.Ignores == b.Ignores
            && a.AvoidsKeyboard == b.AvoidsKeyboard && a.Scroll == b.Scroll && a.PresentationLevel == b.PresentationLevel;

        /// <summary>
        /// Whether two place it the same way against what is outside it: floating against the same thing, reaching past
        /// the safe area on the same edges, avoiding the keyboard alike (what layout cannot take whole: see UITree).
        /// </summary>
        public static bool SameReach(in LayoutInputs a, in LayoutInputs b) =>
            a.Floating.AttachTo == b.Floating.AttachTo && a.Floating.Element == b.Floating.Element && a.Ignores == b.Ignores
            && a.AvoidsKeyboard == b.AvoidsKeyboard;

        private static bool Same(in Sizing a, in Sizing b) => a.Mode == b.Mode && a.Value == b.Value && a.Min == b.Min && a.Max == b.Max;

        private static bool SameFloating(in Floating a, in Floating b) =>
            a.AttachTo == b.AttachTo && a.Element == b.Element && a.Point == b.Point && a.TargetPoint == b.TargetPoint
            && a.Offset == b.Offset;
    }

    /// <summary>
    /// A node's layout inputs: reads its layout values (where they are going) into <see cref="UINode.LaidOutFrom"/>, again
    /// whenever one it read changes. It is not queued to run: invalidated, it tells its tree, which runs it as it lays the
    /// tree out, so laying a tree out reads again only the values of the nodes that changed, not every node's.
    /// </summary>
    internal sealed class NodeLayout : Computation
    {
        private readonly UINode _node;

        public NodeLayout(UINode node) : base(Phase.Layout) => _node = node;

        protected override string ProfilerName => "Node.Layout";

        private protected override void Invalidated() => _node.Tree?.LayoutChanged(_node);

        protected override void Execute()
        {
            var node = _node;
            ref var inputs = ref node.LaidOutFrom;
            inputs.Width = node.Width.Value;
            inputs.Height = node.Height.Value;
            inputs.Padding = node.Padding.Value;
            inputs.Gap = node.Gap.Value;
            inputs.Direction = node.Direction.Value;
            inputs.Wrap = node.Wrap.Value;
            inputs.AlignX = node.AlignX.Value;
            inputs.AlignY = node.AlignY.Value;
            inputs.AspectRatio = node.AspectRatio.Value;
            inputs.Floating = node.Floating.Value;
            inputs.Display = node.Display.Value;
            inputs.Ignores = node.IgnoresSafeArea.Value;
            inputs.AvoidsKeyboard = node.AvoidsKeyboard.Value;
            var scroller = node.Scroller;
            inputs.Scroll = scroller != null && scroller.isActiveAndEnabled ? scroller.Axis.Value : ScrollAxis.None;
            inputs.PresentationLevel = node.PresentationLevel.Value;
        }
    }
}
