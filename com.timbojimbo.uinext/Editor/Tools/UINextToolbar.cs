using System;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Label = UnityEngine.UIElements.Label;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The UI Next toolbar, among the Scene view's overlays: its name, States (a popup previewing states on the selected
    /// node, <see cref="StatesPopup"/>), the debug views (<see cref="UIDebug"/>: Overdraw, Layers, Focus path, each lit
    /// while it is on), and what drawing the UI took in the last frame. The debug views stay on for the rest of the editor
    /// session, through script reloads and play mode. Its icons are Unity's own, unless a PNG of the same name is in the
    /// package's Editor/Icons folder (see <see cref="UINextIcons"/>).
    /// </summary>
    [Overlay(typeof(SceneView), Id, "UI Next", true)]
    public sealed class UINextToolbar : ToolbarOverlay
    {
        private const string Id = "timbojimbo-uinext-toolbar";

        public UINextToolbar() : base(NameLabel.Id, StatesDropdown.Id, OverdrawToggle.Id, LayerBoundsToggle.Id, FocusPathToggle.Id,
            StatsLabel.Id)
        {
            collapsedIcon = UINextIcons.Get(UINextIcons.UINext);
        }
    }

    /// <summary>
    /// The UI Next toolbar's icons: a PNG of the icon's name in the package's Editor/Icons folder if there is one (a
    /// 16-point icon, with a 32-pixel one named "@2x" beside it for high-DPI screens), else one of Unity's own.
    /// </summary>
    internal static class UINextIcons
    {
        private const string Folder = "Packages/com.timbojimbo.uinext/Editor/Icons/";

        // Each icon's name, and the built-in icon standing in for it.
        public static readonly (string Name, string BuiltIn) UINext = ("UINext", "Canvas Icon");
        public static readonly (string Name, string BuiltIn) States = ("States", "AnimatorState Icon");
        public static readonly (string Name, string BuiltIn) Overdraw = ("Overdraw", "Profiler.Rendering");
        public static readonly (string Name, string BuiltIn) Layers = ("Layers", "SceneLayersToggle");
        public static readonly (string Name, string BuiltIn) FocusPath = ("FocusPath", "Keyboard");

        public static Texture2D Get((string Name, string BuiltIn) icon)
        {
            var own = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + icon.Name + ".png");
            if (own != null) return own;
            // The dark skin's variant first, where the editor wears it; FindTexture is quiet about a name it lacks.
            var builtIn = EditorGUIUtility.isProSkin ? EditorGUIUtility.FindTexture("d_" + icon.BuiltIn) : null;
            return builtIn != null ? builtIn : EditorGUIUtility.FindTexture(icon.BuiltIn);
        }
    }

    // The toolbar's name, so what follows is plainly UI Next's.
    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class NameLabel : VisualElement
    {
        public const string Id = "TimboJimbo.UINext/Name";

        public NameLabel()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.paddingLeft = 4;
            style.paddingRight = 6;
            tooltip = "UI Next: preview states on the selected node, and see what the UI costs and how focus moves.";
            var icon = UINextIcons.Get(UINextIcons.UINext);
            if (icon != null)
                Add(new Image { image = icon, style = { width = 16, height = 16, marginRight = 4 } });
            Add(new Label("UI Next") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        }
    }

    // States: opens the popup that previews states on the selected node; lit, with how many, while any are previewed.
    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class StatesDropdown : EditorToolbarDropdown
    {
        public const string Id = "TimboJimbo.UINext/States";

        public StatesDropdown()
        {
            icon = UINextIcons.Get(UINextIcons.States);
            tooltip = "UI Next: preview interaction states, modes and states of your own on the selected node, in the Scene and Game views.";
            clicked += () => UnityEditor.PopupWindow.Show(worldBound, new StatesPopup());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UIStatePreview.Changed += Refresh;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => UIStatePreview.Changed -= Refresh);
            Refresh();
        }

        private void Refresh()
        {
            int count = UIStatePreview.Count;
            text = count > 0 ? "States · " + count : "States";
            style.backgroundColor = count > 0 ? new StyleColor(StatesPopup.Lit) : new StyleColor(StyleKeyword.Null);
        }
    }

    // The debug views as the editor keeps them: put back from the session as scripts load, and kept as they change,
    // shown at once.
    [InitializeOnLoad]
    internal static class DebugViewsSession
    {
        private const string Overdraw = "TimboJimbo.UINext.Debug.Overdraw";
        private const string LayerBounds = "TimboJimbo.UINext.Debug.LayerBounds";
        private const string FocusPath = "TimboJimbo.UINext.Debug.FocusPath";

        static DebugViewsSession()
        {
            UIDebug.Overdraw = SessionState.GetBool(Overdraw, false);
            UIDebug.LayerBounds = SessionState.GetBool(LayerBounds, false);
            UIDebug.FocusPath = SessionState.GetBool(FocusPath, false);
            UIDebug.Changed += Keep;
        }

        private static void Keep()
        {
            SessionState.SetBool(Overdraw, UIDebug.Overdraw);
            SessionState.SetBool(LayerBounds, UIDebug.LayerBounds);
            SessionState.SetBool(FocusPath, UIDebug.FocusPath);
            InternalEditorUtility.RepaintAllViews();
        }
    }

    // A debug view's toggle: turns it on and off, lit while it is on, and kept in step with it however it is turned on
    // or off (from another Scene view's toolbar, or from code).
    internal abstract class DebugViewToggle : EditorToolbarToggle
    {
        private readonly Func<bool> _get;

        protected DebugViewToggle(Func<bool> get, Action<bool> set, (string Name, string BuiltIn) iconName, string label, string tip)
        {
            _get = get;
            icon = UINextIcons.Get(iconName);
            text = label;
            tooltip = tip;
            SetValueWithoutNotify(get());
            this.RegisterValueChangedCallback(e => set(e.newValue));
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                UIDebug.Changed += Sync;
                Sync();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => UIDebug.Changed -= Sync);
        }

        private void Sync() => SetValueWithoutNotify(_get());
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class OverdrawToggle : DebugViewToggle
    {
        public const string Id = "TimboJimbo.UINext/Debug/Overdraw";

        public OverdrawToggle() : base(() => UIDebug.Overdraw, on => UIDebug.Overdraw = on, UINextIcons.Overdraw, "Overdraw",
            "UI Next: how often each pixel is drawn over. What is behind the UI is darkened, and the UI is brighter the more often it is drawn over.") { }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class LayerBoundsToggle : DebugViewToggle
    {
        public const string Id = "TimboJimbo.UINext/Debug/LayerBounds";

        public LayerBoundsToggle() : base(() => UIDebug.LayerBounds, on => UIDebug.LayerBounds = on, UINextIcons.Layers, "Layers",
            "UI Next: where each layer's image lies (a node drawn to an image of its own first): cyan fading as one, magenta blurred, yellow masked, green a flight's end.") { }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class FocusPathToggle : DebugViewToggle
    {
        public const string Id = "TimboJimbo.UINext/Debug/FocusPath";

        public FocusPathToggle() : base(() => UIDebug.FocusPath, on => UIDebug.FocusPath = on, UINextIcons.FocusPath, "Focus path",
            "UI Next: what has keyboard and gamepad focus (white), and every section (teal) and group (orange) it is in.") { }
    }

    // What drawing the UI took in the last frame, kept up to date twice a second.
    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class StatsLabel : Label
    {
        public const string Id = "TimboJimbo.UINext/Stats";

        public StatsLabel()
        {
            style.unityTextAlign = TextAnchor.MiddleLeft;
            style.opacity = 0.7f;
            style.paddingLeft = 6;
            style.paddingRight = 4;
            tooltip = "UI Next: what drawing the UI took in the last frame: draw calls, primitives, and layers (nodes drawn to an image of their own first).";
            schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        private void Refresh() =>
            text = Count(Compositor.DrawCalls, "draw call") + " · " + Count(Compositor.PrimitiveCount, "primitive") + " · "
                   + Count(Compositor.Groups.Count, "layer");

        private static string Count(int count, string what) => count + " " + what + (count == 1 ? "" : "s");
    }
}
