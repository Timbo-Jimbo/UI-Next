using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>
    /// Arena, the demo game's menus: five pages (Shop, Cards, Battle, Clan, Events) side by side in a paging scroll view
    /// over a tab bar, opening on Battle. Tapping a tab scrolls to its page, and the selected tab widens and lifts its
    /// icon, springing; a swipe between pages selects the tab of the page it comes to rest on. Tapping the selected tab
    /// again scrolls its page to the top. It keeps the wallet: gold and gems, spent and earned by the pages, counting up
    /// or down to what they come to in the top bar.
    /// </summary>
    public sealed class ArenaApp : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _tabs = new();
        [SerializeField] private ScrollView _pages;
        [SerializeField] private List<ScrollView> _pageScrolls = new();
        [SerializeField] private Label _goldLabel;
        [SerializeField] private Label _gemsLabel;
        [SerializeField] private int _first = 2;

        // How fast the counters count, as a share of the way left each second.
        private const float CountRate = 6f;

        private static readonly UIAnimation TabChange = new(0.35f, 0.25f);

        private int _current = -1;
        private bool _placed;
        private int _gold, _gems;
        private float _shownGold, _shownGems;

        public int Gold => _gold;
        public int Gems => _gems;

        public void Setup(List<Pressable> tabs, ScrollView pages, List<ScrollView> pageScrolls, Label gold, Label gems, int first)
        {
            _tabs = tabs;
            _pages = pages;
            _pageScrolls = pageScrolls;
            _goldLabel = gold;
            _gemsLabel = gems;
            _first = first;
        }

        private void Awake()
        {
            // Springs want every frame the screen shows.
            int rate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = rate > 0 ? rate : 60;
            _gold = ArenaData.Gold;
            _gems = ArenaData.Gems;
            _shownGold = _gold;
            _shownGems = _gems;
            ShowWallet();
        }

        private void OnEnable()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                int index = i;
                _tabs[i].Tapped += () => Select(index);
            }
            _pages.Scrolled += OnPagesScrolled;
        }

        private void OnDisable() => _pages.Scrolled -= OnPagesScrolled;

        private void Update()
        {
            // Opens on its first page once the pages are laid out.
            if (!_placed && _pages.Viewport.x > 0f)
            {
                _placed = true;
                _pages.ScrollTo(new Vector2(_first * _pages.Viewport.x, 0f), animated: false);
                SelectTab(_first, animated: false);
            }
            float step = 1f - Mathf.Exp(-CountRate * Time.unscaledDeltaTime);
            if (Mathf.Abs(_shownGold - _gold) > 0.5f || Mathf.Abs(_shownGems - _gems) > 0.5f)
            {
                _shownGold = Mathf.Abs(_shownGold - _gold) < 1f ? _gold : Mathf.Lerp(_shownGold, _gold, step);
                _shownGems = Mathf.Abs(_shownGems - _gems) < 1f ? _gems : Mathf.Lerp(_shownGems, _gems, step);
                ShowWallet();
            }
        }

        /// <summary>Takes gold and gems from the wallet, if it holds that much: false, and nothing taken, if it does not.</summary>
        public bool Spend(int gold, int gems)
        {
            if (gold > _gold || gems > _gems) return false;
            _gold -= gold;
            _gems -= gems;
            return true;
        }

        /// <summary>Puts gold and gems in the wallet.</summary>
        public void Earn(int gold, int gems)
        {
            _gold += gold;
            _gems += gems;
        }

        private void ShowWallet()
        {
            if (_goldLabel != null) _goldLabel.Text.Value = ArenaData.Number(Mathf.RoundToInt(_shownGold));
            if (_gemsLabel != null) _gemsLabel.Text.Value = ArenaData.Number(Mathf.RoundToInt(_shownGems));
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
            SelectTab(index, animated: true);
            _pages.ScrollTo(new Vector2(index * _pages.Viewport.x, 0f));
        }

        // A swipe between pages: the tab follows to the page it comes to rest on.
        private void OnPagesScrolled(Vector2 offset)
        {
            if (!_placed || _pages.IsDragging || _pages.Viewport.x <= 0f) return;
            int page = Mathf.Clamp(Mathf.RoundToInt(offset.x / _pages.Viewport.x), 0, _tabs.Count - 1);
            if (page != _current) SelectTab(page, animated: true);
        }

        private void SelectTab(int index, bool animated)
        {
            _current = index;
            if (!animated)
            {
                for (int i = 0; i < _tabs.Count; i++)
                    _tabs[i].IsSelected = i == index;
                return;
            }
            UI.Animate(TabChange, () =>
            {
                for (int i = 0; i < _tabs.Count; i++)
                    _tabs[i].IsSelected = i == index;
            });
        }
    }
}
