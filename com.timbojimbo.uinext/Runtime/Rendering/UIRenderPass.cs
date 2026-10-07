using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Draws the UI into the frame after the camera stack: after the final blit, at native resolution, as URP draws its
    /// own overlay UI. Enqueued on every camera; it draws on the last camera of a stack, and in the scene view.
    /// </summary>
    internal sealed class UIRenderPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public Camera Camera;
            public bool SceneView;
            public TextureHandle Color;
        }

        public UIRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRendering + 10;
            profilingSampler = new ProfilingSampler("UINext");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!Compositor.HasBatches) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            bool sceneView = cameraData.cameraType == CameraType.SceneView;
            if (!sceneView && (cameraData.cameraType != CameraType.Game || !cameraData.resolveFinalTarget)) return;

            using var builder = renderGraph.AddRasterRenderPass<PassData>("UINext", out var data, profilingSampler);
            data.Camera = cameraData.camera;
            data.SceneView = sceneView;
            data.Color = resourceData.activeColorTexture;
            builder.SetRenderAttachment(data.Color, 0, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
            {
                bool flipped = context.GetTextureUVOrigin(pass.Color) == TextureUVOrigin.BottomLeft;
                Compositor.Render(context.cmd, pass.Camera, pass.SceneView, flipped);
            });
        }
    }
}
