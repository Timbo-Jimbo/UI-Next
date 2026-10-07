using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

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
        public void AGroupNotFullyOpaqueIsDrawnAsOneImage()
        {
            var group = Root.Node.Child("Group").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            group.GroupOpacity.Value = true;
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
            group.GroupOpacity.Value = true;
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
