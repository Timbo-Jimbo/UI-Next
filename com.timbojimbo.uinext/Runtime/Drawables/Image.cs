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
        [SerializeField] private UIValue<ImageLoader> _loader = new UIValue<ImageLoader>();
        [SerializeField] private UIValue<Color> _tint = Color.white;
        [SerializeField] private UIValue<ImageFit> _fit = ImageFit.Cover;
        [SerializeField] private UIValue<CornerRadii> _cornerRadii = new CornerRadii();

        // What it draws of its URL's image: the copy it asked for, once that arrives, and until then one standing in for it
        // (a narrower copy loaded already, or its loader's preview). A value drawn from, so a change of it on an animation
        // cross-fades.
        [System.NonSerialized] private UIValue<RemoteImages.Entry> _loaded = new UIValue<RemoteImages.Entry>();
        [System.NonSerialized] private Effect _load;
        // The copy it draws, the one it has asked for and waits for (its first, or a wider one), and one standing in until
        // it has a copy of its own.
        [System.NonSerialized] private RemoteImages.Entry _shown;
        [System.NonSerialized] private RemoteImages.Entry _wanted;
        [System.NonSerialized] private RemoteImages.Entry _standIn;
        // The URL and loader its copies are of, and the widest copy it has asked for, in pixels.
        [System.NonSerialized] private string _source;
        [System.NonSerialized] private ImageLoader _sourceLoader;
        [System.NonSerialized] private int _width;

        /// <summary>The sprite drawn, used over <see cref="Texture"/>.</summary>
        public UIValue<Sprite> Sprite => _sprite;

        /// <summary>A texture drawn whole when there is no sprite (a render texture, say).</summary>
        public UIValue<Texture> Texture => _texture;

        /// <summary>
        /// An image to load from the web and show instead of its sprite and texture: downloaded once and shared by every
        /// image showing it, and kept while anything shows it and for a while after. With a <see cref="Loader"/>, it asks
        /// for a copy only as wide as it is drawn. Until its copy arrives its node is in <see cref="UIStates.Loading"/> (in
        /// <see cref="UIStates.Failed"/> if it cannot be loaded), for variants of it and of what is inside it to follow, and
        /// the copy fades in as it arrives.
        /// </summary>
        public UIValue<string> Url => _url;

        /// <summary>
        /// How it asks for its URL's image (a CDN's resizing): once laid out, a copy as wide as it is drawn, rounded up to
        /// the loader's next width and never narrower than one it has asked for, a wider copy loaded already doing as well,
        /// and until it arrives a narrower copy loaded already, or the loader's preview, standing in. None for the project's
        /// default (Project Settings > Timbo Jimbo > UI Next). With neither, or with its node's width fitted to it (it is
        /// then shown at the image's own size), it loads its URL as it is.
        /// </summary>
        public UIValue<ImageLoader> Loader => _loader;

        /// <summary>
        /// Forgets every image loaded from a URL: those nothing shows go now, and images showing one load it again.
        /// </summary>
        public static void ClearCache() => RemoteImages.Clear();

        public UIValue<Color> Tint => _tint;
        public UIValue<ImageFit> Fit => _fit;
        public UIValue<CornerRadii> CornerRadii => _cornerRadii;

        /// <summary>
        /// Its native size in points, scaled down to <paramref name="availableWidth"/> keeping its aspect when that is
        /// narrower. A copy its loader asked for is scaled to the width either way: its pixels are only as many as it is drawn.
        /// </summary>
        public Vector2 Measure(float availableWidth)
        {
            var native = _native;
            bool scales = native.x > availableWidth || _scalesToWidth;
            if (availableWidth >= 0f && native.x > 0f && scales)
                native *= availableWidth / native.x;
            return native;
        }

        // What it measures, as its watch last read it: its native size in points, and whether it is scaled to the width
        // it is given whatever its size (a copy its loader asked for). Measuring reads none of its values.
        [System.NonSerialized] private Vector2 _native;
        [System.NonSerialized] private bool _scalesToWidth;
        [System.NonSerialized] private Effect _watch;

        // Reads what it shows again whenever that changes, as values resolve, before layout: when its size changes, its
        // node is laid out again (ContentChanged).
        private void WatchSize()
        {
            var texture = Source(out var sprite, out _, out var pixels);
            var native = sprite != null ? sprite.rect.size * (100f / Mathf.Max(1f, sprite.pixelsPerUnit))
                : texture != null ? pixels : Vector2.zero;
            bool scales = _sourceLoader != null && texture != null && sprite == null;
            if (native == _native && scales == _scalesToWidth) return;
            _native = native;
            _scalesToWidth = scales;
            ContentChanged();
        }

        protected internal override void Draw(Painter painter)
        {
            var texture = Source(out var sprite, out var uv, out var native);
            if (texture == null) return;

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
        // its texture; where on that texture it is, and how many pixels it is.
        private Texture Source(out Sprite sprite, out Rect uv, out Vector2 pixels)
        {
            sprite = null;
            uv = new Rect(0f, 0f, 1f, 1f);
            if (!string.IsNullOrEmpty(_url.Value))
            {
                var loaded = _loaded.Value;
                if (loaded == null)
                {
                    pixels = Vector2.zero;
                    return null;
                }
                uv = loaded.Uv;
                pixels = new Vector2(loaded.Width, loaded.Height);
                return loaded.Texture;
            }
            sprite = _sprite.Value;
            if (sprite != null)
            {
                var texture = sprite.texture;
                var rect = sprite.textureRect;
                uv = new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
                pixels = rect.size;
                return texture;
            }
            var own = _texture.Value;
            pixels = own != null ? new Vector2(own.width, own.height) : Vector2.zero;
            return own;
        }

        // ── Loading ──────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            _watch = new Effect(WatchSize);
            _watch.Invalidate();
            // After layout, to know how wide it is drawn.
            _load = new Effect(Load, Phase.Draw);
            _load.Invalidate();
        }

        protected override void OnDisable()
        {
            _watch?.Dispose();
            _watch = null;
            _native = Vector2.zero;
            _scalesToWidth = false;
            _load?.Dispose();
            _load = null;
            LetGo();
            _source = null;
            _sourceLoader = null;
            base.OnDisable();
        }

        // As its URL, its loader or the size it is laid out at changes: the copy of its URL's image to draw.
        private void Load()
        {
            using var load = UIProfiling.ImageLoad.Auto();
            string url = _url.Value;
            if (string.IsNullOrEmpty(url)) url = null;
            var loader = url != null ? LoaderFor() : null;
            if (url != _source || loader != _sourceLoader)
            {
                LetGo();
                _source = url;
                _sourceLoader = loader;
            }
            if (url == null) return;
            if (loader == null)
            {
                if (_shown == null && _wanted == null)
                    Want(url, 0, null, 0f);
                return;
            }

            var node = Node;
            var size = node.Frame.Model.size;
            // Asked for once it has been laid out with a width.
            if (size.x <= 0f) return;
            var tree = node.Tree;
            var root = tree != null ? tree.Root : null;
            float scale = root != null ? root.PixelsPerPoint : 1f;
            // Its node's shape, for a loader asking for it, unless its height is fitted to its image, whose own shape it is.
            bool shaped = loader.AsksForShape && size.y > 0f &&
                          !(node.Height.Value.Mode == SizingMode.Fit && node.AspectRatio.Value <= 0f);
            float shape = shaped ? size.y / size.x : 0f;

            float pixels = size.x * scale;
            var loaded = _loaded.Value;
            if (!shaped && loaded != null && loaded.Height > 0 && size.y > 0f)
            {
                // A copy of the image's own shape covering its node is wider than the node is; one fitting in it, narrower.
                float aspect = (float)loaded.Width / loaded.Height;
                var fit = _fit.Value;
                if (fit == ImageFit.Cover) pixels = Mathf.Max(pixels, size.y * aspect * scale);
                else if (fit == ImageFit.Contain) pixels = Mathf.Min(pixels, size.y * aspect * scale);
            }
            int width = loader.WidthFor(pixels);
            if (width <= _width) return;

            // A wider copy loaded already will do.
            string copy = loader.UrlFor(url, width, HeightAt(width, shape));
            foreach (int wider in loader.Widths ?? System.Array.Empty<int>())
            {
                if (wider <= width) continue;
                string candidate = loader.UrlFor(url, wider, HeightAt(wider, shape));
                if (!RemoteImages.IsLoaded(candidate)) continue;
                copy = candidate;
                width = wider;
                break;
            }
            Want(copy, width, loader, shape);
        }

        // The loader it asks with: its own, else the project's default; none if its node's width is fitted to it, as it is
        // then shown at the image's own size, which only the image as it is has.
        private ImageLoader LoaderFor()
        {
            var node = Node;
            if (node.Width.Value.Mode == SizingMode.Fit && node.AspectRatio.Value <= 0f) return null;
            var loader = _loader.Value;
            if (loader != null) return loader;
            UISettings.Watch();
            var settings = UISettings.Instance;
            return settings != null ? settings.DefaultImageLoader : null;
        }

        private static int HeightAt(int width, float shape) => shape > 0f ? Mathf.Max(1, Mathf.RoundToInt(width * shape)) : 0;

        // Asks for a copy (its first, or a wider one), drawn at once if it is loaded already; until it arrives, what it draws
        // stays, or, without a copy of its own yet, one stands in and its node is in Loading.
        private void Want(string url, int width, ImageLoader loader, float shape)
        {
            if (_wanted != null) Drop(ref _wanted);
            _width = width;
            var entry = Hold(url, width);
            if (!entry.Loading)
            {
                Arrive(entry, animate: false);
                return;
            }
            _wanted = entry;
            entry.Done += Arrived;
            if (_shown != null) return;
            if (loader != null && _standIn == null)
                StandIn(loader, width, shape);
            SetStates(loading: true, failed: false);
        }

        // Until it has a copy of its own: the widest narrower copy loaded already, else the loader's preview.
        private void StandIn(ImageLoader loader, int width, float shape)
        {
            int best = 0;
            string found = null;
            foreach (int narrower in loader.Widths ?? System.Array.Empty<int>())
            {
                if (narrower >= width || narrower <= best) continue;
                string candidate = loader.UrlFor(_source, narrower, HeightAt(narrower, shape));
                if (!RemoteImages.IsLoaded(candidate)) continue;
                best = narrower;
                found = candidate;
            }
            found ??= loader.PreviewFor(_source, width, HeightAt(width, shape));
            if (found == null) return;
            _standIn = Hold(found, best);
            if (_standIn.Loading)
                _standIn.Done += StoodIn;
            else if (_standIn.Texture != null)
                _loaded.Value = _standIn;
        }

        // A stand-in that had to load fades in, unless the copy came first.
        private void StoodIn(RemoteImages.Entry entry)
        {
            if (entry != _standIn || _shown != null || entry.Texture == null) return;
            UI.Animate(UIAnimation.Smooth, () => _loaded.Value = entry);
        }

        private void Arrived(RemoteImages.Entry entry)
        {
            if (entry == _wanted)
                Arrive(entry, animate: true);
        }

        // A copy it asked for has come, or failed to. Come, it is drawn, cross-fading over what stood in for it or the
        // narrower copy it replaces (fading in over nothing), which is let go of. Failed, what is drawn stays, the node in
        // Failed if it has no copy of its own (kept, so that it is not asked for again at that width).
        private void Arrive(RemoteImages.Entry entry, bool animate)
        {
            if (entry.Texture == null)
            {
                if (_shown != null)
                {
                    if (_wanted == entry) _wanted = null;
                    Drop(ref entry);
                    return;
                }
                _wanted = entry;
                Change(animate ? UIAnimation.Smooth : (UIAnimation?)null, () => SetStates(loading: false, failed: true));
                return;
            }
            if (_wanted == entry) _wanted = null;
            var replaced = _shown;
            _shown = entry;
            var animation = !animate ? (UIAnimation?)null
                : _loaded.Peek != null && _sourceLoader != null ? new UIAnimation(_sourceLoader.CrossFade)
                : UIAnimation.Smooth;
            Change(animation, () =>
            {
                _loaded.Value = entry;
                SetStates(loading: false, failed: false);
            });
            if (replaced != null) Drop(ref replaced);
            if (_standIn != null) Drop(ref _standIn);
        }

        private static void Change(UIAnimation? animation, System.Action change)
        {
            if (animation is { } on)
                UI.Animate(on, change);
            else
                change();
        }

        // Its copies forgotten (the cache cleared): it asks for them again.
        private void Reload(RemoteImages.Entry entry)
        {
            if (entry != _shown && entry != _wanted && entry != _standIn) return;
            LetGo();
            _source = null;
            _sourceLoader = null;
            _load?.Invalidate();
        }

        // A copy `width` pixels wide (0 for a preview). Asked for through a loader at the size it is drawn,
        // one small enough to share an atlas page needs no mipmaps; a wider one keeps them, as a narrower image may draw it
        // too (a wider copy loaded already doing for it).
        private RemoteImages.Entry Hold(string url, int width)
        {
            var entry = RemoteImages.Acquire(url, atlas: _sourceLoader != null && width <= ImageAtlas.MaxSize);
            entry.Dropped += Reload;
            return entry;
        }

        private void Drop(ref RemoteImages.Entry entry)
        {
            entry.Done -= Arrived;
            entry.Done -= StoodIn;
            entry.Dropped -= Reload;
            RemoteImages.Release(entry);
            entry = null;
        }

        private void LetGo()
        {
            bool held = _shown != null || _wanted != null || _standIn != null;
            if (_shown != null) Drop(ref _shown);
            if (_wanted != null) Drop(ref _wanted);
            if (_standIn != null) Drop(ref _standIn);
            _width = 0;
            if (!held) return;
            _loaded.Value = null;
            SetStates(loading: false, failed: false);
        }

        private void SetStates(bool loading, bool failed)
        {
            var node = Node;
            if (node == null) return;
            node.SetState(UIStates.Loading, loading);
            node.SetState(UIStates.Failed, failed);
        }
    }
}
