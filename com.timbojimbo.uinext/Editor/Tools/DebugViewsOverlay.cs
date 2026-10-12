using System;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEditorInternal;
using UnityEngine.UIElements;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The debug views (<see cref="UIDebug"/>) as a toolbar in the Scene view (UI Next Debug, among its overlays): a toggle
    /// for each, lit while the view is on. They stay on for the rest of the editor session, through script reloads and
    /// play mode.
    /// </summary>
    [Overlay(typeof(SceneView), Id, "UI Next Debug", true)]
    public sealed class DebugViewsOverlay : ToolbarOverlay
    {
        private const string Id = "timbojimbo-uinext-debug-views";

        public DebugViewsOverlay() : base(OverdrawToggle.Id, LayerBoundsToggle.Id, FocusPathToggle.Id) { }
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

        protected DebugViewToggle(Func<bool> get, Action<bool> set, string label, string tip)
        {
            _get = get;
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

        public OverdrawToggle() : base(() => UIDebug.Overdraw, on => UIDebug.Overdraw = on, "Overdraw",
            "How often each pixel is drawn over: what is behind the UI darkened, and the UI brighter the more often it is drawn over.") { }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class LayerBoundsToggle : DebugViewToggle
    {
        public const string Id = "TimboJimbo.UINext/Debug/LayerBounds";

        public LayerBoundsToggle() : base(() => UIDebug.LayerBounds, on => UIDebug.LayerBounds = on, "Layers",
            "Where each layer's image lies: cyan fading as one, magenta blurred, yellow masked, green a flight's end.") { }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    internal sealed class FocusPathToggle : DebugViewToggle
    {
        public const string Id = "TimboJimbo.UINext/Debug/FocusPath";

        public FocusPathToggle() : base(() => UIDebug.FocusPath, on => UIDebug.FocusPath = on, "Focus",
            "What has focus (white), and every section (teal) and group (orange) it is in.") { }
    }
}
