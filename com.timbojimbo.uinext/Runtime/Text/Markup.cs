using System;
using System.Collections.Generic;
using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>The parts of the built-in Markdown a <see cref="Markup"/> keeps after its own handlers.</summary>
    [Flags]
    public enum MarkdownFeatures
    {
        None = 0,
        /// <summary>**Bold**, *italic* and _italic_.</summary>
        Emphasis = 1 << 0,
        /// <summary>__Underline__.</summary>
        Underline = 1 << 1,
        /// <summary>~~Strikethrough~~.</summary>
        Strikethrough = 1 << 2,
        /// <summary>`Code`, with a background behind it.</summary>
        Code = 1 << 3,
        /// <summary>Code between ``` fences, as a block.</summary>
        CodeBlocks = 1 << 4,
        /// <summary>[Links](https://unity.com) and web addresses as they are written, tappable.</summary>
        Links = 1 << 5,
        /// <summary>Lines starting #, ## or ###.</summary>
        Headings = 1 << 6,
        /// <summary>Lines starting >, as a block with a bar beside it.</summary>
        Quotes = 1 << 7,
        /// <summary>Lines starting -, * or +, and 1., as blocks with their bullet or number beside them.</summary>
        Lists = 1 << 8,
    }

    /// <summary>
    /// What a label's text is read with: its own handlers (span, element and block handlers, or rules of your own), in
    /// order, then those of the built-in Markdown it keeps. At each place the first whose trigger matches takes the text,
    /// so a handler of its own for `code` takes the place of the built-in one. Assign it to a label, or as the project's
    /// default under Project Settings > Timbo Jimbo > UI Next Text.
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Text/Markup", fileName = "Markup")]
    public sealed class Markup : TextRuleSet
    {
        [Tooltip("Handlers of its own, tried in order before the built-in Markdown's.")]
        [SerializeField] private List<TextRule> _handlers = new();

        [Tooltip("The parts of the built-in Markdown that follow its own handlers.")]
        [SerializeField] private MarkdownFeatures _markdown = (MarkdownFeatures)~0;

        /// <summary>Its own handlers, tried in order; tell texts of a change with <see cref="TextSettings.NotifyChanged"/>.</summary>
        public List<TextRule> Handlers => _handlers;

        /// <summary>The parts of the built-in Markdown that follow its own handlers.</summary>
        public MarkdownFeatures Markdown
        {
            get => _markdown;
            set
            {
                _markdown = value;
                TextSettings.NotifyChanged();
            }
        }

        public override void GetRules(List<TextRule> rules)
        {
            foreach (var handler in _handlers)
                if (handler != null)
                    rules.Add(handler);
            BuiltInText.GetRules(_markdown, rules);
        }

        /// <summary>
        /// The fonts the built-in Markdown shows code in: the first of Consolas, Menlo, Droid Sans Mono and DejaVu Sans Mono
        /// the platform has. Prewarm it on a loading screen (<see cref="FontStack.Prewarm"/>), so the first code shown does
        /// not hold up its frame, or show code of your own in it.
        /// </summary>
        public static FontStack CodeFont => BuiltInText.CodeFont;

        /// <summary>A markup made in code: <paramref name="handlers"/> of its own, then the built-in Markdown's <paramref name="markdown"/>.</summary>
        public static Markup Create(IEnumerable<TextRule> handlers, MarkdownFeatures markdown = (MarkdownFeatures)~0)
        {
            var markup = CreateInstance<Markup>();
            if (handlers != null)
                markup._handlers.AddRange(handlers);
            markup._markdown = markdown;
            return markup;
        }
    }
}
