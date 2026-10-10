using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    public sealed class MatchTests : UITestScene
    {
        // A node floating at `at`, `size` big, matched by `name`.
        private UINode Named(string name, Vector2 at, Vector2 size, UINode parent = null)
        {
            var node = (parent != null ? parent : Root.Node).Child(name).Sized(Sizing.Fixed(size.x), Sizing.Fixed(size.y))
                .Float(AttachPoint.LeftTop, AttachPoint.LeftTop, at);
            node.Box(Color.white);
            node.MatchName.Value = name;
            return node;
        }

        // A small card, shown, and a big one of the same name, hidden.
        private (UINode small, UINode big) Pair()
        {
            var small = Named("Card", new Vector2(20f, 100f), new Vector2(50f, 50f));
            var big = Named("Card", new Vector2(40f, 400f), new Vector2(300f, 200f));
            big.Display.Value = DisplayMode.None;
            Frame();
            return (small, big);
        }

        private static void Swap(UINode hide, UINode show, float duration = 0.5f) => UI.Animate(new UIAnimation(duration), () =>
        {
            hide.Display.Value = DisplayMode.None;
            show.Display.Value = DisplayMode.Visible;
        });

        private static void AreEqual(Rect expected, Rect actual, string message = null)
        {
            Assert.AreEqual(expected.x, actual.x, 0.01f, message);
            Assert.AreEqual(expected.y, actual.y, 0.01f, message);
            Assert.AreEqual(expected.width, actual.width, 0.01f, message);
            Assert.AreEqual(expected.height, actual.height, 0.01f, message);
        }

        [Test]
        public void ANodeShownWithTheNameOfOneTheChangeHidesFliesFromWhereThatOneWasDrawn()
        {
            var (small, big) = Pair();
            var change = UI.Animate(new UIAnimation(0.5f), () =>
            {
                small.Display.Value = DisplayMode.None;
                big.Display.Value = DisplayMode.Visible;
            });
            var flight = big.FlightIn;
            Assert.IsNotNull(flight, "the node shown flies");
            Assert.AreSame(flight, small.FlightOut, "taking over from the one hidden, whose picture flies instead");
            Frame(1f / 60f);
            AreEqual(new Rect(20f, 100f, 50f, 50f), flight.Rect, "it sets off from where the hidden one was drawn");
            Assert.AreEqual(2, Compositor.Groups.Count, "a picture and the node itself, each an image of its own");
            Run(0.15f);
            Assert.Greater(flight.Rect.width, 50f, "on its way");
            Assert.Less(flight.Rect.width, 300f);
            Assert.Greater(flight.Progress, 0f, "cross-fading");
            Assert.Less(flight.Progress, 1f);
            Run(2f);
            Assert.IsNull(big.FlightIn, "landed");
            Assert.IsNull(small.FlightOut);
            Assert.AreEqual(0, Compositor.Groups.Count);
            Assert.IsTrue(change.Completed);
        }

        [Test]
        public void WhatIsInsideIsFittedToTheFlightsWidthAndItsBackgroundStretches()
        {
            var (small, big) = Pair();
            Swap(small, big);
            Frame(1f / 60f);
            var flight = big.FlightIn;
            Assert.AreEqual(50f / 300f, flight.LiveScale, 1e-3f, "the node is fitted to the flight's width");
            Assert.AreEqual(1f, flight.PictureScale, 1e-3f, "the picture starts as it was drawn");
            // Its own space's top left is the flight's.
            var topLeft = flight.FitMap * big.Tree.World[big.Index];
            Assert.AreEqual(20f, topLeft.Tx, 0.5f);
            Assert.AreEqual(100f, topLeft.Ty, 0.5f);
        }

        [Test]
        public void ANodeShownWhileItsNamesakeStaysFliesOutOfIt()
        {
            var (small, big) = Pair();
            UI.Animate(new UIAnimation(0.5f), () => big.Display.Value = DisplayMode.Visible);
            var flight = big.FlightIn;
            Assert.IsNotNull(flight);
            Assert.IsFalse(flight.TakesOver);
            Assert.IsNull(small.FlightOut, "the one that stays is still drawn itself");
            Frame(1f / 60f);
            AreEqual(new Rect(20f, 100f, 50f, 50f), flight.Rect);
        }

        [Test]
        public void IdsKeepManyOfAKindApart()
        {
            var list = Root.Node.Child("List").Column(10f);
            UINode Row(string id)
            {
                var row = list.Child("Row " + id).Sized(Sizing.Fixed(200f), Sizing.Fixed(40f));
                row.Box(Color.white);
                row.MatchId.Value = id;
                var title = row.Child("Title").Sized(Sizing.Fixed(100f), Sizing.Fixed(20f));
                title.Box(Color.gray);
                title.MatchName.Value = "Title";
                return title;
            }
            var first = Row("1");
            var second = Row("2");
            var detail = Root.Node.Child("Detail").Sized(Sizing.Fixed(300f), Sizing.Fixed(300f))
                .Float(AttachPoint.LeftTop, AttachPoint.LeftTop, new Vector2(0f, 400f));
            detail.MatchId.Value = "2";
            var heading = Named("Title", Vector2.zero, new Vector2(200f, 30f), detail);
            detail.Display.Value = DisplayMode.None;
            Frame();

            UI.Animate(new UIAnimation(0.5f), () =>
            {
                second.Parent.Display.Value = DisplayMode.Hidden;
                detail.Display.Value = DisplayMode.Visible;
            });
            Assert.IsNotNull(heading.FlightIn);
            Assert.AreSame(heading.FlightIn, second.FlightOut, "the second row's title, whose id the detail inherits");
            Assert.IsNull(first.FlightOut);
        }

        [Test]
        public void ANamedPartFliesInsideItsPair()
        {
            var (small, big) = Pair();
            var smallIcon = Named("Icon", new Vector2(5f, 5f), new Vector2(20f, 20f), small);
            var bigIcon = Named("Icon", new Vector2(20f, 20f), new Vector2(80f, 80f), big);
            Frame();
            Swap(small, big);
            Assert.IsNotNull(bigIcon.FlightIn);
            Assert.AreSame(big.FlightIn, bigIcon.FlightIn.Outer, "inside the whole's flight");
            Assert.AreSame(bigIcon.FlightIn, smallIcon.FlightOut);
            Frame(1f / 60f);
            AreEqual(new Rect(25f, 105f, 20f, 20f), bigIcon.FlightIn.Rect, "from where the small one's icon was drawn");
            Assert.AreEqual(4, Compositor.Groups.Count, "two images a pair");
        }

        [Test]
        public void AFlightIsCarriedByWhatMovesItsDestination()
        {
            var page = Root.Node.Child("Page").Sized(Sizing.Fixed(390f), Sizing.Fixed(600f)).Column();
            var scroll = page.Scroll();
            page.Child("Above").Sized(Sizing.Fixed(390f), Sizing.Fixed(300f)).Box(Color.white);
            var target = page.Child("Target").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            target.Box(Color.white);
            target.MatchName.Value = "Thing";
            target.Display.Value = DisplayMode.None;
            page.Child("Below").Sized(Sizing.Fixed(390f), Sizing.Fixed(600f)).Box(Color.white);
            var origin = Named("Thing", new Vector2(300f, 700f), new Vector2(50f, 50f));
            Frame();

            Swap(origin, target, 1f);
            Frame(1f / 60f);
            Run(0.1f);
            var before = target.FlightIn.Rect;
            scroll.ContentOffset = new Vector2(0f, 50f);
            Frame();
            var after = target.FlightIn.Rect;
            Assert.AreEqual(before.y - 50f, after.y, 0.5f, "the scroll carries it, rather than it chasing the scroll");
            Assert.AreEqual(before.x, after.x, 0.5f);
        }

        [Test]
        public void AFlightTakenBackTurnsRoundFromWhereItIsDrawn()
        {
            var (small, big) = Pair();
            Swap(small, big);
            Frame(1f / 60f);
            Run(0.15f);
            var flight = big.FlightIn;
            var drawn = flight.Rect;
            float faded = flight.Progress;

            Swap(big, small);
            var back = small.FlightIn;
            Assert.IsNotNull(back);
            Assert.AreSame(back, big.FlightOut);
            Assert.IsNull(big.FlightIn, "the first flight is over");
            Frame();
            Assert.AreEqual(drawn.x, back.Rect.x, 0.5f, "it turns round from where it was drawn");
            Assert.AreEqual(drawn.width, back.Rect.width, 0.5f);
            Assert.AreEqual(1f - faded, back.Progress, 1e-3f, "and fades on from there, the other way");
            Run(2f);
            Assert.IsNull(small.FlightIn);
        }

        [Test]
        public void AFlightSetsOffAtTheSpeedItsSourceWasDrawnMoving()
        {
            var (small, big) = Pair();
            UI.Animate(new UIAnimation(1f), () => small.Offset.Value = new Vector2(200f, 0f));
            Frame(1f / 60f);
            Run(0.1f);
            float speed = small.Offset.Velocity.x;
            Assert.Greater(speed, 0f);
            Swap(small, big);
            Assert.AreEqual(speed, big.FlightIn.Offset.Velocity.x, speed * 0.05f);
        }

        [Test]
        public void APresentationGoingAwayHandsItsNodeBackToTheOneItCameFrom()
        {
            var thumbnail = Named("Photo", new Vector2(20f, 100f), new Vector2(80f, 60f));
            var host = Root.Node.Child("Viewer").Center();
            var viewer = host.Add<Presentation>();
            viewer.Transition = PresentationTransition.None;
            var photo = host.Child("Photo").Sized(Sizing.Fixed(390f), Sizing.Fixed(292f));
            photo.Box(Color.white);
            photo.MatchName.Value = "Photo";
            viewer.Presenting += () => thumbnail.Display.Value = DisplayMode.Hidden;
            viewer.Dismissing += () => thumbnail.Display.Value = DisplayMode.Visible;
            Frame();

            viewer.Present();
            Assert.IsNotNull(photo.FlightIn, "the photo flies out of its thumbnail as it is presented");
            Assert.AreSame(photo.FlightIn, thumbnail.FlightOut);
            Run(2f);
            Assert.IsNull(photo.FlightIn);

            viewer.Dismiss();
            var home = thumbnail.FlightIn;
            Assert.IsNotNull(home, "and flies home as it is dismissed");
            Assert.AreSame(host, home.SourceLevel, "over the presentation going away");
            Run(2f);
            Assert.IsNull(thumbnail.FlightIn);
            Assert.AreEqual(DisplayMode.None, host.Display.Peek);
        }
    }
}
