using System.Collections.Generic;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos;
using TimboJimbo.UINext.Demos.Circle;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class CircleBuilder
    {
        // ── Me ───────────────────────────────────────────────────────────────────

        // Me: who you are and your numbers, Share profile (named as the share card, which grows out of it), the settings
        // row (its title named as the settings page's, which rises out of it), and your photos, which open into the viewer.
        private static ScrollView Me(UINode pages, Parts parts)
        {
            var me = TabPage(pages, "Me").Column(16f).Pad(new Insets(16f, 16f, 16f, 120f));
            var scroll = me.Scroll();
            var mine = CircleData.Me;

            var who = me.Child("Who").Column(6f).Pad(new Insets(0f, 0f, 8f, 4f)).Align(AlignX.Center, AlignY.Top);
            who.Width.Value = Sizing.Grow();
            DemoKit.Avatar(who, 88f, mine.Colour, mine.Initials);
            DemoKit.Text(who, "Name", mine.Name, 22f, s_text, bold: true);
            DemoKit.Text(who, "Handle", mine.Handle, 15f, s_secondary);
            DemoKit.Text(who, "Bio", mine.Bio, 15f, s_text).Alignment.Value = TextAnchor.UpperCenter;

            var stats = me.Child("Stats").Row();
            stats.Width.Value = Sizing.Grow();
            foreach (var (count, what) in new[] { ("128", "Posts"), ("1.2k", "Followers"), ("348", "Following") })
            {
                var stat = stats.Child(what).Column(2f).Align(AlignX.Center, AlignY.Top);
                stat.Width.Value = Sizing.Grow();
                DemoKit.Text(stat, "Count", count, 18f, s_text, bold: true);
                DemoKit.Text(stat, "What", what, 12f, s_secondary);
            }

            // Named as the share card, which grows out of it.
            parts.Share = WideButton(me, "Share profile", quiet: true);
            parts.Share.Node.MatchName.Value = "Share";

            // The settings row: its title is named as the settings page's, which rises out of it.
            var row = me.Child("Settings").Row(12f).Pad(14f, 12f).Align(AlignX.Left, AlignY.Center);
            row.Width.Value = Sizing.Grow();
            DemoKit.Fill(row, s_card, 16f).Fill.When(InteractionStates.Pressed, Pressed);
            var icon = row.Child("Icon").Sized(Sizing.Fixed(30f), Sizing.Fixed(30f)).Center();
            icon.Box(Accent, 8f);
            icon.Child("Glyph").Label("⚙", 17f, Color.white);
            var title = DemoKit.Text(row, "Title", "Settings", 17f, s_text);
            title.Node.MatchName.Value = "Settings title";
            row.Child("Space").Width.Value = Sizing.Grow();
            DemoKit.Text(row, "Chevron", "›", 22f, s_secondary);
            parts.SettingsRow = row.Pressable();
            parts.SettingsTitle = title.Node;

            DemoKit.Text(me, "Photos title", "Your photos", 17f, s_text, bold: true);
            var grid = me.Child("Photos").Row(4f);
            grid.Width.Value = Sizing.Grow();
            grid.Wrap.Value = Wrap.Grid(3);
            for (int i = 0; i < 9; i++)
                parts.MyPhotos.Add(GridPhoto(grid, i, "me " + i));
            return scroll;
        }

        // ── Share ────────────────────────────────────────────────────────────────

        // The share card: named as the Share profile button, which it grows out of and goes back into. Your avatar, name
        // and handle over a code to scan, and Done.
        private static void Share(UINode app, Parts parts)
        {
            var presentation = CardHost(app, "Share", "Share", 300f, out var card);
            card.Align(AlignX.Center, AlignY.Top);
            var mine = CircleData.Me;
            DemoKit.Avatar(card, 64f, mine.Colour, mine.Initials);
            var who = card.Child("Who").Column(2f).Align(AlignX.Center, AlignY.Top);
            DemoKit.Text(who, "Name", mine.Name, 19f, s_text, bold: true);
            DemoKit.Text(who, "Handle", mine.Handle, 14f, s_secondary);

            // A code to scan: a grid of squares on white, with a finder in three corners.
            const int cells = 9;
            var code = card.Child("Code").Sized(Sizing.Fixed(196f), Sizing.Fixed(196f)).Row(2f).Pad(14f);
            code.Wrap.Value = Wrap.Grid(cells);
            code.Box(Color.white, 18f);
            for (int y = 0; y < cells; y++)
            {
                for (int x = 0; x < cells; x++)
                {
                    var cell = code.Child("Cell");
                    cell.Width.Value = Sizing.Grow();
                    cell.AspectRatio.Value = 1f;
                    if (CodeCell(x, y, cells))
                        cell.Box(new Color32(0x12, 0x12, 0x16, 0xFF), 2f);
                }
            }

            var done = WideButton(card, "Done");
            done.Node.PrefersFocus.Value = true;
            presentation.Node.Add<PresentFrom>().Setup(parts.Share, new List<Pressable> { done });
        }

        // Whether a cell of the code is dark: a solid finder in three corners, a light border round each, and a scatter
        // elsewhere.
        private static bool CodeCell(int x, int y, int cells)
        {
            bool left = x < 3, right = x >= cells - 3, top = y < 3, bottom = y >= cells - 3;
            if ((left && top) || (right && top) || (left && bottom)) return true;
            bool besideFinder = (x < 4 && y < 4) || (x >= cells - 4 && y < 4) || (x < 4 && y >= cells - 4);
            if (besideFinder) return false;
            return (x * 7 + y * 11 + x * y) % 3 == 0;
        }

        // ── Settings ─────────────────────────────────────────────────────────────

        // Settings, pushed from its row on Me, its title rising out of the row's as the page slides in, with a setting of
        // each kind, in groups: a text field, switches, a segmented control (the theme, which puts the app in Dark mode), a
        // stepper, swatches, a slider, a switch that shows a row, a list with a check mark, a storage bar Clear cache
        // empties, and Log out, whose dialog grows out of it. Each takes focus, and the slider and the stepper take left
        // and right. See SettingsPage.
        private static void Settings(UINode app, Parts parts, out Pressable logOut)
        {
            var presentation = PushedPage(app, "Settings", out _, out var back, out var body, out _);
            var title = DemoKit.Text(body, "Title", "Settings", 32f, s_text, bold: true);
            title.Node.MatchName.Value = "Settings title";

            var account = SettingsGroup(body, "Account");
#if UINEXT_TEXTINPUT
            var nameRow = SettingRow(account, "Name", grows: false);
            var input = nameRow.Child("Field");
            input.Width.Value = Sizing.Grow();
            var field = input.TextField("Your name", 16f, padding: Insets.Symmetric(12f, 8f));
            input.ClipRadii.Value = 10f;
            DemoKit.Fill(input, s_subtle, 10f);
            field.Color.Token = s_text;
            field.PlaceholderColor.Token = s_secondary;
            field.CaretColor.Value = Accent;
            field.SelectionColor.Value = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
            field.Text = CircleData.Me.Name;
            // Its fill shows focus, rather than the ring.
            input.FocusRing.Value = false;
            input.GetComponent<Box>().BorderColor.Value = Accent;
            input.GetComponent<Box>().BorderWidth.When(InteractionStates.Focused, 1.5f);
#endif
            Switch(SettingRow(account, "Private account"));

            var appearance = SettingsGroup(body, "Appearance");
            var theme = Segmented(SettingRow(appearance, "Theme"), new[] { "Light", "Dark" }, 0);
            StepperRow(appearance, "Text size", new List<string> { "Small", "Default", "Large", "Larger" }, 1);
            Swatches(SettingRow(appearance, "Accent"));

            var notifications = SettingsGroup(body, "Notifications");
            Switch(SettingRow(notifications, "Notifications"), on: true);
            var volume = SettingRow(notifications, "Volume", grows: false);
            DemoKit.Text(volume, "Quiet", "🔈", 15f, s_text);
            DemoKit.Slider(volume, 0.6f, Accent);
            DemoKit.Text(volume, "Loud", "🔊", 15f, s_text);
            var quiet = Switch(SettingRow(notifications, "Quiet hours"));
            // Shown while quiet hours are on.
            var quietTimes = notifications.Child("Quiet times").Pad(new Insets(0f, 0f, 0f, 12f));
            quietTimes.Width.Value = Sizing.Grow();
            DemoKit.Text(quietTimes, "Times", "From 22:00 to 7:00, notifications wait until morning.", 14f, s_secondary).Node.Width.Value = Sizing.Grow();
            quietTimes.Display.Value = DisplayMode.None;
            quietTimes.Add<DisplayTransition>().Scale = 0.95f;

            var videos = SettingsGroup(body, "Autoplay videos");
            var choices = new List<Pressable>();
            foreach (var choice in new[] { "Always", "On Wi-Fi only", "Never" })
            {
                var row = SettingRow(videos, choice);
                row.Box(Color.clear).Fill.When(InteractionStates.Pressed, Pressed);
                // A check mark on the one chosen: its row is Selected.
                var check = row.Child("Check").Label("✓", 18f, Accent, bold: true);
                check.Node.Opacity.Value = 0f;
                check.Node.Opacity.When(InteractionStates.Selected, 1f);
                choices.Add(row.Pressable());
            }
            videos.Add<SingleChoice>().Setup(choices, 1);

            var storage = SettingsGroup(body, "Storage");
            var usedRow = SettingRow(storage, "Used");
            var used = DemoKit.Text(usedRow, "Amount", "2.4 GB of 5 GB", 15f, s_secondary);
            var barRow = storage.Child("Bar").Pad(new Insets(0f, 0f, 2f, 10f));
            barRow.Width.Value = Sizing.Grow();
            var bar = barRow.Child("Track").Sized(Sizing.Grow(), Sizing.Fixed(8f));
            bar.Box(new Color(0.47f, 0.47f, 0.5f, 0.2f), 4f);
            var fill = bar.Child("Fill").Sized(Sizing.Percent(0.48f), Sizing.Grow());
            fill.Box(Accent, 4f);
            var clearRow = storage.Child("Clear cache").Row().Pad(0f, 12f).Align(AlignX.Left, AlignY.Center);
            clearRow.Width.Value = Sizing.Grow();
            clearRow.Box(Color.clear).Fill.When(InteractionStates.Pressed, Pressed);
            clearRow.Child("Title").Label("Clear cache", 16f, Accent);
            var clear = clearRow.Pressable();

            // Log out, whose dialog grows out of it: named alike, and its label as the dialog's title.
            var logOutNode = body.Child("Log out").Center();
            logOutNode.Sized(Sizing.Grow(), Sizing.Fixed(50f));
            logOutNode.MatchName.Value = "Log out";
            logOutNode.ClipRadii.Value = 14f;
            DemoKit.Fill(logOutNode, s_card, 14f).Fill.When(InteractionStates.Pressed, Pressed);
            logOutNode.Child("Title").Label("Log out", 17f, Red, bold: true).Node.MatchName.Value = "Log out title";
            logOut = logOutNode.Pressable();

            DemoKit.Text(body, "Footer", "Circle 1.0, a UI Next demo", 13f, s_secondary);

            var host = presentation.Node;
            host.Add<PresentFrom>().Setup(parts.SettingsRow, new List<Pressable> { back }, parts.SettingsTitle);
            host.Add<SettingsPage>().Setup(parts.App, theme, quiet, quietTimes, clear, fill, used);
        }

        // A group of settings: its name over a card holding its rows.
        private static UINode SettingsGroup(UINode body, string name)
        {
            var group = body.Child(name).Column(6f);
            group.Width.Value = Sizing.Grow();
            DemoKit.Text(group, "Caption", name.ToUpperInvariant(), 12f, s_secondary, bold: true).Node.Padding.Value = new Insets(16f, 0f, 0f, 0f);
            var card = group.Child("Card").Column().Pad(16f, 4f);
            card.Width.Value = Sizing.Grow();
            DemoKit.Fill(card, s_card, 16f);
            return card;
        }

        // A setting's row: its name, growing to push what sets it to the row's end (unless told not to), and room after it.
        private static UINode SettingRow(UINode group, string name, bool grows = true)
        {
            var row = group.Child(name).Row(12f).Pad(0f, 8f).Align(AlignX.Left, AlignY.Center);
            row.Sized(Sizing.Grow(), Sizing.Fit(46f));
            var label = DemoKit.Text(row, "Name", name, 16f, s_text);
            if (grows)
                label.Node.Width.Value = Sizing.Grow();
            return row;
        }

        // A switch: its knob springs across its track as it is turned on or off (ToggleSwitch).
        private static ToggleSwitch Switch(UINode parent, bool on = false)
        {
            var track = parent.Child("Switch").Sized(Sizing.Fixed(51f), Sizing.Fixed(31f)).Pad(2f)
                .Align(on ? AlignX.Right : AlignX.Left, AlignY.Center);
            var trackBox = track.Box(new Color(0.47f, 0.47f, 0.5f, 0.32f), 15.5f);
            trackBox.Fill.When(InteractionStates.Selected, Green);
            var knob = track.Child("Knob").Sized(Sizing.Fixed(27f), Sizing.Fixed(27f));
            DemoKit.Shadow(knob.Box(Color.white, 13.5f), 0.2f, 4f, 2f);
            knob.HitTestable.Value = false;
            var pressable = track.Pressable();
            pressable.Toggles = true;
            pressable.IsSelected = on;
            return track.Add<ToggleSwitch>();
        }

        // A segmented control: a pill on the segment chosen, springing across as another is. See SingleChoice.
        private static SingleChoice Segmented(UINode parent, string[] titles, int chosen)
        {
            var control = parent.Child("Segments").Row().Pad(3f);
            DemoKit.Fill(control, s_subtle, 10f);
            var pill = control.Child("Pill");
            DemoKit.Shadow(DemoKit.Fill(pill, s_card, 8f), 0.12f, 4f, 1f);
            var options = new List<Pressable>();
            foreach (var title in titles)
            {
                var segment = control.Child(title).Sized(Sizing.Fixed(72f), Sizing.Fixed(30f)).Center();
                DemoKit.Text(segment, "Title", title, 14f, s_secondary, bold: true).Color.When(InteractionStates.Selected, Accent);
                options.Add(segment.Pressable());
            }
            pill.FloatOn(options[chosen].Node);
            pill.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            var choice = control.Add<SingleChoice>();
            choice.Setup(options, chosen, pill);
            return choice;
        }

        // A setting stepped through a few values: its row is one focus stop, left and right stepping it, and the arrows
        // either side, for the pointer, take no focus. See Stepper.
        private static void StepperRow(UINode group, string name, List<string> values, int value)
        {
            var row = SettingRow(group, name);
            row.Focusable.Value = Focusability.Always;
            var less = StepperArrow(row, "Less", "‹");
            var label = DemoKit.Text(row, "Value", values[value], 15f, s_text, bold: true);
            label.Node.Width.Value = Sizing.Fixed(68f);
            label.Alignment.Value = TextAnchor.UpperCenter;
            var more = StepperArrow(row, "More", "›");
            row.Add<Stepper>().Setup(label, values, value, less, more);
        }

        private static Pressable StepperArrow(UINode row, string name, string glyph)
        {
            var arrow = row.Child(name).Sized(Sizing.Fixed(30f), Sizing.Fixed(30f)).Center();
            DemoKit.Fill(arrow, s_subtle, 15f).Fill.When(InteractionStates.Pressed, Pressed);
            DemoKit.Text(arrow, "Glyph", glyph, 18f, s_text, bold: true);
            arrow.Focusable.Value = Focusability.Never;
            return arrow.Pressable();
        }

        // Swatches: a colour chosen of a few, ringed while it is chosen. See SingleChoice.
        private static void Swatches(UINode parent)
        {
            var swatches = parent.Child("Swatches").Row(8f).Align(AlignX.Left, AlignY.Center);
            var options = new List<Pressable>();
            foreach (var colour in new Color[]
                     {
                         Accent, new Color32(0xFF, 0x7E, 0xB6, 0xFF), new Color32(0xF2, 0xA9, 0x3B, 0xFF), new Color32(0x3F, 0xC1, 0xC9, 0xFF),
                         new Color32(0xA7, 0x7B, 0xFF, 0xFF),
                     })
            {
                var swatch = swatches.Child("Swatch").Sized(Sizing.Fixed(26f), Sizing.Fixed(26f));
                var box = swatch.Box(colour, 13f);
                box.BorderColor.Value = Color.white;
                box.BorderWidth.When(InteractionStates.Selected, 3f);
                options.Add(swatch.Pressable());
            }
            swatches.Add<SingleChoice>().Setup(options, 0);
        }

        // ── Log out ──────────────────────────────────────────────────────────────

        // The log out dialog: named as the Log out button in Settings, which it grows out of and goes back into (its title
        // out of the button's label), and thrown away by a drag anywhere in it, following the finger.
        private static void LogOutDialog(UINode app, Pressable logOut)
        {
            var host = app.Child("Log Out Dialog").Center();
            var presentation = host.Add<Presentation>();
            // It grows out of the Log out button by name: the match is its transition.
            presentation.Transition = PresentationTransition.None;
            presentation.Animation = new UIAnimation(0.4f, 0.1f);
            var alert = host.Child("Alert").Column(14f).Pad(22f).Align(AlignX.Center, AlignY.Top);
            alert.Width.Value = Sizing.Fixed(290f);
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
            var buttons = new List<Pressable>();
            foreach (var (label, colour, fill) in new[] { ("Cancel", (Color?)null, true), ("Log out", (Color?)Color.white, false) })
            {
                var button = row.Child(label).Center().Sized(Sizing.Grow(), Sizing.Fixed(46f));
                var box = fill ? DemoKit.Fill(button, s_subtle, 12f) : button.Box(Red, 12f);
                box.Fill.When(InteractionStates.Pressed, fill ? new Color(0.5f, 0.5f, 0.55f, 0.3f) : Darker(Red));
                if (colour.HasValue) button.Child("Title").Label(label, 16f, colour.Value, bold: true);
                else DemoKit.Text(button, "Title", label, 16f, s_text, bold: true);
                buttons.Add(button.Pressable());
            }
            // Thrown away by a drag anywhere in it, on the alert or the dim around it, following the finger.
            host.Add<Draggable>().Axis = ScrollAxis.Both;
            host.Add<SwipeToDismiss>().Setup(presentation, SwipeDirections.Any, panel: alert);
            host.Add<PresentFrom>().Setup(logOut, buttons);
        }
    }
}
