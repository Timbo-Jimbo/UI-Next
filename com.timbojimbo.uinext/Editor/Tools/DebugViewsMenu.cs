using TimboJimbo.UINext;
using UnityEditor;
using UnityEditorInternal;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The debug views (<see cref="UIDebug"/>) turned on and off from Tools/UI Next/Debug, ticked while on, and kept on
    /// for the rest of the editor session, through script reloads and play mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class DebugViewsMenu
    {
        private const string Overdraw = "Tools/UI Next/Debug/Overdraw";
        private const string LayerBounds = "Tools/UI Next/Debug/Layer Bounds";
        private const string FocusPath = "Tools/UI Next/Debug/Focus Path";

        static DebugViewsMenu()
        {
            UIDebug.Overdraw = SessionState.GetBool(Overdraw, false);
            UIDebug.LayerBounds = SessionState.GetBool(LayerBounds, false);
            UIDebug.FocusPath = SessionState.GetBool(FocusPath, false);
        }

        [MenuItem(Overdraw, priority = 200)]
        private static void ToggleOverdraw() => UIDebug.Overdraw = Toggle(Overdraw, UIDebug.Overdraw);

        [MenuItem(Overdraw, true)]
        private static bool TickOverdraw() => Tick(Overdraw, UIDebug.Overdraw);

        [MenuItem(LayerBounds, priority = 201)]
        private static void ToggleLayerBounds() => UIDebug.LayerBounds = Toggle(LayerBounds, UIDebug.LayerBounds);

        [MenuItem(LayerBounds, true)]
        private static bool TickLayerBounds() => Tick(LayerBounds, UIDebug.LayerBounds);

        [MenuItem(FocusPath, priority = 202)]
        private static void ToggleFocusPath() => UIDebug.FocusPath = Toggle(FocusPath, UIDebug.FocusPath);

        [MenuItem(FocusPath, true)]
        private static bool TickFocusPath() => Tick(FocusPath, UIDebug.FocusPath);

        // Turns a view the other way, remembers it for the session, and shows it at once.
        private static bool Toggle(string item, bool on)
        {
            SessionState.SetBool(item, !on);
            InternalEditorUtility.RepaintAllViews();
            return !on;
        }

        private static bool Tick(string item, bool on)
        {
            Menu.SetChecked(item, on);
            return true;
        }
    }
}
