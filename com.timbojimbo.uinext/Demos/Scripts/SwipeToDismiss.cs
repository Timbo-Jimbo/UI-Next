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
    /// draggable's axis) follows the finger, or shrinks the panel in place, and raises <see cref="Progress"/> for whatever
    /// fades or rounds with it; let go far or fast enough, it dismisses its presentation, and otherwise the panel springs
    /// back. The panel springs home inside the presentation's own changes, so thrown away it carries on at the speed of
    /// the throw as its presentation takes it out, and presented again mid-throw it slides back from where it is. A scroll
    /// view inside it at its end hands it the rest of a drag, and takes it back as the finger turns.
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

        // Let go past this much of the way, or flicked away this fast (points a second), it is dismissed.
        private const float Far = 0.35f;
        private const float Fast = 600f;

        // How small it shrinks to, all the way.
        private const float Smallest = 0.75f;

        private Draggable _drag;
        private UINode _node;

        /// <summary>How far it has been swiped, 0 to 1: raised as it moves, and inside the change that springs it back (0) or takes it away (1).</summary>
        public event Action<float> Progress;

        public void Setup(Presentation presentation, SwipeDirections directions, bool shrinks = false, float distance = 300f)
        {
            if (isActiveAndEnabled) Unwatch();
            _presentation = presentation;
            _directions = directions;
            _shrinks = shrinks;
            _distance = distance;
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
            float progress = Mathf.Clamp01(moved.magnitude / _distance);
            if (_shrinks)
                _node.Scale.Value = Mathf.Lerp(1f, Smallest, progress);
            else
                _node.Offset.Value = moved;
            Progress?.Invoke(progress);
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
            _node.Offset.Value = Vector2.zero;
            _node.Scale.Value = 1f;
            Progress?.Invoke(0f);
        }

        // Inside the dismissal: the panel springs home as the presentation takes it out, whatever went with it going on.
        private void Away()
        {
            _node.Offset.Value = Vector2.zero;
            _node.Scale.Value = 1f;
            Progress?.Invoke(1f);
        }
    }
}
