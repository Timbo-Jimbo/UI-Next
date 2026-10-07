using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Layout: tapping the box moves what is inside it to its next alignment, animated; tapping Add puts another chip
    /// in the wrapping row, popping in while the others make room.
    /// </summary>
    public sealed class LayoutDemo : MonoBehaviour
    {
        [SerializeField] private Pressable _alignBox;
        [SerializeField] private Pressable _add;
        [SerializeField] private UINode _chips;
        [SerializeField] private Color _chipColour = new(0.2f, 0.47f, 0.96f, 0.14f);

        private static readonly (AlignX, AlignY)[] Alignments =
        {
            (AlignX.Left, AlignY.Top), (AlignX.Center, AlignY.Top), (AlignX.Right, AlignY.Top),
            (AlignX.Right, AlignY.Center), (AlignX.Right, AlignY.Bottom), (AlignX.Center, AlignY.Bottom),
            (AlignX.Left, AlignY.Bottom), (AlignX.Left, AlignY.Center), (AlignX.Center, AlignY.Center),
        };

        private static readonly string[] Words = { "spring", "layout", "token", "state", "variant", "clip", "glyph", "gesture", "sheet" };

        private int _alignment = 8;
        private int _added;

        public void Setup(Pressable alignBox, Pressable add, UINode chips)
        {
            _alignBox = alignBox;
            _add = add;
            _chips = chips;
        }

        private void OnEnable()
        {
            var box = _alignBox.Node;
            _alignBox.Tapped += () =>
            {
                _alignment = (_alignment + 1) % Alignments.Length;
                var (x, y) = Alignments[_alignment];
                UI.Animate(UIAnimation.Snappy, () => box.Align(x, y));
            };
            _add.Tapped += AddChip;
        }

        private void AddChip()
        {
            var chip = _chips.Child("Chip").Pad(12f, 6f);
            chip.Box(_chipColour, 14f);
            chip.Child("Text").Label(Words[_added++ % Words.Length], 15f, new Color(0.2f, 0.47f, 0.96f));
            chip.Scale.Value = 0.3f;
            chip.Scale.Settle();
            chip.Opacity.Value = 0f;
            chip.Opacity.Settle();
            UI.LayoutNow();
            UI.Animate(UIAnimation.Bouncy, () =>
            {
                chip.Scale.Value = 1f;
                chip.Opacity.Value = 1f;
            });
        }
    }
}
