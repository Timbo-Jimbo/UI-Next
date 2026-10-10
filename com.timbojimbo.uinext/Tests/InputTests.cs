using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    public sealed class InputTests : UITestScene
    {
        // A page that scrolls, with a box of rows in it that scrolls too: the page at 200 of its 400, the box at `inner` of its 400.
        private (ScrollView outer, ScrollView inner, UINode box) NestedScrolls(float innerAt)
        {
            var page = Root.Node.Child("Page").Sized(Sizing.Fixed(300f), Sizing.Fixed(400f)).Column();
            var outer = page.Scroll();
            page.Child("Top").Sized(Sizing.Grow(), Sizing.Fixed(300f)).Box(Color.white);
            var box = page.Child("Box").Sized(Sizing.Grow(), Sizing.Fixed(200f)).Column();
            var inner = box.Scroll();
            for (int i = 0; i < 10; i++)
                box.Child("Row").Sized(Sizing.Grow(), Sizing.Fixed(60f)).Box(Color.white);
            page.Child("Bottom").Sized(Sizing.Grow(), Sizing.Fixed(300f)).Box(Color.white);
            Frame();
            outer.ContentOffset = new Vector2(0f, 200f);
            inner.ContentOffset = new Vector2(0f, innerAt);
            Frame();
            return (outer, inner, box);
        }

        [Test]
        public void ANewDragHeadingPastAScrollViewsEndScrollsTheScrollViewItIsIn()
        {
            var (outer, inner, box) = NestedScrolls(400f);
            var c = Center(box);
            Down(c);
            Move(c - new Vector2(0f, 10f), 0.02);
            Move(c - new Vector2(0f, 50f), 0.04);
            Assert.IsFalse(inner.IsDragging, "the box at its end does not take a new drag heading past it");
            Assert.IsTrue(outer.IsDragging, "the page takes it from the start");
            Assert.AreEqual(400f, inner.ContentOffset.y, 0.5f);
            Assert.AreEqual(240f, outer.ContentOffset.y, 0.5f, "and scrolls with it, from where it took it");
            Up(c - new Vector2(0f, 50f), 0.06);
        }

        [Test]
        public void AScrollViewThatBouncesKeepsADragThatCrossesItsEnd()
        {
            var (outer, inner, box) = NestedScrolls(380f);
            var c = Center(box);
            Down(c);
            Move(c - new Vector2(0f, 10f), 0.02);
            Move(c - new Vector2(0f, 50f), 0.04);
            Assert.IsTrue(inner.IsDragging, "the box keeps the drag");
            Assert.IsFalse(outer.IsDragging);
            Assert.Greater(inner.ContentOffset.y, 400f, "stretched past its end, to bounce back");
            Up(c - new Vector2(0f, 50f), 0.06);
        }

        [Test]
        public void AScrollViewHeldStillMidDragIsStill()
        {
            var (_, inner, box) = NestedScrolls(100f);
            var c = Center(box);
            Down(c);
            Move(c - new Vector2(0f, 10f), 0.02);
            Move(c - new Vector2(0f, 30f), 0.04);
            Assert.AreNotEqual(0f, inner.Offset.Velocity.y, "moving with the finger");
            Frame(1f / 60f);
            Frame(1f / 60f);
            Assert.AreEqual(0f, inner.Offset.Velocity.y, 1e-3f, "held still, it is still");
            Up(c - new Vector2(0f, 30f), 0.2);
        }

        private const int Finger = UIInput.TouchIdBase;

        private void Down(Vector2 point) => UIInput.Down(Finger, PointerKind.Touch, Screen(point), 0);
        private void Move(Vector2 point, double time) => UIInput.Move(Finger, Screen(point), time);
        private void Up(Vector2 point, double time) => UIInput.Up(Finger, Screen(point), time);

        [Test]
        public void ATapOnAButtonRaisesTapped()
        {
            var button = Root.Node.Child("Button").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            button.Box(Color.white);
            int taps = 0;
            button.Pressable(() => taps++);
            Frame();
            var center = Center(button);
            Down(center);
            Assert.IsTrue(button.GetComponent<Pressable>().IsPressed);
            Up(center, 0.05);
            Assert.AreEqual(1, taps);
        }

        [Test]
        public void LettingGoOutsideIsNoTap()
        {
            var button = Root.Node.Child("Button").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            button.Box(Color.white);
            int taps = 0;
            button.Pressable(() => taps++);
            Frame();
            Down(Center(button));
            Move(new Vector2(300f, 400f), 0.05);
            Up(new Vector2(300f, 400f), 0.1);
            Assert.AreEqual(0, taps);
        }

        [Test]
        public void APressOnAGlidingScrollCatchesItAndPressesNothing()
        {
            var scroll = Root.Node.Child("Scroll").Sized(Sizing.Fixed(300f), Sizing.Fixed(300f)).Column();
            var view = scroll.Scroll();
            int taps = 0;
            for (int i = 0; i < 30; i++)
            {
                var row = scroll.Child("Row").Sized(Sizing.Fixed(300f), Sizing.Fixed(60f));
                row.Box(Color.white);
                row.Pressable(() => taps++);
            }
            Frame();
            var center = Center(scroll);
            // A flick up sets it gliding.
            Down(center);
            Move(center + new Vector2(0f, -40f), 0.02);
            Move(center + new Vector2(0f, -120f), 0.04);
            Up(center + new Vector2(0f, -120f), 0.05);
            Run(0.1f);
            Assert.IsTrue(view.IsDecelerating, "gliding");

            // A press catches it (the scroll view claims the pointer as it goes down) and the row under it is not tapped.
            Down(center);
            Run(0.2f);
            float caught = view.PresentedOffset.y;
            Run(0.3f);
            Up(center, 1.0);
            Assert.AreEqual(caught, view.PresentedOffset.y, 0.5f, "it stopped where it was caught");
            Assert.AreEqual(0, taps);
        }

        // A card dragged down at 1250 points a second, which springs home as it lets go: the speed of its offset as it
        // sets off home.
        private Vector4 Throw(bool tracksVelocity, bool springsHome = true)
        {
            var card = Root.Node.Child("Card").Sized(Sizing.Fixed(200f), Sizing.Fixed(200f));
            card.Box(Color.white);
            var drag = card.Add<Draggable>();
            drag.TracksVelocity = tracksVelocity;
            drag.Changed += e => card.Offset.Value = e.Translation;
            var handedOn = new Vector4(float.NaN, float.NaN, 0f, 0f);
            drag.Ended += e =>
            {
                if (springsHome)
                    UI.Animate(new UIAnimation(0.5f), () => card.Offset.Value = Vector2.zero);
                handedOn = card.Offset.Velocity;
            };
            Frame();
            var c = Center(card);
            Down(c);
            for (int i = 1; i <= 6; i++)
                Move(c + new Vector2(0f, 20f * i), 0.016 * i);
            Up(c + new Vector2(0f, 120f), 0.1);
            return handedOn;
        }

        [Test]
        public void ADragHandsTheSpeedItMovedAValueOnToTheChangeMadeAsItLetsGo()
        {
            var velocity = Throw(tracksVelocity: true);
            Assert.Greater(velocity.y, 800f, "the spring home sets off at the finger's speed");
        }

        [Test]
        public void ADragThatTracksNoVelocityLetsGoStill()
        {
            var velocity = Throw(tracksVelocity: false);
            Assert.AreEqual(0f, velocity.y, 1e-4f, "a precise control's snap starts still");
        }

        [Test]
        public void OnceADragIsOverWhatItMovedThatNothingTookOverLandsStill()
        {
            Throw(tracksVelocity: true, springsHome: false);
            var card = Root.Node.transform.Find("Card").GetComponent<UINode>();
            Assert.AreEqual(Vector4.zero, card.Offset.Velocity);
        }

        [Test]
        public void AScrollViewsOffsetMovesAtTheSpeedItGlides()
        {
            var list = Root.Node.Child("List").Sized(Sizing.Fixed(300f), Sizing.Fixed(300f)).Column();
            var scroll = list.Scroll();
            for (int i = 0; i < 30; i++)
                list.Child("Row").Sized(Sizing.Fixed(300f), Sizing.Fixed(60f)).Box(Color.white);
            Frame();
            var c = Center(list);
            Down(c);
            Move(c + new Vector2(0f, -40f), 0.02);
            Move(c + new Vector2(0f, -120f), 0.04);
            Up(c + new Vector2(0f, -120f), 0.05);
            Run(0.05f);
            Assert.IsTrue(scroll.IsDecelerating);
            Assert.Greater(scroll.Offset.Velocity.y, 100f, "gliding on, its offset has the glide's speed");
        }

        [Test]
        public void ADraggableTakesOnlyTheDragsItSaysItTakes()
        {
            var card = Root.Node.Child("Card").Sized(Sizing.Fixed(200f), Sizing.Fixed(200f));
            card.Box(Color.white);
            var drag = card.Add<Draggable>();
            drag.Takes = t => t.y > 0f;
            Frame();
            var c = Center(card);
            Down(c);
            Move(c + new Vector2(0f, -30f), 0.05);
            Assert.IsFalse(drag.IsDragging, "up is not a way it takes");
            Up(c + new Vector2(0f, -30f), 0.1);
            Down(c);
            Move(c + new Vector2(0f, 30f), 0.15);
            Assert.IsTrue(drag.IsDragging, "down is");
            Up(c + new Vector2(0f, 30f), 0.2);
        }

        [Test]
        public void AScrollViewAtItsTopPassesTheRestOfADragToADraggableThatTakesItAndTakesItBack()
        {
            // A sheet that is pulled down, with a list in it.
            var sheet = Root.Node.Child("Sheet").Sized(Sizing.Fixed(300f), Sizing.Fixed(400f)).Column();
            sheet.Box(Color.white);
            var drag = sheet.Add<Draggable>();
            drag.Axis = ScrollAxis.Vertical;
            drag.Takes = t => t.y > 0f;
            drag.Began += e => sheet.Offset.Value = new Vector2(0f, e.Translation.y);
            drag.Changed += e => sheet.Offset.Value = new Vector2(0f, e.Translation.y);
            drag.Ended += e => sheet.Offset.Value = new Vector2(0f, e.Translation.y);
            var list = sheet.Child("List").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            var scroll = list.Scroll();
            scroll.AtEnd.Value = ScrollEnd.PassOn;
            for (int i = 0; i < 20; i++)
                list.Child("Row").Sized(Sizing.Grow(), Sizing.Fixed(60f)).Box(Color.white);
            Frame();
            scroll.ContentOffset = new Vector2(0f, 30f);
            Frame();

            var c = Center(list);
            Down(c);
            Move(c + new Vector2(0f, 20f), 0.02);
            Move(c + new Vector2(0f, 40f), 0.04);
            Assert.AreEqual(10f, scroll.ContentOffset.y, 0.5f, "the list scrolls back towards its top");
            Move(c + new Vector2(0f, 80f), 0.06);
            Assert.AreEqual(0f, scroll.ContentOffset.y, 0.5f, "it stops at its top");
            Assert.IsTrue(drag.IsDragging, "the sheet has the rest of the drag");
            Assert.AreEqual(30f, sheet.Offset.Peek.y, 0.5f, "and moves with it, from where the list stopped");
            Move(c + new Vector2(0f, 100f), 0.08);
            Assert.AreEqual(50f, sheet.Offset.Peek.y, 0.5f);

            // The finger turns back up past where the sheet took the drag: it hands it back, and the list scrolls on.
            Move(c + new Vector2(0f, 40f), 0.1);
            Assert.IsFalse(drag.IsDragging);
            Assert.AreEqual(0f, sheet.Offset.Peek.y, 0.5f);
            Assert.IsTrue(scroll.IsDragging);
            Assert.AreEqual(10f, scroll.ContentOffset.y, 0.5f);
            Up(c + new Vector2(0f, 40f), 0.12);
        }

        // A sheet that takes a pull down, with a list in it that passes drags on at its ends.
        private (Draggable drag, ScrollView scroll, UINode sheet, UINode list) Sheet()
        {
            var sheet = Root.Node.Child("Sheet").Sized(Sizing.Fixed(300f), Sizing.Fixed(400f)).Column();
            sheet.Box(Color.white);
            var drag = sheet.Add<Draggable>();
            drag.Axis = ScrollAxis.Vertical;
            drag.Takes = t => t.y > 0f;
            drag.Changed += e => sheet.Offset.Value = new Vector2(0f, e.Translation.y);
            var list = sheet.Child("List").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            var scroll = list.Scroll();
            scroll.AtEnd.Value = ScrollEnd.PassOn;
            for (int i = 0; i < 20; i++)
                list.Child("Row").Sized(Sizing.Grow(), Sizing.Fixed(60f)).Box(Color.white);
            Frame();
            return (drag, scroll, sheet, list);
        }

        [Test]
        public void AScrollViewCaughtPastItsEndKeepsTheDragAndPassesNothingOn()
        {
            var (drag, scroll, sheet, list) = Sheet();
            // Bouncing back from past its top.
            scroll.Offset.Jump(new Vector2(0f, -30f));
            UI.Animate(new UIAnimation(0.5f), () => scroll.Offset.Value = Vector2.zero);
            Frame(1f / 60f);
            Assert.IsTrue(scroll.IsDecelerating);

            var c = Center(list);
            Down(c);
            Move(c - new Vector2(0f, 2f), 0.02);
            Assert.IsTrue(scroll.IsDragging, "caught, the list keeps the drag");
            Assert.IsFalse(drag.IsDragging, "and hands the sheet nothing");
            Assert.AreEqual(0f, sheet.Offset.Peek.y, 0.5f);
            Up(c - new Vector2(0f, 2f), 0.04);
        }

        [Test]
        public void ADraggableDisabledWhileAScrollViewPassedItADragHandsItBack()
        {
            var (drag, scroll, _, list) = Sheet();
            var c = Center(list);
            Down(c);
            Move(c + new Vector2(0f, 20f), 0.02);
            Move(c + new Vector2(0f, 60f), 0.04);
            Assert.IsTrue(drag.IsDragging, "the sheet has the rest of the drag");
            drag.enabled = false;
            Assert.IsTrue(scroll.IsDragging, "disabled, it gives the drag back to the list");
            Up(c + new Vector2(0f, 60f), 0.06);
        }

        [Test]
        public void AScrollViewAtItsEndPassesTheRestOfADragToTheScrollViewItIsInWhenItPassesDragsOn()
        {
            var page = Root.Node.Child("Page").Sized(Sizing.Fixed(300f), Sizing.Fixed(400f)).Column();
            var outer = page.Scroll();
            page.Child("Top").Sized(Sizing.Grow(), Sizing.Fixed(300f)).Box(Color.white);
            var box = page.Child("Box").Sized(Sizing.Grow(), Sizing.Fixed(200f)).Column();
            var inner = box.Scroll();
            inner.AtEnd.Value = ScrollEnd.PassOn;
            for (int i = 0; i < 10; i++)
                box.Child("Row").Sized(Sizing.Grow(), Sizing.Fixed(60f)).Box(Color.white);
            page.Child("Bottom").Sized(Sizing.Grow(), Sizing.Fixed(300f)).Box(Color.white);
            Frame();
            outer.ContentOffset = new Vector2(0f, 200f);
            inner.ContentOffset = new Vector2(0f, 380f);
            Frame();

            var c = Center(box);
            Down(c);
            Move(c - new Vector2(0f, 10f), 0.02);
            Move(c - new Vector2(0f, 50f), 0.04);
            Assert.AreEqual(400f, inner.ContentOffset.y, 0.5f, "the list stops at its end");
            Assert.IsTrue(outer.IsDragging, "the page has the rest of the drag");
            Assert.AreEqual(220f, outer.ContentOffset.y, 0.5f, "and scrolls on with it");
            Up(c - new Vector2(0f, 50f), 0.06);
        }

        [Test]
        public void TwoFingersSpreadingAndTurningPinch()
        {
            var photo = Root.Node.Child("Photo").Sized(Sizing.Fixed(300f), Sizing.Fixed(300f));
            photo.Box(Color.white);
            var pinchable = photo.Add<Pinchable>();
            int began = 0, ended = 0;
            PinchEvent last = default;
            pinchable.Began += e => began++;
            pinchable.Changed += e => last = e;
            pinchable.Ended += e => ended++;
            Frame();
            var c = Center(photo);
            UIInput.Down(Finger, PointerKind.Touch, Screen(c - new Vector2(50f, 0f)), 0);
            Assert.AreEqual(0, began, "one finger is no pinch");
            UIInput.Down(Finger + 1, PointerKind.Touch, Screen(c + new Vector2(50f, 0f)), 0);
            Assert.AreEqual(1, began);
            UIInput.Move(Finger, Screen(c - new Vector2(100f, 0f)), 0.1);
            UIInput.Move(Finger + 1, Screen(c + new Vector2(100f, 0f)), 0.1);
            Assert.AreEqual(2f, last.Scale, 0.01f);
            Assert.AreEqual(0f, last.Rotation, 0.5f);
            // A quarter turn clockwise about the centre, a little apart.
            UIInput.Move(Finger, Screen(c - new Vector2(0f, 100f)), 0.2);
            UIInput.Move(Finger + 1, Screen(c + new Vector2(0f, 100f)), 0.2);
            Assert.AreEqual(90f, last.Rotation, 0.5f);
            Assert.Less(last.Translation.magnitude, 0.01f);
            UIInput.Up(Finger, Screen(c - new Vector2(0f, 100f)), 0.3);
            Assert.AreEqual(1, ended);
            UIInput.Up(Finger + 1, Screen(c + new Vector2(0f, 100f)), 0.3);
        }

        [Test]
        public void HeldLongEnoughItIsALongPressAndNoTap()
        {
            var button = Root.Node.Child("Button").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            button.Box(Color.white);
            int taps = 0, longPresses = 0;
            var pressable = button.Pressable(() => taps++);
            pressable.LongPressed += () => longPresses++;
            Frame();
            var center = Center(button);
            Down(center);
            Run(0.3f);
            Assert.AreEqual(0, longPresses);
            Run(0.4f);
            Assert.AreEqual(1, longPresses);
            Up(center, 1.0);
            Assert.AreEqual(0, taps, "letting go after a long press is no tap");

            Down(center);
            Run(0.1f);
            Up(center, 1.2);
            Assert.AreEqual(1, taps, "a short press still taps");
            Assert.AreEqual(1, longPresses);
        }

        [Test]
        public void TheDeepestButtonTakesTheTap()
        {
            var outer = Root.Node.Child("Outer").Sized(Sizing.Fixed(200f), Sizing.Fixed(100f)).Center();
            outer.Box(Color.white);
            int outerTaps = 0, innerTaps = 0;
            outer.Pressable(() => outerTaps++);
            var inner = outer.Child("Inner").Sized(Sizing.Fixed(50f), Sizing.Fixed(30f));
            inner.Box(Color.gray);
            inner.Pressable(() => innerTaps++);
            Frame();
            Down(Center(inner));
            Up(Center(inner), 0.05);
            Assert.AreEqual(1, innerTaps);
            Assert.AreEqual(0, outerTaps);
        }

        [Test]
        public void AScrollThatTakesTheDragCancelsThePress()
        {
            var list = Root.Node.Child("List").Sized(Sizing.Grow(), Sizing.Fixed(300f)).Column();
            var scroll = list.Scroll();
            UINode first = null;
            for (int i = 0; i < 20; i++)
            {
                var row = list.Child("Row").Sized(Sizing.Grow(), Sizing.Fixed(60f));
                row.Box(Color.white);
                row.Pressable();
                first ??= row;
            }
            Frame();
            var start = Center(first);
            Down(start);
            Assert.IsTrue(first.GetComponent<Pressable>().IsPressed);
            Move(start - new Vector2(0f, 40f), 0.05);
            Assert.IsFalse(first.GetComponent<Pressable>().IsPressed, "the scroll claimed the pointer");
            Assert.IsTrue(scroll.IsDragging);
            Move(start - new Vector2(0f, 100f), 0.1);
            Assert.Greater(scroll.ContentOffset.y, 50f);
            Up(start - new Vector2(0f, 100f), 0.15);
        }

        [Test]
        public void AClipHidesWhatIsOutsideItFromThePointer()
        {
            var box = Root.Node.Child("Box").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f)).Clip();
            var inside = box.Child("Wide").Sized(Sizing.Fixed(300f), Sizing.Fixed(100f));
            inside.Box(Color.white);
            int taps = 0;
            inside.Pressable(() => taps++);
            Frame();
            var outside = new Vector2(250f, 50f);
            Down(outside);
            Up(outside, 0.05);
            Assert.AreEqual(0, taps);
            Down(new Vector2(50f, 50f));
            Up(new Vector2(50f, 50f), 0.05);
            Assert.AreEqual(1, taps);
        }

        private static Pressable Button(UINode parent, string name)
        {
            var button = parent.Child(name).Sized(Sizing.Fixed(80f), Sizing.Fixed(40f));
            button.Box(Color.white);
            return button.Pressable();
        }

        [Test]
        public void FocusGoesToTheNearestButtonThatWay()
        {
            var grid = Root.Node.Column(10f).Pad(10f);
            var top = grid.Child("Top").Row(10f);
            var a = Button(top, "A");
            var b = Button(top, "B");
            var bottom = grid.Child("Bottom").Row(10f);
            var c = Button(bottom, "C");
            var d = Button(bottom, "D");
            Frame();
            var down = new Vector2(0f, 1f);
            var right = new Vector2(1f, 0f);
            UIFocus.Move(right);
            Assert.AreSame(a, UIFocus.Focused, "with nothing focused, the first takes it");
            UIFocus.Move(right);
            Assert.AreSame(b, UIFocus.Focused);
            UIFocus.Move(down);
            Assert.AreSame(d, UIFocus.Focused);
            UIFocus.Move(-right);
            Assert.AreSame(c, UIFocus.Focused);
            Assert.IsFalse(UIFocus.Move(-right), "nothing further that way");
            Assert.AreSame(c, UIFocus.Focused);
            Frame();
            Assert.IsTrue((c.Node.Interaction & InteractionStates.Focused) != 0, "the focused button is Focused while focus shows");
        }

        [Test]
        public void TabStepsThroughInOrderAndAPointerHidesFocus()
        {
            var column = Root.Node.Column(10f);
            var a = Button(column, "A");
            var b = Button(column, "B");
            Frame();
            UIFocus.Next();
            UIFocus.Next();
            Assert.AreSame(b, UIFocus.Focused);
            UIFocus.Next();
            Assert.AreSame(a, UIFocus.Focused, "it wraps round");
            Assert.IsTrue(UIFocus.IsVisible);
            Down(new Vector2(300f, 700f));
            Assert.IsFalse(UIFocus.IsVisible);
            Assert.IsTrue((a.Node.Interaction & InteractionStates.Focused) == 0);
        }

        [Test]
        public void ADialogKeepsFocusInsideItself()
        {
            Button(Root.Node, "Below");
            var host = Root.Node.Child("Dialog").Center();
            var dialog = host.Add<Presentation>();
            var ok = Button(host, "OK");
            Frame();
            dialog.Present();
            Run(1f);
            UIFocus.Move(new Vector2(0f, 1f));
            Assert.AreSame(ok, UIFocus.Focused);
            UIFocus.Move(new Vector2(0f, -1f));
            Assert.AreSame(ok, UIFocus.Focused, "the button below the dialog cannot take focus");
        }

        [Test]
        public void APresentationTakesThePointerFromWhatIsBelow()
        {
            var button = Root.Node.Child("Button").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            button.Box(Color.white);
            int taps = 0;
            button.Pressable(() => taps++);
            var presentation = Root.Node.Child("Dialog").Add<Presentation>();
            Frame();
            presentation.Present();
            Run(1f);
            var center = Center(button);
            Down(center);
            Up(center, 0.05);
            Assert.AreEqual(0, taps);
            Assert.IsFalse(presentation.IsPresented, "a tap on the backdrop dismisses it");
        }
    }
}
