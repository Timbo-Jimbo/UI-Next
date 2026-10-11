using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A post's reactions, as Discord's: a chip for each reaction it has (the emoji and how many), wrapping in a row, and a
    /// + chip at the end that opens the <see cref="ReactionPicker"/>. Tapping a chip adds your reaction or takes it back,
    /// inside one change: the chip pops and is Selected while it is yours (its look follows). A chip whose count comes to
    /// nothing leaves the row, and one given its first reaction joins it, coming and going by its DisplayTransition as
    /// the row closes up and opens around it. Every emoji has a chip, out of layout while nobody has reacted with it, named
    /// for the picker's cells to fly into (<see cref="Key"/>).
    /// </summary>
    public sealed class PostReactions : MonoBehaviour
    {
        [SerializeField] private int _post;
        [SerializeField] private List<Pressable> _chips = new();
        [SerializeField] private List<Label> _counts = new();
        [SerializeField] private List<int> _startCounts = new();
        [SerializeField] private Pressable _add;
        [SerializeField] private ReactionPicker _picker;

        private int[] _count;
        private bool[] _mine;

        /// <summary>The post's index.</summary>
        public int Post => _post;

        /// <summary>The + chip, which the picker grows out of.</summary>
        public Pressable AddButton => _add;

        /// <summary>The match id of an emoji's chip on a post, which the picker's cell for it takes as it is picked.</summary>
        public static string Key(int post, int emoji) => post + ":" + emoji;

        /// <summary>The post's index; each emoji's chip, its count and how many it starts with; the + chip and the picker it opens.</summary>
        public void Setup(int post, List<Pressable> chips, List<Label> counts, List<int> startCounts, Pressable add, ReactionPicker picker)
        {
            _post = post;
            _chips = chips;
            _counts = counts;
            _startCounts = startCounts;
            _add = add;
            _picker = picker;
        }

        private void Awake()
        {
            _count = _startCounts.ToArray();
            _mine = new bool[_count.Length];
        }

        private void OnEnable()
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                int emoji = i;
                _chips[i].Tapped += () => Toggle(emoji);
            }
            _add.Tapped += OpenPicker;
        }

        private void OnDisable() => _add.Tapped -= OpenPicker;

        /// <summary>Whether you have reacted with <paramref name="emoji"/>.</summary>
        public bool IsMine(int emoji) => _mine[emoji];

        private void OpenPicker() => _picker.Open(this);

        // A chip tapped: your reaction added or taken back, popping.
        private void Toggle(int emoji)
        {
            if (_chips[emoji].Node.Display.Peek != DisplayMode.Visible) return;
            Pop(emoji);
            UI.Animate(UIAnimation.Bouncy, () => React(emoji));
        }

        /// <summary>
        /// Puts a chip in the row a little smaller at once, for the change that follows to spring it back: called before
        /// that change, for a chip in the row (one joining it comes in by its DisplayTransition instead).
        /// </summary>
        public void Pop(int emoji)
        {
            var node = _chips[emoji].Node;
            if (node.Display.Peek != DisplayMode.Visible) return;
            node.Scale.Value = 0.85f;
            node.Scale.Settle();
        }

        /// <summary>
        /// Adds your reaction with <paramref name="emoji"/>, or takes it back, inside the change that shows it: its chip
        /// joins the row with its first reaction and leaves it with its last.
        /// </summary>
        public void React(int emoji)
        {
            bool mine = !_mine[emoji];
            _mine[emoji] = mine;
            _count[emoji] += mine ? 1 : -1;
            var chip = _chips[emoji];
            chip.IsSelected = mine;
            chip.Node.Scale.Value = 1f;
            chip.Node.Display.Value = _count[emoji] > 0 ? DisplayMode.Visible : DisplayMode.None;
            _counts[emoji].Text.Value = _count[emoji].ToString();
        }
    }
}
