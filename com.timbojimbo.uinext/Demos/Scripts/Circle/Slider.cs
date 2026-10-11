using System;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A slider: a value from 0 to 1, as wide a part of its track as its fill, with a knob riding the fill's end. A drag
    /// along it puts the value under the finger (a drag up or down goes on to the page), a tap on it jumps there, and left
    /// and right step it while it has focus: its track is one focus stop (Focusable Always) whose left and right are its
    /// own, and at its end the focus ring shakes no.
    /// </summary>
    [RequireComponent(typeof(Draggable))]
    public sealed class Slider : MonoBehaviour, IPointerHandler, IFocusMoveHandler
    {
        [SerializeField] private UINode _fill;
        [SerializeField, Range(0f, 1f)] private float _value = 0.5f;
        [SerializeField, Range(0.01f, 1f)] private float _step = 0.1f;

        private Draggable _drag;
        private UINode _node;
        private int _pointer = -1;

        /// <summary>Raised as its value changes.</summary>
        public event Action<float> Changed;

        public float Value => _value;

        /// <summary>The fill, which is as wide a part of the track as the value, and the value it starts at.</summary>
        public void Setup(UINode fill, float value)
        {
            _fill = fill;
            _value = Mathf.Clamp01(value);
            Show();
        }

        private void OnEnable()
        {
            _node = GetComponent<UINode>();
            _drag = GetComponent<Draggable>();
            _drag.Axis = ScrollAxis.Horizontal;
            // The value stays where the finger leaves it.
            _drag.TracksVelocity = false;
            _drag.Began += Follow;
            _drag.Changed += Follow;
        }

        private void OnDisable()
        {
            _drag.Began -= Follow;
            _drag.Changed -= Follow;
            _pointer = -1;
        }

        public FocusMoveResult OnFocusMove(Vector2 direction)
        {
            if (direction.x == 0f) return FocusMoveResult.Ignored;
            float to = Mathf.Clamp01(_value + Mathf.Sign(direction.x) * _step);
            if (Mathf.Approximately(to, _value)) return FocusMoveResult.Blocked;
            Set(to, animated: true);
            return FocusMoveResult.Handled;
        }

        // Set directly as the finger moves, so the knob is under it.
        private void Follow(DragEvent e) => Set(ValueAt(e.Pointer.Position), animated: false);

        // The value at a point in its root's points: how far along its track, inside its padding.
        private float ValueAt(Vector2 point)
        {
            var padding = _node.Padding.Peek;
            float width = _node.Size.x - padding.Horizontal;
            return width > 0f ? Mathf.Clamp01((_node.RootToLocal(point).x - padding.Left) / width) : _value;
        }

        private void Set(float value, bool animated)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, _value)) return;
            _value = value;
            if (animated)
                UI.Animate(UIAnimation.Snappy, Show);
            else
                Show();
            Changed?.Invoke(value);
        }

        private void Show() => _fill.Width.Value = Sizing.Percent(_value);

        // A tap, which no drag took: the value jumps there.
        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer < 0)
                _pointer = e.PointerId;
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e) { }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            Set(ValueAt(e.Position), animated: true);
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            if (e.PointerId == _pointer)
                _pointer = -1;
        }
    }
}
