using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// The reaction picker: a card of emoji that grows out of a post's + chip and goes back into it, the two sharing a
    /// match name under the post's id, which it is given as it opens, and the chip hiding while it is up. Picking one is
    /// one change that takes the card away and adds your reaction (or takes it back, as Discord's picker does; those that
    /// are yours are Selected in it): the cell picked is named as that emoji's chip on the post, so an emoji whose chip
    /// was out of the row flies out of the card into it as it joins the row.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class ReactionPicker : MonoBehaviour
    {
        [SerializeField] private UINode _card;
        [SerializeField] private List<Pressable> _cells = new();
        [SerializeField] private Pressable _close;

        private Presentation _presentation;

        // The post it is open for, or was last.
        private PostReactions _post;

        public void Setup(UINode card, List<Pressable> cells, Pressable close)
        {
            _card = card;
            _cells = cells;
            _close = close;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            for (int i = 0; i < _cells.Count; i++)
            {
                int emoji = i;
                _cells[i].Tapped += () => Pick(emoji);
            }
            _close.Tapped += Close;
            _presentation.Dismissing += ShowAdd;
        }

        private void OnDisable()
        {
            _close.Tapped -= Close;
            _presentation.Dismissing -= ShowAdd;
        }

        /// <summary>Opens for <paramref name="post"/>: the card grows out of its + chip.</summary>
        public void Open(PostReactions post)
        {
            if (_presentation.IsPresented) return;
            _post = post;
            _card.MatchId.Value = post.Post.ToString();
            for (int i = 0; i < _cells.Count; i++)
            {
                // Named as nothing on the post until picked, so nothing flies out of the post's chips as it opens.
                _cells[i].Node.MatchId.Value = "";
                _cells[i].IsSelected = post.IsMine(i);
            }
            UI.Animate(_presentation.Animation, () =>
            {
                post.AddButton.Node.Display.Value = DisplayMode.Hidden;
                _presentation.Present();
            });
        }

        public void Close() => _presentation.Dismiss();

        private void Pick(int emoji)
        {
            if (_post == null || !_presentation.IsPresented) return;
            var post = _post;
            _cells[emoji].Node.MatchId.Value = PostReactions.Key(post.Post, emoji);
            post.Pop(emoji);
            UI.Animate(_presentation.Animation, () =>
            {
                _presentation.Dismiss();
                post.React(emoji);
            });
        }

        // Inside the change that takes it away: the + chip comes back, taking over from the card.
        private void ShowAdd()
        {
            if (_post != null)
                _post.AddButton.Node.Display.Value = DisplayMode.Visible;
        }
    }
}
