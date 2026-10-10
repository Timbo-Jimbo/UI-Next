using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Momentum or precision: two stepped sliders, the same but for their drag's TracksVelocity. Let go of while moving,
    /// each knob snaps to the step nearest where it was let go on the same spring: the one whose drag hands its speed on
    /// carries on past the step and comes back; the precise one lands on the step the finger chose.
    /// </summary>
    public sealed class MomentumDemo : MonoBehaviour
    {
        [SerializeField] private List<Draggable> _knobs = new();
        [SerializeField, Min(2)] private int _steps = 5;

        private float _from;

        public void Setup(List<Draggable> knobs, int steps)
        {
            _knobs = knobs;
            _steps = steps;
        }

        private void OnEnable()
        {
            foreach (var knob in _knobs)
            {
                var drag = knob;
                drag.Began += e =>
                {
                    _from = drag.Node.Offset.Peek.x;
                    Move(drag, e);
                };
                drag.Changed += e => Move(drag, e);
                drag.Ended += e => Snap(drag);
                drag.Cancelled += () => Snap(drag);
            }
        }

        // How far the knob can go along its track.
        private static float Range(Draggable drag)
        {
            var knob = drag.Node;
            var track = knob.Parent;
            return track != null ? Mathf.Max(0f, track.LaidOutRect.width - track.Padding.Peek.Horizontal - knob.LaidOutRect.width) : 0f;
        }

        private void Move(Draggable drag, DragEvent e) =>
            drag.Node.Offset.Value = new Vector2(Mathf.Clamp(_from + e.Translation.x, 0f, Range(drag)), 0f);

        private void Snap(Draggable drag)
        {
            var node = drag.Node;
            float step = Range(drag) / (_steps - 1);
            float x = step > 0f ? Mathf.Round(node.Offset.Peek.x / step) * step : 0f;
            UI.Animate(UIAnimation.Smooth, () => node.Offset.Value = new Vector2(x, 0f));
        }
    }
}
