using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// How a node comes and goes when its Display changes inside <see cref="UI.Animate(UIAnimation, Action)"/>, as
    /// SwiftUI's transitions: shown, it fades, grows or slides in from where it is laid out; hidden, it leaves layout
    /// at once (what is around it closes up, springing) but stays drawn where it was while it fades, shrinks or slides
    /// away, taking no pointers. Changed outside a change, it comes and goes at once. On top of the node's own drawn
    /// values, so they are never touched.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UINode))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Display Transition")]
    public sealed class DisplayTransition : MonoBehaviour
    {
        [SerializeField] private bool _fade = true;

        [Tooltip("Grows in from, and shrinks away to, this much of its size.")]
        [SerializeField, Range(0f, 1f)] private float _scale = 1f;

        [Tooltip("The side it slides in from and away past.")]
        [SerializeField] private DisplayEdge _edge = DisplayEdge.None;

        [Tooltip("Slides all the way past its parent's edge (a page), rather than its own size.")]
        [SerializeField] private bool _pastParent;

        [NonSerialized] private UINode _node;
        [NonSerialized] private Effect _watcher;
        [NonSerialized] private DisplayMode _last;
        [NonSerialized] private bool _entering;
        [NonSerialized] private UITransaction _leaving;

        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        public bool Fade
        {
            get => _fade;
            set => _fade = value;
        }

        /// <summary>How much of its size it grows in from and shrinks away to (1 for none).</summary>
        public float Scale
        {
            get => _scale;
            set => _scale = Mathf.Clamp01(value);
        }

        public DisplayEdge Edge
        {
            get => _edge;
            set => _edge = value;
        }

        public bool PastParent
        {
            get => _pastParent;
            set => _pastParent = value;
        }

        private void OnEnable()
        {
            var node = Node;
            _last = node.Display.Peek;
            node.EnteredLayout = OnEnteredLayout;
            _watcher = new Effect(Watch);
            _watcher.Run();
        }

        private void OnDisable()
        {
            _watcher?.Dispose();
            _watcher = null;
            if (_node == null) return;
            _node.EnteredLayout = null;
            _node.Leaving.Value = false;
            _node.Transition?.Set(Vector2.zero, 1f, 1f);
        }

        // Runs when Display changes: inside a change, it brings the node in (once layout says where) or takes it away.
        private void Watch()
        {
            var display = Node.Display.Value;
            if (display == _last) return;
            var was = _last;
            _last = display;
            var change = UITransaction.Current;
            Computation.Untracked(() =>
            {
                if (display == DisplayMode.Visible)
                {
                    if (_node.Leaving.Peek)
                    {
                        // Brought back while still going: it turns round from where it is drawn, where it was laid out.
                        _leaving = null;
                        _node.Leaving.Value = false;
                        if (change != null) _node.EnsureTransition().To(Vector2.zero, 1f, 1f);
                        else _node.Transition?.Set(Vector2.zero, 1f, 1f);
                        return;
                    }
                    _entering = change != null && was != DisplayMode.Visible;
                }
                else if (was == DisplayMode.Visible)
                {
                    _entering = false;
                    if (change == null || display != DisplayMode.None)
                    {
                        _node.Leaving.Value = false;
                        _node.Transition?.Set(Vector2.zero, 1f, 1f);
                        return;
                    }
                    Leave(change);
                }
            });
        }

        private void OnEnteredLayout(Rect rect)
        {
            if (!_entering) return;
            _entering = false;
            Out(rect, out var offset, out float scale, out float opacity);
            var transition = _node.EnsureTransition();
            transition.Set(offset, scale, opacity);
            transition.To(Vector2.zero, 1f, 1f);
        }

        private void Leave(UITransaction change)
        {
            _leaving = change;
            _node.Leaving.Value = true;
            Out(_node.LaidOutRect, out var offset, out float scale, out float opacity);
            _node.EnsureTransition().To(offset, scale, opacity);
            change.Finished += () =>
            {
                if (_leaving != change || this == null) return;
                _leaving = null;
                _node.Leaving.Value = false;
                _node.Transition?.Set(Vector2.zero, 1f, 1f);
            };
        }

        // Where it comes in from and goes away to, from where it is laid out in its parent.
        private void Out(Rect rect, out Vector2 offset, out float scale, out float opacity)
        {
            opacity = _fade ? 0f : 1f;
            scale = _scale;
            offset = Vector2.zero;
            var parent = _node.Parent != null ? _node.Parent.LaidOutRect.size : rect.size;
            switch (_edge)
            {
                case DisplayEdge.Left:
                    offset.x = _pastParent ? -rect.xMax : -rect.width;
                    break;
                case DisplayEdge.Right:
                    offset.x = _pastParent ? parent.x - rect.xMin : rect.width;
                    break;
                case DisplayEdge.Top:
                    offset.y = _pastParent ? -rect.yMax : -rect.height;
                    break;
                case DisplayEdge.Bottom:
                    offset.y = _pastParent ? parent.y - rect.yMin : rect.height;
                    break;
            }
        }
    }
}
