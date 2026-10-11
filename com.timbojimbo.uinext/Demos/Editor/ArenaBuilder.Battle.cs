using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Arena;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    public static partial class ArenaBuilder
    {
        // ── Battle ───────────────────────────────────────────────────────────────

        // Battle: your trophies and arena, the trophy road's progress to its next reward, the arena, 2v2 and the big
        // Battle button (both look for an opponent), and the four chest slots, each opening into the chest popup.
        private static void Battle(UINode pages, Parts parts)
        {
            var page = Page(pages, "Battle").Column(10f).Pad(new Insets(12f, 12f, 6f, 10f)).Align(AlignX.Center, AlignY.Top);
            // It fits the screen: nothing to scroll back to the top.
            parts.PageScrolls.Add(null);

            var trophies = page.Child("Trophies").Row(6f).Pad(14f, 4f).Align(AlignX.Center, AlignY.Center);
            trophies.Box(Well, 18f);
            trophies.Child("Icon").Label("🏆", 22f);
            Text(trophies, "Count", ArenaData.Number(ArenaData.Trophies), 22f, Gold);
            Text(page, "Arena", ArenaData.ArenaName, 14f);

            // The trophy road: how far to its next reward.
            var road = page.Child("Trophy Road").Row(10f).Pad(12f, 8f).Align(AlignX.Left, AlignY.Center);
            road.Width.Value = Sizing.Grow();
            Panel(road, 12f);
            Text(road, "Title", "Trophy Road", 13f);
            const int from = 4000;
            float progress = (float)(ArenaData.Trophies - from) / (ArenaData.NextReward - from);
            var bar = Bar(road, progress, Gold, 16f, ArenaData.Number(ArenaData.Trophies) + "/" + ArenaData.Number(ArenaData.NextReward),
                out _, out _, out _);
            bar.Width.Value = Sizing.Grow();
            road.Child("Reward").Label("🎁", 22f);

            var arena = page.Child("Arena").Sized(Sizing.Grow(), Sizing.Grow());
            arena.Add<ArenaArt>();
            var arenaFrame = arena.Child("Frame").Float(AttachPoint.LeftTop, AttachPoint.LeftTop);
            arenaFrame.Sized(Sizing.Percent(1f), Sizing.Percent(1f));
            var frameBox = arenaFrame.Box(Color.clear, 18f);
            frameBox.BorderWidth.Value = 3f;
            frameBox.BorderColor.Value = Ink;
            arenaFrame.HitTestable.Value = false;

            var buttons = page.Child("Buttons").Row(10f).Align(AlignX.Center, AlignY.Center);
            buttons.Width.Value = Sizing.Grow();
            parts.Battlers.Add(Button(buttons, "2v2", "2v2", Blue, Sizing.Fixed(92f), 66f, 22f));
            parts.Battlers.Add(Button(buttons, "Battle", "Battle", Yellow, Sizing.Grow(), 74f, 32f));

            var slots = page.Child("Chest Slots").Row(8f);
            slots.Sized(Sizing.Grow(), Sizing.Fixed(112f));
            for (int i = 0; i < 4; i++)
                parts.Slots.Add(ChestSlot(slots, i));
        }

        // A chest slot: its chest (named for the popup to grow out of) over a glow lit when it is ready, what it is doing,
        // and its time. ChestSlots fills it.
        private static ChestSlotParts ChestSlot(UINode slots, int index)
        {
            var slot = slots.Child("Slot " + (index + 1)).Column(1f).Pad(new Insets(4f, 4f, 6f, 4f)).Align(AlignX.Center, AlignY.Top);
            slot.Sized(Sizing.Grow(), Sizing.Grow());
            Raised(slot, new Color32(0x30, 0x60, 0xC0, 0xFF), new Color32(0x1A, 0x3B, 0x86, 0xFF), PanelEdge, 12f, 3f);
            slot.Scale.When(InteractionStates.Pressed, 0.94f);

            var glow = slot.Child("Glow").Float(AttachPoint.CenterCenter, AttachPoint.CenterTop, new Vector2(0f, 32f));
            glow.Sized(Sizing.Fixed(70f), Sizing.Fixed(58f));
            var glowBox = glow.Box(new Color(1f, 0.93f, 0.4f, 0.75f), 29f);
            glowBox.ShadowColor.Value = new Color(1f, 0.85f, 0.2f, 0.9f);
            glowBox.ShadowBlur.Value = 14f;
            glowBox.ShadowOffset.Value = Vector2.zero;
            glow.Opacity.Value = 0f;
            glow.HitTestable.Value = false;

            var art = slot.Child("Chest").Sized(Sizing.Fixed(60f), Sizing.Fixed(52f));
            art.MatchName.Value = "Chest";
            art.MatchId.Value = "slot " + index;
            var chest = art.Add<ChestArt>();
            var status = Text(slot, "Status", "", 11f);
            var time = Text(slot, "Time", "", 13f, Gold);
            return new ChestSlotParts
            {
                Button = slot.Pressable(),
                Art = art,
                Chest = chest,
                Status = status,
                Time = time,
                Glow = glow,
            };
        }
    }
}
