using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// App Store: Today cards that open into full-screen stories. A card and the story share a match name, the story taking
    /// the card's id as it opens, and so do their titles; the card is hidden (keeping its place) as the story is presented.
    /// A pull down past the story's top shrinks it in place (its scroll view hands the drag to the story's swipe, which
    /// hands it back if the finger turns), its corners rounding; letting go closes it back into the list, which scrolls
    /// the card into view as it lands.
    /// </summary>
    public sealed class AppStoreDemo : MonoBehaviour
    {
        /// <summary>The Today stories: what kind, what about, and the colours of their art.</summary>
        public static readonly (string Kicker, string Title, Color From, Color To)[] Stories =
        {
            ("GAME OF THE DAY", "Sail the Glass Sea", new Color32(0x5C, 0x8D, 0xFF, 0xFF), new Color32(0x1E, 0x2F, 0x6B, 0xFF)),
            ("MEET THE DEVELOPER", "Tiny Studio, Big Worlds", new Color32(0xFF, 0x7E, 0xB6, 0xFF), new Color32(0x6B, 0x1E, 0x4A, 0xFF)),
            ("APP OF THE DAY", "Sketch Anything", new Color32(0xF2, 0xA9, 0x3B, 0xFF), new Color32(0x7A, 0x3A, 0x0C, 0xFF)),
            ("HOW TO", "Make Every Photo Pop", new Color32(0x3F, 0xC1, 0xC9, 0xFF), new Color32(0x10, 0x4D, 0x52, 0xFF)),
        };

        [SerializeField] private List<Pressable> _cards = new();
        [SerializeField] private ScrollView _list;
        [SerializeField] private Presentation _story;
        [SerializeField] private UINode _content;
        [SerializeField] private Box _art;
        [SerializeField] private Label _kicker;
        [SerializeField] private Label _title;
        [SerializeField] private ScrollView _scroll;
        [SerializeField] private SwipeToDismiss _swipe;
        [SerializeField] private Pressable _close;

        // How round the story's corners get as it is pulled down all the way.
        private const float Rounded = 30f;

        private int _open = -1;

        public void Setup(List<Pressable> cards, ScrollView list, Presentation story, UINode content, Box art, Label kicker, Label title,
            ScrollView scroll, SwipeToDismiss swipe, Pressable close)
        {
            _cards = cards;
            _list = list;
            _story = story;
            _content = content;
            _art = art;
            _kicker = kicker;
            _title = title;
            _scroll = scroll;
            _swipe = swipe;
            _close = close;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                int index = i;
                _cards[i].Tapped += () => Open(index);
            }
            _close.Tapped += Close;
            _story.Presenting += HideCard;
            _story.Dismissing += ShowCard;
            _swipe.Progress += Round;
        }

        private void OnDisable()
        {
            _close.Tapped -= Close;
            _story.Presenting -= HideCard;
            _story.Dismissing -= ShowCard;
            _swipe.Progress -= Round;
        }

        private void Open(int index)
        {
            if (_story.IsPresented) return;
            var (kicker, title, from, to) = Stories[index];
            _art.Fill.Value = from;
            _art.GradientTo.Value = to;
            _kicker.Text.Value = kicker;
            _title.Text.Value = title;
            _content.MatchId.Value = index.ToString();
            _scroll.ScrollTo(Vector2.zero, animated: false);
            _open = index;
            _story.Present();
        }

        private void Close() => _story.Dismiss();

        // Inside the story's changes: the card is hidden as the story grows out of it; shown again as the story closes back
        // into it, the list scrolling it into view in the same change, so the story flies to where the card is going.
        private void HideCard()
        {
            if (_open >= 0)
                _cards[_open].Node.Display.Value = DisplayMode.Hidden;
        }

        private void ShowCard()
        {
            if (_open < 0) return;
            var card = _cards[_open].Node;
            card.Display.Value = DisplayMode.Visible;
            _list.ScrollIntoView(card);
        }

        // The story's corners round as it is pulled down.
        private void Round(float progress) => _content.ClipRadii.Value = Rounded * progress;
    }
}
