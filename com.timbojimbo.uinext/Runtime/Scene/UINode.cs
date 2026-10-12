using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Tooltip("Draws it and what is inside it as one image first, so its opacity, blur and mask apply to that image as a whole rather than to each part. It costs an image only while one of them is in use.")]
        [FormerlySerializedAs("_groupOpacity")]
        [SerializeField] private UIValue<bool> _compositeGroup = false;

        [Tooltip("Blurs it and what is inside it, by this many points. Only a composite group is blurred.")]
        [SerializeField] private UIValue<float> _blur = 0f;

        [Tooltip("How saturated it and what is inside it are drawn: 1 as they are, 0 grey, more than 1 more vivid.")]
        [SerializeField] private UIValue<float> _saturation = 1f;

        [Tooltip("How bright it and what is inside it are drawn: 1 as they are, less dims them.")]
        [SerializeField] private UIValue<float> _brightness = 1f;

        [Tooltip("A texture whose alpha, stretched over its rect, cuts it and what is inside it; nothing is kept outside its rect. Only a composite group is masked.")]
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

        // ── Matched ──────────────────────────────────────────────────────────────

        [Tooltip("The name it is matched by. Shown in a change that hides a node of the same name and id, it flies from where that one was drawn to its own place; shown while one stays, it flies out of it.")]
        [SerializeField] private UIValue<string> _matchName = "";

        [Tooltip("The id it and everything inside it are matched by, with their names, so many of a kind (a list's rows) stay distinct. Empty: the id of the nearest node above that has one.")]
        [SerializeField] private UIValue<string> _matchId = "";

        [Tooltip("Where what is inside it lines up with its flight while it flies, along the axis the flight does not fit it to: (0, 0) its top left, (0.5, 0.5) its centre, (1, 1) its bottom right.")]
        [SerializeField] private UIValue<Vector2> _matchAnchor = new Vector2(0.5f, 0.5f);

        // ── Focus ────────────────────────────────────────────────────────────────

        [Tooltip("What it is to keyboard and gamepad focus. Section: focus moves in and out of it freely, a move towards it coming into it as one, and Tab goes through it before going on. Group: one stop until Submit enters it; Back leaves it.")]
        [SerializeField] private UIValue<FocusScope> _focusScope = TimboJimbo.UINext.FocusScope.None;

        [Tooltip("With a section: where a move into it from outside lands. Nearest: whatever is nearest that way. Remembered: where focus last was inside it, else what prefers focus, else its first.")]
        [SerializeField] private UIValue<FocusEntry> _focusEntry = TimboJimbo.UINext.FocusEntry.Nearest;

        [Tooltip("Focus starts here, or inside it, when it comes to what this is in: a presentation coming up, a group entered, a section with nothing remembered.")]
        [SerializeField] private UIValue<bool> _prefersFocus = false;

        [Tooltip("Whether it takes keyboard and gamepad focus. Auto: if it is a button. Always: button or not (a slider, a stepper's row). Never: not even as a button (a tap area a key need not stop at).")]
        [SerializeField] private UIValue<Focusability> _focusable = Focusability.Auto;

        [Tooltip("Draws the focus ring round it while it has keyboard or gamepad focus and focus shows. Off for a node that shows focus its own way (a row's highlight).")]
        [SerializeField] private UIValue<bool> _focusRing = true;

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
        /// Whether it and everything inside it are drawn as one image first, so that its opacity, <see cref="Blur"/> and
        /// <see cref="Mask"/> apply to that image as a whole, as SwiftUI's compositingGroup and Core Animation's group
        /// opacity do. Without it, its opacity multiplies into each part, so overlapping parts show through each other,
        /// and blur and masks do nothing. It costs an image only while it is not fully opaque, blurred or masked.
        /// Saturation and brightness need none: they come out the same either way.
        /// </summary>
        public UIValue<bool> CompositeGroup => _compositeGroup;

        /// <summary>
        /// Blurs it and everything inside it by this many points, as SwiftUI's blur(radius:) and CSS's filter: blur():
        /// only a <see cref="CompositeGroup"/> is blurred: its image, blurred, is what is drawn. It animates, so a node can
        /// blur in and out of focus.
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
        /// Only a <see cref="CompositeGroup"/> is masked: the mask is applied to its image.
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

        /// <summary>
        /// Its shape's corner radii: what its clip uses, and the corners of a matched flight's shape at its end (see
        /// <see cref="MatchName"/>), whether it clips or not.
        /// </summary>
        public UIValue<CornerRadii> ClipRadii => _clipRadii;

        /// <summary>Whether it is drawn above everything in its screen, out of its ancestors' clips.</summary>
        public UIValue<bool> TopLayer => _topLayer;

        /// <summary>Whether it takes the pointer.</summary>
        public UIValue<bool> HitTestable => _hitTestable;

        /// <summary>
        /// The name it is matched by, as SwiftUI's matchedGeometryEffect: shown inside a change
        /// (<see cref="UI.Animate(UIAnimation, Action)"/>) that hides a node of the same name and id, it takes over from
        /// that one, flying from where it was drawn to its own place; shown while a node of the same name and id stays, it
        /// flies out of that one. The far end is a picture of the other node as it was drawn; the two cross-fade as one
        /// image. Each end's own drawing (a box, its border, its shadow) is laid out at the flight's rect; what is inside
        /// it is scaled evenly into the flight along the axis the two ends are most alike in, lined up along the other as
        /// its node's <see cref="MatchAnchor"/> says, and cut to the flight's window, whose corners go from the other's
        /// <see cref="ClipRadii"/> to its own. Named nodes inside either end fly on their own. The flight is drawn above
        /// everything at its level, carried by whatever moves it meanwhile (a scroll, a page sliding in), and turns round
        /// from where it is drawn if the change is taken back. Empty: it is not matched.
        /// </summary>
        public UIValue<string> MatchName => _matchName;

        /// <summary>
        /// The id it and everything inside it are matched by, with their names, so that many of a kind stay distinct: a
        /// list's rows each have the row's id, and the page a row opens into is given it. Empty: the id of the nearest node
        /// above it that has one.
        /// </summary>
        public UIValue<string> MatchId => _matchId;

        /// <summary>
        /// Where what is inside it lines up with its flight while it is matched (<see cref="MatchName"/>), as SwiftUI's
        /// matchedGeometryEffect takes an anchor: what is inside is scaled evenly into the flight along one axis (the one
        /// its two ends are most alike in), so this places it along the other, (0, 0) at its top left, (0.5, 0.5) at its
        /// centre, (1, 1) at its bottom right. Its centre by default; a row growing into a page wants its top.
        /// </summary>
        public UIValue<Vector2> MatchAnchor => _matchAnchor;

        /// <summary>
        /// What it is to keyboard and gamepad focus (<see cref="UIFocus"/>), as SwiftUI's focusSection and Xbox's focus
        /// engagement: nothing; a section, which focus moves in and out of freely, a move towards it from outside coming
        /// into it as one (landing as <see cref="FocusEntry"/> says) and Tab going through everything inside it before
        /// going on; or a group, one stop until Submit enters it and Back leaves it. A scope provides FocusWithin to itself
        /// and what is inside it, as a button provides its states.
        /// </summary>
        public UIValue<FocusScope> FocusScope => _focusScope;

        /// <summary>
        /// Where a move into it from outside lands, when it is a section: on whatever is nearest that way inside it, or on
        /// where focus last was inside it (else what prefers focus, else its first), as UIKit's
        /// remembersLastFocusedIndexPath.
        /// </summary>
        public UIValue<FocusEntry> FocusEntry => _focusEntry;

        /// <summary>
        /// Whether focus starts here, or inside it, as SwiftUI's prefersDefaultFocus: when focus comes to what it is in (a
        /// presentation coming up, a group entered, a section entered with nothing remembered), it goes to the first that
        /// prefers it, in reading order, rather than to the first of all; and of equally near candidates, one that prefers
        /// it wins.
        /// </summary>
        public UIValue<bool> PrefersFocus => _prefersFocus;

        /// <summary>
        /// Whether it takes keyboard and gamepad focus, as SwiftUI's focusable(): a button does unless told
        /// <see cref="Focusability.Never"/> (a story's tap areas, a backdrop's tap); anything else only when told
        /// <see cref="Focusability.Always"/> (a slider, a stepper's row), which takes the arrows of its own as it wants
        /// (<see cref="IFocusMoveHandler"/>).
        /// </summary>
        public UIValue<Focusability> Focusable => _focusable;

        /// <summary>
        /// Whether the focus ring is drawn round it while it has focus and focus shows (a key or a gamepad moved it), as
        /// iPadOS's focus halo and a browser's focus outline: a band out past its edge, over what is inside it, in its
        /// root's look (<see cref="UIRoot.FocusRingColor"/>). Off for a node that shows focus its own way, by its values'
        /// Focused variants: a list row's highlight.
        /// </summary>
        public UIValue<bool> FocusRing => _focusRing;

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

        // Whether it is always where layout puts it at once, never springing there inside a change: a label's inline
        // content, which its text puts, as its glyphs are, so the two never part.
        [NonSerialized] internal bool LandsAtOnce;

        // Told when it comes into layout, with where it is laid out (a DisplayTransition bringing it in from there).
        [NonSerialized] internal Action<Rect> EnteredLayout;

        // As compositing last drew it, kept on the node so a picture of it can be taken whatever its tree has done since:
        // its slot (where, how big, its clips in its tree's clip shapes), how opaque it is on its own, whether it was shown,
        // and the compositing pass that drew it.
        [NonSerialized] internal NodeSlot DrawnSlot;
        [NonSerialized] internal float DrawnOpacity;
        [NonSerialized] internal bool DrawnShown;
        [NonSerialized] internal int DrawnPass = -1;

        // The flight it is drawn in while it flies to its place (a matched node shown), and the flight that took over from
        // it, whose picture of it flies instead (a matched node hidden).
        [NonSerialized] internal Flight FlightIn;
        [NonSerialized] internal Flight FlightOut;

        // Whether it has been warned of a blur or a mask, which apply only to a composite group.
        [NonSerialized] internal bool WarnedOutsideGroup;

        // Where keyboard and gamepad focus last was inside it: for focus coming back (a presentation over it going, a
        // section that remembers, a group entered again) and for telling equally near candidates apart.
        [NonSerialized] internal UINode FocusMemory;

        // Whether it has focus and focus shows (CSS's :focus-visible): its focus ring is drawn meanwhile.
        [NonSerialized] internal readonly Observable<bool> FocusVisible = new();

        // How far its focus ring is shaken from its place, shaking no where a key went nowhere (made the first time).
        [NonSerialized] internal AnimatedValue<Vector2> RingShake;

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

        /// <summary>
        /// Whether <paramref name="state"/> is active here: from the nearest node at or above it that provides it (or its
        /// group), unless the editor previews it (or another of its group) everywhere.
        /// </summary>
        public bool IsStateActive(UIState state)
        {
            if (StatePreview.Decides(state, out bool previewed))
                return previewed;
            for (var node = this; node != null; node = node.LayoutParentOrTransform())
            {
                if (node.TryGetLocalState(state, out bool on))
                    return on;
            }
            return false;
        }

        /// <summary>
        /// The interaction states active here: those of the nearest node at or above it that provides them (a button), but
        /// for whether focus is on it or within it, which the nearest focus scope (<see cref="FocusScope"/>), or node that
        /// takes focus itself (<see cref="Focusable"/> Always), says when there is one nearer: content in a section inside a
        /// disabled card is still Disabled. Those the editor previews everywhere are on too.
        /// </summary>
        public InteractionStates ActiveInteraction() => ProvidedInteraction() | StatePreview.Interaction;

        private InteractionStates ProvidedInteraction()
        {
            const InteractionStates focus = InteractionStates.Focused | InteractionStates.FocusWithin;
            var scoped = InteractionStates.None;
            bool inScope = false;
            for (var node = this; node != null; node = node.LayoutParentOrTransform())
            {
                // Read on every node passed, so a provider added or a parent changed on the way is noticed.
                _ = node._stateRevision.Value;
                if (node._interactionProviders > 0)
                    return inScope ? (node._interaction.Value & ~focus) | scoped : node._interaction.Value;
                if (!inScope && node.SaysFocus)
                {
                    inScope = true;
                    scoped = node._interaction.Value & focus;
                }
            }
            return scoped;
        }

        /// <summary>The interaction states this node provides itself.</summary>
        public InteractionStates Interaction => _interaction.Peek;

        // Whether it provides interaction states to itself and what is inside it (a button does).
        internal bool ProvidesInteraction => _interactionProviders > 0;

        // A focus scope, and a node that takes focus itself, say whether focus is on or within them, to themselves and what
        // is inside them. Tracked, so what reads states through a node made one, or no longer one, reads them again.
        private bool SaysFocus => _focusScope.Value != TimboJimbo.UINext.FocusScope.None || _focusable.Value == Focusability.Always;

        internal void AddInteractionProvider()
        {
            _interactionProviders++;
            _stateRevision.Value++;
        }

        internal void RemoveInteractionProvider()
        {
            if (--_interactionProviders <= 0)
            {
                // A button's states go with it; whether focus is within it is focus's.
                _interactionProviders = 0;
                _interaction.Value = _interaction.Peek & InteractionStates.FocusWithin;
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
        /// fades in. Called when a drawn value that cannot interpolate changes inside a change. Cross-fading already, what
        /// fades out is what is drawn now, both drawings as far as each has faded, as a Core Animation transition starts
        /// from the presentation layer: a count stepped quickly fades on from where it is, never back to where it began.
        /// </summary>
        internal void CrossFadeDrawing(UIAnimation animation)
        {
            if (Instances.Count == 0 && OverlayInstances.Count == 0) return;
            _crossFade ??= new AnimatedValue<float>(1f, 0.002f, true);
            _outgoing ??= new List<PrimitiveInstance>();
            _outgoingTextures ??= new List<Texture>();
            // As last drawn: a drawing still fading out is in it already, at the opacity it was drawn at.
            _outgoing.Clear();
            _outgoingTextures.Clear();
            _outgoing.AddRange(Instances);
            _outgoingTextures.AddRange(InstanceTextures);
            _outgoing.AddRange(OverlayInstances);
            _outgoingTextures.AddRange(OverlayTextures);
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
            action(_opacity); action(_offset); action(_scale); action(_rotation); action(_zIndex); action(_compositeGroup); action(_blur);
            action(_saturation); action(_brightness); action(_mask); action(_avoidsKeyboard);
            action(_clips); action(_clipRadii); action(_topLayer); action(_hitTestable); action(_matchName); action(_matchId); action(_matchAnchor);
            action(_focusScope); action(_focusEntry); action(_prefersFocus); action(_focusable); action(_focusRing);
        }
    }
}
