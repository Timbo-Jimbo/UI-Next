using System.Collections.Generic;
using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    // Visibility decided once in the tree: a node scrolled out from under a clip is offscreen, drawn again only when it
    // comes back, and kept in view by what it draws past its rect.
    public sealed class VisibilityTests : UITestScene
    {
        // A scroll view 300 points tall of 60-point rows: rows 0 to 4 in view.
        private (ScrollView view, List<Box> boxes) Feed(int rows)
        {
            var scroll = Root.Node.Child("Scroll").Sized(Sizing.Fixed(300f), Sizing.Fixed(300f)).Column();
            var view = scroll.Scroll();
            var boxes = new List<Box>();
            for (int i = 0; i < rows; i++)
            {
                var row = scroll.Child("Row " + i).Sized(Sizing.Fixed(300f), Sizing.Fixed(60f));
                boxes.Add(row.Box(Color.white));
            }
            return (view, boxes);
        }

        [Test]
        public void ANodeScrolledOutOfViewIsOffscreenAndDrawnAgainOnlyWhenItComesBack()
        {
            var (view, boxes) = Feed(30);
            Frame();
            var tree = Root.Node.Tree;
            var near = boxes[2].Node;
            var far = boxes[20].Node;
            Assert.IsFalse(tree.Offscreen[near.Index], "a row in view");
            Assert.IsTrue(tree.Offscreen[far.Index], "a row scrolled away");
            Assert.AreEqual(1, far.Instances.Count, "drawn once as it was made");

            // Changed while away: its drawing waits, as it was.
            boxes[20].Fill.Value = Color.red;
            Frame();
            Assert.IsTrue(far.DrawWaiting, "its drawing waits");
            Assert.AreEqual((Vector4)Color.white, far.Instances[far.Instances.Count - 1].Color, "kept as it was");

            // Back in view: drawn again that frame.
            view.ScrollTo(new Vector2(0f, 20 * 60f), animated: false);
            Frame();
            Assert.IsFalse(tree.Offscreen[far.Index], "back in view");
            Assert.IsFalse(far.DrawWaiting);
            Assert.AreEqual((Vector4)Color.red, far.Instances[far.Instances.Count - 1].Color, "drawn as it is now");
            Assert.IsTrue(tree.Offscreen[near.Index], "the row that scrolled away");
        }

        [Test]
        public void WhatANodeDrawsPastItsRectKeepsItInView()
        {
            var (_, boxes) = Feed(10);
            // Row 6 starts 60 points below the view's end; its shadow's blur reaches 77 points past it.
            boxes[6].ShadowColor.Value = Color.black;
            boxes[6].ShadowBlur.Value = 30f;
            Frame();
            var tree = Root.Node.Tree;
            Assert.IsFalse(tree.Offscreen[boxes[6].Node.Index], "its shadow reaches into view");
            Assert.IsTrue(tree.Offscreen[boxes[7].Node.Index], "a plain row as far away is offscreen");
            Assert.IsFalse(tree.Offscreen[boxes[5].Node.Index], "a plain row starting at the end is kept: its smoothed edge touches the view");
        }

        [Test]
        public void AnOffscreenNodeIsNotHit()
        {
            var (_, boxes) = Feed(10);
            int presses = 0;
            boxes[6].Node.Pressable(() => presses++);
            Frame();
            var tree = Root.Node.Tree;
            // Where row 6 is laid out, below the scroll view: nothing there takes the pointer.
            Assert.AreEqual(-1, tree.HitTest(new Vector2(150f, 390f)));
            Assert.AreEqual(boxes[2].Node.Index, tree.HitTest(new Vector2(150f, 150f)));
        }
    }
}
