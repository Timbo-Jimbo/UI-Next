using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// What a drawable draws with: it adds primitives to its node, in the node's own space (points, x right, y down from
    /// its top-left corner). Primitives relative to the node follow its drawn size as it springs, without the drawable
    /// being asked to draw again.
    /// </summary>
    public readonly struct Painter
    {
        private readonly UINode _node;

        internal Painter(UINode node) => _node = node;

        /// <summary>The node's laid-out size (where it is going), which content lays itself out at.</summary>
        public Vector2 Size => _node.Frame.Model.size;

        /// <summary>A rounded rect filling the node, inset by <paramref name="inset"/>, with an optional border inside its edge.</summary>
        public void FillNode(Color fill, CornerRadii radii, Insets inset = default, float borderWidth = 0f, Color border = default)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(inset.Left, inset.Top, inset.Right, inset.Bottom),
                Radii = radii.ToVector(),
                Color = fill,
                Color2 = border,
                Params = new Vector4(borderWidth, 0f, 0f, 0f),
                Type = (uint)PrimitiveType.Shape,
                Flags = (uint)PrimitiveFlags.RelativeToNode,
            }, null);
        }

        /// <summary>A rounded rect filling the node with a gradient from <paramref name="from"/> to <paramref name="to"/>, at <paramref name="angle"/> degrees (0 left to right, 90 top to bottom).</summary>
        public void GradientNode(Color from, Color to, float angle, CornerRadii radii, Insets inset = default)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(inset.Left, inset.Top, inset.Right, inset.Bottom),
                Radii = radii.ToVector(),
                Color = from,
                Color2 = to,
                Params = new Vector4(0f, 0f, angle, 0f),
                Type = (uint)PrimitiveType.Shape,
                Flags = (uint)(PrimitiveFlags.RelativeToNode | PrimitiveFlags.Gradient),
            }, null);
        }

        /// <summary>A soft shadow of the node's rounded rect, moved by <paramref name="offset"/> and grown by <paramref name="spread"/>.</summary>
        public void ShadowNode(Color color, CornerRadii radii, float blur, Vector2 offset, float spread = 0f)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(offset.x - spread, offset.y - spread, -offset.x - spread, -offset.y - spread),
                Radii = radii.ToVector() + Vector4.one * spread,
                Color = color,
                Params = new Vector4(0f, Mathf.Max(0f, blur), 0f, 0f),
                Type = (uint)PrimitiveType.Shadow,
                Flags = (uint)PrimitiveFlags.RelativeToNode,
            }, null);
        }

        /// <summary>A rounded rect at <paramref name="rect"/> in the node's space.</summary>
        public void Rect(Rect rect, Color fill, CornerRadii radii, float borderWidth = 0f, Color border = default)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(rect.x, rect.y, rect.width, rect.height),
                Radii = radii.ToVector(),
                Color = fill,
                Color2 = border,
                Params = new Vector4(borderWidth, 0f, 0f, 0f),
                Type = (uint)PrimitiveType.Shape,
            }, null);
        }

        /// <summary>
        /// A texture filling the node (inset by <paramref name="inset"/>), its <paramref name="uv"/> rect drawn tinted by
        /// <paramref name="tint"/> and cut to <paramref name="radii"/>.
        /// </summary>
        public void ImageNode(Texture texture, Rect uv, Color tint, CornerRadii radii, Insets inset = default)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(inset.Left, inset.Top, inset.Right, inset.Bottom),
                Radii = radii.ToVector(),
                Color = tint,
                Uv = new Vector4(uv.x, uv.y, uv.width, uv.height),
                Type = (uint)PrimitiveType.Image,
                Flags = (uint)PrimitiveFlags.RelativeToNode,
            }, texture);
        }

        /// <summary>A texture at <paramref name="rect"/> in the node's space.</summary>
        public void Image(Rect rect, Texture texture, Rect uv, Color tint, CornerRadii radii = default)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(rect.x, rect.y, rect.width, rect.height),
                Radii = radii.ToVector(),
                Color = tint,
                Uv = new Vector4(uv.x, uv.y, uv.width, uv.height),
                Type = (uint)PrimitiveType.Image,
            }, texture);
        }

        /// <summary>
        /// A glyph quad at <paramref name="rect"/> from an atlas: a signed distance field one with a distance range of
        /// <paramref name="sdfRange"/> texels, or a bitmap one (colour emoji) when <paramref name="sdf"/> is false.
        /// </summary>
        public void Glyph(Rect rect, Rect uv, Texture atlas, Color color, bool sdf, float sdfRange)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(rect.x, rect.y, rect.width, rect.height),
                Color = color,
                Params = new Vector4(sdfRange, 0f, 0f, 0f),
                Uv = new Vector4(uv.x, uv.y, uv.width, uv.height),
                Type = (uint)(sdf ? PrimitiveType.GlyphSdf : PrimitiveType.GlyphBitmap),
            }, atlas);
        }

        /// <summary>Adds a primitive as it is, with the texture it samples (null for none).</summary>
        public void Add(in PrimitiveInstance instance, Texture texture)
        {
            _node.Instances.Add(instance);
            _node.InstanceTextures.Add(texture);
        }
    }
}
