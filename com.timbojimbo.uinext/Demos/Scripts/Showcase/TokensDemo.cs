using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Tokens: the page's colours come from tokens with a value per theme (a mode state, one of a group). Picking a
    /// theme sets its state on the page inside UI.Animate, and every colour from a token springs to the new theme's.
    /// </summary>
    public sealed class TokensDemo : MonoBehaviour
    {
        [SerializeField] private UINode _page;
        [SerializeField] private List<Pressable> _themes = new();
        [SerializeField] private List<UIState> _modes = new();

        public void Setup(UINode page, List<Pressable> themes, List<UIState> modes)
        {
            _page = page;
            _themes = themes;
            _modes = modes;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _themes.Count; i++)
            {
                int index = i;
                _themes[i].Tapped += () => Pick(index);
            }
        }

        private void Pick(int index)
        {
            for (int i = 0; i < _themes.Count; i++)
                _themes[i].IsSelected = i == index;
            UI.Animate(new UIAnimation(0.45f), () =>
            {
                // The first theme is the tokens' defaults: no mode at all.
                for (int i = 0; i < _modes.Count; i++)
                    if (_modes[i] != null)
                        _page.SetState(_modes[i], i == index);
            });
        }
    }
}
