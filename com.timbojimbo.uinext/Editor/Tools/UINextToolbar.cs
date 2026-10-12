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
    /// The UI Next toolbar, among the Scene view's overlays: its name, then States (previewing states on the selected
    /// node, <see cref="StatesPopup"/>), Debug (the debug views, <see cref="DebugPopup"/>) and Stats (what drawing the UI
    /// took in the last frame, <see cref="StatsPopup"/>), each a dropdown opening a popup. States and Debug are lit,
    /// with how many, while anything is previewed or on. Its icons are Unity's own, unless a PNG of the same name is in
    /// the package's Editor/Icons folder (see <see cref="UINextIcons"/>).
    /// </summary>
    [Overlay(typeof(SceneView), Id, "UI Next", true)]
    public sealed class UINextToolbar : ToolbarOverlay
    {
        private const string Id = "timbojimbo-uinext-toolbar";

        public UINextToolbar() : base(NameLabel.Id, StatesDropdown.Id, DebugDropdown.Id, StatsDropdown.Id)
        {
            collapsedIcon = UINextIcons.Get(UINextIcons.UINext);
        }

        // Lit while what a dropdown opens is in use.
        internal static readonly Color Lit = new(0.17f, 0.36f, 0.53f);
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
        public static readonly (string Name, string BuiltIn) Debug = ("Debug", "ViewToolZoom");
        public static readonly (string Name, string BuiltIn) Stats = ("Stats", "Profiler.Rendering");

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
            tooltip = "UI Next: preview states on the selected node, turn the debug views on, and see what drawing the UI takes.";
            var icon = UINextIcons.Get(UINextIcons.UINext);
            if (icon != null)
                Add(new Image { image = icon, style = { width = 16, height = 16, marginRight = 4 } });
            Add(new Label("UI Next") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        }
    }

    // A dropdown on the toolbar: its icon and name, opening its popup; lit, with how many, while what it opens is in use.
    internal abstract class ToolbarDropdown : EditorToolbarDropdown
    {
        private readonly string _name;

        protected ToolbarDropdown((string Name, string BuiltIn) iconName, string name, string tip)
        {
            _name = name;
            icon = UINextIcons.Get(iconName);
            text = name;
            tooltip = tip;
            clicked += () => UnityEditor.PopupWindow.Show(worldBound, Popup());
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Listen(true);
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => Listen(false));
        }

        protected abstract PopupWindowContent Popup();

        // How many of what it opens are in use (0 for none, or for a dropdown that counts nothing).
        protected virtual int InUse => 0;

        // Hears what changes how many are in use.
        protected virtual void Listen(bool on) { }

        protected void Refresh()
        {
            int count = InUse;
            text = count > 0 ? _name + " · " + count : _name;
            style.backgroundColor = count > 0 ? new StyleColor(UINextToolbar.Lit) : new StyleColor(StyleKeyword.Null);
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class StatesDropdown : ToolbarDropdown
    {
        public const string Id = "TimboJimbo.UINext/States";

        public StatesDropdown() : base(UINextIcons.States, "States",
            "UI Next: preview interaction states, modes and states of your own on the selected node, in the Scene and Game views.") { }

        protected override PopupWindowContent Popup() => new StatesPopup();

        protected override int InUse => UIStatePreview.Count;

        protected override void Listen(bool on)
        {
            UIStatePreview.Changed -= Refresh;
            if (on) UIStatePreview.Changed += Refresh;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class DebugDropdown : ToolbarDropdown
    {
        public const string Id = "TimboJimbo.UINext/Debug";

        public DebugDropdown() : base(UINextIcons.Debug, "Debug",
            "UI Next: the debug views: overdraw, layer bounds and the focus path, drawn over the UI.") { }

        protected override PopupWindowContent Popup() => new DebugPopup();

        protected override int InUse => (UIDebug.Overdraw ? 1 : 0) + (UIDebug.LayerBounds ? 1 : 0) + (UIDebug.FocusPath ? 1 : 0);

        protected override void Listen(bool on)
        {
            UIDebug.Changed -= Refresh;
            if (on) UIDebug.Changed += Refresh;
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class StatsDropdown : ToolbarDropdown
    {
        public const string Id = "TimboJimbo.UINext/Stats";

        public StatsDropdown() : base(UINextIcons.Stats, "Stats",
            "UI Next: what drawing the UI took in the last frame: draw calls, primitives, layers, nodes and springs.") { }

        protected override PopupWindowContent Popup() => new StatsPopup();
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
}
