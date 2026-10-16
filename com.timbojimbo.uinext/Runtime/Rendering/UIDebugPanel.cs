using UnityEngine;
using UnityEngine.Rendering;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// UI Next's panel in the Rendering Debugger (Window › Analysis › Rendering Debugger in the editor; in a development
    /// build, its overlay: Ctrl+Backspace, a three-finger double tap, or both sticks pressed in): the debug views
    /// (<see cref="UIDebug"/>) to turn on and off, and what drawing the UI took in the last frame. Added through SRP Core's
    /// DebugManager, as URP adds its own panels.
    /// </summary>
    internal static class UIDebugPanel
    {
        private const string Name = "UI Next";

        // How often the numbers are read again, in seconds.
        private const float Refresh = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterInPlayer()
        {
            if (Debug.isDebugBuild || Application.isEditor)
                Register();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => Register();
#endif

        private static void Register()
        {
            var panel = DebugManager.instance.GetPanel(Name, createIfNull: true);
            if (panel.children.Count > 0) return;
            panel.children.Add(new DebugUI.Foldout
            {
                displayName = "Debug views",
                opened = true,
                children =
                {
                    new DebugUI.BoolField
                    {
                        displayName = "Overdraw",
                        tooltip = "How often each pixel is drawn over: what is behind the UI darkens, and the UI is brighter the more often it is drawn over.",
                        getter = () => UIDebug.Overdraw,
                        setter = on => UIDebug.Overdraw = on,
                    },
                    new DebugUI.BoolField
                    {
                        displayName = "Layer bounds",
                        tooltip = "Where each layer's image lies (a node drawn to an image of its own first): cyan fading as one, magenta blurred, yellow masked, green a flight's end.",
                        getter = () => UIDebug.LayerBounds,
                        setter = on => UIDebug.LayerBounds = on,
                    },
                    new DebugUI.BoolField
                    {
                        displayName = "Focus path",
                        tooltip = "What has keyboard and gamepad focus (white), and every section (teal) and group (orange) it is in.",
                        getter = () => UIDebug.FocusPath,
                        setter = on => UIDebug.FocusPath = on,
                    },
                },
            });
            panel.children.Add(new DebugUI.Foldout
            {
                displayName = "Last frame",
                opened = true,
                children =
                {
                    new DebugUI.Value
                    {
                        displayName = "Draw calls",
                        tooltip = "Draw calls in the main pass, and drawing layers' images.",
                        getter = DrawCalls,
                        refreshRate = Refresh,
                    },
                    new DebugUI.Value
                    {
                        displayName = "Primitives",
                        tooltip = "Shapes, shadows, images and glyphs drawn, layers' included.",
                        getter = () => Compositor.PrimitiveCount,
                        refreshRate = Refresh,
                    },
                    new DebugUI.Value
                    {
                        displayName = "Layers",
                        tooltip = "Nodes drawn to an image of their own first (fading as one, blurred, masked, a flight's ends), and their images' memory.",
                        getter = Layers,
                        refreshRate = Refresh,
                    },
                    new DebugUI.Value
                    {
                        displayName = "Offscreen nodes",
                        tooltip = "Nodes scrolled out from under a clip over them: not drawn, not drawn again until they come back, not hit.",
                        getter = () => Compositor.OffscreenNodes,
                        refreshRate = Refresh,
                    },
                    new DebugUI.Value
                    {
                        displayName = "Nodes",
                        tooltip = "Nodes in every root.",
                        getter = Nodes,
                        refreshRate = Refresh,
                    },
                    new DebugUI.Value
                    {
                        displayName = "Springs moving",
                        tooltip = "Values springing to where they are going.",
                        getter = () => SpringStore.ActiveCount,
                        refreshRate = Refresh,
                    },
                },
            });
        }

        private static object DrawCalls()
        {
            int main = Compositor.MainDrawCalls, layers = Compositor.DrawCalls - main;
            return layers > 0 ? (main + layers) + " (" + main + " main, " + layers + " for layers)" : main.ToString();
        }

        private static object Layers()
        {
            long bytes = 0;
            foreach (var group in Compositor.Groups)
                if (group.Target != null)
                    bytes += (long)group.Target.width * group.Target.height * 4;
            int count = Compositor.Groups.Count;
            return count == 0 ? "0" : count + " · " + (bytes >= 1 << 20 ? (bytes / (float)(1 << 20)).ToString("0.0") + " MB" : bytes / 1024 + " KB");
        }

        private static object Nodes()
        {
            int nodes = 0;
            var trees = UITree.Trees;
            int roots = trees.Count;
            for (int i = 0; i < roots; i++)
                nodes += trees[i].Nodes.Count;
            return nodes + " in " + roots + (roots == 1 ? " root" : " roots");
        }
    }
}
