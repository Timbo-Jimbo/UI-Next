using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Images: photos loaded from the web, each asking its loader for a copy as wide as it is drawn, which a tap opens full
    /// screen, flying out of its place: there it asks for a wider copy, the page's standing in until that arrives. Clearing
    /// the cache loads them all again. A photo on the page and the one full screen share a match name, the full screen one
    /// taking the tapped one's id; the page's is hidden while it is open. Full screen, a tap or back closes it, and a drag
    /// anywhere throws it away any way, as Circle's photo viewer does, the backdrop fading as it goes.
    /// </summary>
    public sealed class ImagesDemo : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _photos = new();
        [SerializeField] private List<string> _urls = new();
        [SerializeField] private Pressable _clear;
        [SerializeField] private Presentation _viewer;
        [SerializeField] private UINode _backdrop;
        [SerializeField] private Image _image;
        [SerializeField] private SwipeToDismiss _swipe;

        private readonly List<Action> _opens = new();
        private Pressable _tapBackdrop;
        private Pressable _tapPhoto;
        private int _open = -1;

        public void Setup(List<Pressable> photos, List<string> urls, Pressable clear, Presentation viewer, UINode backdrop,
            Image image, SwipeToDismiss swipe)
        {
            _photos = photos;
            _urls = urls;
            _clear = clear;
            _viewer = viewer;
            _backdrop = backdrop;
            _image = image;
            _swipe = swipe;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _photos.Count; i++)
            {
                int index = i;
                Action open = () => Open(index);
                _opens.Add(open);
                _photos[i].Tapped += open;
            }
            _clear.Tapped += Image.ClearCache;
            _viewer.Presenting += Opening;
            _viewer.Dismissing += Closing;
            _viewer.Dismissed += Closed;
            _tapBackdrop = _backdrop.GetComponent<Pressable>();
            _tapPhoto = _image.GetComponent<Pressable>();
            _tapBackdrop.Tapped += Close;
            _tapPhoto.Tapped += Close;
            _swipe.Progress += Fade;
        }

        private void OnDisable()
        {
            for (int i = 0; i < _opens.Count; i++)
                _photos[i].Tapped -= _opens[i];
            _opens.Clear();
            _clear.Tapped -= Image.ClearCache;
            _viewer.Presenting -= Opening;
            _viewer.Dismissing -= Closing;
            _viewer.Dismissed -= Closed;
            _tapBackdrop.Tapped -= Close;
            _tapPhoto.Tapped -= Close;
            _swipe.Progress -= Fade;
        }

        // Full screen, it asks for a copy as wide as the screen; the page's copy stands in for it until that arrives.
        private void Open(int index)
        {
            if (_viewer.IsPresented) return;
            _open = index;
            _image.Url.Value = _urls[index];
            _image.Node.MatchId.Value = index.ToString();
            _viewer.Present();
        }

        private void Close()
        {
            if (_viewer.IsPresented)
                _viewer.Dismiss();
        }

        // The backdrop fades as the photo is thrown away, and comes back as it springs home.
        private void Fade(float progress) => _backdrop.Opacity.Value = 1f - progress;

        // Inside the viewer's changes: the page's photo is hidden as this one flies out of it and the backdrop fades in;
        // closing, the page's photo is shown again (scrolled into view, for this one to fly home to) as the backdrop fades.
        private void Opening()
        {
            _photos[_open].Node.Display.Value = DisplayMode.Hidden;
            _backdrop.Opacity.Value = 1f;
        }

        private void Closing()
        {
            _backdrop.Opacity.Value = 0f;
            if (_open < 0) return;
            var photo = _photos[_open].Node;
            photo.Display.Value = DisplayMode.Visible;
            var page = photo.GetComponentInParent<ScrollView>();
            if (page != null)
                page.ScrollIntoView(photo);
        }

        // Closed, its copy is let go of; it stays in the cache a while, for opening it again.
        private void Closed()
        {
            if (_viewer.IsPresented) return;
            _image.Url.Value = "";
            _open = -1;
        }
    }
}
