using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Something drawn on a node: a box, an image, a label, or a drawable of your own. It draws in its node's space with
    /// a <see cref="Painter"/>; its <see cref="UIValue{T}"/> fields are found once per type, tied to the node (for its
    /// states), and every value it reads while drawing is tracked, so it draws again only when one of them changes.
    /// Implement <see cref="IMeasurable"/> as well for its node to fit to it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UINode))]
    public abstract class UIDrawable : MonoBehaviour
    {
        private static readonly Dictionary<Type, FieldInfo[]> s_valueFields = new();

        [NonSerialized] private UINode _node;
        [NonSerialized] private bool _enabled;
        [NonSerialized] private Unity.Profiling.ProfilerMarker _drawMarker;
        [NonSerialized] private bool _hasDrawMarker;

        // Its drawing's marker in the Profiler: "UINext.Draw.Label", by its type.
        internal Unity.Profiling.ProfilerMarker DrawMarker
        {
            get
            {
                if (_hasDrawMarker) return _drawMarker;
                _drawMarker = UIProfiling.MarkerFor("Draw." + GetType().Name);
                _hasDrawMarker = true;
                return _drawMarker;
            }
        }

        /// <summary>The node it draws on.</summary>
        public UINode Node => _node != null ? _node : _node = GetComponent<UINode>();

        /// <summary>Draws it. Values read here are tracked (in the draw phase they read as drawn), so it runs again when one changes.</summary>
        protected internal abstract void Draw(Painter painter);

        protected virtual void OnEnable()
        {
            _enabled = true;
            var node = Node;
            foreach (var value in Values())
            {
                value.Attach(node);
                if (value is IDrawnValue drawn)
                    drawn.CrossFades = true;
            }
            node.RegisterDrawable(this);
        }

        protected virtual void OnDisable()
        {
            _enabled = false;
            foreach (var value in Values())
                value.Detach();
            if (_node != null)
                _node.UnregisterDrawable(this);
        }

        protected virtual void OnValidate()
        {
            if (!_enabled) return;
            foreach (var value in Values())
                value.Refresh();
            ContentChanged();
            UI.RequestRepaint();
        }

        /// <summary>Asks for it to be drawn again and, if it measures, its tree laid out again (for state that is not a UIValue).</summary>
        protected void ContentChanged()
        {
            if (_node == null) return;
            _node.DrawComputation?.Invalidate();
            if (this is IMeasurable)
                _node.Tree?.ContentChanged(_node);
        }

        // Its UIValue fields, found once per type.
        private IEnumerable<IUIValue> Values()
        {
            var type = GetType();
            if (!s_valueFields.TryGetValue(type, out var fields))
            {
                var found = new List<FieldInfo>();
                for (var t = type; t != null && t != typeof(UIDrawable); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        if (typeof(IUIValue).IsAssignableFrom(field.FieldType))
                            found.Add(field);
                }
                fields = found.ToArray();
                s_valueFields[type] = fields;
            }
            foreach (var field in fields)
            {
                if (field.GetValue(this) is IUIValue value)
                    yield return value;
            }
        }
    }

    /// <summary>
    /// A node's drawing: runs its drawables into its primitive list, again whenever what they read changes. A node not
    /// seen as compositing last found it (hidden, inside something hidden, or faded to nothing) is not drawn: its drawing
    /// waits, watching nothing meanwhile, until compositing finds it seen again, which draws it then (a page not presented
    /// costs nothing as what is in it changes). So does one scrolled out from under a clip, once it has drawn: its drawing
    /// stays recorded, so the draw order holds while scrolling, and it is drawn again the frame it comes back.
    /// </summary>
    internal sealed class NodeDraw : Computation
    {
        private readonly UINode _node;

        public NodeDraw(UINode node) : base(Phase.Draw) => _node = node;

        protected override void Execute()
        {
            var tree = _node.Tree;
            int index = _node.Index;
            if (tree != null && index >= 0 && index < tree.Nodes.Count
                && (!tree.Shown[index] || tree.Opacities[index] <= 0f || (tree.Offscreen[index] && _node.HasDrawn)))
            {
                _node.DrawWaiting = true;
                return;
            }
            _node.DrawWaiting = false;
            _node.Instances.Clear();
            _node.InstanceTextures.Clear();
            _node.OverlayInstances.Clear();
            _node.OverlayTextures.Clear();
            if (_node.Display.Value == DisplayMode.Visible || _node.Leaving.Value)
            {
                // Cross-fading: the old drawing first, fading out, under the new one, fading in.
                float t = _node.CrossFadeProgress();
                if (t < 1f)
                {
                    using (UIProfiling.CrossFade.Auto())
                        _node.DrawOutgoing(1f - t);
                }
                int first = _node.Instances.Count, firstOverlay = _node.OverlayInstances.Count;
                var painter = new Painter(_node);
                var drawables = _node.Drawables;
                for (int i = 0; i < drawables.Count; i++)
                {
                    var drawable = drawables[i];
                    if (drawable != null && drawable.isActiveAndEnabled)
                    {
                        using (drawable.DrawMarker.Auto())
                            drawable.Draw(painter);
                    }
                }
                if (t < 1f)
                {
                    UINode.Fade(_node.Instances, first, t);
                    UINode.Fade(_node.OverlayInstances, firstOverlay, t);
                }
                // While focus shows on it, the focus ring, over the rest.
                if (_node.FocusVisible.Value && _node.FocusRing.Value)
                    UIFocus.DrawRing(_node, painter.Overlay);
            }
            Compositor.MarkRedrawn(_node);

            // What it reaches past its rect now; compositing looks again at whether it is in view if that changed.
            var reach = Vector4.zero;
            var union = default(Rect);
            bool outright = false;
            Bound(_node.Instances, ref reach, ref union, ref outright);
            Bound(_node.OverlayInstances, ref reach, ref union, ref outright);
            bool changed = reach != _node.PaintReach || outright != _node.PaintsOutright || (outright && union != _node.PaintUnion);
            _node.PaintReach = reach;
            _node.PaintUnion = union;
            _node.PaintsOutright = outright;
            _node.HasDrawn = true;
            if (changed && tree != null)
                tree.CompositeChanged(_node);
        }

        // Takes each primitive's rect, with the room round it the shader draws into (two points of smoothed edge, and a
        // shadow's blur, as PrimitiveKinds.hlsl has them; none for a glyph, whose rect has its own, or a group's image),
        // into what the node reaches past its edges (a primitive placed against them) or the rect round what it draws
        // outright.
        private static void Bound(List<PrimitiveInstance> instances, ref Vector4 reach, ref Rect union, ref bool outright)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                var type = (PrimitiveType)instance.Type;
                float margin = type == PrimitiveType.GlyphSdf || type == PrimitiveType.GlyphBitmap || type == PrimitiveType.Group || type == PrimitiveType.Pair
                    ? 0f : 2f + (type == PrimitiveType.Shadow ? instance.Params.y * 2.5f : 0f);
                var r = instance.Rect;
                if ((instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0)
                {
                    reach = Vector4.Max(reach, new Vector4(margin - r.x, margin - r.y, margin - r.z, margin - r.w));
                    continue;
                }
                var rect = Rect.MinMaxRect(r.x - margin, r.y - margin, r.x + r.z + margin, r.y + r.w + margin);
                union = outright
                    ? Rect.MinMaxRect(Mathf.Min(union.xMin, rect.xMin), Mathf.Min(union.yMin, rect.yMin), Mathf.Max(union.xMax, rect.xMax), Mathf.Max(union.yMax, rect.yMax))
                    : rect;
                outright = true;
            }
        }
    }
}
