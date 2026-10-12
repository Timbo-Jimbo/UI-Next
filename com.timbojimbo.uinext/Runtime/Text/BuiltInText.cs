using System.Collections.Generic;
using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The built-in text handlers and the prefabs they make nodes from, made in code the first time they are wanted,
    /// hidden and never saved, on the same classes as any of your own. The Markdown: a label with no markup, in a project
    /// with no default, reads its text with all of it, and a <see cref="Markup"/> keeps the parts it chooses after its own
    /// handlers. The chip an element handler with no prefab makes, and the image an image handler with none shows.
    /// </summary>
    internal static class BuiltInText
    {
        // Neutral greys at a low opacity, which read on light and dark alike, and a blue that does too.
        private static readonly Color Wash = new(0.55f, 0.58f, 0.62f, 0.16f);
        private static readonly Color Rule = new(0.55f, 0.58f, 0.62f, 0.5f);
        private static readonly Color Blue = new(0.2f, 0.5f, 0.95f, 1f);

        // Monospaced families, one of which each platform has: Windows, Apple's, Android, Linux.
        private static readonly string[] CodeFamilies = { "Consolas", "Menlo", "Droid Sans Mono", "DejaVu Sans Mono" };

        private static readonly List<(MarkdownFeatures Feature, TextRule Rule)> s_rules = new();
        private static readonly List<Object> s_made = new();
        private static Markup s_all;
        private static GameObject s_chip;
        private static GameObject s_image;

        /// <summary>All of the Markdown, as a markup.</summary>
        public static Markup All
        {
            get
            {
                if (s_all != null) return s_all;
                s_all = Markup.Create(null);
                s_all.name = "Built-in Markdown";
                s_all.hideFlags = HideFlags.HideAndDontSave;
                return s_all;
            }
        }

        /// <summary>A pill with a match as written in it (@name), in blue: what an element handler with no prefab makes.</summary>
        public static GameObject Chip => s_chip != null ? s_chip : s_chip = MakeChip();

        /// <summary>An image node a <see cref="TextImage"/> shows its match's image in: what an image handler with no prefab makes.</summary>
        public static GameObject Image => s_image != null ? s_image : s_image = MakeImage();

        /// <summary>Adds the Markdown's handlers for <paramref name="features"/> to <paramref name="rules"/>, in the order they are tried.</summary>
        public static void GetRules(MarkdownFeatures features, List<TextRule> rules)
        {
            if (features == MarkdownFeatures.None) return;
            MakeMarkdown();
            foreach (var (feature, rule) in s_rules)
                if ((features & feature) != 0)
                    rules.Add(rule);
        }

        // The Markdown's handlers, line rules first, longer delimiters before the shorter ones they start with.
        private static void MakeMarkdown()
        {
            if (s_rules.Count > 0 && s_rules[0].Rule != null) return;
            ClearMarkdown();
            var code = CodeFont();
            var item = ListItem();
            Add(MarkdownFeatures.CodeBlocks, "Code block", BlockHandler.Create(TextTrigger.Fenced(), CodeBlock(code)));
            Add(MarkdownFeatures.Headings, "Heading 1", SpanHandler.Create(TextTrigger.Line("# "), scale: 1.6f, bold: true));
            Add(MarkdownFeatures.Headings, "Heading 2", SpanHandler.Create(TextTrigger.Line("## "), scale: 1.35f, bold: true));
            Add(MarkdownFeatures.Headings, "Heading 3", SpanHandler.Create(TextTrigger.Line("### "), scale: 1.15f, bold: true));
            Add(MarkdownFeatures.Quotes, "Quote", BlockHandler.Create(TextTrigger.Line("> ", merge: true), Quote()));
            Add(MarkdownFeatures.Lists, "Bullet", BlockHandler.Create(TextTrigger.LineMatching(@"[-*+] "), item));
            Add(MarkdownFeatures.Lists, "Numbered item", BlockHandler.Create(TextTrigger.LineMatching(@"\d{1,9}[.)] "), item));
            Add(MarkdownFeatures.Code, "Code", SpanHandler.Create(TextTrigger.Wrapped("`", literal: true), scale: 0.9f, font: code,
                background: CodeBackground(), padding: new Vector2(0.25f, 0.08f)));
            Add(MarkdownFeatures.Links, "Link", SpanHandler.Create(TextTrigger.Link(), color: Blue, underline: true, tappable: true));
            Add(MarkdownFeatures.Emphasis, "Bold", SpanHandler.Create(TextTrigger.Wrapped("**", insideWords: true), bold: true));
            Add(MarkdownFeatures.Underline, "Underline", SpanHandler.Create(TextTrigger.Wrapped("__"), underline: true));
            Add(MarkdownFeatures.Strikethrough, "Strikethrough", SpanHandler.Create(TextTrigger.Wrapped("~~", insideWords: true), strikethrough: true));
            Add(MarkdownFeatures.Emphasis, "Italic", SpanHandler.Create(TextTrigger.Wrapped("*", insideWords: true), italic: true));
            Add(MarkdownFeatures.Emphasis, "Italic underscores", SpanHandler.Create(TextTrigger.Wrapped("_"), italic: true));
        }

        private static void Add(MarkdownFeatures feature, string name, TextRule rule)
        {
            rule.name = name;
            rule.hideFlags = HideFlags.HideAndDontSave;
            s_rules.Add((feature, rule));
            s_made.Add(rule);
        }

        // ── Prefabs ──────────────────────────────────────────────────────────────

        // The background behind code: a box as big as its piece of the line.
        private static GameObject CodeBackground()
        {
            var node = Template("Code background");
            node.Box(Wash, CornerRadii.All(4f));
            return Made(node);
        }

        // A code block: its code in a box across the width, in the code font at nine tenths of the text's size and in its
        // colour, as written.
        private static GameObject CodeBlock(FontStack font)
        {
            var node = Template("Code block").Pad(0f, 4f);
            var box = Node("Box", node).Pad(12f, 10f);
            box.Width.Value = Sizing.Grow();
            box.Box(Wash, CornerRadii.All(8f));
            var text = Node("Code", box);
            text.Width.Value = Sizing.Grow();
            var label = text.Label("");
            label.Font.Value = font;
            label.RichText.Value = false;
            var slot = text.Add<TextSlot>();
            slot.Inherits = TextSlotInherits.Size | TextSlotInherits.Color;
            slot.Scale = 0.9f;
            return Made(node);
        }

        // A quote: a bar beside its lines, read as the text is, a little faded.
        private static GameObject Quote()
        {
            var node = Template("Quote").Row(10f).Pad(0f, 2f);
            var bar = Node("Bar", node).Sized(Sizing.Fixed(3f), Sizing.Grow());
            bar.Box(Rule, CornerRadii.All(1.5f));
            var text = Node("Text", node);
            text.Width.Value = Sizing.Grow();
            text.Opacity.Value = 0.8f;
            text.Label("");
            text.Add<TextSlot>();
            return Made(node);
        }

        // A list item: its bullet or number beside its text, which hangs clear of it as it wraps.
        private static GameObject ListItem()
        {
            var node = Template("List item").Row(6f).Pad(new Insets(4f, 0f, 1f, 1f));
            var marker = Node("Marker", node);
            marker.Label("");
            var shown = marker.Add<TextSlot>();
            shown.Shows = TextSlotShows.Marker;
            shown.Inherits = TextSlotInherits.Font | TextSlotInherits.Size | TextSlotInherits.Color;
            var text = Node("Text", node);
            text.Width.Value = Sizing.Grow();
            text.Label("");
            text.Add<TextSlot>();
            return Made(node);
        }

        // A chip: a pill with the match as written in it, in blue at nine tenths of the text's size, darker held down.
        private static GameObject MakeChip()
        {
            var node = Template("Chip").Pad(7f, 1f).Center();
            var box = node.Box(new Color(Blue.r, Blue.g, Blue.b, 0.14f), CornerRadii.All(999f));
            box.Fill.When(InteractionStates.Pressed, new Color(Blue.r, Blue.g, Blue.b, 0.3f));
            var text = Node("Text", node);
            var label = text.Label("", 17f, Blue);
            label.RichText.Value = false;
            var slot = text.Add<TextSlot>();
            slot.Shows = TextSlotShows.Content;
            slot.Inherits = TextSlotInherits.Font | TextSlotInherits.Size;
            slot.Scale = 0.9f;
            return Made(node);
        }

        // An image: what its image handler has for its match, as big as the handler says, kept whole.
        private static GameObject MakeImage()
        {
            var node = Template("Image");
            node.Add<ImageView>().Fit.Value = ImageFit.Contain;
            node.Add<TextImage>();
            return Made(node);
        }

        // Code's fonts: the first monospaced family the platform has.
        private static FontStack CodeFont()
        {
            var stack = ScriptableObject.CreateInstance<FontStack>();
            stack.name = "Built-in code font";
            stack.hideFlags = HideFlags.HideAndDontSave;
            foreach (var family in CodeFamilies)
                stack.Sources.Add(new FontSource { Kind = FontSourceKind.OsFamily, Family = family });
            s_made.Add(stack);
            return stack;
        }

        // The root of a prefab made in code: inactive, so it is in no tree (what is made from it is), hidden and never saved.
        private static UINode Template(string name) => Node(name, null);

        private static UINode Node(string name, UINode parent)
        {
#if UNITY_EDITOR
            var go = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(RectTransform));
#else
            var go = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
#endif
            if (parent != null)
                go.transform.SetParent(parent.transform, false);
            else
                go.SetActive(false);
            return go.AddComponent<UINode>();
        }

        private static GameObject Made(UINode node)
        {
            s_made.Add(node.gameObject);
            return node.gameObject;
        }

        // ── Lifetime ─────────────────────────────────────────────────────────────

        // The Markdown made again: what it made goes, but for the chip and the image, which have their own lifetimes.
        private static void ClearMarkdown()
        {
            for (int i = s_made.Count - 1; i >= 0; i--)
            {
                var made = s_made[i];
                if (made == s_chip || made == s_image) continue;
                Destroy(made);
                s_made.RemoveAt(i);
            }
            s_rules.Clear();
        }

        private static void Destroy(Object made)
        {
            if (made == null) return;
            if (Application.isPlaying)
                Object.Destroy(made);
            else
                Object.DestroyImmediate(made);
        }

#if UNITY_EDITOR
        // What it made outlives a script reload, which forgets it: it goes before.
        [UnityEditor.InitializeOnLoadMethod]
        private static void ClearOnReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            foreach (var made in s_made)
                Destroy(made);
            s_made.Clear();
            s_rules.Clear();
            Destroy(s_all);
            s_all = null;
            s_chip = s_image = null;
        };
#endif
    }
}
