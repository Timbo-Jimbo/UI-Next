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

        // Frames run so far: changes held for drawing are told which frame drew them.
        internal static int FrameIndex { get; private set; }
        private static UIRenderPass s_pass;

        // A screen size to lay roots out at instead of the screen's (captures at a size of their own).
        internal static Vector2? ScreenSizeOverride;

        // The game's safe area as last read while the game view rendered: Screen reports whichever view is rendering,
        // and the scene view's frames must not take the scene view's rect for it.
        private static Rect? s_safeArea;

        // Outside play mode, frames run as they would in play mode, but only step when told to (Advance): for tests
        // and captures that play a change frame by frame.
        internal static bool Simulating;

        /// <summary>Whether changes animate: in play mode, or while simulating.</summary>
        internal static bool Live => Application.isPlaying || Simulating;

        // A camera rendering to a texture that draws the UI all the same (a capture); every other one that renders to
        // a texture (a minimap, a portrait) leaves it out.
        internal static Camera CaptureCamera;

        /// <summary>Whether <paramref name="camera"/> draws the UI: the scene view, and game cameras drawing to the screen.</summary>
        internal static bool DrawsUI(Camera camera) =>
            camera.cameraType == CameraType.SceneView
            || (camera.cameraType == CameraType.Game && (camera.targetTexture == null || camera == CaptureCamera));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_lastStepped = -1;
            s_safeArea = null;
        }

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
            if (!DrawsUI(camera)) return;
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data) && camera.cameraType == CameraType.Game) return;
            var renderer = data != null ? data.scriptableRenderer : null;
            if (renderer == null && camera.cameraType == CameraType.SceneView)
            {
                var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                renderer = asset != null ? asset.scriptableRenderer : null;
            }
            renderer?.EnqueuePass(s_pass ??= new UIRenderPass());
        }

        /// <summary>
        /// Raised as a frame ends, everything in it laid out and drawn: for what follows it on screen (the text input
        /// system hands the platform where the text being edited is).
        /// </summary>
        internal static event System.Action Ended;

        /// <summary>Runs the frame: once per frame with stepping, and again (without stepping) whenever something is pending.</summary>
        public static void Run()
        {
            int frame = Time.frameCount;
            bool step = Application.isPlaying && frame != s_lastStepped;
            if (step) s_lastStepped = frame;
            Frame(step ? Time.unscaledDeltaTime : 0f);
        }

        /// <summary>While simulating, runs a frame <paramref name="dt"/> seconds on from the last.</summary>
        internal static void Advance(float dt) => Frame(Mathf.Max(0f, dt));

        private static void Frame(float dt)
        {
            using var frame = UIProfiling.Frame.Auto();
            FrameIndex++;
            using (UIProfiling.Roots.Auto())
                UpdateRoots();
            using (UIProfiling.Lists.Auto())
                ListView.ReconcileAll();
            UIScheduler.Flush(Phase.Layout);
            if (dt > 0f)
            {
                using (UIProfiling.Step.Auto())
                using (UIProfiling.Measure(UIProfiling.MotionTime))
                {
                    ScrollView.StepAll(dt);
                    Pressable.StepAll(dt);
                    SpringStore.Step(dt);
                }
            }
            else if (!Live && SpringStore.ActiveCount > 0)
            {
                SpringStore.StopAll();
            }
            UIScheduler.Flush(Phase.Draw);
            // A Finished handler may change what is drawn: it is drawn this frame, without stepping again.
            if (UITransaction.FlushFinished())
                UIScheduler.Flush(Phase.Draw);
            Compositor.Build();
            PendingSprings.LandUnread(FrameIndex);
            Ended?.Invoke();
            UIProfiling.CountFrame();
        }

        private static void UpdateRoots()
        {
            var pixelSize = ScreenPixelSize();
            // The screen's safe area is in its own pixels: a size of our own (a capture) has none to speak of, and only
            // the game view's frames read it (the scene view, rendering, reports its own rect as the screen's).
            var safeArea = new Rect(0f, 0f, pixelSize.x, pixelSize.y);
            if (Application.isPlaying && !ScreenSizeOverride.HasValue)
            {
                if (Mathf.Abs(Screen.width - pixelSize.x) < 1f && Mathf.Abs(Screen.height - pixelSize.y) < 1f)
                    s_safeArea = Screen.safeArea;
                if (s_safeArea.HasValue)
                    safeArea = s_safeArea.Value;
            }
            // By index: a foreach through the read-only list boxes its enumerator, every frame.
            var trees = UITree.Trees;
            for (int i = 0; i < trees.Count; i++)
            {
                var root = trees[i].Root;
                if (root != null)
                    root.UpdateMetrics(pixelSize, safeArea);
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
