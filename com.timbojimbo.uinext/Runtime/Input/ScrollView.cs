using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Makes a node scroll its children, as a UIScrollView: layout lets them run past its edge the way it scrolls, it
    /// clips them, and they are drawn moved by its offset.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UINode))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Scroll View")]
    public sealed partial class ScrollView : MonoBehaviour
    {
        [SerializeField] private UIValue<ScrollAxis> _axis = ScrollAxis.Vertical;

        [System.NonSerialized] private UINode _node;

        // How far its children are scrolled (x right, y down), with where it is drawn while it springs or glides.
        [System.NonSerialized] internal readonly AnimatedValue<Vector2> Offset = new(Vector2.zero, 0.5f);

        /// <summary>Which way it scrolls.</summary>
        public UIValue<ScrollAxis> Axis => _axis;

        /// <summary>Its node.</summary>
        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        /// <summary>Its size, as last laid out.</summary>
        public Vector2 Viewport { get; private set; }

        /// <summary>How big what it holds is, padding included.</summary>
        public Vector2 ContentSize { get; private set; }

        /// <summary>How far it can scroll each way.</summary>
        public Vector2 Range => new(
            Scrolls(0) ? Mathf.Max(0f, ContentSize.x - Viewport.x) : 0f,
            Scrolls(1) ? Mathf.Max(0f, ContentSize.y - Viewport.y) : 0f);

        /// <summary>How far its children are scrolled, as drawn.</summary>
        public Vector2 ScrollOffset => Offset.Presented;

        private bool Scrolls(int axis)
        {
            var value = _axis.Peek;
            return axis == 0 ? value is ScrollAxis.Horizontal or ScrollAxis.Both : value is ScrollAxis.Vertical or ScrollAxis.Both;
        }

        private void OnEnable()
        {
            ((IUIValue)_axis).Attach(Node);
            Node.Scroller = this;
            Node.Tree?.InvalidateLayout();
            OnEnableInput();
        }

        private void OnDisable()
        {
            ((IUIValue)_axis).Detach();
            if (_node != null && _node.Scroller == this)
            {
                _node.Scroller = null;
                _node.Tree?.InvalidateLayout();
            }
            OnDisableInput();
        }

        private void OnValidate()
        {
            ((IUIValue)_axis).Refresh();
            UI.RequestRepaint();
        }

        // After layout: its size and what it holds, which its range comes from.
        internal void SetExtent(Vector2 viewport, Vector2 content, Insets reach)
        {
            Viewport = viewport;
            ContentSize = content;
            OnExtentChanged();
        }

        partial void OnEnableInput();

        partial void OnDisableInput();

        partial void OnExtentChanged();
    }
}
