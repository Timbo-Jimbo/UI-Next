using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Content that holds floating children of its node in its own flow, as a label's text holds its inline content
    /// (an element in a line, a block across it, a background behind a piece of a line). It is measured with those
    /// children at the sizes layout has given them, may size them along an axis, and places them, all in the same layout
    /// pass, as SwiftUI's Layout sizes and places its subviews and TextKit lays out its attachments' views: nothing is
    /// placed a frame late.
    /// </summary>
    internal interface IContentHost : IMeasurable
    {
        /// <summary>Its size at <paramref name="availableWidth"/> (negative: no limit), its children at the sizes they have so far.</summary>
        Vector2 Measure(float availableWidth, in HostedChildren children);

        /// <summary>The narrowest it can go, its children at the sizes they have so far.</summary>
        float Narrowest(in HostedChildren children);

        /// <summary>
        /// The size it gives <paramref name="child"/> along <paramref name="axis"/>, once its own content box is final
        /// that way (<paramref name="contentSize"/>; the other axis may not be yet); false leaves the child's own sizing
        /// to it.
        /// </summary>
        bool SizeChild(UINode child, int axis, Vector2 contentSize, in HostedChildren children, out float size);

        /// <summary>
        /// Where <paramref name="child"/>'s top-left goes in its content box, once its size is final; false places it as
        /// any floating node.
        /// </summary>
        bool PlaceChild(UINode child, Vector2 contentSize, in HostedChildren children, out Vector2 topLeft);
    }

    /// <summary>A content node's floating children as layout has them while it works: each one's node and size.</summary>
    internal readonly struct HostedChildren
    {
        private readonly SolverNode[] _nodes;
        private readonly int _parent;

        internal HostedChildren(SolverNode[] nodes, int parent)
        {
            _nodes = nodes;
            _parent = parent;
        }

        /// <summary>The size layout has given a floating child of the node so far; false for a node that is not one.</summary>
        public bool TryGetSize(UINode child, out Vector2 size) => TryGetSize(child, out size, out _);

        /// <summary>
        /// The size layout has given a floating child of the node so far, and the smallest it can be squeezed to (its
        /// content's own smallest, within its min and max); false for a node that is not one.
        /// </summary>
        public bool TryGetSize(UINode child, out Vector2 size, out Vector2 min)
        {
            if (_nodes != null)
            {
                for (int c = _nodes[_parent].FirstChild; c >= 0; c = _nodes[c].NextSibling)
                {
                    if (_nodes[c].Node != child || !_nodes[c].Floating.IsFloating) continue;
                    size = _nodes[c].Size;
                    min = _nodes[c].MinSize;
                    return true;
                }
            }
            size = min = default;
            return false;
        }
    }
}
