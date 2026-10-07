using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// States: a button whose fill follows Pressed, Hovered and Disabled (the switch turns Interactable off); a
    /// segmented control whose segments are Selected one at a time, the pill springing to the selected one; and a
    /// custom Warning state set on a panel, which every value inside it with a Warning variant follows.
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

        public void Setup(Pressable button, Pressable disable, List<Pressable> segments, UINode pill, Pressable warn, UINode panel, UIState warning)
        {
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
            _warn.Toggled += on => UI.Animate(new UIAnimation(0.3f), () => _panel.SetState(_warning, on));
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
