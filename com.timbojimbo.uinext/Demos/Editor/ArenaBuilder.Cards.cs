using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Arena;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class ArenaBuilder
    {
        // ── Cards ────────────────────────────────────────────────────────────────

        // Cards: the battle deck of eight and its average cost, then the rest of the collection; every card opens into the
        // card popup. See CardsPage.
        private static void Cards(UINode pages, Parts parts)
        {
            var body = ScrollingBody(Page(pages, "Cards"), parts);

            Heading(body, "Battle Deck", "Deck 1");
            var deck = body.Child("Deck").Column(10f).Pad(10f);
            deck.Width.Value = Sizing.Grow();
            Panel(deck);
            var deckGrid = Grid(deck, 4);
            float elixir = 0f;
            foreach (int card in ArenaData.Deck)
            {
                parts.Tiles.Add(CardTile(deckGrid, card));
                elixir += ArenaData.Cards[card].Elixir;
            }
            var average = deck.Child("Average").Row(6f).Align(AlignX.Center, AlignY.Center);
            average.Width.Value = Sizing.Grow();
            average.Child("Drop").Label("💧", 16f);
            Text(average, "Text", "Average Elixir: " + (elixir / ArenaData.Deck.Length).ToString("0.0"), 14f);

            var rest = new List<int>();
            for (int card = 0; card < ArenaData.Cards.Length; card++)
                if (System.Array.IndexOf(ArenaData.Deck, card) < 0)
                    rest.Add(card);
            Heading(body, "Card Collection", "Found: " + ArenaData.Cards.Length + "/24");
            var collection = body.Child("Collection").Column(10f).Pad(10f);
            collection.Width.Value = Sizing.Grow();
            Panel(collection);
            var collectionGrid = Grid(collection, 4);
            foreach (int card in rest)
                parts.Tiles.Add(CardTile(collectionGrid, card));
        }

        // A grid of `columns` as wide as its parent.
        private static UINode Grid(UINode parent, int columns)
        {
            var grid = parent.Child("Grid").Row(8f);
            grid.Width.Value = Sizing.Grow();
            grid.Wrap.Value = Wrap.Grid(columns);
            return grid;
        }

        // A card in the deck or the collection: its face (named for the popup to grow out of), its level along its foot,
        // and its copies towards the next level under it. CardsPage fills them in.
        private static CardTileParts CardTile(UINode grid, int card)
        {
            var tile = grid.Child(ArenaData.Cards[card].Name).Column(5f).Align(AlignX.Center, AlignY.Top);
            tile.Width.Value = Sizing.Grow();
            tile.Scale.When(InteractionStates.Pressed, 0.94f);
            var frame = CardFace(tile, card, 0f, out _, out _, out _);
            var foot = frame.Child("Foot").Float(AttachPoint.CenterBottom, AttachPoint.CenterBottom);
            foot.Sized(Sizing.Percent(1f), Sizing.Fixed(18f)).Center();
            foot.Box(new Color(0f, 0f, 0f, 0.45f), new CornerRadii(0f, 0f, 10f, 10f));
            var level = Text(foot, "Level", "", 11f);
            var bar = Bar(tile, 0.5f, Green.Bottom, 14f, "", out var fill, out var fillBox, out var progress);
            bar.Width.Value = Sizing.Grow();
            return new CardTileParts
            {
                Card = card,
                Button = tile.Pressable(),
                Frame = frame,
                Level = level,
                Fill = fill,
                FillBox = fillBox,
                Progress = progress,
            };
        }
    }
}
