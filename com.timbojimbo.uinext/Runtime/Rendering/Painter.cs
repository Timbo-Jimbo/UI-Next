using TimboJimbo.UINext.Text;
using TimboJimbo.UINext.Text.Bridge;
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
        private readonly bool _overlay;

        internal Painter(UINode node, bool overlay = false)
        {
            _node = node;
            _overlay = overlay;
        }

        /// <summary>A painter for the same node whose primitives are drawn over its children rather than under them.</summary>
        public Painter Overlay => new(_node, true);

        /// <summary>The node's laid-out size (where it is going), which content lays itself out at.</summary>
        public Vector2 Size => _node.Frame.Model.size;

        /// <summary>Where content goes: the node's laid-out size inside its padding, in its own space.</summary>
        public Rect ContentRect
        {
            get
            {
                var size = Size;
                var padding = _node.Padding.Value;
                return new Rect(padding.Left, padding.Top, Mathf.Max(0f, size.x - padding.Horizontal), Mathf.Max(0f, size.y - padding.Vertical));
            }
        }

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

        /// <summary>
        /// A soft shadow of the node's rounded rect, moved by <paramref name="offset"/> and grown by <paramref name="spread"/>:
        /// in <paramref name="color"/>, or with <paramref name="to"/>, a gradient from it to that at <paramref name="angle"/>
        /// degrees.
        /// </summary>
        public void ShadowNode(Color color, CornerRadii radii, float blur, Vector2 offset, float spread = 0f, Color? to = null,
            float angle = 90f)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(offset.x - spread, offset.y - spread, -offset.x - spread, -offset.y - spread),
                Radii = radii.ToVector() + Vector4.one * spread,
                Color = color,
                Color2 = to ?? color,
                Params = new Vector4(0f, Mathf.Max(0f, blur), angle, 0f),
                Type = (uint)PrimitiveType.Shadow,
                Flags = (uint)(PrimitiveFlags.RelativeToNode | (to.HasValue ? PrimitiveFlags.Gradient : PrimitiveFlags.None)),
            }, null);
        }

        /// <summary>
        /// The node's rounded rect's border alone, <paramref name="width"/> wide inside its edge: in <paramref name="from"/>,
        /// or with <paramref name="to"/>, a gradient from it to that at <paramref name="angle"/> degrees.
        /// </summary>
        public void BorderNode(Color from, float width, CornerRadii radii, Color? to = null, float angle = 90f, Insets inset = default)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(inset.Left, inset.Top, inset.Right, inset.Bottom),
                Radii = radii.ToVector(),
                Color = from,
                Color2 = to ?? from,
                Params = new Vector4(Mathf.Max(0f, width), 0f, angle, 0f),
                Type = (uint)PrimitiveType.Shape,
                Flags = (uint)(PrimitiveFlags.RelativeToNode | PrimitiveFlags.Ring | (to.HasValue ? PrimitiveFlags.Gradient : PrimitiveFlags.None)),
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
        /// A laid-out text with its top-left corner at <paramref name="origin"/> (layout pixels are points): its glyphs,
        /// as signed distance fields (colour emoji keeping their own colours), and its underlines and strikethroughs, each
        /// run's colour multiplied by <paramref name="tint"/>, with an <paramref name="outline"/> round each glyph
        /// <paramref name="outlineWidth"/> points wide (as far as its atlas's padding reaches). A
        /// <paramref name="silhouette"/> (a shadow) leaves colour emoji out.
        /// </summary>
        public void Text(TextLayout layout, Vector2 origin, Color tint, float outlineWidth = 0f, Color outline = default,
            bool silhouette = false)
        {
            float left = origin.x;
            float top = origin.y;

            var decorations = layout.Decorations;
            for (int i = 0; i < decorations.Count; i++)
            {
                var decoration = decorations[i];
                var rect = decoration.Px;
                rect.x += left;
                rect.y += top;
                Rect(rect, Multiply(decoration.Color, tint), default);
            }

            var handle = layout.Handle;
            for (int g = 0; g < handle.GroupCount; g++)
            {
                var group = handle.GetGroup(g);
                if (AtgSpriteAssets.IsPlaceholder(in group) || (silhouette && group.IsBitmap)) continue;
                // Synthetic bold widens the glyph by a weight the engine gives in its own units; as a share of the
                // field, through this atlas's padding, so it is the same in texels whatever the padding.
                float dilateScale = group.AtlasPadding > 0 ? 0.5f / group.AtlasPadding * BoldTexelsPerUnit : 0f;
                for (int q = 0; q < group.Count; q++)
                {
                    handle.GetQuad(g, q, out var quad);
                    var topLeft = quad.TopLeft.Position;
                    var bottomLeft = quad.BottomLeft.Position;
                    var bottomRight = quad.BottomRight.Position;
                    var rect = new Rect(left + bottomLeft.x, top + topLeft.y, bottomRight.x - bottomLeft.x, bottomLeft.y - topLeft.y);
                    float skew = topLeft.x - bottomLeft.x;
                    var uvMin = quad.BottomLeft.Uv;
                    var uvMax = quad.TopRight.Uv;
                    var uv = new Rect(uvMin, uvMax - uvMin);
                    // Colour bitmaps (emoji) keep their own colours: only the alpha is tinted.
                    var color = group.IsBitmap
                        ? new Color(1f, 1f, 1f, quad.BottomLeft.Color.a / 255f * tint.a)
                        : Multiply(quad.BottomLeft.Color, tint);
                    float dilate = quad.BottomLeft.Dilate * dilateScale;
                    if (outlineWidth <= 0f || group.IsBitmap || group.AtlasPadding <= 0 || group.Atlas == null || rect.width <= 0f)
                    {
                        Glyph(rect, skew, uv, group.Atlas, color, !group.IsBitmap, dilate);
                        continue;
                    }
                    // The outline as a share of the field: its width in atlas texels (by how many the quad spans across
                    // its width), the field falling from 0.5 at the edge to nothing at the padding, short of that.
                    float texelsPerPoint = Mathf.Abs(uv.width) * group.Atlas.width / rect.width;
                    float share = Mathf.Min(outlineWidth * texelsPerPoint * 0.5f / group.AtlasPadding, 0.45f - dilate);
                    Add(new PrimitiveInstance
                    {
                        Rect = new Vector4(rect.x, rect.y, rect.width, rect.height),
                        Color = color,
                        Color2 = new Color(outline.r, outline.g, outline.b, outline.a * tint.a),
                        Params = new Vector4(0f, dilate, skew, Mathf.Max(0f, share)),
                        Uv = new Vector4(uv.x, uv.y, uv.width, uv.height),
                        Type = (uint)PrimitiveType.GlyphSdf,
                    }, group.Atlas);
                }
            }
        }

        // Atlas texels one unit of the engine's bold weight dilates a glyph by, as the text engine's own drawing has it.
        private const float BoldTexelsPerUnit = 2.25f;

        private static Color Multiply(Color32 run, Color tint) =>
            new(run.r / 255f * tint.r, run.g / 255f * tint.g, run.b / 255f * tint.b, run.a / 255f * tint.a);

        /// <summary>
        /// A glyph quad at <paramref name="rect"/> from an atlas, its top moved right by <paramref name="skew"/> (italic):
        /// a signed distance field one, widened by <paramref name="dilate"/> (a share of the field, for synthetic bold),
        /// or a colour bitmap one (emoji) when <paramref name="sdf"/> is false.
        /// </summary>
        public void Glyph(Rect rect, float skew, Rect uv, Texture atlas, Color color, bool sdf, float dilate = 0f)
        {
            Add(new PrimitiveInstance
            {
                Rect = new Vector4(rect.x, rect.y, rect.width, rect.height),
                Color = color,
                Params = new Vector4(0f, dilate, skew, 0f),
                Uv = new Vector4(uv.x, uv.y, uv.width, uv.height),
                Type = (uint)(sdf ? PrimitiveType.GlyphSdf : PrimitiveType.GlyphBitmap),
            }, atlas);
        }

        /// <summary>Adds a primitive as it is, with the texture it samples (null for none).</summary>
        public void Add(in PrimitiveInstance instance, Texture texture)
        {
            if (_overlay)
            {
                _node.OverlayInstances.Add(instance);
                _node.OverlayTextures.Add(texture);
                return;
            }
            _node.Instances.Add(instance);
            _node.InstanceTextures.Add(texture);
        }
    }
}
