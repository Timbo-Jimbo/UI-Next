using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A photo in the demo: one of <see cref="CircleData.Photos"/>, shown by the <see cref="Image"/> on its node (loaded
    /// from the web through the demo's loader, as wide as it is drawn), which it hands the photo's URL. On a node with
    /// no image it stands in with a landscape of its own: a sky fading to a glow, a sun, and two hills, drawn with the
    /// painter's rounded rects inside the node's corners.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next Demos/Scene Photo")]
    public sealed class ScenePhoto : UIDrawable
    {
        [SerializeField] private UIValue<string> _url = "";
        [SerializeField] private UIValue<Color> _sky = new Color(0.37f, 0.66f, 0.83f);
        [SerializeField] private UIValue<Color> _glow = new Color(0.81f, 0.91f, 0.95f);
        [SerializeField] private UIValue<Color> _hills = new Color(0.24f, 0.42f, 0.35f);
        [SerializeField] private UIValue<float> _cornerRadius = 14f;

        /// <summary>The photo's URL, which the image on its node loads; empty for the landscape.</summary>
        public UIValue<string> Url => _url;
        public UIValue<Color> Sky => _sky;
        public UIValue<Color> Glow => _glow;
        public UIValue<Color> Hills => _hills;
        public UIValue<float> CornerRadius => _cornerRadius;

        private Image Photo => GetComponent<Image>();

        /// <summary>Shows the <paramref name="scene"/>th photo (of <see cref="CircleData.Photos"/>, or the landscape of <see cref="CircleData.Scenes"/>).</summary>
        public void Show(int scene)
        {
            var (sky, glow, hills) = CircleData.Scenes[Mathf.Abs(scene) % CircleData.Scenes.Length];
            _sky.Value = sky;
            _glow.Value = glow;
            _hills.Value = hills;
            var photos = CircleData.Photos;
            Load(photos.Length > 0 ? photos[Mathf.Abs(scene) % photos.Length] : "");
        }

        /// <summary>Shows what <paramref name="other"/> shows.</summary>
        public void CopyFrom(ScenePhoto other)
        {
            _sky.Value = other._sky.Peek;
            _glow.Value = other._glow.Peek;
            _hills.Value = other._hills.Peek;
            Load(other._url.Peek);
        }

        private void Load(string url)
        {
            _url.Value = url;
            var photo = Photo;
            if (photo != null)
                photo.Url.Value = url;
        }

        protected override void Draw(Painter painter)
        {
            // The image on the node shows the photo; nothing of this is painted over it.
            if (Photo != null) return;
            var size = painter.Size;
            if (size.x <= 0f || size.y <= 0f) return;
            float radius = _cornerRadius.Value;
            var glow = _glow.Value;
            var hills = _hills.Value;
            painter.GradientNode(_sky.Value, glow, 90f, radius);

            float sun = size.x * 0.16f;
            painter.Rect(new Rect(size.x * 0.66f, size.y * 0.3f, sun, sun), Color.Lerp(glow, Color.white, 0.6f), sun * 0.5f);

            // The far hill, lighter, and the near one, each a rounded top meeting the photo's own corners below.
            var far = Color.Lerp(hills, glow, 0.35f);
            var farRect = new Rect(size.x * 0.32f, size.y * 0.52f, size.x * 0.68f, size.y * 0.48f);
            painter.Rect(farRect, far, new CornerRadii(farRect.height * 0.9f, 0f, radius, 0f));
            var nearRect = new Rect(0f, size.y * 0.68f, size.x * 0.72f, size.y * 0.32f);
            painter.Rect(nearRect, hills, new CornerRadii(0f, nearRect.height * 1.4f, 0f, radius));
        }
    }
}
