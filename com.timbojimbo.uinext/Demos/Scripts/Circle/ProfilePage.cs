using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A person's profile, pushed over the feed from a post's author: the author's avatar and name share match names with
    /// the page's, under the post's id, and hide as the page comes, so they rise out of the post into the page's header as
    /// it slides in, and fly back into the post as it is swiped, backed or tapped away. Its photos are the person's,
    /// painted as it opens, and open into the viewer as the feed's do; Follow follows them, or stops.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class ProfilePage : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _authors = new();
        [SerializeField] private List<UINode> _avatars = new();
        [SerializeField] private List<UINode> _names = new();
        [SerializeField] private UINode _content;
        [SerializeField] private ScrollView _scroll;
        [SerializeField] private Pressable _back;
        [SerializeField] private Box _avatar;
        [SerializeField] private Label _initials;
        [SerializeField] private Label _name;
        [SerializeField] private Label _handle;
        [SerializeField] private Label _bio;
        [SerializeField] private Pressable _follow;
        [SerializeField] private Label _followLabel;
        [SerializeField] private List<ScenePhoto> _photos = new();
        [SerializeField] private PhotoViewer _viewer;

        private Presentation _presentation;
        private readonly HashSet<int> _following = new();

        // The post whose author is open, or was last.
        private int _open = -1;

        /// <summary>Each post's author (the button opening their profile), and its avatar and name.</summary>
        public void SetupPosts(List<Pressable> authors, List<UINode> avatars, List<UINode> names)
        {
            _authors = authors;
            _avatars = avatars;
            _names = names;
        }

        /// <summary>
        /// The page: what is presented, its scroll view and back button; the avatar, initials, name, handle and bio in its
        /// header; its follow button and label; its photos, and the viewer they open into.
        /// </summary>
        public void SetupPage(UINode content, ScrollView scroll, Pressable back, Box avatar, Label initials, Label name, Label handle,
            Label bio, Pressable follow, Label followLabel, List<ScenePhoto> photos, PhotoViewer viewer)
        {
            _content = content;
            _scroll = scroll;
            _back = back;
            _avatar = avatar;
            _initials = initials;
            _name = name;
            _handle = handle;
            _bio = bio;
            _follow = follow;
            _followLabel = followLabel;
            _photos = photos;
            _viewer = viewer;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            for (int i = 0; i < _authors.Count; i++)
            {
                int post = i;
                _authors[i].Tapped += () => Open(post);
            }
            for (int i = 0; i < _photos.Count; i++)
            {
                int index = i;
                var tap = _photos[i].GetComponent<Pressable>();
                if (tap != null)
                    tap.Tapped += () => OpenPhoto(index);
            }
            _back.Tapped += Close;
            _follow.Tapped += Follow;
            _presentation.Presenting += HideAuthor;
            _presentation.Dismissing += ShowAuthor;
        }

        private void OnDisable()
        {
            _back.Tapped -= Close;
            _follow.Tapped -= Follow;
            _presentation.Presenting -= HideAuthor;
            _presentation.Dismissing -= ShowAuthor;
        }

        /// <summary>Opens the author of the <paramref name="post"/>th post: their avatar and name rise into the page.</summary>
        public void Open(int post)
        {
            if (_presentation.IsPresented) return;
            int who = CircleData.Posts[post].Person;
            var (name, initials, colour, handle, bio) = CircleData.People[who];
            _avatar.Fill.Value = colour;
            _initials.Text.Value = initials;
            _name.Text.Value = name;
            _handle.Text.Value = handle;
            _bio.Text.Value = bio;
            for (int i = 0; i < _photos.Count; i++)
                _photos[i].Show(CircleData.ProfileScene(who, i));
            ShowFollowing(_following.Contains(who));
            // Matched to the post it opens from, and at its top.
            _content.MatchId.Value = post.ToString();
            _scroll.ScrollToTop(animated: false);
            _open = post;
            _presentation.Present();
        }

        public void Close() => _presentation.Dismiss();

        private void Follow()
        {
            if (_open < 0) return;
            int who = CircleData.Posts[_open].Person;
            if (!_following.Add(who))
                _following.Remove(who);
            UI.Animate(UIAnimation.Snappy, () => ShowFollowing(_following.Contains(who)));
        }

        // Followed, the button is Selected (its look follows) and says so.
        private void ShowFollowing(bool following)
        {
            _follow.IsSelected = following;
            _followLabel.Text.Value = following ? "Following" : "Follow";
        }

        private void OpenPhoto(int index)
        {
            if (_open < 0) return;
            var name = CircleData.People[CircleData.Posts[_open].Person].Name;
            _viewer.Open(_photos[index], CircleData.FirstName(name) + "'s photo", "profile " + index);
        }

        // Inside the presentation's changes: the post's avatar and name hide as the page's rise out of them, and show
        // again as they fly back into them.
        private void HideAuthor() => ShowAuthor(false);

        private void ShowAuthor() => ShowAuthor(true);

        private void ShowAuthor(bool shown)
        {
            if (_open < 0) return;
            var display = shown ? DisplayMode.Visible : DisplayMode.Hidden;
            _avatars[_open].Display.Value = display;
            _names[_open].Display.Value = display;
        }
    }
}
