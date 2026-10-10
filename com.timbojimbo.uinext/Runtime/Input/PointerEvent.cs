using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>What kind of device a pointer is.</summary>
    public enum PointerKind
    {
        Mouse,
        Touch,
        Pen,
    }

    /// <summary>One moment of a pointer that went down on a node, as the gestures on it and its ancestors hear it.</summary>
    public readonly struct PointerEvent
    {
        /// <summary>Which pointer: the mouse, a pen, or one finger of several.</summary>
        public readonly int PointerId;

        public readonly PointerKind Kind;

        /// <summary>The root it went down in.</summary>
        public readonly UIRoot Root;

        /// <summary>Where it is, in its root's points (x right, y down).</summary>
        public readonly Vector2 Position;

        /// <summary>Where it went down, in its root's points.</summary>
        public readonly Vector2 StartPosition;

        /// <summary>When, in seconds (unscaled).</summary>
        public readonly double Time;

        /// <summary>How fast it is moving, in its root's points a second.</summary>
        public readonly Vector2 Velocity;

        /// <summary>The node it went down on: the deepest under it that takes the pointer.</summary>
        public readonly UINode Target;

        internal PointerEvent(int pointerId, PointerKind kind, UIRoot root, Vector2 position, Vector2 startPosition, double time, Vector2 velocity, UINode target)
        {
            Target = target;
            PointerId = pointerId;
            Kind = kind;
            Root = root;
            Position = position;
            StartPosition = startPosition;
            Time = time;
            Velocity = velocity;
        }

        /// <summary>How far it has moved since it went down, in its root's points.</summary>
        public Vector2 Translation => Position - StartPosition;
    }

    /// <summary>
    /// A component on a node that hears pointers going down on it or anything inside it: from the node under the
    /// pointer up to its root, deepest first, every handler hears the pointer until one claims it
    /// (<see cref="UIInput.Claim"/>), when the others are cancelled.
    /// </summary>
    public interface IPointerHandler
    {
        void OnPointerDown(in PointerEvent e);
        void OnPointerMove(in PointerEvent e);
        void OnPointerUp(in PointerEvent e);

        /// <summary>It will hear no more of the pointer: another handler claimed it, or the press was cut short.</summary>
        void OnPointerCancel(in PointerEvent e);
    }

    /// <summary>A component on a node that is told when the mouse comes over it or anything inside it, and leaves.</summary>
    public interface IHoverHandler
    {
        void OnHoverChanged(bool hovered);
    }

    /// <summary>A component on a node that the mouse wheel scrolls when it is over it or anything inside it.</summary>
    public interface IScrollHandler
    {
        /// <summary>Scrolls by <paramref name="delta"/> points (x right, y down); false to let an ancestor have it.</summary>
        bool OnScroll(Vector2 delta);
    }

    /// <summary>
    /// Something's recent places (a pointer's position, a value a gesture sets), for how fast it was moving: a pointer as
    /// it lets go, a value as the gesture moving it lets go. Up to four components, as a spring moves.
    /// </summary>
    internal sealed class VelocityTracker
    {
        // Samples over this long count, as UIKit's and Android's trackers look back about a tenth of a second.
        private const double Window = 0.1;
        private const int Capacity = 16;

        private readonly Vector4[] _values = new Vector4[Capacity];
        private readonly double[] _times = new double[Capacity];
        private int _count;
        private int _next;

        /// <summary>How many samples it holds.</summary>
        public int Count => _count;

        public void Clear()
        {
            _count = 0;
            _next = 0;
        }

        public void Add(Vector4 value, double time)
        {
            _values[_next] = value;
            _times[_next] = time;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        /// <summary>Units a second, by a least-squares line through the samples on each axis, looking back from the newest.</summary>
        public Vector4 Velocity
        {
            get
            {
                if (_count < 2) return Vector4.zero;
                int newest = (_next - 1 + Capacity) % Capacity;
                double now = _times[newest];
                double sumT = 0, sumTT = 0;
                Vector4 sumP = Vector4.zero, sumTP = Vector4.zero;
                int n = 0;
                for (int k = 0; k < _count; k++)
                {
                    int i = (newest - k + Capacity) % Capacity;
                    double age = now - _times[i];
                    if (age > Window) break;
                    float t = (float)-age;
                    sumT += t;
                    sumTT += t * t;
                    sumP += _values[i];
                    sumTP += _values[i] * t;
                    n++;
                }
                if (n < 2) return Vector4.zero;
                double denominator = n * sumTT - sumT * sumT;
                if (denominator < 1e-9) return Vector4.zero;
                return (sumTP * n - sumP * (float)sumT) / (float)denominator;
            }
        }
    }
}
