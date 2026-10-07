using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A photo opened full screen. It flies out of the feed from where it is drawn (its Offset and Scale set to match,
    /// then animated to nothing, so layout never moves), the backdrop fading in behind it. Drag it away to close it
    /// (the backdrop fading as it goes) or let go to have it spring back; pinch it to zoom in under the fingers, and it
    /// springs back as they let go, as Instagram's photos do; tap it, the backdrop or back to close it, and it flies home
    /// to wherever its photo in the feed is drawn by then.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class PhotoViewer : MonoBehaviour
    {
        [SerializeField] private UINode _backdrop;
        [SerializeField] private ScenePhoto _photo;
        [SerializeField] private Label _caption;

        private static readonly UIAnimation Flight = new(0.45f, 0.1f);

        private Presentation _presentation;
        private ScenePhoto _source;
        private Draggable _drag;
        private Pinchable _pinch;
        private Vector2 _pinchCenter;
        private Pressable _tapPhoto;
        private Pressable _tapBackdrop;
        private bool _closing;

        public void Setup(UINode backdrop, ScenePhoto photo, Label caption)
        {
            _backdrop = backdrop;
            _photo = photo;
            _caption = caption;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            _presentation.DismissOnBack = false;
            _drag = _photo.GetComponent<Draggable>();
            _tapPhoto = _photo.GetComponent<Pressable>();
            _tapBackdrop = _backdrop.GetComponent<Pressable>();
            _drag.Changed += OnDrag;
            _drag.Ended += OnRelease;
            _drag.Cancelled += SpringBack;
            _pinch = _photo.GetComponent<Pinchable>();
            if (_pinch != null)
            {
                _pinch.Began += OnPinchBegan;
                _pinch.Changed += OnPinch;
                _pinch.Ended += OnPinchEnded;
                _pinch.Cancelled += SpringBack;
            }
            _tapPhoto.Tapped += Close;
            _tapBackdrop.Tapped += Close;
            UIInput.Back += OnBack;
        }

        private void OnDisable()
        {
            _drag.Changed -= OnDrag;
            _drag.Ended -= OnRelease;
            _drag.Cancelled -= SpringBack;
            if (_pinch != null)
            {
                _pinch.Began -= OnPinchBegan;
                _pinch.Changed -= OnPinch;
                _pinch.Ended -= OnPinchEnded;
                _pinch.Cancelled -= SpringBack;
            }
            _tapPhoto.Tapped -= Close;
            _tapBackdrop.Tapped -= Close;
            UIInput.Back -= OnBack;
        }

        private void OnPinchBegan(PinchEvent e)
        {
            _pinchCenter = _photo.Node.DrawnRect.center;
            OnPinch(e);
        }

        // Zoomed about the point between the fingers, which stays under them as they spread and move.
        private void OnPinch(PinchEvent e)
        {
            if (_closing) return;
            var node = _photo.Node;
            float scale = Mathf.Clamp(e.Scale, 0.6f, 4f);
            Set(node.Scale, scale);
            Set(node.Offset, e.Translation + (1f - scale) * (e.StartCenter - _pinchCenter));
        }

        private void OnPinchEnded(PinchEvent e) => SpringBack();

        /// <summary>Opens <paramref name="source"/>, flying out of where it is drawn.</summary>
        public void Open(ScenePhoto source, string caption)
        {
            if (_presentation.IsPresented) return;
            _source = source;
            _closing = false;
            _photo.CopyFrom(source);
            _caption.Text.Value = caption;
            _presentation.Present();

            // Over where the source is drawn, at once; the source itself hides while its picture is away.
            var node = _photo.Node;
            Match(node, source.Node.DrawnRect, out var offset, out float scale);
            Set(node.Offset, offset);
            Set(node.Scale, scale);
            Set(_backdrop.Opacity, 0f);
            Set(_caption.Node.Opacity, 0f);
            Set(source.Node.Opacity, 0f);
            UI.Animate(Flight, () =>
            {
                node.Offset.Value = Vector2.zero;
                node.Scale.Value = 1f;
                _backdrop.Opacity.Value = 1f;
                _caption.Node.Opacity.Value = 1f;
            });
        }

        /// <summary>Closes it, flying home to where its photo in the feed is drawn now.</summary>
        public void Close()
        {
            if (!_presentation.IsPresented || _closing || _source == null) return;
            _closing = true;
            var node = _photo.Node;
            Match(node, _source.Node.DrawnRect, out var offset, out float scale);
            var source = _source;
            UI.Animate(Flight, () =>
            {
                node.Offset.Value = offset;
                node.Scale.Value = scale;
                _backdrop.Opacity.Value = 0f;
                _caption.Node.Opacity.Value = 0f;
            }).Finished += () =>
            {
                Set(source.Node.Opacity, 1f);
                _presentation.Dismiss();
                Set(node.Offset, Vector2.zero);
                Set(node.Scale, 1f);
                _closing = false;
            };
        }

        private bool OnBack()
        {
            if (!_presentation.IsPresented) return false;
            Close();
            return true;
        }

        // A drag moves the photo with the finger, shrinking it a little and fading what is around it the further it goes.
        private void OnDrag(DragEvent e)
        {
            if (_closing) return;
            var node = _photo.Node;
            float progress = Mathf.Clamp01(Mathf.Abs(e.Translation.y) / 400f);
            Set(node.Offset, e.Translation);
            Set(node.Scale, 1f - 0.25f * progress);
            Set(_backdrop.Opacity, 1f - progress);
            Set(_caption.Node.Opacity, 1f - Mathf.Clamp01(progress * 3f));
        }

        private void OnRelease(DragEvent e)
        {
            if (Mathf.Abs(e.Translation.y) > 120f || Mathf.Abs(e.Velocity.y) > 800f) Close();
            else SpringBack();
        }

        private void SpringBack()
        {
            if (_closing) return;
            var node = _photo.Node;
            UI.Animate(UIAnimation.Snappy, () =>
            {
                node.Offset.Value = Vector2.zero;
                node.Scale.Value = 1f;
                _backdrop.Opacity.Value = 1f;
                _caption.Node.Opacity.Value = 1f;
            });
        }

        // The offset and scale that draw `node` (laid out as it is) over `target`, a rect in its root's points.
        private static void Match(UINode node, Rect target, out Vector2 offset, out float scale)
        {
            var laidOut = node.LaidOutRect;
            for (var parent = node.Parent; parent != null && parent.Parent != null; parent = parent.Parent)
                laidOut.position += parent.LaidOutRect.position;
            scale = laidOut.width > 0f ? target.width / laidOut.width : 1f;
            offset = target.center - laidOut.center;
        }

        // Puts a value somewhere at once, whatever was moving it.
        private static void Set<T>(UIValue<T> value, T to)
        {
            value.Value = to;
            value.Settle();
        }
    }
}
