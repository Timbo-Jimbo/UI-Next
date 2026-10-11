using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>What a shop offer sells.</summary>
    public enum OfferKind
    {
        Deal,
        Chest,
        Gold,
        Gems,
    }

    /// <summary>An offer in the shop, as the builder made it: what it is, its button, and its price.</summary>
    [Serializable]
    public sealed class OfferParts
    {
        public OfferKind Kind;
        public int Index;
        public Pressable Button;
        public UINode Tile;
        public Label Price;
        public UINode Art;
    }

    /// <summary>The buy dialog, as the builder made it.</summary>
    [Serializable]
    public sealed class BuyDialogParts
    {
        public Presentation Presentation;
        public Label Icon;
        public Label Title;
        public Label Detail;
        public Pressable Buy;
        public Label BuyLabel;
        public Pressable Cancel;
    }

    /// <summary>
    /// The Shop page: the daily deals (cards for gold, sold out once bought), chests and gold for gems, and gems for
    /// money (not sold in a demo). An offer asks in a dialog first; bought, cards go into the collection, gold into the
    /// wallet, and a chest opens at once, growing out of the offer into the chest popup.
    /// </summary>
    public sealed class ShopPage : MonoBehaviour
    {
        [SerializeField] private List<OfferParts> _offers = new();
        [SerializeField] private BuyDialogParts _dialog = new();
        [SerializeField] private ArenaApp _app;
        [SerializeField] private CardCollection _cards;
        [SerializeField] private ChestSlots _chests;

        private static readonly UIAnimation Change = new(0.35f, 0.2f);

        private readonly HashSet<int> _sold = new();
        private int _asking = -1;

        public void Setup(List<OfferParts> offers, BuyDialogParts dialog, ArenaApp app, CardCollection cards, ChestSlots chests)
        {
            _offers = offers;
            _dialog = dialog;
            _app = app;
            _cards = cards;
            _chests = chests;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                int index = i;
                _offers[i].Button.Tapped += () => Ask(index);
            }
            _dialog.Buy.Tapped += OnBuy;
            _dialog.Cancel.Tapped += Close;
        }

        private void OnDisable()
        {
            _dialog.Buy.Tapped -= OnBuy;
            _dialog.Cancel.Tapped -= Close;
        }

        private void Close() => _dialog.Presentation.Dismiss();

        // What an offer gives and costs: its picture, its name, what it says, and its price in gold and gems.
        private (string Icon, string Title, string Detail, int Gold, int Gems, bool ForSale) Describe(OfferParts offer)
        {
            switch (offer.Kind)
            {
                case OfferKind.Deal:
                {
                    var (card, count, price) = ArenaData.Deals[offer.Index];
                    var data = ArenaData.Cards[card];
                    return (data.Emoji, data.Name, "x" + count + " " + data.Rarity + " cards", price, 0, true);
                }
                case OfferKind.Chest:
                {
                    var (kind, price) = ArenaData.ShopChests[offer.Index];
                    return ("🎁", ArenaData.ChestName(kind), "Opens at once", 0, price, true);
                }
                case OfferKind.Gold:
                {
                    var (gold, price) = ArenaData.GoldOffers[offer.Index];
                    return ("💰", ArenaData.Number(gold) + " Gold", "Straight into your wallet", 0, price, true);
                }
                default:
                {
                    var (gems, price) = ArenaData.GemOffers[offer.Index];
                    return ("💎", ArenaData.Number(gems) + " Gems", price + " (not for sale in a demo)", 0, 0, false);
                }
            }
        }

        private void Ask(int index)
        {
            if (_dialog.Presentation.IsPresented || _sold.Contains(index)) return;
            _asking = index;
            var (icon, title, detail, gold, gems, forSale) = Describe(_offers[index]);
            _dialog.Icon.Text.Value = icon;
            _dialog.Title.Text.Value = title;
            _dialog.Detail.Text.Value = detail;
            _dialog.Buy.Interactable.Value = forSale;
            _dialog.BuyLabel.Text.Value = !forSale ? "Not for sale" : gold == 0 && gems == 0 ? "Free!"
                : gold > 0 ? "Buy  💰 " + ArenaData.Number(gold) : "Buy  💎 " + ArenaData.Number(gems);
            _dialog.Presentation.Present();
        }

        private void OnBuy()
        {
            if (_asking < 0) return;
            var offer = _offers[_asking];
            var (_, _, _, gold, gems, forSale) = Describe(offer);
            if (!forSale) return;
            if (!_app.Spend(gold, gems))
            {
                UI.Animate(Change, () => _dialog.BuyLabel.Text.Value = gold > 0 ? "Not enough gold" : "Not enough gems");
                return;
            }
            Close();
            switch (offer.Kind)
            {
                case OfferKind.Deal:
                {
                    var (card, count, _) = ArenaData.Deals[offer.Index];
                    _cards.Add(card, count);
                    int sold = _asking;
                    _sold.Add(sold);
                    UI.Animate(Change, () =>
                    {
                        offer.Price.Text.Value = "Sold out";
                        offer.Button.Interactable.Value = false;
                        offer.Tile.Saturation.Value = 0.2f;
                    });
                    break;
                }
                case OfferKind.Chest:
                    _chests.OpenBought(ArenaData.ShopChests[offer.Index].Kind, offer.Art);
                    break;
                case OfferKind.Gold:
                    _app.Earn(ArenaData.GoldOffers[offer.Index].Gold, 0);
                    break;
            }
            _asking = -1;
        }
    }
}
