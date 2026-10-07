using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Draws the UI into the frame after the camera stack: after the final blit, at native resolution, as URP draws its
    /// own overlay UI. Enqueued on every camera; it draws on the last camera of a stack, and in the scene view. Groups
    /// (nodes fading as one image) are drawn to their images first, innermost first.
    /// </summary>
    internal sealed class UIRenderPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public Camera Camera;
            public bool SceneView;
            public TextureHandle Color;
        }

        private sealed class GroupPassData
        {
            public int Index;
            public TextureHandle Target;
        }

        // The render graph's handles on the groups' images, made once an image.
        private static readonly Dictionary<RenderTexture, RTHandle> s_handles = new();
        private static readonly List<TextureHandle> s_groupTextures = new();

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
            if (!UIFrame.DrawsUI(cameraData.camera)) return;

            // Each group's image, innermost first: an outer group's image draws the inner ones'.
            s_groupTextures.Clear();
            var groups = Compositor.Groups;
            for (int g = groups.Count - 1; g >= 0; g--)
            {
                var target = groups[g].Target;
                if (target == null) continue;
                var handle = renderGraph.ImportTexture(HandleFor(target));
                s_groupTextures.Add(handle);
                using var groupBuilder = renderGraph.AddRasterRenderPass<GroupPassData>("UINext Group", out var groupData, profilingSampler);
                groupData.Index = g;
                groupData.Target = handle;
                groupBuilder.SetRenderAttachment(handle, 0, AccessFlags.Write);
                for (int i = 0; i < s_groupTextures.Count - 1; i++)
                    groupBuilder.UseTexture(s_groupTextures[i], AccessFlags.Read);
                groupBuilder.AllowPassCulling(false);
                groupBuilder.SetRenderFunc(static (GroupPassData pass, RasterGraphContext context) =>
                {
                    bool flipped = context.GetTextureUVOrigin(pass.Target) == TextureUVOrigin.BottomLeft;
                    Compositor.RenderGroup(context.cmd, pass.Index, flipped);
                });
            }

            using var builder = renderGraph.AddRasterRenderPass<PassData>("UINext", out var data, profilingSampler);
            data.Camera = cameraData.camera;
            data.SceneView = sceneView;
            data.Color = resourceData.activeColorTexture;
            builder.SetRenderAttachment(data.Color, 0, AccessFlags.ReadWrite);
            foreach (var groupTexture in s_groupTextures)
                builder.UseTexture(groupTexture, AccessFlags.Read);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
            {
                bool flipped = context.GetTextureUVOrigin(pass.Color) == TextureUVOrigin.BottomLeft;
                Compositor.Render(context.cmd, pass.Camera, pass.SceneView, flipped);
            });
        }

        private static RTHandle HandleFor(RenderTexture target)
        {
            if (s_handles.TryGetValue(target, out var handle) && handle != null && handle.rt == target) return handle;
            // Images replaced since (the screen resized) are let go of.
            var stale = new List<RenderTexture>();
            foreach (var pair in s_handles)
                if (pair.Key == null)
                    stale.Add(pair.Key);
            foreach (var key in stale)
            {
                s_handles[key]?.Release();
                s_handles.Remove(key);
            }
            handle = RTHandles.Alloc(target);
            s_handles[target] = handle;
            return handle;
        }
    }
}
