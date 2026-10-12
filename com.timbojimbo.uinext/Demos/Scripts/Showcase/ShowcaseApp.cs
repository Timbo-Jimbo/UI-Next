using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>The showcase: a list of the stack's features, each opening its page, pushed over the list.</summary>
    public sealed class ShowcaseApp : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _entries = new();
        [SerializeField] private List<Presentation> _pages = new();
        [SerializeField] private List<Pressable> _backButtons = new();

        public void Setup(List<Pressable> entries, List<Presentation> pages, List<Pressable> backButtons)
        {
            _entries = entries;
            _pages = pages;
            _backButtons = backButtons;
        }

        private void Awake()
        {
            int rate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = rate > 0 ? rate : 60;

            // The Rich text page's code font, made as the showcase starts rather than as the page first opens.
            Markup.CodeFont.Prewarm();
        }

        private void OnEnable()
        {
            for (int i = 0; i < _entries.Count && i < _pages.Count; i++)
            {
                var page = _pages[i];
                _entries[i].Tapped += () => page.Present();
            }
            for (int i = 0; i < _backButtons.Count && i < _pages.Count; i++)
            {
                var page = _pages[i];
                _backButtons[i].Tapped += () => page.Dismiss();
            }
        }
    }
}
