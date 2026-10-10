using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// Circle, the demo app: three pages (Home, Chats, Me) side by side in a paging scroll view, under a floating tab
    /// bar. Tapping a tab scrolls to its page and springs the selection pill (floating on the selected tab) across;
    /// swiping between pages moves the pill when the page settles. Tapping the selected tab again scrolls its page to
    /// the top. Feed photos open in the viewer, flying out of the feed; the Dark mode switch turns the Dark mode state on
    /// at the root, and every colour from a token follows, animated. Log out grows its dialog out of its button: the two
    /// share a match name.
    /// </summary>
    public sealed class CircleApp : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _tabs = new();
        [SerializeField] private UINode _pill;
        [SerializeField] private ScrollView _pages;
        [SerializeField] private List<ScrollView> _pageScrolls = new();
        [SerializeField] private List<ScenePhoto> _photos = new();
        [SerializeField] private List<string> _captions = new();
        [SerializeField] private PhotoViewer _viewer;
        [SerializeField] private UIState _dark;
        [SerializeField] private ToggleSwitch _darkSwitch;
        [SerializeField] private Pressable _logOut;
        [SerializeField] private Presentation _logOutDialog;
        [SerializeField] private List<Pressable> _logOutButtons = new();

        private int _current;

        public void Setup(List<Pressable> tabs, UINode pill, ScrollView pages, List<ScrollView> pageScrolls, List<ScenePhoto> photos,
            List<string> captions, PhotoViewer viewer, UIState dark, ToggleSwitch darkSwitch, Pressable logOut, Presentation logOutDialog,
            List<Pressable> logOutButtons)
        {
            _tabs = tabs;
            _pill = pill;
            _pages = pages;
            _pageScrolls = pageScrolls;
            _photos = photos;
            _captions = captions;
            _viewer = viewer;
            _dark = dark;
            _darkSwitch = darkSwitch;
            _logOut = logOut;
            _logOutDialog = logOutDialog;
            _logOutButtons = logOutButtons;
        }

        private void Awake()
        {
            // Springs want every frame the screen shows.
            int rate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = rate > 0 ? rate : 60;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                _tabs[i].Tapped += () => Select(index);
            }
            for (int i = 0; i < _photos.Count; i++)
            {
                int index = i;
                var pressable = _photos[i].GetComponent<Pressable>();
                // A post's photo is matched by the post's index, which its post gives it.
                if (pressable != null)
                    pressable.Tapped += () => _viewer.Open(_photos[index], _captions[index], index.ToString());
            }
            _pages.Scrolled += OnPagesScrolled;
            _darkSwitch.Changed += SetDark;
            _logOut.Tapped += () => _logOutDialog.Present();
            foreach (var button in _logOutButtons)
                button.Tapped += () => _logOutDialog.Dismiss();
        }

        private void OnDisable()
        {
            _pages.Scrolled -= OnPagesScrolled;
            _darkSwitch.Changed -= SetDark;
        }

        /// <summary>Shows the <paramref name="index"/>th page; the page showing already scrolls back to its top.</summary>
        public void Select(int index)
        {
            if (index == _current)
            {
                if (index < _pageScrolls.Count && _pageScrolls[index] != null)
                    _pageScrolls[index].ScrollToTop();
                return;
            }
            MovePill(index);
            _pages.ScrollTo(new Vector2(index * _pages.Viewport.x, 0f));
        }

        // A swipe between pages: the pill follows to the page it comes to rest on.
        private void OnPagesScrolled(Vector2 offset)
        {
            if (_pages.IsDragging || _pages.Viewport.x <= 0f) return;
            int page = Mathf.Clamp(Mathf.RoundToInt(offset.x / _pages.Viewport.x), 0, _tabs.Count - 1);
            if (page != _current) MovePill(page);
        }

        private void MovePill(int index)
        {
            _current = index;
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].IsSelected = i == index;
            var floating = _pill.Floating.Peek;
            floating.Element = _tabs[index].Node;
            UI.Animate(UIAnimation.Snappy, () => _pill.Floating.Value = floating);
        }

        private void SetDark(bool on)
        {
            var root = GetComponent<UINode>();
            UI.Animate(new UIAnimation(0.5f), () => root.SetState(_dark, on));
        }
    }
}
