using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// A long list that makes only the rows near view, as UICollectionView and SwiftUI's LazyVStack do. On a node with a
    /// vertical <see cref="ScrollView"/>, it keeps a node inside it as tall as all its rows, and the rows near view
    /// floating in it where they go: made by <see cref="CreateCell"/>, filled for their row by <see cref="BindCell"/>, and
    /// reused as they scroll away. A row is as tall as its cell lays out to; until it has been made, it counts as
    /// <see cref="EstimatedRowHeight"/>; as rows above the view are measured, the offset moves with them, so what is in
    /// view stays put. Rows are kept made <see cref="Overscan"/> viewports past what is in view, so a quick scroll never
    /// shows one not laid out yet.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollView))]
    [AddComponentMenu("Timbo Jimbo/UI Next/List View")]
    public sealed class ListView : MonoBehaviour
    {
        // Every list enabled, made up to date once a frame before layout.
        private static readonly List<ListView> s_lists = new();

        [Tooltip("How tall a row counts as until it has been made and measured, in points.")]
        [SerializeField, Min(1f)] private float _estimatedRowHeight = 60f;

        [Tooltip("The space between rows, in points.")]
        [SerializeField, Min(0f)] private float _gap;

        [Tooltip("How far past what is in view rows are kept made, in viewports.")]
        [SerializeField, Min(0f)] private float _overscan = 1f;

        [NonSerialized] private ScrollView _scroll;
        [NonSerialized] private UINode _rows;
        [NonSerialized] private int _count;

        // Each row's height as measured (negative until it has been made), and where each row starts (the last entry,
        // where they all end), worked out again when a height changes.
        [NonSerialized] private readonly List<float> _heights = new();
        [NonSerialized] private float[] _tops = { 0f };
        [NonSerialized] private bool _topsStale = true;

        // The cells in use, by row, and those put away to be used again.
        [NonSerialized] private readonly Dictionary<int, UINode> _cells = new();
        [NonSerialized] private readonly Stack<UINode> _spare = new();
        [NonSerialized] private readonly List<int> _leaving = new();
        [NonSerialized] private bool _rebind;

        /// <summary>Makes a cell inside the given node (the list's rows): one row's worth of nodes, filled later.</summary>
        public Func<UINode, UINode> CreateCell { get; set; }

        /// <summary>Fills a cell for a row: called as it comes near view, and for every cell made on <see cref="Reload"/>.</summary>
        public Action<UINode, int> BindCell { get; set; }

        /// <summary>How many rows it has.</summary>
        public int Count => _count;

        /// <summary>How many cells it has made and is using now: those near view.</summary>
        public int CellCount => _cells.Count;

        /// <summary>How tall a row counts as until it has been made and measured, in points.</summary>
        public float EstimatedRowHeight
        {
            get => _estimatedRowHeight;
            set
            {
                _estimatedRowHeight = Mathf.Max(1f, value);
                _topsStale = true;
            }
        }

        /// <summary>The space between rows, in points.</summary>
        public float Gap
        {
            get => _gap;
            set
            {
                _gap = Mathf.Max(0f, value);
                _topsStale = true;
            }
        }

        /// <summary>How far past what is in view rows are kept made, in viewports.</summary>
        public float Overscan
        {
            get => _overscan;
            set => _overscan = Mathf.Max(0f, value);
        }

        private UINode Node => _scroll != null ? _scroll.Node : null;

        /// <summary>Gives it <paramref name="count"/> rows, every cell made filled again (the rows' data changed).</summary>
        public void SetCount(int count)
        {
            count = Mathf.Max(0, count);
            if (count < _heights.Count)
                _heights.RemoveRange(count, _heights.Count - count);
            while (_heights.Count < count)
                _heights.Add(-1f);
            _count = count;
            _topsStale = true;
            _rebind = true;
            Reconcile();
        }

        /// <summary>Fills every cell made again, for rows whose data changed; heights are measured again as they lay out.</summary>
        public void Reload()
        {
            _rebind = true;
            Reconcile();
        }

        /// <summary>Where a row starts, in points down the list (by measured heights, and estimated ones for rows not made yet).</summary>
        public float TopOf(int index)
        {
            EnsureTops();
            return _tops[Mathf.Clamp(index, 0, _count)];
        }

        /// <summary>The cell showing a row, if it is made.</summary>
        public bool TryGetCell(int index, out UINode cell) => _cells.TryGetValue(index, out cell);

        /// <summary>Scrolls a row to the top of the view, as far as the list goes, and makes the rows there.</summary>
        public void ScrollTo(int index, bool animated = true)
        {
            if (_scroll == null) return;
            float range = Mathf.Max(0f, TopOf(_count) - _scroll.Viewport.y);
            _scroll.ScrollTo(new Vector2(0f, Mathf.Min(TopOf(index), range)), animated);
            Reconcile();
        }

        private void OnEnable()
        {
            _scroll = GetComponent<ScrollView>();
            s_lists.Add(this);
        }

        private void OnDisable() => s_lists.Remove(this);

        /// <summary>Once a frame, before layout: every list makes the rows near view.</summary>
        internal static void ReconcileAll()
        {
            for (int i = s_lists.Count - 1; i >= 0; i--)
            {
                if (s_lists[i] == null)
                    s_lists.RemoveAt(i);
                else
                    s_lists[i].Reconcile();
            }
        }

        // Takes up the heights the cells laid out to, puts away the cells gone out of reach, makes and fills those come
        // into it, and places them all.
        private void Reconcile()
        {
            var node = Node;
            if (CreateCell == null || BindCell == null || node == null || !isActiveAndEnabled) return;
            EnsureRows();

            // What is in view is anchored to the row at its top: rows above it measuring other than they were counted
            // move the offset with them, not what is in view.
            EnsureTops();
            float offset = _scroll.PresentedOffset.y;
            int anchor = _count > 0 ? RowAt(offset) : -1;
            float intoAnchor = anchor >= 0 ? offset - _tops[anchor] : 0f;

            foreach (var pair in _cells)
            {
                float height = pair.Value.LaidOutRect.height;
                if (height > 0f && pair.Key < _heights.Count && !Mathf.Approximately(height, _heights[pair.Key]))
                {
                    _heights[pair.Key] = height;
                    _topsStale = true;
                }
            }
            EnsureTops();
            if (anchor >= 0 && anchor < _count)
            {
                float shift = _tops[anchor] + intoAnchor - offset;
                if (Mathf.Abs(shift) > 0.01f)
                {
                    _scroll.ShiftBy(new Vector2(0f, shift));
                    offset += shift;
                }
            }

            float viewport = _scroll.Viewport.y > 0f ? _scroll.Viewport.y : node.Size.y;
            int first = RowAt(offset - viewport * _overscan);
            int last = _count == 0 ? -1 : RowAt(offset + viewport * (1f + _overscan));

            _leaving.Clear();
            foreach (var pair in _cells)
            {
                if (pair.Key < first || pair.Key > last)
                    _leaving.Add(pair.Key);
            }
            foreach (int index in _leaving)
            {
                var cell = _cells[index];
                _cells.Remove(index);
                if (cell == null) continue;
                cell.Display.Value = DisplayMode.None;
                _spare.Push(cell);
            }

            for (int i = first; i <= last; i++)
            {
                bool made = false;
                if (!_cells.TryGetValue(i, out var cell) || cell == null)
                {
                    cell = Take();
                    if (cell == null) break;
                    _cells[i] = cell;
                    made = true;
                }
                if (made || _rebind)
                    BindCell(cell, i);
                Place(cell, _tops[i]);
            }
            _rebind = false;

            float total = Mathf.Max(0f, _tops[_count] - (_count > 0 ? _gap : 0f));
            var size = _rows.Height.Peek;
            if (size.Mode != SizingMode.Fixed || !Mathf.Approximately(size.Value, total))
                _rows.Height.Value = Sizing.Fixed(total);
        }

        // A spare cell shown again, or a new one: floating in the rows, as wide as them.
        private UINode Take()
        {
            while (_spare.Count > 0)
            {
                var spare = _spare.Pop();
                if (spare == null) continue;
                spare.Display.Value = DisplayMode.Visible;
                return spare;
            }
            var cell = CreateCell(_rows);
            if (cell == null) return null;
            Hide(cell.gameObject);
            cell.Width.Value = Sizing.Percent(1f);
            return cell;
        }

        private static void Place(UINode cell, float top)
        {
            var floating = cell.Floating.Peek;
            if (floating.AttachTo == FloatingAttach.Parent && floating.Point == AttachPoint.LeftTop && floating.TargetPoint == AttachPoint.LeftTop
                && Mathf.Approximately(floating.Offset.y, top) && floating.Offset.x == 0f)
                return;
            cell.Floating.Value = new Floating
            {
                AttachTo = FloatingAttach.Parent,
                Point = AttachPoint.LeftTop,
                TargetPoint = AttachPoint.LeftTop,
                Offset = new Vector2(0f, top),
            };
        }

        // The node the rows float in, made the first time (never saved: the list makes it again).
        private void EnsureRows()
        {
            if (_rows != null) return;
            _rows = UIBuild.Node("Rows", transform);
            Hide(_rows.gameObject);
            _rows.Width.Value = Sizing.Grow();
            _rows.Height.Value = Sizing.Fixed(0f);
        }

        private static void Hide(GameObject go) => go.hideFlags |= HideFlags.DontSave;

        private void EnsureTops()
        {
            if (!_topsStale && _tops.Length == _count + 1) return;
            if (_tops.Length != _count + 1)
                _tops = new float[_count + 1];
            float y = 0f;
            for (int i = 0; i < _count; i++)
            {
                _tops[i] = y;
                float height = _heights[i] >= 0f ? _heights[i] : _estimatedRowHeight;
                y += height + _gap;
            }
            _tops[_count] = y;
            _topsStale = false;
        }

        // The row at a point down the list: the last one starting at or above it, within the list.
        private int RowAt(float y)
        {
            if (_count == 0) return 0;
            int low = 0, high = _count - 1;
            while (low < high)
            {
                int mid = (low + high + 1) >> 1;
                if (_tops[mid] <= y) low = mid;
                else high = mid - 1;
            }
            return low;
        }
    }
}
