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
