using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A story, opened full screen from its ring in the feed. The avatar in the ring and the story share a match name and
    /// the person's id, and the avatar is hidden as the story is presented and shown again as it is dismissed, so the
    /// story grows out of the avatar and shrinks back into it. A tap, back or a swipe down (following the finger) closes it.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class StoryViewer : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _stories = new();
        [SerializeField] private List<UINode> _ringAvatars = new();
        [SerializeField] private UINode _story;
        [SerializeField] private Box _background;
        [SerializeField] private Box _avatar;
        [SerializeField] private Label _initials;
        [SerializeField] private Label _name;

        private Presentation _presentation;
        private Pressable _tap;
        private int _open = -1;

        public void Setup(List<Pressable> stories, List<UINode> ringAvatars, UINode story, Box background, Box avatar, Label initials, Label name)
        {
            _stories = stories;
            _ringAvatars = ringAvatars;
            _story = story;
            _background = background;
            _avatar = avatar;
            _initials = initials;
            _name = name;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            _presentation.Presenting += HideRingAvatar;
            _presentation.Dismissing += ShowRingAvatar;
            for (int i = 0; i < _stories.Count; i++)
            {
                int index = i;
                _stories[i].Tapped += () => Open(index);
            }
            _tap = _story.GetComponent<Pressable>();
            if (_tap != null)
                _tap.Tapped += Close;
        }

        private void OnDisable()
        {
            _presentation.Presenting -= HideRingAvatar;
            _presentation.Dismissing -= ShowRingAvatar;
            if (_tap != null)
                _tap.Tapped -= Close;
        }

        /// <summary>Opens the <paramref name="index"/>th person's story, growing out of the avatar in their ring.</summary>
        public void Open(int index)
        {
            if (_presentation.IsPresented) return;
            var (name, initials, colour) = CircleData.People[index];
            _background.Fill.Value = colour;
            _background.GradientTo.Value = Color.Lerp(colour, Color.black, 0.55f);
            _avatar.Fill.Value = colour;
            _initials.Text.Value = initials;
            _name.Text.Value = name;
            _story.MatchId.Value = index.ToString();
            _open = index;
            _presentation.Present();
        }

        public void Close()
        {
            if (_presentation.IsPresented)
                _presentation.Dismiss();
        }

        // Inside the presentation's changes: the ring's avatar is hidden (keeping its place) as the story grows out of it,
        // and shown again as the story shrinks back into it.
        private void HideRingAvatar()
        {
            if (_open >= 0 && _open < _ringAvatars.Count)
                _ringAvatars[_open].Display.Value = DisplayMode.Hidden;
        }

        private void ShowRingAvatar()
        {
            if (_open >= 0 && _open < _ringAvatars.Count)
                _ringAvatars[_open].Display.Value = DisplayMode.Visible;
        }
    }
}
