using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A photo opened full screen. Its photo and the one in the feed share a match name, and the feed's is hidden as the
    /// viewer is presented and shown again as it is dismissed, so the photo flies out of the feed and home again
    /// (wherever the feed has scrolled it meanwhile). Thrown away in any direction, or pinched small, it flies home from
    /// where it was let go, at the speed it was moving; let go of near where it was, it springs back. Pinched, it zooms
    /// under the fingers, as Instagram's photos do; tapped, or back, it closes. The backdrop and caption fade with a throw.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class PhotoViewer : MonoBehaviour
    {
        [SerializeField] private UINode _backdrop;
        [SerializeField] private ScenePhoto _photo;
        [SerializeField] private Label _caption;
        [SerializeField] private SwipeToDismiss _swipe;

        // Pinched smaller than this, it closes as the fingers let go.
        private const float PinchedAway = 0.8f;

        private Presentation _presentation;
        private ScenePhoto _source;
        private Pinchable _pinch;
        private Vector2 _pinchCenter;
        private Pressable _tapPhoto;
        private Pressable _tapBackdrop;

        public void Setup(UINode backdrop, ScenePhoto photo, Label caption, SwipeToDismiss swipe)
        {
            _backdrop = backdrop;
            _photo = photo;
            _caption = caption;
            _swipe = swipe;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            _presentation.DismissOnBack = false;
            _presentation.Presenting += HideSource;
            _presentation.Dismissing += ShowSource;
            _swipe.Progress += Fade;
            _tapPhoto = _photo.GetComponent<Pressable>();
            _tapBackdrop = _backdrop.GetComponent<Pressable>();
            _tapPhoto.Tapped += Close;
            _tapBackdrop.Tapped += Close;
            _pinch = _photo.GetComponent<Pinchable>();
            if (_pinch != null)
            {
                _pinch.Began += OnPinchBegan;
                _pinch.Changed += OnPinch;
                _pinch.Ended += OnPinchEnded;
                _pinch.Cancelled += SpringBack;
            }
            UIInput.Back += OnBack;
        }

        private void OnDisable()
        {
            _presentation.Presenting -= HideSource;
            _presentation.Dismissing -= ShowSource;
            _swipe.Progress -= Fade;
            _tapPhoto.Tapped -= Close;
            _tapBackdrop.Tapped -= Close;
            if (_pinch != null)
            {
                _pinch.Began -= OnPinchBegan;
                _pinch.Changed -= OnPinch;
                _pinch.Ended -= OnPinchEnded;
                _pinch.Cancelled -= SpringBack;
            }
            UIInput.Back -= OnBack;
        }

        /// <summary>Opens <paramref name="source"/>, a photo in the feed matched by <paramref name="id"/>: it flies out of it.</summary>
        public void Open(ScenePhoto source, string caption, string id)
        {
            if (_presentation.IsPresented) return;
            _source = source;
            _photo.CopyFrom(source);
            _caption.Text.Value = caption;
            _photo.Node.MatchId.Value = id;
            _presentation.Present();
        }

        /// <summary>Closes it: it flies home to its photo in the feed.</summary>
        public void Close()
        {
            if (_presentation.IsPresented)
                _presentation.Dismiss();
        }

        // Inside the presentation's changes: the feed's photo is hidden as this one flies out of it, and shown again as
        // this one flies home into it.
        private void HideSource()
        {
            if (_source != null)
                _source.Node.Display.Value = DisplayMode.Hidden;
        }

        private void ShowSource()
        {
            if (_source != null)
                _source.Node.Display.Value = DisplayMode.Visible;
        }

        // What fades as the photo is thrown away.
        private void Fade(float progress)
        {
            _backdrop.Opacity.Value = 1f - progress;
            _caption.Node.Opacity.Value = 1f - Mathf.Clamp01(progress * 3f);
        }

        private bool OnBack()
        {
            if (!_presentation.IsPresented) return false;
            Close();
            return true;
        }

        private void OnPinchBegan(PinchEvent e)
        {
            _pinchCenter = _photo.Node.DrawnRect.center;
            OnPinch(e);
        }

        // Zoomed about the point between the fingers, which stays under them as they spread and move.
        private void OnPinch(PinchEvent e)
        {
            var node = _photo.Node;
            float scale = Mathf.Clamp(e.Scale, 0.5f, 4f);
            node.Scale.Value = scale;
            node.Offset.Value = e.Translation + (1f - scale) * (e.StartCenter - _pinchCenter);
            Fade(Mathf.Clamp01((1f - scale) * 2f));
        }

        // Pinched small, it flies home from there at the speed the fingers moved it; otherwise it springs back.
        private void OnPinchEnded(PinchEvent e)
        {
            if (e.Scale < PinchedAway) Close();
            else SpringBack();
        }

        private void SpringBack() => UI.Animate(UIAnimation.Snappy, () =>
        {
            _photo.Node.Scale.Value = 1f;
            _photo.Node.Offset.Value = Vector2.zero;
            Fade(0f);
        });
    }
}
