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
