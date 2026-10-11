using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A setting with a few values in order (Small, Default, Large), stepped through by the arrows either side of it, or
    /// with left and right while its row has focus: the row is one focus stop (Focusable Always) whose left and right are
    /// its own, and the arrows, for the pointer, take no focus. Its value cross-fades as it changes, and an arrow at its
    /// end fades.
    /// </summary>
    public sealed class Stepper : MonoBehaviour, IFocusMoveHandler
    {
        [SerializeField] private Label _label;
        [SerializeField] private List<string> _values = new();
        [SerializeField] private int _value;
        [SerializeField] private Pressable _less;
        [SerializeField] private Pressable _more;

        /// <summary>Raised as its value changes, with the index of the new one.</summary>
        public event Action<int> Changed;

        /// <summary>The label saying its value, the values in order, the one it starts on, and the arrows back and on.</summary>
        public void Setup(Label label, List<string> values, int value, Pressable less, Pressable more)
        {
            _label = label;
            _values = values;
            _value = value;
            _less = less;
            _more = more;
            Show();
        }

        private void OnEnable()
        {
            _less.Tapped += StepBack;
            _more.Tapped += StepOn;
        }

        private void OnDisable()
        {
            _less.Tapped -= StepBack;
            _more.Tapped -= StepOn;
        }

        /// <summary>One value back (-1) or on (1), stopping at the ends.</summary>
        public void Step(int by)
        {
            int to = Mathf.Clamp(_value + by, 0, _values.Count - 1);
            if (to == _value) return;
            _value = to;
            UI.Animate(UIAnimation.Snappy, Show);
            Changed?.Invoke(to);
        }

        public bool OnFocusMove(Vector2 direction)
        {
            if (direction.x == 0f) return false;
            Step(direction.x > 0f ? 1 : -1);
            return true;
        }

        private void StepBack() => Step(-1);

        private void StepOn() => Step(1);

        private void Show()
        {
            _label.Text.Value = _values[_value];
            _less.Node.Opacity.Value = _value > 0 ? 1f : 0.3f;
            _more.Node.Opacity.Value = _value < _values.Count - 1 ? 1f : 0.3f;
        }
    }
}
