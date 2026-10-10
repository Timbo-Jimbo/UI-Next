using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// Circle's chats: tapping a row grows it open into its conversation (a Cover presentation, the list culled once it
    /// is in place): the row and the conversation share a match name and the row's id, so the conversation flies out of
    /// the row, the row's avatar and name flying into the header's and its preview fading. Back, the back button or a
    /// swipe to the right (following the finger) collapses it back into the row. Quick replies, and what is typed in the
    /// composer, add a bubble that pops in at the end of the thread, which scrolls to it.
    /// </summary>
    public sealed class ChatsController : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _rows = new();
        [SerializeField] private Presentation _conversation;
        [SerializeField] private UINode _content;
        [SerializeField] private Pressable _back;
        [SerializeField] private Box _avatar;
        [SerializeField] private Label _initials;
        [SerializeField] private Label _name;
        [SerializeField] private UINode _thread;
        [SerializeField] private ScrollView _threadScroll;
        [SerializeField] private List<Pressable> _replies = new();
        [SerializeField] private ColorToken _bubble;
        [SerializeField] private ColorToken _text;
        [SerializeField] private Color _mine = new(0.2f, 0.47f, 0.96f);
#if UINEXT_TEXTINPUT
        [SerializeField] private TextField _composer;
        [SerializeField] private Pressable _send;
#endif

        // The row the conversation open is of.
        private int _open = -1;

        // Around a bubble's text.
        private static readonly Vector2 BubblePadding = new(14f, 9f);

        public void Setup(List<Pressable> rows, Presentation conversation, UINode content, Pressable back, Box avatar, Label initials,
            Label name, UINode thread, ScrollView threadScroll, List<Pressable> replies, ColorToken bubble, ColorToken text, Color mine)
        {
            _rows = rows;
            _conversation = conversation;
            _content = content;
            _back = back;
            _avatar = avatar;
            _initials = initials;
            _name = name;
            _thread = thread;
            _threadScroll = threadScroll;
            _replies = replies;
            _bubble = bubble;
            _text = text;
            _mine = mine;
        }

#if UINEXT_TEXTINPUT
        public void SetupComposer(TextField composer, Pressable send)
        {
            _composer = composer;
            _send = send;
        }

        // Return sends (and typing carries on, as in Messages), as does the send button, lit while there is text. What was
        // typed lifts out of the field into its bubble, which fills in around it, its text turning white on the way.
        private void SendTyped(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var typed = _composer.TextNode.DrawnRect;
            var typedColor = _composer.Color.Peek;
            _composer.Text = "";
            var bubble = AddBubble(text.Trim(), mine: true);
            UI.LayoutNow();
            // Where the bubble's text will be drawn, in the root's points: through its line and the thread, as scrolled
            // now (the thread scrolls on to it as it flies).
            var landing = _thread.DrawnRect.position - _threadScroll.PresentedOffset + bubble.Parent.LaidOutRect.position
                          + bubble.LaidOutRect.position + BubblePadding;
            var box = bubble.GetComponent<Box>();
            var label = bubble.GetComponentInChildren<Label>();
            Set(bubble.Offset, typed.position - landing);
            Set(box.Fill, new Color(_mine.r, _mine.g, _mine.b, 0f));
            Set(label.Color, typedColor);
            // Over everything while it flies (the thread clips what is in it), back in the thread once it lands.
            bubble.TopLayer.Value = true;
            UI.Animate(new UIAnimation(0.5f, 0.1f), () =>
            {
                bubble.Offset.Value = Vector2.zero;
                box.Fill.Value = _mine;
                label.Color.Value = Color.white;
            }).Finished += () =>
            {
                if (bubble != null)
                    bubble.TopLayer.Value = false;
            };
            _threadScroll.ScrollTo(new Vector2(0f, _threadScroll.Range.y));
        }

        // Puts a value somewhere at once, whatever was moving it.
        private static void Set<T>(UIValue<T> value, T to)
        {
            value.Value = to;
            value.Settle();
        }

        private void SendComposer() => SendTyped(_composer.Text);

        private void OnTyped(string text) => _send.Interactable.Value = !string.IsNullOrWhiteSpace(text);
#endif

        private void OnEnable()
        {
#if UINEXT_TEXTINPUT
            if (_composer != null && _send != null)
            {
                _composer.Submitted.AddListener(SendTyped);
                _composer.TextChanged.AddListener(OnTyped);
                _send.Tapped += SendComposer;
                OnTyped(_composer.Text);
            }
#endif
            for (int i = 0; i < _rows.Count; i++)
            {
                int index = i;
                _rows[i].Tapped += () => Open(index);
            }
            for (int i = 0; i < _replies.Count; i++)
            {
                int index = i;
                _replies[i].Tapped += () => Send(CircleData.Replies[index % CircleData.Replies.Length]);
            }
            _back.Tapped += Close;
            _conversation.Presenting += HideRow;
            _conversation.Dismissing += ShowRow;
        }

        private void OnDisable()
        {
#if UINEXT_TEXTINPUT
            if (_composer != null && _send != null)
            {
                _composer.Submitted.RemoveListener(SendTyped);
                _composer.TextChanged.RemoveListener(OnTyped);
                _send.Tapped -= SendComposer;
            }
#endif
            _back.Tapped -= Close;
            _conversation.Presenting -= HideRow;
            _conversation.Dismissing -= ShowRow;
        }

        /// <summary>Grows the <paramref name="index"/>th chat's row open into its conversation.</summary>
        public void Open(int index)
        {
            if (_conversation.IsPresented) return;
            var (person, _, _, thread) = CircleData.Chats[index];
            var (name, initials, colour) = CircleData.People[person];
            _avatar.Fill.Value = colour;
            _initials.Text.Value = initials;
            _name.Text.Value = name;
            // The row's id: the conversation is matched to its row.
            _content.MatchId.Value = index.ToString();

            // Out of the tree at once (disabled), gone at the end of the frame.
            for (int i = _thread.transform.childCount - 1; i >= 0; i--)
            {
                var old = _thread.transform.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            for (int i = 0; i < thread.Length; i++)
                AddBubble(thread[i], mine: i % 2 == 1);
            _open = index;
            _conversation.Present();
        }

        // Inside the presentation's changes: the row is hidden (keeping its place) as the conversation grows out of it,
        // and shown again as it collapses back into it.
        private void HideRow()
        {
            if (_open < 0) return;
            _rows[_open].Node.Display.Value = DisplayMode.Hidden;
            // The thread opens at its end, where it is laid out now.
            _threadScroll.ScrollTo(new Vector2(0f, _threadScroll.Range.y), animated: false);
        }

        private void ShowRow()
        {
            if (_open >= 0)
                _rows[_open].Node.Display.Value = DisplayMode.Visible;
        }

        public void Close()
        {
#if UINEXT_TEXTINPUT
            if (_composer != null)
                _composer.EndEditing();
#endif
            _conversation.Dismiss();
        }

        /// <summary>Sends <paramref name="text"/>: a bubble of mine pops in at the end, and the thread scrolls to it.</summary>
        public void Send(string text)
        {
            var bubble = AddBubble(text, mine: true);
            bubble.Scale.Value = 0.4f;
            bubble.Scale.Settle();
            bubble.Opacity.Value = 0f;
            bubble.Opacity.Settle();
            UI.LayoutNow();
            UI.Animate(UIAnimation.Bouncy, () =>
            {
                bubble.Scale.Value = 1f;
                bubble.Opacity.Value = 1f;
            });
            _threadScroll.ScrollTo(new Vector2(0f, _threadScroll.Range.y));
        }

        // A message: a line across the thread holding a bubble at its start (theirs) or end (mine), as wide as its text
        // up to most of the line.
        private UINode AddBubble(string text, bool mine)
        {
            var line = UIBuild.Node("Line", _thread.transform).Row();
            line.Width.Value = Sizing.Grow();
            line.AlignX.Value = mine ? AlignX.Right : AlignX.Left;
            var bubble = line.Child("Bubble").Pad(BubblePadding.x, BubblePadding.y);
            bubble.Width.Value = Sizing.Fit(0f, 260f);
            var box = bubble.Box(_mine, 18f);
            if (!mine) box.Fill.Token = _bubble;
            var label = bubble.Child("Text").Label(text, 16f, Color.white);
            label.Node.Width.Value = Sizing.Grow();
            if (!mine) label.Color.Token = _text;
            return bubble;
        }

    }
}
