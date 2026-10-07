using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Draws the UI into the frame after the camera stack: after the final blit, at native resolution, as URP draws its
    /// own overlay UI. Enqueued on every camera; it draws on the last camera of a stack, and in the scene view. Groups
    /// (nodes fading or blurred as one image) are drawn to their images first, innermost first, and blurred ones are
    /// blurred there by dual Kawase passes.
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

        private sealed class BlurPassData
        {
            public TextureHandle Source;
            public Vector4 Spread;
            public int Pass;
        }

        private static readonly int s_blitTexture = Shader.PropertyToID("_BlitTexture");
        private static readonly int s_blitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int s_blurSpread = Shader.PropertyToID("_UINextBlur");
        private static readonly MaterialPropertyBlock s_blurProperties = new();
        private static readonly List<TextureHandle> s_levels = new();
        private static readonly List<Vector2Int> s_levelSizes = new();
        private static Material s_blurMaterial;

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
                using (var groupBuilder = renderGraph.AddRasterRenderPass<GroupPassData>("UINext Group", out var groupData, profilingSampler))
                {
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
                // Blurred after it is drawn (its pass recorded first: passes are recorded one at a time).
                if (groups[g].Blur > 0.01f)
                    Blur(renderGraph, handle, target.width, target.height, groups[g].Blur);
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

        // Blurs an image by `radius` pixels: down to half its size a step at a time, then back up into it. Each step doubles
        // the blur; how far apart each step's samples are spread makes up the rest, so the radius moves smoothly.
        private void Blur(RenderGraph renderGraph, TextureHandle image, int width, int height, float radius)
        {
            var material = BlurMaterial;
            if (material == null) return;
            int steps = Mathf.Clamp(Mathf.FloorToInt(Mathf.Log(Mathf.Max(radius, 1f), 2f)), 1, 6);
            float spread = radius / (1 << steps);
            s_levels.Clear();
            s_levelSizes.Clear();
            s_levels.Add(image);
            s_levelSizes.Add(new Vector2Int(width, height));
            for (int i = 1; i <= steps; i++)
            {
                var size = new Vector2Int(Mathf.Max(1, s_levelSizes[i - 1].x / 2), Mathf.Max(1, s_levelSizes[i - 1].y / 2));
                s_levelSizes.Add(size);
                s_levels.Add(renderGraph.CreateTexture(new TextureDesc(size.x, size.y)
                {
                    colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "UINext Blur",
                }));
            }
            for (int i = 0; i < steps; i++)
                BlurStep(renderGraph, s_levels[i], s_levelSizes[i], s_levels[i + 1], spread, pass: 0);
            for (int i = steps; i > 0; i--)
                BlurStep(renderGraph, s_levels[i], s_levelSizes[i], s_levels[i - 1], spread, pass: 1);
        }

        private void BlurStep(RenderGraph renderGraph, TextureHandle source, Vector2Int sourceSize, TextureHandle destination, float spread, int pass)
        {
            using var builder = renderGraph.AddRasterRenderPass<BlurPassData>("UINext Blur", out var data, profilingSampler);
            data.Source = source;
            data.Spread = new Vector4(0.5f / sourceSize.x * spread, 0.5f / sourceSize.y * spread, 0f, 0f);
            data.Pass = pass;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (BlurPassData step, RasterGraphContext context) =>
            {
                // A block of its own for each draw: its values are taken as the draw is recorded.
                s_blurProperties.Clear();
                s_blurProperties.SetTexture(s_blitTexture, ((RTHandle)step.Source).rt);
                s_blurProperties.SetVector(s_blitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                s_blurProperties.SetVector(s_blurSpread, step.Spread);
                context.cmd.DrawProcedural(Matrix4x4.identity, s_blurMaterial, step.Pass, MeshTopology.Triangles, 3, 1, s_blurProperties);
            });
        }

        /// <summary>Lets go of the blur's material (before a script reload, and on quit).</summary>
        internal static void ReleaseResources()
        {
            if (s_blurMaterial != null)
                Object.DestroyImmediate(s_blurMaterial);
            s_blurMaterial = null;
        }

        private static Material BlurMaterial
        {
            get
            {
                if (s_blurMaterial != null) return s_blurMaterial;
                var shader = Resources.Load<Shader>("UINext/Blur");
                if (shader == null) return null;
                s_blurMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return s_blurMaterial;
            }
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
