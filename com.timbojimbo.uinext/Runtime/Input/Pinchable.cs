using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>A pinch as <see cref="Pinchable"/> reports it.</summary>
    public readonly struct PinchEvent
    {
        /// <summary>How far apart the two pointers are now, against how far apart they were as it began.</summary>
        public readonly float Scale;

        /// <summary>How far the line between them has turned since it began, in degrees (clockwise on screen).</summary>
        public readonly float Rotation;

        /// <summary>The point between them, in their root's points.</summary>
        public readonly Vector2 Center;

        /// <summary>The point between them as it began, in their root's points.</summary>
        public readonly Vector2 StartCenter;

        /// <summary>How fast the scale is changing, a second.</summary>
        public readonly float Velocity;

        internal PinchEvent(float scale, float rotation, Vector2 center, Vector2 startCenter, float velocity)
        {
            Scale = scale;
            Rotation = rotation;
            Center = center;
            StartCenter = startCenter;
            Velocity = velocity;
        }

        /// <summary>How far the point between them has moved since it began, in their root's points.</summary>
        public Vector2 Translation => Center - StartCenter;
    }

    /// <summary>
    /// A two-finger pinch on a node, as UIKit's UIPinchGestureRecognizer and UIRotationGestureRecognizer together: once a
    /// second pointer goes down on the node (or inside it) while a first is down there, it takes both (cancelling presses
    /// and drags under them) and reports how far they have spread, turned and moved, for zooming a photo or a map. A first
    /// pointer something else has taken already (a drag under way) is not pinched.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UINode))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Pinchable")]
    public sealed class Pinchable : MonoBehaviour, IPointerHandler
    {
        // The two pointers, where they went down and where they are, in their root's points.
        private readonly int[] _ids = { -1, -1 };
        private readonly Vector2[] _at = new Vector2[2];
        private bool _pinching;
        private float _startDistance;
        private float _startAngle;
        private Vector2 _startCenter;
        private float _lastScale;
        private double _lastTime;
        private float _velocity;
        private UINode _node;

        /// <summary>Raised as the second pointer comes down and the pinch begins.</summary>
        public event Action<PinchEvent> Began;

        /// <summary>Raised as either pointer moves.</summary>
        public event Action<PinchEvent> Changed;

        /// <summary>Raised as either pointer lets go, with how fast the scale was changing.</summary>
        public event Action<PinchEvent> Ended;

        /// <summary>Raised when it is cut short (a pointer taken, the node disabled).</summary>
        public event Action Cancelled;

        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        /// <summary>Whether a pinch is under way.</summary>
        public bool IsPinching => _pinching;

        private void OnDisable()
        {
            if (_pinching) Cancelled?.Invoke();
            Reset();
        }

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            int slot = _ids[0] < 0 ? 0 : _ids[1] < 0 ? 1 : -1;
            if (slot < 0 || _pinching) return;
            _ids[slot] = e.PointerId;
            _at[slot] = e.Position;
            if (_ids[0] < 0 || _ids[1] < 0) return;

            // Two down: both are the pinch's, unless the first has been taken already.
            if (!UIInput.Claim(this, _ids[0]))
            {
                Forget(0);
                return;
            }
            UIInput.Claim(this, _ids[1]);
            _pinching = true;
            _startDistance = Mathf.Max(1f, Vector2.Distance(_at[0], _at[1]));
            _startAngle = Angle();
            _startCenter = (_at[0] + _at[1]) * 0.5f;
            _lastScale = 1f;
            _lastTime = e.Time;
            _velocity = 0f;
            Began?.Invoke(Event());
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e)
        {
            int slot = SlotOf(e.PointerId);
            if (slot < 0) return;
            _at[slot] = e.Position;
            if (!_pinching) return;
            var pinch = Event();
            // The scale's speed, smoothed over the last moves.
            float dt = (float)(e.Time - _lastTime);
            if (dt > 1e-4f)
            {
                _velocity = Mathf.Lerp(_velocity, (pinch.Scale - _lastScale) / dt, 0.6f);
                _lastScale = pinch.Scale;
                _lastTime = e.Time;
            }
            Changed?.Invoke(Event());
        }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            int slot = SlotOf(e.PointerId);
            if (slot < 0) return;
            _at[slot] = e.Position;
            if (_pinching)
            {
                var pinch = Event();
                Reset();
                Ended?.Invoke(pinch);
                return;
            }
            Forget(slot);
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            int slot = SlotOf(e.PointerId);
            if (slot < 0) return;
            if (_pinching)
            {
                Reset();
                Cancelled?.Invoke();
                return;
            }
            Forget(slot);
        }

        private PinchEvent Event()
        {
            float scale = Vector2.Distance(_at[0], _at[1]) / _startDistance;
            float rotation = Mathf.DeltaAngle(_startAngle, Angle());
            return new PinchEvent(scale, rotation, (_at[0] + _at[1]) * 0.5f, _startCenter, _velocity);
        }

        // The angle of the line from the first pointer to the second, in degrees, clockwise on screen (y down).
        private float Angle()
        {
            var d = _at[1] - _at[0];
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        private int SlotOf(int pointerId) => _ids[0] == pointerId ? 0 : _ids[1] == pointerId ? 1 : -1;

        // A pointer that is not part of a pinch: the other, if down, moves into the first place.
        private void Forget(int slot)
        {
            if (slot == 0)
            {
                _ids[0] = _ids[1];
                _at[0] = _at[1];
            }
            _ids[1] = -1;
        }

        private void Reset()
        {
            _ids[0] = _ids[1] = -1;
            _pinching = false;
        }
    }
}
