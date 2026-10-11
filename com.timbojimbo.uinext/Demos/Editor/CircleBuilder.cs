using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Circle;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>
    /// Builds Circle, the demo app, with the screens the UI package's mobile demo has: three tabs (Home, Chats, Me) side by
    /// side under a floating tab bar they scroll clear of, and what they open, each coming out of what was tapped and going
    /// back into it: the photo viewer, the stories, a chat's conversation, a profile, the compose card, the reaction picker,
    /// the comments sheet, the share card, the settings page with a control of each kind, and the log out dialog. Keyboards
    /// and gamepads move focus through all of it. Its colours are tokens, with a Dark mode.
    /// </summary>
    public static partial class CircleBuilder
    {
        private const string Folder = DemoKit.DemosFolder + "/Circle";
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Circle.unity";

        private static readonly Color Accent = new Color32(0x34, 0x78, 0xF6, 0xFF);
        private static readonly Color Red = new Color32(0xFF, 0x3B, 0x30, 0xFF);
        private static readonly Color Green = new Color32(0x34, 0xC7, 0x59, 0xFF);

        // A press, over something clear or faint.
        private static readonly Color Pressed = new(0.5f, 0.5f, 0.55f, 0.2f);

        // The room the scroll views keep round what they bring into view (focus moving to it, a photo coming home), so it
        // lands the same whatever brings it.
        private static readonly Insets ScrollRoom = Insets.All(12f);

        // The tokens, made by Tokens().
        private static UIState s_dark;
        private static ColorToken s_background, s_card, s_text, s_secondary, s_subtle, s_bubble, s_bar;

        // What the pieces of the app are wired to, gathered as they are made.
        private sealed class Parts
        {
            public CircleApp App;
            public PhotoViewer Viewer;
            public ReactionPicker Picker;
            public Presentation Comments;

            // Home: the compose button, your post, the feed and its photos, the stories, each post's author, and the
            // comments buttons.
            public Pressable Compose;
            public UINode YourPost;
            public ScrollView Feed;
            public ScrollView Tray;
            public readonly List<ScenePhoto> Photos = new();
            public readonly List<string> Captions = new();
            public readonly List<Pressable> Stories = new();
            public readonly List<UINode> RingAvatars = new();
            public readonly List<Box> Rings = new();
            public readonly List<Pressable> Authors = new();
            public readonly List<UINode> AuthorAvatars = new();
            public readonly List<UINode> AuthorNames = new();
            public readonly List<Pressable> CommentButtons = new();

            // Me: Share profile, the settings row and its title, and your photos.
            public Pressable Share;
            public Pressable SettingsRow;
            public UINode SettingsTitle;
            public readonly List<ScenePhoto> MyPhotos = new();
        }

        [MenuItem("Tools/UI Next/Demos/Build Circle")]
        private static void BuildAndOpen() => Build(open: true);

        public static void Build(bool open) => DemoKit.BuildScene(ScenePath, _ => BuildScene(), open);

        private static void Tokens()
        {
            DemoKit.EnsureFolder(Folder);
            s_dark = DemoKit.State(Folder + "/Dark.asset", "Dark", StateKind.Mode);
            s_background = DemoKit.Token(Folder + "/Background.asset", new Color32(0xF2, 0xF2, 0xF7, 0xFF), s_dark, new Color32(0x0B, 0x0B, 0x0F, 0xFF));
            s_card = DemoKit.Token(Folder + "/Card.asset", Color.white, s_dark, new Color32(0x1C, 0x1C, 0x22, 0xFF));
            s_text = DemoKit.Token(Folder + "/Text.asset", new Color32(0x12, 0x12, 0x16, 0xFF), s_dark, new Color32(0xF2, 0xF2, 0xF7, 0xFF));
            s_secondary = DemoKit.Token(Folder + "/Secondary.asset", new Color32(0x6E, 0x6E, 0x78, 0xFF), s_dark, new Color32(0x9A, 0x9A, 0xA6, 0xFF));
            s_subtle = DemoKit.Token(Folder + "/Subtle.asset", new Color(0f, 0f, 0f, 0.05f), s_dark, new Color(1f, 1f, 1f, 0.08f));
            s_bubble = DemoKit.Token(Folder + "/Bubble.asset", new Color32(0xE9, 0xE9, 0xEF, 0xFF), s_dark, new Color32(0x2C, 0x2C, 0x34, 0xFF));
            s_bar = DemoKit.Token(Folder + "/Bar.asset", new Color(1f, 1f, 1f, 0.94f), s_dark, new Color(0.13f, 0.13f, 0.16f, 0.94f));
            AssetDatabase.SaveAssets();
        }

        private static void BuildScene()
        {
            Tokens();
            DemoKit.Camera(Color.black);
            var root = DemoKit.Root("Circle");
            var app = root.Node.Column();
            DemoKit.Fill(app, s_background);
            var parts = new Parts { App = app.Add<CircleApp>() };

            var pages = app.Child("Pages").Sized(Sizing.Grow(), Sizing.Grow()).Row();
            var pager = pages.Scroll(ScrollAxis.Horizontal);
            pager.Snap.Value = ScrollSnap.Pages;
            pager.ShowsIndicators.Value = false;

            // What the pages open into, made first, so the pages can be wired to them.
            parts.Viewer = Viewer(app);
            parts.Picker = Picker(app);
            parts.Comments = CommentsSheet(app);

            var homeScroll = Home(pages, parts);
            var chats = Chats(pages, out var chatsScroll, out var rows);
            var meScroll = Me(pages, parts);
            var tabs = TabBar(app, out var pill, out var bar);
            // The pages scroll clear of the tab bar, by reference, with a little room above it: what they bring into view
            // (a focused row, a photo coming home) lands clear of it, with room round it.
            foreach (var page in new[] { homeScroll, chatsScroll, meScroll })
            {
                page.ClearOfBottom.Add(bar);
                page.ClearMargin.Value = new Insets(0f, 0f, 0f, 12f);
                page.ScrollPadding.Value = ScrollRoom;
            }

            // What opens from what the pages hold, each drawn at its own level as it is presented.
            StoryScreen(app, parts);
            Conversation(app, rows, chats);
            Profile(app, parts);
            Compose(app, parts);
            Share(app, parts);
            Settings(app, parts, out var logOut);
            LogOutDialog(app, logOut);

            parts.App.Setup(tabs, pill, pager, new List<ScrollView> { homeScroll, chatsScroll, meScroll }, parts.Photos, parts.Captions,
                parts.MyPhotos, parts.Viewer, parts.CommentButtons, parts.Comments, s_dark);
        }

        private static Color Darker(Color colour) => new(colour.r * 0.8f, colour.g * 0.8f, colour.b * 0.8f, colour.a);

        // ── Pieces ───────────────────────────────────────────────────────────────

        // A tab's page in the pager: a focus section, which focus comes back into where it left.
        private static UINode TabPage(UINode pages, string name)
        {
            var page = pages.Child(name).Sized(Sizing.Percent(1f), Sizing.Grow()).Column();
            page.FocusScope.Value = FocusScope.Section;
            page.FocusEntry.Value = FocusEntry.Remembered;
            return page;
        }

        // A round button with a glyph on it.
        private static Pressable IconButton(UINode parent, string name, string glyph, float size, Color fill, Color glyphColour)
        {
            var node = parent.Child(name).Sized(Sizing.Fixed(size), Sizing.Fixed(size)).Center();
            node.Box(fill, size * 0.5f).Fill.When(InteractionStates.Pressed, Darker(fill));
            node.Child("Glyph").Label(glyph, Mathf.Round(size * 0.5f), glyphColour, bold: true);
            return node.Pressable();
        }

        // A round close button: a × on a faint disc.
        private static Pressable CloseButton(UINode parent, float size = 32f)
        {
            var node = parent.Child("Close").Sized(Sizing.Fixed(size), Sizing.Fixed(size)).Center();
            DemoKit.Fill(node, s_subtle, size * 0.5f).Fill.When(InteractionStates.Pressed, Pressed);
            DemoKit.Text(node, "Glyph", "×", Mathf.Round(size * 0.6f), s_secondary, bold: true);
            return node.Pressable();
        }

        // A button as wide as its parent, its title centred: the accent's, or a faint one.
        private static Pressable WideButton(UINode parent, string title, bool quiet = false) => WideButton(parent, title, quiet, out _);

        private static Pressable WideButton(UINode parent, string title, bool quiet, out Label label)
        {
            var node = parent.Child(title).Sized(Sizing.Grow(), Sizing.Fixed(46f)).Center();
            node.ClipRadii.Value = 13f;
            if (quiet)
            {
                DemoKit.Fill(node, s_subtle, 13f).Fill.When(InteractionStates.Pressed, Pressed);
                label = DemoKit.Text(node, "Title", title, 16f, s_text, bold: true);
            }
            else
            {
                node.Box(Accent, 13f).Fill.When(InteractionStates.Pressed, Darker(Accent));
                label = node.Child("Title").Label(title, 16f, Color.white, bold: true);
            }
            return node.Pressable();
        }

        // A small rounded chip, for a post's reactions and comments, fitting what is put in it.
        private static UINode Chip(UINode parent, string name)
        {
            var chip = parent.Child(name).Row(5f).Pad(11f, 6f).Align(AlignX.Left, AlignY.Center);
            DemoKit.Fill(chip, s_subtle, 15f).Fill.When(InteractionStates.Pressed, Pressed);
            return chip;
        }

        // A page pushed over the tabs: a cover sliding in from the right over a faint dim, swiped back from anywhere on it,
        // its header a back button and the rest of it scrolling. `content` is what is presented; `body` what it holds.
        private static Presentation PushedPage(UINode app, string name, out UINode content, out Pressable back, out UINode body,
            out ScrollView scroll)
        {
            var host = app.Child(name).Column();
            var page = host.Add<Presentation>();
            page.Style = PresentationStyle.Cover;
            page.Transition = PresentationTransition.Push;
            page.Dim.Value = new Color(0f, 0f, 0f, 0.15f);
            page.Animation = new UIAnimation(0.4f);
            content = host.Child("Content").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            content.IgnoresSafeArea.Value = Edges.All;
            DemoKit.Fill(content, s_background);
            content.Add<Draggable>().Axis = ScrollAxis.Horizontal;
            content.Add<SwipeToDismiss>().Setup(page, SwipeDirections.Right);
            var header = content.Child("Header").Row(8f).Pad(new Insets(8f, 16f, 8f, 4f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            var backNode = header.Child("Back").Pad(8f, 4f);
            backNode.Child("Title").Label("‹ Back", 17f, Accent);
            back = backNode.Pressable();
            body = content.Child("Body").Sized(Sizing.Grow(), Sizing.Grow()).Column(18f).Pad(new Insets(16f, 16f, 8f, 40f));
            scroll = body.Scroll();
            scroll.ScrollPadding.Value = ScrollRoom;
            return page;
        }

        // A card over a dim in the middle of the screen, `width` wide, named as the button it grows out of, thrown away by a
        // drag on it or on the dim around it, following the finger. `card` is what grows.
        private static Presentation CardHost(UINode app, string name, string matchName, float width, out UINode card)
        {
            var host = app.Child(name).Center();
            var presentation = host.Add<Presentation>();
            presentation.Transition = PresentationTransition.None;
            presentation.Animation = new UIAnimation(0.45f, 0.1f);
            card = host.Child("Card").Column(14f).Pad(20f);
            card.Width.Value = Sizing.Fixed(width);
            card.MatchName.Value = matchName;
            card.ClipRadii.Value = 24f;
            DemoKit.Shadow(DemoKit.Fill(card, s_card, 24f), 0.2f, 30f, 10f);
            host.Add<Draggable>().Axis = ScrollAxis.Both;
            host.Add<SwipeToDismiss>().Setup(presentation, SwipeDirections.Any, panel: card);
            return presentation;
        }

        // ── Tab bar ──────────────────────────────────────────────────────────────

        // The tab bar: a capsule floating over the bottom of the pages, a focus section that comes back to the tab it left.
        // The selection pill floats on the selected tab, behind its icon and title, and springs to the next (CircleApp).
        private static List<Pressable> TabBar(UINode app, out UINode pill, out UINode bar)
        {
            bar = app.Child("Tab Bar").Row(2f).Pad(6f).Float(AttachPoint.CenterBottom, AttachPoint.CenterBottom, new Vector2(0f, -28f));
            DemoKit.Shadow(DemoKit.Fill(bar, s_bar, 30f), 0.12f, 24f, 8f);
            bar.FocusScope.Value = FocusScope.Section;
            bar.FocusEntry.Value = FocusEntry.Remembered;
            pill = bar.Child("Pill");
            pill.Box(new Color(Accent.r, Accent.g, Accent.b, 0.14f), 24f);
            var tabs = new List<Pressable>();
            foreach (var (icon, title) in new[] { ("🏠", "Home"), ("💬", "Chats"), ("🙂", "Me") })
            {
                var tab = bar.Child(title).Column(2f).Pad(22f, 8f).Align(AlignX.Center, AlignY.Center);
                tab.Box(Color.clear, 24f).Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.1f));
                tab.FocusRing.Value = false;
                tab.Child("Icon").Label(icon, 20f);
                var label = DemoKit.Text(tab, "Title", title, 11f, s_secondary, bold: true);
                label.Color.When(InteractionStates.Selected, Accent);
                tabs.Add(tab.Pressable());
            }
            pill.FloatOn(tabs[0].Node);
            pill.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            tabs[0].IsSelected = true;
            return tabs;
        }

        // ── Photo viewer ─────────────────────────────────────────────────────────

        // The photo viewer: the photo, named as the one it opens from, over a black backdrop (which takes no focus) with its
        // caption, thrown away any way from the photo or the backdrop, or pinched. See PhotoViewer.
        private static PhotoViewer Viewer(UINode app)
        {
            var host = app.Child("Photo Viewer").Column(18f).Center();
            var presentation = host.Add<Presentation>();
            presentation.Transition = PresentationTransition.None;
            presentation.Dim.Value = Color.clear;
            presentation.DismissOnBackdrop = false;
            // The backdrop and caption fade in as the photo flies out of the feed.
            var backdrop = host.Child("Backdrop").Float(AttachPoint.LeftTop, AttachPoint.LeftTop);
            backdrop.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            backdrop.IgnoresSafeArea.Value = Edges.All;
            backdrop.Box(Color.black);
            backdrop.Opacity.Value = 0f;
            backdrop.Focusable.Value = Focusability.Never;
            backdrop.Pressable();
            var photoNode = host.Child("Photo");
            photoNode.Width.Value = Sizing.Grow();
            photoNode.AspectRatio.Value = 4f / 3f;
            photoNode.MatchName.Value = "Photo";
            var photo = photoNode.Add<ScenePhoto>();
            photo.CornerRadius.Value = 0f;
            photoNode.Pressable();
            photoNode.Add<Pinchable>();
            // Thrown away any way by a drag anywhere in the viewer, on the photo or the backdrop, following the finger.
            host.Add<Draggable>().Axis = ScrollAxis.Both;
            var swipe = host.Add<SwipeToDismiss>();
            swipe.Setup(presentation, SwipeDirections.Any, panel: photoNode);
            var caption = host.Child("Caption").Pad(24f, 0f).Label("", 16f, Color.white);
            caption.Alignment.Value = TextAnchor.UpperCenter;
            caption.Node.Opacity.Value = 0f;
            var viewer = host.Add<PhotoViewer>();
            viewer.Setup(backdrop, photo, caption, swipe);
            return viewer;
        }
    }
}
