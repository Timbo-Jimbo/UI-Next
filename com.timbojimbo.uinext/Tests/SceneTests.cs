using System.Text.RegularExpressions;
using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;
using UnityEngine.TestTools;

namespace TimboJimboTests.UINext
{
    public sealed class SceneTests : UITestScene
    {
        [Test]
        public void AColumnStacksItsChildrenGapApart()
        {
            var column = Root.Node.Column(10f).Pad(20f);
            var a = column.Child("A").Sized(Sizing.Fixed(50f), Sizing.Fixed(30f));
            var b = column.Child("B").Sized(Sizing.Fixed(50f), Sizing.Fixed(30f));
            Frame();
            Assert.AreEqual(new Rect(20f, 20f, 50f, 30f), a.LaidOutRect);
            Assert.AreEqual(new Rect(20f, 60f, 50f, 30f), b.LaidOutRect);
        }

        [Test]
        public void GrowFillsWhatIsLeft()
        {
            var row = Root.Node.Row();
            var fixedOne = row.Child("Fixed").Sized(Sizing.Fixed(90f), Sizing.Fixed(10f));
            var grow = row.Child("Grow").Sized(Sizing.Grow(), Sizing.Fixed(10f));
            Frame();
            Assert.AreEqual(90f, fixedOne.LaidOutRect.width);
            Assert.AreEqual(Root.Size.x - 90f, grow.LaidOutRect.width, 1e-3f);
        }

        [Test]
        public void DisplayNoneLeavesLayout()
        {
            var column = Root.Node.Column();
            var a = column.Child("A").Sized(Sizing.Fixed(50f), Sizing.Fixed(30f));
            var b = column.Child("B").Sized(Sizing.Fixed(50f), Sizing.Fixed(30f));
            Frame();
            a.Display.Value = DisplayMode.None;
            Frame();
            Assert.AreEqual(0f, b.LaidOutRect.y);
        }

        [Test]
        public void ALabelFitsItsText()
        {
            var label = Root.Node.Child("Label").Label("Hello", 20f);
            Frame();
            var size = label.Node.LaidOutRect.size;
            Assert.Greater(size.x, 20f);
            Assert.Greater(size.y, 15f);
            label.Text.Value = "Hello, world, at quite a lot more length";
            Frame();
            Assert.Greater(label.Node.LaidOutRect.width, size.x);
        }

        [Test]
        public void ACompositeGroupNotFullyOpaqueIsDrawnAsOneImage()
        {
            var group = Root.Node.Child("Group").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            group.CompositeGroup.Value = true;
            var child = group.Child("Child").Sized(Sizing.Fixed(50f), Sizing.Fixed(50f));
            child.Box(Color.white);
            Frame();
            Assert.AreEqual(0, Compositor.Groups.Count, "fully opaque, it is drawn as usual");

            group.Opacity.Value = 0.5f;
            Frame();
            Assert.AreEqual(1, Compositor.Groups.Count);
            Assert.AreEqual(0.5f, group.Tree.GroupOpacities[group.Index], 1e-5f);
            Assert.AreEqual(1f, group.Tree.Slots[child.Index].SizeOpacity.z, 1e-5f, "what is in it is drawn at its own opacity into the image");
            Assert.AreEqual(0.5f, group.Tree.Opacities[child.Index], 1e-5f, "while it counts as half there for the pointer");
        }

        [Test]
        public void AGroupsImageCoversWhatItDrawsNotTheWholeRoot()
        {
            Root.Node.Pad(100f);
            var group = Root.Node.Child("Group").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            group.CompositeGroup.Value = true;
            group.Opacity.Value = 0.5f;
            var box = group.Box(Color.white);
            box.ShadowColor.Value = Color.black;
            box.ShadowBlur.Value = 10f;
            box.ShadowOffset.Value = new Vector2(0f, 40f);
            Frame();
            Assert.AreEqual(1, Compositor.Groups.Count);
            // The box is at 100,100; its shadow is 40 lower, its blur reaching 25 past that.
            var region = Compositor.Groups[0].Region;
            Assert.LessOrEqual(region.xMin, 75f);
            Assert.LessOrEqual(region.yMin, 100f);
            Assert.GreaterOrEqual(region.xMax, 225f);
            Assert.GreaterOrEqual(region.yMax, 265f);
            Assert.Less(region.width, Root.Size.x);
            Assert.Less(region.height, Root.Size.y);
        }

        [Test]
        public void ABlurredNodeIsDrawnAsABlurredImageWithRoomForItsBlur()
        {
            Root.Node.Pad(100f);
            var card = Root.Node.Child("Card").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            card.Box(Color.white);
            card.CompositeGroup.Value = true;
            Frame();
            Assert.AreEqual(0, Compositor.Groups.Count, "sharp, it is drawn as usual");
            card.Blur.Value = 8f;
            Frame();
            Assert.AreEqual(1, Compositor.Groups.Count, "however opaque, a blurred node is drawn as an image first");
            Assert.AreEqual(8f * Root.PixelsPerPoint, Compositor.Groups[0].Blur, 1e-3f);
            var region = Compositor.Groups[0].Region;
            Assert.LessOrEqual(region.xMin, 84f, "room past it for the blur to spread into");
            Assert.GreaterOrEqual(region.xMax, 216f);
        }

        [Test]
        public void BlurAndMasksApplyOnlyToACompositeGroup()
        {
            var card = Root.Node.Child("Card").Sized(Sizing.Fixed(100f), Sizing.Fixed(60f));
            card.Box(Color.white);
            LogAssert.Expect(LogType.Warning, new Regex("composite group"));
            card.Blur.Value = 8f;
            Frame();
            Assert.AreEqual(0, Compositor.Groups.Count, "not a composite group, it is drawn as usual, unblurred");
            card.CompositeGroup.Value = true;
            Frame();
            Assert.AreEqual(1, Compositor.Groups.Count, "a composite group, it is blurred");
        }

        [Test]
        public void SaturationAndBrightnessMultiplyDownTheTreeWithoutAnImage()
        {
            var outer = Root.Node.Child("Outer").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            var inner = outer.Child("Inner").Sized(Sizing.Fixed(50f), Sizing.Fixed(50f));
            inner.Box(Color.red);
            outer.Saturation.Value = 0.5f;
            inner.Saturation.Value = 0.5f;
            outer.Brightness.Value = 0.8f;
            Frame();
            var slot = inner.Tree.Slots[inner.Index];
            Assert.AreEqual(0.25f, slot.Row0.w, 1e-5f);
            Assert.AreEqual(0.8f, slot.Row1.w, 1e-5f);
            Assert.AreEqual(0, Compositor.Groups.Count, "no image is made for a colour filter");
        }

        [Test]
        public void AMaskedNodeIsDrawnAsAnImageTheMaskCuts()
        {
            var card = Root.Node.Child("Card").Sized(Sizing.Fixed(100f), Sizing.Fixed(60f));
            card.Box(Color.white);
            card.CompositeGroup.Value = true;
            Frame();
            Assert.AreEqual(0, Compositor.Groups.Count);
            var mask = new Texture2D(4, 4);
            try
            {
                card.Mask.Value = mask;
                Frame();
                Assert.AreEqual(1, Compositor.Groups.Count, "however opaque, a masked node is drawn as an image first");
                Assert.AreSame(mask, Compositor.Groups[0].Mask);
                Assert.AreEqual(new Vector2(100f, 60f), Compositor.Groups[0].MaskSize);
            }
            finally
            {
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void ANodeAvoidingTheKeyboardKeepsWhatIsInsideItAboveIt()
        {
            var page = Root.Node.Column();
            page.Height.Value = Sizing.Grow();
            page.Width.Value = Sizing.Grow();
            page.Child("Thread").Sized(Sizing.Grow(), Sizing.Grow());
            var bar = page.Child("Composer").Pad(new Insets(10f, 10f, 6f, 30f));
            bar.Width.Value = Sizing.Grow();
            bar.AvoidsKeyboard.Value = true;
            var field = bar.Child("Field").Sized(Sizing.Grow(), Sizing.Fixed(36f));
            try
            {
                Frame();
                Assert.AreEqual(Root.Size.y - 30f, field.DrawnRect.yMax, 0.5f, "the home indicator's room, with the keyboard down");
                UIRoot.KeyboardHeight = 300f * Root.PixelsPerPoint;
                Frame();
                Assert.AreEqual(Root.Size.y - 300f, field.DrawnRect.yMax, 0.5f, "on the keyboard, with it up");
                UIRoot.KeyboardHeight = 0f;
                Frame();
                Assert.AreEqual(Root.Size.y - 30f, field.DrawnRect.yMax, 0.5f);
            }
            finally
            {
                UIRoot.KeyboardHeight = 0f;
            }
        }

        [Test]
        public void APresentationMakesItsChangesInsideItsOwnChangeAndCodeJoinsIt()
        {
            var host = Root.Node.Child("Sheet");
            host.Child("Card").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f)).Box(Color.white);
            var presentation = host.Add<Presentation>();
            var below = Root.Node.Child("Below").Sized(Sizing.Fixed(50f), Sizing.Fixed(50f));
            below.Box(Color.white);
            Frame();

            UITransaction presenting = null;
            bool shown = false;
            presentation.Presenting += () =>
            {
                presenting = UITransaction.Current;
                shown = host.Display.Peek == DisplayMode.Visible && host.PresentationLevel.Peek > 0;
                below.Opacity.Value = 0.2f;
            };
            var change = presentation.Present();
            Assert.IsNotNull(presenting);
            Assert.AreSame(change, presenting, "raised inside the presentation's own change");
            Assert.IsTrue(shown, "after its own state has changed");
            Frame(1f / 60f);
            Frame(0.1f);
            Assert.Less(below.Opacity.Presented, 1f, "what is changed there moves with it");
            Assert.Greater(below.Opacity.Presented, 0.2f);
            Run(2f);
            Assert.IsTrue(change.Completed, "nothing it moved was cut short");

            UITransaction dismissing = null;
            int level = 0;
            presentation.Dismissing += () =>
            {
                dismissing = UITransaction.Current;
                level = host.PresentationLevel.Peek;
            };
            var dismissal = presentation.Dismiss();
            Assert.AreSame(dismissal, dismissing);
            Assert.Less(level, 0, "on its way out, taking no pointers, inside the change");
            Run(2f);
            Assert.IsTrue(dismissal.Completed);
            Assert.AreEqual(DisplayMode.None, host.Display.Peek);
        }

        [Test]
        public void APresentedNodeIsLaidOutOverTheWholeRoot()
        {
            Root.Node.Pad(30f);
            var host = Root.Node.Child("Sheet");
            var presentation = host.Add<Presentation>();
            Frame();
            Assert.AreEqual(DisplayMode.None, host.Display.Peek, "hidden until presented");
            presentation.Present();
            Frame();
            Assert.AreEqual(new Rect(Vector2.zero, Root.Size), host.LaidOutRect);
            Assert.AreSame(presentation, Presentation.Top);
            presentation.Dismiss();
            Run(2f);
            Assert.AreEqual(DisplayMode.None, host.Display.Peek);
            Assert.IsNull(Presentation.Top);
        }
    }
}
