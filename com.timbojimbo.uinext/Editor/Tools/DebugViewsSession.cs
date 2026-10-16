using TimboJimbo.UINext;
using UnityEditor;
using UnityEditorInternal;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The debug views (<see cref="UIDebug"/>) kept on for the rest of the editor session, however they are turned on (the
    /// Rendering Debugger's UI Next panel, or code): put back as scripts load, kept as they change, and shown at once.
    /// </summary>
    [InitializeOnLoad]
    internal static class DebugViewsSession
    {
        private const string Overdraw = "TimboJimbo.UINext.Debug.Overdraw";
        private const string LayerBounds = "TimboJimbo.UINext.Debug.LayerBounds";
        private const string FocusPath = "TimboJimbo.UINext.Debug.FocusPath";
        private const string SubtreeMatches = "TimboJimbo.UINext.Debug.SubtreeMatches";

        static DebugViewsSession()
        {
            UIDebug.Overdraw = SessionState.GetBool(Overdraw, false);
            UIDebug.LayerBounds = SessionState.GetBool(LayerBounds, false);
            UIDebug.FocusPath = SessionState.GetBool(FocusPath, false);
            UIDebug.SubtreeMatches = SessionState.GetBool(SubtreeMatches, false);
            UIDebug.Changed += Keep;
        }

        private static void Keep()
        {
            SessionState.SetBool(Overdraw, UIDebug.Overdraw);
            SessionState.SetBool(LayerBounds, UIDebug.LayerBounds);
            SessionState.SetBool(FocusPath, UIDebug.FocusPath);
            SessionState.SetBool(SubtreeMatches, UIDebug.SubtreeMatches);
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
