using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Arena;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>
    /// Builds Arena, a demo of a mobile game's menus in the style of the big card-battle games: a top bar with your
    /// level, gold and gems; five pages swiped between over a tab bar whose selected tab widens and lifts (Shop, Cards,
    /// Battle, Clan, Events); chests that unlock and open into their rewards, cards that open into their details and
    /// upgrade, a shop whose offers ask first, a clan chat to donate cards in, and events to play. Chunky gradient buttons
    /// with hard shadows that squash as they are pressed, and white text outlined dark, throughout.
    /// </summary>
    public static partial class ArenaBuilder
    {
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Arena.unity";

        // The look: deep blues, panels a step lighter, buttons in yellow, blue, green and red, text white outlined dark.
        private static readonly Color Ink = new Color32(0x0B, 0x14, 0x30, 0xFF);
        private static readonly Color Gold = new Color32(0xFF, 0xD5, 0x4A, 0xFF);
        private static readonly Color PanelTop = new Color32(0x45, 0x7C, 0xDE, 0xFF);
        private static readonly Color PanelBottom = new Color32(0x27, 0x52, 0xAE, 0xFF);
        private static readonly Color PanelEdge = new Color32(0x0E, 0x2A, 0x66, 0xFF);
        private static readonly Color Well = new(0f, 0.04f, 0.15f, 0.45f);
        private static readonly (Color Top, Color Bottom, Color Edge) Yellow = (new Color32(0xFF, 0xDE, 0x5A, 0xFF), new Color32(0xF5, 0x9E, 0x1B, 0xFF), new Color32(0x9A, 0x52, 0x00, 0xFF));
        private static readonly (Color Top, Color Bottom, Color Edge) Blue = (new Color32(0x6C, 0xBE, 0xFF, 0xFF), new Color32(0x2B, 0x6C, 0xE0, 0xFF), new Color32(0x10, 0x3C, 0x8C, 0xFF));
        private static readonly (Color Top, Color Bottom, Color Edge) Green = (new Color32(0x9B, 0xEA, 0x5E, 0xFF), new Color32(0x3E, 0xA6, 0x30, 0xFF), new Color32(0x1C, 0x63, 0x15, 0xFF));
        private static readonly (Color Top, Color Bottom, Color Edge) Red = (new Color32(0xFF, 0x7A, 0x6E, 0xFF), new Color32(0xD8, 0x34, 0x34, 0xFF), new Color32(0x7A, 0x12, 0x12, 0xFF));

        // What the pieces are wired to, gathered as they are made.
        private sealed class Parts
        {
            public ArenaApp App;
            public CardCollection Cards;
            public ChestSlots Chests;
            public Label Gold;
            public Label Gems;
            public readonly List<Pressable> Battlers = new();
            public readonly List<ChestSlotParts> Slots = new();
            public readonly ChestPopupParts ChestPopup = new();
            public readonly List<CardTileParts> Tiles = new();
            public readonly CardPopupParts CardPopup = new();
            public readonly List<OfferParts> Offers = new();
            public readonly BuyDialogParts BuyDialog = new();
            public readonly List<RequestParts> Requests = new();
            public readonly List<ScrollView> PageScrolls = new();
        }

        [MenuItem("Tools/UI Next/Demos/Build Arena")]
        private static void BuildAndOpen() => Build(open: true);

        public static void Build(bool open) => DemoKit.BuildScene(ScenePath, _ => BuildScene(), open);

        private static void BuildScene()
        {
            DemoKit.Camera(Color.black);
            var root = DemoKit.Root("Arena");
            root.FocusRingColor.Value = Gold;
            var app = root.Node.Column();
            var background = app.Box(new Color32(0x2C, 0x5F, 0xCB, 0xFF));
            background.Gradient.Value = true;
            background.GradientTo.Value = new Color32(0x0F, 0x26, 0x63, 0xFF);
            var parts = new Parts
            {
                App = app.Add<ArenaApp>(),
                Cards = app.Add<CardCollection>(),
            };

            TopBar(app, parts);
            var pages = app.Child("Pages").Sized(Sizing.Grow(), Sizing.Grow()).Row();
            var pager = pages.Scroll(ScrollAxis.Horizontal);
            pager.Snap.Value = ScrollSnap.Pages;
            pager.ShowsIndicators.Value = false;
            var tabs = TabBar(app);

            // What the pages open into, made first, so the pages can be wired to them.
            var chestHost = ChestPopup(app, parts.ChestPopup);
            CardPopup(app, parts.CardPopup);
            BuyDialog(app, parts.BuyDialog);

            Shop(pages, parts);
            Cards(pages, parts);
            Battle(pages, parts);
            Clan(pages, parts);
            Events(pages, parts);
            BattleSearch(app, parts);

            parts.Chests = chestHost.Add<ChestSlots>();
            parts.Chests.Setup(parts.Slots, parts.ChestPopup, parts.App, parts.Cards);
            app.Add<CardsPage>().Setup(parts.Tiles, parts.CardPopup, parts.App, parts.Cards);
            app.Add<ShopPage>().Setup(parts.Offers, parts.BuyDialog, parts.App, parts.Cards, parts.Chests);
            app.Add<ClanPage>().Setup(parts.Requests, parts.App);
            parts.App.Setup(tabs, pager, parts.PageScrolls, parts.Gold, parts.Gems, first: 2);
        }

        // ── The look ─────────────────────────────────────────────────────────────

        // White text outlined dark with a hard shadow under it, as the game writes everything.
        private static Label Text(UINode parent, string name, string text, float size, Color? colour = null)
        {
            var label = parent.Child(name).Label(text, size, colour ?? Color.white, bold: true);
            label.OutlineWidth.Value = Mathf.Max(1.2f, size * 0.09f);
            label.OutlineColor.Value = Ink;
            label.ShadowColor.Value = new Color(0f, 0f, 0f, 0.55f);
            label.ShadowOffset.Value = new Vector2(0f, Mathf.Max(1f, size * 0.08f));
            return label;
        }

        // A box with a vertical gradient, an edge bevelled from light above to dark below, and a hard shadow under it, as
        // a game's panels and buttons stand up.
        private static Box Raised(UINode node, Color top, Color bottom, Color edge, CornerRadii radii, float depth = 4f)
        {
            var box = node.Box(top, radii);
            box.Gradient.Value = true;
            box.GradientTo.Value = bottom;
            box.GradientAngle.Value = 90f;
            box.BorderWidth.Value = 2.5f;
            box.BorderGradient.Value = true;
            box.BorderColor.Value = Color.Lerp(top, Color.white, 0.45f);
            box.BorderColorTo.Value = edge;
            box.ShadowColor.Value = Color.Lerp(edge, Color.black, 0.3f);
            box.ShadowBlur.Value = 0.5f;
            box.ShadowOffset.Value = new Vector2(0f, depth);
            return box;
        }

        // A panel: the blue a step lighter than the background, raised.
        private static Box Panel(UINode node, float radius = 14f) => Raised(node, PanelTop, PanelBottom, PanelEdge, radius, 3f);

        // A chunky button: a raised gradient, a shine across its top half, its title outlined, squashing as it is pressed
        // and greying while it cannot be.
        private static Pressable Button(UINode parent, string name, string title, (Color Top, Color Bottom, Color Edge) colours,
            Sizing width, float height, float textSize, out Label label)
        {
            var node = parent.Child(name).Center();
            node.Sized(width, Sizing.Fixed(height));
            node.Pad(10f, 0f);
            float radius = Mathf.Min(14f, height * 0.3f);
            Raised(node, colours.Top, colours.Bottom, colours.Edge, radius);
            var shine = node.Child("Shine").Float(AttachPoint.CenterTop, AttachPoint.CenterTop, new Vector2(0f, 3f));
            shine.Sized(Sizing.Percent(0.92f), Sizing.Percent(0.42f));
            shine.Box(new Color(1f, 1f, 1f, 0.22f), radius * 0.8f);
            shine.HitTestable.Value = false;
            label = Text(node, "Title", title, textSize);
            node.Scale.When(InteractionStates.Pressed, 0.93f);
            node.Saturation.When(InteractionStates.Disabled, 0.15f);
            node.Brightness.When(InteractionStates.Disabled, 0.85f);
            return node.Pressable();
        }

        private static Pressable Button(UINode parent, string name, string title, (Color Top, Color Bottom, Color Edge) colours,
            Sizing width, float height, float textSize) => Button(parent, name, title, colours, width, height, textSize, out _);

        // A round close button: a red ×.
        private static Pressable CloseButton(UINode card)
        {
            var close = Button(card, "Close", "×", Red, Sizing.Fixed(38f), 38f, 22f);
            close.Node.Pad(0f);
            close.Node.Float(AttachPoint.RightTop, AttachPoint.RightTop, new Vector2(12f, -12f));
            return close;
        }

        // A dark well with a bar in it as full as `value`, and a label over it: experience, copies, donations.
        private static UINode Bar(UINode parent, float value, Color fill, float height, string text, out UINode bar, out Box fillBox,
            out Label label)
        {
            var track = parent.Child("Bar").Row().Align(AlignX.Left, AlignY.Center);
            track.Height.Value = Sizing.Fixed(height);
            track.Box(Well, height * 0.5f).BorderWidth.Value = 1.5f;
            track.Clip(height * 0.5f);
            bar = track.Child("Fill").Sized(Sizing.Percent(value), Sizing.Percent(1f));
            fillBox = bar.Box(fill, height * 0.5f);
            label = Text(track, "Text", text, Mathf.Round(height * 0.62f));
            label.Node.Float(AttachPoint.CenterCenter, AttachPoint.CenterCenter);
            return track;
        }

        // A page in the pager: a focus section, which focus comes back into where it left.
        private static UINode Page(UINode pages, string name)
        {
            var page = pages.Child(name).Sized(Sizing.Percent(1f), Sizing.Grow()).Column();
            page.FocusScope.Value = FocusScope.Section;
            page.FocusEntry.Value = FocusEntry.Remembered;
            return page;
        }

        // A page's body, scrolling, kept clear of nothing (the tab bar sits below the pages, not over them).
        private static UINode ScrollingBody(UINode page, Parts parts)
        {
            var body = page.Child("Body").Sized(Sizing.Grow(), Sizing.Grow()).Column(14f).Pad(new Insets(12f, 12f, 8f, 20f));
            var scroll = body.Scroll();
            scroll.ScrollPadding.Value = Insets.All(12f);
            scroll.IndicatorColor.Value = new Color(1f, 1f, 1f, 0.4f);
            parts.PageScrolls.Add(scroll);
            return body;
        }

        // A section's heading: its title, and a note at the right.
        private static void Heading(UINode parent, string title, string note = null)
        {
            var row = parent.Child(title).Row(8f).Align(AlignX.Left, AlignY.Center);
            row.Width.Value = Sizing.Grow();
            Text(row, "Title", title, 20f).Node.Width.Value = Sizing.Grow();
            if (note != null)
                Text(row, "Note", note, 13f, Gold);
        }

        // A card's face: its rarity's gradient in a dark frame, its picture, its cost in a drop at the corner.
        private static UINode CardFace(UINode parent, int card, float width, out Box box, out Label icon, out Label elixir)
        {
            var (_, emoji, cost, rarity, _, _, _) = ArenaData.Cards[card];
            var (top, bottom) = ArenaData.RarityColours(rarity);
            var frame = parent.Child("Frame");
            frame.Width.Value = width > 0f ? Sizing.Fixed(width) : Sizing.Grow();
            frame.AspectRatio.Value = 0.82f;
            frame.MatchName.Value = "Card";
            frame.MatchId.Value = card.ToString();
            frame.ClipRadii.Value = 10f;
            box = Raised(frame, top, bottom, Ink, 10f, 3f);
            icon = frame.Child("Icon").Float(AttachPoint.CenterCenter, AttachPoint.CenterCenter).Label(emoji, width > 0f ? width * 0.42f : 34f);
            var drop = frame.Child("Elixir").Sized(Sizing.Fixed(26f), Sizing.Fixed(26f)).Center();
            drop.Float(AttachPoint.CenterCenter, AttachPoint.LeftTop, new Vector2(4f, 4f));
            Raised(drop, new Color32(0xF5, 0x7C, 0xF0, 0xFF), new Color32(0xB8, 0x2E, 0xC4, 0xFF), Ink, 13f, 2f);
            elixir = Text(drop, "Cost", cost.ToString(), 14f);
            return frame;
        }

        // ── Top bar ──────────────────────────────────────────────────────────────

        // Your level in a shield, your name and experience, and your gold and gems, each with a + to buy more.
        private static void TopBar(UINode app, Parts parts)
        {
            var top = app.Child("Top Bar").Row(8f).Pad(new Insets(10f, 10f, 6f, 6f)).Align(AlignX.Left, AlignY.Center);
            top.Width.Value = Sizing.Grow();
            var shield = top.Child("Level").Sized(Sizing.Fixed(42f), Sizing.Fixed(46f)).Center();
            Raised(shield, Blue.Top, Blue.Bottom, Ink, new CornerRadii(12f, 12f, 21f, 21f), 2f);
            Text(shield, "Number", ArenaData.PlayerLevel.ToString(), 19f);

            var who = top.Child("Who").Column(3f);
            Text(who, "Name", ArenaData.PlayerName, 15f);
            var xp = Bar(who, (float)ArenaData.Experience / ArenaData.ExperienceNeeded, new Color32(0x4F, 0xD8, 0xF5, 0xFF), 14f,
                ArenaData.Number(ArenaData.Experience) + "/" + ArenaData.Number(ArenaData.ExperienceNeeded), out _, out _, out _);
            xp.Width.Value = Sizing.Fixed(104f);

            top.Child("Space").Width.Value = Sizing.Grow();
            parts.Gold = Wallet(top, "Gold", "💰", ArenaData.Gold);
            parts.Gems = Wallet(top, "Gems", "💎", ArenaData.Gems);
        }

        // A wallet pill: its icon, its amount (counted by ArenaApp), and a green + (for show).
        private static Label Wallet(UINode top, string name, string icon, int amount)
        {
            var pill = top.Child(name).Row(4f).Pad(new Insets(4f, 4f, 3f, 3f)).Align(AlignX.Left, AlignY.Center);
            var box = pill.Box(Well, 15f);
            box.BorderWidth.Value = 1.5f;
            box.BorderColor.Value = new Color(0f, 0f, 0f, 0.5f);
            pill.Child("Icon").Label(icon, 17f);
            var label = Text(pill, "Amount", ArenaData.Number(amount), 14f);
            label.Node.Width.Value = Sizing.Fit(48f);
            var plus = Button(pill, "Plus", "+", Green, Sizing.Fixed(22f), 22f, 15f);
            plus.Node.Pad(0f);
            return label;
        }

        // ── Tab bar ──────────────────────────────────────────────────────────────

        // The tab bar, docked under the pages and out under the home indicator: five tabs, the selected one wider and lit,
        // its icon lifted and bigger, its title showing. A focus section that comes back to the tab it left.
        private static List<Pressable> TabBar(UINode app)
        {
            var bar = app.Child("Tab Bar").Row().Pad(new Insets(4f, 4f, 5f, 5f)).Align(AlignX.Left, AlignY.Center);
            bar.Sized(Sizing.Grow(), Sizing.Fixed(76f));
            bar.IgnoresSafeArea.Value = Edges.Bottom;
            var box = bar.Box(new Color32(0x1E, 0x42, 0x8E, 0xFF));
            box.Gradient.Value = true;
            box.GradientTo.Value = new Color32(0x0C, 0x20, 0x52, 0xFF);
            box.BorderWidth.Value = 2f;
            box.BorderColor.Value = Ink;
            bar.FocusScope.Value = FocusScope.Section;
            bar.FocusEntry.Value = FocusEntry.Remembered;

            var tabs = new List<Pressable>();
            foreach (var (icon, title) in new[] { ("🛒", "Shop"), ("🃏", "Cards"), ("⚔️", "Battle"), ("🛡️", "Clan"), ("🎪", "Events") })
            {
                var tab = bar.Child(title).Center();
                tab.Sized(Sizing.Percent(0.18f), Sizing.Grow());
                tab.Width.When(InteractionStates.Selected, Sizing.Percent(0.28f));
                var lit = tab.Child("Lit").Float(AttachPoint.LeftTop, AttachPoint.LeftTop);
                lit.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
                Raised(lit, Blue.Top, Blue.Bottom, Ink, 14f, 2f);
                lit.Opacity.Value = 0f;
                lit.Opacity.When(InteractionStates.Selected, 1f);
                lit.HitTestable.Value = false;
                var iconNode = tab.Child("Icon");
                iconNode.Label(icon, 28f);
                iconNode.Scale.When(InteractionStates.Selected, 1.3f);
                iconNode.Offset.When(InteractionStates.Selected, new Vector2(0f, -8f));
                iconNode.Scale.When(InteractionStates.Pressed, 0.9f);
                var name = Text(tab, "Title", title, 13f);
                name.Node.Float(AttachPoint.CenterBottom, AttachPoint.CenterBottom, new Vector2(0f, -3f));
                name.Node.Opacity.Value = 0f;
                name.Node.Opacity.When(InteractionStates.Selected, 1f);
                tabs.Add(tab.Pressable());
            }
            // It opens on Battle.
            tabs[2].IsSelected = true;
            return tabs;
        }

        // ── Popups ───────────────────────────────────────────────────────────────

        // A popup's host and its card, a raised panel `width` wide in the middle of the screen over a dim, coming in by
        // `transition` (a card that grows out of something comes by its flight instead).
        private static Presentation Popup(UINode app, string name, float width, PresentationTransition transition, out UINode card)
        {
            var host = app.Child(name).Center();
            var presentation = host.Add<Presentation>();
            presentation.Transition = transition;
            presentation.Dim.Value = new Color(0f, 0.02f, 0.1f, 0.6f);
            presentation.Animation = new UIAnimation(0.45f, 0.15f);
            card = host.Child("Card").Column(12f).Pad(20f).Align(AlignX.Center, AlignY.Top);
            card.Width.Value = Sizing.Fixed(width);
            Raised(card, PanelTop, PanelBottom, PanelEdge, 22f, 5f);
            return presentation;
        }

        // The chest popup: the chest (named as the slots', which it grows out of), what it holds, and its buttons; and
        // where its rewards are revealed, one a tap. See ChestSlots. Returns its host.
        private static UINode ChestPopup(UINode app, ChestPopupParts popup)
        {
            popup.Presentation = Popup(app, "Chest Popup", 310f, PresentationTransition.Pop, out var card);
            popup.Close = CloseButton(card);
            popup.Title = Text(card, "Title", "Silver Chest", 24f);
            popup.Art = card.Child("Chest").Sized(Sizing.Fixed(150f), Sizing.Fixed(128f));
            popup.Art.MatchName.Value = "Chest";
            popup.Chest = popup.Art.Add<ChestArt>();

            popup.Details = card.Child("Details").Column(10f).Align(AlignX.Center, AlignY.Top);
            popup.Details.Width.Value = Sizing.Grow();
            popup.Contents = Text(popup.Details, "Contents", "", 17f);
            popup.UnlockTime = Text(popup.Details, "Unlock time", "", 15f, Gold);
            popup.Primary = Button(popup.Details, "Primary", "Start Unlock", Yellow, Sizing.Grow(), 54f, 20f, out popup.PrimaryLabel);
            popup.Gems = Button(popup.Details, "Gems", "Open now", Blue, Sizing.Grow(), 46f, 16f, out popup.GemsLabel);

            popup.Reward = card.Child("Reward").Column(6f).Align(AlignX.Center, AlignY.Top);
            popup.Reward.Width.Value = Sizing.Grow();
            popup.Reward.Display.Value = DisplayMode.None;
            popup.RewardIcon = popup.Reward.Child("Icon").Label("💰", 64f);
            popup.RewardName = Text(popup.Reward, "Name", "", 22f);
            popup.RewardCount = Text(popup.Reward, "Count", "", 28f, Gold);
            popup.Next = Button(popup.Reward, "Next", "Next", Green, Sizing.Fixed(180f), 50f, 20f, out popup.NextLabel);
            return popup.Presentation.Node;
        }

        // The card popup: the card (named as the deck's, which it grows out of), its name, rarity and level, its stats,
        // its copies towards the next level, and Upgrade. See CardsPage.
        private static void CardPopup(UINode app, CardPopupParts popup)
        {
            popup.Presentation = Popup(app, "Card Popup", 320f, PresentationTransition.Pop, out var card);
            popup.Close = CloseButton(card);
            var head = card.Child("Head").Row(16f).Align(AlignX.Left, AlignY.Center);
            head.Width.Value = Sizing.Grow();
            popup.Frame = CardFace(head, 0, 104f, out popup.FrameBox, out popup.Icon, out popup.Elixir);
            var info = head.Child("Info").Column(4f);
            info.Width.Value = Sizing.Grow();
            popup.Name = Text(info, "Name", "", 24f);
            popup.Rarity = Text(info, "Rarity", "", 15f);
            popup.Level = Text(info, "Level", "", 17f, Gold);

            var stats = card.Child("Stats").Column(8f).Pad(12f);
            stats.Width.Value = Sizing.Grow();
            stats.Box(Well, 12f);
            popup.Hitpoints = Stat(stats, "❤️  Hitpoints");
            popup.Damage = Stat(stats, "⚔️  Damage");

            var bar = Bar(card, 0.5f, Green.Bottom, 22f, "", out popup.Fill, out popup.FillBox, out popup.Progress);
            bar.Width.Value = Sizing.Grow();
            popup.Upgrade = Button(card, "Upgrade", "Upgrade", Green, Sizing.Grow(), 54f, 18f, out popup.UpgradeLabel);
        }

        // A stat's row: its name at the left, its value (returned) at the right.
        private static Label Stat(UINode stats, string name)
        {
            var row = stats.Child(name).Row(8f).Align(AlignX.Left, AlignY.Center);
            row.Width.Value = Sizing.Grow();
            Text(row, "Name", name, 15f).Node.Width.Value = Sizing.Grow();
            return Text(row, "Value", "", 17f);
        }

        // The shop's buy dialog: what an offer is, and Buy or Cancel. See ShopPage.
        private static void BuyDialog(UINode app, BuyDialogParts dialog)
        {
            dialog.Presentation = Popup(app, "Buy Dialog", 290f, PresentationTransition.Pop, out var card);
            dialog.Icon = card.Child("Icon").Label("💰", 56f);
            dialog.Title = Text(card, "Title", "", 22f);
            dialog.Detail = Text(card, "Detail", "", 14f);
            dialog.Detail.Alignment.Value = TextAnchor.UpperCenter;
            dialog.Buy = Button(card, "Buy", "Buy", Green, Sizing.Grow(), 50f, 18f, out dialog.BuyLabel);
            dialog.Cancel = Button(card, "Cancel", "Cancel", Blue, Sizing.Grow(), 44f, 16f);
        }

        // Looking for an opponent: turning swords, the time, and Cancel. See BattleSearch.
        private static void BattleSearch(UINode app, Parts parts)
        {
            var presentation = Popup(app, "Battle Search", 290f, PresentationTransition.Pop, out var card);
            var swords = card.Child("Swords").Sized(Sizing.Fixed(96f), Sizing.Fixed(96f)).Center();
            swords.Child("Icon").Label("⚔️", 64f);
            var status = Text(card, "Status", "", 20f);
            var time = Text(card, "Time", "0:00", 16f, Gold);
            var cancel = Button(card, "Cancel", "Cancel", Red, Sizing.Fixed(160f), 48f, 18f);
            presentation.Node.Add<BattleSearch>().Setup(parts.Battlers, cancel, swords, status, time);
        }
    }
}
