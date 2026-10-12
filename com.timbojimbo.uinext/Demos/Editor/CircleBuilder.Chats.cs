using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Circle;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class CircleBuilder
    {
        // ── Chats ────────────────────────────────────────────────────────────────

        // Chats: a row for each chat, the newest two unread, each growing open into its conversation. See ChatsController.
        private static ChatsController Chats(UINode pages, out ScrollView scroll, out List<Pressable> rows)
        {
            var chats = TabPage(pages, "Chats");
            var controller = chats.Add<ChatsController>();
            var header = chats.Child("Header").Pad(new Insets(20f, 20f, 12f, 0f));
            DemoKit.Text(header, "Title", "Chats", 30f, s_text, bold: true);

            var list = chats.Child("List").Sized(Sizing.Grow(), Sizing.Grow()).Column();
            scroll = list.Scroll();
            rows = new List<Pressable>();
            for (int c = 0; c < CircleData.Chats.Length; c++)
            {
                var (person, time, preview, _) = CircleData.Chats[c];
                var (name, initials, colour, _, _) = CircleData.People[person];
                var row = list.Child("Chat").Row(14f).Pad(20f, 12f).Align(AlignX.Left, AlignY.Center);
                row.Width.Value = Sizing.Grow();
                // The row grows into its conversation: matched by name and the chat's index, its avatar and name flying
                // into the conversation's header.
                row.MatchName.Value = "Chat";
                row.MatchId.Value = c.ToString();
                // What is inside it hangs from its top as it grows, so it turns into the conversation's header.
                row.MatchAnchor.Value = new Vector2(0.5f, 0f);
                var rowBox = DemoKit.Fill(row, s_background);
                rowBox.Fill.When(InteractionStates.Pressed, new Color(0.5f, 0.5f, 0.55f, 0.15f));
                rowBox.Fill.When(InteractionStates.Focused, new Color(Accent.r, Accent.g, Accent.b, 0.12f));
                // It shows focus by its highlight, as a list's row does, rather than by the ring.
                row.FocusRing.Value = false;
                DemoKit.Avatar(row, 50f, colour, initials).MatchName.Value = "Avatar";
                var middle = row.Child("Middle").Column(3f);
                middle.Width.Value = Sizing.Grow();
                DemoKit.Text(middle, "Name", name, 16f, s_text, bold: true).Node.MatchName.Value = "Name";
                var line = DemoKit.Text(middle, "Preview", preview, 14f, s_secondary);
                line.MaxLines.Value = 1;
                line.Node.Width.Value = Sizing.Grow();
                var end = row.Child("End").Column(6f).Align(AlignX.Right, AlignY.Center);
                DemoKit.Text(end, "Time", time, 13f, s_secondary);
                // The newest two are unread.
                if (c < 2)
                    end.Child("Unread").Sized(Sizing.Fixed(10f), Sizing.Fixed(10f)).Box(Accent, 5f);
                rows.Add(row.Pressable());
            }
            return controller;
        }

        // ── Conversation ─────────────────────────────────────────────────────────

        // A conversation, grown open out of its row: the person's avatar and name in its header (named as the row's, which
        // they fly out of), whether they are there, the thread, quick replies, and the composer the keyboard types into.
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
            content.MatchAnchor.Value = new Vector2(0.5f, 0f);
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
            var who = header.Child("Who").Column(1f);
            var name = DemoKit.Text(who, "Name", "", 17f, s_text, bold: true);
            name.Node.MatchName.Value = "Name";
            who.Child("Status").Label("Active now", 12f, Green);

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
    }
}
