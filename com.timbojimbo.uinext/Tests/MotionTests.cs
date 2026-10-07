using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    public sealed class MotionTests : UITestScene
    {
        [Test]
        public void ACriticallyDampedSpringNeverOvershoots()
        {
            UIAnimation.Smooth.Parameters(out float omega, out float zeta);
            float x = 1f, v = 0f;
            for (int i = 0; i < 120; i++)
            {
                SpringStore.Evolve(omega, zeta, 1f / 60f, ref x, ref v);
                Assert.GreaterOrEqual(x, -1e-5f);
            }
            Assert.Less(Mathf.Abs(x), 1e-3f);
        }

        [Test]
        public void ABouncySpringOvershoots()
        {
            UIAnimation.Bouncy.Parameters(out float omega, out float zeta);
            float x = 1f, v = 0f, lowest = 1f;
            for (int i = 0; i < 120; i++)
            {
                SpringStore.Evolve(omega, zeta, 1f / 60f, ref x, ref v);
                lowest = Mathf.Min(lowest, x);
            }
            Assert.Less(lowest, 0f);
        }

        [Test]
        public void SteppingInSmallStepsOrOneBigStepLandsInTheSamePlace()
        {
            UIAnimation.Snappy.Parameters(out float omega, out float zeta);
            float a = 1f, va = 0.5f, b = 1f, vb = 0.5f;
            for (int i = 0; i < 30; i++)
                SpringStore.Evolve(omega, zeta, 1f / 60f, ref a, ref va);
            SpringStore.Evolve(omega, zeta, 0.5f, ref b, ref vb);
            Assert.AreEqual(a, b, 1e-4f);
            Assert.AreEqual(va, vb, 1e-3f);
        }

        [Test]
        public void ARectMovedByLayoutInsideAnimateSprings()
        {
            var column = Root.Node.Column();
            var first = column.Child("First").Sized(Sizing.Fixed(100f), Sizing.Fixed(50f));
            var second = column.Child("Second").Sized(Sizing.Fixed(100f), Sizing.Fixed(50f));
            first.Box(Color.white);
            second.Box(Color.white);
            Frame();
            Assert.AreEqual(50f, second.LaidOutRect.y);

            UI.Animate(new UIAnimation(0.5f), () => first.Height.Value = Sizing.Fixed(150f));
            Assert.AreEqual(150f, second.LaidOutRect.y, "layout puts it where it is going at once");
            Frame(1f / 60f);
            Frame(0.1f);
            Assert.Greater(second.PresentedRect.y, 50f);
            Assert.Less(second.PresentedRect.y, 150f);
            Run(2f);
            Assert.AreEqual(150f, second.PresentedRect.y);
        }

        [Test]
        public void TurnedRoundMidWayItKeepsItsSpeed()
        {
            var node = Root.Node.Child("Node").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f));
            node.Box(Color.white);
            Frame();
            UI.Animate(new UIAnimation(0.6f), () => node.Offset.Value = new Vector2(100f, 0f));
            Frame(1f / 60f);
            Run(0.15f);
            var velocity = node.Offset.Velocity;
            Assert.Greater(velocity.x, 0f);
            UI.Animate(new UIAnimation(0.6f), () => node.Offset.Value = Vector2.zero);
            Assert.AreEqual(velocity.x, node.Offset.Velocity.x, 1e-3f, "it sets off back from where it is, at the speed it had");
        }

        [Test]
        public void ANodeHiddenInsideAnimateStaysDrawnWhileItGoes()
        {
            var column = Root.Node.Column();
            var first = column.Child("First").Sized(Sizing.Fixed(100f), Sizing.Fixed(50f));
            first.Box(Color.white);
            var transition = first.Add<DisplayTransition>();
            var second = column.Child("Second").Sized(Sizing.Fixed(100f), Sizing.Fixed(50f));
            second.Box(Color.white);
            Frame();

            UI.Animate(new UIAnimation(0.4f), () => first.Display.Value = DisplayMode.None);
            Assert.AreEqual(0f, second.LaidOutRect.y, "what was below closes up at once in layout");
            Frame(1f / 60f);
            Frame(0.1f);
            Assert.IsTrue(first.Leaving.Peek);
            int index = first.Index;
            Assert.IsTrue(first.Tree.Shown[index], "still drawn while it goes");
            Assert.Less(first.Tree.Opacities[index], 1f, "fading");
            Assert.Greater(first.Tree.GroupOpacities[index], 0f, "as one image");
            Assert.Greater(second.PresentedRect.y, 0f, "and what was below springs up");
            Run(2f);
            Assert.IsFalse(first.Leaving.Peek);
            Assert.IsFalse(first.Tree.Shown[first.Index]);

            UI.Animate(new UIAnimation(0.4f), () => first.Display.Value = DisplayMode.Visible);
            Assert.AreEqual(new Rect(0f, 0f, 100f, 50f), first.PresentedRect, "it comes back where it goes, not from where it was");
            Frame(1f / 60f);
            Frame(0.1f);
            Assert.Less(first.Tree.Opacities[first.Index], 1f, "fading in");
            Run(2f);
            Assert.AreEqual(1f, first.Tree.Opacities[first.Index]);
            Assert.AreEqual(0f, first.Tree.GroupOpacities[first.Index], "no image of its own once it is in");
            Object.DestroyImmediate(transition);
        }

        [Test]
        public void AChangeFinishesWhenEverythingItMovedHasLanded()
        {
            var node = Root.Node.Child("Node").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f));
            node.Box(Color.white);
            Frame();
            bool finished = false;
            var change = UI.Animate(new UIAnimation(0.3f), () => node.Scale.Value = 2f);
            change.Finished += () => finished = true;
            Frame(1f / 60f);
            Assert.IsFalse(finished);
            Run(2f);
            Assert.IsTrue(finished);
            Assert.IsTrue(change.Completed);
        }

        [Test]
        public void AChangeTakenOverDoesNotComplete()
        {
            var node = Root.Node.Child("Node").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f));
            node.Box(Color.white);
            Frame();
            var first = UI.Animate(new UIAnimation(0.5f), () => node.Scale.Value = 2f);
            Frame(1f / 60f);
            Run(0.1f);
            var second = UI.Animate(new UIAnimation(0.5f), () => node.Scale.Value = 3f);
            Run(2f);
            Assert.IsTrue(first.IsFinished);
            Assert.IsFalse(first.Completed);
            Assert.IsTrue(second.Completed);
        }
    }
}
