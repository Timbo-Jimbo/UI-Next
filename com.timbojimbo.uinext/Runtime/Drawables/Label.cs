using TimboJimbo.UI.Text;
using TimboJimbo.UI.Text.Markup;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text drawn in its node, inside the node's padding: Markdown (<c>**bold**</c>, <c>*italic*</c>, links, code),
    /// wrapped to the node's width, cut short with an ellipsis at its height or <see cref="MaxLines"/>, shaped by the
    /// text engine (any script, emoji, right to left) and drawn as signed distance field glyphs, crisp at any scale. Its
    /// node fits to it when nothing else sizes it. A change of colour draws it again without shaping it again.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next/Label")]
    public sealed class Label : UIDrawable, IMeasurable
    {
        [SerializeField] private UIValue<string> _text = "";
        [SerializeField] private UIValue<FontStack> _font = new UIValue<FontStack>();
        [SerializeField] private UIValue<float> _fontSize = 17f;
        [SerializeField] private UIValue<Color> _color = new Color(0.1f, 0.1f, 0.12f, 1f);
        [SerializeField] private UIValue<TextAnchor> _alignment = TextAnchor.UpperLeft;
        [SerializeField] private UIValue<bool> _bold = false;
        [SerializeField] private UIValue<bool> _italic = false;

        [Tooltip("Wraps to the node's width. Off, it runs on in one line.")]
        [SerializeField] private UIValue<bool> _wrap = true;

        [Tooltip("At most this many lines, the last cut short with an ellipsis; 0 for any number.")]
        [SerializeField] private UIValue<int> _maxLines = 0;

        [Tooltip("What happens to text that runs past the node's height or Max Lines.")]
        [SerializeField] private UIValue<TextBlockOverflow> _overflow = TextBlockOverflow.Ellipsis;

        [Tooltip("Reads the text as Markdown: **bold**, *italic*, [links](url), `code`.")]
        [SerializeField] private UIValue<bool> _markdown = true;

        [SerializeField] private UIValue<float> _characterSpacing = 0f;

        [Tooltip("An outline round every glyph, in points: game UI's stroked text, as CSS's text-stroke. 0 for none.")]
        [SerializeField] private UIValue<float> _outlineWidth = 0f;
        [SerializeField] private UIValue<Color> _outlineColor = UnityEngine.Color.black;

        [Tooltip("A hard shadow drawn under the text (and its outline), as CSS's text-shadow without blur. Clear for none.")]
        [SerializeField] private UIValue<Color> _shadowColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private UIValue<Vector2> _shadowOffset = new Vector2(0f, 2f);

        // The layout drawn from, and what it was laid out with: a change of colour or opacity reuses it.
        private TextLayout _layout;
        private TextLayoutInput _laidOut;
        private bool _hasLaidOut;

        // Measures, kept until what they were measured from changes: unwrapped, and at the last width asked for.
        private TextLayoutInput _measuredFor;
        private Vector2 _unwrapped = new(-1f, -1f);
        private float _minWidth = -1f;
        private float _measuredWidth = -1f;
        private Vector2 _measured;

        /// <summary>What it says: Markdown unless <see cref="Markdown"/> is off.</summary>
        public UIValue<string> Text => _text;

        /// <summary>The fonts it draws with; none for the project's default stack.</summary>
        public UIValue<FontStack> Font => _font;

        /// <summary>Its size in points.</summary>
        public UIValue<float> FontSize => _fontSize;

        public UIValue<Color> Color => _color;
        public UIValue<TextAnchor> Alignment => _alignment;
        public UIValue<bool> Bold => _bold;
        public UIValue<bool> Italic => _italic;
        public UIValue<bool> Wrap => _wrap;
        public UIValue<int> MaxLines => _maxLines;
        public UIValue<TextBlockOverflow> Overflow => _overflow;
        public UIValue<bool> Markdown => _markdown;
        public UIValue<float> CharacterSpacing => _characterSpacing;

        /// <summary>
        /// An outline round every glyph, in points, in <see cref="OutlineColor"/>: the stroked text of game UI, as CSS's
        /// text-stroke and TextMeshPro's outline. It lies outside the glyph, so the text keeps its weight, and takes no
        /// room in layout. As wide as the font's atlas padding allows (about a tenth of its size). Colour emoji have none.
        /// </summary>
        public UIValue<float> OutlineWidth => _outlineWidth;

        public UIValue<Color> OutlineColor => _outlineColor;

        /// <summary>
        /// A hard shadow under the text, its outline included, in this colour (clear for none), moved by
        /// <see cref="ShadowOffset"/>: as CSS's text-shadow with no blur.
        /// </summary>
        public UIValue<Color> ShadowColor => _shadowColor;

        public UIValue<Vector2> ShadowOffset => _shadowOffset;

        /// <summary>The layout it last drew, for questions about where its characters are (null before it draws).</summary>
        public TextLayout Layout => _hasLaidOut ? _layout : null;

        // ── Measuring ────────────────────────────────────────────────────────────

        public Vector2 Measure(float availableWidth)
        {
            var input = Input(availableWidth, -1f);
            Remeasure(in input);
            if (availableWidth < 0f || !input.WordWrap)
            {
                if (_unwrapped.x < 0f)
                {
                    var unwrapped = input;
                    unwrapped.WidthPx = -1f;
                    _unwrapped = TextLayout.Measure(in unwrapped, Context(in input));
                }
                return _unwrapped;
            }
            if (!Mathf.Approximately(_measuredWidth, availableWidth))
            {
                _measuredWidth = availableWidth;
                _measured = TextLayout.Measure(in input, Context(in input));
            }
            return _measured;
        }

        public float MinWidth
        {
            get
            {
                var input = Input(-1f, -1f);
                Remeasure(in input);
                if (_minWidth < 0f)
                    _minWidth = input.WordWrap ? TextLayout.LongestWord(in input, Context(in input)) : Measure(-1f).x;
                return _minWidth;
            }
        }

        // Forgets the measures when what they were measured from has changed.
        private void Remeasure(in TextLayoutInput input)
        {
            if (SameShape(in input, in _measuredFor, ignoreBox: true)) return;
            _measuredFor = input;
            _unwrapped = new Vector2(-1f, -1f);
            _minWidth = -1f;
            _measuredWidth = -1f;
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        protected internal override void Draw(Painter painter)
        {
            var box = painter.ContentRect;
            var input = Input(box.width, box.height);
            if (string.IsNullOrEmpty(input.Text) || box.width <= 0f) return;

            _layout ??= new TextLayout();
            if (!_hasLaidOut || !SameShape(in input, in _laidOut, ignoreBox: false))
            {
                _hasLaidOut = _layout.Generate(in input, Context(in input));
                _laidOut = input;
            }
            if (!_hasLaidOut) return;
            var origin = new Vector2(box.x, box.y + _layout.TopPx);
            float outline = Mathf.Max(0f, _outlineWidth.Value);
            var shadow = _shadowColor.Value;
            if (shadow.a > 0f)
                painter.Text(_layout, origin + _shadowOffset.Value, shadow, outline, shadow, silhouette: true);
            painter.Text(_layout, origin, _color.Value, outline, _outlineColor.Value);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _layout?.Dispose();
            _layout = null;
            _hasLaidOut = false;
        }

        private TextLayoutInput Input(float width, float height)
        {
            var input = TextLayoutInput.Default;
            input.Text = _text.Value ?? "";
            input.RichText = _markdown.Value;
            input.Font = _font.Value;
            input.FontSizePx = Mathf.Max(1f, _fontSize.Value);
            input.WidthPx = width;
            input.HeightPx = height;
            input.WordWrap = _wrap.Value;
            input.Alignment = _alignment.Value;
            input.NaturalAlignment = true;
            input.Overflow = _overflow.Value;
            input.MaxLines = Mathf.Max(0, _maxLines.Value);
            input.Bold = _bold.Value;
            input.Italic = _italic.Value;
            input.CharacterSpacingPx = _characterSpacing.Value;
            // Room round every glyph for its edge's anti-aliasing, and for its outline.
            input.AntiAliasMarginPx = 1f + Mathf.Max(0f, _outlineWidth.Value);
            return input;
        }

        private static InlineContext Context(in TextLayoutInput input) => new(null, input.FontSizePx, UnityEngine.Color.white);

        // Whether two inputs shape the same text: everything but colour, and the box when asked to ignore it.
        private static bool SameShape(in TextLayoutInput a, in TextLayoutInput b, bool ignoreBox) =>
            a.Text == b.Text && a.RichText == b.RichText && a.Font == b.Font && a.FontSizePx == b.FontSizePx
            && a.WordWrap == b.WordWrap && a.Alignment == b.Alignment && a.Overflow == b.Overflow && a.MaxLines == b.MaxLines
            && a.Bold == b.Bold && a.Italic == b.Italic && a.CharacterSpacingPx == b.CharacterSpacingPx
            && a.AntiAliasMarginPx == b.AntiAliasMarginPx && (ignoreBox || (a.WidthPx == b.WidthPx && a.HeightPx == b.HeightPx));
    }
}
