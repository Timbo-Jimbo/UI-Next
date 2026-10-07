using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// Circle's chats: tapping a row pushes its conversation over the list (a Cover presentation that slides in, the
    /// list culled once it is in place). Back, the back button or a swipe from anywhere in it to the right takes it
    /// away again, following the finger. Quick replies add a bubble that pops in at the end of the thread, which
    /// scrolls to it.
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

        private Draggable _swipe;

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

        private void OnEnable()
        {
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
            _swipe = _content.GetComponent<Draggable>();
            if (_swipe != null)
            {
                _swipe.Changed += OnSwipe;
                _swipe.Ended += OnSwipeEnded;
                _swipe.Cancelled += SpringBack;
            }
            _conversation.Dismissed += OnDismissed;
        }

        private void OnDisable()
        {
            _back.Tapped -= Close;
            if (_swipe != null)
            {
                _swipe.Changed -= OnSwipe;
                _swipe.Ended -= OnSwipeEnded;
                _swipe.Cancelled -= SpringBack;
            }
            _conversation.Dismissed -= OnDismissed;
        }

        /// <summary>Pushes the <paramref name="index"/>th chat's conversation.</summary>
        public void Open(int index)
        {
            if (_conversation.IsPresented) return;
            var (person, _, _, thread) = CircleData.Chats[index];
            var (name, initials, colour) = CircleData.People[person];
            _avatar.Fill.Value = colour;
            _initials.Text.Value = initials;
            _name.Text.Value = name;

            // Out of the tree at once (disabled), gone at the end of the frame.
            for (int i = _thread.transform.childCount - 1; i >= 0; i--)
            {
                var old = _thread.transform.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            for (int i = 0; i < thread.Length; i++)
                AddBubble(thread[i], mine: i % 2 == 1);
            _conversation.Present();
            _threadScroll.ScrollTo(new Vector2(0f, _threadScroll.Range.y), animated: false);
        }

        public void Close() => _conversation.Dismiss();

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
            var bubble = line.Child("Bubble").Pad(14f, 9f);
            bubble.Width.Value = Sizing.Fit(0f, 260f);
            var box = bubble.Box(_mine, 18f);
            if (!mine) box.Fill.Token = _bubble;
            var label = bubble.Child("Text").Label(text, 16f, Color.white);
            label.Node.Width.Value = Sizing.Grow();
            if (!mine) label.Color.Token = _text;
            return bubble;
        }

        // A swipe right takes the conversation with the finger.
        private void OnSwipe(DragEvent e)
        {
            _content.Offset.Value = new Vector2(Mathf.Max(0f, e.Translation.x), 0f);
            _content.Offset.Settle();
        }

        private void OnSwipeEnded(DragEvent e)
        {
            if (e.Translation.x > 120f || e.Velocity.x > 600f) Close();
            else SpringBack();
        }

        private void SpringBack() => UI.Animate(UIAnimation.Snappy, () => _content.Offset.Value = Vector2.zero);

        private void OnDismissed()
        {
            _content.Offset.Value = Vector2.zero;
            _content.Offset.Settle();
        }
    }
}
