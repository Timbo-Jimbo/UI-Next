using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Arena;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class ArenaBuilder
    {
        // ── Shop ─────────────────────────────────────────────────────────────────

        // Shop: the daily deals, chests, gold and gems, three to a row, each asking in the buy dialog. See ShopPage.
        private static void Shop(UINode pages, Parts parts)
        {
            var body = ScrollingBody(Page(pages, "Shop"), parts);

            Heading(body, "Daily Deals", "Refreshes in 7h 12m");
            var deals = Grid(body, 3);
            for (int i = 0; i < ArenaData.Deals.Length; i++)
            {
                var (card, count, price) = ArenaData.Deals[i];
                var tile = Offer(deals, ArenaData.Cards[card].Name, OfferKind.Deal, i, parts);
                CardFace(tile.Tile, card, 62f, out _, out _, out _).MatchName.Value = "";
                Text(tile.Tile, "Count", "x" + count, 16f);
                tile.Price = PriceTag(tile.Tile, price == 0 ? "Free!" : "💰 " + ArenaData.Number(price), price == 0 ? Green : Yellow);
            }

            Heading(body, "Chests");
            var chests = Grid(body, 3);
            for (int i = 0; i < ArenaData.ShopChests.Length; i++)
            {
                var (kind, price) = ArenaData.ShopChests[i];
                var tile = Offer(chests, ArenaData.ChestName(kind), OfferKind.Chest, i, parts);
                var art = tile.Tile.Child("Chest").Sized(Sizing.Fixed(72f), Sizing.Fixed(62f));
                art.MatchName.Value = "Chest";
                art.MatchId.Value = "shop " + i;
                art.Add<ChestArt>().Kind.Value = kind;
                tile.Art = art;
                Text(tile.Tile, "Name", ArenaData.ChestName(kind).Replace(" Chest", ""), 13f);
                tile.Price = PriceTag(tile.Tile, "💎 " + price, Blue);
            }

            Heading(body, "Gold");
            var gold = Grid(body, 3);
            for (int i = 0; i < ArenaData.GoldOffers.Length; i++)
            {
                var (amount, price) = ArenaData.GoldOffers[i];
                var tile = Offer(gold, "Gold " + (i + 1), OfferKind.Gold, i, parts);
                tile.Tile.Child("Icon").Label(i == 0 ? "💰" : i == 1 ? "💰💰" : "🏦", 34f);
                Text(tile.Tile, "Amount", ArenaData.Number(amount), 15f, Gold);
                tile.Price = PriceTag(tile.Tile, "💎 " + ArenaData.Number(price), Blue);
            }

            Heading(body, "Gems");
            var gems = Grid(body, 3);
            for (int i = 0; i < ArenaData.GemOffers.Length; i++)
            {
                var (amount, price) = ArenaData.GemOffers[i];
                var tile = Offer(gems, "Gems " + (i + 1), OfferKind.Gems, i, parts);
                tile.Tile.Child("Icon").Label(i == 0 ? "💎" : i == 1 ? "💎💎" : "👑", 34f);
                Text(tile.Tile, "Amount", ArenaData.Number(amount), 15f);
                tile.Price = PriceTag(tile.Tile, price, Green);
            }
        }

        // An offer's tile: a raised panel, the whole of it a button, squashing as it is pressed. Added to the shop's offers.
        private static OfferParts Offer(UINode grid, string name, OfferKind kind, int index, Parts parts)
        {
            var tile = grid.Child(name).Column(6f).Pad(new Insets(6f, 6f, 10f, 8f)).Align(AlignX.Center, AlignY.Top);
            tile.Width.Value = Sizing.Grow();
            Raised(tile, new Color32(0x5B, 0x92, 0xEC, 0xFF), new Color32(0x2D, 0x5C, 0xBE, 0xFF), PanelEdge, 12f, 3f);
            tile.Scale.When(InteractionStates.Pressed, 0.94f);
            var offer = new OfferParts { Kind = kind, Index = index, Tile = tile, Button = tile.Pressable() };
            parts.Offers.Add(offer);
            return offer;
        }

        // A price at the foot of an offer: a small raised tag in its colour (returns its label).
        private static Label PriceTag(UINode tile, string price, (Color Top, Color Bottom, Color Edge) colours)
        {
            var tag = tile.Child("Price").Center();
            tag.Sized(Sizing.Grow(), Sizing.Fixed(30f));
            Raised(tag, colours.Top, colours.Bottom, colours.Edge, 9f, 2f);
            tag.HitTestable.Value = false;
            return Text(tag, "Text", price, 14f);
        }
    }
}
