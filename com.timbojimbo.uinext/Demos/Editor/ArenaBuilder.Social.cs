using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Arena;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class ArenaBuilder
    {
        // ── Clan ─────────────────────────────────────────────────────────────────

        // Clan: the clan's badge, name and standing, its chat (messages, and requests for cards to donate to), and a bar
        // to request cards or chat (for show). See ClanPage.
        private static void Clan(UINode pages, Parts parts)
        {
            var page = Page(pages, "Clan");
            var head = page.Child("Header").Row(12f).Pad(new Insets(12f, 12f, 10f, 10f)).Align(AlignX.Left, AlignY.Center);
            head.Width.Value = Sizing.Grow();
            var badge = head.Child("Badge").Sized(Sizing.Fixed(54f), Sizing.Fixed(60f)).Center();
            Raised(badge, new Color32(0xFF, 0xA9, 0x4D, 0xFF), new Color32(0xE0, 0x55, 0x10, 0xFF), Ink, new CornerRadii(14f, 14f, 27f, 27f), 3f);
            badge.Child("Icon").Label("🐺", 28f);
            var about = head.Child("About").Column(3f);
            about.Width.Value = Sizing.Grow();
            Text(about, "Name", "Northern Lights", 20f);
            Text(about, "Members", "48/50 members  ·  🏆 52,310", 13f, Gold);
            Button(head, "War", "War", Yellow, Sizing.Fixed(74f), 46f, 18f);

            var chat = page.Child("Chat").Sized(Sizing.Grow(), Sizing.Grow()).Column(10f).Pad(new Insets(12f, 12f, 6f, 12f));
            var scroll = chat.Scroll();
            scroll.ScrollPadding.Value = Insets.All(12f);
            scroll.IndicatorColor.Value = new Color(1f, 1f, 1f, 0.4f);
            parts.PageScrolls.Add(scroll);
            for (int m = 0; m < ArenaData.Chat.Length; m++)
            {
                var (name, role, text, card, _, _) = ArenaData.Chat[m];
                if (card < 0) Message(chat, name, role, text);
                else parts.Requests.Add(Request(chat, m, name, role, card));
            }

            var compose = page.Child("Compose").Row(8f).Pad(10f, 8f).Align(AlignX.Left, AlignY.Center);
            compose.Width.Value = Sizing.Grow();
            compose.Box(Well);
            Button(compose, "Request", "Request", Yellow, Sizing.Fixed(104f), 42f, 16f);
            var field = compose.Child("Field").Row().Pad(16f, 0f).Align(AlignX.Left, AlignY.Center);
            field.Sized(Sizing.Grow(), Sizing.Fixed(42f));
            field.Box(new Color(1f, 1f, 1f, 0.92f), 21f);
            field.Child("Placeholder").Label("Chat with your clan…", 15f, new Color(0.35f, 0.4f, 0.5f));
        }

        // Who said it and their role, over a bubble of what they said.
        private static void Message(UINode chat, string name, string role, string text)
        {
            var message = chat.Child(name).Column(4f);
            message.Width.Value = Sizing.Grow();
            Who(message, name, role);
            var bubble = message.Child("Bubble").Pad(12f, 8f);
            bubble.Box(new Color32(0xF2, 0xF6, 0xFF, 0xFF), new CornerRadii(4f, 14f, 14f, 14f));
            bubble.Child("Text").Label(text, 15f, (Color)new Color32(0x1A, 0x24, 0x40, 0xFF));
        }

        // A clanmate's name and role, coloured by rank.
        private static void Who(UINode parent, string name, string role)
        {
            var who = parent.Child("Who").Row(6f).Align(AlignX.Left, AlignY.Center);
            Text(who, "Name", name, 14f);
            var rank = role is "Leader" or "Co-leader" ? Gold : (Color)new Color32(0xB8, 0xD4, 0xFF, 0xFF);
            Text(who, "Role", role, 11f, rank);
        }

        // A request for cards: the card, how many came of how many, and Donate (returned, for ClanPage).
        private static RequestParts Request(UINode chat, int message, string name, string role, int card)
        {
            var request = chat.Child(name + " requests").Column(8f).Pad(10f);
            request.Width.Value = Sizing.Grow();
            Panel(request, 12f);
            Who(request, name, role);
            var row = request.Child("Row").Row(10f).Align(AlignX.Left, AlignY.Center);
            row.Width.Value = Sizing.Grow();
            CardFace(row, card, 52f, out _, out _, out _).MatchName.Value = "";
            var middle = row.Child("Middle").Column(6f);
            middle.Width.Value = Sizing.Grow();
            Text(middle, "Asks", "Requesting " + ArenaData.Cards[card].Name, 14f);
            var bar = Bar(middle, 0f, Green.Bottom, 18f, "", out var fill, out _, out var progress);
            bar.Width.Value = Sizing.Grow();
            var donate = Button(row, "Donate", "Donate", Green, Sizing.Fixed(92f), 42f, 16f, out var label);
            return new RequestParts { Message = message, Donate = donate, DonateLabel = label, Fill = fill, Progress = progress };
        }

        // ── Events ───────────────────────────────────────────────────────────────

        // Events: a card for each, in its own colours with its picture, what it is, how long it runs, and Play (which looks
        // for an opponent, as Battle does).
        private static void Events(UINode pages, Parts parts)
        {
            var body = ScrollingBody(Page(pages, "Events"), parts);
            Heading(body, "Events", "New every week");
            foreach (var (title, subtitle, from, to, emoji, ends) in ArenaData.Events)
            {
                var card = body.Child(title).Column(8f).Pad(16f);
                card.Width.Value = Sizing.Grow();
                Raised(card, from, to, Ink, 18f, 4f);
                var art = card.Child("Art").Float(AttachPoint.RightCenter, AttachPoint.RightCenter, new Vector2(-10f, -10f));
                art.Label(emoji, 64f);
                art.HitTestable.Value = false;
                Text(card, "Title", title, 22f);
                var about = Text(card, "About", subtitle, 13f);
                about.Node.Width.Value = Sizing.Percent(0.66f);
                var foot = card.Child("Foot").Row(8f).Align(AlignX.Left, AlignY.Center);
                foot.Width.Value = Sizing.Grow();
                var timer = foot.Child("Timer").Row(4f).Pad(10f, 4f).Align(AlignX.Left, AlignY.Center);
                timer.Box(Well, 12f);
                Text(timer, "Text", "⏱ Ends in " + ends, 12f);
                foot.Child("Space").Width.Value = Sizing.Grow();
                parts.Battlers.Add(Button(foot, "Play", "Play", Yellow, Sizing.Fixed(100f), 44f, 18f));
            }
        }
    }
}
