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
    /// moved far enough the way it allows, it takes the pointer (cancelling presses under it) and reports the drag as it
    /// goes and the speed it ends with, for whatever it moves: a sheet, a card swiped away, a slider's thumb. A scroll
    /// view inside it that scrolls the way the drag goes takes the drag first. What its change callbacks set directly is
    /// sampled, so the change made as it lets go starts at the speed it moved it (<see cref="TracksVelocity"/>).
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
        private Vector2 _origin;
        private readonly GestureVelocity _velocity = new();
        private UINode _node;

        /// <summary>Raised when the drag begins (the pointer has moved far enough).</summary>
        public event Action<DragEvent> Began;

        /// <summary>Raised as it moves.</summary>
        public event Action<DragEvent> Changed;

        /// <summary>Raised when it lets go, with the speed it was moving at.</summary>
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
            _pointer = -1;
            _dragging = false;
            if (dragging) Cancel();
            else _velocity.Forget();
        }

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer >= 0) return;
            _pointer = e.PointerId;
            _dragging = false;
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            var translation = ParentVector(e.Translation);
            if (!_dragging)
            {
                if (!Allows(translation) || !UIInput.Claim(this, e.PointerId)) return;
                _dragging = true;
                // The drag starts from here, so what it moves does not jump by the threshold.
                _origin = translation.normalized * _threshold;
                Raise(Began, new DragEvent(translation - _origin, ParentVector(e.Velocity), in e), e.Time);
                return;
            }
            Raise(Changed, new DragEvent(translation - _origin, ParentVector(e.Velocity), in e), e.Time);
        }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            if (!_dragging) return;
            _dragging = false;
            var drag = new DragEvent(ParentVector(e.Translation) - _origin, ParentVector(e.Velocity), in e);
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

        // Whether a pointer moved this far starts a drag: far enough, and more the way it drags than the other.
        private bool Allows(Vector2 translation)
        {
            float x = Mathf.Abs(translation.x), y = Mathf.Abs(translation.y);
            if (Mathf.Max(x, y) < _threshold) return false;
            return _axis switch
            {
                ScrollAxis.Horizontal => x > y,
                ScrollAxis.Vertical => y > x,
                ScrollAxis.None => false,
                _ => true,
            };
        }

        private Vector2 ParentVector(Vector2 rootVector)
        {
            var parent = Node.Parent;
            return parent != null ? UIInput.ToNodeVector(parent, rootVector) : rootVector;
        }
    }
}
