using System;
using System.Collections.Generic;
using TimboJimbo.UINext.Text;
using TimboJimbo.UINext.Text.Markup;
using UnityEngine;
using UnityEngine.Events;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text drawn in its node, inside the node's padding: Markdown (<c>**bold**</c>, <c>*italic*</c>, <c>__underline__</c>,
    /// <c>~~strike~~</c>, links, code), wrapped to the node's width, cut short with an ellipsis at its height or
    /// <see cref="MaxLines"/>, shaped by the text engine (any script, emoji, right to left) and drawn as signed distance
    /// field glyphs, crisp at any scale. Its node fits to it when nothing else sizes it. A change of colour draws it again
    /// without shaping it again.
    /// <para>
    /// Underlines and strikethroughs are drawn under the glyphs, and code has a background behind it (the theme's
    /// <see cref="InlineBackground"/> handlers). A tap on a link raises <see cref="LinkClicked"/> with its href, taking the
    /// tap from a button the label is in. Inline content of the theme's handlers (an emote, a mention chip) is made from
    /// the handler's prefab, a UI Next node, as a node inside the label's own, placed over the token as the text is laid
    /// out; a prefab whose root has an <see cref="IInlineContent"/> is told what it stands for.
    /// </para>
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next/Label")]
    public sealed class Label : UIDrawable, IMeasurable, IPointerHandler
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
        [SerializeField] private UIValue<TextOverflow> _overflow = TextOverflow.Ellipsis;

        [Tooltip("Reads the text as Markdown: **bold**, *italic*, __underline__, ~~strike~~, [links](url), `code`.")]
        [SerializeField] private UIValue<bool> _markdown = true;

        [SerializeField] private UIValue<float> _characterSpacing = 0f;

        [Tooltip("An outline round every glyph, in points: game UI's stroked text, as CSS's text-stroke. 0 for none.")]
        [SerializeField] private UIValue<float> _outlineWidth = 0f;
        [SerializeField] private UIValue<Color> _outlineColor = UnityEngine.Color.black;

        [Tooltip("A hard shadow drawn under the text (and its outline), as CSS's text-shadow without blur. Clear for none.")]
        [SerializeField] private UIValue<Color> _shadowColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private UIValue<Vector2> _shadowOffset = new Vector2(0f, 2f);

        [Tooltip("Raised with a link's href when a link in the text is tapped.")]
        [SerializeField] private UnityEvent<string> _onLinkClicked = new();

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

        // What the layout wants placed, and where its top left was last drawn in the node's space.
        private readonly List<InlinePlacementRequest> _placements = new();
        private Vector2 _origin;

        // The nodes made from inline handlers' prefabs: one for each placement with a prefab, in order, and spares, hidden,
        // kept to be used again as a list keeps its rows, rather than destroyed as a frame draws.
        private readonly List<(GameObject Prefab, UINode Node)> _inlines = new();
        private readonly List<(GameObject Prefab, UINode Node)> _spares = new();
        private bool _inlinesQueued;
        private static readonly List<Label> s_queued = new();

        // A link pressed, and the pointer pressing it.
        private int _pointer = -1;
        private int _link = -1;

        private static readonly HashSet<Label> s_enabled = new();
        private static bool s_listening;

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
        public UIValue<TextOverflow> Overflow => _overflow;
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

        /// <summary>Raised with a link's href when a link in the text is tapped.</summary>
        public event Action<string> LinkClicked;

        /// <summary>Raised with a link's href when a link in the text is tapped (wired in the inspector).</summary>
        public UnityEvent<string> OnLinkClicked => _onLinkClicked;

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
            Forget();
            _measuredFor = input;
        }

        private void Forget()
        {
            _measuredFor = default;
            _unwrapped = new Vector2(-1f, -1f);
            _minWidth = -1f;
            _measuredWidth = -1f;
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        protected internal override void Draw(Painter painter)
        {
            var box = painter.ContentRect;
            var input = Input(box.width, box.height);
            if (string.IsNullOrEmpty(input.Text) || box.width <= 0f)
            {
                if (_placements.Count > 0)
                {
                    _placements.Clear();
                    QueueInlines();
                }
                return;
            }

            _layout ??= new TextLayout();
            if (!_hasLaidOut || !SameShape(in input, in _laidOut, ignoreBox: false))
            {
                _hasLaidOut = _layout.Generate(in input, Context(in input));
                _laidOut = input;
                _placements.Clear();
                if (_hasLaidOut)
                    _layout.CollectPlacements(Context(in input), _placements);
                QueueInlines();
            }
            if (!_hasLaidOut) return;
            var origin = new Vector2(box.x, box.y + _layout.TopPx);
            if (origin != _origin)
            {
                _origin = origin;
                QueueInlines();
            }

            var tint = _color.Value;
            DrawBackgrounds(painter, origin, input.FontSizePx);
            float outline = Mathf.Max(0f, _outlineWidth.Value);
            var shadow = _shadowColor.Value;
            if (shadow.a > 0f)
                painter.Text(_layout, origin + _shadowOffset.Value, shadow, outline, shadow, silhouette: true);
            foreach (var decoration in _layout.Decorations)
                painter.Rect(new Rect(origin + decoration.Px.position, decoration.Px.size), (Color)decoration.Color * tint, default);
            painter.Text(_layout, origin, tint, outline, _outlineColor.Value);
        }

        // Backgrounds the theme's InlineBackground handlers put behind their text (code), under everything else.
        private void DrawBackgrounds(Painter painter, Vector2 origin, float fontSize)
        {
            foreach (var placement in _placements)
            {
                if (placement.Prefab != null || placement.Handler is not InlineBackground background) continue;
                float pad = background.VerticalPaddingEm * fontSize;
                var rect = new Rect(origin.x + placement.Px.x, origin.y + placement.Px.y - pad, placement.Px.width, placement.Px.height + 2f * pad);
                painter.Rect(rect, background.Color, CornerRadii.All(background.CornerRadiusEm * fontSize), background.BorderWidth,
                    background.BorderColor);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            s_enabled.Add(this);
#if UNITY_EDITOR
            // Inline content outlives a script reload as children never saved, while this label forgets it: what it no
            // longer knows of goes, once the editor is out of the frame.
            UnityEditor.EditorApplication.delayCall += Sweep;
#endif
            if (!s_listening)
            {
                s_listening = true;
                TextSettings.Changed += LayOutAll;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            s_enabled.Remove(this);
            _layout?.Dispose();
            _layout = null;
            _hasLaidOut = false;
            _placements.Clear();
            _pointer = -1;
            // Its inline content goes out of sight with it: as children when its object goes, hidden when the label alone does.
            if (gameObject.activeInHierarchy)
                foreach (var (_, node) in _inlines)
                    if (node != null)
                        node.Display.Value = DisplayMode.None;
        }

        // A font stack, a theme or the text settings changed: every label lays its text out again.
        private static void LayOutAll()
        {
            foreach (var label in s_enabled)
            {
                label._hasLaidOut = false;
                label.Forget();
                label.ContentChanged();
            }
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

        private InlineContext Context(in TextLayoutInput input) => new(this, input.FontSizePx, _color.Peek);

        // Whether two inputs shape the same text: everything but colour, and the box when asked to ignore it.
        private static bool SameShape(in TextLayoutInput a, in TextLayoutInput b, bool ignoreBox) =>
            a.Text == b.Text && a.RichText == b.RichText && a.Font == b.Font && a.FontSizePx == b.FontSizePx
            && a.WordWrap == b.WordWrap && a.Alignment == b.Alignment && a.Overflow == b.Overflow && a.MaxLines == b.MaxLines
            && a.Bold == b.Bold && a.Italic == b.Italic && a.CharacterSpacingPx == b.CharacterSpacingPx
            && a.AntiAliasMarginPx == b.AntiAliasMarginPx && (ignoreBox || (a.WidthPx == b.WidthPx && a.HeightPx == b.HeightPx));

        // ── Inline content ───────────────────────────────────────────────────────

        // Drawing found the inline content changed or moved: it is made or moved before the next frame lays out, as a
        // frame draws, and changes to the hierarchy, wait.
        private void QueueInlines()
        {
            if (_inlinesQueued) return;
            if (_inlines.Count == 0 && !HasPrefabs()) return;
            _inlinesQueued = true;
            s_queued.Add(this);
            UI.RequestRepaint();
        }

        private bool HasPrefabs()
        {
            foreach (var placement in _placements)
                if (placement.Prefab != null)
                    return true;
            return false;
        }

        /// <summary>Makes, moves and takes away the inline content labels found changed as they drew: before a frame lays out.</summary>
        internal static void PlaceInlines()
        {
            if (s_queued.Count == 0) return;
            var queued = s_queued.ToArray();
            s_queued.Clear();
            foreach (var label in queued)
            {
                if (label == null) continue;
                label._inlinesQueued = false;
                if (label.isActiveAndEnabled)
                    label.Place();
            }
        }

        // One node for each placement with a prefab, in order: kept where the prefab is the same, taken from the spares or
        // made where it is not, and those left over hidden as spares.
        private void Place()
        {
            int used = 0;
            foreach (var placement in _placements)
            {
                if (placement.Prefab == null) continue;
                if (used < _inlines.Count && (_inlines[used].Prefab != placement.Prefab || _inlines[used].Node == null))
                {
                    Spare(_inlines[used]);
                    _inlines.RemoveAt(used);
                }
                if (used >= _inlines.Count || _inlines[used].Prefab != placement.Prefab)
                {
                    var made = Take(placement.Prefab);
                    if (made == null) continue;
                    _inlines.Insert(used, (placement.Prefab, made));
                }
                var node = _inlines[used].Node;
                used++;
                node.Display.Value = DisplayMode.Visible;
                var rect = placement.Px;
                node.Floating.Value = new Floating
                {
                    AttachTo = FloatingAttach.Parent, Point = AttachPoint.LeftTop, TargetPoint = AttachPoint.LeftTop,
                    Offset = _origin + rect.position,
                };
                node.Width.Value = Sizing.Fixed(rect.width);
                node.Height.Value = Sizing.Fixed(rect.height);
                if (_hasLaidOut && placement.TokenIndex < _layout.Document.Tokens.Count && node.TryGetComponent(out IInlineContent content))
                {
                    var fragment = new InlineFragment(rect, placement.Index, placement.Count, placement.BaselinePx, placement.AscentPx,
                        placement.DescentPx);
                    content.Bind(_layout.Document.Tokens[placement.TokenIndex], Context(in _laidOut), fragment);
                }
            }
            for (int i = _inlines.Count - 1; i >= used; i--)
            {
                Spare(_inlines[i]);
                _inlines.RemoveAt(i);
            }
        }

        // A node for an inline handler's prefab: a spare made from it, or one made now, inside the label's own node and
        // never saved with it.
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
                Debug.LogWarning($"UI Next: {prefab.name}, an inline handler's prefab, has no UINode on its root, so {name} cannot place it.", this);
                return null;
            }
            var instance = Instantiate(prefab, transform, false);
            instance.name = prefab.name;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags |= HideFlags.DontSave;
            instance.AddComponent<InlineOf>().Label = this;
            return instance.GetComponent<UINode>();
        }

        // An inline node no longer wanted: told so, hidden and kept to be used again.
        private void Spare((GameObject Prefab, UINode Node) inline)
        {
            if (inline.Node == null) return;
            if (inline.Node.TryGetComponent(out IInlineContent content))
                content.Unbind();
            inline.Node.Display.Value = DisplayMode.None;
            _spares.Add(inline);
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

        // ── Links ────────────────────────────────────────────────────────────────

        void IPointerHandler.OnPointerDown(in PointerEvent e)
        {
            if (_pointer >= 0 || !_hasLaidOut || _layout.Document.Links.Count == 0) return;
            int link = LinkAt(e.Position);
            if (link < 0 || !UIInput.TakeTap(this, e.PointerId)) return;
            _pointer = e.PointerId;
            _link = link;
        }

        void IPointerHandler.OnPointerMove(in PointerEvent e) { }

        void IPointerHandler.OnPointerUp(in PointerEvent e)
        {
            if (e.PointerId != _pointer) return;
            _pointer = -1;
            if (!_hasLaidOut || LinkAt(e.Position) != _link) return;
            var href = _layout.Document.Links[_link].Href;
            LinkClicked?.Invoke(href);
            _onLinkClicked.Invoke(href);
        }

        void IPointerHandler.OnPointerCancel(in PointerEvent e)
        {
            if (e.PointerId == _pointer)
                _pointer = -1;
        }

        // The link under a point in its root's points, or -1.
        private int LinkAt(Vector2 point) => _layout.LinkAt(Node.RootToLocal(point) - _origin);
    }
}
