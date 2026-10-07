using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// An element of the UI: a rect laid out as Clay lays out elements (fit, grow, fixed or percent, padded, its children
    /// in a row or column, wrapped or not, aligned, or floating against its parent, its root or another node), drawn by
    /// the drawables on its object, and moved, faded, scaled and rotated apart from its layout by its drawn properties.
    /// Every property is a <see cref="UIValue{T}"/>: set it inside <see cref="UI.Animate(UIAnimation, Action)"/> and it
    /// springs there, give it per-state values and it follows the states active on it. Positions are in points, x right
    /// and y down, from the parent's top-left corner.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Node")]
    public sealed class UINode : MonoBehaviour
    {
        // ── Layout ───────────────────────────────────────────────────────────────

        [SerializeField] private UIValue<Sizing> _width = Sizing.Fit();
        [SerializeField] private UIValue<Sizing> _height = Sizing.Fit();
        [SerializeField] private UIValue<Insets> _padding = new Insets();
        [SerializeField] private UIValue<float> _gap = 0f;
        [SerializeField] private UIValue<LayoutDirection> _direction = LayoutDirection.LeftToRight;
        // Qualified: the bare names are the properties below.
        [SerializeField] private UIValue<Wrap> _wrap = TimboJimbo.UINext.Wrap.None;
        [SerializeField] private UIValue<AlignX> _alignX = TimboJimbo.UINext.AlignX.Left;
        [SerializeField] private UIValue<AlignY> _alignY = TimboJimbo.UINext.AlignY.Top;
        [SerializeField] private UIValue<float> _aspectRatio = 0f;
        [SerializeField] private UIValue<Floating> _floating = new Floating();
        [SerializeField] private UIValue<DisplayMode> _display = DisplayMode.Visible;

        [Tooltip("The edges it reaches out to its root's edge on where it lies against the safe area: a backdrop under the notch and home bar, its content staying clear of them.")]
        [SerializeField] private UIValue<Edges> _ignoresSafeArea = Edges.None;

        // ── Drawn ────────────────────────────────────────────────────────────────

        [SerializeField] private UIValue<float> _opacity = 1f;
        [SerializeField] private UIValue<Vector2> _offset = Vector2.zero;
        [SerializeField] private UIValue<float> _scale = 1f;
        [SerializeField] private UIValue<float> _rotation = 0f;
        [SerializeField] private UIValue<int> _zIndex = 0;

        [Tooltip("Its opacity fades it and what is inside it as one image, so overlapping parts do not show through each other.")]
        [SerializeField] private UIValue<bool> _groupOpacity = false;

        [Tooltip("Blurs it and what is inside it, by this many points.")]
        [SerializeField] private UIValue<float> _blur = 0f;

        [Tooltip("How saturated it and what is inside it are drawn: 1 as they are, 0 grey, more than 1 more vivid.")]
        [SerializeField] private UIValue<float> _saturation = 1f;

        [Tooltip("How bright it and what is inside it are drawn: 1 as they are, less dims them.")]
        [SerializeField] private UIValue<float> _brightness = 1f;

        [Tooltip("A texture whose alpha, stretched over its rect, cuts it and what is inside it; nothing is kept outside its rect.")]
        [SerializeField] private UIValue<Texture> _mask = new UIValue<Texture>();

        [Tooltip("Keeps what is inside it above the software keyboard: its bottom padding is at least how far the keyboard reaches into it.")]
        [SerializeField] private UIValue<bool> _avoidsKeyboard = false;

        [Tooltip("Cuts what is inside to its rect, with its corner radii.")]
        [SerializeField] private UIValue<bool> _clips = false;
        [SerializeField] private UIValue<CornerRadii> _clipRadii = new CornerRadii();

        [Tooltip("Draws it above everything else in its screen, out of its ancestors' clips, while it stays laid out where it is.")]
        [SerializeField] private UIValue<bool> _topLayer = false;

        [Tooltip("Whether it takes the pointer. Off, presses go to what is under it.")]
        [SerializeField] private UIValue<bool> _hitTestable = true;

        /// <summary>How wide it is: fit, grow, fixed or percent.</summary>
        public UIValue<Sizing> Width => _width;

        /// <summary>How tall it is: fit, grow, fixed or percent.</summary>
        public UIValue<Sizing> Height => _height;

        /// <summary>Space between its edges and its children or content.</summary>
        public UIValue<Insets> Padding => _padding;

        /// <summary>Space between one child and the next, and between lines when they wrap.</summary>
        public UIValue<float> Gap => _gap;

        /// <summary>Which way it lays its children out.</summary>
        public UIValue<LayoutDirection> Direction => _direction;

        /// <summary>Whether and how its children wrap into lines.</summary>
        public UIValue<Wrap> Wrap => _wrap;

        /// <summary>Where its children sit across its width.</summary>
        public UIValue<AlignX> AlignX => _alignX;

        /// <summary>Where its children sit down its height.</summary>
        public UIValue<AlignY> AlignY => _alignY;

        /// <summary>Width over height: above 0, its height follows its width.</summary>
        public UIValue<float> AspectRatio => _aspectRatio;

        /// <summary>Floats it out of its parent's flow, placed against its parent, its root or another node.</summary>
        public UIValue<Floating> Floating => _floating;

        /// <summary>Visible, Hidden (keeps its space, drawn and hit by nothing) or None (leaves layout).</summary>
        public UIValue<DisplayMode> Display => _display;

        /// <summary>
        /// The edges it reaches out to its root's edge on, where it lies against the safe area, as SwiftUI's
        /// ignoresSafeArea: it grows by as much, its padding with it, so what is inside stays clear.
        /// </summary>
        public UIValue<Edges> IgnoresSafeArea => _ignoresSafeArea;

        /// <summary>How opaque it is drawn, 0 to 1, with everything inside it.</summary>
        public UIValue<float> Opacity => _opacity;

        /// <summary>How far it is drawn from where layout puts it, in points, x right and y down, taking no space.</summary>
        public UIValue<Vector2> Offset => _offset;

        /// <summary>How much bigger it is drawn around its centre, taking no space.</summary>
        public UIValue<float> Scale => _scale;

        /// <summary>How far it is turned around its centre, in degrees clockwise, taking no space.</summary>
        public UIValue<float> Rotation => _rotation;

        /// <summary>Its drawing order among its siblings: higher draws above. Does not affect layout.</summary>
        public UIValue<int> ZIndex => _zIndex;

        /// <summary>
        /// Whether its opacity fades it and everything inside it as one image, as Core Animation's group opacity and
        /// SwiftUI's compositingGroup do: while it is not fully opaque, it is drawn to an image of its own first, so
        /// overlapping children do not show through each other. Fully opaque, it costs nothing.
        /// </summary>
        public UIValue<bool> GroupOpacity => _groupOpacity;

        /// <summary>
        /// Blurs it and everything inside it by this many points, as SwiftUI's blur(radius:) and CSS's filter: blur():
        /// while it is above zero it is drawn to an image of its own first (as a group), blurred, then drawn. It animates,
        /// so a node can blur in and out of focus.
        /// </summary>
        public UIValue<float> Blur => _blur;

        /// <summary>
        /// How saturated it and everything inside it are drawn, as CSS's filter: saturate() and grayscale(): 1 as they
        /// are, 0 grey (a locked item), more than 1 more vivid. It multiplies down the tree and costs nothing: every pixel
        /// drawn applies it, no image is made.
        /// </summary>
        public UIValue<float> Saturation => _saturation;

        /// <summary>How bright it and everything inside it are drawn, as CSS's filter: brightness(): 1 as they are, less dims them. It multiplies down the tree.</summary>
        public UIValue<float> Brightness => _brightness;

        /// <summary>
        /// A texture whose alpha cuts it and everything inside it, stretched over its rect as it is drawn, as CSS's
        /// mask-image: a fade at a scroll view's edges, a shape cut out of a photo. Nothing is kept outside its rect.
        /// While it has one, it is drawn to an image of its own first (as a group), and the mask is applied to that.
        /// </summary>
        public UIValue<Texture> Mask => _mask;

        /// <summary>
        /// Keeps what is inside it above the software keyboard, as a view laid out against UIKit's keyboard layout guide:
        /// its bottom padding is at least how far the keyboard reaches into it (<see cref="UIRoot.KeyboardHeight"/>), so with
        /// the keyboard down its own padding (room for the home indicator) holds, and with it up its content sits on the
        /// keyboard, following it as it slides. It and its background stay where they are.
        /// </summary>
        public UIValue<bool> AvoidsKeyboard => _avoidsKeyboard;

        /// <summary>Whether it cuts what is inside it to its rect.</summary>
        public UIValue<bool> Clips => _clips;

        /// <summary>The corner radii its clip uses.</summary>
        public UIValue<CornerRadii> ClipRadii => _clipRadii;

        /// <summary>Whether it is drawn above everything in its screen, out of its ancestors' clips.</summary>
        public UIValue<bool> TopLayer => _topLayer;

        /// <summary>Whether it takes the pointer.</summary>
        public UIValue<bool> HitTestable => _hitTestable;

        // ── Runtime ──────────────────────────────────────────────────────────────

        // Where layout puts it in its parent's space (top-left and size), with where it is drawn while it springs there.
        [NonSerialized] internal readonly AnimatedValue<Rect> Frame = new(default, 0.5f);

        [NonSerialized] internal UITree Tree;
        [NonSerialized] internal UINode ParentNode;
        [NonSerialized] internal UINode LastParent;
        [NonSerialized] internal int Index = -1;

        // Its drawables, in component order, and the first that measures.
        [NonSerialized] internal readonly List<UIDrawable> Drawables = new();
        [NonSerialized] internal IMeasurable Content;

        // What it draws, in its own space, made by its draw computation: under its children, and over them.
        [NonSerialized] internal readonly List<PrimitiveInstance> Instances = new();
        [NonSerialized] internal readonly List<Texture> InstanceTextures = new();
        [NonSerialized] internal readonly List<PrimitiveInstance> OverlayInstances = new();
        [NonSerialized] internal readonly List<Texture> OverlayTextures = new();
        [NonSerialized] internal NodeDraw DrawComputation;

        // Its scroll container behaviour, if any, which offsets what is inside it.
        [NonSerialized] internal ScrollView Scroller;

        // Moved, scaled and faded on top of its own drawn values by a presentation bringing it in or taking it away;
        // made the first time one does.
        [NonSerialized] internal NodeTransition Transition;

        // Above 0, it is presented: drawn at that level of its root's presentation stack, above what is below, out of
        // its ancestors' clips. Negative while it is being dismissed (drawn, but taking no pointers).
        [NonSerialized] internal readonly Observable<int> PresentationLevel = new();

        // While it goes away by its DisplayTransition: out of layout (Display None) but still drawn where it was, taking
        // no pointers, until the transition is over.
        [NonSerialized] internal readonly Observable<bool> Leaving = new();

        // The layout pass it was last laid out in: a node laid out in the pass before carries on from where it is drawn;
        // one coming back into layout (or laid out for the first time) lands where it goes at once.
        [NonSerialized] internal int LayoutPass = -1;

        // Told when it comes into layout, with where it is laid out (a DisplayTransition bringing it in from there).
        [NonSerialized] internal Action<Rect> EnteredLayout;

        // A cross-fade of its drawing: what it drew before, and how far the new drawing has faded in (made the first
        // time it cross-fades).
        [NonSerialized] private List<PrimitiveInstance> _outgoing;
        [NonSerialized] private List<Texture> _outgoingTextures;
        [NonSerialized] private AnimatedValue<float> _crossFade;

        // States it provides to itself and what is inside it.
        [NonSerialized] private Dictionary<UIState, bool> _states;
        [NonSerialized] private readonly Observable<int> _stateRevision = new();
        [NonSerialized] private readonly Observable<InteractionStates> _interaction = new();
        [NonSerialized] private int _interactionProviders;

        [NonSerialized] private RectTransform _rectTransform;
        [NonSerialized] private bool _enabled;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

        /// <summary>Its layout parent: the node its transform's parent holds, or null for a root.</summary>
        public UINode Parent => ParentNode;

        /// <summary>The root it is laid out under, or null when it is under none.</summary>
        public UIRoot Root => Tree?.Root;

        /// <summary>Where layout puts it in its parent's space, in points: its top-left corner and size.</summary>
        public Rect LaidOutRect => Frame.Model;

        /// <summary>Where it is drawn now in its parent's space, while it springs to <see cref="LaidOutRect"/>.</summary>
        public Rect PresentedRect => Frame.Presented;

        /// <summary>Its size as laid out, in points.</summary>
        public Vector2 Size => Frame.Model.size;

        /// <summary>
        /// Where it is drawn now, in its root's points: the bounds of its rect as drawn, moved, scaled and turned with
        /// everything above it, as of the last frame. For flying something from where this is (a photo opening).
        /// </summary>
        public Rect DrawnRect
        {
            get
            {
                var tree = Tree;
                if (tree == null || Index < 0 || Index >= tree.Nodes.Count) return Frame.Peek;
                var world = tree.World[Index];
                var size = (Vector2)tree.Slots[Index].SizeOpacity;
                Vector2 a = world.Apply(Vector2.zero), b = world.Apply(new Vector2(size.x, 0f));
                Vector2 c = world.Apply(size), d = world.Apply(new Vector2(0f, size.y));
                var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
                var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
                return new Rect(min, max - min);
            }
        }

        /// <summary>A point in its root's points as a point in its own space, as it is drawn (as of the last frame).</summary>
        public Vector2 RootToLocal(Vector2 point)
        {
            var tree = Tree;
            if (tree == null || Index < 0 || Index >= tree.Nodes.Count) return point - Frame.Peek.position;
            return tree.World[Index].Inverse.Apply(point);
        }

        /// <summary>A point in its own space as a point in its root's points, as it is drawn (as of the last frame).</summary>
        public Vector2 LocalToRoot(Vector2 point)
        {
            var tree = Tree;
            if (tree == null || Index < 0 || Index >= tree.Nodes.Count) return point + Frame.Peek.position;
            return tree.World[Index].Apply(point);
        }

        // ── States ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Turns <paramref name="state"/> on or off for this node and everything inside it. Turning one on clears the
        /// others of its group here.
        /// </summary>
        public void SetState(UIState state, bool on = true)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Outside a change, a state with an animation for coming on (or going off) moves everything it changes by
            // it: variants, and token values that follow a mode, alike.
            if (UITransaction.Current == null && (on ? state.Enter : state.Exit) is { } animation)
            {
                UI.Animate(animation, () => SetState(state, on));
                return;
            }
            _states ??= new Dictionary<UIState, bool>();
            bool changed = !_states.TryGetValue(state, out var was) || was != on;
            if (on && !string.IsNullOrEmpty(state.Group))
            {
                List<UIState> others = null;
                foreach (var pair in _states)
                    if (pair.Key != state && pair.Value && pair.Key.Group == state.Group)
                        (others ??= new List<UIState>()).Add(pair.Key);
                if (others != null)
                {
                    foreach (var other in others)
                        _states[other] = false;
                    changed = true;
                }
            }
            if (!changed) return;
            _states[state] = on;
            _stateRevision.Value++;
        }

        /// <summary>Stops this node providing <paramref name="state"/>: it takes it from above again.</summary>
        public void ClearState(UIState state)
        {
            if (_states == null || !_states.Remove(state)) return;
            _stateRevision.Value++;
        }

        /// <summary>Whether <paramref name="state"/> is active here: from the nearest node at or above it that provides it (or its group).</summary>
        public bool IsStateActive(UIState state)
        {
            for (var node = this; node != null; node = node.LayoutParentOrTransform())
            {
                if (node.TryGetLocalState(state, out bool on))
                    return on;
            }
            return false;
        }

        /// <summary>The interaction states active here: those of the nearest node at or above it that provides them.</summary>
        public InteractionStates ActiveInteraction()
        {
            for (var node = this; node != null; node = node.LayoutParentOrTransform())
            {
                // Read on every node passed, so a provider added or a parent changed on the way is noticed.
                _ = node._stateRevision.Value;
                if (node._interactionProviders > 0)
                    return node._interaction.Value;
            }
            return InteractionStates.None;
        }

        /// <summary>The interaction states this node provides itself.</summary>
        public InteractionStates Interaction => _interaction.Peek;

        // Whether it provides interaction states to itself and what is inside it (a button does).
        internal bool ProvidesInteraction => _interactionProviders > 0;

        internal void AddInteractionProvider()
        {
            _interactionProviders++;
            _stateRevision.Value++;
        }

        internal void RemoveInteractionProvider()
        {
            if (--_interactionProviders <= 0)
            {
                _interactionProviders = 0;
                _interaction.Value = InteractionStates.None;
            }
            _stateRevision.Value++;
        }

        // Its layout parent changed: what reads states through it reads them again.
        internal void HierarchyChanged() => _stateRevision.Value++;

        internal void SetInteraction(InteractionStates states, bool on)
        {
            var current = _interaction.Peek;
            _interaction.Value = on ? current | states : current & ~states;
        }

        private bool TryGetLocalState(UIState state, out bool on)
        {
            _ = _stateRevision.Value;
            on = false;
            if (_states == null) return false;
            if (_states.TryGetValue(state, out on)) return true;
            if (string.IsNullOrEmpty(state.Group)) return false;
            foreach (var pair in _states)
            {
                if (pair.Value && pair.Key.Group == state.Group)
                    return true;
            }
            return false;
        }

        // The layout parent once the tree knows it, otherwise the node on the transform's parent.
        private UINode LayoutParentOrTransform()
        {
            if (Tree != null) return ParentNode;
            var parent = transform.parent;
            return parent != null && parent.TryGetComponent(out UINode node) && node.isActiveAndEnabled ? node : null;
        }

        /// <summary>
        /// Cross-fades its drawing on <paramref name="animation"/>: what it draws now fades out as what it draws next
        /// fades in. Called when a drawn value that cannot interpolate changes inside a change. Cross-fading already,
        /// the drawing on its way out stays the one that was there first.
        /// </summary>
        internal void CrossFadeDrawing(UIAnimation animation)
        {
            if (Instances.Count == 0 && OverlayInstances.Count == 0) return;
            _crossFade ??= new AnimatedValue<float>(1f, 0.002f, true);
            if (!_crossFade.IsAnimating || _outgoing == null)
            {
                _outgoing ??= new List<PrimitiveInstance>();
                _outgoingTextures ??= new List<Texture>();
                _outgoing.Clear();
                _outgoingTextures.Clear();
                _outgoing.AddRange(Instances);
                _outgoingTextures.AddRange(InstanceTextures);
                _outgoing.AddRange(OverlayInstances);
                _outgoingTextures.AddRange(OverlayTextures);
            }
            _crossFade.Jump(0f);
            _crossFade.SpringTo(1f, Vector4.zero, animation);
            DrawComputation?.Invalidate();
        }

        // How far the new drawing has faded in: 1 when it is not cross-fading. Tracked, so drawing follows it.
        internal float CrossFadeProgress()
        {
            if (_crossFade == null || _outgoing == null) return 1f;
            float t = Mathf.Clamp01(_crossFade.Value);
            if (t >= 0.999f && !_crossFade.IsAnimating)
            {
                _outgoing.Clear();
                return 1f;
            }
            return t;
        }

        // The drawing on its way out, at <paramref name="opacity"/>.
        internal void DrawOutgoing(float opacity)
        {
            if (_outgoing == null || _outgoing.Count == 0) return;
            int first = Instances.Count;
            Instances.AddRange(_outgoing);
            InstanceTextures.AddRange(_outgoingTextures);
            Fade(Instances, first, opacity);
        }

        // Fades primitives from <paramref name="first"/> on to <paramref name="opacity"/> of what they were.
        internal static void Fade(List<PrimitiveInstance> instances, int first, float opacity)
        {
            for (int i = first; i < instances.Count; i++)
            {
                var instance = instances[i];
                instance.Color.w *= opacity;
                instance.Color2.w *= opacity;
                instances[i] = instance;
            }
        }

        /// <summary>Its transition values, made if it has none yet (the tree composites it with them from now on).</summary>
        internal NodeTransition EnsureTransition()
        {
            if (Transition != null) return Transition;
            Transition = new NodeTransition();
            Tree?.InvalidateComposite();
            return Transition;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _enabled = true;
            ForEachValue(value => value.Attach(this));
            DrawComputation ??= new NodeDraw(this);
            DrawComputation.Invalidate();
            UITree.NodeChanged(this);
        }

        private void OnDisable()
        {
            _enabled = false;
            ForEachValue(value => value.Detach());
            DrawComputation?.Dispose();
            DrawComputation = null;
            Frame.Settle();
            UITree.NodeChanged(this);
            Tree?.Remove(this);
        }

        private void OnValidate()
        {
            if (!_enabled) return;
            ForEachValue(value => value.Refresh());
            UI.RequestRepaint();
        }

        private void OnTransformParentChanged()
        {
            if (_enabled) UITree.NodeChanged(this);
        }

        private void OnTransformChildrenChanged()
        {
            if (_enabled) UITree.NodeChanged(this);
        }

        internal void RegisterDrawable(UIDrawable drawable)
        {
            if (Drawables.Contains(drawable)) return;
            Drawables.Add(drawable);
            SortDrawables();
            DrawComputation?.Invalidate();
            Tree?.InvalidateLayout();
        }

        internal void UnregisterDrawable(UIDrawable drawable)
        {
            if (!Drawables.Remove(drawable)) return;
            SortDrawables();
            DrawComputation?.Invalidate();
            Tree?.InvalidateLayout();
        }

        // Drawables draw in component order; the first that measures is the content layout fits to.
        private void SortDrawables()
        {
            if (Drawables.Count > 1)
            {
                var order = GetComponents<UIDrawable>();
                Drawables.Sort((a, b) => Array.IndexOf(order, a).CompareTo(Array.IndexOf(order, b)));
            }
            Content = null;
            foreach (var drawable in Drawables)
            {
                if (drawable is IMeasurable measurable && drawable.isActiveAndEnabled)
                {
                    Content = measurable;
                    break;
                }
            }
        }

        private void ForEachValue(Action<IUIValue> action)
        {
            action(_width); action(_height); action(_padding); action(_gap); action(_direction); action(_wrap);
            action(_alignX); action(_alignY); action(_aspectRatio); action(_floating); action(_display); action(_ignoresSafeArea);
            action(_opacity); action(_offset); action(_scale); action(_rotation); action(_zIndex); action(_groupOpacity); action(_blur);
            action(_saturation); action(_brightness); action(_mask); action(_avoidsKeyboard);
            action(_clips); action(_clipRadii); action(_topLayer); action(_hitTestable);
        }
    }
}
