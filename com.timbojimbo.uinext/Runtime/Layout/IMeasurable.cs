using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Content a node measures, such as a text or an image: a drawable on the node's object that says how big it is.
    /// A node with no children in its flow fits to it. Layout measures it when it lays its node out, and goes by what it
    /// found when a pass does not reach the node, so measuring tracks nothing it reads: when what it measures changes,
    /// the drawable says so (<see cref="UIDrawable.ContentChanged"/>, from an <see cref="Effect"/> watching its values,
    /// say), and its node is laid out again.
    /// </summary>
    public interface IMeasurable
    {
        /// <summary>Its size in points at <paramref name="availableWidth"/>, or at no limit when that is negative.</summary>
        Vector2 Measure(float availableWidth);

        /// <summary>The narrowest it can go: a wrapped text's longest word.</summary>
        float MinWidth { get; }
    }
}
