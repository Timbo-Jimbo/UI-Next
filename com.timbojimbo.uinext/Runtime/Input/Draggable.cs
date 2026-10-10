using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>A drag as <see cref="Draggable"/> reports it.</summary>
    public readonly struct DragEvent
    {
        /// <summary>How far it has been dragged since it began, in the node's parent's points (x right, y down).</summary>
        public readonly Vector2 Translation;

        /// <summary>How fast it is moving, in the node's parent's points a second.</summary>
        public readonly Vector2 Velocity;

        /// <summary>The pointer, as it was last heard.</summary>
        public readonly PointerEvent Pointer;

        internal DragEvent(Vector2 translation, Vector2 velocity, in PointerEvent pointer)
        {
            Translation = translation;
            Velocity = velocity;
            Pointer = pointer;
        }
    }

    /// <summary>
    /// A pan gesture on a node, as UIKit's UIPanGestureRecognizer: once a pointer down on the node (or inside it) has
    /// moved far enough a way it takes (<see cref="Axis"/>, <see cref="Takes"/>), it takes the pointer (cancelling presses
    /// under it) and reports the drag as it goes and the speed it ends with, for whatever it moves: a sheet, a card swiped
    /// away, a slider's thumb. A scroll view inside it that scrolls the way the drag goes takes the drag first; at its end,
    /// it passes the rest of the drag on to this one if this takes it, and this hands it back as soon as the finger turns a
    /// way it does not take, as a sheet and the scroll view in it share one drag on iOS. What its change callbacks set
    /// directly is sampled, so the change made as it lets go starts at the speed it moved it (<see cref="TracksVelocity"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UINode))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Draggable")]
    public sealed class Draggable : MonoBehaviour, IPointerHandler
    {
        [Tooltip("Which way it drags: it takes a drag only once it has moved more that way than the other.")]
        [SerializeField] private ScrollAxis _axis = ScrollAxis.Both;

        [Tooltip("How far, in points, the pointer moves before the drag begins.")]
        [SerializeField, Min(0f)] private float _threshold = 8f;

        [Tooltip("Hands the speed it moves things at on to the change made as it lets go, as SwiftUI's tracksVelocity. Off for precise controls (a stepped slider) whose snap must not overshoot what the finger chose.")]
        [SerializeField] private bool _tracksVelocity = true;

        private int _pointer = -1;
        private bool _dragging;
        // Where the drag is measured from, in its root's points (where the pointer went down, or where a scroll view
        // passed the drag on), and how far it had moved as it began, so what it moves does not jump by the threshold.
        private Vector2 _start;
        private Vector2 _origin;
        // The scroll view that passed it the drag under way, which it hands the drag back to.
        private ScrollView _passedBy;
        private readonly GestureVelocity _velocity = new();
        private UINode _node;

        /// <summary>Raised when the drag begins: the pointer has moved far enough, or a scroll view passed the drag on.</summary>
        public event Action<DragEvent> Began;

        /// <summary>Raised as it moves.</summary>
        public event Action<DragEvent> Changed;

        /// <summary>Raised when it lets go, with the speed it was moving at; and when it hands a drag back, with none.</summary>
        public event Action<DragEvent> Ended;

        /// <summary>Raised when it is cut short (the pointer was taken, the node disabled).</summary>
        public event Action Cancelled;

        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        public ScrollAxis Axis
        {
            get => _axis;
            set => _axis = value;
        }

        /// <summary>
        /// Which drags it takes, within its <see cref="Axis"/>, as UIKit's gestureRecognizerShouldBegin: given how far one
        /// has moved (its node's parent's points, x right, y down), whether it is one it takes (a sheet takes drags down,
        /// not up). Asked as a drag begins and when a scroll view at its end passes one on; while it has a drag passed on,
        /// it hands the drag back as soon as this says no. Null: every drag its axis allows.
        /// </summary>
        public Func<Vector2, bool> Takes { get; set; }

        /// <summary>
        /// Whether it hands its speed on, as SwiftUI's tracksVelocity, on by default: while <see cref="Began"/> and
        /// <see cref="Changed"/> run, the values they set directly are sampled, and the animated change made as it lets go
        /// (in <see cref="Ended"/> or <see cref="Cancelled"/>) starts each at the speed it moved it; once it is over, what
        /// it moved that nothing took over lands still. Off, what it moves lands still as it lets go: for precise controls,
        /// a stepped slider whose snap must not overshoot the step the finger chose.
        /// </summary>
        public bool TracksVelocity
        {
            get => _tracksVelocity;
            set => _tracksVelocity = value;
        }

        /// <summary>Whether a drag is under way.</summary>
        public bool IsDragging => _dragging;

        private void OnDisable()
        {
            bool dragging = _dragging;
            int pointer = _pointer;
            var passedBy = _passedBy;
            _pointer = -1;
            _dragging = false;
            _passedBy = null;
            if (dragging) Cancel();
            else _velocity.Forget();
            // A drag a scroll view passed it goes back to that scroll view, which carries on with it from here.
            if (dragging && passedBy != null && passedBy.isActiveAndEnabled && UIInput.TryEventOf(pointer, out var e)
                && UIInput.Pass(this, passedBy, pointer))
                passedBy.Receive(in e, e.Position);
        }

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer >= 0) return;
            _pointer = e.PointerId;
            _dragging = false;
            _passedBy = null;
            _start = e.StartPosition;
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            var translation = ParentVector(e.Position - _start);
            if (!_dragging)
            {
                // Far enough, a way it takes.
                if (Mathf.Max(Mathf.Abs(translation.x), Mathf.Abs(translation.y)) < _threshold || !TakesDrag(translation)
                    || !UIInput.Claim(this, e.PointerId)) return;
                _dragging = true;
                // The drag starts from here, so what it moves does not jump by the threshold.
                _origin = translation.normalized * _threshold;
                Raise(Began, new DragEvent(translation - _origin, ParentVector(e.Velocity), in e), e.Time);
                return;
            }
            translation -= _origin;
            // A drag passed on goes back as soon as the finger turns, along the way the scroll view that passed it scrolls,
            // a way this does not take.
            if (_passedBy != null && !TakesDrag(_passedBy.Along(translation)))
            {
                HandBack(in e);
                return;
            }
            Raise(Changed, new DragEvent(translation, ParentVector(e.Velocity), in e), e.Time);
        }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            _passedBy = null;
            if (!_dragging) return;
            _dragging = false;
            var drag = new DragEvent(ParentVector(e.Position - _start) - _origin, ParentVector(e.Velocity), in e);
            if (_tracksVelocity)
                _velocity.LetGo(e.Time);
            try
            {
                Ended?.Invoke(drag);
            }
            finally
            {
                _velocity.Forget();
            }
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            _passedBy = null;
            if (!_dragging) return;
            _dragging = false;
            if (_tracksVelocity)
                _velocity.LetGo(e.Time);
            Cancel();
        }

        private void Cancel()
        {
            try
            {
                Cancelled?.Invoke();
            }
            finally
            {
                _velocity.Forget();
            }
        }

        // Raises a change callback; what it sets directly is sampled, unless it hands no speed on.
        private void Raise(Action<DragEvent> callback, DragEvent drag, double time)
        {
            if (_tracksVelocity) _velocity.Run(callback, drag, time);
            else callback?.Invoke(drag);
        }

        // ── A drag passed on ─────────────────────────────────────────────────────

        /// <summary>
        /// Whether it takes the rest of a drag a scroll view inside it passes on, the finger having gone
        /// <paramref name="rootTranslation"/> (its root's points) past the scroll view's end.
        /// </summary>
        internal bool Receives(Vector2 rootTranslation) => isActiveAndEnabled && _pointer < 0 && TakesDrag(ParentVector(rootTranslation));

        /// <summary>Takes the rest of a drag <paramref name="from"/> passes on, measured from <paramref name="start"/> (its root's points).</summary>
        internal void Receive(in PointerEvent e, Vector2 start, ScrollView from)
        {
            _pointer = e.PointerId;
            _dragging = true;
            _passedBy = from;
            _start = start;
            _origin = Vector2.zero;
            Raise(Began, new DragEvent(ParentVector(e.Position - start), ParentVector(e.Velocity), in e), e.Time);
        }

        // The finger turned a way it does not take: its drag ends where it began, handing no speed on (the speed goes back
        // with the drag), and the scroll view that passed it carries on from where this one began.
        private void HandBack(in PointerEvent e)
        {
            var scroll = _passedBy;
            var start = _start;
            _passedBy = null;
            _pointer = -1;
            _dragging = false;
            _velocity.Forget();
            Ended?.Invoke(new DragEvent(Vector2.zero, Vector2.zero, in e));
            if (scroll != null && scroll.isActiveAndEnabled && UIInput.Pass(this, scroll, e.PointerId))
                scroll.Receive(in e, start);
        }

        // Whether a drag that has moved this far is one it takes: more the way it drags than the other, and as it says.
        private bool TakesDrag(Vector2 translation)
        {
            float x = Mathf.Abs(translation.x), y = Mathf.Abs(translation.y);
            bool way = _axis switch
            {
                ScrollAxis.Horizontal => x > y,
                ScrollAxis.Vertical => y > x,
                ScrollAxis.None => false,
                _ => x > 0f || y > 0f,
            };
            return way && (Takes == null || Takes(translation));
        }

        private Vector2 ParentVector(Vector2 rootVector)
        {
            var parent = Node.Parent;
            return parent != null ? UIInput.ToNodeVector(parent, rootVector) : rootVector;
        }
    }
}
