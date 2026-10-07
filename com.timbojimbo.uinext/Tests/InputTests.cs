using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    public sealed class InputTests : UITestScene
    {
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
