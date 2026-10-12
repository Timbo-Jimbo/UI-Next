using System.Runtime.InteropServices;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>What a primitive draws as, which the shader branches on.</summary>
    public enum PrimitiveType : uint
    {
        /// <summary>A rounded rect: fill, optional border, optional gradient.</summary>
        Shape = 0,

        /// <summary>A soft rounded-rect shadow.</summary>
        Shadow = 1,

        /// <summary>A texture, cut to a rounded rect.</summary>
        Image = 2,

        /// <summary>A glyph from a signed distance field atlas.</summary>
        GlyphSdf = 3,

        /// <summary>A glyph from a bitmap or colour atlas (emoji).</summary>
        GlyphBitmap = 4,

        /// <summary>A group's image, already premultiplied, drawn at its group's opacity.</summary>
        Group = 5,

        /// <summary>
        /// A matched pair: two premultiplied images (a picture of where a flight came from, and where it goes), summed as
        /// they cross-fade (by Color's alpha).
        /// </summary>
        Pair = 6,
    }

    /// <summary>Flags a primitive carries.</summary>
    [System.Flags]
    public enum PrimitiveFlags : uint
    {
        None = 0,

        /// <summary>Its rect is insets from its node's edges (left, top, right, bottom), so it follows the node's drawn size without being drawn again.</summary>
        RelativeToNode = 1,

        /// <summary>Its fill (or a shadow's colour) is a gradient from its colour to its second colour, at the angle in Params.z (degrees).</summary>
        Gradient = 2,

        /// <summary>A shape drawn as its border alone, Params.x wide inside its edge, in its colour (or its gradient).</summary>
        Ring = 4,

        /// <summary>Drawn by a debug view (<see cref="UIDebug"/>): drawn as it is in the overdraw view, not counted.</summary>
        Debug = 8,

        /// <summary>An image drawn as a nine-slice: its borders in points in Params and as shares of its image in Color2 (each left, top, right, bottom).</summary>
        Sliced = 16,
    }

    /// <summary>
    /// One primitive as the shader reads it: a quad the vertex shader expands from this record. Lengths are points in its
    /// node's space, x right and y down from the node's top-left corner. Colours are sRGB with straight alpha.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PrimitiveInstance
    {
        public Vector4 Rect;
        public Vector4 Radii;
        public Vector4 Color;
        public Vector4 Color2;

        // Shape: x border width, z gradient angle. Shadow: y blur. Image: a nine-slice's borders. Glyph SDF: y synthetic bold's
        // widening and w an outline's width, as shares of the field (the outline in Color2), z italic's skew.
        public Vector4 Params;

        // The rect in the bound texture, in UV: x, y, width, height.
        public Vector4 Uv;

        public uint Slot;
        public uint Type;
        public uint Flags;
        // Which of its batch's textures it samples, and in the next four bits a second (a pair's); set by the compositor.
        public uint Texture;

        public const int Stride = 6 * 16 + 4 * 4;
    }

    /// <summary>One node as the shader reads it: where it is drawn in its root's space, its drawn size, opacity and clips.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NodeSlot
    {
        // Its affine transform from its own space to its root's: (a, b, tx) and (c, d, ty); and its colour filters, with
        // everything above it: saturation in Row0.w, brightness in Row1.w.
        public Vector4 Row0;
        public Vector4 Row1;

        // Its drawn size, its opacity (with everything above it), and points per pixel at its scale.
        public Vector4 SizeOpacity;

        // Up to four clip indices, -1 for none.
        public Vector4 Clips;

        public const int Stride = 4 * 16;
    }

    /// <summary>A clip as the shader reads it: a rounded rect, with the transform from its root's space into the clip's own.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ClipShape
    {
        public Vector4 Inverse0;
        public Vector4 Inverse1;
        public Vector4 Size;
        public Vector4 Radii;

        public const int Stride = 4 * 16;
    }
}
