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
        // A batch's textures: up to this many, each primitive sampling one of them by index.
        private const int TexturesPerBatch = 8;

        internal struct Batch
        {
            public UITree Tree;
            public int Start;
            public int Count;
            public int Textures;
        }

        // Every batch's textures, TexturesPerBatch to a batch.
        private static readonly List<Texture> s_batchTextures = new();

        /// <summary>A node drawn as a group: its primitives drawn to an image of its own, which a quad in its place draws.</summary>
        internal struct Group
        {
            public UITree Tree;
            public UINode Node;
            public RenderTexture Target;
            // The part of its root, in points, its image covers.
            public Rect Region;
            public int FirstBatch;
            public int BatchCount;
        }

        // Group images grow and shrink in steps of this many pixels, and are kept while what they cover fits.
        private const int GroupStep = 64;

        // The groups of this build, outermost first (draw them last first), and their batches.
        internal static readonly List<Group> Groups = new();
        internal static readonly List<Batch> GroupBatches = new();
        private static readonly List<RenderTexture> s_groupTargets = new();

        // While a group is walked: the node it is of (drawn whole, not as another quad), and whether a node in the top
        // layer inside it is drawn in place (a group is one image).
        private static UINode s_groupRoot;

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

        // Scratch for drawing order: one node's children sorted by ZIndex, the top layer of the level being walked, and
        // the presented nodes of the tree being walked, in level order.
        private static readonly List<UINode> s_children = new();
        private static readonly List<UINode> s_topLayer = new();
        private static readonly List<UINode> s_levels = new();

        private static Material s_material;
        private static readonly MaterialPropertyBlock s_properties = new();

        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int ClipsId = Shader.PropertyToID("_Clips");
        private static readonly int InstanceOffsetId = Shader.PropertyToID("_InstanceOffset");
        private static readonly int MatrixId = Shader.PropertyToID("_UIMatrix");
        private static readonly int[] TextureIds =
        {
            Shader.PropertyToID("_Texture0"), Shader.PropertyToID("_Texture1"), Shader.PropertyToID("_Texture2"), Shader.PropertyToID("_Texture3"),
            Shader.PropertyToID("_Texture4"), Shader.PropertyToID("_Texture5"), Shader.PropertyToID("_Texture6"), Shader.PropertyToID("_Texture7"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Batches.Clear();
            s_slotBase.Clear();
            s_orderDirty = s_slotsDirty = true;
            s_instanceCount = s_slotCount = s_clipCount = 0;
        }

        public static bool HasBatches => Batches.Count > 0;

        // The GPU's copies and the groups' images outlive a script reload otherwise: let go of them before one, and on quitting.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookRelease()
        {
            Application.quitting -= ReleaseResources;
            Application.quitting += ReleaseResources;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void HookReload()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseResources;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseResources;
        }
#endif

        private static void ReleaseResources()
        {
            s_instanceBuffer?.Release();
            s_slotBuffer?.Release();
            s_clipBuffer?.Release();
            s_instanceBuffer = s_slotBuffer = s_clipBuffer = null;
            foreach (var target in s_groupTargets)
            {
                if (target == null) continue;
                target.Release();
                Object.DestroyImmediate(target);
            }
            s_groupTargets.Clear();
            Groups.Clear();
            Batches.Clear();
            if (s_material != null)
                Object.DestroyImmediate(s_material);
            s_material = null;
            s_orderDirty = s_slotsDirty = true;
        }

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
                s_batchTextures.Clear();
                Groups.Clear();
                GroupBatches.Clear();
                for (int t = 0; t < trees.Count; t++)
                {
                    var tree = trees[t];
                    if (tree.Nodes.Count == 0) continue;
                    int start = s_instanceCount;
                    tree.DrawOrder.Clear();
                    tree.LevelStarts.Clear();
                    tree.LevelInteractive.Clear();
                    CollectLevels(tree);
                    // From the topmost cover that is in place: nothing under it shows.
                    int from = 0;
                    for (int l = s_levels.Count - 1; l >= 1; l--)
                    {
                        if (!Presentation.CoversWhatIsBelow(s_levels[l])) continue;
                        from = l;
                        break;
                    }
                    for (int l = from; l < s_levels.Count; l++)
                    {
                        var level = s_levels[l];
                        tree.LevelStarts.Add(tree.DrawOrder.Count);
                        tree.LevelInteractive.Add(level.PresentationLevel.Peek >= 0);
                        s_topLayer.Clear();
                        Walk(tree, level, s_slotBase[tree], true, level);
                        for (int i = 0; i < s_topLayer.Count; i++)
                            Walk(tree, s_topLayer[i], s_slotBase[tree], true, level);
                    }
                    MakeBatches(Batches, tree, start, s_instanceCount);
                    WalkGroups(tree);
                }
            }

            Upload();
            s_orderDirty = false;
            s_slotsDirty = false;
        }

        // The levels of a tree: its root, then every presented node in it, lowest level first.
        private static void CollectLevels(UITree tree)
        {
            s_levels.Clear();
            s_levels.Add(tree.RootNode);
            for (int i = 1; i < tree.Nodes.Count; i++)
            {
                var node = tree.Nodes[i];
                if (node != null && node.PresentationLevel.Peek != 0)
                    s_levels.Add(node);
            }
            if (s_levels.Count > 2)
                s_levels.Sort(1, s_levels.Count - 1, LevelOrder.Instance);
        }

        private sealed class LevelOrder : IComparer<UINode>
        {
            public static readonly LevelOrder Instance = new();

            public int Compare(UINode a, UINode b) =>
                Mathf.Abs(a.PresentationLevel.Peek).CompareTo(Mathf.Abs(b.PresentationLevel.Peek));
        }

        // Adds a node's primitives, then its children's in ZIndex order (siblings with equal ZIndex in hierarchy order);
        // a node in the top layer is put off until the end of its level, and a presented node to its own level, unless
        // it is the one being drawn there now.
        private static void Walk(UITree tree, UINode node, int slotBase, bool lifted, UINode level)
        {
            if (node.Index < 0 || node.Tree != tree) return;
            if (node != level && node.PresentationLevel.Peek != 0) return;
            // Inside a group it is drawn in place: a group is one image.
            if (!lifted && s_groupRoot == null && node.TopLayer.Peek)
            {
                s_topLayer.Add(node);
                return;
            }
            if (!tree.Shown[node.Index]) return;

            // A group: a quad over the root draws its image here; what is in it is walked into a range of its own
            // later. It still takes its place in drawing order for hit testing.
            if (node != s_groupRoot && tree.GroupOpacities[node.Index] > 0f)
            {
                if (s_groupRoot == null)
                    AddDrawOrder(tree, node);
                EmitGroupQuad(tree, node, slotBase);
                return;
            }

            if (s_groupRoot == null)
                tree.DrawOrder.Add(node.Index);
            uint slot = (uint)(slotBase + node.Index);
            Emit(node.Instances, node.InstanceTextures, slot);

            int first = s_children.Count;
            int end = tree.SubtreeEnd[node.Index];
            for (int i = node.Index + 1; i < end; i = tree.SubtreeEnd[i])
                s_children.Add(tree.Nodes[i]);
            int count = s_children.Count - first;
            if (count > 1)
                SortByZIndex(first, count);
            for (int i = 0; i < count; i++)
                Walk(tree, s_children[first + i], slotBase, false, level);
            s_children.RemoveRange(first, count);
            Emit(node.OverlayInstances, node.OverlayTextures, slot);
        }

        // A group's quad: covering its root (its slot is the root's), sampling the group's image at the group's opacity.
        private static void EmitGroupQuad(UITree tree, UINode node, int slotBase)
        {
            var root = tree.Root;
            float scale = root.PixelsPerPoint;
            // What the group covers, in its root's pixels: everything drawn in it, and the pixels its edges are
            // smoothed over, within the root (nothing outside it is seen). Its image is that size, in steps, kept
            // while it fits.
            var bounds = GroupBounds(tree, node);
            const float smoothing = 2f;
            int x = Mathf.FloorToInt(Mathf.Max(0f, bounds.xMin * scale - smoothing));
            int y = Mathf.FloorToInt(Mathf.Max(0f, bounds.yMin * scale - smoothing));
            int xMax = Mathf.CeilToInt(Mathf.Min(root.Size.x * scale, bounds.xMax * scale + smoothing));
            int yMax = Mathf.CeilToInt(Mathf.Min(root.Size.y * scale, bounds.yMax * scale + smoothing));
            var target = GroupTarget(Groups.Count, Mathf.Max(1, xMax - x), Mathf.Max(1, yMax - y));
            var region = new Rect(x / scale, y / scale, target.width / scale, target.height / scale);
            Groups.Add(new Group { Tree = tree, Node = node, Target = target, Region = region });
            EnsureInstances(s_instanceCount + 1);
            s_instances[s_instanceCount] = new PrimitiveInstance
            {
                Rect = new Vector4(region.x, region.y, region.width, region.height),
                Color = new Color(1f, 1f, 1f, tree.GroupOpacities[node.Index]),
                Uv = new Vector4(0f, 0f, 1f, 1f),
                Slot = (uint)slotBase,
                Type = (uint)PrimitiveType.Group,
            };
            s_textures[s_instanceCount] = target;
            s_instanceCount++;
        }

        // The bounds of what a group draws, in its root's points: every primitive the walk puts in it (its nodes but
        // hidden and presented ones, with what is inside them), placed as the shader places it.
        private static Rect GroupBounds(UITree tree, UINode group)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            int end = tree.SubtreeEnd[group.Index];
            for (int i = group.Index; i < end;)
            {
                var node = tree.Nodes[i];
                if (!tree.Shown[i] || (node != group && node.PresentationLevel.Peek != 0))
                {
                    i = tree.SubtreeEnd[i];
                    continue;
                }
                var size = (Vector2)tree.Slots[i].SizeOpacity;
                Include(node.Instances, tree.World[i], size, ref min, ref max);
                Include(node.OverlayInstances, tree.World[i], size, ref min, ref max);
                i++;
            }
            return min.x > max.x ? Rect.zero : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Grows the bounds by primitives of a node <paramref name="size"/> big at <paramref name="world"/>: each one's
        // rect, a shadow's blur past it and an italic glyph's slant, as the shader draws them.
        private static void Include(List<PrimitiveInstance> instances, Affine2 world, Vector2 size, ref Vector2 min, ref Vector2 max)
        {
            for (int k = 0; k < instances.Count; k++)
            {
                var instance = instances[k];
                var rect = instance.Rect;
                if ((instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0)
                    rect = new Vector4(rect.x, rect.y, size.x - rect.x - rect.z, size.y - rect.y - rect.w);
                float blur = instance.Type == (uint)PrimitiveType.Shadow ? instance.Params.y * 2.5f : 0f;
                float left = rect.x - blur, top = rect.y - blur;
                float right = rect.x + Mathf.Max(rect.z, 0f) + blur, bottom = rect.y + Mathf.Max(rect.w, 0f) + blur;
                float slant = instance.Type >= (uint)PrimitiveType.GlyphSdf ? instance.Params.z : 0f;
                Include(world.Apply(new Vector2(left + slant, top)), ref min, ref max);
                Include(world.Apply(new Vector2(right + slant, top)), ref min, ref max);
                Include(world.Apply(new Vector2(right, bottom)), ref min, ref max);
                Include(world.Apply(new Vector2(left, bottom)), ref min, ref max);
            }
        }

        private static void Include(Vector2 point, ref Vector2 min, ref Vector2 max)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        // The nodes of a group in drawing order, for hit testing, without drawing them.
        private static void AddDrawOrder(UITree tree, UINode node)
        {
            int end = tree.SubtreeEnd[node.Index];
            for (int i = node.Index; i < end; i++)
                if (tree.Shown[i] && tree.Nodes[i].PresentationLevel.Peek == 0)
                    tree.DrawOrder.Add(i);
        }

        // Walks each group found in the tree into a range of its own and batches it; groups inside groups are found as
        // their group is walked, and walked after it.
        private static void WalkGroups(UITree tree)
        {
            for (int g = 0; g < Groups.Count; g++)
            {
                var group = Groups[g];
                if (group.Tree != tree) continue;
                int start = s_instanceCount;
                s_groupRoot = group.Node;
                try
                {
                    Walk(tree, group.Node, s_slotBase[tree], true, group.Node);
                }
                finally
                {
                    s_groupRoot = null;
                }
                group.FirstBatch = GroupBatches.Count;
                MakeBatches(GroupBatches, tree, start, s_instanceCount);
                group.BatchCount = GroupBatches.Count - group.FirstBatch;
                Groups[g] = group;
            }
        }

        // The image the index-th group of a build is drawn to: at least width by height pixels, in steps, kept while
        // it is big enough and not more than a step too big each way.
        private static RenderTexture GroupTarget(int index, int width, int height)
        {
            width = (width + GroupStep - 1) / GroupStep * GroupStep;
            height = (height + GroupStep - 1) / GroupStep * GroupStep;
            while (s_groupTargets.Count <= index)
                s_groupTargets.Add(null);
            var target = s_groupTargets[index];
            if (target != null && target.width >= width && target.height >= height
                && target.width <= width + GroupStep && target.height <= height + GroupStep)
                return target;
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "UINext Group " + index,
                hideFlags = HideFlags.HideAndDontSave,
            };
            target.Create();
            s_groupTargets[index] = target;
            return target;
        }

        private static void Emit(List<PrimitiveInstance> instances, List<Texture> textures, uint slot)
        {
            EnsureInstances(s_instanceCount + instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                instance.Slot = slot;
                s_instances[s_instanceCount] = instance;
                s_textures[s_instanceCount] = textures[i];
                s_instanceCount++;
            }
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

        // Runs of primitives drawn in one call, each with up to eight textures that its primitives say which of they
        // sample: a run ends only when a ninth texture turns up. A primitive that samples nothing (a shape, a shadow)
        // goes with any run.
        private static void MakeBatches(List<Batch> batches, UITree tree, int start, int end)
        {
            int batchStart = start;
            int first = s_batchTextures.Count;
            int used = 0;
            for (int i = start; i < end; i++)
            {
                var texture = s_textures[i];
                if (texture == null)
                {
                    s_instances[i].Texture = 0;
                    continue;
                }
                int slot = s_batchTextures.IndexOf(texture, first, used);
                if (slot < 0)
                {
                    if (used == TexturesPerBatch)
                    {
                        CloseBatch(batches, tree, batchStart, i, first);
                        batchStart = i;
                        first = s_batchTextures.Count;
                        used = 0;
                    }
                    s_batchTextures.Add(texture);
                    slot = first + used++;
                }
                s_instances[i].Texture = (uint)(slot - first);
            }
            if (end > batchStart)
                CloseBatch(batches, tree, batchStart, end, first);
        }

        private static void CloseBatch(List<Batch> batches, UITree tree, int start, int end, int firstTexture)
        {
            // Unused slots hold the white texture, so every slot is bound.
            while (s_batchTextures.Count < firstTexture + TexturesPerBatch)
                s_batchTextures.Add(null);
            batches.Add(new Batch { Tree = tree, Start = start, Count = end - start, Textures = firstTexture });
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
        public static void Render(RasterCommandBuffer cmd, Camera camera, bool sceneView, bool flipped) =>
            Draw(cmd, Batches, 0, Batches.Count, camera, sceneView, flipped);

        /// <summary>Draws the index-th group's image: what is in it, the part of its root it covers, over a clear image.</summary>
        public static void RenderGroup(RasterCommandBuffer cmd, int index, bool flipped)
        {
            var group = Groups[index];
            cmd.ClearRenderTarget(false, true, Color.clear);
            var region = group.Region;
            var matrix = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(region.xMin, region.xMax, region.yMax, region.yMin, -1f, 1f), flipped);
            Draw(cmd, GroupBatches, group.FirstBatch, group.BatchCount, null, false, flipped, matrix);
        }

        private static void Draw(RasterCommandBuffer cmd, List<Batch> batches, int first, int count, Camera camera, bool sceneView, bool flipped,
            Matrix4x4? region = null)
        {
            var material = Material;
            if (material == null || count == 0 || s_instanceBuffer == null) return;
            UITree currentTree = null;
            bool skip = false;
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int b = first; b < first + count; b++)
            {
                var batch = batches[b];
                if (batch.Tree != currentTree)
                {
                    currentTree = batch.Tree;
                    // A root gone since the build: none of its batches are drawn.
                    skip = currentTree.Root == null;
                    if (!skip)
                        matrix = region ?? MatrixFor(currentTree.Root, camera, sceneView, flipped);
                }
                if (skip) continue;
                var properties = s_properties;
                properties.Clear();
                properties.SetBuffer(InstancesId, s_instanceBuffer);
                properties.SetBuffer(SlotsId, s_slotBuffer);
                properties.SetBuffer(ClipsId, s_clipBuffer);
                properties.SetInt(InstanceOffsetId, batch.Start);
                properties.SetMatrix(MatrixId, matrix);
                for (int t = 0; t < TexturesPerBatch; t++)
                {
                    var texture = s_batchTextures[batch.Textures + t];
                    properties.SetTexture(TextureIds[t], texture != null ? texture : Texture2D.whiteTexture);
                }
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
