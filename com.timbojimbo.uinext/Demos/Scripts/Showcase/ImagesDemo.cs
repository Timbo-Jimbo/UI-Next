using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Images: photos loaded from the web as small copies, which a tap opens full screen, flying out of their place as a
    /// larger copy loads over them there, and the cache cleared to load them all again. The CDN (Bunny's optimizer) sends
    /// each copy at the width its URL asks for. A photo on the page and the one full screen share a match name, the full
    /// screen one taking the tapped one's id; the page's is hidden while it is open. Full screen, a tap or back closes it,
    /// and a drag anywhere throws it away any way, as Circle's photo viewer does, the backdrop fading as it goes.
    /// </summary>
    public sealed class ImagesDemo : MonoBehaviour
    {
        /// <summary>A photo's copy for the page, as small as it is drawn there.</summary>
        public static string Small(string url) => url + "?width=480";

        /// <summary>A photo's copy for full screen.</summary>
        public static string Large(string url) => url + "?width=1600";

        [SerializeField] private List<Pressable> _photos = new();
        [SerializeField] private List<string> _urls = new();
        [SerializeField] private Pressable _clear;
        [SerializeField] private Presentation _viewer;
        [SerializeField] private UINode _backdrop;
        [SerializeField] private UINode _shown;
        [SerializeField] private Image _small;
        [SerializeField] private Image _large;
        [SerializeField] private SwipeToDismiss _swipe;

        private readonly List<Action> _opens = new();
        private Pressable _tapBackdrop;
        private Pressable _tapPhoto;
        private int _open = -1;

        public void Setup(List<Pressable> photos, List<string> urls, Pressable clear, Presentation viewer, UINode backdrop,
            UINode shown, Image small, Image large, SwipeToDismiss swipe)
        {
            _photos = photos;
            _urls = urls;
            _clear = clear;
            _viewer = viewer;
            _backdrop = backdrop;
            _shown = shown;
            _small = small;
            _large = large;
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
            _tapPhoto = _shown.GetComponent<Pressable>();
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

        // Full screen, the small copy, which the page has already, shows at once; the large one loads over it, fading in
        // as it arrives.
        private void Open(int index)
        {
            if (_viewer.IsPresented) return;
            _open = index;
            _small.Url.Value = Small(_urls[index]);
            _large.Url.Value = Large(_urls[index]);
            _shown.MatchId.Value = index.ToString();
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

        // Closed, its copies are let go of: the large one stays in the cache a while, for opening it again.
        private void Closed()
        {
            if (_viewer.IsPresented) return;
            _small.Url.Value = "";
            _large.Url.Value = "";
            _open = -1;
        }
    }
}
