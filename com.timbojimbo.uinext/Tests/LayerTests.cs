using NUnit.Framework;
using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboTests.UINext
{
    // The compositor's layers: nothing within a layer overlaps, so a layer can draw kind by kind in any order.
    public sealed class LayerTests : UITestScene
    {
        [Test]
        public void AChildIsInTheLayerAboveItsParent()
        {
            var parent = Root.Node.Child("Parent").Sized(Sizing.Fixed(200f), Sizing.Fixed(200f));
            parent.Box(Color.white);
            var child = parent.Child("Child").Sized(Sizing.Fixed(50f), Sizing.Fixed(50f));
            child.Box(Color.red);
            Frame();
            Assert.AreEqual(Compositor.LayerOf(parent) + 1, Compositor.LayerOf(child));
        }

        [Test]
        public void SiblingsThatDoNotOverlapShareALayerAndOnesThatDoDoNot()
        {
            var column = Root.Node.Column(10f);
            var a = column.Child("A").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            a.Box(Color.white);
            var b = column.Child("B").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            b.Box(Color.white);
            // Moved up over A by its offset: drawn after A, so it must be above it.
            var c = column.Child("C").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            c.Box(Color.white);
            c.Offset.Value = new Vector2(0f, -70f);
            Frame();
            Assert.AreEqual(Compositor.LayerOf(a), Compositor.LayerOf(b), "A and B lie apart");
            Assert.Greater(Compositor.LayerOf(c), Compositor.LayerOf(b), "C lies over B");
        }

        [Test]
        public void SiblingsThatTouchOrReachEachOtherOnlyByShadowShareALayer()
        {
            var column = Root.Node.Column();
            // Rows that abut exactly: their smoothed edges share a pixel row, which is no overlap.
            var a = column.Child("A").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            a.Box(Color.white);
            var b = column.Child("B").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            b.Box(Color.white);
            // A card whose shadow reaches onto the one before it, and the one after it.
            var c = column.Child("C").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            var box = c.Box(Color.white);
            box.ShadowColor.Value = Color.black;
            box.ShadowBlur.Value = 30f;
            var d = column.Child("D").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            d.Box(Color.white);
            Frame();
            Assert.AreEqual(Compositor.LayerOf(a), Compositor.LayerOf(b), "rows that touch");
            Assert.AreEqual(Compositor.LayerOf(b), Compositor.LayerOf(c), "a shadow reaching up onto the row before");
            Assert.AreEqual(Compositor.LayerOf(c), Compositor.LayerOf(d), "a shadow reaching down onto the row after");
        }

        [Test]
        public void ANodeThatPaintsNothingCostsNoLayer()
        {
            // A header's stories, three containers deep, and a body's cards, two deep: none of the containers paint, and
            // the two areas lie apart, so every card shares one layer and draws in one run.
            var column = Root.Node.Column(10f);
            var header = column.Child("Header").Sized(Sizing.Fixed(300f), Sizing.Fixed(60f)).Row(10f);
            var sections = header.Child("Sections").Sized(Sizing.Fixed(300f), Sizing.Fixed(60f)).Row(10f);
            var stories = sections.Child("Stories").Sized(Sizing.Fixed(300f), Sizing.Fixed(60f)).Row(10f);
            var story = stories.Child("Story").Sized(Sizing.Fixed(50f), Sizing.Fixed(50f));
            story.Box(Color.white);
            var body = column.Child("Body").Sized(Sizing.Fixed(300f), Sizing.Fixed(200f)).Column(10f);
            var cards = body.Child("Cards").Sized(Sizing.Fixed(300f), Sizing.Fixed(200f)).Column(10f);
            var card = cards.Child("Card").Sized(Sizing.Fixed(300f), Sizing.Fixed(80f));
            card.Box(Color.white);
            Frame();
            Assert.AreEqual(Compositor.LayerOf(story), Compositor.LayerOf(card), "cards under paintless containers share a layer");
            // A container that paints a shadow alone costs no layer either: shadows draw first within a layer.
            var shadowed = column.Child("Shadowed").Sized(Sizing.Fixed(300f), Sizing.Fixed(80f));
            var box = shadowed.Box(new Color(1f, 1f, 1f, 0f));
            box.ShadowColor.Value = Color.black;
            box.ShadowBlur.Value = 10f;
            var inner = shadowed.Child("Inner").Sized(Sizing.Fixed(100f), Sizing.Fixed(40f));
            inner.Box(Color.white);
            Frame();
            Assert.AreEqual(2, shadowed.Instances.Count, "the shadow, and a fill at no alpha");
            Assert.AreEqual(Compositor.LayerOf(shadowed), Compositor.LayerOf(inner), "a child shares its parent's layer above a shadow and an invisible fill");
        }

        [Test]
        public void AShapeOverAnotherInOneNodeIsInTheLayerAbove()
        {
            var node = Root.Node.Child("Node").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            node.Box(Color.white);
            node.Box(Color.red);
            Frame();
            Assert.AreEqual(2, node.Instances.Count);
            // Both shapes cover the node: the second must be above the first, since a layer's shapes draw in any order.
            Assert.AreEqual(Compositor.LayerOf(node) + 1, Compositor.LayerOf(node, 1));
        }

        [Test]
        public void ANodeDrawnInTwoLayersStillPatchesInPlace()
        {
            var node = Root.Node.Child("Node").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            var box = node.Box(Color.white);
            node.Box(Color.red);
            Frame();
            int builds = Compositor.Builds;
            box.Fill.Value = Color.blue;
            Frame();
            Assert.AreEqual(builds, Compositor.Builds, "a redraw with as many primitives patches, it does not rebuild");
        }
    }
}
