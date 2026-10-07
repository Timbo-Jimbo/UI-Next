using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Circle;
using TimboJimbo.UINext.Demos.Showcase;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>
    /// Builds the showcase: a list of the stack's features, each opening a page of a few small cases, one idea each.
    /// </summary>
    public static class ShowcaseBuilder
    {
        private const string Folder = DemoKit.DemosFolder + "/Showcase";
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Showcase.unity";

        private static readonly Color Background = new Color32(0xF2, 0xF2, 0xF7, 0xFF);
        private static readonly Color Card = Color.white;
        private static readonly Color Text = new Color32(0x12, 0x12, 0x16, 0xFF);
        private static readonly Color Secondary = new Color32(0x6E, 0x6E, 0x78, 0xFF);
        private static readonly Color Accent = new Color32(0x34, 0x78, 0xF6, 0xFF);
        private static readonly Color Pressed = new(0.5f, 0.5f, 0.55f, 0.18f);
        private static readonly Color[] Palette =
        {
            new Color32(0xFF, 0x7E, 0xB6, 0xFF), new Color32(0x5C, 0x8D, 0xFF, 0xFF), new Color32(0xF2, 0xA9, 0x3B, 0xFF),
            new Color32(0x3F, 0xC1, 0xC9, 0xFF), new Color32(0xA7, 0x7B, 0xFF, 0xFF), new Color32(0xFF, 0x6B, 0x6B, 0xFF),
        };

        [MenuItem("Tools/UI Next/Demos/Build Showcase")]
        private static void BuildAndOpen() => Build(open: true);

        public static void Build(bool open) => DemoKit.BuildScene(ScenePath, _ => BuildScene(), open);

        private static void BuildScene()
        {
            DemoKit.EnsureFolder(Folder);
            DemoKit.Camera(Color.black);
            var root = DemoKit.Root("Showcase");
            var app = root.Node.Column();
            app.Box(Background);
            var showcase = app.Add<ShowcaseApp>();

            var home = app.Child("Home").Sized(Sizing.Grow(), Sizing.Grow()).Column(8f).Pad(new Insets(16f, 16f, 16f, 24f));
            home.Scroll();
            home.Child("Title").Label("UI Next", 34f, Text, bold: true);
            home.Child("Subtitle").Label("What the stack does, one idea a case.", 15f, Secondary);
            var list = home.Child("List").Column();
            list.Width.Value = Sizing.Grow();
            list.Box(Card, 16f);

            var entries = new List<Pressable>();
            var pages = new List<Presentation>();
            var backs = new List<Pressable>();
            void Add(string icon, string title, System.Action<UINode, UINode> build)
            {
                int index = entries.Count;
                entries.Add(Entry(list, icon, title, Palette[index % Palette.Length]));
                var (page, back, body) = Page(app, title);
                pages.Add(page);
                backs.Add(back);
                build(page.Node, body);
            }

            Add("📐", "Layout", LayoutPage);
            Add("🌀", "Animate", AnimatePage);
            Add("👆", "States", StatesPage);
            Add("🎨", "Tokens and themes", TokensPage);
            Add("🔤", "Text", TextPage);
            Add("📜", "Scrolling", ScrollPage);
            Add("🪟", "Presentations", PresentPage);
            Add("✂️", "Clips and order", OrderPage);
            Add("🔗", "Bindings", BindingsPage);
            showcase.Setup(entries, pages, backs);
        }

        // ── Scaffolding ──────────────────────────────────────────────────────────

        private static Pressable Entry(UINode list, string icon, string title, Color colour)
        {
            var row = list.Child(title).Row(12f).Pad(16f, 12f).Align(AlignX.Left, AlignY.Center);
            row.Width.Value = Sizing.Grow();
            var rowBox = row.Box(Color.clear);
            rowBox.Fill.When(InteractionStates.Pressed, Pressed);
            rowBox.Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
            var badge = row.Child("Icon").Sized(Sizing.Fixed(32f), Sizing.Fixed(32f)).Center();
            badge.Box(colour, 8f);
            badge.Child("Glyph").Label(icon, 17f);
            var label = row.Child("Title").Label(title, 17f, Text);
            label.Node.Width.Value = Sizing.Grow();
            row.Child("Chevron").Label("›", 22f, Secondary);
            return row.Pressable();
        }

        private static (Presentation page, Pressable back, UINode body) Page(UINode app, string title)
        {
            var host = app.Child(title + " Page").Column();
            var page = host.Add<Presentation>();
            page.Style = PresentationStyle.Cover;
            page.Transition = PresentationTransition.Push;
            page.Dim.Value = new Color(0f, 0f, 0f, 0.15f);
            page.Animation = new UIAnimation(0.4f);
            var content = host.Child("Content").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            content.IgnoresSafeArea.Value = Edges.All;
            content.Box(Background);
            var header = content.Child("Header").Row(8f).Pad(new Insets(8f, 16f, 10f, 10f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            header.Box(Card);
            var back = header.Child("Back").Pad(8f, 4f);
            back.Box(Color.clear, 8f).Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.15f));
            back.Child("Title").Label("‹ Back", 17f, Accent);
            var title_ = header.Child("Title").Label(title, 17f, Text, bold: true);
            title_.Node.Width.Value = Sizing.Grow();
            var body = content.Child("Body").Sized(Sizing.Grow(), Sizing.Grow()).Column(22f).Pad(new Insets(16f, 16f, 16f, 40f));
            body.Scroll();
            return (page, back.Pressable(), body);
        }

        // A case: a caption, and the node its demo goes in.
        private static UINode Case(UINode body, string caption)
        {
            var group = body.Child(caption).Column(8f);
            group.Width.Value = Sizing.Grow();
            group.Child("Caption").Label(caption.ToUpperInvariant(), 12f, Secondary, bold: true);
            var demo = group.Child("Demo");
            demo.Width.Value = Sizing.Grow();
            return demo;
        }

        private static UINode Panel(UINode node, float padding = 12f)
        {
            node.Pad(padding);
            node.Box(Card, 14f);
            return node;
        }

        private static Pressable Button(UINode parent, string title, Color fill, Color text)
        {
            var button = parent.Child(title).Pad(18f, 10f).Center();
            var box = button.Box(fill, 12f);
            box.Fill.When(InteractionStates.Pressed, Color.Lerp(fill, Color.black, 0.25f));
            box.Fill.When(InteractionStates.Hovered, Color.Lerp(fill, Color.white, 0.15f));
            box.Fill.When(InteractionStates.Disabled, new Color(0.78f, 0.78f, 0.8f));
            DemoKit.FocusRing(box, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
            button.Child("Title").Label(title, 16f, text, bold: true);
            return button.Pressable();
        }

        private static Pressable Switch(UINode parent, string title)
        {
            var row = parent.Child(title).Row(10f).Align(AlignX.Left, AlignY.Center);
            row.Child("Title").Label(title, 15f, Text);
            var track = row.Child("Switch").Sized(Sizing.Fixed(51f), Sizing.Fixed(31f)).Pad(2f).Align(AlignX.Left, AlignY.Center);
            var trackBox = track.Box(new Color(0.47f, 0.47f, 0.5f, 0.32f), 15.5f);
            trackBox.Fill.When(InteractionStates.Selected, new Color32(0x34, 0xC7, 0x59, 0xFF));
            DemoKit.FocusRing(trackBox, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
            var knob = track.Child("Knob").Sized(Sizing.Fixed(27f), Sizing.Fixed(27f));
            DemoKit.Shadow(knob.Box(Color.white, 13.5f), 0.2f, 4f, 2f);
            var pressable = track.Pressable();
            pressable.Toggles = true;
            track.Add<ToggleSwitch>();
            return pressable;
        }

        // ── Pages ────────────────────────────────────────────────────────────────

        private static void LayoutPage(UINode page, UINode body)
        {
            var sizes = Panel(Case(body, "Fit, fixed, grow, percent").Row(8f), 8f);
            foreach (var (title, width, colour) in new[]
                     {
                         ("Fit", Sizing.Fit(), Palette[0]), ("80", Sizing.Fixed(80f), Palette[1]),
                         ("Grow", Sizing.Grow(), Palette[2]), ("25%", Sizing.Percent(0.25f), Palette[3]),
                     })
            {
                var cell = sizes.Child(title).Sized(width, Sizing.Fixed(44f)).Pad(10f, 0f).Center();
                cell.Box(colour, 10f);
                cell.Child("Title").Label(title, 15f, Color.white, bold: true);
            }

            var alignDemo = Case(body, "Alignment (tap it)");
            var box = Panel(alignDemo.Child("Box").Sized(Sizing.Grow(), Sizing.Fixed(130f)), 10f).Center();
            box.Child("Dot").Sized(Sizing.Fixed(40f), Sizing.Fixed(40f)).Box(Accent, 12f);
            var alignPressable = box.Pressable();

            var wrapDemo = Case(body, "Wrapping").Column(10f);
            var chips = Panel(wrapDemo.Child("Chips").Row(8f), 10f);
            chips.Width.Value = Sizing.Grow();
            chips.Wrap.Value = Wrap.Lines;
            foreach (var word in new[] { "layout", "springs", "text", "tokens", "states" })
            {
                var chip = chips.Child("Chip").Pad(12f, 6f);
                chip.Box(new Color(Accent.r, Accent.g, Accent.b, 0.14f), 14f);
                chip.Child("Text").Label(word, 15f, Accent);
            }
            var add = Button(wrapDemo, "Add a chip", Accent, Color.white);

            var floatDemo = Panel(Case(body, "Floating").Row(24f), 16f);
            for (int i = 0; i < 3; i++)
            {
                var icon = floatDemo.Child("Icon").Sized(Sizing.Fixed(56f), Sizing.Fixed(56f)).Center();
                icon.Box(Palette[i + 3], 14f);
                icon.Child("Glyph").Label(new[] { "✉️", "🔔", "🛒" }[i], 24f);
                var badge = icon.Child("Badge").Sized(Sizing.Fixed(24f), Sizing.Fixed(24f)).Center()
                    .Float(AttachPoint.CenterCenter, AttachPoint.RightTop, new Vector2(-4f, 4f));
                badge.Box(new Color32(0xFF, 0x3B, 0x30, 0xFF), 12f);
                badge.Child("Count").Label((i * 4 + 3).ToString(), 13f, Color.white, bold: true);
            }

            page.Add<LayoutDemo>().Setup(alignPressable, add, chips);
        }

        private static void AnimatePage(UINode page, UINode body)
        {
            var tracksDemo = Case(body, "Springs (tap a track, and again mid-way)").Column(10f);
            var tracks = new List<Pressable>();
            var animations = new List<UIAnimation>();
            foreach (var (name, animation) in new[] { ("Smooth", UIAnimation.Smooth), ("Snappy", UIAnimation.Snappy), ("Bouncy", UIAnimation.Bouncy), ("Slow", new UIAnimation(1.2f, 0.2f)) })
            {
                tracksDemo.Child(name).Label(name, 13f, Secondary);
                var track = tracksDemo.Child(name + " Track").Sized(Sizing.Grow(), Sizing.Fixed(44f)).Pad(4f).Align(AlignX.Left, AlignY.Center);
                track.Box(Card, 22f);
                track.Child("Ball").Sized(Sizing.Fixed(36f), Sizing.Fixed(36f)).Box(Palette[tracks.Count], 18f);
                tracks.Add(track.Pressable());
                animations.Add(animation);
            }

            var sizeDemo = Case(body, "Size: what is below moves with it").Column(10f);
            var card = sizeDemo.Child("Card").Sized(Sizing.Grow(), Sizing.Fixed(72f)).Pad(16f);
            card.Box(Accent, 16f);
            card.Child("Text").Label("Tap to grow", 16f, Color.white, bold: true);
            var below = sizeDemo.Child("Below").Sized(Sizing.Grow(), Sizing.Fixed(50f)).Pad(16f).Align(AlignX.Left, AlignY.Center);
            below.Box(Card, 14f);
            below.Child("Text").Label("I move with it", 15f, Secondary);

            var comeGo = Case(body, "Appear and disappear (tap the toggles)").Column(10f);
            var toggleRow = comeGo.Child("Toggles").Row(8f);
            toggleRow.Width.Value = Sizing.Grow();
            var toggles = new List<Pressable>();
            var items = new List<UINode>();
            var list = Panel(comeGo.Child("Items").Column(8f), 10f);
            list.Width.Value = Sizing.Grow();
            foreach (var (name, fade, scale, edge, colour) in new[]
                     {
                         ("Fades", true, 1f, DisplayEdge.None, Palette[0]), ("Grows", true, 0.6f, DisplayEdge.None, Palette[1]),
                         ("Slides", false, 1f, DisplayEdge.Right, Palette[2]),
                     })
            {
                // Each toggle hides its item; greyed while it is hidden.
                var toggle = Button(toggleRow, name, Accent, Color.white);
                toggle.Toggles = true;
                toggle.Node.Width.Value = Sizing.Grow();
                toggle.GetComponent<Box>().Fill.When(InteractionStates.Selected, new Color(0.62f, 0.62f, 0.66f));
                toggles.Add(toggle);
                var item = list.Child(name).Sized(Sizing.Grow(), Sizing.Fixed(44f)).Pad(14f, 0f).Align(AlignX.Left, AlignY.Center);
                item.Box(colour, 12f);
                item.Child("Title").Label(name, 16f, Color.white, bold: true);
                var transition = item.Add<DisplayTransition>();
                transition.Fade = fade;
                transition.Scale = scale;
                transition.Edge = edge;
                transition.PastParent = edge != DisplayEdge.None;
                items.Add(item);
            }
            list.Clip(14f);

            var fadeCase = Case(body, "Cross-fade: text cannot interpolate (tap it)");
            var weather = fadeCase.Child("Weather").Pad(18f, 12f).Align(AlignX.Left, AlignY.Center);
            weather.Box(Card, 14f);
            var weatherText = weather.Child("Text").Label("☀️ Sunny", 22f, Text, bold: true);

            page.Add<AnimateDemo>().Setup(tracks, animations, card.Pressable(), toggles, items, weather.Pressable(), weatherText);
        }

        private static void StatesPage(UINode page, UINode body)
        {
            var interaction = Panel(Case(body, "Pressed, hovered, disabled").Row(16f), 14f).Align(AlignX.Left, AlignY.Center);
            var button = Button(interaction, "Press me", Accent, Color.white);
            var disable = Switch(interaction, "Disabled");

            var segmented = Case(body, "Selected").Row(0f).Pad(4f);
            segmented.Box(new Color(0.47f, 0.47f, 0.5f, 0.12f), 12f);
            var pill = segmented.Child("Pill");
            DemoKit.Shadow(pill.Box(Card, 9f), 0.12f, 6f, 1f);
            var segments = new List<Pressable>();
            foreach (var title in new[] { "Day", "Week", "Month" })
            {
                var segment = segmented.Child(title).Sized(Sizing.Grow(), Sizing.Fixed(36f)).Center();
                var label = segment.Child("Title").Label(title, 15f, Secondary, bold: true);
                label.Color.When(InteractionStates.Selected, Text);
                segments.Add(segment.Pressable());
            }
            pill.FloatOn(segments[0].Node);
            pill.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            segments[0].IsSelected = true;

            var warning = DemoKit.State(Folder + "/Warning.asset", "Warning", StateKind.State);
            var customDemo = Case(body, "A state of your own").Column(10f);
            var panel = customDemo.Child("Panel").Column(4f).Pad(14f);
            panel.Width.Value = Sizing.Grow();
            var panelBox = panel.Box(Card, 14f);
            panelBox.Fill.When(warning, new Color32(0xFF, 0xF1, 0xDC, 0xFF));
            panelBox.BorderWidth.Value = 1.5f;
            panelBox.BorderColor.Value = new Color(0f, 0f, 0f, 0.06f);
            panelBox.BorderColor.When(warning, new Color32(0xF2, 0xA9, 0x3B, 0xFF));
            var title_ = panel.Child("Title").Label("Battery 82%", 17f, Text, bold: true);
            title_.Text.When(warning, "Battery 12%");
            title_.Color.When(warning, new Color32(0xB2, 0x5E, 0x00, 0xFF));
            var detail = panel.Child("Detail").Label("All good.", 15f, Secondary);
            detail.Text.When(warning, "Charge it soon.");
            var warn = Switch(customDemo, "Warning");

            page.Add<StatesDemo>().Setup(button, disable, segments, pill, warn, panel, warning);
        }

        private static void TokensPage(UINode page, UINode body)
        {
            var dark = DemoKit.State(Folder + "/Dark.asset", "Dark", StateKind.Mode);
            var sepia = DemoKit.State(Folder + "/Sepia.asset", "Sepia", StateKind.Mode);
            ColorToken Token(string name, Color light, Color darkValue, Color sepiaValue)
            {
                var token = DemoKit.Token(Folder + "/" + name + ".asset", light, dark, darkValue);
                token.SetMode(sepia, sepiaValue);
                EditorUtility.SetDirty(token);
                return token;
            }
            var background = Token("Theme Background", Background, new Color32(0x0B, 0x0B, 0x0F, 0xFF), new Color32(0xF4, 0xEC, 0xD8, 0xFF));
            var surface = Token("Theme Surface", Card, new Color32(0x1C, 0x1C, 0x22, 0xFF), new Color32(0xFB, 0xF5, 0xE6, 0xFF));
            var text = Token("Theme Text", Text, new Color32(0xF2, 0xF2, 0xF7, 0xFF), new Color32(0x4A, 0x3B, 0x2A, 0xFF));
            var secondary = Token("Theme Secondary", Secondary, new Color32(0x9A, 0x9A, 0xA6, 0xFF), new Color32(0x8C, 0x76, 0x5A, 0xFF));
            var accent = Token("Theme Accent", Accent, new Color32(0x0A, 0x84, 0xFF, 0xFF), new Color32(0xC0, 0x6C, 0x2B, 0xFF));
            AssetDatabase.SaveAssets();

            // The page's own colours come from the tokens too.
            var content = page.transform.Find("Content").GetComponent<UINode>();
            content.GetComponent<Box>().Fill.Token = background;
            var header = content.transform.Find("Header");
            header.GetComponent<Box>().Fill.Token = surface;
            header.Find("Title").GetComponent<Label>().Color.Token = text;

            var picker = Case(body, "Theme").Row(8f);
            var themes = new List<Pressable>();
            foreach (var name in new[] { "Light", "Dark", "Sepia" })
            {
                var theme = picker.Child(name).Sized(Sizing.Grow(), Sizing.Fixed(40f)).Center();
                var box = DemoKit.Fill(theme, surface, 12f);
                box.BorderWidth.Value = 2f;
                box.BorderColor.Value = Color.clear;
                box.BorderColor.When(InteractionStates.Selected, Accent);
                DemoKit.Text(theme, "Title", name, 15f, text, bold: true);
                themes.Add(theme.Pressable());
            }
            themes[0].IsSelected = true;

            var sample = Case(body, "Everything here is a token").Column(10f).Pad(18f);
            DemoKit.Fill(sample, surface, 18f);
            DemoKit.Text(sample, "Title", "Weekly summary", 20f, text, bold: true);
            var paragraph = DemoKit.Text(sample, "Body", "Each colour on this page is a token with a value per theme. Picking a theme sets a mode on the page, animated.", 15f, secondary);
            paragraph.Node.Width.Value = Sizing.Grow();
            var cta = sample.Child("Button").Pad(16f, 10f).Center();
            DemoKit.Fill(cta, accent, 12f);
            cta.Child("Title").Label("Open report", 15f, Color.white, bold: true);
            cta.Pressable();

            page.Add<TokensDemo>().Setup(content, themes, new List<UIState> { null, dark, sepia });
        }

        private static void TextPage(UINode page, UINode body)
        {
            var sizes = Panel(Case(body, "Sizes and weights").Column(6f), 14f);
            sizes.Child("Large").Label("Large title", 30f, Text, bold: true);
            sizes.Child("Title").Label("Title", 22f, Text, bold: true);
            sizes.Child("Body").Label("Body text, the size most reading happens at.", 16f, Text);
            sizes.Child("Caption").Label("Caption", 12f, Secondary);

            var markdown = Panel(Case(body, "Markdown").Column(6f), 14f);
            var md = markdown.Child("Text").Label("Text is Markdown: **bold**, *italic*, `code`, and [links](https://unity.com).", 16f, Text);
            md.Node.Width.Value = Sizing.Grow();

            var align = Panel(Case(body, "Alignment").Column(6f), 14f);
            foreach (var (title, anchor) in new[] { ("Left", TextAnchor.UpperLeft), ("Centre", TextAnchor.UpperCenter), ("Right", TextAnchor.UpperRight) })
            {
                var label = align.Child(title).Label(title, 16f, Text);
                label.Alignment.Value = anchor;
                label.Node.Width.Value = Sizing.Grow();
            }

            var lines = Panel(Case(body, "At most two lines").Column(6f), 14f);
            var limited = lines.Child("Text").Label("A long paragraph that keeps going well past what two lines can hold at this size, so it is cut short with an ellipsis rather than running on.", 16f, Text);
            limited.MaxLines.Value = 2;
            limited.Node.Width.Value = Sizing.Grow();

            var scripts = Panel(Case(body, "Emoji and scripts").Column(6f), 14f);
            scripts.Child("Emoji").Label("Emoji 🎉🚀✨🦊", 18f, Text);
            scripts.Child("Scripts").Label("Ελληνικά · 日本語 · 한국어", 18f, Text);
            scripts.Child("Right to left").Label("مرحبا بالعالم · שלום עולם", 18f, Text);
        }

        private static void ScrollPage(UINode page, UINode body)
        {
            var pagesDemo = Case(body, "Paging (swipe)");
            var pager = pagesDemo.Child("Pager").Row(0f);
            pager.Sized(Sizing.Grow(), Sizing.Fixed(150f));
            pager.Clip(16f);
            var scroll = pager.Scroll(ScrollAxis.Horizontal);
            scroll.Snap.Value = ScrollSnap.Pages;
            for (int i = 0; i < 5; i++)
            {
                var card = pager.Child("Page " + (i + 1)).Sized(Sizing.Percent(1f), Sizing.Grow()).Center();
                var box = card.Box(Palette[i], 16f);
                box.Gradient.Value = true;
                box.GradientTo.Value = Color.Lerp(Palette[i], Color.black, 0.3f);
                card.Child("Number").Label((i + 1).ToString(), 44f, Color.white, bold: true);
            }

            var carousel = Case(body, "Free scrolling").Row(10f);
            carousel.Scroll(ScrollAxis.Horizontal);
            for (int i = 0; i < 12; i++)
            {
                var tile = carousel.Child("Tile").Sized(Sizing.Fixed(90f), Sizing.Fixed(90f)).Center();
                tile.Box(Palette[i % Palette.Length], 18f);
                tile.Child("Number").Label((i + 1).ToString(), 22f, Color.white, bold: true);
            }

            var listDemo = Case(body, "A list in a box (it bounces at the ends)");
            var listBox = listDemo.Child("List").Column().Sized(Sizing.Grow(), Sizing.Fixed(300f));
            listBox.Box(Card, 14f);
            listBox.Clip(14f);
            listBox.Scroll();
            for (int i = 0; i < 30; i++)
            {
                var row = listBox.Child("Row").Pad(16f, 12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                row.Box(Color.clear).Fill.When(InteractionStates.Pressed, Pressed);
                row.Child("Text").Label("Row " + (i + 1), 16f, Text);
                row.Pressable();
            }
        }

        private static void PresentPage(UINode page, UINode body)
        {
            // The tree is not built yet while building: the app is the page's transform parent.
            var app = page.transform.parent.GetComponent<UINode>();
            var buttons = Panel(Case(body, "Present").Column(10f), 14f);
            buttons.Width.Value = Sizing.Grow();

            // A sheet, with an alert over it.
            var sheetHost = app.Child("Demo Sheet").Column().Align(AlignX.Left, AlignY.Bottom);
            var sheet = sheetHost.Add<Presentation>();
            var sheetCard = sheetHost.Child("Card").Column(12f).Pad(new Insets(20f, 20f, 20f, 34f));
            sheetCard.Width.Value = Sizing.Grow();
            sheetCard.IgnoresSafeArea.Value = Edges.Bottom;
            sheetCard.Box(Card, new CornerRadii(24f, 24f, 0f, 0f));
            sheetCard.Child("Title").Label("A sheet", 22f, Text, bold: true);
            var sheetBody = sheetCard.Child("Body").Label("Tap the dim or press back to dismiss it, or present an alert over it.", 15f, Secondary);
            sheetBody.Node.Width.Value = Sizing.Grow();
            var alertOverSheet = Button(sheetCard, "Present an alert over it", Accent, Color.white);
            alertOverSheet.Node.Width.Value = Sizing.Grow();
            var closeSheet = Button(sheetCard, "Close", new Color(0.47f, 0.47f, 0.5f, 0.15f), Text);
            closeSheet.Node.Width.Value = Sizing.Grow();

            var alertHost = app.Child("Demo Alert").Center();
            var alert = alertHost.Add<Presentation>();
            alert.Transition = PresentationTransition.Pop;
            alert.Animation = new UIAnimation(0.35f, 0.15f);
            var alertCard = alertHost.Child("Card").Column(14f).Pad(22f).Align(AlignX.Center, AlignY.Top);
            alertCard.Width.Value = Sizing.Fixed(280f);
            DemoKit.Shadow(alertCard.Box(Card, 20f), 0.2f, 30f, 10f);
            alertCard.Child("Title").Label("An alert", 18f, Text, bold: true);
            var alertBody = alertCard.Child("Body").Label("It pops over whatever is up.", 14f, Secondary);
            alertBody.Alignment.Value = TextAnchor.UpperCenter;
            var okAlert = Button(alertCard, "OK", Accent, Color.white);
            okAlert.Node.Width.Value = Sizing.Grow();

            var coverHost = app.Child("Demo Cover").Column(16f).Center();
            var cover = coverHost.Add<Presentation>();
            cover.Style = PresentationStyle.Cover;
            cover.Transition = PresentationTransition.Fade;
            cover.Dim.Value = Color.clear;
            var coverContent = coverHost.Child("Content").Sized(Sizing.Grow(), Sizing.Grow()).Column(16f).Center();
            coverContent.IgnoresSafeArea.Value = Edges.All;
            var coverBox = coverContent.Box(Palette[4]);
            coverBox.Gradient.Value = true;
            coverBox.GradientTo.Value = Palette[1];
            coverBox.GradientAngle.Value = 60f;
            coverContent.Child("Title").Label("Full screen", 30f, Color.white, bold: true);
            coverContent.Child("Body").Label("A cover: once it is in, nothing under it is drawn.", 15f, Color.white);
            var closeCover = Button(coverContent, "Close", Color.white, Palette[4]);

            var presentSheet = Button(buttons, "Sheet", Accent, Color.white);
            presentSheet.Node.Width.Value = Sizing.Grow();
            var presentAlert = Button(buttons, "Alert", Accent, Color.white);
            presentAlert.Node.Width.Value = Sizing.Grow();
            var presentCover = Button(buttons, "Full screen", Accent, Color.white);
            presentCover.Node.Width.Value = Sizing.Grow();

            page.Add<PresentDemo>().Setup(
                new List<Pressable> { presentSheet, presentAlert, presentCover, alertOverSheet },
                new List<Presentation> { sheet, alert, cover, alert },
                new List<Pressable> { closeSheet, okAlert, closeCover },
                new List<Presentation> { sheet, alert, cover });
        }

        private static void OrderPage(UINode page, UINode body)
        {
            var clips = Panel(Case(body, "Rounded clips").Row(20f), 16f).Align(AlignX.Left, AlignY.Center);
            var circle = clips.Child("Circle").Sized(Sizing.Fixed(110f), Sizing.Fixed(110f)).Clip(55f);
            var photo = circle.Child("Photo").Sized(Sizing.Fixed(180f), Sizing.Fixed(140f));
            photo.Add<ScenePhoto>().Show(2);
            photo.Offset.Value = new Vector2(-40f, -20f);
            var rounded = clips.Child("Rounded").Sized(Sizing.Fixed(150f), Sizing.Fixed(110f)).Clip(new CornerRadii(30f, 4f, 30f, 4f));
            var tilted = rounded.Child("Tilted").Sized(Sizing.Fixed(200f), Sizing.Fixed(60f)).Center();
            var tiltedBox = tilted.Box(Palette[2]);
            tiltedBox.Gradient.Value = true;
            tiltedBox.GradientTo.Value = Palette[5];
            tilted.Rotation.Value = -25f;
            tilted.Offset.Value = new Vector2(-25f, 25f);
            tilted.Child("Text").Label("Clipped", 18f, Color.white, bold: true);

            var stack = Case(body, "ZIndex (tap a card)");
            stack.Height.Value = Sizing.Fixed(170f);
            var cards = new List<Pressable>();
            for (int i = 0; i < 3; i++)
            {
                var card = stack.Child("Card " + (i + 1)).Sized(Sizing.Fixed(170f), Sizing.Fixed(110f)).Pad(14f)
                    .Float(AttachPoint.LeftTop, AttachPoint.LeftTop, new Vector2(i * 70f, i * 28f));
                DemoKit.Shadow(card.Box(Palette[i], 18f), 0.18f, 18f, 6f);
                card.Child("Title").Label("Card " + (i + 1), 18f, Color.white, bold: true);
                cards.Add(card.Pressable());
            }

            var fading = Panel(Case(body, "Fading as one image (group opacity)").Row(24f), 16f);
            foreach (var (title, group) in new[] { ("Each part", false), ("As one", true) })
            {
                var pair = fading.Child(title).Sized(Sizing.Fixed(140f), Sizing.Fixed(130f));
                pair.Opacity.Value = 0.5f;
                pair.GroupOpacity.Value = group;
                pair.Child("Back").Sized(Sizing.Fixed(90f), Sizing.Fixed(90f)).Box(Palette[1], 16f);
                var front = pair.Child("Front").Sized(Sizing.Fixed(90f), Sizing.Fixed(90f))
                    .Float(AttachPoint.LeftTop, AttachPoint.LeftTop, new Vector2(45f, 40f)).Center();
                front.Box(Palette[5], 16f);
                front.Child("Title").Label(title, 14f, Color.white, bold: true);
            }

            var lift = Case(body, "Top layer").Column(14f);
            var clipped = lift.Child("Clipped Box").Sized(Sizing.Grow(), Sizing.Fixed(90f)).Pad(14f).Align(AlignX.Center, AlignY.Bottom);
            clipped.Box(Card, 14f);
            clipped.Clip(14f);
            clipped.Child("Text").Label("This box clips what is in it", 14f, Secondary);
            var tooltip = clipped.Child("Tooltip").Pad(12f, 8f).Float(AttachPoint.CenterBottom, AttachPoint.CenterTop, new Vector2(0f, 17f));
            DemoKit.Shadow(tooltip.Box(Text, 10f), 0.25f, 10f, 3f);
            tooltip.Child("Text").Label("A tooltip half outside it", 14f, Color.white, bold: true);
            var liftSwitch = Switch(lift, "Lift it into the top layer");

            page.Add<OrderDemo>().Setup(cards, liftSwitch, tooltip);
        }

        private static void BindingsPage(UINode page, UINode body)
        {
            var panel = Panel(Case(body, "One value, bound everywhere").Column(14f), 18f).Align(AlignX.Center, AlignY.Top);
            panel.Width.Value = Sizing.Grow();
            var top = panel.Child("Top").Row(14f).Align(AlignX.Left, AlignY.Center);
            var badge = top.Child("Badge").Sized(Sizing.Fixed(64f), Sizing.Fixed(64f)).Center();
            var badgeBox = badge.Box(Accent, 32f);
            var count = badge.Child("Count").Label("3", 28f, Color.white, bold: true);
            var level = top.Child("Level").Label("Low", 22f, Text, bold: true);

            var track = panel.Child("Track").Sized(Sizing.Grow(), Sizing.Fixed(14f));
            track.Box(new Color(0.47f, 0.47f, 0.5f, 0.2f), 7f);
            var bar = track.Child("Bar").Sized(Sizing.Percent(0.3f), Sizing.Grow());
            bar.Box(Accent, 7f);

            var buttons = panel.Child("Buttons").Row(12f);
            var minus = Button(buttons, "−", Accent, Color.white);
            minus.Node.Width.Value = Sizing.Fixed(80f);
            var plus = Button(buttons, "+", Accent, Color.white);
            plus.Node.Width.Value = Sizing.Fixed(80f);

            page.Add<BindingsDemo>().Setup(minus, plus, count, level, bar, badgeBox);
        }
    }
}
