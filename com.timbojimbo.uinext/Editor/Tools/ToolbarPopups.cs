using System;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Label = UnityEngine.UIElements.Label;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// A popup from the UI Next toolbar: built with UI Toolkit into its window as it opens, as tall as what it holds.
    /// </summary>
    internal abstract class ToolbarPopup : PopupWindowContent
    {
        private float _height = 120f;

        protected VisualElement Content { get; private set; }

        protected virtual float Width => 300f;

        // Sized to what it holds, measured once it is laid out.
        public override Vector2 GetWindowSize() => new(Width, _height);

        public override void OnGUI(Rect rect) { }

        public override void OnOpen()
        {
            Content = new VisualElement { style = { paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 8 } };
            Content.RegisterCallback<GeometryChangedEvent>(e =>
            {
                _height = Mathf.Ceil(e.newRect.height);
                editorWindow.Repaint();
            });
            editorWindow.rootVisualElement.Add(Content);
            Build();
        }

        protected abstract void Build();

        // A bold line heading the popup.
        protected Label Title(string text)
        {
            var label = new Label(text) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } };
            Content.Add(label);
            return label;
        }

        // A small, faint line of explanation.
        protected static Label Note(string text) =>
            new(text) { style = { opacity = 0.65f, fontSize = 10, whiteSpace = WhiteSpace.Normal } };

        // A colour swatch and what it stands for, for a view's key.
        protected static VisualElement Swatch(Color colour, string meaning)
        {
            var swatch = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 8 } };
            swatch.Add(new VisualElement
            {
                style =
                {
                    width = 9, height = 9, marginRight = 3, backgroundColor = colour,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2, borderBottomLeftRadius = 2, borderBottomRightRadius = 2,
                },
            });
            swatch.Add(new Label(meaning) { style = { fontSize = 10 } });
            return swatch;
        }
    }

    /// <summary>
    /// The UI Next toolbar's Debug popup: the debug views (<see cref="UIDebug"/>), each a toggle with what it shows and
    /// its colours' key, kept in step with them however they are turned on or off.
    /// </summary>
    internal sealed class DebugPopup : ToolbarPopup
    {
        private Toggle _overdraw, _layers, _focus;

        protected override void Build()
        {
            Title("Debug views");
            _overdraw = View("Overdraw", v => UIDebug.Overdraw = v,
                "How often each pixel is drawn over: what is behind the UI darkens, and the UI is brighter the more often it is drawn over.");
            _layers = View("Layer bounds", v => UIDebug.LayerBounds = v,
                "Where each layer's image lies (a node drawn to an image of its own first):",
                Swatch(Compositor.DebugFading, "fading as one"), Swatch(Compositor.DebugBlurred, "blurred"),
                Swatch(Compositor.DebugMasked, "masked"), Swatch(Compositor.DebugFlight, "a flight's end"));
            _focus = View("Focus path", v => UIDebug.FocusPath = v,
                "What has keyboard and gamepad focus, and every section and group it is in:",
                Swatch(Compositor.DebugFocused, "what has focus"), Swatch(Compositor.DebugSection, "a section"),
                Swatch(Compositor.DebugGroup, "a group"));
            var note = Note("Drawn in the Scene and Game views, and kept on for this editor session.");
            note.style.marginTop = 6;
            Content.Add(note);
            Sync();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            UIDebug.Changed += Sync;
        }

        public override void OnClose() => UIDebug.Changed -= Sync;

        // A view's toggle, what it shows, and its colours' key, if it has one.
        private Toggle View(string name, Action<bool> set, string about, params VisualElement[] key)
        {
            var toggle = new Toggle { text = name, style = { marginTop = 4 } };
            toggle.RegisterValueChangedCallback(e => set(e.newValue));
            Content.Add(toggle);
            var note = Note(about);
            note.style.marginLeft = 4;
            Content.Add(note);
            if (key.Length > 0)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = UnityEngine.UIElements.Wrap.Wrap, marginLeft = 4, marginTop = 2 } };
                foreach (var swatch in key)
                    row.Add(swatch);
                Content.Add(row);
            }
            return toggle;
        }

        private void Sync()
        {
            _overdraw?.SetValueWithoutNotify(UIDebug.Overdraw);
            _layers?.SetValueWithoutNotify(UIDebug.LayerBounds);
            _focus?.SetValueWithoutNotify(UIDebug.FocusPath);
        }
    }

    /// <summary>
    /// The UI Next toolbar's Stats popup: what drawing the UI took in the last frame, kept up to date twice a second.
    /// </summary>
    internal sealed class StatsPopup : ToolbarPopup
    {
        private Label _drawCalls, _primitives, _layers, _nodes, _springs;

        protected override void Build()
        {
            Title("Last frame");
            _drawCalls = Row("Draw calls", "Draw calls in the main pass, and drawing layers' images.");
            _primitives = Row("Primitives", "Shapes, shadows, images and glyphs drawn, layers' included.");
            _layers = Row("Layers", "Nodes drawn to an image of their own first (fading as one, blurred, masked, a flight's ends), and their images' memory.");
            _nodes = Row("Nodes", "Nodes in every root.");
            _springs = Row("Springs moving", "Values springing to where they are going.");
            Content.schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        // A row: what it counts at the left, its count at the right.
        private Label Row(string name, string tip)
        {
            var row = new VisualElement { tooltip = tip, style = { flexDirection = FlexDirection.Row, marginTop = 1 } };
            row.Add(new Label(name) { style = { flexGrow = 1, opacity = 0.75f } });
            var value = new Label { style = { unityTextAlign = TextAnchor.MiddleRight } };
            row.Add(value);
            Content.Add(row);
            return value;
        }

        private void Refresh()
        {
            int main = Compositor.Batches.Count, layers = Compositor.GroupBatches.Count;
            _drawCalls.text = layers > 0 ? (main + layers) + " (" + main + " main, " + layers + " for layers)" : main.ToString();
            _primitives.text = Compositor.PrimitiveCount.ToString();
            long bytes = 0;
            foreach (var group in Compositor.Groups)
                if (group.Target != null)
                    bytes += (long)group.Target.width * group.Target.height * 4;
            _layers.text = Compositor.Groups.Count > 0 ? Compositor.Groups.Count + " · " + EditorUtility.FormatBytes(bytes) : "0";
            int nodes = 0, roots = 0;
            foreach (var tree in UITree.Trees)
            {
                roots++;
                nodes += tree.Nodes.Count;
            }
            _nodes.text = nodes + " in " + roots + (roots == 1 ? " root" : " roots");
            _springs.text = SpringStore.ActiveCount.ToString();
        }
    }
}
