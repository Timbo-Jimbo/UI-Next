using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Circle;
using TimboJimbo.UINext.Demos.Showcase;
using TimboJimbo.UINext.Text;
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

        // Photos on a CDN, for the Images page.
        private static readonly string[] WebPhotos =
        {
            "https://cdn.staticshiftracing.com/build-share-dev/0b0ec06f-bdae-4576-b2f2-a1f160a965cc.jpeg",
            "https://cdn.staticshiftracing.com/build-share-dev/e182026f-c3e8-4d0c-aaec-31b56eb608fa.jpeg",
            "https://cdn.staticshiftracing.com/build-share-dev/f84c08c0-d8bd-46ab-b3c2-720c3c72301a.jpeg",
        };
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
            Add("📝", "Rich text", RichTextPage);
            Add("🖼️", "Images", ImagesPage);
            Add("📜", "Scrolling", ScrollPage);
            Add("🪟", "Presentations", PresentPage);
            Add("✂️", "Clips and order", OrderPage);
            Add("🔗", "Bindings", BindingsPage);
            Add("🛍️", "Store", StorePage);
            Add("📰", "App Store", AppStorePage);
            Add("🎯", "Focus", FocusPage);
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
            // It shows focus by its highlight, as a list's row does, rather than by the ring.
            row.FocusRing.Value = false;
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
            // A swipe right takes the page back off, carrying on at the swipe's speed as it slides out.
            content.Add<Draggable>().Axis = ScrollAxis.Horizontal;
            content.Add<SwipeToDismiss>().Setup(page, SwipeDirections.Right);
            var header = content.Child("Header").Row(8f).Pad(new Insets(8f, 16f, 10f, 10f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            header.Box(Card);
            var back = header.Child("Back").Pad(8f, 4f);
            back.Box(Color.clear, 8f).Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.15f));
            back.FocusRing.Value = false;
            back.Child("Title").Label("‹ Back", 17f, Accent);
            var title_ = header.Child("Title").Label(title, 17f, Text, bold: true);
            title_.Node.Width.Value = Sizing.Grow();
            var body = content.Child("Body").Sized(Sizing.Grow(), Sizing.Grow()).Column(22f).Pad(new Insets(16f, 16f, 16f, 40f));
            body.Scroll();
            // Room past what it holds for the keyboard, so a field near its end can scroll up above it.
            body.AvoidsKeyboard.Value = true;
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

            // Two stepped sliders, alike but for whether their drag hands its speed on.
            var speedCase = Case(body, "Momentum or precision (flick a knob)").Column(10f);
            var knobs = new List<Draggable>();
            foreach (var (name, tracksVelocity) in new[] { ("Momentum", true), ("Precise", false) })
            {
                speedCase.Child(name).Label(name, 13f, Secondary);
                var track = speedCase.Child(name + " Slider").Sized(Sizing.Grow(), Sizing.Fixed(36f)).Pad(4f).Align(AlignX.Left, AlignY.Center);
                track.Box(Card, 18f);
                // Five steps, marked where the knob's middle comes to rest: over the whole track, padded as it is.
                var marks = track.Child("Steps").Row().Pad(4f).Float(AttachPoint.LeftTop, AttachPoint.LeftTop);
                marks.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
                for (int i = 0; i < 5; i++)
                {
                    var step = marks.Child("Step").Sized(Sizing.Percent(0.2f), Sizing.Grow()).Center();
                    step.Child("Mark").Sized(Sizing.Fixed(6f), Sizing.Fixed(6f)).Box(new Color(0.47f, 0.47f, 0.5f, 0.3f), 3f);
                }
                var knob = track.Child("Knob").Sized(Sizing.Percent(0.2f), Sizing.Fixed(28f));
                DemoKit.Shadow(knob.Box(Accent, 14f), 0.2f, 4f, 2f);
                var drag = knob.Add<Draggable>();
                drag.Axis = ScrollAxis.Horizontal;
                drag.TracksVelocity = tracksVelocity;
                knobs.Add(drag);
            }
            page.Add<MomentumDemo>().Setup(knobs, 5);

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
            // It animates itself: quickly on, slowly off.
            warning.Enter = UIAnimation.Snappy;
            warning.Exit = new UIAnimation(0.8f);
            EditorUtility.SetDirty(warning);
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

            var holdDemo = Case(body, "Tap or long press");
            var hold = holdDemo.Child("Tile").Pad(18f, 14f).Center();
            hold.Width.Value = Sizing.Grow();
            var holdBox = hold.Box(Card, 14f);
            holdBox.Fill.When(InteractionStates.Pressed, new Color(0.9f, 0.9f, 0.93f));
            var holdLabel = hold.Child("Text").Label("Hold me", 17f, Text, bold: true);

            page.Add<StatesDemo>().Setup(button, disable, segments, pill, warn, panel, warning, hold.Pressable(), holdLabel);
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

#if UINEXT_TEXTINPUT
            var fields = Panel(Case(body, "Text fields (tap one)").Column(10f), 14f);
            Field(fields, "Name", "Your name");
            var password = Field(fields, "Password", "Password");
            var secure = password.Config;
            secure.ContentType = TimboJimbo.UINext.TextInput.TextContentType.Password;
            password.Config = secure;
            Field(fields, "Note", "A note that grows to four lines", multiline: true, maxLines: 4);
#endif
        }

        // Rich text: a label's text read with its markup, the built-in Markdown unless it has one of its own: inline styles
        // and links, blocks, and handlers of the page's own, saying what was tapped (RichTextDemo).
        private static void RichTextPage(UINode page, UINode body)
        {
            var inline = Panel(Case(body, "Markdown").Column(6f), 14f);
            var markdown = inline.Child("Text").Label("Text is Markdown: **bold**, *italic*, __underline__, ~~strike~~, `code`, and [links](https://unity.com) (tap one).", 16f, Text);
            markdown.Node.Width.Value = Sizing.Grow();
            markdown.gameObject.AddComponent<OpenLinks>();

            var blocks = Panel(Case(body, "Blocks").Column(6f), 14f);
            var block = blocks.Child("Text").Label("### A heading\n> A quote, read as **Markdown** too\n- A list item\n- One long enough to wrap, hanging clear of its bullet\n```\nvar answer = 42;\n```", 16f, Text);
            block.Node.Width.Value = Sizing.Grow();

            var own = Panel(Case(body, "Handlers of its own").Column(10f), 14f);
            var chat = own.Child("Text").Label("Thanks @timbo :heart: It ships ==today==, notes in #design :star:", 16f, Text);
            chat.Node.Width.Value = Sizing.Grow();
            chat.Markup.Value = RichTextMarkup();
            var tapped = own.Child("Tapped").Label("Tap the mention or the tag.", 13f, Secondary);
            page.Add<RichTextDemo>().Setup(chat, tapped);
        }

#if UINEXT_TEXTINPUT
        // A field on a soft grey ground whose border lights up while it is typed into.
        private static TextField Field(UINode parent, string name, string placeholder, bool multiline = false, int maxLines = 5)
        {
            var node = parent.Child(name);
            node.Width.Value = Sizing.Grow();
            var field = node.TextField(placeholder, 16f, multiline, maxLines, Insets.Symmetric(12f, 10f));
            node.ClipRadii.Value = 10f;
            var box = node.Box(new Color(0.47f, 0.47f, 0.5f, 0.1f), 10f);
            box.BorderWidth.Value = 1.5f;
            box.BorderColor.Value = new Color(Accent.r, Accent.g, Accent.b, 0f);
            box.BorderColor.When(InteractionStates.Focused, Accent);
            // Its border shows focus, rather than the ring.
            node.FocusRing.Value = false;
            field.Color.Value = Text;
            field.PlaceholderColor.Value = new Color(Secondary.r, Secondary.g, Secondary.b, 0.6f);
            field.CaretColor.Value = Accent;
            field.SelectionColor.Value = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            return field;
        }
#endif

        // Images: one small sprite nine-sliced to sizes of every shape, its corners kept as it stretches, and photos loaded
        // from the web as small copies, saying so while they load; a tap opens one full screen, where a larger copy loads
        // over it (ImagesDemo).
        private static void ImagesPage(UINode page, UINode body)
        {
            var frame = SlicedSprite("Frame", 48, 16, Frame);
            var sliced = Panel(Case(body, "One sprite, sliced to any size").Row(10f), 14f);
            foreach (var (name, width, height) in new[] { ("Small", Sizing.Fixed(48f), 48f), ("Wide", Sizing.Fixed(120f), 48f), ("Large", Sizing.Grow(), 110f) })
                sliced.Child(name).Sized(width, Sizing.Fixed(height)).Image(frame, ImageFit.Sliced).Tint.Value = Accent;

            var web = Panel(Case(body, "From the web (tap one)").Column(12f), 14f);
            var row = web.Child("Photos").Row(10f);
            row.Width.Value = Sizing.Grow();
            var photos = new List<Pressable>();
            for (int i = 0; i < WebPhotos.Length; i++)
            {
                var photo = row.Child("Photo " + (i + 1)).Center();
                photo.Width.Value = Sizing.Grow();
                photo.AspectRatio.Value = 4f / 3f;
                photo.MatchName.Value = "Web photo";
                photo.MatchId.Value = i.ToString();
                photo.Box(Background, 10f);
                photo.Image(ImagesDemo.Small(WebPhotos[i])).CornerRadii.Value = 10f;
                LoadingNote(photo);
                photos.Add(photo.Pressable());
                photo.Scale.When(InteractionStates.Pressed, 0.96f);
            }
            var clear = Button(web, "Clear the cache", Accent, Color.white);

            // Full screen: the photo across the width over a black backdrop, its small copy at once and a large one loading
            // over it, closed by a tap or back.
            var app = page.transform.parent.GetComponent<UINode>();
            var host = app.Child("Photo Viewer").Column().Center();
            var viewer = host.Add<Presentation>();
            viewer.Transition = PresentationTransition.None;
            viewer.Dim.Value = Color.clear;
            viewer.DismissOnBackdrop = false;
            var backdrop = host.Child("Backdrop").Float(AttachPoint.LeftTop, AttachPoint.LeftTop);
            backdrop.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            backdrop.IgnoresSafeArea.Value = Edges.All;
            backdrop.Box(Color.black);
            backdrop.Opacity.Value = 0f;
            backdrop.Focusable.Value = Focusability.Never;
            backdrop.Pressable();
            var shown = host.Child("Photo");
            shown.Width.Value = Sizing.Grow();
            shown.AspectRatio.Value = 4f / 3f;
            shown.MatchName.Value = "Web photo";
            var small = shown.Image("");
            var large = shown.Child("Large").Float(AttachPoint.LeftTop, AttachPoint.LeftTop).Center();
            large.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            var largeImage = large.Image("");
            LoadingNote(large);
            shown.Pressable();

            page.Add<ImagesDemo>().Setup(photos, new List<string>(WebPhotos), clear, viewer, backdrop, shown, small, largeImage);
        }

        // A "Loading…" pill in the middle of an image's node, there only while its image loads.
        private static void LoadingNote(UINode node)
        {
            var note = node.Child("Loading").Pad(10f, 4f).Center();
            note.Box(new Color(0f, 0f, 0f, 0.45f), 999f);
            note.Child("Text").Label("Loading…", 12f, Color.white);
            note.Opacity.Value = 0f;
            note.Opacity.When(UIStates.Loading, 1f);
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

            var listDemo = Case(body, "A list in a box (at its end, the drag goes on to the page)");
            var listBox = listDemo.Child("List").Column().Sized(Sizing.Grow(), Sizing.Fixed(300f));
            listBox.Box(Card, 14f);
            listBox.Clip(14f);
            listBox.Scroll();
            for (int i = 0; i < 30; i++)
            {
                var row = listBox.Child("Row").Pad(16f, 12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                var rowBox = row.Box(Color.clear);
                rowBox.Fill.When(InteractionStates.Pressed, Pressed);
                rowBox.Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
                row.FocusRing.Value = false;
                row.Child("Text").Label("Row " + (i + 1), 16f, Text);
                row.Pressable();
            }

            var longDemo = Case(body, "Ten thousand rows (made as they scroll in)");
            var longBox = longDemo.Child("Long list").Sized(Sizing.Grow(), Sizing.Fixed(300f));
            longBox.Box(Card, 14f);
            longBox.Clip(14f);
            longBox.Scroll();
            var longList = longBox.Add<ListView>();
            longList.EstimatedRowHeight = 52f;
            page.Add<LongListDemo>().Setup(longList, Text, Palette);
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
            sheetCard.Add<Draggable>().Axis = ScrollAxis.Vertical;
            sheetCard.Add<SwipeToDismiss>().Setup(sheet, SwipeDirections.Down);
            sheetCard.Child("Title").Label("A sheet", 22f, Text, bold: true);
            var sheetBody = sheetCard.Child("Body").Label("Swipe it down, tap the dim or press back to dismiss it, or present an alert over it.", 15f, Secondary);
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

            // Masks: a row that fades out at its edges as it scrolls, and a photo cut to a star.
            var masks = Case(body, "Masks (a texture's alpha cuts it)").Row(16f).Align(AlignX.Left, AlignY.Center);
            var faded = masks.Child("Faded edges").Row(10f).Sized(Sizing.Grow(), Sizing.Fixed(90f));
            faded.Scroll(ScrollAxis.Horizontal);
            faded.CompositeGroup.Value = true;
            faded.Mask.Value = MaskTexture("Edge Fade", 256, 4, (x, y) => Mathf.Clamp01(Mathf.Min(x, 1f - x) / 0.15f));
            for (int i = 0; i < 12; i++)
            {
                var tile = faded.Child("Tile").Sized(Sizing.Fixed(90f), Sizing.Fixed(90f)).Center();
                tile.Box(Palette[i % Palette.Length], 18f);
                tile.Child("Number").Label((i + 1).ToString(), 22f, Color.white, bold: true);
            }
            var star = masks.Child("Star").Sized(Sizing.Fixed(100f), Sizing.Fixed(100f));
            star.Add<ScenePhoto>().Show(1);
            star.CompositeGroup.Value = true;
            star.Mask.Value = MaskTexture("Star", 256, 256, Star);

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

            var fading = Panel(Case(body, "Fading as one image (composite group)").Row(24f), 16f);
            foreach (var (title, group) in new[] { ("Each part", false), ("As one", true) })
            {
                var pair = fading.Child(title).Sized(Sizing.Fixed(140f), Sizing.Fixed(130f));
                pair.Opacity.Value = 0.5f;
                pair.CompositeGroup.Value = group;
                pair.Child("Back").Sized(Sizing.Fixed(90f), Sizing.Fixed(90f)).Box(Palette[1], 16f);
                var front = pair.Child("Front").Sized(Sizing.Fixed(90f), Sizing.Fixed(90f))
                    .Float(AttachPoint.LeftTop, AttachPoint.LeftTop, new Vector2(45f, 40f)).Center();
                front.Box(Palette[5], 16f);
                front.Child("Title").Label(title, 14f, Color.white, bold: true);
            }

            var blurCase = Case(body, "Blur (tap it)");
            var blurred = blurCase.Child("Card").Column(6f).Pad(18f);
            blurred.Width.Value = Sizing.Grow();
            var blurBox = blurred.Box(Palette[4], 16f);
            blurBox.Gradient.Value = true;
            blurBox.GradientTo.Value = Palette[1];
            blurred.Child("Title").Label("In and out of focus", 20f, Color.white, bold: true);
            blurred.Child("Body").Label("Everything inside it blurs with it.", 15f, Color.white);
            blurred.Pressable();
            blurred.CompositeGroup.Value = true;
            blurred.Add<BlurToggle>().Setup(blurred, 10f);

            var lockCase = Panel(Case(body, "Locked: grey and dim (tap them)").Row(14f), 16f);
            foreach (var (emoji, colour) in new[] { ("🗡️", Palette[5]), ("🛡️", Palette[1]), ("🏹", Palette[3]) })
            {
                var item = lockCase.Child(emoji).Sized(Sizing.Fixed(84f), Sizing.Fixed(84f)).Center();
                var itemBox = item.Box(colour, 18f);
                itemBox.Gradient.Value = true;
                itemBox.GradientTo.Value = Color.Lerp(colour, Color.black, 0.25f);
                item.Child("Icon").Label(emoji, 40f);
                item.Pressable();
                item.Add<LockToggle>();
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

        // The Rich text page's markup: @mentions as chips and #tags as pills, both tapped, emoji of its own, and
        // ==highlights==, before the built-in Markdown. Made once as an asset beside its scene, with its prefabs and emoji.
        private static Markup RichTextMarkup()
        {
            string path = Folder + "/Rich Text Markup.asset";
            var markup = AssetDatabase.LoadAssetAtPath<Markup>(path);
            if (markup != null) return markup;
            // The theme a highlight was, before text handlers had prefabs.
            AssetDatabase.DeleteAsset(Folder + "/Highlight Theme.asset");

            var mention = ElementHandler.Create(TextTrigger.Prefix("@"), tappable: true);
            mention.name = "Mention";
            var tag = SpanHandler.Create(TextTrigger.Prefix("#"), color: Accent, background: Wash("Tag", Accent, 0.12f),
                padding: new Vector2(0.2f, 0.06f), tappable: true);
            tag.name = "Tag";
            var emoji = ImageHandler.Create(TextTrigger.Wrapped(":"), new[]
            {
                new NamedImage("heart", Emoji("Heart", new Color(1f, 0.33f, 0.4f), Heart)),
                new NamedImage("star", Emoji("Star", new Color(1f, 0.74f, 0.2f), (u, v) => Star(u, v) > 0.5f)),
            });
            emoji.name = "Emoji";
            var highlight = SpanHandler.Create(TextTrigger.Wrapped("=="), background: Wash("Highlight", new Color(1f, 0.8f, 0.15f), 0.45f),
                padding: new Vector2(0.1f, 0.05f));
            highlight.name = "Highlight";

            markup = Markup.Create(new TextRule[] { mention, tag, emoji, highlight });
            AssetDatabase.CreateAsset(markup, path);
            foreach (var handler in markup.Handlers)
                AssetDatabase.AddObjectToAsset(handler, markup);
            AssetDatabase.SaveAssets();
            return markup;
        }

        // A rounded box in a colour at an opacity, twice as strong held down, saved as a prefab beside the scene: what a
        // span handler puts behind its text.
        private static GameObject Wash(string name, Color colour, float opacity)
        {
            string path = Folder + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var node = UIBuild.Node(name);
            var box = node.Box(new Color(colour.r, colour.g, colour.b, opacity), CornerRadii.All(5f));
            box.Fill.When(InteractionStates.Pressed, new Color(colour.r, colour.g, colour.b, Mathf.Min(1f, opacity * 2f)));
            var prefab = PrefabUtility.SaveAsPrefabAsset(node.gameObject, path);
            Object.DestroyImmediate(node.gameObject);
            return prefab;
        }

        // An emoji of the showcase's own, made once as a sprite beside its scene ("Emoji Heart.png"): a shape in one colour,
        // `inside` saying whether a point is in it (0 to 1 each way, y up), smoothed by sampling each pixel sixteen times.
        // Whatever is at its path already is kept, a sprite or not.
        private static Sprite Emoji(string name, Color colour, System.Func<float, float, bool> inside)
        {
            string path = Folder + "/Emoji " + name + ".png";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            const int size = 64, samples = 4;
            var pixels = new Color32[size * size];
            Color32 fill = colour;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                            if (inside((x + (sx + 0.5f) / samples) / size, (y + (sy + 0.5f) / samples) / size))
                                hits++;
                    fill.a = (byte)Mathf.RoundToInt(255f * hits / (samples * samples));
                    pixels[y * size + x] = fill;
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(FileUtil.GetPhysicalPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Whether a point (0 to 1 each way, y up) is inside a heart filling the square: the curve (x² + y² - 1)³ = x²y³.
        private static bool Heart(float u, float v)
        {
            float x = (u - 0.5f) * 2.6f, y = (v - 0.42f) * 2.6f;
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        // A sprite for the showcase to nine-slice, made once as a PNG beside its scene: white (tinted where it is drawn),
        // `size` pixels square, its alpha from `alpha` (0 to 1 each way, y up) averaged over sixteen points a pixel, and
        // `border` pixels in from each edge kept at their size as it stretches.
        private static Sprite SlicedSprite(string name, int size, int border, System.Func<float, float, float> alpha)
        {
            string path = Folder + "/" + name + ".png";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            const int samples = 4;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                            sum += Mathf.Clamp01(alpha((x + (sx + 0.5f) / samples) / size, (y + (sy + 0.5f) / samples) / size));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * sum / (samples * samples)));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(FileUtil.GetPhysicalPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.one * border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // The Images page's frame (0 to 1 each way): a square with corners 14 of its 48 pixels round, a ring 3 pixels wide
        // round its edge, and a faint fill inside.
        private static float Frame(float u, float v)
        {
            const float size = 48f, radius = 14f, ring = 3f;
            var q = new Vector2(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * size - Vector2.one * (size * 0.5f - radius);
            float distance = Mathf.Min(Mathf.Max(q.x, q.y), 0f) + Vector2.Max(q, Vector2.zero).magnitude - radius;
            return distance > 0f ? 0f : distance > -ring ? 1f : 0.2f;
        }

        // A mask texture for the showcase, made once as a PNG beside its scene: white, its alpha from `alpha` at each
        // pixel's centre (0 to 1 across, 0 to 1 up).
        private static Texture2D MaskTexture(string name, int width, int height, System.Func<float, float, float> alpha)
        {
            string path = Folder + "/" + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height))));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(FileUtil.GetPhysicalPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // A five-pointed star's coverage at a point (0 to 1 each way), smoothed over about a texel at 256.
        private static float Star(float x, float y)
        {
            var p = new Vector2(x - 0.5f, y - 0.5f);
            float angle = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
            float sector = Mathf.PI * 2f / 5f;
            float a = Mathf.Repeat(angle, sector) - sector / 2f;
            // The star's edge: between its points (radius 0.48) and its notches (0.2), straight between them.
            float outer = 0.48f, inner = 0.2f;
            var tip = new Vector2(outer, 0f);
            var notch = new Vector2(Mathf.Cos(sector / 2f), Mathf.Sin(sector / 2f)) * inner;
            var q = new Vector2(Mathf.Cos(a), Mathf.Abs(Mathf.Sin(a))) * p.magnitude;
            var edge = notch - tip;
            float side = (edge.x * (q.y - tip.y) - edge.y * (q.x - tip.x)) / edge.magnitude;
            return Mathf.Clamp01(0.5f + side * 256f / 1.5f);
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

        // Focus: keyboard and gamepad focus to try (the arrows, Tab, Enter, Escape; a gamepad's stick, A and B), an idea a
        // case, with a readout under the page of where it is (FocusDemo).
        private static void FocusPage(UINode page, UINode body)
        {
            // The tree is not built yet while building: the app is the page's transform parent.
            var app = page.transform.parent.GetComponent<UINode>();

            // A grid: the arrows go to the nearest that way; Tab goes in reading order, along rows the way the switch says.
            var arrows = Case(body, "Arrows and Tab").Column(12f);
            var grid = Panel(arrows.Child("Grid").Column(10f), 14f);
            grid.Width.Value = Sizing.Grow();
            for (int row = 0; row < 3; row++)
            {
                var line = grid.Child("Row " + (row + 1)).Row(10f);
                line.Width.Value = Sizing.Grow();
                for (int column = 0; column < 3; column++)
                    Button(line, (row * 3 + column + 1).ToString(), Accent, Color.white).Node.Width.Value = Sizing.Grow();
            }
            var rightToLeft = Switch(arrows, "Rows read right to left");

            // Two sections side by side, each lit while focus is in it: a move across goes into the other as one, back to
            // where focus last was in it.
            var sections = Case(body, "Sections: back where you left").Row(12f).Align(AlignX.Left, AlignY.Top);
            var mailboxes = FocusSection(sections, "Mailboxes").Column(8f);
            foreach (var title in new[] { "Inbox", "Sent", "Drafts", "Archive" })
                Button(mailboxes, title, new Color(0.47f, 0.47f, 0.5f, 0.12f), Text).Node.Width.Value = Sizing.Grow();
            var mail = FocusSection(sections, "Mail").Column(10f);
            mail.Width.Value = Sizing.Grow();
            var actions = new[] { "Reply", "Forward", "Flag", "Delete" };
            for (int row = 0; row < 2; row++)
            {
                var line = mail.Child("Row " + (row + 1)).Row(10f);
                line.Width.Value = Sizing.Grow();
                for (int column = 0; column < 2; column++)
                    Button(line, actions[row * 2 + column], Palette[row * 2 + column], Color.white).Node.Width.Value = Sizing.Grow();
            }

            // A group: one stop, ringed as one, until Enter goes in; inside, focus stays until Escape comes out.
            var toolbar = Panel(Case(body, "A group: Enter goes in, Escape comes out").Row(10f), 10f);
            toolbar.FocusScope.Value = FocusScope.Group;
            toolbar.GetComponent<Box>().Fill.When(InteractionStates.FocusWithin, Lit);
            foreach (var title in new[] { "Bold", "Italic", "Underline" })
            {
                var tool = Button(toolbar, title, new Color(0.47f, 0.47f, 0.5f, 0.12f), Text);
                tool.Node.Width.Value = Sizing.Grow();
                tool.Toggles = true;
                tool.GetComponent<Box>().Fill.When(InteractionStates.Selected, new Color(Accent.r, Accent.g, Accent.b, 0.25f));
            }

            // A control that takes its own arrows: left and right step the slider, and at its end the ring shakes no, as it
            // does wherever a key finds nowhere to go.
            var slider = Panel(Case(body, "A slider: left and right are its own").Row(10f), 14f).Align(AlignX.Left, AlignY.Center);
            DemoKit.Slider(slider, 0.5f, Accent);

            // Pressed, a button goes, and focus goes from it to the nearest, until they are brought back.
            var lost = Case(body, "Press one: it goes, and focus goes to the nearest").Column(12f);
            var goersRow = Panel(lost.Child("Buttons").Row(10f), 14f);
            goersRow.Width.Value = Sizing.Grow();
            var goers = new List<Pressable>();
            for (int i = 0; i < 4; i++)
            {
                var goer = Button(goersRow, ((char)('A' + i)).ToString(), Palette[i], Color.white);
                goer.Node.Width.Value = Sizing.Grow();
                goers.Add(goer);
            }
            var bringBack = Button(lost, "Bring them back", new Color(0.47f, 0.47f, 0.5f, 0.15f), Text);
            bringBack.Node.Width.Value = Sizing.Grow();

            // A row wider than the page: focus going along it scrolls it, with room for the ring (and room above and
            // below it, which the row's clip would cut).
            var strip = Case(body, "A row wider than the page").Row(10f).Pad(6f);
            strip.Scroll(ScrollAxis.Horizontal).ShowsIndicators.Value = false;
            for (int i = 0; i < 12; i++)
                Button(strip, ((char)('A' + i)).ToString(), Palette[i % Palette.Length], Color.white);

            // A dialog: focus goes into it as it comes up, to what prefers it, stays inside, and comes back to Open as it goes.
            var dialogCase = Panel(Case(body, "A dialog: focus starts on OK, and comes back").Column(10f), 14f);
            var open = Button(dialogCase, "Open a dialog", Accent, Color.white);
            open.Node.Width.Value = Sizing.Grow();
            var dialogHost = app.Child("Focus Dialog").Center();
            var dialog = dialogHost.Add<Presentation>();
            dialog.Transition = PresentationTransition.Pop;
            dialog.Animation = new UIAnimation(0.35f, 0.15f);
            var card = dialogHost.Child("Card").Column(14f).Pad(22f).Align(AlignX.Center, AlignY.Top);
            card.Width.Value = Sizing.Fixed(290f);
            DemoKit.Shadow(card.Box(Card, 20f), 0.2f, 30f, 10f);
            card.Child("Title").Label("A dialog", 18f, Text, bold: true);
            var dialogBody = card.Child("Body").Label("Focus stays in here while it is up.", 14f, Secondary);
            dialogBody.Alignment.Value = TextAnchor.UpperCenter;
            var choices = card.Child("Choices").Row(10f);
            choices.Width.Value = Sizing.Grow();
            var cancel = Button(choices, "Cancel", new Color(0.47f, 0.47f, 0.5f, 0.15f), Text);
            cancel.Node.Width.Value = Sizing.Grow();
            var ok = Button(choices, "OK", Accent, Color.white);
            ok.Node.Width.Value = Sizing.Grow();
            ok.Node.PrefersFocus.Value = true;
            page.Add<PresentDemo>().Setup(new List<Pressable> { open }, new List<Presentation> { dialog },
                new List<Pressable> { cancel, ok }, new List<Presentation> { dialog, dialog });

#if UINEXT_TEXTINPUT
            // A field: while it is typed into, the arrows and Enter are its own; Tab moves on.
            var typing = Panel(Case(body, "Typing: Tab moves on").Column(10f), 14f);
            Field(typing, "Field", "Type here, then Tab on");
#endif

            // Under the page, out of its scroll: where focus is, and whether it shows.
            var content = body.transform.parent.GetComponent<UINode>();
            var readout = content.Child("Readout").Column(4f).Pad(new Insets(16f, 16f, 12f, 30f));
            readout.Width.Value = Sizing.Grow();
            readout.Box(Card);
            var focused = readout.Child("Focused").Label("Focus: none", 16f, Text, bold: true);
            var state = readout.Child("State").Label("Nothing has focus", 13f, Secondary);
            focused.Node.Width.Value = Sizing.Grow();
            state.Node.Width.Value = Sizing.Grow();
            page.Add<FocusDemo>().Setup(body, focused, state, rightToLeft, goers, bringBack);
        }

        // A light tint for what focus is within.
        private static readonly Color Lit = new Color32(0xE6, 0xEF, 0xFE, 0xFF);

        // A panel that is a section to focus: focus moves in and out of it, coming back to where it last was, and it is lit
        // while focus is in it.
        private static UINode FocusSection(UINode parent, string name)
        {
            var section = Panel(parent.Child(name), 10f);
            section.FocusScope.Value = FocusScope.Section;
            section.FocusEntry.Value = FocusEntry.Remembered;
            section.GetComponent<Box>().Fill.When(InteractionStates.FocusWithin, Lit);
            return section;
        }

        private static void StorePage(UINode page, UINode body)
        {
            // A grid of items; each grows into a dialog and flies home. An item and the dialog's card are matched by name
            // and the item's index, and so are their icons, names and prices, which fly on their own.
            var grid = Case(body, "Tap an item (drag it away to close)").Row(12f);
            grid.Wrap.Value = Wrap.Grid(2);
            var items = new List<Pressable>();
            for (int i = 0; i < StoreDemo.Items.Length; i++)
            {
                var (emoji, name, price, colour) = StoreDemo.Items[i];
                var item = grid.Child(name).Column(8f).Pad(14f).Align(AlignX.Center, AlignY.Top);
                item.MatchName.Value = "Item";
                item.MatchId.Value = i.ToString();
                item.ClipRadii.Value = 18f;
                item.Box(Card, 18f);
                var icon = item.Child("Icon").Sized(Sizing.Fixed(72f), Sizing.Fixed(72f)).Center();
                icon.MatchName.Value = "Icon";
                icon.ClipRadii.Value = 20f;
                icon.Box(colour, 20f);
                icon.Child("Emoji").Label(emoji, 36f);
                item.Child("Name").Label(name, 15f, Text, bold: true).Node.MatchName.Value = "Name";
                item.Child("Price").Label(price, 13f, Secondary).Node.MatchName.Value = "Price";
                items.Add(item.Pressable());
                item.Scale.When(InteractionStates.Pressed, 0.96f);
            }

            // The dialog: its card takes the id of the item it opens from.
            var app = page.transform.parent.GetComponent<UINode>();
            var host = app.Child("Store Dialog").Center();
            var dialog = host.Add<Presentation>();
            dialog.Transition = PresentationTransition.None;
            dialog.Animation = new UIAnimation(0.45f, 0.1f);
            var card = host.Child("Card").Column(12f).Pad(24f).Align(AlignX.Center, AlignY.Top);
            card.Width.Value = Sizing.Fixed(300f);
            card.MatchName.Value = "Item";
            card.ClipRadii.Value = 26f;
            card.Box(Card, 26f);
            var bigIcon = card.Child("Icon").Sized(Sizing.Fixed(150f), Sizing.Fixed(150f)).Center();
            bigIcon.MatchName.Value = "Icon";
            bigIcon.ClipRadii.Value = 36f;
            var iconBox = bigIcon.Box(Accent, 36f);
            var emojiLabel = bigIcon.Child("Emoji").Label("", 76f);
            var nameLabel = card.Child("Name").Label("", 22f, Text, bold: true);
            nameLabel.Node.MatchName.Value = "Name";
            var priceLabel = card.Child("Price").Label("", 16f, Secondary);
            priceLabel.Node.MatchName.Value = "Price";
            var buy = Button(card, "Add to bag", Accent, Color.white);
            buy.Node.Width.Value = Sizing.Grow();
            var close = Button(card, "Close", new Color(0.47f, 0.47f, 0.5f, 0.15f), Text);
            close.Node.Width.Value = Sizing.Grow();

            // Thrown away by a drag anywhere in it, on the card or the dim around it, following the finger.
            host.Add<Draggable>().Axis = ScrollAxis.Both;
            host.Add<SwipeToDismiss>().Setup(dialog, SwipeDirections.Any, panel: card);

            page.Add<StoreDemo>().Setup(items, dialog, card, iconBox, emojiLabel, nameLabel, priceLabel, new List<Pressable> { buy, close });
        }

        private static void AppStorePage(UINode page, UINode body)
        {
            // Today cards; each opens into a full-screen story, matched to it by name and its index, its title flying into
            // the story's.
            var cards = new List<Pressable>();
            var today = Case(body, "Today (pull a story down to close it)").Column(20f);
            for (int i = 0; i < AppStoreDemo.Stories.Length; i++)
            {
                var (kicker, title, from, to) = AppStoreDemo.Stories[i];
                var card = today.Child("Card " + (i + 1)).Column(6f).Pad(22f);
                card.Sized(Sizing.Grow(), Sizing.Fixed(360f));
                card.MatchName.Value = "Today";
                card.MatchId.Value = i.ToString();
                // What is inside it hangs from its top as it opens into its story, whose art is at its top too.
                card.MatchAnchor.Value = new Vector2(0.5f, 0f);
                card.ClipRadii.Value = 22f;
                var art = card.Box(from, 22f);
                art.Gradient.Value = true;
                art.GradientTo.Value = to;
                DemoKit.Shadow(art, 0.15f, 20f, 8f);
                card.Child("Kicker").Label(kicker, 13f, new Color(1f, 1f, 1f, 0.8f), bold: true);
                var heading = card.Child("Title").Label(title, 28f, Color.white, bold: true);
                heading.Node.Width.Value = Sizing.Grow();
                heading.Node.MatchName.Value = "Title";
                cards.Add(card.Pressable());
                card.Scale.When(InteractionStates.Pressed, 0.97f);
            }

            // The story: a cover, its content taking the card's id as it opens. Pulled down past its top it shrinks in
            // place: its scroll view hands the drag to its swipe.
            var app = page.transform.parent.GetComponent<UINode>();
            var host = app.Child("Today Story").Column();
            var story = host.Add<Presentation>();
            story.Style = PresentationStyle.Cover;
            story.Transition = PresentationTransition.None;
            story.Dim.Value = Color.clear;
            story.Animation = new UIAnimation(0.5f, 0.12f);
            // It clips what is in it to its corners, which round as it shrinks.
            var content = host.Child("Content").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            content.IgnoresSafeArea.Value = Edges.All;
            content.MatchName.Value = "Today";
            content.MatchAnchor.Value = new Vector2(0.5f, 0f);
            content.Clips.Value = true;
            content.Add<Draggable>().Axis = ScrollAxis.Vertical;
            var swipe = content.Add<SwipeToDismiss>();
            swipe.Setup(story, SwipeDirections.Down, shrinks: true);
            var scroll = content.Child("Scroll").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            scroll.Box(Background);
            var storyScroll = scroll.Scroll();
            storyScroll.ShowsIndicators.Value = false;
            // At its top, the rest of a pull down goes to the swipe, which shrinks the story; pushed back up, it scrolls on.
            storyScroll.AtEnd.Value = ScrollEnd.PassOn;
            var hero = scroll.Child("Art").Column(6f).Pad(new Insets(22f, 22f, 72f, 22f));
            hero.Sized(Sizing.Grow(), Sizing.Fixed(460f));
            var heroArt = hero.Box(Palette[0]);
            heroArt.Gradient.Value = true;
            var heroKicker = hero.Child("Kicker").Label("", 13f, new Color(1f, 1f, 1f, 0.8f), bold: true);
            var heroTitle = hero.Child("Title").Label("", 34f, Color.white, bold: true);
            heroTitle.Node.Width.Value = Sizing.Grow();
            heroTitle.Node.MatchName.Value = "Title";
            var text = scroll.Child("Text").Column(14f).Pad(22f);
            text.Width.Value = Sizing.Grow();
            foreach (var paragraph in new[]
                     {
                         "Every story here grows out of its card, and closes back into it.",
                         "Pull it down from the top and it shrinks where it is, its corners rounding. Let go to close it, or turn back up and it scrolls on.",
                         "Its title flies on its own, from the card's place to the story's.",
                         "Closing, the list scrolls the card into view as the story lands in it.",
                     })
                text.Child("Paragraph").Label(paragraph, 17f, Text).Node.Width.Value = Sizing.Grow();
            var close = content.Child("Close").Sized(Sizing.Fixed(32f), Sizing.Fixed(32f)).Center()
                .Float(AttachPoint.RightTop, AttachPoint.RightTop, new Vector2(-16f, 56f));
            close.Box(new Color(0f, 0f, 0f, 0.35f), 16f);
            close.Child("Mark").Label("✕", 15f, Color.white, bold: true);

            page.Add<AppStoreDemo>().Setup(cards, body.GetComponent<ScrollView>(), story, content, heroArt, heroKicker, heroTitle,
                storyScroll, swipe, close.Pressable());
        }
    }
}
