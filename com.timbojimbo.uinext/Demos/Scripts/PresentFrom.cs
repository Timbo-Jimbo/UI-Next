using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos
{
    /// <summary>
    /// Presents its presentation from a button, growing out of it: what is presented and the button (or a part of it, its
    /// title) share a match name, and that part hides as the presentation comes and shows again as it goes, so the
    /// presentation takes over from it and goes back into it. Its close buttons dismiss it.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class PresentFrom : MonoBehaviour
    {
        [SerializeField] private Pressable _opener;
        [Tooltip("What hides as it comes and shows as it goes: the opener's own node if empty, or a part of it (its title).")]
        [SerializeField] private UINode _part;
        [SerializeField] private List<Pressable> _closers = new();

        private Presentation _presentation;

        // What hides while it is up.
        private UINode Part => _part != null ? _part : _opener.Node;

        public void Setup(Pressable opener, List<Pressable> closers, UINode part = null)
        {
            _opener = opener;
            _closers = closers;
            _part = part;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            _opener.Tapped += Open;
            foreach (var closer in _closers)
                closer.Tapped += Close;
            _presentation.Dismissing += ShowPart;
        }

        private void OnDisable()
        {
            _opener.Tapped -= Open;
            foreach (var closer in _closers)
                closer.Tapped -= Close;
            _presentation.Dismissing -= ShowPart;
        }

        /// <summary>One change: the part goes and the presentation comes, so it takes over from the part and grows out of it.</summary>
        public void Open()
        {
            if (_presentation.IsPresented) return;
            UI.Animate(_presentation.Animation, () =>
            {
                Part.Display.Value = DisplayMode.Hidden;
                _presentation.Present();
            });
        }

        public void Close() => _presentation.Dismiss();

        // Inside the change that takes it away: the part comes back, taking over from it as it goes.
        private void ShowPart() => Part.Display.Value = DisplayMode.Visible;
    }
}
