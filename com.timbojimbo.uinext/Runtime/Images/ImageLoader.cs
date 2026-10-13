using System;
using System.Globalization;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// How images ask for what they load, as Next.js's image loader does: the URL of a copy as wide as an image is drawn,
    /// rounded up to one of a few widths so the CDN and the cache keep few copies of each image, and of a small preview
    /// shown while that loads. Its templates name what changes: <c>{url}</c> the image's URL, <c>{origin}</c> its scheme
    /// and host and <c>{path}</c> the rest of it (for CDNs taking their options in the path), and <c>{width}</c> and
    /// <c>{height}</c> the copy's size in pixels. With <c>{height}</c>, a copy is asked for in its image node's shape, for
    /// the CDN to crop to (Bunny's <c>aspect_ratio={width}:{height}</c>, Cloudflare's <c>fit=cover</c>, imgix's
    /// <c>fit=crop</c>, Cloudinary's <c>c_fill</c>); where a node has no shape of its own (its height fitted to its image),
    /// the part of the template naming <c>{height}</c> is left out, between the separators around it (?, &amp;, / or ,).
    /// Assign one to an image (<see cref="Image.Loader"/>) or as the project's default (Project Settings > Timbo Jimbo >
    /// UI Next); without one, an image loads its URL as it is. Subclass it to make URLs in code.
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Image Loader", fileName = "Image Loader")]
    public class ImageLoader : ScriptableObject
    {
        private static readonly char[] Separators = { '?', '&', '/', ',' };

        [Tooltip("The URL of a copy of an image: {url} the image's URL, {origin} its scheme and host, {path} the rest of it, and {width} and {height} the copy's size in pixels. With {height}, a copy is asked for in its image node's shape, for the CDN to crop to.")]
        [SerializeField] private string _template = "{url}?width={width}";

        [Tooltip("The URL of a small copy shown while a copy loads (a blurred one, say), or empty for none. The same names; {width} and {height} are the copy's, for its shape.")]
        [SerializeField] private string _previewTemplate = "";

        [Tooltip("The widths asked for, in pixels: an image asks for the narrowest at least as wide as it is drawn, or the widest.")]
        [SerializeField] private int[] _widths = { 64, 128, 256, 384, 512, 768, 1024, 1536, 2048 };

        [Tooltip("How long a copy takes to fade in over what stood in for it, in seconds.")]
        [SerializeField, Min(0f)] private float _crossFade = 0.3f;

        /// <summary>The URL of a copy of an image, with the names above.</summary>
        public string Template
        {
            get => _template;
            set
            {
                _template = value;
                UISettings.NotifyChanged();
            }
        }

        /// <summary>The URL of a small copy shown while a copy loads, or empty for none.</summary>
        public string PreviewTemplate
        {
            get => _previewTemplate;
            set
            {
                _previewTemplate = value;
                UISettings.NotifyChanged();
            }
        }

        /// <summary>The widths asked for, in pixels.</summary>
        public int[] Widths
        {
            get => _widths;
            set
            {
                _widths = value;
                UISettings.NotifyChanged();
            }
        }

        /// <summary>How long a copy takes to fade in over what stood in for it, in seconds.</summary>
        public float CrossFade
        {
            get => _crossFade;
            set => _crossFade = Mathf.Max(0f, value);
        }

        /// <summary>Whether a copy is asked for in its image node's shape (its template names {height}).</summary>
        public virtual bool AsksForShape => _template != null && _template.Contains("{height}");

        /// <summary>The width asked for to draw an image <paramref name="pixels"/> wide: the narrowest of its widths at least that wide, or the widest.</summary>
        public int WidthFor(float pixels)
        {
            if (_widths == null || _widths.Length == 0)
                return Mathf.Max(1, Mathf.CeilToInt(pixels));
            int best = int.MaxValue, widest = 0;
            foreach (int width in _widths)
            {
                if (width >= pixels && width < best) best = width;
                if (width > widest) widest = width;
            }
            return best != int.MaxValue ? best : widest;
        }

        /// <summary>
        /// The URL of a copy of <paramref name="url"/> <paramref name="width"/> pixels wide and <paramref name="height"/>
        /// high, or of the image's own shape when that is 0.
        /// </summary>
        public virtual string UrlFor(string url, int width, int height) => Fill(_template, url, width, height);

        /// <summary>
        /// The URL of a small copy of <paramref name="url"/> to show while the copy <paramref name="width"/> by
        /// <paramref name="height"/> loads, or null for none.
        /// </summary>
        public virtual string PreviewFor(string url, int width, int height) =>
            string.IsNullOrEmpty(_previewTemplate) ? null : Fill(_previewTemplate, url, width, height);

        /// <summary>A template filled in for a copy of <paramref name="url"/>; with no height, the part naming it left out.</summary>
        protected static string Fill(string template, string url, int width, int height)
        {
            if (string.IsNullOrEmpty(template)) return url;
            if (height <= 0)
                template = WithoutHeight(template);
            string origin = "", path = url;
            if ((template.Contains("{origin}") || template.Contains("{path}")) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                origin = uri.GetLeftPart(UriPartial.Authority);
                path = uri.PathAndQuery;
            }
            return template
                .Replace("{url}", url)
                .Replace("{origin}", origin)
                .Replace("{path}", path)
                .Replace("{width}", width.ToString(CultureInfo.InvariantCulture))
                .Replace("{height}", height.ToString(CultureInfo.InvariantCulture));
        }

        // The template without each part naming {height}: what lies between the separators either side of it, and one of
        // them. The one before it stays where it starts the query or a path segment (? or /), or where both are alike
        // (&...&, ,...,); otherwise it goes, and the one after it stays (,h_{height}/ in a path).
        private static string WithoutHeight(string template)
        {
            int at;
            while ((at = template.IndexOf("{height}", StringComparison.Ordinal)) >= 0)
            {
                int start = at > 0 ? template.LastIndexOfAny(Separators, at - 1) : -1;
                int end = template.IndexOfAny(Separators, at);
                if (end < 0)
                    template = start >= 0 ? template.Remove(start) : "";
                else if (start < 0 || template[start] == '?' || template[start] == '/' || template[start] == template[end])
                    template = template.Remove(start + 1, end - start);
                else
                    template = template.Remove(start, end - start);
            }
            return template;
        }

        // Edits reach every image asking with it.
        protected virtual void OnValidate() => UISettings.NotifyChanged();
    }
}
