using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Content a node measures, such as a text or an image: a drawable on the node's object that says how big it is.
    /// A node with no children in its flow fits to it. Measuring runs inside the layout computation, so every
    /// <see cref="UIValue{T}"/> it reads is tracked as a layout input: change one and the tree is laid out again.
    /// </summary>
    public interface IMeasurable
    {
        /// <summary>Its size in points at <paramref name="availableWidth"/>, or at no limit when that is negative.</summary>
        Vector2 Measure(float availableWidth);

        /// <summary>The narrowest it can go: a wrapped text's longest word.</summary>
        float MinWidth { get; }
    }
}
