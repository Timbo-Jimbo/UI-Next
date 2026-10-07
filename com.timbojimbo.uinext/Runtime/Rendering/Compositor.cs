using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Turns every tree's nodes into what the GPU draws: one buffer of primitives in draw order, one of node slots
    /// (where each node is drawn), one of clip shapes, and the batches to draw them in (split where the texture changes or
    /// a new root starts). Rebuilt only when something changed: a node drew something new, or something moved.
    /// </summary>
    internal static class Compositor
    {
        internal struct Batch
        {
            public UITree Tree;
            public int Start;
            public int Count;
            public Texture Texture;
        }

        private static PrimitiveInstance[] s_instances = new PrimitiveInstance[256];
        private static Texture[] s_textures = new Texture[256];
        private static int s_instanceCount;
        private static NodeSlot[] s_slots = new NodeSlot[64];
        private static int s_slotCount;
        private static ClipShape[] s_clips = new ClipShape[16];
        private static int s_clipCount;
        internal static readonly List<Batch> Batches = new();

        // Per tree, where its slots and clips start in the shared buffers.
        private static readonly Dictionary<UITree, int> s_slotBase = new();

        private static GraphicsBuffer s_instanceBuffer;
        private static GraphicsBuffer s_slotBuffer;
        private static GraphicsBuffer s_clipBuffer;

        private static bool s_orderDirty = true;
        private static bool s_slotsDirty = true;

        // Scratch for drawing order: one node's children sorted by ZIndex, and the top layer of the tree being walked.
        private static readonly List<UINode> s_children = new();
        private static readonly List<UINode> s_topLayer = new();

        private static Material s_material;
        private static readonly MaterialPropertyBlock s_properties = new();

        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int ClipsId = Shader.PropertyToID("_Clips");
        private static readonly int InstanceOffsetId = Shader.PropertyToID("_InstanceOffset");
        private static readonly int MatrixId = Shader.PropertyToID("_UIMatrix");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int TexSizeId = Shader.PropertyToID("_TexSize");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Batches.Clear();
            s_slotBase.Clear();
            s_orderDirty = s_slotsDirty = true;
            s_instanceCount = s_slotCount = s_clipCount = 0;
        }

        public static bool HasBatches => Batches.Count > 0;

        public static void MarkInstancesDirty(UITree tree) => s_orderDirty = true;

        public static void MarkOrderDirty() => s_orderDirty = true;

        public static void MarkSlotsDirty() => s_slotsDirty = true;

        /// <summary>Brings the buffers and batches up to date with the trees.</summary>
        public static void Build()
        {
            if (!s_orderDirty && !s_slotsDirty) return;
            var trees = UITree.Trees;

            // Slots and clips: every tree's, one after another.
            s_slotCount = 0;
            s_clipCount = 0;
            s_slotBase.Clear();
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                int nodes = tree.Nodes.Count;
                s_slotBase[tree] = s_slotCount;
                EnsureSlots(s_slotCount + nodes);
                EnsureClips(s_clipCount + tree.ClipShapes.Count);
                int clipBase = s_clipCount;
                for (int i = 0; i < nodes; i++)
                {
                    var slot = tree.Slots[i];
                    slot.Clips = OffsetChain(slot.Clips, clipBase);
                    s_slots[s_slotCount + i] = slot;
                }
                for (int c = 0; c < tree.ClipShapes.Count; c++)
                    s_clips[s_clipCount + c] = tree.ClipShapes[c];
                s_slotCount += nodes;
                s_clipCount += tree.ClipShapes.Count;
            }

            if (s_orderDirty)
            {
                s_instanceCount = 0;
                Batches.Clear();
                for (int t = 0; t < trees.Count; t++)
                {
                    var tree = trees[t];
                    if (tree.Nodes.Count == 0) continue;
                    int start = s_instanceCount;
                    s_topLayer.Clear();
                    Walk(tree, tree.RootNode, s_slotBase[tree], false);
                    for (int i = 0; i < s_topLayer.Count; i++)
                        Walk(tree, s_topLayer[i], s_slotBase[tree], true);
                    MakeBatches(tree, start, s_instanceCount);
                }
            }

            Upload();
            s_orderDirty = false;
            s_slotsDirty = false;
        }

        // Adds a node's primitives, then its children's in ZIndex order (siblings with equal ZIndex in hierarchy order);
        // a node in the top layer is put off until the end of its tree, unless it is the one being drawn there now.
        private static void Walk(UITree tree, UINode node, int slotBase, bool lifted)
        {
            if (node.Index < 0 || node.Tree != tree) return;
            if (!lifted && node != tree.RootNode && node.TopLayer.Peek)
            {
                s_topLayer.Add(node);
                return;
            }
            if (!tree.Shown[node.Index]) return;

            var instances = node.Instances;
            var textures = node.InstanceTextures;
            EnsureInstances(s_instanceCount + instances.Count);
            uint slot = (uint)(slotBase + node.Index);
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                instance.Slot = slot;
                s_instances[s_instanceCount] = instance;
                s_textures[s_instanceCount] = textures[i];
                s_instanceCount++;
            }

            int first = s_children.Count;
            int end = tree.SubtreeEnd[node.Index];
            for (int i = node.Index + 1; i < end; i = tree.SubtreeEnd[i])
                s_children.Add(tree.Nodes[i]);
            int count = s_children.Count - first;
            if (count > 1)
                SortByZIndex(first, count);
            for (int i = 0; i < count; i++)
                Walk(tree, s_children[first + i], slotBase, false);
            s_children.RemoveRange(first, count);
        }

        // A stable insertion sort of one node's children by ZIndex.
        private static void SortByZIndex(int first, int count)
        {
            for (int i = first + 1; i < first + count; i++)
            {
                var node = s_children[i];
                int z = node.ZIndex.Peek;
                int j = i - 1;
                while (j >= first && s_children[j].ZIndex.Peek > z)
                {
                    s_children[j + 1] = s_children[j];
                    j--;
                }
                s_children[j + 1] = node;
            }
        }

        private static void MakeBatches(UITree tree, int start, int end)
        {
            int batchStart = start;
            for (int i = start + 1; i <= end; i++)
            {
                if (i < end && s_textures[i] == s_textures[batchStart]) continue;
                if (i > batchStart)
                    Batches.Add(new Batch { Tree = tree, Start = batchStart, Count = i - batchStart, Texture = s_textures[batchStart] });
                batchStart = i;
            }
        }

        private static Vector4 OffsetChain(Vector4 chain, int clipBase)
        {
            for (int c = 0; c < 4; c++)
                if (chain[c] >= 0f)
                    chain[c] += clipBase;
            return chain;
        }

        private static void Upload()
        {
            if (s_instanceCount == 0 && s_slotCount == 0) return;
            Ensure(ref s_instanceBuffer, Mathf.Max(1, s_instanceCount), PrimitiveInstance.Stride);
            Ensure(ref s_slotBuffer, Mathf.Max(1, s_slotCount), NodeSlot.Stride);
            Ensure(ref s_clipBuffer, Mathf.Max(1, s_clipCount), ClipShape.Stride);
            if (s_instanceCount > 0) s_instanceBuffer.SetData(s_instances, 0, 0, s_instanceCount);
            if (s_slotCount > 0) s_slotBuffer.SetData(s_slots, 0, 0, s_slotCount);
            if (s_clipCount > 0) s_clipBuffer.SetData(s_clips, 0, 0, s_clipCount);
        }

        private static void Ensure(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.IsValid() && buffer.count >= count) return;
            buffer?.Release();
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(count), stride);
        }

        private static void EnsureInstances(int count)
        {
            if (s_instances.Length >= count) return;
            int size = Mathf.NextPowerOfTwo(count);
            System.Array.Resize(ref s_instances, size);
            System.Array.Resize(ref s_textures, size);
        }

        private static void EnsureSlots(int count)
        {
            if (s_slots.Length < count) System.Array.Resize(ref s_slots, Mathf.NextPowerOfTwo(count));
        }

        private static void EnsureClips(int count)
        {
            if (s_clips.Length < count) System.Array.Resize(ref s_clips, Mathf.NextPowerOfTwo(Mathf.Max(count, 16)));
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        private static Material Material
        {
            get
            {
                if (s_material != null) return s_material;
                var shader = Resources.Load<Shader>("UINext/Primitive");
                if (shader == null) return null;
                s_material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return s_material;
            }
        }

        /// <summary>
        /// Draws every batch with <paramref name="cmd"/>. In the game view a root covers the screen; in the scene view it
        /// is drawn where its RectTransform is, seen by <paramref name="camera"/>.
        /// </summary>
        public static void Render(RasterCommandBuffer cmd, Camera camera, bool sceneView, bool flipped)
        {
            var material = Material;
            if (material == null || Batches.Count == 0 || s_instanceBuffer == null) return;
            UITree currentTree = null;
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int b = 0; b < Batches.Count; b++)
            {
                var batch = Batches[b];
                if (batch.Tree != currentTree)
                {
                    currentTree = batch.Tree;
                    if (currentTree.Root == null) continue;
                    matrix = MatrixFor(currentTree.Root, camera, sceneView, flipped);
                }
                var properties = s_properties;
                properties.Clear();
                properties.SetBuffer(InstancesId, s_instanceBuffer);
                properties.SetBuffer(SlotsId, s_slotBuffer);
                properties.SetBuffer(ClipsId, s_clipBuffer);
                properties.SetInt(InstanceOffsetId, batch.Start);
                properties.SetMatrix(MatrixId, matrix);
                var texture = batch.Texture != null ? batch.Texture : Texture2D.whiteTexture;
                properties.SetTexture(MainTexId, texture);
                properties.SetVector(TexSizeId, new Vector4(texture.width, texture.height, 1f / Mathf.Max(1, texture.width), 1f / Mathf.Max(1, texture.height)));
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 6, batch.Count, properties);
            }
        }

        private static Matrix4x4 MatrixFor(UIRoot root, Camera camera, bool sceneView, bool flipped)
        {
            if (sceneView)
            {
                var rootToLocal = Matrix4x4.Scale(new Vector3(1f, -1f, 1f));
                var world = root.transform.localToWorldMatrix * rootToLocal;
                return GL.GetGPUProjectionMatrix(camera.projectionMatrix, flipped) * camera.worldToCameraMatrix * world;
            }
            var size = root.Size;
            var ortho = Matrix4x4.Ortho(0f, size.x, size.y, 0f, -1f, 1f);
            return GL.GetGPUProjectionMatrix(ortho, flipped);
        }
    }
}
