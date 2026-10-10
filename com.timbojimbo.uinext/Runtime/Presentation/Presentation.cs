using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TimboJimbo.UINext
{
    /// <summary>Whether a presentation hides what is below it.</summary>
    public enum PresentationStyle
    {
        /// <summary>Translucent: what is below stays visible, behind its dim, but takes no pointers.</summary>
        Over,

        /// <summary>Opaque and full screen: once it is in place, nothing below it is drawn at all.</summary>
        Cover,
    }

    /// <summary>How a presentation's content comes in and goes away.</summary>
    public enum PresentationTransition
    {
        /// <summary>At once.</summary>
        None,

        /// <summary>Fades in and out.</summary>
        Fade,

        /// <summary>Slides up from below the screen and back down, as a sheet.</summary>
        Sheet,

        /// <summary>Slides in from the right and back out, as a pushed page.</summary>
        Push,

        /// <summary>Grows a little and fades in, and shrinks a little as it fades out, as an alert.</summary>
        Pop,
    }

    /// <summary>
    /// Makes a node a screen, sheet or dialog presented over the rest of its root, as UIKit's presented view controllers
    /// and the web's modal dialogs: it covers its root, is drawn above everything else in it at its level of the root's
    /// presentation stack (each presentation above the last, a tooltip in one never covering the next), out of every
    /// clip, and takes the pointer from everything below it while it is up. Its backdrop dims what is below and, tapped,
    /// dismisses it; so does back (Escape, a gamepad's B). Its content (its children) comes in and goes away by its
    /// <see cref="Transition"/>, on top of their own values. Hidden (Display None) while it is not presented.
    /// <para>
    /// Presenting and dismissing each make all their state changes inside one animated change of their own, and raise
    /// <see cref="Presenting"/> or <see cref="Dismissing"/> inside it, so code joins the change: what it shows, hides or
    /// sets there moves with the presentation, as one change. A node hidden there whose namesake the presentation shows
    /// flies into it, and back out as it is dismissed (see <see cref="UINode.MatchName"/>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Timbo Jimbo/UI Next/Presentation")]
    public sealed class Presentation : UIDrawable, IPointerHandler
    {
        [SerializeField] private PresentationStyle _style = PresentationStyle.Over;
        [SerializeField] private PresentationTransition _transition = PresentationTransition.Sheet;

        [Tooltip("The colour laid over what is below while it is up.")]
        [SerializeField] private UIValue<Color> _dim = new Color(0f, 0f, 0f, 0.4f);

        [Tooltip("Tapping the backdrop (outside its content) dismisses it.")]
        [SerializeField] private bool _dismissOnBackdrop = true;

        [Tooltip("Back (Escape, a gamepad's B, Android's back) dismisses it.")]
        [SerializeField] private bool _dismissOnBack = true;

        [SerializeField] private UIAnimation _animation = new(0.45f, 0.08f);

        [SerializeField] private UnityEvent _onPresented = new();
        [SerializeField] private UnityEvent _onDismissed = new();

        private static readonly List<Presentation> s_stack = new();

        [NonSerialized] private readonly AnimatedValue<float> _backdrop = new(0f, 0.002f, true);
        [NonSerialized] private bool _presented;
        [NonSerialized] private bool _settled;
        [NonSerialized] private bool _backdropPressed;
        [NonSerialized] private UITransaction _change;
        [NonSerialized] private readonly List<UINode> _content = new();

        /// <summary>
        /// Raised inside the change that brings it up, once its own state has changed (shown, laid out, its content on its
        /// way in): what is changed here is part of that change, and moves with it.
        /// </summary>
        public event Action Presenting;

        /// <summary>
        /// Raised inside the change that takes it away, once its own state has changed (taking no pointers, its content on
        /// its way out): what is changed here is part of that change, and moves with it.
        /// </summary>
        public event Action Dismissing;

        /// <summary>Raised once it is in place.</summary>
        public event Action Presented;

        /// <summary>Raised once it has gone.</summary>
        public event Action Dismissed;

        /// <summary>Every presentation up or on its way, lowest first.</summary>
        public static IReadOnlyList<Presentation> Stack => s_stack;

        /// <summary>The topmost presentation up, or null.</summary>
        public static Presentation Top
        {
            get
            {
                for (int i = s_stack.Count - 1; i >= 0; i--)
                    if (s_stack[i]._presented)
                        return s_stack[i];
                return null;
            }
        }

        public PresentationStyle Style
        {
            get => _style;
            set => _style = value;
        }

        public PresentationTransition Transition
        {
            get => _transition;
            set => _transition = value;
        }

        /// <summary>The colour laid over what is below while it is up.</summary>
        public UIValue<Color> Dim => _dim;

        public bool DismissOnBackdrop
        {
            get => _dismissOnBackdrop;
            set => _dismissOnBackdrop = value;
        }

        public bool DismissOnBack
        {
            get => _dismissOnBack;
            set => _dismissOnBack = value;
        }

        /// <summary>The spring it comes in and goes away on.</summary>
        public UIAnimation Animation
        {
            get => _animation;
            set => _animation = value;
        }

        /// <summary>Whether it is up (or on its way up).</summary>
        public bool IsPresented => _presented;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_stack.Clear();

        /// <summary>
        /// Whether a presented node of <paramref name="tree"/> is a cover in place, which nothing below it shows through:
        /// up, and it and its content drawn where they are laid out, unscaled and opaque. Moved by anything (a swipe
        /// taking it away with the finger), what is below is drawn again, as UIKit puts the presenting views back for an
        /// interactive dismissal.
        /// </summary>
        internal static bool CoversWhatIsBelow(UITree tree, UINode node)
        {
            if (!node.TryGetComponent(out Presentation presentation) || !presentation._presented || !presentation._settled
                || presentation._style != PresentationStyle.Cover)
                return false;
            // A presented node is laid out over its whole root, from its corner.
            if (!DrawnInPlace(tree, node.Index, Vector2.zero)) return false;
            int end = tree.SubtreeEnd[node.Index];
            for (int i = node.Index + 1; i < end; i = tree.SubtreeEnd[i])
            {
                var child = tree.Nodes[i];
                if (!tree.Shown[i] || child.PresentationLevel.Peek != 0) continue;
                if (!DrawnInPlace(tree, i, child.Frame.Peek.position)) return false;
            }
            return true;
        }

        // Whether node `index` is drawn at `position` in its root's points, unscaled, unturned and fully opaque.
        private static bool DrawnInPlace(UITree tree, int index, Vector2 position)
        {
            var world = tree.World[index];
            return tree.Opacities[index] >= 0.999f
                   && Mathf.Abs(world.A - 1f) < 1e-4f && Mathf.Abs(world.B) < 1e-4f
                   && Mathf.Abs(world.C) < 1e-4f && Mathf.Abs(world.D - 1f) < 1e-4f
                   && Mathf.Abs(world.Tx - position.x) < 0.01f && Mathf.Abs(world.Ty - position.y) < 0.01f;
        }

        /// <summary>
        /// Brings it up over everything in its root, its content coming in by its transition, in one change that
        /// <see cref="Presenting"/> is raised inside.
        /// </summary>
        public UITransaction Present()
        {
            if (_presented) return _change;
            _presented = true;
            _settled = false;
            // Brought back while still going away: it turns round from where it is drawn.
            bool returning = s_stack.Contains(this);
            s_stack.Remove(this);
            s_stack.Add(this);
            UIInput.CancelAll();
            var node = Node;
            var change = UI.Animate(_animation, () =>
            {
                node.PresentationLevel.Value = s_stack.Count;
                node.Display.Value = DisplayMode.Visible;
                // Laid out now, so the content's size is known to slide it by; put where it comes in from, at once, and
                // sent in.
                UI.LayoutNow();
                CollectContent();
                foreach (var child in _content)
                {
                    var transition = child.EnsureTransition();
                    if (!returning)
                    {
                        Out(child, out var offset, out float scale, out float opacity);
                        transition.Set(offset, scale, opacity);
                    }
                    transition.To(Vector2.zero, 1f, 1f);
                }
                if (!returning)
                    _backdrop.Jump(0f);
                _backdrop.Value = 1f;
                Presenting?.Invoke();
            });
            _change = change;
            change.Finished += () =>
            {
                if (_change != change || !_presented) return;
                _settled = true;
                Compositor.MarkOrderDirty();
                Presented?.Invoke();
                _onPresented?.Invoke();
            };
            return change;
        }

        /// <summary>
        /// Takes it away, its content going by its transition, in one change that <see cref="Dismissing"/> is raised
        /// inside; once it has gone, it is hidden again.
        /// </summary>
        public UITransaction Dismiss()
        {
            if (!_presented) return _change;
            _presented = false;
            _settled = false;
            var node = Node;
            var change = UI.Animate(_animation, () =>
            {
                // On its way out it takes no pointers: what it uncovers does, at once.
                node.PresentationLevel.Value = -Mathf.Abs(node.PresentationLevel.Peek);
                Compositor.MarkOrderDirty();
                CollectContent();
                foreach (var child in _content)
                {
                    Out(child, out var offset, out float scale, out float opacity);
                    child.EnsureTransition().To(offset, scale, opacity);
                }
                _backdrop.Value = 0f;
                Dismissing?.Invoke();
            });
            _change = change;
            change.Finished += () =>
            {
                if (_change != change || _presented) return;
                Hide();
                Dismissed?.Invoke();
                _onDismissed?.Invoke();
            };
            return change;
        }

        /// <summary>Presents it if it is not up, dismisses it if it is.</summary>
        public void Toggle()
        {
            if (_presented) Dismiss();
            else Present();
        }

        // Where a content node comes in from and goes away to, by the transition: below the screen for a sheet, past its
        // right edge for a push.
        private void Out(UINode child, out Vector2 offset, out float scale, out float opacity)
        {
            offset = Vector2.zero;
            scale = 1f;
            opacity = 1f;
            var rootSize = Node.Root != null ? Node.Root.Size : new Vector2(390f, 844f);
            var rect = child.LaidOutRect;
            switch (_transition)
            {
                case PresentationTransition.Fade:
                    opacity = 0f;
                    break;
                case PresentationTransition.Sheet:
                    offset = new Vector2(0f, Mathf.Max(rect.height, rootSize.y - rect.y));
                    break;
                case PresentationTransition.Push:
                    offset = new Vector2(Mathf.Max(rect.width, rootSize.x - rect.x), 0f);
                    break;
                case PresentationTransition.Pop:
                    scale = 0.9f;
                    opacity = 0f;
                    break;
            }
        }

        private void CollectContent()
        {
            _content.Clear();
            var node = Node;
            foreach (Transform child in node.transform)
                if (child.TryGetComponent(out UINode childNode) && childNode.isActiveAndEnabled)
                    _content.Add(childNode);
        }

        private void Hide()
        {
            var node = Node;
            node.Display.Value = DisplayMode.None;
            node.PresentationLevel.Value = 0;
            s_stack.Remove(this);
            foreach (var child in _content)
                child.Transition?.Set(Vector2.zero, 1f, 1f);
            Compositor.MarkOrderDirty();
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!_presented)
                Node.Display.Value = DisplayMode.None;
            UIInput.Back += OnBack;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            UIInput.Back -= OnBack;
            // A change still moving it is no longer its: when it finishes, its handlers find it is not the current one.
            _change = null;
            if (_presented || s_stack.Contains(this))
            {
                _presented = false;
                Hide();
            }
        }

        private bool OnBack()
        {
            if (!_presented || !_dismissOnBack || Top != this) return false;
            Dismiss();
            return true;
        }

        // ── Backdrop ─────────────────────────────────────────────────────────────

        protected internal override void Draw(Painter painter)
        {
            var dim = _dim.Value;
            dim.a *= Mathf.Clamp01(_backdrop.Value);
            if (dim.a <= 0.001f) return;
            painter.FillNode(dim, default);
        }

        void IPointerHandler.OnPointerDown(in PointerEvent e) => _backdropPressed = e.Target == Node && _presented;

        void IPointerHandler.OnPointerMove(in PointerEvent e) { }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (!_backdropPressed) return;
            _backdropPressed = false;
            if (_dismissOnBackdrop && _presented && UIInput.IsOver(Node, e.Position))
                Dismiss();
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e) => _backdropPressed = false;
    }
}
