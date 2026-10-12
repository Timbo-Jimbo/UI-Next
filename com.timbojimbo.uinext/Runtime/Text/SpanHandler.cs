using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text its trigger finds stays text, wrapping with the rest: in a style of its own (fonts, size, colour, weight,
    /// lines), with a node made from <see cref="Background"/> behind each line of it (a Box, with tokens and states, as
    /// behind code), and perhaps tappable: a tap raises the label's <see cref="Label.SpanTapped"/>, or with a Link
    /// trigger, its <see cref="Label.LinkClicked"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Text/Span Handler", fileName = "Span Handler")]
    public class SpanHandler : TextRule
    {
        [Tooltip("The fonts it draws with; none keeps the text's.")]
        [SerializeField] private FontStack _font;

        [Tooltip("Its size as a multiple of the text's.")]
        [SerializeField, Min(0.01f)] private float _scale = 1f;

        [Tooltip("Its colour, kept whatever colour the label draws in. Clear keeps the label's.")]
        [SerializeField] private Color _color = Color.clear;

        [SerializeField] private bool _bold;
        [SerializeField] private bool _italic;
        [SerializeField] private bool _underline;
        [SerializeField] private bool _strikethrough;

        [Tooltip("A prefab with a UINode at its root, made into a node behind each line of the span, as big as its piece of that line. Its components implementing ITextContent are told the match.")]
        [SerializeField] private GameObject _background;

        [Tooltip("x: room the layout keeps either side of its text, in em. y: how far its background reaches above and below its line, in em.")]
        [SerializeField] private Vector2 _padding;

        [Tooltip("A tap on its text raises the label's Span Tapped, or for a link, its Link Clicked.")]
        [SerializeField] private bool _tappable;

        public override TextRole Role => TextRole.Span;
        public override float PaddingEm => Mathf.Max(0f, _padding.x);
        public override bool Tappable => _tappable;

        /// <summary>The prefab made into a node behind each line of a span, or null for none.</summary>
        public GameObject Background => _background;

        /// <summary>How far a span's background reaches above and below its line, in em of the label's size.</summary>
        public float BackgroundReachEm => _padding.y;

        public override void Style(in TextMatch match, ref TextStyle style)
        {
            if (_font != null) style.Font = _font;
            style.Scale *= _scale > 0f ? _scale : 1f;
            if (_color.a > 0f) style.Color = _color;
            style.Bold |= _bold;
            style.Italic |= _italic;
            style.Underline |= _underline;
            style.Strikethrough |= _strikethrough;
        }

        /// <summary>The prefab behind a match's lines: <see cref="Background"/>, unless a handler of your own picks one per match.</summary>
        public virtual GameObject BackgroundFor(in TextMatch match) => _background;

        /// <summary>
        /// A tap on a span's text, when it is tappable: raises the label's <see cref="Label.LinkClicked"/> with a link's
        /// target, and its <see cref="Label.SpanTapped"/> for the rest. Override to handle it in the handler.
        /// </summary>
        public virtual void Tapped(in TextMatch match, Label label) => label.RaiseTapped(in match);

        /// <summary>A span handler made in code, as the built-in Markdown's are.</summary>
        public static SpanHandler Create(TextTrigger trigger, float scale = 1f, Color? color = null, bool bold = false, bool italic = false,
            bool underline = false, bool strikethrough = false, FontStack font = null, GameObject background = null, Vector2 padding = default,
            bool tappable = false)
        {
            var handler = CreateInstance<SpanHandler>();
            handler.Configure(trigger);
            handler._font = font;
            handler._scale = scale;
            handler._color = color ?? Color.clear;
            handler._bold = bold;
            handler._italic = italic;
            handler._underline = underline;
            handler._strikethrough = strikethrough;
            handler._background = background;
            handler._padding = padding;
            handler._tappable = tappable;
            return handler;
        }
    }
}
