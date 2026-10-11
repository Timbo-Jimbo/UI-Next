using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>A card on the Cards page, as the builder made it: its frame, its level, and its copies towards the next.</summary>
    [Serializable]
    public sealed class CardTileParts
    {
        public int Card;
        public Pressable Button;
        public UINode Frame;
        public Label Level;
        public UINode Fill;
        public Box FillBox;
        public Label Progress;
    }

    /// <summary>The card popup, as the builder made it.</summary>
    [Serializable]
    public sealed class CardPopupParts
    {
        public Presentation Presentation;
        public UINode Frame;
        public Box FrameBox;
        public Label Icon;
        public Label Elixir;
        public Label Name;
        public Label Rarity;
        public Label Level;
        public Label Hitpoints;
        public Label Damage;
        public UINode Fill;
        public Box FillBox;
        public Label Progress;
        public Pressable Upgrade;
        public Label UpgradeLabel;
        public Pressable Close;
    }

    /// <summary>
    /// The Cards page: the deck and the collection, each card showing its level and its copies towards the next, its bar
    /// green once it has enough. A card opens into the popup, its frame growing out of the card's (they share a match name
    /// and the card's index), with its stats; Upgrade spends the copies and gold for a level, the stats and the card's
    /// own tile following as it springs.
    /// </summary>
    public sealed class CardsPage : MonoBehaviour
    {
        [SerializeField] private List<CardTileParts> _tiles = new();
        [SerializeField] private CardPopupParts _popup = new();
        [SerializeField] private ArenaApp _app;
        [SerializeField] private CardCollection _cards;

        private static readonly Color Ready = new Color32(0x6F, 0xD8, 0x4A, 0xFF);
        private static readonly Color Filling = new Color32(0x4A, 0x9B, 0xF5, 0xFF);
        private static readonly UIAnimation Pop = new(0.45f, 0.45f);

        private int _open = -1;

        public void Setup(List<CardTileParts> tiles, CardPopupParts popup, ArenaApp app, CardCollection cards)
        {
            _tiles = tiles;
            _popup = popup;
            _app = app;
            _cards = cards;
        }

        private void OnEnable()
        {
            for (int t = 0; t < _tiles.Count; t++)
            {
                int index = t;
                _tiles[t].Button.Tapped += () => Open(index);
            }
            _cards.Changed += OnCardChanged;
            _popup.Upgrade.Tapped += OnUpgrade;
            _popup.Close.Tapped += Close;
            _popup.Presentation.Dismissing += OnDismissing;
        }

        private void OnDisable()
        {
            _cards.Changed -= OnCardChanged;
            _popup.Upgrade.Tapped -= OnUpgrade;
            _popup.Close.Tapped -= Close;
            _popup.Presentation.Dismissing -= OnDismissing;
        }

        private void Start()
        {
            foreach (var tile in _tiles)
                ShowTile(tile);
        }

        private void OnCardChanged(int card)
        {
            foreach (var tile in _tiles)
                if (tile.Card == card)
                    ShowTile(tile);
            if (_open >= 0 && _tiles[_open].Card == card)
                FillPopup();
        }

        private void ShowTile(CardTileParts tile)
        {
            int card = tile.Card;
            tile.Level.Text.Value = "Level " + _cards.Level(card);
            Bar(tile.Fill, tile.FillBox, tile.Progress, card);
        }

        // A card's copies towards its next level: a bar as full as they are, green once there are enough.
        private void Bar(UINode fill, Box fillBox, Label label, int card)
        {
            int count = _cards.Count(card), needed = _cards.Needed(card);
            fill.Width.Value = Sizing.Percent(Mathf.Clamp01((float)count / needed));
            fillBox.Fill.Value = count >= needed ? Ready : Filling;
            label.Text.Value = count >= needed ? "Upgrade!" : ArenaData.Number(count) + "/" + ArenaData.Number(needed);
        }

        /// <summary>Opens the <paramref name="t"/>th tile's card into the popup, its frame growing out of the tile's.</summary>
        public void Open(int t)
        {
            if (_popup.Presentation.IsPresented) return;
            _open = t;
            var tile = _tiles[t];
            _popup.Frame.MatchId.Value = tile.Frame.MatchId.Peek;
            FillPopup();
            UI.Animate(_popup.Presentation.Animation, () =>
            {
                tile.Frame.Display.Value = DisplayMode.Hidden;
                _popup.Presentation.Present();
            });
        }

        private void Close() => _popup.Presentation.Dismiss();

        // Inside the change that takes the popup away: the tile's frame comes back for the popup's to go into.
        private void OnDismissing()
        {
            if (_open >= 0)
                _tiles[_open].Frame.Display.Value = DisplayMode.Visible;
            _open = -1;
        }

        private void FillPopup()
        {
            int card = _tiles[_open].Card;
            var (name, emoji, elixir, rarity, _, _, _) = ArenaData.Cards[card];
            var (top, bottom) = ArenaData.RarityColours(rarity);
            int level = _cards.Level(card);
            var (hitpoints, damage) = ArenaData.Stats(card, level);
            _popup.FrameBox.Fill.Value = Color.Lerp(top, bottom, 0.5f);
            _popup.Icon.Text.Value = emoji;
            _popup.Elixir.Text.Value = elixir.ToString();
            _popup.Name.Text.Value = name;
            _popup.Rarity.Text.Value = rarity.ToString();
            _popup.Rarity.Color.Value = top;
            _popup.Level.Text.Value = "Level " + level;
            _popup.Hitpoints.Text.Value = ArenaData.Number(hitpoints);
            _popup.Damage.Text.Value = ArenaData.Number(damage);
            Bar(_popup.Fill, _popup.FillBox, _popup.Progress, card);
            bool can = _cards.CanUpgrade(card);
            _popup.Upgrade.Interactable.Value = can;
            _popup.UpgradeLabel.Text.Value = can ? "Upgrade  💰 " + ArenaData.Number(ArenaData.UpgradeCost(level)) : "Collect more cards";
        }

        // Spends the copies and the gold for a level, the card popping as the level, stats and bars spring to theirs.
        private void OnUpgrade()
        {
            if (_open < 0) return;
            int card = _tiles[_open].Card;
            if (!_cards.CanUpgrade(card)) return;
            int cost = ArenaData.UpgradeCost(_cards.Level(card));
            if (!_app.Spend(cost, 0))
            {
                UI.Animate(Pop, () => _popup.UpgradeLabel.Text.Value = "Not enough gold");
                return;
            }
            var frame = _popup.Frame;
            UI.Animate(new UIAnimation(0.12f), () => frame.Scale.Value = 1.15f).Finished += () => UI.Animate(Pop, () => frame.Scale.Value = 1f);
            UI.Animate(Pop, () => _cards.Upgrade(card));
        }
    }
}
