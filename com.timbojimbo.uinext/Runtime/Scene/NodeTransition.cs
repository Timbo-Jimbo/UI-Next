using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// How far a node is moved, scaled and faded on top of its own drawn values while something brings it in or takes
    /// it away (a presentation's sheet sliding up, a dialog popping in), so the transition never touches the values its
    /// author set. At rest it is no change at all.
    /// </summary>
    internal sealed class NodeTransition
    {
        public readonly AnimatedValue<Vector2> Offset = new(Vector2.zero, 0.25f, true);
        public readonly AnimatedValue<float> Scale = new(1f, 0.0005f, true);
        public readonly AnimatedValue<float> Opacity = new(1f, 0.002f, true);

        /// <summary>
        /// Puts it at <paramref name="offset"/>, <paramref name="scale"/> and <paramref name="opacity"/> at once, still,
        /// even inside a change (where it comes in from, before the change springs it in).
        /// </summary>
        public void Set(Vector2 offset, float scale, float opacity)
        {
            Offset.Jump(offset);
            Scale.Jump(scale);
            Opacity.Jump(opacity);
        }

        /// <summary>Sends it to <paramref name="offset"/>, <paramref name="scale"/> and <paramref name="opacity"/> (springing, inside a change).</summary>
        public void To(Vector2 offset, float scale, float opacity)
        {
            Offset.SetModel(offset, null);
            Scale.SetModel(scale, null);
            Opacity.SetModel(opacity, null);
        }
    }
}
