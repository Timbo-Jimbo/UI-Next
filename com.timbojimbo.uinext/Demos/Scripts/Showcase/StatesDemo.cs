using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// States: a button whose fill follows Pressed, Hovered and Disabled (the switch turns Interactable off); a
    /// segmented control whose segments are Selected one at a time, the pill springing to the selected one; a custom
    /// Warning state set on a panel, which every value inside it with a Warning variant follows, moving as the state's
    /// own enter and exit animations say; and a tile that tells a tap from a long press.
    /// </summary>
    public sealed class StatesDemo : MonoBehaviour
    {
        [SerializeField] private Pressable _button;
        [SerializeField] private Pressable _disable;
        [SerializeField] private List<Pressable> _segments = new();
        [SerializeField] private UINode _pill;
        [SerializeField] private Pressable _warn;
        [SerializeField] private UINode _panel;
        [SerializeField] private UIState _warning;
        [SerializeField] private Pressable _hold;
        [SerializeField] private Label _holdLabel;

        public void Setup(Pressable button, Pressable disable, List<Pressable> segments, UINode pill, Pressable warn, UINode panel, UIState warning,
            Pressable hold, Label holdLabel)
        {
            _hold = hold;
            _holdLabel = holdLabel;
            _button = button;
            _disable = disable;
            _segments = segments;
            _pill = pill;
            _warn = warn;
            _panel = panel;
            _warning = warning;
        }

        private void OnEnable()
        {
            _disable.Toggled += on => _button.Interactable.Value = !on;
            for (int i = 0; i < _segments.Count; i++)
            {
                int index = i;
                _segments[i].Tapped += () => Select(index);
            }
            // No Animate: the state's own enter and exit animations move what it changes.
            _warn.Toggled += on => _panel.SetState(_warning, on);
            _hold.Tapped += () => UI.Animate(UIAnimation.Snappy, () => _holdLabel.Text.Value = "Tapped");
            _hold.LongPressed += () => UI.Animate(UIAnimation.Snappy, () => _holdLabel.Text.Value = "Long pressed");
        }

        private void Select(int index)
        {
            for (int i = 0; i < _segments.Count; i++)
                _segments[i].IsSelected = i == index;
            var floating = _pill.Floating.Peek;
            floating.Element = _segments[index].Node;
            UI.Animate(UIAnimation.Snappy, () => _pill.Floating.Value = floating);
        }
    }
}
