using System;
using System.Collections.Generic;
using TimboJimbo.UINext.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text drawn in its node, inside the node's padding: read with its <see cref="Markup"/> (else the project's default,
    /// else the built-in Markdown: **bold**, *italic*, __underline__, ~~strike~~, `code`, fenced code blocks,
    /// [links](url), headings, quotes and lists), wrapped to the node's width, cut short with an ellipsis at its height or
    /// <see cref="MaxLines"/>, shaped by the text engine (any script, emoji, right to left) and drawn as signed distance
    /// field glyphs, crisp at any scale. Its node fits to it when nothing else sizes it. A change of colour draws it again
    /// without shaping it again.
    /// <para>
    /// Its markup's handlers say how what they find is shown. A <see cref="SpanHandler"/>'s stays text in a style of its
    /// own, with a node made from a prefab behind each line of it (the background behind code). An
    /// <see cref="ElementHandler"/>'s becomes a node made from a prefab, sitting in its line as a big character would and
    /// as big as its own layout makes it (a chip, an image), and a <see cref="BlockHandler"/>'s a node across the width
    /// between the lines above and below it (a code block, a quote, a list item). Those nodes are its node's children,
    /// sized and placed as its text is laid out, in the same pass, and its glyphs are drawn over them. Their components
    /// implementing <see cref="ITextContent"/> are told what they stand for (a <see cref="TextSlot"/> shows it). A tap on a
    /// tappable span or element raises <see cref="MatchTapped"/>, or on a link, <see cref="LinkClicked"/>, taking the tap
    /// from a button the label is in, and what shows it is Pressed while it is held.
    /// </para>
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next/Label")]
    public sealed class Label : UIDrawable, IMeasurable, IContentHost, IPointerHandler
    {
        // Inside more inline content than this, a label shows its text as written: a handler whose prefab shows its match
        // in a way that matches again would otherwise nest without end.
        private const int MaxDepth = 8;

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
        [SerializeField] private UIValue<TextOverflow> _overflow = TextOverflow.Ellipsis;

        [Tooltip("Reads the text with its Markup: Markdown unless it has another. Off, the text is shown as written.")]
        [FormerlySerializedAs("_markdown")]
        [SerializeField] private UIValue<bool> _richText = true;

        [Tooltip("The handlers its text is read with. Empty for the project's default markup, else the built-in Markdown.")]
        [SerializeField] private UIValue<Markup> _markup = new UIValue<Markup>();

        [SerializeField] private UIValue<float> _characterSpacing = 0f;

        [Tooltip("An outline round every glyph, in points: game UI's stroked text, as CSS's text-stroke. 0 for none.")]
        [SerializeField] private UIValue<float> _outlineWidth = 0f;
        [SerializeField] private UIValue<Color> _outlineColor = UnityEngine.Color.black;

        [Tooltip("A hard shadow drawn under the text (and its outline), as CSS's text-shadow without blur. Clear for none.")]
        [SerializeField] private UIValue<Color> _shadowColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private UIValue<Vector2> _shadowOffset = new Vector2(0f, 2f);

        [Tooltip("Raised with a link's target when a link in the text is tapped.")]
        [SerializeField] private UnityEvent<string> _onLinkClicked = new();

        [Tooltip("Raised with a tappable span's or element's text when it is tapped (a mention, a #tag).")]
        [SerializeField] private UnityEvent<string> _onMatchTapped = new();

        // The text as read, and what it was read from: the text, whether rich, the rules, and the settings' version.
        private readonly TextDocument _document = new();
        private string _readText;
        private bool _readRich;
        private TextRuleSet _readMarkup;
        private int _readSettings = -1;
        // Counts readings, so what was laid out from an older one is laid out again.
        private int _version;
        private Effect _reader;
        private int _depth;

        // Each element's and block's size as layout last gave it, by match, and the same with each block as narrow as it
        // can be (what the narrowest the label can be is measured with), each counting its changes.
        private readonly List<Vector2> _sizes = new();
        private readonly List<Vector2> _narrowest = new();
        private int _sizesVersion;
        private int _narrowestVersion;

        // How many lines each span with a background is on, by match, and the elements and blocks the text was cut short
        // before, in the last final layout: their nodes, and the backgrounds' as many, are made as the text is read.
        private readonly List<int> _pieces = new();
        private readonly HashSet<int> _cut = new();

        // The layout in its content box (at a width alone while widths are shared out), what it was laid out from, and
        // whether it drew anything. What it draws, places its inline content by and finds taps on.
        private TextLayout _layout;
        private TextLayoutInput _laidOut;
        private int _laidOutVersion;
        private int _laidOutSizes;
        private bool _valid;
        private bool _drawn;
        private int _stamp;
        private int _watched = -1;
        private Vector2 _origin;

        // A span's line pieces in the layout last asked for them.
        private readonly List<Rect> _rects = new();
        private int _rectsMatch = -1;
        private int _rectsStamp = -1;

        // Measures, kept until what they were measured from changes: unwrapped, at the last width asked for, and narrowest.
        private TextLayoutInput _measuredFor;
        private int _measuredVersion = -1;
        private int _measuredSizes = -1;
        private Vector2 _unwrapped = new(-1f, -1f);
        private float _measuredWidth = -1f;
        private Vector2 _measured;
        private TextLayoutInput _narrowestFor;
        private int _narrowestForVersion = -1;
        private int _narrowestForSizes = -1;
        private float _minWidth = -1f;

        // Its inline content: a node for each element and block, and one for each line of a span with a background, in
        // text order; and spares, hidden, kept to be used again as a list keeps its rows, rather than destroyed.
        private enum InlineKind : byte { Element, Block, Background }

        private struct Inline
        {
            public GameObject Prefab;
            public UINode Node;
            public InlineKind Kind;
            public int Match;
            public int Piece;
            // The match it was last told it stands for, and in which reading.
            public int BoundMatch;
            public int BoundVersion;
            // Whether it gives itself and what is in it interaction states, as a button does: it stands for a tappable
            // match, and is Pressed while that is held.
            public bool Provides;
        }

        private readonly List<Inline> _inlines = new();
        private readonly Dictionary<UINode, int> _inlineIndex = new();
        private readonly List<(GameObject Prefab, UINode Node)> _spares = new();

        // A tappable span or element pressed, and the pointer pressing it.
        private int _pointer = -1;
        private int _pressed = -1;

        private static readonly HashSet<Label> s_enabled = new();
        private static readonly List<TextRule> s_rules = new();
        private static readonly List<ITextContent> s_contents = new();
        private static readonly List<int> s_around = new();
        private static bool s_listening;
        private static int s_settings;

        /// <summary>What it says, read with its markup unless <see cref="RichText"/> is off.</summary>
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
        public UIValue<TextOverflow> Overflow => _overflow;

        /// <summary>Whether its text is read with its markup; off, it is shown as written.</summary>
        public UIValue<bool> RichText => _richText;

        /// <summary>
        /// The handlers its text is read with: none for the project's default (Project Settings > Timbo Jimbo > UI Next Text),
        /// else the built-in Markdown.
        /// </summary>
        public UIValue<Markup> Markup => _markup;

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

        /// <summary>Raised with a link's target when a link in the text is tapped.</summary>
        public event Action<string> LinkClicked;

        /// <summary>Raised with a link's target when a link in the text is tapped (wired in the inspector).</summary>
        public UnityEvent<string> OnLinkClicked => _onLinkClicked;

        /// <summary>Raised with the match when a tappable span or element other than a link is tapped (a mention, a #tag).</summary>
        public event Action<TextMatch> MatchTapped;

        /// <summary>Raised with a tappable span's or element's text when it is tapped, a link aside (wired in the inspector).</summary>
        public UnityEvent<string> OnMatchTapped => _onMatchTapped;

        /// <summary>Its text as last read: what its markup's handlers found in it.</summary>
        public TextDocument Document => _document;

        /// <summary>The layout it last drew, for questions about where its characters are (null before it draws).</summary>
        public TextLayout Layout => _valid && _drawn ? _layout : null;

        /// <summary>The rules its text is read with: its own markup, else the project's default, else the built-in Markdown.</summary>
        public TextRuleSet EffectiveMarkup => MarkupFor(_markup.Peek);

        /// <summary>
        /// The size the text around a match is drawn at, in points: its own, scaled by every span the match is in (a
        /// heading's). An element sized in em is sized against it; read in a binding, it follows the label's size.
        /// </summary>
        public float FontSizeAround(in TextMatch match)
        {
            var style = TextStyle.Plain;
            var matches = _document.Matches;
            s_around.Clear();
            for (int p = match.Parent; p >= 0 && p < matches.Count; p = matches[p].Parent)
                s_around.Add(p);
            for (int i = s_around.Count - 1; i >= 0; i--)
            {
                var span = matches[s_around[i]];
                if (span.Role == TextRole.Span && span.Rule != null)
                    span.Rule.Style(in span, ref style);
            }
            s_around.Clear();
            return Mathf.Max(1f, _fontSize.Value) * (style.Scale > 0f ? style.Scale : 1f);
        }

        // ── Measuring ────────────────────────────────────────────────────────────

        public Vector2 Measure(float availableWidth)
        {
            var input = Input(availableWidth, -1f, _sizes);
            if (!SameShape(in input, in _measuredFor, ignoreBox: true) || _measuredVersion != _version || _measuredSizes != _sizesVersion)
            {
                _measuredFor = input;
                _measuredVersion = _version;
                _measuredSizes = _sizesVersion;
                _unwrapped = new Vector2(-1f, -1f);
                _measuredWidth = -1f;
            }
            if (availableWidth < 0f || !input.WordWrap)
            {
                if (_unwrapped.x < 0f)
                {
                    var unwrapped = input;
                    unwrapped.WidthPx = -1f;
                    _unwrapped = TextLayout.Measure(in unwrapped);
                }
                return _unwrapped;
            }
            if (!Mathf.Approximately(_measuredWidth, availableWidth))
            {
                _measuredWidth = availableWidth;
                _measured = TextLayout.Measure(in input);
            }
            return _measured;
        }

        public float MinWidth
        {
            get
            {
                var input = Input(-1f, -1f, _narrowest);
                if (!SameShape(in input, in _narrowestFor, ignoreBox: true) || _narrowestForVersion != _version || _narrowestForSizes != _narrowestVersion)
                {
                    _narrowestFor = input;
                    _narrowestForVersion = _version;
                    _narrowestForSizes = _narrowestVersion;
                    _minWidth = -1f;
                }
                if (_minWidth < 0f)
                    _minWidth = input.WordWrap ? TextLayout.LongestWord(in input) : Measure(-1f).x;
                return _minWidth;
            }
        }

        // Forgets the measures and the layout, for what they were made from that they do not see change.
        private void Forget()
        {
            _measuredVersion = _narrowestForVersion = -1;
            _valid = false;
        }

        // ── Hosting its inline content ───────────────────────────────────────────

        Vector2 IContentHost.Measure(float availableWidth, in HostedChildren children)
        {
            TakeSizes(in children);
            return Measure(availableWidth);
        }

        float IContentHost.Narrowest(in HostedChildren children)
        {
            TakeSizes(in children);
            return MinWidth;
        }

        // A block is as wide as the text; a background as big as its piece of its span's line, reaching above and below it.
        // An element sizes itself.
        bool IContentHost.SizeChild(UINode child, int axis, Vector2 contentSize, in HostedChildren children, out float size)
        {
            size = 0f;
            if (!_inlineIndex.TryGetValue(child, out int index)) return false;
            var inline = _inlines[index];
            switch (inline.Kind)
            {
                case InlineKind.Block when axis == 0:
                    size = contentSize.x;
                    return true;
                case InlineKind.Background:
                    TakeSizes(in children);
                    // Widths are shared out before heights are known: the lines are broken by widths alone.
                    LayOut(axis == 0 ? new Vector2(contentSize.x, -1f) : contentSize, final: axis == 1);
                    var rect = PieceRect(in inline);
                    size = axis == 0 ? rect.width : rect.height;
                    return true;
                default:
                    return false;
            }
        }

        // Where the text put it: an element in its line, a block on its line, a background on its piece of its span's line.
        bool IContentHost.PlaceChild(UINode child, Vector2 contentSize, in HostedChildren children, out Vector2 topLeft)
        {
            topLeft = default;
            if (!_inlineIndex.TryGetValue(child, out int index)) return false;
            TakeSizes(in children);
            LayOut(contentSize, final: true);
            var inline = _inlines[index];
            var origin = new Vector2(0f, _layout.TopPx);
            if (inline.Kind == InlineKind.Background)
                topLeft = origin + PieceRect(in inline).position;
            else if (_layout.TryGetObjectRect(inline.Match, out var rect))
                topLeft = origin + rect.position;
            else
                // Cut off with the text after it, until it is hidden as its text is read again: below the box.
                topLeft = new Vector2(0f, contentSize.y);
            return true;
        }

        // Takes up the sizes layout has given its elements and blocks so far, and how narrow each block can be.
        private void TakeSizes(in HostedChildren children)
        {
            bool changed = false, narrower = false;
            foreach (var inline in _inlines)
            {
                if (inline.Kind == InlineKind.Background || inline.Node == null || inline.Match >= _sizes.Count) continue;
                if (!children.TryGetSize(inline.Node, out var size, out var min)) continue;
                if (_sizes[inline.Match] != size)
                {
                    _sizes[inline.Match] = size;
                    changed = true;
                }
                var narrowest = inline.Kind == InlineKind.Block ? new Vector2(Mathf.Min(min.x, size.x), size.y) : size;
                if (_narrowest[inline.Match] != narrowest)
                {
                    _narrowest[inline.Match] = narrowest;
                    narrower = true;
                }
            }
            if (changed) _sizesVersion++;
            if (narrower) _narrowestVersion++;
        }

        // A background's rect in the layout: its piece of its span's line, reaching above and below it as its handler says;
        // one past the lines there are, nothing at the end of the last.
        private Rect PieceRect(in Inline inline)
        {
            if (_rectsMatch != inline.Match || _rectsStamp != _stamp)
            {
                _rectsMatch = inline.Match;
                _rectsStamp = _stamp;
                _layout.GetMatchRects(inline.Match, _rects);
            }
            if (_rects.Count == 0) return default;
            if (inline.Piece >= _rects.Count)
            {
                var last = _rects[_rects.Count - 1];
                return new Rect(last.xMax, last.y, 0f, 0f);
            }
            var rect = _rects[inline.Piece];
            var handler = _document.Matches[inline.Match].Rule as SpanHandler;
            float reach = handler != null ? handler.BackgroundReachEm * Mathf.Max(1f, _fontSize.Value) : 0f;
            return new Rect(rect.x, rect.y - reach, rect.width, rect.height + 2f * reach);
        }

        // ── Laying out and drawing ───────────────────────────────────────────────

        // Lays its text out in a content box, its elements and blocks at the sizes layout gave them, unless it is laid out
        // so already. A height that cannot change the layout is left out, so laid out at a width alone and then in the
        // whole box, it is laid out once. The final layout, in the box it is drawn in, is watched (Watch).
        private void LayOut(Vector2 box, bool final)
        {
            var input = Input(box.x, HeightMatters ? box.y : -1f, _sizes);
            if (!_valid || !SameShape(in input, in _laidOut, ignoreBox: false) || _laidOutVersion != _version || _laidOutSizes != _sizesVersion)
            {
                _layout ??= new TextLayout();
                _drawn = _layout.Generate(in input);
                _valid = true;
                _laidOut = input;
                _laidOutVersion = _version;
                _laidOutSizes = _sizesVersion;
                _stamp++;
            }
            if (final)
                Watch();
        }

        // Whether the height of its box can change its layout: when it cuts the text short, or aligns it lower than its top.
        private bool HeightMatters => _overflow.Value != TextOverflow.Overflow || _maxLines.Value > 0 || (int)_alignment.Value / 3 != 0;

        // What the final layout says of its inline content: how many lines each span with a background is on, and which
        // elements and blocks the text was cut short before. When that changes, the text's inline content is made again,
        // as values resolve, so this frame lays it out and draws it.
        private void Watch()
        {
            if (_watched == _stamp) return;
            _watched = _stamp;
            bool cut = _drawn && _layout.IsElided;
            bool changed = false;
            foreach (var inline in _inlines)
            {
                if (inline.Match >= _pieces.Count) continue;
                if (inline.Kind == InlineKind.Background)
                {
                    if (inline.Piece != 0) continue;
                    _layout.GetMatchRects(inline.Match, _rects);
                    _rectsMatch = inline.Match;
                    _rectsStamp = _stamp;
                    int pieces = Mathf.Max(1, _rects.Count);
                    if (_pieces[inline.Match] == pieces) continue;
                    _pieces[inline.Match] = pieces;
                    changed = true;
                    continue;
                }
                bool isCut = cut && !_layout.TryGetObjectRect(inline.Match, out _);
                if (isCut == _cut.Contains(inline.Match)) continue;
                if (isCut) _cut.Add(inline.Match);
                else _cut.Remove(inline.Match);
                changed = true;
            }
            if (changed)
                _reader?.Invalidate();
        }

        protected internal override void Draw(Painter painter)
        {
            var box = painter.ContentRect;
            if (string.IsNullOrEmpty(_text.Value) || box.width <= 0f) return;
            LayOut(box.size, final: true);
            if (!_drawn) return;
            _origin = new Vector2(box.x, box.y + _layout.TopPx);

            // Over its inline content: the backgrounds behind its spans are its children, drawn before its overlay.
            var over = painter.Overlay;
            var tint = _color.Value;
            float outline = Mathf.Max(0f, _outlineWidth.Value);
            var shadow = _shadowColor.Value;
            if (shadow.a > 0f)
                over.Text(_layout, _origin + _shadowOffset.Value, shadow, outline, shadow, silhouette: true);
            over.Text(_layout, _origin, tint, outline, _outlineColor.Value);
        }

        private TextLayoutInput Input(float width, float height, List<Vector2> sizes)
        {
            Read(_text.Value ?? "", _richText.Value, _markup.Value);
            var input = TextLayoutInput.Default;
            input.Text = _readText;
            input.Markup = _readMarkup;
            input.Document = _readMarkup != null ? _document : null;
            input.ObjectSizes = sizes;
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

        // Whether two inputs shape the same text: everything but colour (and the sizes, counted apart), and the box when
        // asked to ignore it.
        private static bool SameShape(in TextLayoutInput a, in TextLayoutInput b, bool ignoreBox) =>
            a.Text == b.Text && a.Markup == b.Markup && a.Document == b.Document && a.Font == b.Font && a.FontSizePx == b.FontSizePx
            && a.WordWrap == b.WordWrap && a.Alignment == b.Alignment && a.Overflow == b.Overflow && a.MaxLines == b.MaxLines
            && a.Bold == b.Bold && a.Italic == b.Italic && a.CharacterSpacingPx == b.CharacterSpacingPx
            && a.AntiAliasMarginPx == b.AntiAliasMarginPx && (ignoreBox || (a.WidthPx == b.WidthPx && a.HeightPx == b.HeightPx));

        // ── Reading ──────────────────────────────────────────────────────────────

        // The rules a markup stands for: itself, else the project's default, else the built-in Markdown.
        private static TextRuleSet MarkupFor(Markup markup)
        {
            if (markup != null) return markup;
            var settings = TextSettings.Instance;
            if (settings != null && settings.DefaultMarkup != null) return settings.DefaultMarkup;
            return BuiltInText.All;
        }

        // Its reading, run as values resolve, before its tree is laid out: reads its text again when it, or what it is read
        // with, changes, and makes the inline content for what it found.
        private void ReadText()
        {
            string text = _text.Value ?? "";
            bool rich = _richText.Value;
            var markup = _markup.Value;
            Computation.Untracked(() =>
            {
                Read(text, rich, markup);
                Reconcile();
            });
        }

        // Reads the text again, if it or what it is read with has changed since it was last read.
        private void Read(string text, bool rich, Markup markup)
        {
            var rules = rich && _depth <= MaxDepth ? MarkupFor(markup) : null;
            if (_readSettings == s_settings && rich == _readRich && rules == _readMarkup && string.Equals(text, _readText)) return;
            _readText = text;
            _readRich = rich;
            _readMarkup = rules;
            _readSettings = s_settings;
            if (rules != null)
            {
                s_rules.Clear();
                rules.GetRules(s_rules);
                _document.Parse(text, s_rules);
                s_rules.Clear();
            }
            else
            {
                _document.SetPlain(text);
            }
            _version++;
            int count = _document.Matches.Count;
            Fill(_sizes, count, Vector2.zero);
            Fill(_narrowest, count, Vector2.zero);
            Fill(_pieces, count, 1);
            _sizesVersion++;
            _narrowestVersion++;
            _cut.Clear();
        }

        private static void Fill<T>(List<T> list, int count, T value)
        {
            list.Clear();
            for (int i = 0; i < count; i++)
                list.Add(value);
        }

        // One node for each element and block, and for each line of a span with a background, in text order: kept where its
        // prefab is the same, taken from the spares or made where it is not, and those left over hidden as spares. Each is
        // told what it stands for when that changes; elements and blocks the text was cut short before are hidden.
        private void Reconcile()
        {
            int used = 0;
            var matches = _document.Matches;
            for (int m = 0; m < matches.Count; m++)
            {
                var match = matches[m];
                switch (match.Rule)
                {
                    case ElementHandler element:
                        Use(ref used, element.PrefabFor(in match), InlineKind.Element, m, 0);
                        break;
                    case BlockHandler block:
                        Use(ref used, block.PrefabFor(in match), InlineKind.Block, m, 0);
                        break;
                    case SpanHandler span:
                        var background = span.BackgroundFor(in match);
                        if (background == null) break;
                        for (int piece = 0; piece < _pieces[m]; piece++)
                            Use(ref used, background, InlineKind.Background, m, piece);
                        break;
                }
            }
            for (int i = _inlines.Count - 1; i >= used; i--)
            {
                Spare(_inlines[i]);
                _inlines.RemoveAt(i);
            }
            _inlineIndex.Clear();
            for (int i = 0; i < _inlines.Count; i++)
                _inlineIndex[_inlines[i].Node] = i;
        }

        private void Use(ref int used, GameObject prefab, InlineKind kind, int match, int piece)
        {
            if (prefab == null) return;
            if (used < _inlines.Count && (_inlines[used].Prefab != prefab || _inlines[used].Node == null))
            {
                Spare(_inlines[used]);
                _inlines.RemoveAt(used);
            }
            if (used >= _inlines.Count || _inlines[used].Prefab != prefab)
            {
                var made = Take(prefab);
                if (made == null) return;
                _inlines.Insert(used, new Inline { Prefab = prefab, Node = made, BoundMatch = -1 });
            }
            var inline = _inlines[used];
            inline.Kind = kind;
            inline.Match = match;
            inline.Piece = piece;
            var node = inline.Node;
            node.Display.Value = kind != InlineKind.Background && _cut.Contains(match) ? DisplayMode.None : DisplayMode.Visible;
            node.Floating.Value = new Floating { AttachTo = FloatingAttach.Parent };
            var rule = _document.Matches[match].Rule;
            bool tappable = rule != null && rule.Tappable;
            if (tappable != inline.Provides)
            {
                if (tappable)
                    node.AddInteractionProvider();
                else
                    node.RemoveInteractionProvider();
                inline.Provides = tappable;
            }
            if (inline.BoundMatch != match || inline.BoundVersion != _version)
            {
                inline.BoundMatch = match;
                inline.BoundVersion = _version;
                Bind(node, _document.Matches[match]);
            }
            _inlines[used++] = inline;
        }

        // Tells the components of a node what it stands for, but for those inside another label's inline content in it.
        private void Bind(UINode node, in TextMatch match)
        {
            s_contents.Clear();
            node.GetComponentsInChildren(true, s_contents);
            foreach (var content in s_contents)
            {
                if (content is Component component && InlineRootOf(component.transform) == node.transform)
                    content.Bind(in match, this);
            }
            s_contents.Clear();
        }

        // The root of the inline content a transform is in: the nearest object at or above it a label made.
        private static Transform InlineRootOf(Transform transform)
        {
            for (var t = transform; t != null; t = t.parent)
                if (t.TryGetComponent(out InlineOf _))
                    return t;
            return null;
        }

        // A node for a handler's prefab: a spare made from it, or one made now, inside the label's own node and never
        // saved with it.
        private UINode Take(GameObject prefab)
        {
            for (int i = _spares.Count - 1; i >= 0; i--)
            {
                var (made, spare) = _spares[i];
                if (spare == null)
                {
                    _spares.RemoveAt(i);
                    continue;
                }
                if (made != prefab) continue;
                _spares.RemoveAt(i);
                return spare;
            }
            if (!prefab.TryGetComponent(out UINode _))
            {
                Debug.LogWarning($"UI Next: {prefab.name}, a text handler's prefab, has no UINode on its root, so {name} cannot place it.", this);
                return null;
            }
            var instance = Instantiate(prefab, transform, false);
            instance.name = prefab.name;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.DontSave;
            instance.AddComponent<InlineOf>().Label = this;
            var node = instance.GetComponent<UINode>();
            // Where the text puts it at once, as its glyphs are, rather than springing there inside a change.
            node.LandsAtOnce = true;
            // Prefabs made in code are kept inactive, out of every tree.
            if (!instance.activeSelf)
                instance.SetActive(true);
            return node;
        }

        // An inline node no longer wanted: hidden and kept to be used again, standing for nothing tappable.
        private void Spare(in Inline inline)
        {
            if (inline.Node == null) return;
            if (inline.Provides)
            {
                inline.Node.SetInteraction(InteractionStates.Pressed, false);
                inline.Node.RemoveInteractionProvider();
            }
            inline.Node.Display.Value = DisplayMode.None;
            _spares.Add((inline.Prefab, inline.Node));
        }

#if UNITY_EDITOR
        // Inline nodes among its children it does not know of (left by a script reload) are destroyed.
        private void Sweep()
        {
            if (this == null) return;
            foreach (var inline in GetComponentsInChildren<InlineOf>(true))
            {
                if (inline.transform.parent != transform) continue;
                var node = inline.GetComponent<UINode>();
                if (_inlines.Exists(i => i.Node == node) || _spares.Exists(i => i.Node == node)) continue;
                DestroyImmediate(inline.gameObject);
            }
        }
#endif

        // How deep in inline content it is: how many labels' inline content it is inside.
        private int DepthOf()
        {
            int depth = 0;
            for (var t = transform.parent; t != null; t = t.parent)
                if (t.TryGetComponent(out InlineOf _))
                    depth++;
            return depth;
        }

        // ── Lifetime ─────────────────────────────────────────────────────────────

        protected override void OnEnable()
        {
            base.OnEnable();
            s_enabled.Add(this);
            _depth = DepthOf();
            _reader = new Effect(ReadText);
            _reader.Invalidate();
            UI.RequestRepaint();
#if UNITY_EDITOR
            // Inline content outlives a script reload as children never saved, while this label forgets it: what it no
            // longer knows of goes, once the editor is out of the frame.
            UnityEditor.EditorApplication.delayCall += Sweep;
#endif
            if (!s_listening)
            {
                s_listening = true;
                TextSettings.Changed += ReadAll;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            s_enabled.Remove(this);
            _reader?.Dispose();
            _reader = null;
            _layout?.Dispose();
            _layout = null;
            _valid = _drawn = false;
            _readSettings = -1;
            _pointer = -1;
            Press(-1);
            // Its inline content goes out of sight with it: as children when its object goes, hidden when the label alone does.
            if (gameObject.activeInHierarchy)
                foreach (var inline in _inlines)
                    if (inline.Node != null)
                        inline.Node.Display.Value = DisplayMode.None;
        }

        private void OnTransformParentChanged()
        {
            int depth = DepthOf();
            if (depth == _depth) return;
            _depth = depth;
            _reader?.Invalidate();
        }

        // A font stack, a markup, a rule or the text settings changed: every label reads its text again and lays it out.
        private static void ReadAll()
        {
            s_settings++;
            foreach (var label in s_enabled)
            {
                label.Forget();
                label.ContentChanged();
                label._reader?.Invalidate();
            }
            UI.RequestRepaint();
        }

        // ── Taps ─────────────────────────────────────────────────────────────────

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer >= 0 || !_valid) return;
            int match = TappableAt(e.Position);
            if (match < 0 || !UIInput.TakeTap(this, e.PointerId)) return;
            _pointer = e.PointerId;
            Press(match);
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e) { }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            int pressed = _pressed;
            Press(-1);
            if (pressed < 0 || pressed >= _document.Matches.Count || TappableAt(e.Position) != pressed) return;
            var match = _document.Matches[pressed];
            switch (match.Rule)
            {
                case SpanHandler span:
                    span.Tapped(in match, this);
                    break;
                case ElementHandler element:
                    element.Tapped(in match, this);
                    break;
                default:
                    RaiseTapped(in match);
                    break;
            }
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            Press(-1);
        }

        // The tappable match under a point in its root's points: an element whose node it is over, else the span whose
        // text it is over; -1 for none.
        private int TappableAt(Vector2 point)
        {
            var local = Node.RootToLocal(point);
            foreach (var inline in _inlines)
            {
                if (inline.Kind != InlineKind.Element || !inline.Provides || inline.Node == null) continue;
                if (inline.Node.Display.Peek == DisplayMode.Visible && inline.Node.LaidOutRect.Contains(local))
                    return inline.Match;
            }
            return _drawn ? _layout.MatchAt(local - _origin) : -1;
        }

        // Shows a match held down, Pressed on the nodes standing for it (a chip, the backgrounds behind a span), and the
        // one held before not.
        private void Press(int match)
        {
            if (match == _pressed) return;
            foreach (var inline in _inlines)
            {
                if (!inline.Provides || inline.Node == null) continue;
                if (inline.Match == _pressed || inline.Match == match)
                    inline.Node.SetInteraction(InteractionStates.Pressed, inline.Match == match);
            }
            _pressed = match;
        }

        /// <summary>
        /// Raises what a tap on a span or an element raises: <see cref="LinkClicked"/> with a link's target (a span with a
        /// Link trigger), <see cref="MatchTapped"/> for the rest.
        /// </summary>
        public void RaiseTapped(in TextMatch match)
        {
            if (match.Rule != null && match.Rule.Trigger.Kind == TextTriggerKind.Link)
            {
                var target = match.Argument ?? match.Text;
                LinkClicked?.Invoke(target);
                _onLinkClicked.Invoke(target);
                return;
            }
            MatchTapped?.Invoke(match);
            _onMatchTapped.Invoke(match.Text);
        }
    }
}
