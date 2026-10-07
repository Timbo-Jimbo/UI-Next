using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The frame, once per rendered frame, just before cameras render (play mode and edit mode alike): bindings and
    /// values resolve, trees that changed lay out, springs step, what moved or changed is composited and drawn, and the
    /// compositor brings its buffers up to date. Then the render pass draws it.
    /// </summary>
    internal static class UIFrame
    {
        private static bool s_hooked;
        private static int s_lastStepped = -1;
        private static UIRenderPass s_pass;

        // A screen size to lay roots out at instead of the screen's (captures at a size of their own).
        internal static Vector2? ScreenSizeOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_lastStepped = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void EnsureHooked()
        {
            if (s_hooked) return;
            s_hooked = true;
            RenderPipelineManager.beginContextRendering += OnBeginContext;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            UIInput.Hook();
        }

        private static void OnBeginContext(ScriptableRenderContext context, List<Camera> cameras) => Run();

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) && camera.cameraType == CameraType.Game) return;
            var renderer = data != null ? data.scriptableRenderer : null;
            if (renderer == null && camera.cameraType == CameraType.SceneView)
            {
                var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                renderer = asset != null ? asset.scriptableRenderer : null;
            }
            renderer?.EnqueuePass(s_pass ??= new UIRenderPass());
        }

        /// <summary>Runs the frame: once per frame with stepping, and again (without stepping) whenever something is pending.</summary>
        public static void Run()
        {
            int frame = Time.frameCount;
            bool playing = Application.isPlaying;
            bool step = playing && frame != s_lastStepped;
            if (step) s_lastStepped = frame;

            UpdateRoots();
            UIScheduler.Flush(Phase.Layout);
            if (step)
                SpringStore.Step(Time.unscaledDeltaTime);
            else if (!playing && SpringStore.ActiveCount > 0)
                SpringStore.StopAll();
            UIScheduler.Flush(Phase.Draw);
            // A Finished handler may change what is drawn: it is drawn this frame, without stepping again.
            if (UITransaction.FlushFinished())
                UIScheduler.Flush(Phase.Draw);
            Compositor.Build();
        }

        private static void UpdateRoots()
        {
            var pixelSize = ScreenPixelSize();
            var safeArea = Screen.safeArea;
            if (!Application.isPlaying)
                safeArea = new Rect(0f, 0f, pixelSize.x, pixelSize.y);
            foreach (var tree in UITree.Trees)
            {
                if (tree.Root != null)
                    tree.Root.UpdateMetrics(pixelSize, safeArea);
            }
        }

        // The game view's size: in the editor the main game view's (Screen reports whichever view is rendering).
        internal static Vector2 ScreenPixelSize()
        {
            if (ScreenSizeOverride is { } overridden) return overridden;
#if UNITY_EDITOR
            var size = UnityEditor.Handles.GetMainGameViewSize();
            if (size.x >= 1f && size.y >= 1f) return size;
#endif
            return new Vector2(Screen.width, Screen.height);
        }
    }
}
