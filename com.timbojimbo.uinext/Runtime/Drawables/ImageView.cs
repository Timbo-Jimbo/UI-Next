using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>How an image fills its node.</summary>
    public enum ImageFit
    {
        /// <summary>Stretched to the node's rect.</summary>
        Fill,

        /// <summary>As large as fits inside, keeping its aspect.</summary>
        Contain,

        /// <summary>Covering the rect, keeping its aspect, the rest cropped.</summary>
        Cover,
    }

    /// <summary>
    /// A sprite or texture drawn in its node, cut to rounded corners (a circle for an avatar). Its node fits to its
    /// native size, in points, when nothing else sizes it.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next/Image View")]
    public sealed class ImageView : UIDrawable, IMeasurable
    {
        [SerializeField] private UIValue<Sprite> _sprite = new UIValue<Sprite>();
        [SerializeField] private UIValue<Texture> _texture = new UIValue<Texture>();
        [SerializeField] private UIValue<Color> _tint = Color.white;
        [SerializeField] private UIValue<ImageFit> _fit = ImageFit.Cover;
        [SerializeField] private UIValue<CornerRadii> _cornerRadii = new CornerRadii();

        /// <summary>The sprite drawn, used over <see cref="Texture"/>.</summary>
        public UIValue<Sprite> Sprite => _sprite;

        /// <summary>A texture drawn whole when there is no sprite (a render texture, a downloaded image).</summary>
        public UIValue<Texture> Texture => _texture;

        public UIValue<Color> Tint => _tint;
        public UIValue<ImageFit> Fit => _fit;
        public UIValue<CornerRadii> CornerRadii => _cornerRadii;

        /// <summary>Its native size in points, scaled down to <paramref name="availableWidth"/> keeping its aspect when that is narrower.</summary>
        public Vector2 Measure(float availableWidth)
        {
            Vector2 native;
            var sprite = _sprite.Value;
            if (sprite != null)
            {
                native = sprite.rect.size * (100f / Mathf.Max(1f, sprite.pixelsPerUnit));
            }
            else
            {
                var texture = _texture.Value;
                native = texture != null ? new Vector2(texture.width, texture.height) : Vector2.zero;
            }
            if (availableWidth >= 0f && native.x > availableWidth && native.x > 0f)
                native *= availableWidth / native.x;
            return native;
        }

        public float MinWidth => 0f;

        protected internal override void Draw(Painter painter)
        {
            Texture texture;
            Rect uv;
            Vector2 native;
            var sprite = _sprite.Value;
            if (sprite != null)
            {
                texture = sprite.texture;
                var rect = sprite.textureRect;
                uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
                native = rect.size;
            }
            else
            {
                texture = _texture.Value;
                if (texture == null) return;
                uv = new Rect(0f, 0f, 1f, 1f);
                native = new Vector2(texture.width, texture.height);
            }

            var size = painter.Size;
            var fit = _fit.Value;
            var radii = _cornerRadii.Value;
            var tint = _tint.Value;
            if (fit == ImageFit.Fill || native.x <= 0f || native.y <= 0f || size.x <= 0f || size.y <= 0f)
            {
                painter.ImageNode(texture, uv, tint, radii);
                return;
            }

            float aspect = native.x / native.y, boxAspect = size.x / size.y;
            if (fit == ImageFit.Cover)
            {
                // Crop the texture: the part of it with the node's aspect.
                if (aspect > boxAspect)
                {
                    float keep = boxAspect / aspect;
                    uv = new Rect(uv.x + uv.width * (1f - keep) * 0.5f, uv.y, uv.width * keep, uv.height);
                }
                else
                {
                    float keep = aspect / boxAspect;
                    uv = new Rect(uv.x, uv.y + uv.height * (1f - keep) * 0.5f, uv.width, uv.height * keep);
                }
                painter.ImageNode(texture, uv, tint, radii);
                return;
            }

            // Contain: inset the image within the node.
            var inset = default(Insets);
            if (aspect > boxAspect)
            {
                float height = size.x / aspect;
                inset.Top = inset.Bottom = (size.y - height) * 0.5f;
            }
            else
            {
                float width = size.y * aspect;
                inset.Left = inset.Right = (size.x - width) * 0.5f;
            }
            painter.ImageNode(texture, uv, tint, radii, inset);
        }
    }
}
