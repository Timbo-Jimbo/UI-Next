using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Presentations: each button presents its own (a sheet, an alert, a cover that fades in over everything); a button
    /// inside one presents the next over it, and its Close buttons dismiss it.
    /// </summary>
    public sealed class PresentDemo : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _buttons = new();
        [SerializeField] private List<Presentation> _presentations = new();
        [SerializeField] private List<Pressable> _closeButtons = new();
        [SerializeField] private List<Presentation> _closes = new();

        public void Setup(List<Pressable> buttons, List<Presentation> presentations, List<Pressable> closeButtons, List<Presentation> closes)
        {
            _buttons = buttons;
            _presentations = presentations;
            _closeButtons = closeButtons;
            _closes = closes;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var presentation = _presentations[i];
                _buttons[i].Tapped += () => presentation.Present();
            }
            for (int i = 0; i < _closeButtons.Count; i++)
            {
                var presentation = _closes[i];
                _closeButtons[i].Tapped += () => presentation.Dismiss();
            }
        }
    }
}
