using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// Posting from the compose card, which grows out of the compose button (<see cref="PresentFrom"/>). The draft on the
    /// card and the text of the post it becomes, at the top of the feed, share a match name: Post is one change that takes
    /// the card away, back into the compose button, and shows the post, whose text takes over from the draft, flying out
    /// of the card into the feed as the feed scrolls back to its top to meet it. Posted once, the post stays, the draft
    /// names nothing more (opened again, it does not fly out of the post it became), and posting again only puts the card
    /// away.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class ComposeCard : MonoBehaviour
    {
        [SerializeField] private Pressable _post;
        [SerializeField] private UINode _draft;
        [SerializeField] private UINode _yourPost;
        [SerializeField] private ScrollView _feed;

        private Presentation _presentation;

        /// <summary>The Post button, the draft on the card, the post it becomes (out of the feed until posted), and the feed.</summary>
        public void Setup(Pressable post, UINode draft, UINode yourPost, ScrollView feed)
        {
            _post = post;
            _draft = draft;
            _yourPost = yourPost;
            _feed = feed;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            _post.Tapped += Post;
        }

        private void OnDisable() => _post.Tapped -= Post;

        private void Post()
        {
            if (!_presentation.IsPresented) return;
            bool first = _yourPost.Display.Peek != DisplayMode.Visible;
            var change = UI.Animate(_presentation.Animation, () =>
            {
                _presentation.Dismiss();
                if (!first) return;
                _yourPost.Display.Value = DisplayMode.Visible;
                _feed.ScrollToTop();
            });
            if (first)
                change.Finished += () => _draft.MatchName.Value = "";
        }
    }
}
