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

        /// <summary>
        /// Stretched to the node's rect as a nine-slice: its sprite's borders (set in the Sprite Editor) keep their size in
        /// points, shrinking together only where the node is smaller than they are, while the edges between them stretch
        /// along and the middle both ways. An image with no borders is stretched whole.
        /// </summary>
        Sliced,
    }

    /// <summary>
    /// A sprite, a texture or an image loaded from a URL, drawn in its node, cut to rounded corners (a circle for an
    /// avatar), or a sprite nine-sliced to any size (<see cref="ImageFit.Sliced"/>). Its node fits to its native size, in
    /// points, when nothing else sizes it.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next/Image")]
    public sealed class Image : UIDrawable, IMeasurable
    {
        [SerializeField] private UIValue<Sprite> _sprite = new UIValue<Sprite>();
        [SerializeField] private UIValue<Texture> _texture = new UIValue<Texture>();
        [SerializeField] private UIValue<string> _url = new UIValue<string>();
        [SerializeField] private UIValue<Color> _tint = Color.white;
        [SerializeField] private UIValue<ImageFit> _fit = ImageFit.Cover;
        [SerializeField] private UIValue<CornerRadii> _cornerRadii = new CornerRadii();

        // The image loaded from its URL, once it has arrived: a value drawn from, so its arriving on an animation fades it in.
        [System.NonSerialized] private UIValue<Texture> _loaded = new UIValue<Texture>();
        [System.NonSerialized] private RemoteImages.Entry _entry;
        [System.NonSerialized] private Effect _loader;

        /// <summary>The sprite drawn, used over <see cref="Texture"/>.</summary>
        public UIValue<Sprite> Sprite => _sprite;

        /// <summary>A texture drawn whole when there is no sprite (a render texture, say).</summary>
        public UIValue<Texture> Texture => _texture;

        /// <summary>
        /// An image to load from the web and show instead of its sprite and texture: downloaded once and shared by every
        /// image showing it, and kept while anything shows it and for a while after. Until it arrives its node is in
        /// <see cref="UIStates.Loading"/> (in <see cref="UIStates.Failed"/> if it cannot be loaded), for variants of it and
        /// of what is inside it to follow, and it fades in as it arrives.
        /// </summary>
        public UIValue<string> Url => _url;

        /// <summary>
        /// Forgets every image loaded from a URL: those nothing shows go now, and images showing one load it again.
        /// </summary>
        public static void ClearCache() => RemoteImages.Clear();

        public UIValue<Color> Tint => _tint;
        public UIValue<ImageFit> Fit => _fit;
        public UIValue<CornerRadii> CornerRadii => _cornerRadii;

        /// <summary>Its native size in points, scaled down to <paramref name="availableWidth"/> keeping its aspect when that is narrower.</summary>
        public Vector2 Measure(float availableWidth)
        {
            Vector2 native;
            Source(out var sprite, out var texture);
            if (sprite != null)
                native = sprite.rect.size * (100f / Mathf.Max(1f, sprite.pixelsPerUnit));
            else
                native = texture != null ? new Vector2(texture.width, texture.height) : Vector2.zero;
            if (availableWidth >= 0f && native.x > availableWidth && native.x > 0f)
                native *= availableWidth / native.x;
            return native;
        }

        public float MinWidth => 0f;

        protected internal override void Draw(Painter painter)
        {
            Source(out var sprite, out var texture);
            if (texture == null) return;
            Rect uv;
            Vector2 native;
            if (sprite != null)
            {
                var rect = sprite.textureRect;
                uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
                native = rect.size;
            }
            else
            {
                uv = new Rect(0f, 0f, 1f, 1f);
                native = new Vector2(texture.width, texture.height);
            }

            var size = painter.Size;
            var fit = _fit.Value;
            var radii = _cornerRadii.Value;
            var tint = _tint.Value;
            if (fit == ImageFit.Sliced && sprite != null && sprite.border != Vector4.zero)
            {
                // The sprite's borders (left, bottom, right, top, in its pixels) in points, as its size is measured.
                float points = 100f / Mathf.Max(1f, sprite.pixelsPerUnit);
                var border = sprite.border * points;
                painter.SlicedImageNode(texture, uv, sprite.rect.size * points, new Insets(border.x, border.z, border.w, border.y), tint, radii);
                return;
            }
            if (fit == ImageFit.Fill || fit == ImageFit.Sliced || native.x <= 0f || native.y <= 0f || size.x <= 0f || size.y <= 0f)
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

        // What it shows: the image loaded from its URL when it has one (nothing until that arrives), else its sprite, else
        // its texture.
        private void Source(out Sprite sprite, out Texture texture)
        {
            if (!string.IsNullOrEmpty(_url.Value))
            {
                sprite = null;
                texture = _loaded.Value;
                return;
            }
            sprite = _sprite.Value;
            texture = sprite != null ? sprite.texture : _texture.Value;
        }

        // ── Loading ──────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _loader = new Effect(Load);
            _loader.Invalidate();
        }

        protected override void OnDisable()
        {
            _loader?.Dispose();
            _loader = null;
            LetGo();
            base.OnDisable();
        }

        // The image at its URL asked for as the URL changes, the one asked for before let go.
        private void Load()
        {
            string url = _url.Value;
            if (string.IsNullOrEmpty(url)) url = null;
            if (_entry?.Url == url) return;
            LetGo();
            if (url == null) return;
            _entry = RemoteImages.Acquire(url);
            _entry.Dropped += Reload;
            if (_entry.Loading)
            {
                _entry.Done += Arrived;
                Show(null, loading: true, failed: false);
            }
            else
            {
                Show(_entry.Texture, loading: false, failed: _entry.Texture == null);
            }
        }

        // As it arrives it fades in over what was there, as any value drawn from does when it changes on an animation.
        private void Arrived(RemoteImages.Entry entry)
        {
            if (entry != _entry) return;
            UI.Animate(UIAnimation.Smooth, () => Show(entry.Texture, loading: false, failed: entry.Texture == null));
        }

        // Its image forgotten (the cache cleared): it loads it again.
        private void Reload(RemoteImages.Entry entry)
        {
            if (entry != _entry) return;
            LetGo();
            _loader?.Invalidate();
        }

        private void LetGo()
        {
            if (_entry == null) return;
            _entry.Done -= Arrived;
            _entry.Dropped -= Reload;
            RemoteImages.Release(_entry);
            _entry = null;
            Show(null, loading: false, failed: false);
        }

        private void Show(Texture texture, bool loading, bool failed)
        {
            _loaded.Value = texture;
            var node = Node;
            if (node == null) return;
            node.SetState(UIStates.Loading, loading);
            node.SetState(UIStates.Failed, failed);
        }
    }
}
