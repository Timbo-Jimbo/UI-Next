using System;
using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>What a <see cref="TextSlot"/> shows of the match its prefab stands for.</summary>
    public enum TextSlotShows
    {
        /// <summary>Its text: what was wrapped, the word after a prefix, a quote's lines, a code block's code.</summary>
        Text,
        /// <summary>It as written, as it would show as text: a mention with its @, a tag with its #.</summary>
        Content,
        /// <summary>Its argument: a link's target, a code block's language, a line's marker.</summary>
        Argument,
        /// <summary>Its argument as a list marker: a bullet (•) for -, * and +, the number as written otherwise.</summary>
        Marker,
    }

    /// <summary>What a <see cref="TextSlot"/>'s label takes from the label the match is in.</summary>
    [Flags]
    public enum TextSlotInherits
    {
        None = 0,
        Font = 1 << 0,
        /// <summary>Its size (of the text around the match, a heading's in a heading), times the slot's scale.</summary>
        Size = 1 << 1,
        Color = 1 << 2,
        /// <summary>Its markup and whether it reads it, so what it shows is read as the label's text is.</summary>
        Markup = 1 << 3,
    }

    /// <summary>
    /// Shows what a match says in a label in a handler's prefab, no code needed: the match's text (a quote's lines,
    /// Markdown and all), the match as written, its argument or its list marker, in the font, size, colour and markup of
    /// the label the match is in, as far as it takes them, following them as they change (a dark mode, a state).
    /// </summary>
    [RequireComponent(typeof(Label))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Text Slot")]
    public sealed class TextSlot : MonoBehaviour, ITextContent
    {
        [Tooltip("What it shows of the match.")]
        [SerializeField] private TextSlotShows _shows = TextSlotShows.Text;

        [Tooltip("What its label takes from the label the match is in.")]
        [SerializeField] private TextSlotInherits _inherits = (TextSlotInherits)~0;

        [Tooltip("Its size as a multiple of the label's, when it takes its size.")]
        [SerializeField, Min(0.01f)] private float _scale = 1f;

        public TextSlotShows Shows { get => _shows; set => _shows = value; }
        public TextSlotInherits Inherits { get => _inherits; set => _inherits = value; }
        public float Scale { get => _scale; set => _scale = value; }

        public void Bind(in TextMatch match, Label label)
        {
            var own = GetComponent<Label>();
            own.Text.Value = _shows switch
            {
                TextSlotShows.Content => ContentOf(in match, label),
                TextSlotShows.Argument => match.Argument ?? "",
                TextSlotShows.Marker => MarkerOf(match.Argument),
                _ => match.Text ?? "",
            };
            if (label == null || label == own) return;
            if ((_inherits & TextSlotInherits.Font) != 0)
                own.Font.Bind(() => label.Font.Value);
            if ((_inherits & TextSlotInherits.Size) != 0)
            {
                var around = match;
                own.FontSize.Bind(() => label.FontSizeAround(in around) * _scale);
            }
            if ((_inherits & TextSlotInherits.Color) != 0)
                own.Color.Bind(() => label.Color.Value);
            if ((_inherits & TextSlotInherits.Markup) != 0)
            {
                own.Markup.Bind(() => label.Markup.Value);
                own.RichText.Bind(() => label.RichText.Value);
            }
        }

        // A match as written: the part of the label's text it shows when it stays text.
        private static string ContentOf(in TextMatch match, Label label)
        {
            var source = label != null ? label.Document.Text : null;
            if (source == null || match.ContentStart < 0 || match.ContentEnd > source.Length) return match.Text ?? "";
            return source.Substring(match.ContentStart, match.ContentLength);
        }

        // A list item's marker as shown: a bullet for -, * and +, a number as written.
        private static string MarkerOf(string marker) => marker switch
        {
            null => "",
            "-" or "*" or "+" => "•",
            _ => marker,
        };
    }
}
