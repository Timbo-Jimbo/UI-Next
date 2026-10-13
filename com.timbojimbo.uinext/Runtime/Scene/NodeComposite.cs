using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// What compositing works from for one node, on its own: its drawn values as they are drawn (its frame, offset, scale,
    /// turn and opacity, with its transition's), whether it is shown and how, its colour filters, whether it is a composite
    /// group, its clip, and how far its scrolling moves what it holds. Its tree composites it, and everything inside it,
    /// from these and its parent's.
    /// </summary>
    internal struct CompositeInputs
    {
        public Rect Frame;
        public Vector2 Offset;
        public float Scale;
        public float Rotation;
        public float Opacity;
        public float Saturation;
        public float Brightness;
        public float Blur;
        public Texture Mask;
        public Vector4 ClipRadii;
        public Vector2 Scrolled;
        public int ZIndex;
        public bool Presented;
        public bool Visible;
        public bool Lifted;
        public bool FadingIn;
        public bool Composite;
        public bool Clips;
    }

    /// <summary>
    /// A node's compositing inputs: reads its values into <see cref="UINode.Composited"/>, again whenever one it read
    /// changes. It is not queued to run: invalidated, it tells its tree, which runs it as it composites the node, in tree
    /// order, so a node's inputs are read once a frame however many of its values changed, and only the nodes that changed
    /// (with everything inside them) are composited again.
    /// </summary>
    internal sealed class NodeComposite : Computation
    {
        private readonly UINode _node;

        public NodeComposite(UINode node) : base(Phase.Draw) => _node = node;

        protected override string ProfilerName => "Node.Composite";

        private protected override void Invalidated() => _node.Tree?.CompositeChanged(_node);

        protected override void Execute()
        {
            var node = _node;
            ref var inputs = ref node.Composited;
            inputs.Presented = node.PresentationLevel.Value != 0;
            inputs.Frame = node.Frame.Value;
            var offset = node.Offset.Value;
            float scale = node.Scale.Value;
            float opacity = Mathf.Clamp01(node.Opacity.Value);
            bool fadingIn = false;
            var transition = node.Transition;
            if (transition != null)
            {
                offset += transition.Offset.Value;
                scale *= transition.Scale.Value;
                float faded = transition.Opacity.Value;
                opacity *= Mathf.Clamp01(faded);
                // A node a transition is fading (a page, an alert, an item going away) fades as a group, so its parts
                // never show through each other on the way.
                fadingIn = faded < 0.999f;
            }
            inputs.Offset = offset;
            inputs.Scale = scale;
            inputs.Rotation = node.Rotation.Value;
            inputs.Opacity = opacity;
            inputs.FadingIn = fadingIn;
            inputs.Visible = node.Display.Value == DisplayMode.Visible || node.Leaving.Value;
            inputs.Lifted = node.TopLayer.Value;
            inputs.ZIndex = node.ZIndex.Value;
            // Colour filters multiply down the tree, as opacity does, and every pixel drawn applies them.
            inputs.Saturation = Mathf.Max(0f, node.Saturation.Value);
            inputs.Brightness = Mathf.Max(0f, node.Brightness.Value);
            // Blur and masks apply only to a composite group.
            bool composite = node.CompositeGroup.Value;
            inputs.Composite = composite;
            inputs.Blur = composite ? Mathf.Max(0f, node.Blur.Value) : 0f;
            inputs.Mask = composite ? node.Mask.Value : null;
            if (!composite && !node.WarnedOutsideGroup && (node.Blur.Value > 0.01f || node.Mask.Value != null))
            {
                node.WarnedOutsideGroup = true;
                Debug.LogWarning($"UI Next: {node.name} has a blur or a mask, which apply only to a composite group; turn on its Composite Group.", node);
            }
            // A scroll view clips what it holds, and moves it: drawn from under its top and left clear insets (which hang
            // on where the nodes it keeps clear of are drawn), less its offset.
            var scroller = node.Scroller;
            bool scrolls = scroller != null && scroller.isActiveAndEnabled;
            inputs.Clips = node.Clips.Value || scrolls;
            inputs.ClipRadii = inputs.Clips ? node.ClipRadii.Value.ToVector() : default;
            if (scrolls)
                scroller.TrackClearNodes();
            inputs.Scrolled = scrolls ? scroller.ClearShift - scroller.Offset.Value : Vector2.zero;
        }
    }
}
