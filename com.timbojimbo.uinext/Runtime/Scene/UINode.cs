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

        // ── States ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Turns <paramref name="state"/> on or off for this node and everything inside it. Turning one on clears the
        /// others of its group here.
        /// </summary>
        public void SetState(UIState state, bool on = true)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
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
            action(_opacity); action(_offset); action(_scale); action(_rotation); action(_zIndex);
            action(_clips); action(_clipRadii); action(_topLayer); action(_hitTestable);
        }
    }
}
