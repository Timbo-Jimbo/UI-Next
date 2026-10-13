using System.Collections.Generic;
using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    /// <summary>
    /// Layout solving only what can have moved, taking the rest whole (see UITree.LayOut): every pass is checked against
    /// solving every node (UIDebug.VerifyLayout), which counts each node laid out anywhere else.
    /// </summary>
    public sealed class LayoutTests : UITestScene
    {
        private int _mismatches;

        [SetUp]
        public void Verify()
        {
            UIDebug.VerifyLayout = true;
            _mismatches = UITree.LayoutMismatches;
        }

        [TearDown]
        public void StopVerifying() => UIDebug.VerifyLayout = false;

        [Test]
        public void ALabelCountingUpLaysOutItsRowNotTheFeedBelowIt()
        {
            var column = Root.Node.Column(8f).Pad(16f);
            var header = column.Child("Header").Row(8f).Sized(Sizing.Grow(), Sizing.Fit());
            header.Child("Title").Label("Title", 20f);
            header.Child("Spacer").Sized(Sizing.Grow(), Sizing.Fixed(1f));
            var counter = header.Child("Counter").Label("0", 15f);
            var feed = column.Child("Feed").Column(8f).Sized(Sizing.Grow(), Sizing.Grow());
            for (int i = 0; i < 30; i++)
            {
                var card = feed.Child("Card " + i).Column(4f).Pad(8f).Sized(Sizing.Grow(), Sizing.Fit());
                card.Child("Name").Label("Someone " + i, 15f);
                card.Child("Body").Label("Some words that wrap across the card, and more of them", 13f);
                card.Child("Photo").Sized(Sizing.Grow(), Sizing.Fixed(40f));
            }
            Frame();
            int nodes = Root.Node.Tree.Nodes.Count;

            for (int n = 1; n <= 12; n++)
            {
                counter.Text.Value = (n * 997).ToString();
                Frame();
                Assert.Less(Root.Node.Tree.LaidOutLastPass, nodes / 4, "the feed is taken whole");
            }
            Assert.AreEqual(_mismatches, UITree.LayoutMismatches, "laid out where solving every node puts it");
        }

        [Test]
        public void ChangesOfEveryKindLandWhereSolvingEveryNodePutsThem()
        {
            var column = Root.Node.Column(6f).Pad(12f);
            var row = column.Child("Row").Row(4f).Sized(Sizing.Grow(), Sizing.Fit());
            var left = row.Child("Left").Label("Left", 15f);
            var grow = row.Child("Grow").Sized(Sizing.Grow(), Sizing.Fixed(10f));
            var right = row.Child("Right").Column(2f).Pad(4f);
            right.Child("Words").Label("A wrapping text that runs on and on", 13f);
            right.Child("Square").Sized(Sizing.Fixed(30f), Sizing.Fixed(30f));

            var wrap = column.Child("Wrap").Row(4f).Sized(Sizing.Grow(), Sizing.Fit());
            wrap.Wrap.Value = Wrap.Lines;
            var chips = new List<Label>();
            for (int i = 0; i < 8; i++)
                chips.Add(wrap.Child("Chip " + i).Pad(6f, 2f).Label("Chip " + i, 13f));

            var grid = column.Child("Grid").Row(4f).Sized(Sizing.Grow(), Sizing.Fit());
            grid.Wrap.Value = Wrap.Grid(3);
            var cells = new List<UINode>();
            for (int i = 0; i < 7; i++)
            {
                var cell = grid.Child("Cell " + i).Column(2f).Sized(Sizing.Grow(), Sizing.Fit());
                cell.Child("Square").Sized(Sizing.Grow(), Sizing.Fixed(20f));
                cell.Child("Caption").Label("Cell " + i, 11f);
                cells.Add(cell);
            }

            var cards = column.Child("Cards").Column(6f).Sized(Sizing.Grow(), Sizing.Grow());
            cards.Scroll();
            var bodies = new List<Label>();
            var squares = new List<UINode>();
            for (int i = 0; i < 12; i++)
            {
                var card = cards.Child("Card " + i).Column(4f).Pad(8f).Sized(Sizing.Grow(), Sizing.Fit());
                var top = card.Child("Top").Row(6f);
                top.Child("Avatar").Sized(Sizing.Fixed(24f), Sizing.Fixed(24f));
                top.Child("Name").Label("Name " + i, 14f);
                bodies.Add(card.Child("Body").Label("A body of text long enough to wrap onto a second line inside its card", 12f));
                card.Child("Half").Sized(Sizing.Percent(0.5f), Sizing.Fixed(12f));
                var square = card.Child("Wide").Sized(Sizing.Grow(), Sizing.Fit());
                square.AspectRatio.Value = 2f;
                squares.Add(square);
                card.Child("Badge").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f)).Float(AttachPoint.RightTop, AttachPoint.RightTop);
            }
            column.Child("Marker").Sized(Sizing.Fixed(8f), Sizing.Fixed(8f)).FloatOn(right);
            Frame();

            left.Text.Value = "A much longer label on the left";
            Frame();
            right.Pad(10f);
            Frame();
            grow.Width.Value = Sizing.Fixed(40f);
            Frame();
            grow.Width.Value = Sizing.Grow();
            Frame();
            wrap.Gap.Value = 10f;
            Frame();
            chips[2].Text.Value = "A chip grown much longer";
            Frame();
            cells[2].Display.Value = DisplayMode.None;
            Frame();
            cells[2].Display.Value = DisplayMode.Visible;
            Frame();
            bodies[3].MaxLines.Value = 1;
            Frame();
            bodies[3].MaxLines.Value = 0;
            Frame();
            bodies[5].Text.Value = "Short";
            Frame();
            squares[7].AspectRatio.Value = 1f;
            Frame();
            column.Gap.Value = 12f;
            Frame();
            Assert.AreEqual(_mismatches, UITree.LayoutMismatches, "laid out where solving every node puts it");
        }

        [Test]
        public void ANodeTakenWholeThatComesOutADifferentSizeIsLaidOutAgainInside()
        {
            var column = Root.Node.Column(4f).Sized(Sizing.Fixed(300f), Sizing.Fixed(400f));
            var top = column.Child("Top").Label("One line", 15f);
            var rest = column.Child("Rest").Column(4f).Sized(Sizing.Grow(), Sizing.Grow());
            var filler = rest.Child("Filler").Sized(Sizing.Grow(), Sizing.Grow());
            rest.Child("Foot").Sized(Sizing.Grow(), Sizing.Fixed(20f));

            var row = column.Child("Row").Row(4f).Sized(Sizing.Grow(), Sizing.Fit());
            var name = row.Child("Name").Label("Name", 15f);
            var side = row.Child("Side").Column(2f).Sized(Sizing.Grow(), Sizing.Fit());
            var words = side.Child("Words").Label("Words that wrap to the width this column is given, however wide", 13f);
            side.Child("Under").Sized(Sizing.Grow(), Sizing.Fixed(6f));
            Frame();

            // Taller text above: what is taken whole below grows shorter, and what grows in it gives up the room.
            float filled = filler.LaidOutRect.height;
            top.Text.Value = "Now a text long enough to wrap onto two lines, three even, in a column three hundred points wide";
            Frame();
            Assert.Less(filler.LaidOutRect.height, filled);

            // A wider name beside it: what is taken whole comes out narrower, so it is solved from inside, its text
            // wrapped again.
            float wide = words.Node.LaidOutRect.width;
            name.Text.Value = "A much, much longer name";
            Frame();
            Assert.Less(words.Node.LaidOutRect.width, wide);
            Assert.AreEqual(_mismatches, UITree.LayoutMismatches, "laid out where solving every node puts it");
        }
    }
}
