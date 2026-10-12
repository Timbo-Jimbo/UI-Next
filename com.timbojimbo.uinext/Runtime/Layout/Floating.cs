using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Floats a node out of its parent's flow, as Clay's floating elements: it takes no space there, and is placed so
    /// that its <see cref="Point"/> meets <see cref="TargetPoint"/> on what it is attached to (its parent, its root, or
    /// an <see cref="Element"/>), moved by <see cref="Offset"/>. It is still sized as any node is, growing to fill what
    /// it is attached to or a percentage of it: of its whole rect, padding and all (the root's safe rect, for one
    /// attached to the root), as Clay's floating elements and CSS's absolutely positioned boxes are, so a background
    /// that grows both ways reaches its parent's edges.
    /// </summary>
    [Serializable]
    public struct Floating
    {
        [Tooltip("What it is placed against: nothing (it is in its parent's flow), its parent, its layout root, or an element (another node in its tree).")]
        public FloatingAttach AttachTo;

        /// <summary>
        /// The node it is placed against when <see cref="AttachTo"/> is <see cref="FloatingAttach.Element"/>, as Clay's
        /// floating elements attach to an element by id: any node laid out in the same tree, before or after it. Grow
        /// and percent sizes are of the element's whole size, and it stays on the element as drawn while the scroll
        /// containers the element is inside scroll, though it is drawn (and clipped) where it is in the hierarchy.
        /// Without one laid out in its tree (none, disabled, Display None, or in another tree), or with one it cannot
        /// be placed after (itself, something inside it, or one attached back to it), it is placed against its parent.
        /// </summary>
        [Tooltip("The node it is placed against when attached to an element: any node laid out in the same tree. It follows the element as its scroll containers scroll. Without one, it is placed against its parent.")]
        public UINode Element;

        [Tooltip("The point on this node that is placed.")]
        public AttachPoint Point;

        [Tooltip("The point on what it is attached to that it is placed at.")]
        public AttachPoint TargetPoint;

        [Tooltip("How far it is moved from there, in points, x right and y down.")]
        public Vector2 Offset;

        /// <summary>Whether it floats at all.</summary>
        public bool IsFloating => AttachTo != FloatingAttach.None;
    }
}
