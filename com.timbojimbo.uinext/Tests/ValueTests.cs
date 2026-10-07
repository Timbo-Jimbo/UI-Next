using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    public sealed class ValueTests : UITestScene
    {
        [Test]
        public void ABindingFollowsWhatItReads()
        {
            var node = Root.Node.Child("Node");
            var source = new Observable<float>(2f);
            node.Opacity.Bind(() => source.Value / 4f);
            Assert.AreEqual(0.5f, node.Opacity.Peek);
            source.Value = 4f;
            Frame();
            Assert.AreEqual(1f, node.Opacity.Peek);
        }

        [Test]
        public void TheVariantForAnActiveStateWins()
        {
            var node = Root.Node.Child("Node");
            var selected = UIState.Create("Chosen");
            var box = node.Box(Color.white);
            box.Fill.When(selected, Color.red);
            Assert.AreEqual(Color.white, box.Fill.Peek);
            node.SetState(selected);
            Frame();
            Assert.AreEqual(Color.red, box.Fill.Peek);
            node.SetState(selected, false);
            Frame();
            Assert.AreEqual(Color.white, box.Fill.Peek);
        }

        [Test]
        public void StatesReachEverythingInside()
        {
            var parent = Root.Node.Child("Parent");
            var child = parent.Child("Child");
            var warning = UIState.Create("Warning");
            child.Opacity.When(warning, 0.25f);
            parent.SetState(warning);
            Frame();
            Assert.AreEqual(0.25f, child.Opacity.Peek);
        }

        [Test]
        public void PressedOutranksHoveredOutranksTheLiteral()
        {
            var button = Root.Node.Child("Button");
            var box = button.Box(Color.white);
            box.Fill.When(InteractionStates.Hovered, Color.yellow);
            box.Fill.When(InteractionStates.Pressed, Color.red);
            // Added after the variants: the provider's arrival is noticed.
            button.Pressable();
            button.SetInteraction(InteractionStates.Hovered, true);
            Frame();
            Assert.AreEqual(Color.yellow, box.Fill.Peek);
            button.SetInteraction(InteractionStates.Pressed, true);
            Frame();
            Assert.AreEqual(Color.red, box.Fill.Peek);
        }

        [Test]
        public void ATokenTakesTheValueOfTheActiveMode()
        {
            var dark = UIState.Create("Dark", StateKind.Mode);
            var token = ScriptableObject.CreateInstance<ColorToken>();
            token.Default = Color.white;
            token.SetMode(dark, Color.black);
            var box = Root.Node.Child("Node").Box(Color.gray);
            box.Fill.Token = token;
            Assert.AreEqual(Color.white, box.Fill.Peek);
            Root.Node.SetState(dark);
            Frame();
            Assert.AreEqual(Color.black, box.Fill.Peek);
            Object.DestroyImmediate(token);
        }

        [Test]
        public void ASetMadeInAnimateSpringsWhereItIsDrawn()
        {
            var node = Root.Node.Child("Node").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f));
            node.Box(Color.white);
            Frame();
            UI.Animate(new UIAnimation(0.5f), () => node.Opacity.Value = 0f);
            Assert.AreEqual(0f, node.Opacity.Peek, "the model is where it is going at once");
            Frame(1f / 60f);
            Frame(0.1f);
            float drawn = node.Opacity.Presented;
            Assert.Greater(drawn, 0f);
            Assert.Less(drawn, 1f);
            Run(2f);
            Assert.AreEqual(0f, node.Opacity.Presented);
            Assert.IsFalse(node.Opacity.IsAnimating);
        }
    }
}
