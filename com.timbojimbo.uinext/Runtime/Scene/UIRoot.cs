using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>How a root turns screen pixels into points.</summary>
    public enum UIScaleMode
    {
        /// <summary>A point is <see cref="UIRoot.Scale"/> pixels.</summary>
        ConstantScale,

        /// <summary>Points are scaled so the screen matches a reference size, by width, by height or between (as CanvasScaler).</summary>
        ReferenceSize,
    }

    /// <summary>
    /// The root of a UI: it covers the screen, in points, keeps its content clear of the safe area, and is drawn by the
    /// stack's own renderer after the camera stack, at native resolution. Roots draw in <see cref="SortOrder"/>. Its
    /// node's children (and theirs) are laid out inside it; plain objects in between are passed through.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UINode))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Root")]
    public sealed class UIRoot : MonoBehaviour
    {
        [Tooltip("Roots draw in this order, lowest first.")]
        [SerializeField] private int _sortOrder;

        [SerializeField] private UIScaleMode _scaleMode = UIScaleMode.ReferenceSize;

        [Tooltip("ReferenceSize: the screen size, in points, the layout is designed for.")]
        [SerializeField] private Vector2 _referenceSize = new(390f, 844f);

        [Tooltip("ReferenceSize: 0 matches the reference width, 1 its height, in between a blend.")]
        [SerializeField, Range(0f, 1f)] private float _match;

        [Tooltip("ConstantScale: pixels per point.")]
        [SerializeField, Min(0.01f)] private float _scale = 1f;

        [Tooltip("The edges it keeps its content clear of the screen's notch, rounded corners and home bar on.")]
        [SerializeField] private Edges _safeArea = Edges.All;

        internal UITree Tree;
        internal readonly Observable<Vector2> SizeObservable = new(new Vector2(390f, 844f));
        internal readonly Observable<Insets> SafeAreaObservable = new();

        private UINode _node;

        // Its RectTransform's size and pivot are the stack's, as a canvas's are: driven, so changing them dirties nothing.
        private DrivenRectTransformTracker _tracker;

        /// <summary>Its node: the top of its tree.</summary>
        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        /// <summary>Roots draw in this order, lowest first.</summary>
        public int SortOrder
        {
            get => _sortOrder;
            set
            {
                if (_sortOrder == value) return;
                _sortOrder = value;
                UITree.SortTrees();
            }
        }

        /// <summary>Its size in points.</summary>
        public Vector2 Size => SizeObservable.Peek;

        /// <summary>How many screen pixels a point is.</summary>
        public float PixelsPerPoint { get; private set; } = 1f;

        /// <summary>The screen size it covers, in pixels.</summary>
        public Vector2 PixelSize { get; private set; }

        /// <summary>Pixels per point under ConstantScale.</summary>
        public float Scale
        {
            get => _scale;
            set => _scale = Mathf.Max(0.01f, value);
        }

        /// <summary>A point on screen (pixels, y up from the bottom) in its space (points, y down from the top).</summary>
        public Vector2 ScreenToRoot(Vector2 screen) =>
            new(screen.x / PixelsPerPoint, (PixelSize.y - screen.y) / PixelsPerPoint);

        private void OnEnable()
        {
            _tracker.Add(this, Node.RectTransform, DrivenTransformProperties.Pivot | DrivenTransformProperties.SizeDelta);
            Tree = new UITree(this);
            UITree.Register(Tree);
            UIFrame.EnsureHooked();
            UI.RequestRepaint();
        }

        private void OnDisable()
        {
            _tracker.Clear();
            if (Tree != null)
            {
                UITree.Unregister(Tree);
                Tree.Dispose();
                Tree = null;
            }
            UI.RequestRepaint();
        }

        private void OnValidate() => UI.RequestRepaint();

        // Brings its size, scale and safe area up to date with the screen.
        internal void UpdateMetrics(Vector2 pixelSize, Rect safeAreaPixels)
        {
            if (pixelSize.x < 1f || pixelSize.y < 1f) return;
            PixelSize = pixelSize;
            float scale = _scale;
            if (_scaleMode == UIScaleMode.ReferenceSize && _referenceSize.x > 0f && _referenceSize.y > 0f)
            {
                float byWidth = Mathf.Log(pixelSize.x / _referenceSize.x, 2f);
                float byHeight = Mathf.Log(pixelSize.y / _referenceSize.y, 2f);
                scale = Mathf.Pow(2f, Mathf.Lerp(byWidth, byHeight, _match));
            }
            PixelsPerPoint = Mathf.Max(0.01f, scale);
            var size = pixelSize / PixelsPerPoint;
            SizeObservable.Value = size;

            var safe = default(Insets);
            if ((_safeArea & Edges.Left) != 0) safe.Left = Mathf.Max(0f, safeAreaPixels.xMin) / PixelsPerPoint;
            if ((_safeArea & Edges.Right) != 0) safe.Right = Mathf.Max(0f, pixelSize.x - safeAreaPixels.xMax) / PixelsPerPoint;
            if ((_safeArea & Edges.Top) != 0) safe.Top = Mathf.Max(0f, pixelSize.y - safeAreaPixels.yMax) / PixelsPerPoint;
            if ((_safeArea & Edges.Bottom) != 0) safe.Bottom = Mathf.Max(0f, safeAreaPixels.yMin) / PixelsPerPoint;
            SafeAreaObservable.Value = safe;

            // Its RectTransform shows where it is in the scene view: its top-left at its pivot, one unit a point.
            var rt = Node.RectTransform;
            var pivot = new Vector2(0f, 1f);
            if (rt.pivot != pivot) rt.pivot = pivot;
            if (rt.sizeDelta != size) rt.sizeDelta = size;
        }
    }
}
