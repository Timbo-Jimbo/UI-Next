using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// The stories along the top of the feed, and the viewer they open into, full screen. Each story's avatar in the tray
    /// and the viewer share a match name, under the story's index, and the avatar hides as the viewer comes and shows again
    /// as it goes, so the viewer grows out of the avatar and shrinks back into it. A tap on its right side goes on to the
    /// next story and on its left back one (left and right with keys, <see cref="StorySteps"/>), its colours turning to the
    /// next one's as the tray scrolls that one's avatar into view, so it closes into the right avatar; on from the last, it
    /// closes. A story seen has its ring greyed. Its close button, back or a swipe down (following the finger) closes it.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class StoryViewer : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _stories = new();
        [SerializeField] private List<UINode> _ringAvatars = new();
        [SerializeField] private List<Box> _rings = new();
        [SerializeField] private ScrollView _tray;
        [SerializeField] private Color _seen = Color.gray;
        [SerializeField] private UINode _story;
        [SerializeField] private Box _background;
        [SerializeField] private List<Box> _segments = new();
        [SerializeField] private Box _avatar;
        [SerializeField] private Label _initials;
        [SerializeField] private Label _name;
        [SerializeField] private Label _text;
        [SerializeField] private Pressable _close;
        [SerializeField] private Pressable _backSide;
        [SerializeField] private Pressable _onSide;
        [SerializeField] private Pressable _like;

        private Presentation _presentation;

        // The story open, or last open.
        private int _open = -1;

        /// <summary>Each story in the tray (the button opening it), its avatar and the ring round it, the tray, and a seen ring's colour.</summary>
        public void SetupTray(List<Pressable> stories, List<UINode> ringAvatars, List<Box> rings, ScrollView tray, Color seen)
        {
            _stories = stories;
            _ringAvatars = ringAvatars;
            _rings = rings;
            _tray = tray;
            _seen = seen;
        }

        /// <summary>
        /// The viewer: what is presented, its background (top to bottom), its progress segments, the avatar, initials, name
        /// and text it shows, its close button, the sides that step back and on, and its like.
        /// </summary>
        public void SetupViewer(UINode story, Box background, List<Box> segments, Box avatar, Label initials, Label name, Label text,
            Pressable close, Pressable backSide, Pressable onSide, Pressable like)
        {
            _story = story;
            _background = background;
            _segments = segments;
            _avatar = avatar;
            _initials = initials;
            _name = name;
            _text = text;
            _close = close;
            _backSide = backSide;
            _onSide = onSide;
            _like = like;
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
            _close.Tapped += Close;
            _backSide.Tapped += StepBack;
            _onSide.Tapped += StepOn;
        }

        private void OnDisable()
        {
            _presentation.Presenting -= HideRingAvatar;
            _presentation.Dismissing -= ShowRingAvatar;
            _close.Tapped -= Close;
            _backSide.Tapped -= StepBack;
            _onSide.Tapped -= StepOn;
        }

        /// <summary>Opens the <paramref name="index"/>th story, growing out of its avatar in the tray.</summary>
        public void Open(int index)
        {
            if (_presentation.IsPresented) return;
            _open = index;
            Show(index);
            _like.IsSelected = false;
            _presentation.Present();
        }

        /// <summary>One story on (1) or back (-1); on from the last, it closes.</summary>
        public void Step(int by)
        {
            if (!_presentation.IsPresented || _open < 0) return;
            int next = Mathf.Max(0, _open + by);
            if (next >= CircleData.Stories.Length)
            {
                Close();
                return;
            }
            if (next == _open) return;
            MarkSeen(_open);
            // It closes into the next one's avatar now: the two swap at once, outside any change, so neither flies.
            _ringAvatars[_open].Display.Value = DisplayMode.Visible;
            _ringAvatars[next].Display.Value = DisplayMode.Hidden;
            _open = next;
            UI.Animate(UIAnimation.Smooth, () =>
            {
                Show(next);
                _like.IsSelected = false;
            });
            _tray.ScrollIntoView(_stories[next].Node);
        }

        public void Close()
        {
            if (!_presentation.IsPresented) return;
            MarkSeen(_open);
            _presentation.Dismiss();
        }

        private void StepBack() => Step(-1);

        private void StepOn() => Step(1);

        // The viewer shows story `index`, under its id, so it closes into that one's avatar.
        private void Show(int index)
        {
            var (person, text, top, bottom) = CircleData.Stories[index];
            var (name, initials, colour, _, _) = CircleData.People[person];
            _story.MatchId.Value = index.ToString();
            _background.Fill.Value = top;
            _background.GradientTo.Value = bottom;
            _avatar.Fill.Value = colour;
            _initials.Text.Value = initials;
            _name.Text.Value = name;
            _text.Text.Value = text;
            for (int i = 0; i < _segments.Count; i++)
                _segments[i].Fill.Value = new Color(1f, 1f, 1f, i <= index ? 0.95f : 0.3f);
        }

        // A story seen has its ring greyed.
        private void MarkSeen(int index)
        {
            if (index < 0 || index >= _rings.Count) return;
            _rings[index].Gradient.Value = false;
            _rings[index].Fill.Value = _seen;
        }

        // Inside the presentation's changes: the tray's avatar hides as the story grows out of it, and shows again as the
        // story shrinks back into it.
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
