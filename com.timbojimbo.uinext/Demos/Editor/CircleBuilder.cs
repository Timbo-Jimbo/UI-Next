using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Circle;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>Builds Circle, the demo app: its scene, and the colour tokens and Dark mode state it is styled with.</summary>
    public static class CircleBuilder
    {
        private const string Folder = DemoKit.DemosFolder + "/Circle";
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Circle.unity";

        private static readonly Color Accent = new Color32(0x34, 0x78, 0xF6, 0xFF);
        private static readonly Color Red = new Color32(0xFF, 0x3B, 0x30, 0xFF);
        private static readonly Color Green = new Color32(0x34, 0xC7, 0x59, 0xFF);

        // The tokens, made by Tokens().
        private static UIState s_dark;
        private static ColorToken s_background, s_card, s_text, s_secondary, s_subtle, s_bubble, s_bar;

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
            var circle = app.Add<CircleApp>();

            var pages = app.Child("Pages").Sized(Sizing.Grow(), Sizing.Grow()).Row();
            var pager = pages.Scroll(ScrollAxis.Horizontal);
            pager.Snap.Value = ScrollSnap.Pages;
            pager.ShowsIndicators.Value = false;

            var photos = new List<ScenePhoto>();
            var captions = new List<string>();
            var stories = new List<Pressable>();
            var ringAvatars = new List<UINode>();
            var homeScroll = Home(pages, photos, captions, stories, ringAvatars);
            var chats = Chats(pages, out var chatsScroll, out var rows);
            var meScroll = Me(pages, out var darkSwitch, out var logOut);

            var tabs = TabBar(app, out var pill);
            var viewer = Viewer(app);
            StoryScreen(app, stories, ringAvatars);
            Conversation(app, rows, chats);
            var dialog = LogOutDialog(app, out var dialogButtons);

            circle.Setup(tabs, pill, pager, new List<ScrollView> { homeScroll, chatsScroll, meScroll }, photos, captions, viewer, s_dark,
                darkSwitch, logOut, dialog, dialogButtons);
        }

        private static Color Darker(Color colour) => new(colour.r * 0.8f, colour.g * 0.8f, colour.b * 0.8f, colour.a);

        // ── Home ─────────────────────────────────────────────────────────────────

        private static ScrollView Home(UINode pages, List<ScenePhoto> photos, List<string> captions, List<Pressable> storyButtons, List<UINode> ringAvatars)
        {
            var home = pages.Child("Home").Sized(Sizing.Percent(1f), Sizing.Grow()).Column();
            var header = home.Child("Header").Row().Pad(new Insets(20f, 20f, 12f, 8f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            DemoKit.Text(header, "Title", "Circle", 30f, s_text, bold: true).Node.Width.Value = Sizing.Grow();
            DemoKit.Avatar(header, 36f, CircleData.Me.Colour, CircleData.Me.Initials);

            var feed = home.Child("Feed").Sized(Sizing.Grow(), Sizing.Grow()).Column(16f).Pad(new Insets(0f, 0f, 4f, 120f));
            var scroll = feed.Scroll();

            var stories = feed.Child("Stories").Row(14f).Pad(new Insets(20f, 20f, 4f, 4f));
            stories.Width.Value = Sizing.Grow();
            var storiesScroll = stories.Scroll(ScrollAxis.Horizontal);
            storiesScroll.ShowsIndicators.Value = false;
            for (int i = 0; i < CircleData.People.Length; i++)
            {
                var (name, initials, colour) = CircleData.People[i];
                var story = stories.Child("Story").Column(6f).Align(AlignX.Center, AlignY.Top);
                // The avatar in the ring and the story it opens are matched by name and by the person's index.
                story.MatchId.Value = i.ToString();
                var ring = story.Child("Ring").Sized(Sizing.Fixed(68f), Sizing.Fixed(68f)).Center();
                var ringBox = ring.Box(new Color32(0xFF, 0x5E, 0x9A, 0xFF), 34f);
                ringBox.Gradient.Value = true;
                ringBox.GradientTo.Value = new Color32(0xFF, 0xB3, 0x47, 0xFF);
                ringBox.GradientAngle.Value = 45f;
                var gap = ring.Child("Gap").Sized(Sizing.Fixed(62f), Sizing.Fixed(62f)).Center();
                DemoKit.Fill(gap, s_background, 31f);
                var avatar = DemoKit.Avatar(gap, 56f, colour, initials);
                avatar.MatchName.Value = "Story";
                DemoKit.Text(story, "Name", name.Split(' ')[0], 12f, s_secondary);
                storyButtons.Add(story.Pressable());
                story.Scale.When(InteractionStates.Pressed, 0.92f);
                ringAvatars.Add(avatar);
            }

            for (int p = 0; p < CircleData.Posts.Length; p++)
            {
                var (person, time, caption, scene, reactions) = CircleData.Posts[p];
                var (name, initials, colour) = CircleData.People[person];
                var row = feed.Child("Post").Pad(16f, 0f);
                row.Width.Value = Sizing.Grow();
                // What is matched inside a post is told apart by the post's index.
                row.MatchId.Value = p.ToString();
                var card = row.Child("Card").Column(12f).Pad(16f);
                card.Width.Value = Sizing.Grow();
                DemoKit.Shadow(DemoKit.Fill(card, s_card, 22f), 0.06f, 18f, 4f);

                var head = card.Child("Header").Row(10f).Align(AlignX.Left, AlignY.Center);
                DemoKit.Avatar(head, 40f, colour, initials);
                var names = head.Child("Names").Column(2f);
                DemoKit.Text(names, "Name", name, 16f, s_text, bold: true);
                DemoKit.Text(names, "Time", time, 13f, s_secondary);

                var photoNode = card.Child("Photo");
                photoNode.Width.Value = Sizing.Grow();
                photoNode.AspectRatio.Value = 4f / 3f;
                photoNode.MatchName.Value = "Photo";
                photoNode.ClipRadii.Value = 14f;
                var photo = photoNode.Add<ScenePhoto>();
                photo.Show(scene);
                photoNode.Pressable();
                photoNode.Scale.When(InteractionStates.Pressed, 0.97f);
                photos.Add(photo);
                captions.Add(caption);

                var text = DemoKit.Text(card, "Caption", caption, 15f, s_text);
                text.Node.Width.Value = Sizing.Grow();

                var chips = card.Child("Reactions").Row(8f);
                foreach (var (emoji, count) in reactions)
                {
                    var chip = chips.Child("Reaction").Row(5f).Pad(11f, 6f).Align(AlignX.Left, AlignY.Center);
                    var chipBox = DemoKit.Fill(chip, s_subtle, 15f);
                    chipBox.Fill.When(InteractionStates.Selected, new Color(Accent.r, Accent.g, Accent.b, 0.18f));
                    DemoKit.FocusRing(chipBox, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
                    chip.Child("Emoji").Label(emoji, 15f);
                    var number = DemoKit.Text(chip, "Count", count.ToString(), 14f, s_secondary, bold: true);
                    number.Color.When(InteractionStates.Selected, Accent);
                    // The chip toggles itself, inside a change.
                    chip.Pressable();
                    chip.Add<ReactionChip>().Setup(number, count);
                }
            }
            return scroll;
        }

        // ── Chats ────────────────────────────────────────────────────────────────

        private static ChatsController Chats(UINode pages, out ScrollView scroll, out List<Pressable> rows)
        {
            var chats = pages.Child("Chats").Sized(Sizing.Percent(1f), Sizing.Grow()).Column();
            var controller = chats.Add<ChatsController>();
            var header = chats.Child("Header").Pad(new Insets(20f, 20f, 12f, 8f));
            DemoKit.Text(header, "Title", "Chats", 30f, s_text, bold: true);

            var list = chats.Child("List").Sized(Sizing.Grow(), Sizing.Grow()).Column().Pad(new Insets(0f, 0f, 0f, 120f));
            scroll = list.Scroll();
            rows = new List<Pressable>();
            for (int c = 0; c < CircleData.Chats.Length; c++)
            {
                var (person, time, preview, _) = CircleData.Chats[c];
                var (name, initials, colour) = CircleData.People[person];
                var row = list.Child("Chat").Row(14f).Pad(20f, 12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                // The row grows into its conversation: matched by name and the chat's index, its avatar and name flying
                // into the conversation's header.
                row.MatchName.Value = "Chat";
                row.MatchId.Value = c.ToString();
                var rowBox = DemoKit.Fill(row, s_background);
                rowBox.Fill.When(InteractionStates.Pressed, new Color(0.5f, 0.5f, 0.55f, 0.15f));
                rowBox.Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
                DemoKit.Avatar(row, 50f, colour, initials).MatchName.Value = "Avatar";
                var middle = row.Child("Middle").Column(3f);
                middle.Width.Value = Sizing.Grow();
                DemoKit.Text(middle, "Name", name, 16f, s_text, bold: true).Node.MatchName.Value = "Name";
                var line = DemoKit.Text(middle, "Preview", preview, 14f, s_secondary);
                line.MaxLines.Value = 1;
                line.Node.Width.Value = Sizing.Grow();
                DemoKit.Text(row, "Time", time, 13f, s_secondary);
                rows.Add(row.Pressable());
            }
            return controller;
        }

        // ── Me ───────────────────────────────────────────────────────────────────

        private static ScrollView Me(UINode pages, out ToggleSwitch darkSwitch, out Pressable logOut)
        {
            var me = pages.Child("Me").Sized(Sizing.Percent(1f), Sizing.Grow()).Column(16f).Pad(new Insets(16f, 16f, 16f, 120f));
            var scroll = me.Scroll();

            var profile = me.Child("Profile").Column(6f).Pad(24f).Align(AlignX.Center, AlignY.Top);
            profile.Width.Value = Sizing.Grow();
            DemoKit.Shadow(DemoKit.Fill(profile, s_card, 22f), 0.06f, 18f, 4f);
            DemoKit.Avatar(profile, 88f, CircleData.Me.Colour, CircleData.Me.Initials);
            DemoKit.Text(profile, "Name", CircleData.Me.Name, 22f, s_text, bold: true);
            DemoKit.Text(profile, "Handle", CircleData.Me.Handle, 15f, s_secondary);
            var bio = DemoKit.Text(profile, "Bio", CircleData.Me.Bio, 15f, s_text);
            bio.Alignment.Value = TextAnchor.UpperCenter;

            var settings = me.Child("Settings").Column().Pad(16f, 4f);
            settings.Width.Value = Sizing.Grow();
            DemoKit.Fill(settings, s_card, 18f);
            darkSwitch = null;
            foreach (var title in new[] { "Dark mode", "Notifications", "Private account" })
            {
                var row = settings.Child("Setting").Row(12f).Pad(0f, 12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                DemoKit.Text(row, "Title", title, 16f, s_text).Node.Width.Value = Sizing.Grow();
                var toggle = Switch(row);
                darkSwitch ??= toggle;
            }

            var button = me.Child("Log out").Center();
            button.Sized(Sizing.Grow(), Sizing.Fixed(50f));
            // Its dialog grows out of it: the two share a match name.
            button.MatchName.Value = "Log out";
            button.ClipRadii.Value = 14f;
            var buttonBox = DemoKit.Fill(button, s_card, 14f);
            buttonBox.Fill.When(InteractionStates.Pressed, new Color(0.5f, 0.5f, 0.55f, 0.25f));
            DemoKit.FocusRing(buttonBox, new Color(Red.r, Red.g, Red.b, 0.5f));
            // Its label turns into the dialog's title as the dialog grows out of it: named alike.
            button.Child("Title").Label("Log out", 17f, Red, bold: true).Node.MatchName.Value = "Log out title";
            logOut = button.Pressable();
            return scroll;
        }

        private static ToggleSwitch Switch(UINode parent)
        {
            var track = parent.Child("Switch").Sized(Sizing.Fixed(51f), Sizing.Fixed(31f)).Pad(2f).Align(AlignX.Left, AlignY.Center);
            var trackBox = track.Box(new Color(0.47f, 0.47f, 0.5f, 0.32f), 15.5f);
            trackBox.Fill.When(InteractionStates.Selected, Green);
            DemoKit.FocusRing(trackBox, new Color(Accent.r, Accent.g, Accent.b, 0.55f));
            var knob = track.Child("Knob").Sized(Sizing.Fixed(27f), Sizing.Fixed(27f));
            DemoKit.Shadow(knob.Box(Color.white, 13.5f), 0.2f, 4f, 2f);
            knob.HitTestable.Value = false;
            var pressable = track.Pressable();
            pressable.Toggles = true;
            return track.Add<ToggleSwitch>();
        }

        // ── Tab bar ──────────────────────────────────────────────────────────────

        private static List<Pressable> TabBar(UINode app, out UINode pill)
        {
            var bar = app.Child("Tab Bar").Row(2f).Pad(6f).Float(AttachPoint.CenterBottom, AttachPoint.CenterBottom, new Vector2(0f, -28f));
            DemoKit.Shadow(DemoKit.Fill(bar, s_bar, 30f), 0.12f, 24f, 8f);
            pill = bar.Child("Pill");
            pill.Box(new Color(Accent.r, Accent.g, Accent.b, 0.14f), 24f);
            var tabs = new List<Pressable>();
            foreach (var (icon, title) in new[] { ("🏠", "Home"), ("💬", "Chats"), ("🙂", "Me") })
            {
                var tab = bar.Child(title).Column(2f).Pad(22f, 8f).Align(AlignX.Center, AlignY.Center);
                tab.Box(Color.clear, 24f).Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.1f));
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
            backdrop.Box(Color.black);
            backdrop.Opacity.Value = 0f;
            backdrop.Pressable();
            var photoNode = host.Child("Photo");
            photoNode.Width.Value = Sizing.Grow();
            photoNode.AspectRatio.Value = 4f / 3f;
            photoNode.MatchName.Value = "Photo";
            var photo = photoNode.Add<ScenePhoto>();
            photo.CornerRadius.Value = 0f;
            photoNode.Pressable();
            photoNode.Add<Draggable>().Axis = ScrollAxis.Both;
            var swipe = photoNode.Add<SwipeToDismiss>();
            swipe.Setup(presentation, SwipeDirections.Any);
            photoNode.Add<Pinchable>();
            var caption = host.Child("Caption").Pad(24f, 0f).Label("", 16f, Color.white);
            caption.Alignment.Value = TextAnchor.UpperCenter;
            caption.Node.Opacity.Value = 0f;
            var viewer = host.Add<PhotoViewer>();
            viewer.Setup(backdrop, photo, caption, swipe);
            return viewer;
        }

        // ── Story viewer ─────────────────────────────────────────────────────────

        private static void StoryScreen(UINode app, List<Pressable> stories, List<UINode> ringAvatars)
        {
            var host = app.Child("Story Viewer").Column();
            var presentation = host.Add<Presentation>();
            presentation.Style = PresentationStyle.Cover;
            presentation.Transition = PresentationTransition.None;
            presentation.Dim.Value = Color.clear;
            // The story and the avatar in its ring share a match name; a swipe down or a tap takes it back into the avatar.
            var story = host.Child("Story").Sized(Sizing.Grow(), Sizing.Grow()).Column(14f).Pad(new Insets(16f, 16f, 60f, 40f));
            story.IgnoresSafeArea.Value = Edges.All;
            story.MatchName.Value = "Story";
            story.Clips.Value = true;
            var background = story.Box(Color.gray);
            background.Gradient.Value = true;
            background.GradientAngle.Value = 90f;
            story.Pressable();
            story.Add<Draggable>().Axis = ScrollAxis.Vertical;
            story.Add<SwipeToDismiss>().Setup(presentation, SwipeDirections.Down);

            var progress = story.Child("Progress").Sized(Sizing.Grow(), Sizing.Fixed(3f));
            progress.Box(new Color(1f, 1f, 1f, 0.9f), 1.5f);
            var head = story.Child("Header").Row(10f).Align(AlignX.Left, AlignY.Center);
            var avatar = DemoKit.Avatar(head, 36f, Color.gray, "");
            var name = head.Child("Name").Label("", 16f, Color.white, bold: true);
            head.Child("Time").Label("2h", 14f, new Color(1f, 1f, 1f, 0.7f));
            var middle = story.Child("Middle").Sized(Sizing.Grow(), Sizing.Grow()).Center();
            middle.Child("Moment").Label("🌅", 96f);

            host.Add<StoryViewer>().Setup(stories, ringAvatars, story, background, avatar.GetComponent<Box>(),
                avatar.transform.GetChild(0).GetComponent<Label>(), name);
        }

        // ── Conversation ─────────────────────────────────────────────────────────

        private static Presentation Conversation(UINode app, List<Pressable> rows, ChatsController controller)
        {
            var host = app.Child("Conversation").Column();
            var presentation = host.Add<Presentation>();
            presentation.Style = PresentationStyle.Cover;
            // It grows out of its row and collapses back into it (matched), rather than sliding.
            presentation.Transition = PresentationTransition.None;
            presentation.Dim.Value = new Color(0f, 0f, 0f, 0.2f);
            presentation.Animation = new UIAnimation(0.45f, 0.08f);

            var content = host.Child("Content").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            DemoKit.Fill(content, s_background);
            content.IgnoresSafeArea.Value = Edges.All;
            content.MatchName.Value = "Chat";
            content.Add<Draggable>().Axis = ScrollAxis.Horizontal;
            content.Add<SwipeToDismiss>().Setup(presentation, SwipeDirections.Right);

            var header = content.Child("Header").Row(10f).Pad(new Insets(10f, 16f, 10f, 10f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            DemoKit.Fill(header, s_card);
            var back = header.Child("Back").Pad(8f, 4f);
            back.Child("Title").Label("‹ Chats", 17f, Accent);
            var backPressable = back.Pressable();
            var avatar = DemoKit.Avatar(header, 36f, Color.gray, "");
            avatar.MatchName.Value = "Avatar";
            var name = DemoKit.Text(header, "Name", "", 17f, s_text, bold: true);
            name.Node.MatchName.Value = "Name";

            var thread = content.Child("Thread").Sized(Sizing.Grow(), Sizing.Grow()).Column(8f).Pad(16f);
            var threadScroll = thread.Scroll();

#if UINEXT_TEXTINPUT
            var replies = content.Child("Replies").Row(8f).Pad(new Insets(16f, 16f, 8f, 4f));
#else
            var replies = content.Child("Replies").Row(8f).Pad(new Insets(16f, 16f, 10f, 30f));
#endif
            replies.Width.Value = Sizing.Grow();
            var replyButtons = new List<Pressable>();
            foreach (var reply in CircleData.Replies)
            {
                var chip = replies.Child("Reply").Pad(14f, 8f);
                var chipBox = chip.Box(new Color(Accent.r, Accent.g, Accent.b, 0.12f), 16f);
                chipBox.Fill.When(InteractionStates.Pressed, new Color(Accent.r, Accent.g, Accent.b, 0.3f));
                chip.Child("Text").Label(reply, 15f, Accent);
                replyButtons.Add(chip.Pressable());
            }

            controller.Setup(rows, presentation, content, backPressable, avatar.GetComponent<Box>(),
                avatar.transform.GetChild(0).GetComponent<Label>(), name, thread, threadScroll, replyButtons, s_bubble, s_text, Accent);

#if UINEXT_TEXTINPUT
            // The composer: a pill that grows with its lines, and a send button lit while there is something to send.
            var composer = content.Child("Composer").Row(8f).Pad(new Insets(12f, 12f, 6f, 30f)).Align(AlignX.Left, AlignY.Bottom);
            composer.Width.Value = Sizing.Grow();
            // Room for the home indicator with the keyboard down; on the keyboard with it up.
            composer.AvoidsKeyboard.Value = true;
            var input = composer.Child("Input");
            input.Width.Value = Sizing.Grow();
            var field = input.TextField("Message", 16f, multiline: true, maxLines: 5, padding: Insets.Symmetric(14f, 9f));
            input.ClipRadii.Value = 18f;
            var inputBox = input.Box(Color.white, 18f);
            inputBox.Fill.Token = s_card;
            inputBox.BorderWidth.Value = 1f;
            inputBox.BorderColor.Token = s_subtle;
            field.Color.Token = s_text;
            field.PlaceholderColor.Token = s_secondary;
            field.CaretColor.Value = Accent;
            field.SelectionColor.Value = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            var config = field.Config;
            config.ReturnKey = TimboJimbo.UI.TextInput.TextReturnKey.Send;
            field.Config = config;
            var send = composer.Child("Send").Sized(Sizing.Fixed(36f), Sizing.Fixed(36f)).Center();
            var sendBox = send.Box(Accent, 18f);
            sendBox.Fill.When(InteractionStates.Disabled, new Color(0.47f, 0.47f, 0.5f, 0.35f));
            sendBox.Fill.When(InteractionStates.Pressed, Color.Lerp(Accent, Color.black, 0.25f));
            send.Child("Arrow").Label("↑", 19f, Color.white, bold: true);
            controller.SetupComposer(field, send.Pressable());
#endif
            return presentation;
        }

        // ── Log out dialog ───────────────────────────────────────────────────────

        private static Presentation LogOutDialog(UINode app, out List<Pressable> buttons)
        {
            var host = app.Child("Log Out Dialog").Center();
            var presentation = host.Add<Presentation>();
            // It grows out of the Log out button by name: the match is its transition.
            presentation.Transition = PresentationTransition.None;
            presentation.Animation = new UIAnimation(0.4f, 0.1f);
            var alert = host.Child("Alert").Column(14f).Pad(22f).Align(AlignX.Center, AlignY.Top);
            alert.Width.Value = Sizing.Fixed(290f);
            // It takes over from the Log out button, which hides as it comes and shows again as it goes (CircleApp).
            alert.MatchName.Value = "Log out";
            alert.ClipRadii.Value = 20f;
            DemoKit.Shadow(DemoKit.Fill(alert, s_card, 20f), 0.2f, 30f, 10f);
            var title = DemoKit.Text(alert, "Title", "Log out of Circle?", 18f, s_text, bold: true);
            title.Alignment.Value = TextAnchor.UpperCenter;
            // It flies out of the Log out button's label, named alike.
            title.Node.MatchName.Value = "Log out title";
            var body = DemoKit.Text(alert, "Body", "You can log back in at any time.", 14f, s_secondary);
            body.Alignment.Value = TextAnchor.UpperCenter;
            var row = alert.Child("Buttons").Row(10f);
            row.Width.Value = Sizing.Grow();
            buttons = new List<Pressable>();
            foreach (var (label, colour, fill) in new[] { ("Cancel", (Color?)null, true), ("Log out", (Color?)Color.white, false) })
            {
                var button = row.Child(label).Center().Sized(Sizing.Grow(), Sizing.Fixed(46f));
                var box = fill ? DemoKit.Fill(button, s_subtle, 12f) : button.Box(Red, 12f);
                box.Fill.When(InteractionStates.Pressed, fill ? new Color(0.5f, 0.5f, 0.55f, 0.3f) : Darker(Red));
                if (colour.HasValue) button.Child("Title").Label(label, 16f, colour.Value, bold: true);
                else DemoKit.Text(button, "Title", label, 16f, s_text, bold: true);
                buttons.Add(button.Pressable());
            }
            return presentation;
        }
    }
}
