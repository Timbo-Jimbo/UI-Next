using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>The radius of each corner of a rounded rect, in points.</summary>
    [Serializable]
    public struct CornerRadii : IEquatable<CornerRadii>
    {
        [Min(0f)] public float TopLeft;
        [Min(0f)] public float TopRight;
        [Min(0f)] public float BottomRight;
        [Min(0f)] public float BottomLeft;

        public CornerRadii(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        /// <summary>The same radius on every corner.</summary>
        public static CornerRadii All(float radius) => new(radius, radius, radius, radius);

        public static implicit operator CornerRadii(float radius) => All(radius);

        public Vector4 ToVector() => new(TopLeft, TopRight, BottomRight, BottomLeft);

        public bool Equals(CornerRadii other) =>
            TopLeft == other.TopLeft && TopRight == other.TopRight && BottomRight == other.BottomRight && BottomLeft == other.BottomLeft;

        public override bool Equals(object obj) => obj is CornerRadii other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft);

        public static bool operator ==(CornerRadii a, CornerRadii b) => a.Equals(b);

        public static bool operator !=(CornerRadii a, CornerRadii b) => !a.Equals(b);
    }
}
