using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Circle;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class CircleBuilder
    {
        // ── Home ─────────────────────────────────────────────────────────────────

        // Home: the app's name and the compose button, the stories in a tray scrolling sideways, your post (once you post
        // it) and the feed. A story opens out of its avatar; a post's author opens their profile, its photo the viewer, its
        // + chip the reaction picker and its comments the sheet; the compose button grows the compose card out of itself.
        private static ScrollView Home(UINode pages, Parts parts)
        {
            var home = TabPage(pages, "Home");
            var header = home.Child("Header").Row().Pad(new Insets(20f, 20f, 12f, 8f)).Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            DemoKit.Text(header, "Title", "Circle", 30f, s_text, bold: true).Node.Width.Value = Sizing.Grow();
            // Named as the compose card, which grows out of it.
            parts.Compose = IconButton(header, "Compose", "+", 38f, Accent, Color.white);
            parts.Compose.Node.MatchName.Value = "Compose";
            parts.Compose.Node.ClipRadii.Value = 19f;

            var feed = home.Child("Feed").Sized(Sizing.Grow(), Sizing.Grow()).Column(16f).Pad(new Insets(0f, 0f, 0f, 16f));
            parts.Feed = feed.Scroll();

            Tray(feed, parts);
            var posts = feed.Child("Posts").Column(16f).Pad(16f, 0f);
            posts.Width.Value = Sizing.Grow();
            parts.YourPost = YourPost(posts);
            for (int p = 0; p < CircleData.Posts.Length; p++)
                Post(posts, p, parts);
            return parts.Feed;
        }

        // The stories: a tray scrolling sideways, each story its person's avatar in a ring, named for the story viewer to
        // grow out of. A focus section, which focus comes back into where it left.
        private static void Tray(UINode feed, Parts parts)
        {
            var tray = feed.Child("Stories").Row(14f).Pad(new Insets(8f, 8f, 6f, 6f));
            tray.Width.Value = Sizing.Grow();
            tray.FocusScope.Value = FocusScope.Section;
            tray.FocusEntry.Value = FocusEntry.Remembered;
            parts.Tray = tray.Scroll(ScrollAxis.Horizontal);
            parts.Tray.ShowsIndicators.Value = false;
            parts.Tray.ClearMargin.Value = ScrollMargin;
            for (int i = 0; i < CircleData.Stories.Length; i++)
            {
                var (name, initials, colour, _, _) = CircleData.People[CircleData.Stories[i].Person];
                var story = tray.Child("Story").Column(6f).Align(AlignX.Center, AlignY.Top);
                // The avatar in the ring and the viewer it opens are matched by name and by the story's index.
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
                DemoKit.Text(story, "Name", CircleData.FirstName(name), 12f, s_secondary);
                parts.Stories.Add(story.Pressable());
                story.Scale.When(InteractionStates.Pressed, 0.92f);
                parts.RingAvatars.Add(avatar);
                parts.Rings.Add(ringBox);
            }
        }

        // A post's card, rounded, on a soft shadow.
        private static UINode PostCard(UINode posts, string name)
        {
            var post = posts.Child(name).Column(12f).Pad(16f);
            post.Width.Value = Sizing.Grow();
            DemoKit.Shadow(DemoKit.Fill(post, s_card, 22f), 0.06f, 18f, 4f);
            return post;
        }

        // Your post, out of the feed until the compose card posts it: its text is named as the card's draft, which it takes
        // over from, flying out of the card into the feed.
        private static UINode YourPost(UINode posts)
        {
            var post = PostCard(posts, "Your post");
            post.Display.Value = DisplayMode.None;
            post.Add<DisplayTransition>().Scale = 0.95f;
            var me = CircleData.Me;
            var author = post.Child("Author").Row(10f).Align(AlignX.Left, AlignY.Center);
            DemoKit.Avatar(author, 40f, me.Colour, me.Initials);
            var who = author.Child("Who").Column(2f);
            DemoKit.Text(who, "Name", me.Name, 16f, s_text, bold: true);
            DemoKit.Text(who, "Time", "Just now", 13f, s_secondary);
            var text = DemoKit.Text(post, "Text", CircleData.Draft, 16f, s_text);
            text.Node.Width.Value = Sizing.Grow();
            text.Node.MatchName.Value = "Draft";
            return post;
        }

        // A post: its author (a button opening their profile, their avatar and name named for rising into it), its photo,
        // what it says, and its reactions and comments.
        private static void Post(UINode posts, int index, Parts parts)
        {
            var (person, time, caption, scene, reactions) = CircleData.Posts[index];
            var (name, initials, colour, _, _) = CircleData.People[person];
            var post = PostCard(posts, "Post " + (index + 1));
            // What is matched in a post is told apart by the post's index.
            post.MatchId.Value = index.ToString();

            var author = post.Child("Author").Row(10f).Pad(4f).Align(AlignX.Left, AlignY.Center);
            author.Box(Color.clear, 12f).Fill.When(InteractionStates.Pressed, Pressed);
            var avatar = DemoKit.Avatar(author, 40f, colour, initials);
            avatar.MatchName.Value = "Author avatar";
            var who = author.Child("Who").Column(2f);
            var nameLabel = DemoKit.Text(who, "Name", name, 16f, s_text, bold: true);
            nameLabel.Node.MatchName.Value = "Author name";
            DemoKit.Text(who, "Time", time, 13f, s_secondary);
            parts.Authors.Add(author.Pressable());
            parts.AuthorAvatars.Add(avatar);
            parts.AuthorNames.Add(nameLabel.Node);

            var photoNode = post.Child("Photo");
            photoNode.Width.Value = Sizing.Grow();
            photoNode.AspectRatio.Value = 4f / 3f;
            photoNode.MatchName.Value = "Photo";
            photoNode.ClipRadii.Value = 14f;
            var photo = photoNode.Add<ScenePhoto>();
            photo.Show(scene);
            photoNode.Pressable();
            photoNode.Scale.When(InteractionStates.Pressed, 0.97f);
            parts.Photos.Add(photo);
            parts.Captions.Add(caption);

            var text = DemoKit.Text(post, "Caption", "**" + CircleData.FirstName(name) + "**  " + caption, 15f, s_text);
            text.Node.Width.Value = Sizing.Grow();

            var actions = post.Child("Actions").Row(8f).Align(AlignX.Left, AlignY.Top);
            actions.Width.Value = Sizing.Grow();
            Reactions(actions, index, reactions, parts);
            var comments = Chip(actions, "Comments");
            comments.Child("Emoji").Label("💬", 15f);
            DemoKit.Text(comments, "Count", CircleData.Comments.Length.ToString(), 14f, s_secondary, bold: true);
            parts.CommentButtons.Add(comments.Pressable());
        }

        // A post's reactions, wrapping in a row: a chip for every emoji the picker has, out of the row until someone reacts
        // with it, Selected while it is yours, its emoji named for the picker's cell to fly into it; and the + chip, named
        // as the picker, which grows out of it. See PostReactions.
        private static void Reactions(UINode actions, int post, (int Emoji, int Count)[] start, Parts parts)
        {
            var row = actions.Child("Reactions").Row(6f);
            row.Width.Value = Sizing.Grow();
            row.Wrap.Value = Wrap.Lines;
            var counts = new int[CircleData.Reactions.Length];
            foreach (var (emoji, count) in start)
                counts[emoji] = count;

            var chips = new List<Pressable>();
            var labels = new List<Label>();
            for (int i = 0; i < counts.Length; i++)
            {
                var chip = Chip(row, CircleData.Reactions[i]);
                var box = chip.GetComponent<Box>();
                box.Fill.When(InteractionStates.Selected, new Color(Accent.r, Accent.g, Accent.b, 0.18f));
                box.BorderColor.Value = Accent;
                box.BorderWidth.When(InteractionStates.Selected, 1.5f);
                chip.MatchId.Value = PostReactions.Key(post, i);
                chip.Display.Value = counts[i] > 0 ? DisplayMode.Visible : DisplayMode.None;
                chip.Add<DisplayTransition>().Scale = 0.5f;
                chip.Child("Emoji").Label(CircleData.Reactions[i], 15f).Node.MatchName.Value = "Reaction";
                var number = DemoKit.Text(chip, "Count", counts[i].ToString(), 14f, s_secondary, bold: true);
                number.Color.When(InteractionStates.Selected, Accent);
                chips.Add(chip.Pressable());
                labels.Add(number);
            }

            var add = Chip(row, "Add reaction");
            add.Width.Value = Sizing.Fit(42f);
            add.AlignX.Value = AlignX.Center;
            add.MatchName.Value = "Reaction picker";
            add.ClipRadii.Value = 15f;
            DemoKit.Text(add, "Plus", "+", 16f, s_secondary, bold: true);
            row.Add<PostReactions>().Setup(post, chips, labels, new List<int>(counts), add.Pressable(), parts.Picker);
        }

        // ── Compose ──────────────────────────────────────────────────────────────

        // The compose card: named as the compose button, which it grows out of and goes back into. The draft is named as
        // your post's text, which takes it over as it is posted. See ComposeCard.
        private static void Compose(UINode app, Parts parts)
        {
            var presentation = CardHost(app, "Compose", "Compose", 342f, out var card);
            var header = card.Child("Header").Row().Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            DemoKit.Text(header, "Title", "New post", 19f, s_text, bold: true).Node.Width.Value = Sizing.Grow();
            var close = CloseButton(header);
            var me = CircleData.Me;
            var author = card.Child("Author").Row(10f).Align(AlignX.Left, AlignY.Center);
            DemoKit.Avatar(author, 36f, me.Colour, me.Initials);
            DemoKit.Text(author, "Name", me.Name, 15f, s_text, bold: true);
            var draft = DemoKit.Text(card, "Draft", CircleData.Draft, 16f, s_text);
            draft.Node.Width.Value = Sizing.Grow();
            draft.Node.MatchName.Value = "Draft";
            var post = WideButton(card, "Post");
            post.Node.PrefersFocus.Value = true;

            var host = presentation.Node;
            host.Add<PresentFrom>().Setup(parts.Compose, new List<Pressable> { close });
            host.Add<ComposeCard>().Setup(post, draft.Node, parts.YourPost, parts.Feed);
        }

        // ── Reactions ────────────────────────────────────────────────────────────

        // The reaction picker: a card named as each post's + chip, which it grows out of and goes back into, holding a grid
        // of emoji, each Selected while it is yours. See ReactionPicker.
        private static ReactionPicker Picker(UINode app)
        {
            var presentation = CardHost(app, "Reactions", "Reaction picker", 330f, out var card);
            var header = card.Child("Header").Row().Align(AlignX.Left, AlignY.Center);
            header.Width.Value = Sizing.Grow();
            DemoKit.Text(header, "Title", "Add a reaction", 17f, s_text, bold: true).Node.Width.Value = Sizing.Grow();
            var close = CloseButton(header);

            var grid = card.Child("Grid").Row(6f);
            grid.Width.Value = Sizing.Grow();
            grid.Wrap.Value = Wrap.Grid(6);
            var cells = new List<Pressable>();
            foreach (var emoji in CircleData.Reactions)
            {
                var cell = grid.Child(emoji).Center();
                cell.Width.Value = Sizing.Grow();
                cell.AspectRatio.Value = 1f;
                var box = DemoKit.Fill(cell, s_subtle, 12f);
                box.Fill.When(InteractionStates.Selected, new Color(Accent.r, Accent.g, Accent.b, 0.3f));
                box.Fill.When(InteractionStates.Pressed, Pressed);
                cell.Child("Emoji").Label(emoji, 24f).Node.MatchName.Value = "Reaction";
                cells.Add(cell.Pressable());
            }

            var picker = presentation.Node.Add<ReactionPicker>();
            picker.Setup(card, cells, close);
            return picker;
        }

        // ── Comments ─────────────────────────────────────────────────────────────

        // The comments sheet: up from the bottom over a dim, a grabber and a list of comments that scrolls, each a row whose
        // dot is a like. Pulled down it follows the finger, once the list is at its top, and goes.
        private static Presentation CommentsSheet(UINode app)
        {
            var host = app.Child("Comments").Column().Align(AlignX.Left, AlignY.Bottom);
            var sheet = host.Add<Presentation>();
            var card = host.Child("Sheet").Column(10f).Pad(new Insets(16f, 16f, 10f, 30f));
            card.Sized(Sizing.Grow(), Sizing.Fixed(520f));
            card.IgnoresSafeArea.Value = Edges.Bottom;
            DemoKit.Fill(card, s_card, new CornerRadii(26f, 26f, 0f, 0f));
            card.Add<Draggable>().Axis = ScrollAxis.Vertical;
            card.Add<SwipeToDismiss>().Setup(sheet, SwipeDirections.Down);

            var top = card.Child("Top").Column(10f).Align(AlignX.Center, AlignY.Top);
            top.Width.Value = Sizing.Grow();
            top.Child("Grabber").Sized(Sizing.Fixed(40f), Sizing.Fixed(5f)).Box(new Color(0.5f, 0.5f, 0.55f, 0.4f), 2.5f);
            DemoKit.Text(top, "Title", "Comments", 17f, s_text, bold: true);

            var list = card.Child("List").Sized(Sizing.Grow(), Sizing.Grow()).Column(4f);
            var scroll = list.Scroll();
            // At its top, the rest of a pull down goes to the sheet, which follows the finger.
            scroll.AtEnd.Value = ScrollEnd.PassOn;
            scroll.ClearMargin.Value = ScrollMargin;
            foreach (var (who, text) in CircleData.Comments)
            {
                var (name, initials, colour, _, _) = CircleData.People[who];
                var row = list.Child(name).Row(12f).Pad(8f, 8f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                row.Box(Color.clear, 12f).Fill.When(InteractionStates.Pressed, Pressed);
                DemoKit.Avatar(row, 34f, colour, initials);
                var words = row.Child("Words").Column(2f);
                words.Width.Value = Sizing.Grow();
                DemoKit.Text(words, "Name", name, 13f, s_secondary, bold: true);
                DemoKit.Text(words, "Text", text, 15f, s_text).Node.Width.Value = Sizing.Grow();
                // The dot is a like: the row toggles it.
                var like = row.Child("Like").Sized(Sizing.Fixed(12f), Sizing.Fixed(12f));
                DemoKit.Fill(like, s_subtle, 6f).Fill.When(InteractionStates.Selected, Red);
                row.Pressable().Toggles = true;
            }
            return sheet;
        }

        // ── Profile ──────────────────────────────────────────────────────────────

        // A person's profile, pushed from a post's author: their avatar and name (named as the post's, which they rise out
        // of), handle and line, a follow button, and their photos, which open into the viewer. See ProfilePage.
        private static void Profile(UINode app, Parts parts)
        {
            var presentation = PushedPage(app, "Profile", out var content, out var back, out var body, out var scroll);
            var who = body.Child("Who").Column(6f).Align(AlignX.Center, AlignY.Top);
            who.Width.Value = Sizing.Grow();
            var avatar = DemoKit.Avatar(who, 96f, Color.gray, "");
            avatar.MatchName.Value = "Author avatar";
            var name = DemoKit.Text(who, "Name", "", 24f, s_text, bold: true);
            name.Node.MatchName.Value = "Author name";
            var handle = DemoKit.Text(who, "Handle", "", 14f, s_secondary);
            var bio = DemoKit.Text(who, "Bio", "", 15f, s_text);
            bio.Alignment.Value = TextAnchor.UpperCenter;

            // Following, it goes faint.
            var follow = WideButton(body, "Follow", false, out var followLabel);
            follow.Node.GetComponent<Box>().Fill.When(InteractionStates.Selected, new Color(0.47f, 0.47f, 0.5f, 0.2f));
            followLabel.Color.When(InteractionStates.Selected, Accent);
            follow.Node.PrefersFocus.Value = true;

            DemoKit.Text(body, "Posts title", "Posts", 17f, s_text, bold: true);
            var grid = body.Child("Photos").Row(4f);
            grid.Width.Value = Sizing.Grow();
            grid.Wrap.Value = Wrap.Grid(3);
            var photos = new List<ScenePhoto>();
            for (int i = 0; i < 6; i++)
                photos.Add(GridPhoto(grid, i, "profile " + i));

            var page = presentation.Node.Add<ProfilePage>();
            page.SetupPosts(parts.Authors, parts.AuthorAvatars, parts.AuthorNames);
            page.SetupPage(content, scroll, back, avatar.GetComponent<Box>(), avatar.transform.GetChild(0).GetComponent<Label>(), name, handle,
                bio, follow, followLabel, photos, parts.Viewer);
        }

        // A square photo in a grid, named by `id` for the viewer to open out of.
        private static ScenePhoto GridPhoto(UINode grid, int index, string id)
        {
            var slot = grid.Child("Photo " + (index + 1));
            slot.Width.Value = Sizing.Grow();
            slot.AspectRatio.Value = 1f;
            slot.MatchName.Value = "Photo";
            slot.MatchId.Value = id;
            slot.ClipRadii.Value = 10f;
            var photo = slot.Add<ScenePhoto>();
            photo.CornerRadius.Value = 10f;
            photo.Show(index);
            slot.Pressable();
            slot.Scale.When(InteractionStates.Pressed, 0.96f);
            return photo;
        }

        // ── Stories ──────────────────────────────────────────────────────────────

        // The story viewer: full screen, named as the avatars in the tray, which it grows out of and shrinks back into. Its
        // progress along the stories, whose they are, what they say, and a like; taps on its sides step through them (with
        // keys, left and right on its middle, one focus stop); pulled down it follows the finger and goes. See StoryViewer.
        private static void StoryScreen(UINode app, Parts parts)
        {
            var host = app.Child("Story Viewer").Column();
            var presentation = host.Add<Presentation>();
            presentation.Style = PresentationStyle.Cover;
            presentation.Transition = PresentationTransition.None;
            presentation.Dim.Value = Color.clear;
            var story = host.Child("Story").Sized(Sizing.Grow(), Sizing.Grow()).Column(14f).Pad(new Insets(16f, 16f, 10f, 30f));
            story.IgnoresSafeArea.Value = Edges.All;
            story.MatchName.Value = "Story";
            story.Clips.Value = true;
            var background = story.Box(Color.gray);
            background.Gradient.Value = true;
            background.GradientAngle.Value = 90f;
            story.Add<Draggable>().Axis = ScrollAxis.Vertical;
            story.Add<SwipeToDismiss>().Setup(presentation, SwipeDirections.Down);

            var progress = story.Child("Progress").Row(4f);
            progress.Width.Value = Sizing.Grow();
            var segments = new List<Box>();
            for (int i = 0; i < CircleData.Stories.Length; i++)
                segments.Add(progress.Child("Segment").Sized(Sizing.Grow(), Sizing.Fixed(3f)).Box(Color.white, 1.5f));

            var head = story.Child("Header").Row(10f).Align(AlignX.Left, AlignY.Center);
            head.Width.Value = Sizing.Grow();
            var avatar = DemoKit.Avatar(head, 36f, Color.gray, "");
            var name = head.Child("Name").Label("", 16f, Color.white, bold: true);
            head.Child("Time").Label("2h", 14f, new Color(1f, 1f, 1f, 0.7f)).Node.Width.Value = Sizing.Grow();
            var close = IconButton(head, "Close", "×", 32f, new Color(1f, 1f, 1f, 0.2f), Color.white);

            // The middle: a tap on its left side steps back a story and on its right on (clear buttons over it, which take
            // no focus); with keys it is one stop, which left and right step.
            var middle = story.Child("Middle").Sized(Sizing.Grow(), Sizing.Grow()).Center();
            middle.Focusable.Value = Focusability.Always;
            middle.PrefersFocus.Value = true;
            var steps = middle.Add<StorySteps>();
            var backSide = StorySide(middle, "Back", 0.35f, AttachPoint.LeftTop);
            var onSide = StorySide(middle, "On", 0.65f, AttachPoint.RightTop);
            var text = middle.Child("Text").Pad(20f, 0f).Label("", 30f, Color.white, bold: true);
            text.Alignment.Value = TextAnchor.UpperCenter;
            text.Node.Width.Value = Sizing.Grow();
            text.Node.HitTestable.Value = false;

            var actions = story.Child("Actions").Row().Align(AlignX.Center, AlignY.Center);
            actions.Width.Value = Sizing.Grow();
            var like = actions.Child("Like").Row(6f).Pad(18f, 10f).Align(AlignX.Center, AlignY.Center);
            like.Box(new Color(1f, 1f, 1f, 0.2f), 20f).Fill.When(InteractionStates.Selected, Red);
            like.Child("Heart").Label("♥", 16f, Color.white);
            like.Child("Title").Label("Like", 15f, Color.white, bold: true);
            var likeButton = like.Pressable();
            likeButton.Toggles = true;

            var viewer = host.Add<StoryViewer>();
            viewer.SetupTray(parts.Stories, parts.RingAvatars, parts.Rings, parts.Tray, new Color(0.62f, 0.62f, 0.66f));
            viewer.SetupViewer(story, background, segments, avatar.GetComponent<Box>(), avatar.transform.GetChild(0).GetComponent<Label>(), name,
                text, close, backSide, onSide, likeButton);
            steps.Setup(viewer);
        }

        // A clear side of the story's middle, `part` of its width, which a tap steps through the stories from; it takes no
        // focus.
        private static Pressable StorySide(UINode middle, string name, float part, AttachPoint point)
        {
            var side = middle.Child(name).Sized(Sizing.Percent(part), Sizing.Percent(1f)).Float(point, point);
            side.Focusable.Value = Focusability.Never;
            return side.Pressable();
        }
    }
}
