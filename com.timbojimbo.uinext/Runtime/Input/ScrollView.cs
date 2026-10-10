using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>What a drag does past a scroll view's end.</summary>
    public enum ScrollEnd
    {
        /// <summary>It stretches past the end and bounces back, as iOS's do.</summary>
        Bounce,

        /// <summary>The rest of the same drag goes to what is around it that takes it: a scroll view it is in, or a draggable.</summary>
        PassOn,
    }

    /// <summary>
    /// Makes a node scroll what is inside it, as UIScrollView: layout lets its children run past its edge the way it
    /// scrolls, it clips them, and drags move them, gliding on when let go and slowing as iOS's do, bouncing back into
    /// range, and coming to rest on a page when it pages. The mouse wheel scrolls it. A new drag heading past an end it is
    /// already at goes to a scroll view it is in that can still scroll that way, as nested scroll views chain on the web:
    /// a new drag at the bottom of a list in a page scrolls the page. Past an end, a drag stretches it and it bounces back;
    /// or, if it passes drags on (<see cref="AtEnd"/>), the rest of the same drag goes to what is around it that takes it,
    /// as the scroll view in an iOS sheet hands the drag to the sheet: a scroll view it is in that can still go that way,
    /// or a <see cref="Draggable"/> that takes it (which hands it back as the finger turns); with nothing to take it, it
    /// stretches. The wheel at an end scrolls a scroll view it is in. A pointer that goes down while it moves catches it,
    /// and does not press what is under it; caught bouncing back from past an end, a drag heading further past it goes
    /// where a new drag from that end would, and it springs back. It draws its scroll indicators over what it holds while
    /// it moves.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Timbo Jimbo/UI Next/Scroll View")]
    public sealed class ScrollView : UIDrawable, IPointerHandler, IScrollHandler
    {
        [SerializeField] private UIValue<ScrollAxis> _axis = ScrollAxis.Vertical;

        [Tooltip("Whether it stretches past its ends while dragged and bounces back, as iOS's do.")]
        [SerializeField] private UIValue<bool> _bounces = true;

        [Tooltip("Past an end: stretch and bounce back, or pass the rest of the drag to what is around it that takes it.")]
        [SerializeField] private UIValue<ScrollEnd> _atEnd = ScrollEnd.Bounce;

        [Tooltip("Pages: it comes to rest on a whole page of what it shows.")]
        [SerializeField] private UIValue<ScrollSnap> _snap = ScrollSnap.None;

        [SerializeField] private UIValue<bool> _showsIndicators = true;
        [SerializeField] private UIValue<Color> _indicatorColor = new Color(0f, 0f, 0f, 0.35f);

        // How far a pointer moves before a drag begins, in points.
        private const float Slop = 8f;

        // iOS's: how much of the way past its end a drag goes, and how fast a glide slows (per millisecond).
        private const float RubberBand = 0.55f;
        private const float Deceleration = 0.998f;

        // A glide slower than this, in points a second, stops.
        private const float RestSpeed = 8f;

        private static readonly UIAnimation BounceBack = new(0.5f);
        private static readonly UIAnimation PageTurn = new(0.4f);
        private static readonly UIAnimation WheelStep = new(0.25f);
        private static readonly UIAnimation IndicatorFade = new(0.3f, 0f, 0.6f);

        private static readonly List<ScrollView> s_gliding = new();

        // How far its children are scrolled (x right, y down), with where it is drawn while it springs or glides.
        [NonSerialized] internal readonly AnimatedValue<Vector2> Offset = new(Vector2.zero, 0.25f);
        [NonSerialized] private readonly AnimatedValue<float> _indicatorOpacity = new(0f, 0.01f);

        [NonSerialized] private int _pointer = -1;
        [NonSerialized] private bool _dragging;
        // Where the drag is measured from, in its root's points: where the pointer went down, or where the drag was
        // passed to it.
        [NonSerialized] private Vector2 _start;
        [NonSerialized] private Vector2 _dragFrom;
        [NonSerialized] private Vector2 _dragOrigin;
        // Whether the drag set off within its range: one that caught it past an end, pushing it back, passes nothing on.
        [NonSerialized] private bool _startedInRange;
        // Caught past an end as the pointer went down: the drag's way decides whose it is.
        [NonSerialized] private bool _caughtPastEnd;
        [NonSerialized] private bool _gliding;
        [NonSerialized] private Vector2 _velocity;
        [NonSerialized] private Vector2 _pageFrom;

        /// <summary>Raised whenever it is scrolled, by a drag, a glide, the wheel or code, with its offset as set.</summary>
        public event Action<Vector2> Scrolled;

        /// <summary>Which way it scrolls.</summary>
        public UIValue<ScrollAxis> Axis => _axis;

        public UIValue<bool> Bounces => _bounces;

        /// <summary>What a drag does past an end: stretches it, which bounces back, or goes on to what is around it that takes it.</summary>
        public UIValue<ScrollEnd> AtEnd => _atEnd;
        public UIValue<ScrollSnap> Snap => _snap;
        public UIValue<bool> ShowsIndicators => _showsIndicators;
        public UIValue<Color> IndicatorColor => _indicatorColor;

        /// <summary>Its size, as last laid out.</summary>
        public Vector2 Viewport { get; private set; }

        /// <summary>How big what it holds is, padding included.</summary>
        public Vector2 ContentSize { get; private set; }

        /// <summary>How far it can scroll each way.</summary>
        public Vector2 Range => new(
            Scrolls(0) ? Mathf.Max(0f, ContentSize.x - Viewport.x) : 0f,
            Scrolls(1) ? Mathf.Max(0f, ContentSize.y - Viewport.y) : 0f);

        /// <summary>How far its children are scrolled, where it is going (see <see cref="PresentedOffset"/> for as drawn).</summary>
        public Vector2 ContentOffset
        {
            get => Offset.Peek;
            set => ScrollTo(value, animated: false);
        }

        /// <summary>How far its children are scrolled as drawn now.</summary>
        public Vector2 PresentedOffset => Offset.Presented;

        /// <summary>Whether a drag is moving it.</summary>
        public bool IsDragging => _dragging;

        /// <summary>Whether it is moving on its own: gliding, bouncing back or turning a page.</summary>
        public bool IsDecelerating => _gliding || Offset.IsAnimating;

        /// <summary>
        /// Scrolls to <paramref name="offset"/> (kept in range), springing there from where it is drawn at the speed it
        /// has unless told not to: inside a change (<see cref="UI.Animate(UIAnimation, Action)"/>) on the change's
        /// animation, as part of it, as SwiftUI scrolls inside withAnimation; outside one on a spring of its own, its
        /// indicators flashing.
        /// </summary>
        public void ScrollTo(Vector2 offset, bool animated = true)
        {
            StopGlide();
            var target = Clamp(offset);
            if (!animated)
            {
                Offset.Jump(target);
            }
            else if (UITransaction.Current != null)
            {
                Offset.SetModel(target, null);
            }
            else
            {
                Offset.SpringTo(target, Offset.Velocity, UIAnimation.Default);
                Flash();
            }
            Scrolled?.Invoke(target);
        }

        /// <summary>Scrolls back to its start.</summary>
        public void ScrollToTop(bool animated = true) => ScrollTo(Vector2.zero, animated);

        /// <summary>
        /// Moves the offset by <paramref name="delta"/> without changing how it moves (a glide, a spring or a drag carries
        /// on from there): a list whose rows above the view changed height keeps what is in view where it is, as
        /// UITableView adjusts its offset as estimated rows are measured.
        /// </summary>
        internal void ShiftBy(Vector2 delta)
        {
            if (delta == Vector2.zero) return;
            Offset.Shift(delta);
            _dragFrom += delta;
            _pageFrom += delta;
        }

        /// <summary>Scrolls the least it takes to show <paramref name="node"/>, a node inside it, whole (or its start, if it is bigger).</summary>
        public void ScrollIntoView(UINode node, bool animated = true)
        {
            if (node == null) return;
            // Where the node is laid out in what this scrolls: its rect, moved by every node between it and this.
            var rect = node.LaidOutRect;
            for (var parent = node.Parent; parent != null && parent != Node; parent = parent.Parent)
                rect.position += parent.LaidOutRect.position;
            var offset = ContentOffset;
            for (int axis = 0; axis < 2; axis++)
            {
                if (!Scrolls(axis)) continue;
                float min = rect.position[axis], max = min + rect.size[axis], view = Viewport[axis];
                if (min < offset[axis]) offset[axis] = min;
                else if (max > offset[axis] + view) offset[axis] = Mathf.Min(min, max - view);
            }
            ScrollTo(offset, animated);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            Node.Scroller = this;
            Node.Tree?.InvalidateLayout();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopGlide();
            _pointer = -1;
            _dragging = false;
            _caughtPastEnd = false;
            if (Node.Scroller == this)
            {
                Node.Scroller = null;
                Node.Tree?.InvalidateLayout();
            }
        }

        private bool Scrolls(int axis)
        {
            var value = _axis.Peek;
            return axis == 0 ? value is ScrollAxis.Horizontal or ScrollAxis.Both : value is ScrollAxis.Vertical or ScrollAxis.Both;
        }

        // After layout: its size and what it holds, which its range comes from. An offset left past the end by
        // what it holds shrinking comes back into range (springing, if the layout change animates).
        internal void SetExtent(Vector2 viewport, Vector2 content, Insets reach)
        {
            Viewport = viewport;
            ContentSize = content;
            if (_dragging || _gliding || _caughtPastEnd) return;
            var offset = Offset.Peek;
            var clamped = Clamp(offset);
            if (clamped != offset)
                Offset.SetModel(clamped, null);
        }

        private Vector2 Clamp(Vector2 offset)
        {
            var range = Range;
            return new Vector2(Mathf.Clamp(offset.x, 0f, range.x), Mathf.Clamp(offset.y, 0f, range.y));
        }

        private Vector2 Mask(Vector2 vector) => new(Scrolls(0) ? vector.x : 0f, Scrolls(1) ? vector.y : 0f);

        /// <summary>A movement with only the parts of it along the way it scrolls.</summary>
        internal Vector2 Along(Vector2 vector) => Mask(vector);

        // ── Dragging ─────────────────────────────────────────────────────────────

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer >= 0) return;
            _pointer = e.PointerId;
            _start = e.StartPosition;
            // Moving on its own: the pointer catches it where it is drawn, and takes the drag from the start. Caught past
            // an end, the drag's way decides whose it is, as it does for a new drag at that end.
            if (IsDecelerating && UIInput.Claim(this, e.PointerId))
            {
                Catch();
                _caughtPastEnd = Clamp(Offset.Peek) != Offset.Peek;
                if (!_caughtPastEnd)
                    BeginDrag(Vector2.zero);
            }
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            var moved = UIInput.ToNodeVector(Node, e.Position - _start);
            if (!_dragging)
            {
                if (!WantsDrag(moved)) return;
                if (_caughtPastEnd)
                {
                    _caughtPastEnd = false;
                    if (HandOn(in e, e.Position - _start)) return;
                }
                else if (OuterTakes(e.Position - _start))
                {
                    return;
                }
                if (!UIInput.Claim(this, e.PointerId)) return;
                // The content follows from here, so it does not jump by the slop.
                BeginDrag(Mask(moved));
            }
            Drag(in e, Mask(moved));
        }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            if (LetGoCaught() || !_dragging) return;
            _dragging = false;
            Release(-Mask(UIInput.ToNodeVector(Node, e.Velocity)));
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            if (LetGoCaught() || !_dragging) return;
            _dragging = false;
            Release(Vector2.zero);
        }

        // Whether a pointer moved this far (in its own space) is a drag it takes: far enough, mostly its way.
        private bool WantsDrag(Vector2 moved)
        {
            float x = Mathf.Abs(moved.x), y = Mathf.Abs(moved.y);
            if (Mathf.Max(x, y) < Slop) return false;
            var range = Range;
            bool horizontal = Scrolls(0) && x >= y, vertical = Scrolls(1) && y >= x;
            if (_axis.Peek == ScrollAxis.Both) return true;
            // Nothing to scroll that way and no bouncing: let an ancestor have it.
            if (horizontal) return range.x > 0f || _bounces.Peek;
            if (vertical) return range.y > 0f || _bounces.Peek;
            return false;
        }

        // Moves what it holds against the finger, at its speed. Past an end, a drag that set off within its range goes on to
        // what is around it that takes it, if it passes drags on; otherwise it stretches.
        private void Drag(in PointerEvent e, Vector2 translation)
        {
            var target = _dragFrom - (translation - _dragOrigin);
            var end = Clamp(target);
            if (end != target && _atEnd.Peek == ScrollEnd.PassOn && _startedInRange && PassOn(in e, target - end, end)) return;
            Offset.MoveAt(Stretch(target), -Mask(UIInput.ToNodeVector(Node, e.Velocity)));
            Scrolled?.Invoke(Offset.Peek);
        }

        // Past its end by `over` (its offset's units): the rest of the drag goes to the nearest thing around it that takes
        // it, as the scroll view in an iOS sheet hands the drag to the sheet, and this stops at its end. False with nothing
        // to take it.
        private bool PassOn(in PointerEvent e, Vector2 over, Vector2 end)
        {
            // The finger has gone as far past the end as the offset would have, the other way.
            var excess = RootVector(-over);
            var to = Taker(excess);
            return to != null && Pass(in e, excess, end, to);
        }

        // The nearest thing around it that takes a drag going `way` (its root's points): a scroll view it is in that can
        // still go that way, or a draggable that takes the way the finger goes. What is below a presentation never takes
        // its drags. Null with none.
        private IPointerHandler Taker(Vector2 way)
        {
            for (var parent = Node.Parent; parent != null; parent = parent.Parent)
            {
                var outer = parent.Scroller;
                if (outer != null && outer.isActiveAndEnabled && outer._pointer < 0
                    && outer.CanScrollToward(UIInput.ToNodeVector(parent, way)))
                    return outer;
                if (parent.TryGetComponent(out Draggable draggable) && draggable.Receives(way))
                    return draggable;
                if (parent.PresentationLevel.Peek != 0) break;
            }
            return null;
        }

        // Caught past an end, a drag heading further past it goes where a new drag from that end would: to a scroll view it
        // is in that can still go that way or, if this passes drags on, to the nearest thing around it that takes it. That
        // one takes the drag from where the finger is, and this springs back to its end. False if the drag is this one's.
        private bool HandOn(in PointerEvent e, Vector2 rootTranslation)
        {
            if (CanScrollToward(UIInput.ToNodeVector(Node, rootTranslation))) return false;
            if (!OuterTakes(rootTranslation) && _atEnd.Peek != ScrollEnd.PassOn) return false;
            var to = Taker(rootTranslation);
            if (to == null || !UIInput.Pass(this, to, e.PointerId)) return false;
            _pointer = -1;
            SpringBack();
            if (to is ScrollView scroll) scroll.Receive(in e, e.Position);
            else ((Draggable)to).Receive(in e, e.Position, this);
            return true;
        }

        // Caught past an end and let go before it was dragged: it springs back to its end. False if it was not.
        private bool LetGoCaught()
        {
            if (!_caughtPastEnd) return false;
            _caughtPastEnd = false;
            SpringBack();
            return true;
        }

        // Caught past an end and not dragged: it springs back to its end, or to the page there.
        private void SpringBack()
        {
            _pageFrom = Clamp(Offset.Peek);
            Release(Vector2.zero);
        }

        private bool Pass(in PointerEvent e, Vector2 excess, Vector2 end, IPointerHandler to)
        {
            if (!UIInput.Pass(this, to, e.PointerId)) return false;
            _pointer = -1;
            _dragging = false;
            Offset.MoveAt(end, Vector4.zero);
            Scrolled?.Invoke(end);
            HideIndicators();
            // The rest of the drag is measured from where the finger was as this reached its end.
            var start = e.Position - excess;
            if (to is ScrollView scroll) scroll.Receive(in e, start);
            else ((Draggable)to).Receive(in e, start, this);
            return true;
        }

        /// <summary>
        /// Takes the rest of a drag, measured from <paramref name="start"/> (its root's points): one a scroll view inside it
        /// passes on at its end, or one a draggable it passed a drag to hands back as the finger turns.
        /// </summary>
        internal void Receive(in PointerEvent e, Vector2 start)
        {
            _pointer = e.PointerId;
            _start = start;
            if (IsDecelerating)
                Catch();
            BeginDrag(Vector2.zero);
            Drag(in e, Mask(UIInput.ToNodeVector(Node, e.Position - start)));
        }

        // Moving on its own, it stops where it is drawn, still.
        private void Catch()
        {
            var at = Offset.Presented;
            StopGlide();
            Offset.SetModel(at, null);
            Offset.Settle();
        }

        // A vector in its own space as one in its root's points, as it is drawn.
        private Vector2 RootVector(Vector2 local)
        {
            var node = Node;
            return node.Tree != null && node.Index >= 0 ? node.Tree.World[node.Index].ApplyVector(local) : local;
        }

        // A new drag heading past the end this is already at goes to a scroll view it is in that can still scroll that way,
        // as nested scroll views chain on the web: a new drag at the bottom of a list in a page scrolls the page. With none
        // that can, it is this one's. A drag already taken stays with the one that took it.
        private bool OuterTakes(Vector2 rootTranslation)
        {
            if (CanScrollToward(UIInput.ToNodeVector(Node, rootTranslation))) return false;
            for (var parent = Node.Parent; parent != null; parent = parent.Parent)
            {
                var outer = parent.Scroller;
                if (outer != null && outer.isActiveAndEnabled && outer.CanScrollToward(UIInput.ToNodeVector(parent, rootTranslation)))
                    return true;
                // What is below a presentation never takes its drags.
                if (parent.PresentationLevel.Peek != 0) break;
            }
            return false;
        }

        // Whether a drag of `moved` (in its own space) has room to scroll it, along the axis the drag mostly follows: the
        // offset moves against the finger.
        private bool CanScrollToward(Vector2 moved)
        {
            int axis = Mathf.Abs(moved.x) >= Mathf.Abs(moved.y) ? 0 : 1;
            if (!Scrolls(axis)) return false;
            float offset = Offset.Peek[axis];
            return moved[axis] < 0f ? offset < Range[axis] - 0.5f : offset > 0.5f;
        }

        private void BeginDrag(Vector2 origin)
        {
            _dragging = true;
            _dragOrigin = origin;
            _dragFrom = Offset.Peek;
            _startedInRange = Clamp(_dragFrom) == _dragFrom;
            _pageFrom = Clamp(_dragFrom);
            ShowIndicators();
        }

        // Past either end a drag goes only part of the way, less the further it goes, as iOS's rubber band.
        private Vector2 Stretch(Vector2 offset)
        {
            var range = Range;
            bool bounces = _bounces.Peek;
            for (int axis = 0; axis < 2; axis++)
            {
                float value = offset[axis], max = range[axis], size = Mathf.Max(1f, Viewport[axis]);
                if (value < 0f) offset[axis] = bounces ? -Band(-value, size) : 0f;
                else if (value > max) offset[axis] = bounces ? max + Band(value - max, size) : max;
            }
            return offset;
        }

        private static float Band(float distance, float size) => (1f - 1f / (distance * RubberBand / size + 1f)) * size;

        // Let go at a velocity (points a second the offset was changing): it pages, glides or bounces back.
        private void Release(Vector2 velocity)
        {
            var offset = Offset.Peek;
            if (_snap.Peek == ScrollSnap.Pages)
            {
                var page = PageFor(offset, velocity);
                Offset.SpringTo(page, velocity, PageTurn);
                HideIndicators();
                Scrolled?.Invoke(page);
                return;
            }
            var clamped = Clamp(offset);
            if (clamped != offset)
            {
                Offset.SpringTo(clamped, velocity, BounceBack);
                HideIndicators();
                return;
            }
            if (velocity.magnitude < RestSpeed)
            {
                // Let go of still: it lands where it is.
                Offset.MoveAt(offset, Vector4.zero);
                HideIndicators();
                return;
            }
            _velocity = velocity;
            if (!_gliding)
            {
                _gliding = true;
                s_gliding.Add(this);
            }
        }

        // The page a release comes to rest on: the one it is nearest, thrown on one page at most by a flick.
        private Vector2 PageFor(Vector2 offset, Vector2 velocity)
        {
            var range = Range;
            var page = offset;
            for (int axis = 0; axis < 2; axis++)
            {
                float size = Mathf.Max(1f, Viewport[axis]);
                if (!Scrolls(axis) || range[axis] <= 0f) continue;
                float from = Mathf.Round(_pageFrom[axis] / size);
                float projected = (offset[axis] + velocity[axis] * 0.15f) / size;
                float target = Mathf.Clamp(Mathf.Round(projected), from - 1f, from + 1f);
                page[axis] = Mathf.Clamp(target * size, 0f, range[axis]);
            }
            return page;
        }

        // ── Gliding ──────────────────────────────────────────────────────────────

        /// <summary>Moves every gliding scroll view <paramref name="dt"/> seconds on: once a frame, before springs step.</summary>
        internal static void StepAll(float dt)
        {
            for (int i = s_gliding.Count - 1; i >= 0; i--)
            {
                var view = s_gliding[i];
                if (view == null || !view.isActiveAndEnabled || !view._gliding)
                {
                    s_gliding.RemoveAt(i);
                    continue;
                }
                view.Glide(dt);
            }
        }

        private void Glide(float dt)
        {
            // Exactly, as the velocity decays by the rate each millisecond: v(t) = v0 r^(1000t).
            float decay = Mathf.Pow(Deceleration, dt * 1000f);
            float k = 1000f * Mathf.Log(Deceleration);
            var offset = Offset.Peek + _velocity * ((decay - 1f) / k);
            _velocity *= decay;

            var clamped = Clamp(offset);
            if (clamped != offset)
            {
                var velocity = _velocity;
                StopGlide();
                if (_bounces.Peek)
                {
                    // It runs on past the end with the speed it had, and springs back.
                    Offset.SetModel(offset, null);
                    Offset.SpringTo(clamped, velocity, BounceBack);
                }
                else
                {
                    Offset.SetModel(clamped, null);
                }
                HideIndicators();
                Scrolled?.Invoke(Offset.Peek);
                return;
            }
            bool resting = _velocity.magnitude < RestSpeed;
            Offset.MoveAt(offset, resting ? Vector2.zero : _velocity);
            Scrolled?.Invoke(offset);
            if (resting)
            {
                StopGlide();
                HideIndicators();
            }
        }

        private void StopGlide()
        {
            if (!_gliding) return;
            _gliding = false;
            _velocity = Vector2.zero;
            s_gliding.Remove(this);
            // Stopped, it is still.
            Offset.MoveAt(Offset.Peek, Vector4.zero);
        }

        // ── Wheel ────────────────────────────────────────────────────────────────

        bool IScrollHandler.OnScroll(Vector2 delta)
        {
            delta = Mask(UIInput.ToNodeVector(Node, delta));
            if (delta == Vector2.zero) return false;
            var from = _gliding || Offset.IsAnimating ? Offset.Model : Offset.Peek;
            var target = Clamp(from + delta);
            // Already at the end that way: an ancestor may scroll instead.
            if (target == Clamp(from)) return false;
            StopGlide();
            Offset.SpringTo(target, Offset.Velocity, WheelStep);
            Flash();
            Scrolled?.Invoke(target);
            return true;
        }

        // ── Indicators ───────────────────────────────────────────────────────────

        private void ShowIndicators()
        {
            _indicatorOpacity.SetModel(1f, null);
            _indicatorOpacity.Settle();
        }

        private void HideIndicators() => UI.Animate(IndicatorFade, () => _indicatorOpacity.Value = 0f);

        private void Flash()
        {
            ShowIndicators();
            HideIndicators();
        }

        protected internal override void Draw(Painter painter)
        {
            if (!_showsIndicators.Value) return;
            float opacity = _indicatorOpacity.Value;
            if (opacity <= 0.001f) return;
            var offset = Offset.Value;
            var color = _indicatorColor.Value;
            color.a *= opacity;
            var overlay = painter.Overlay;
            var viewport = painter.Size;
            var range = Range;
            const float thickness = 3f, inset = 3f, shortest = 32f;
            for (int axis = 0; axis < 2; axis++)
            {
                if (!Scrolls(axis) || range[axis] <= 0.5f) continue;
                float along = viewport[axis], content = Mathf.Max(along, ContentSize[axis]);
                float track = along - inset * 2f;
                float length = Mathf.Max(shortest, track * along / content);
                // Past an end it shrinks, as iOS's do.
                float over = offset[axis] < 0f ? -offset[axis] : offset[axis] > range[axis] ? offset[axis] - range[axis] : 0f;
                length = Mathf.Max(thickness * 2f, length - over);
                float t = Mathf.Clamp01(offset[axis] / range[axis]);
                float start = inset + t * (track - length);
                var rect = axis == 1
                    ? new Rect(viewport.x - inset - thickness, start, thickness, length)
                    : new Rect(start, viewport.y - inset - thickness, length, thickness);
                overlay.Rect(rect, color, thickness * 0.5f);
            }
        }
    }
}
