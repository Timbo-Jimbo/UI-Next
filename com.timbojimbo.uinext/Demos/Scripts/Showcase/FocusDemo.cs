using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// The Focus page: a readout, under the page, of where keyboard and gamepad focus is and whether it shows, as
    /// <see cref="UIFocus"/> reports it; the switch that has rows read right to left (<see cref="UI.ReadingDirection"/>,
    /// put back as the page goes); and buttons that go when pressed, so focus goes from them to the nearest, until they
    /// are brought back.
    /// </summary>
    public sealed class FocusDemo : MonoBehaviour
    {
        [SerializeField] private UINode _body;
        [SerializeField] private Label _focused;
        [SerializeField] private Label _state;
        [SerializeField] private Pressable _rightToLeft;
        [SerializeField] private List<Pressable> _goers = new();
        [SerializeField] private Pressable _bringBack;

        private ReadingDirection _direction;
        private UINode _lastFocused;
        private bool _lastVisible;
        private UINode _lastResponder;

        public void Setup(UINode body, Label focused, Label state, Pressable rightToLeft, List<Pressable> goers, Pressable bringBack)
        {
            _body = body;
            _focused = focused;
            _state = state;
            _rightToLeft = rightToLeft;
            _goers = goers;
            _bringBack = bringBack;
        }

        private void OnEnable()
        {
            _direction = UI.ReadingDirection;
            _rightToLeft.Toggled += OnRightToLeft;
            foreach (var goer in _goers)
            {
                var node = goer.Node;
                goer.Tapped += () => UI.Animate(() => node.Display.Value = DisplayMode.None);
            }
            _bringBack.Tapped += BringBack;
            Refresh(force: true);
        }

        private void OnDisable()
        {
            _rightToLeft.Toggled -= OnRightToLeft;
            _bringBack.Tapped -= BringBack;
            UI.ReadingDirection = _direction;
        }

        private void OnRightToLeft(bool on) =>
            UI.ReadingDirection = on ? ReadingDirection.RightToLeft : ReadingDirection.LeftToRight;

        private void BringBack() => UI.Animate(() =>
        {
            foreach (var goer in _goers)
                goer.Node.Display.Value = DisplayMode.Visible;
        });

        // Whether focus shows changes without an event (a pointer used hides it), so the readout looks every frame.
        private void Update() => Refresh(force: false);

        private void Refresh(bool force)
        {
            var focused = UIFocus.Focused;
            bool visible = UIFocus.IsVisible;
            var responder = UIFocus.FirstResponder;
            if (!force && focused == _lastFocused && visible == _lastVisible && responder == _lastResponder) return;
            _lastFocused = focused;
            _lastVisible = visible;
            _lastResponder = responder;
            _focused.Text.Value = "Focus: " + (focused != null ? NameOf(focused) : "none");
            string shows = visible ? "Showing: a key or a gamepad moved it" : focused != null ? "Not showing: a pointer was used since" : "Nothing has focus";
            _state.Text.Value = responder != null ? shows + "  ·  Typing into it" : shows;
        }

        // A node's name after the case it is in ("Arrows and Tab › 5"), or its own outside the page's cases (Back, a
        // dialog's buttons).
        private string NameOf(UINode node)
        {
            for (var n = node; n != null; n = n.Parent)
            {
                if (n.Parent != _body) continue;
                return n == node ? node.name : n.name + " › " + node.name;
            }
            return node.name;
        }
    }
}
