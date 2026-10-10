using System;
using UnityEngine;

namespace TimboJimbo.UINext.Demos
{
    /// <summary>The ways a swipe can take a panel away.</summary>
    [Flags]
    public enum SwipeDirections
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        Any = Left | Right | Up | Down,
    }

    /// <summary>
    /// The one component behind every swipe to dismiss in the demos. A drag the ways it allows (chosen within its
    /// draggable's axis) moves the panel with the finger, or shrinks it in place, and raises <see cref="Progress"/> for
    /// whatever fades or rounds with it; let go far or fast enough, it dismisses its presentation, and otherwise the panel
    /// springs back. The panel goes as far as the finger up to where letting go takes it away, and past that only part of
    /// the way, less the further it goes, with a scroll view's rubber band. The panel is its own node, or one inside it,
    /// so a drag anywhere in a presentation, on its backdrop too, takes the panel away. The panel springs home inside the
    /// presentation's own changes, so thrown away it carries on at the speed of the throw as its presentation takes it
    /// out, and presented again mid-throw it slides back from where it is. A scroll view inside it at its end hands it the
    /// rest of a drag, and takes it back as the finger turns.
    /// </summary>
    [RequireComponent(typeof(Draggable))]
    public sealed class SwipeToDismiss : MonoBehaviour
    {
        [SerializeField] private Presentation _presentation;

        [Tooltip("The ways a swipe takes it away, within its draggable's axis.")]
        [SerializeField] private SwipeDirections _directions = SwipeDirections.Down;

        [Tooltip("Shrinks in place as it is swiped, rather than following the finger.")]
        [SerializeField] private bool _shrinks;

        [Tooltip("How far a swipe goes to take it all the way, in points.")]
        [SerializeField, Min(1f)] private float _distance = 300f;

        [Tooltip("What a swipe moves (empty: its own node): a panel inside it, so a drag anywhere around the panel moves it.")]
        [SerializeField] private UINode _panel;

        // Let go past this much of the way, or flicked away this fast (points a second), it is dismissed.
        private const float Far = 0.35f;
        private const float Fast = 600f;

        // Past where letting go takes it away, how much of the way a drag goes, as a scroll view's rubber band (iOS's).
        private const float RubberBand = 0.55f;

        // How small it shrinks to, all the way.
        private const float Smallest = 0.75f;

        private Draggable _drag;
        private UINode _node;

        // What it moves.
        private UINode Panel => _panel != null ? _panel : _node;

        /// <summary>How far it has been swiped, 0 to 1: raised as it moves, and inside the change that springs it back (0) or takes it away (1).</summary>
        public event Action<float> Progress;

        public void Setup(Presentation presentation, SwipeDirections directions, bool shrinks = false, float distance = 300f,
            UINode panel = null)
        {
            if (isActiveAndEnabled) Unwatch();
            _presentation = presentation;
            _directions = directions;
            _shrinks = shrinks;
            _distance = distance;
            _panel = panel;
            if (isActiveAndEnabled) Watch();
        }

        private void OnEnable()
        {
            _drag = GetComponent<Draggable>();
            _node = _drag.Node;
            _drag.Takes = Takes;
            _drag.Began += Follow;
            _drag.Changed += Follow;
            _drag.Ended += LetGo;
            _drag.Cancelled += SpringBack;
            Watch();
        }

        private void OnDisable()
        {
            _drag.Takes = null;
            _drag.Began -= Follow;
            _drag.Changed -= Follow;
            _drag.Ended -= LetGo;
            _drag.Cancelled -= SpringBack;
            Unwatch();
        }

        private void Watch()
        {
            if (_presentation == null) return;
            _presentation.Presenting += Rest;
            _presentation.Dismissing += Away;
        }

        private void Unwatch()
        {
            if (_presentation == null) return;
            _presentation.Presenting -= Rest;
            _presentation.Dismissing -= Away;
        }

        // A drag it takes goes mostly one of the ways it allows.
        private bool Takes(Vector2 translation)
        {
            var way = Mathf.Abs(translation.x) >= Mathf.Abs(translation.y)
                ? translation.x > 0f ? SwipeDirections.Right : SwipeDirections.Left
                : translation.y > 0f ? SwipeDirections.Down : SwipeDirections.Up;
            return (_directions & way) != 0;
        }

        // The part of a movement the ways it allows: swiped right only, it never goes left.
        private Vector2 Allowed(Vector2 movement)
        {
            var towards = (movement.x > 0f ? SwipeDirections.Right : SwipeDirections.Left) | (movement.y > 0f ? SwipeDirections.Down : SwipeDirections.Up);
            return new Vector2(
                (_directions & towards & (SwipeDirections.Left | SwipeDirections.Right)) != 0 ? movement.x : 0f,
                (_directions & towards & (SwipeDirections.Up | SwipeDirections.Down)) != 0 ? movement.y : 0f);
        }

        // Set directly as the finger moves: the drag hands their speed on as it lets go.
        private void Follow(DragEvent e)
        {
            var moved = Allowed(e.Translation);
            float distance = moved.magnitude;
            var direction = distance > 0f ? moved / distance : Vector2.zero;
            // The rubber band's size: its node's along the way the finger went, as a scroll view's is its own.
            var size = _node.LaidOutRect.size;
            float shown = Shown(distance, Mathf.Abs(direction.x) * size.x + Mathf.Abs(direction.y) * size.y);
            float progress = Mathf.Clamp01(shown / _distance);
            if (_shrinks)
                Panel.Scale.Value = Mathf.Lerp(1f, Smallest, progress);
            else
                Panel.Offset.Value = direction * shown;
            Progress?.Invoke(progress);
        }

        // How far a drag of `distance` points moves it: as far as the finger up to where letting go takes it away, and
        // past that only part of the way, less the further it goes, as a scroll view `size` big stretches past its end.
        private float Shown(float distance, float size)
        {
            float free = Far * _distance;
            if (distance <= free) return distance;
            size = Mathf.Max(1f, size);
            return free + (1f - 1f / ((distance - free) * RubberBand / size + 1f)) * size;
        }

        private void LetGo(DragEvent e)
        {
            var moved = Allowed(e.Translation);
            var speed = Allowed(e.Velocity);
            bool far = moved.magnitude / _distance > Far;
            bool flicked = speed.magnitude > Fast && Vector2.Dot(speed, moved) > 0f;
            if ((far || flicked) && _presentation != null && _presentation.IsPresented)
                _presentation.Dismiss();
            else
                SpringBack();
        }

        private void SpringBack() => UI.Animate(UIAnimation.Snappy, Rest);

        // At rest, inside a change: springing back, or presented again.
        private void Rest()
        {
            Panel.Offset.Value = Vector2.zero;
            Panel.Scale.Value = 1f;
            Progress?.Invoke(0f);
        }

        // Inside the dismissal: the panel springs home as the presentation takes it out, whatever went with it going on.
        private void Away()
        {
            Panel.Offset.Value = Vector2.zero;
            Panel.Scale.Value = 1f;
            Progress?.Invoke(1f);
        }
    }
}
