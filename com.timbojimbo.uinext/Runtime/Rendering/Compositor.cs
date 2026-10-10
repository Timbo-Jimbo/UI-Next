using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Turns every tree's nodes into what the GPU draws: one buffer of primitives in draw order, one of node slots
    /// (where each node is drawn), one of clip shapes, and the batches to draw them in (split where the texture changes or
    /// a new root starts). Rebuilt only when something changed: a node drew something new, or something moved. A matched
    /// flight is drawn above everything at its level as a pair: its picture and its destination each drawn to an image of
    /// their own, and one quad summing them, cut to the flight's rounded shape.
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
            // How far its image is blurred, in its pixels (0: not at all).
            public float Blur;
            // What cuts its image (null: nothing), and the masked node's size and its transform from the root's points
            // to its own space, as it is drawn.
            public Texture Mask;
            public Vector2 MaskSize;
            public Affine2 MaskInverse;
            // Or one end of a matched flight, in place of a node: its picture (0) or its destination (1).
            public Flight Flight;
            public int End;
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
        // A second texture a primitive samples (a matched pair's destination image; null for the rest).
        private static Texture[] s_textures2 = new Texture[256];
        private static int s_instanceCount;

        // Scratch for a flight's picture: whether each of its nodes is left out (a named part flying on its own).
        private static readonly List<bool> s_skip = new();
        private static int s_build;
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

        // What the last build made, for the editor's readout: primitives (groups' included), draw calls in the main pass
        // and in groups' images, and groups.
        internal static int PrimitiveCount => s_instanceCount;
        internal static int DrawCalls => Batches.Count + GroupBatches.Count;

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
            if (s_maskMaterial != null)
                Object.DestroyImmediate(s_maskMaterial);
            s_maskMaterial = null;
            UIRenderPass.ReleaseResources();
            s_orderDirty = s_slotsDirty = true;
        }

        public static void MarkInstancesDirty(UITree tree) => s_orderDirty = true;

        public static void MarkOrderDirty() => s_orderDirty = true;

        public static void MarkSlotsDirty() => s_slotsDirty = true;

        /// <summary>Brings the buffers and batches up to date with the trees.</summary>
        public static void Build()
        {
            // A flight moves every frame it flies.
            if (MatchedGeometry.Flights.Count > 0)
                s_orderDirty = s_slotsDirty = true;
            if (!s_orderDirty && !s_slotsDirty) return;
            if (MatchedGeometry.Flights.Count > 0)
                MatchedGeometry.Update();
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
            AddFlightSlots();

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
                        if (!Presentation.CoversWhatIsBelow(tree, s_levels[l])) continue;
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
                        EmitFlights(tree, l);
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
            // Hidden, or drawn at no opacity: nothing in it is seen.
            if (!tree.Shown[node.Index] || tree.Opacities[node.Index] <= 0f) return;

            // Flying to its place: drawn in its flight, above everything at its level, while it is hit where it is laid
            // out. Taken over by a node matched to it: its picture flies instead.
            var flying = node.FlightIn;
            if (flying != null && flying.InBuild)
            {
                if (s_groupRoot == null)
                    AddDrawOrder(tree, node);
                return;
            }
            if (node.FlightOut != null) return;

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
            // A blur spreads what is drawn past it: room for its reach.
            float blur = tree.Blurs[node.Index];
            if (blur > 0f && bounds.width > 0f)
            {
                float reach = blur * 2f;
                bounds = Rect.MinMaxRect(bounds.xMin - reach, bounds.yMin - reach, bounds.xMax + reach, bounds.yMax + reach);
            }
            const float smoothing = 2f;
            int x = Mathf.FloorToInt(Mathf.Max(0f, bounds.xMin * scale - smoothing));
            int y = Mathf.FloorToInt(Mathf.Max(0f, bounds.yMin * scale - smoothing));
            int xMax = Mathf.CeilToInt(Mathf.Min(root.Size.x * scale, bounds.xMax * scale + smoothing));
            int yMax = Mathf.CeilToInt(Mathf.Min(root.Size.y * scale, bounds.yMax * scale + smoothing));
            var target = GroupTarget(Groups.Count, Mathf.Max(1, xMax - x), Mathf.Max(1, yMax - y));
            var region = new Rect(x / scale, y / scale, target.width / scale, target.height / scale);
            Groups.Add(new Group
            {
                Tree = tree,
                Node = node,
                Target = target,
                Region = region,
                Blur = blur * scale,
                Mask = node.Mask.Peek,
                MaskSize = tree.Slots[node.Index].SizeOpacity,
                MaskInverse = tree.World[node.Index].Inverse,
            });
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
            s_textures2[s_instanceCount] = null;
            s_instanceCount++;
        }

        // The bounds of what a group draws, in its root's points: every primitive the walk puts in it (its nodes but
        // hidden, unseen and presented ones, with what is inside them), placed as the shader places it.
        private static Rect GroupBounds(UITree tree, UINode group)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            int end = tree.SubtreeEnd[group.Index];
            for (int i = group.Index; i < end;)
            {
                var node = tree.Nodes[i];
                if (!tree.Shown[i] || tree.Opacities[i] <= 0f || (node != group && node.PresentationLevel.Peek != 0))
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
                if (group.Flight != null)
                {
                    EmitFlightEnd(tree, group.Flight, group.End);
                }
                else
                {
                    s_groupRoot = group.Node;
                    try
                    {
                        Walk(tree, group.Node, s_slotBase[tree], true, group.Node);
                    }
                    finally
                    {
                        s_groupRoot = null;
                    }
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

        private static void Emit(List<PrimitiveInstance> instances, List<Texture> textures, uint slot) => Emit(instances, textures, slot, slot);

        // A node's primitives in its slot, those it draws relative to its rect in `relative` (a flight end's own
        // background, laid out at the flight's size).
        private static void Emit(List<PrimitiveInstance> instances, List<Texture> textures, uint slot, uint relative)
        {
            EnsureInstances(s_instanceCount + instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                instance.Slot = (instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0 ? relative : slot;
                s_instances[s_instanceCount] = instance;
                s_textures[s_instanceCount] = textures[i];
                s_textures2[s_instanceCount] = null;
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
        // goes with any run; one that samples two (a matched pair) has both in its run, the second's index in the
        // second four bits of its texture index.
        private static void MakeBatches(List<Batch> batches, UITree tree, int start, int end)
        {
            int batchStart = start;
            int first = s_batchTextures.Count;
            int used = 0;
            for (int i = start; i < end; i++)
            {
                var texture = s_textures[i];
                var second = s_textures2[i];
                if (texture == null && second == null)
                {
                    s_instances[i].Texture = 0;
                    continue;
                }
                // Room for what it samples that the run does not have yet.
                int needed = (texture != null && s_batchTextures.IndexOf(texture, first, used) < 0 ? 1 : 0)
                             + (second != null && second != texture && s_batchTextures.IndexOf(second, first, used) < 0 ? 1 : 0);
                if (used + needed > TexturesPerBatch)
                {
                    CloseBatch(batches, tree, batchStart, i, first);
                    batchStart = i;
                    first = s_batchTextures.Count;
                    used = 0;
                }
                int index = BatchTexture(texture, first, ref used);
                if (second != null)
                    index |= BatchTexture(second, first, ref used) << 4;
                s_instances[i].Texture = (uint)index;
            }
            if (end > batchStart)
                CloseBatch(batches, tree, batchStart, end, first);
        }

        // Where a texture is in the run starting at `first`, added if it is not there yet (0 for none).
        private static int BatchTexture(Texture texture, int first, ref int used)
        {
            if (texture == null) return 0;
            int slot = s_batchTextures.IndexOf(texture, first, used);
            if (slot < 0)
            {
                s_batchTextures.Add(texture);
                slot = first + used++;
            }
            return slot - first;
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
            System.Array.Resize(ref s_textures2, size);
        }

        private static void EnsureSlots(int count)
        {
            if (s_slots.Length < count) System.Array.Resize(ref s_slots, Mathf.NextPowerOfTwo(count));
        }

        private static void EnsureClips(int count)
        {
            if (s_clips.Length < count) System.Array.Resize(ref s_clips, Mathf.NextPowerOfTwo(Mathf.Max(count, 16)));
        }

        // ── Flights ──────────────────────────────────────────────────────────────

        // Slots and clips for every flight placed, after the trees': its pair's (in its root's space, inside the shape of
        // the flight it is a part of), its destination's nodes fitted into it, its picture's nodes, and each end's own
        // background laid out at its size. Outer flights first, as a part's pair is cut to its outer flight's shape.
        private static void AddFlightSlots()
        {
            s_build++;
            foreach (var flight in MatchedGeometry.Flights)
                AddFlightSlots(flight);
        }

        private static void AddFlightSlots(Flight flight)
        {
            if (flight.SlotsBuild == s_build) return;
            flight.SlotsBuild = s_build;
            var tree = flight.Tree;
            var destination = flight.Destination;
            flight.InBuild = flight.Placed && tree != null && tree.Root != null && s_slotBase.ContainsKey(tree)
                             && destination != null && destination.Tree == tree && destination.Index >= 0;
            if (!flight.InBuild) return;
            var outer = flight.Outer;
            if (outer != null)
                AddFlightSlots(outer);

            var rect = flight.Rect;
            float pixels = tree.Root.PixelsPerPoint;
            // Its shape, which cuts the pairs of the named parts inside it.
            flight.ShapeClip = AddClip(Affine2.Translate(-rect.position), rect.size, flight.Radii);
            flight.PairClips = outer != null && outer.InBuild ? Append(outer.PairClips, outer.ShapeClip) : new Vector4(-1f, -1f, -1f, -1f);
            flight.PairSlot = AddSlot(new NodeSlot
            {
                Row0 = new Vector4(1f, 0f, 0f, 1f),
                Row1 = new Vector4(0f, 1f, 0f, 1f),
                SizeOpacity = new Vector4(rect.width, rect.height, 1f, 1f / Mathf.Max(1e-4f, pixels)),
                Clips = flight.PairClips,
            });

            // The destination, live: what is in it fitted to the flight's width from its top, at its own opacity (its
            // transition's left out: the flight fades it in), cut only by clips inside it.
            int d = destination.Index, end = tree.SubtreeEnd[d];
            flight.LiveSlots = s_slotCount;
            EnsureSlots(s_slotCount + end - d);
            var fromRoot = flight.FitMap.Inverse;
            var outside = tree.Slots[d].Clips;
            for (int i = d; i < end; i++)
            {
                var slot = tree.Slots[i];
                var world = flight.FitMap * tree.World[i];
                var size = (Vector2)slot.SizeOpacity;
                float opacity;
                if (i == d)
                {
                    opacity = Mathf.Clamp01(destination.Opacity.Drawn);
                }
                else
                {
                    var parent = tree.Nodes[i].ParentNode;
                    int p = parent != null ? parent.Index : -1;
                    opacity = p >= d && p < end ? s_slots[flight.LiveSlots + p - d].SizeOpacity.z * tree.LocalOpacities[i] : 0f;
                }
                var clips = FlightClips(tree.ClipShapes, slot.Clips, outside, fromRoot);
                s_slots[flight.LiveSlots + i - d] = new NodeSlot
                {
                    Row0 = new Vector4(world.A, world.B, world.Tx, slot.Row0.w),
                    Row1 = new Vector4(world.C, world.D, world.Ty, slot.Row1.w),
                    SizeOpacity = new Vector4(size.x, size.y, opacity, 1f / Mathf.Max(1e-4f, pixels * world.ScaleFactor)),
                    Clips = clips,
                };
            }
            s_slotCount += end - d;
            // Its own background (what it draws relative to its rect: its box, border and shadow, an image filling it),
            // laid out at the flight's size, unscaled, as it would draw at that size: its corners keep their points, and
            // the flight's shape, going from one end's corners to the other's, cuts it.
            flight.LiveBackground = AddBackground(s_slots[flight.LiveSlots], rect, pixels);

            // The picture, as the source was drawn, fitted the same way.
            var picture = flight.Picture;
            flight.PictureSlots = s_slotCount;
            EnsureSlots(s_slotCount + picture.Slots.Count);
            var fromPicture = flight.PictureMap.Inverse;
            for (int k = 0; k < picture.Slots.Count; k++)
            {
                var drawn = picture.Slots[k];
                var world = flight.PictureMap * drawn.Transform;
                var size = drawn.Size;
                var clips = new Vector4(-1f, -1f, -1f, -1f);
                for (int c = 0; c < 4; c++)
                {
                    int index = (int)drawn.Clips[c];
                    if (index < 0 || index >= picture.Clips.Count) break;
                    var clip = picture.Clips[index];
                    clips[c] = AddClip(Picture.InverseOf(clip) * fromPicture, clip.Size, clip.Radii);
                }
                s_slots[flight.PictureSlots + k] = new NodeSlot
                {
                    Row0 = new Vector4(world.A, world.B, world.Tx, drawn.Saturation),
                    Row1 = new Vector4(world.C, world.D, world.Ty, drawn.Brightness),
                    SizeOpacity = new Vector4(size.x, size.y, drawn.Opacity, 1f / Mathf.Max(1e-4f, pixels * world.ScaleFactor)),
                    Clips = clips,
                };
            }
            s_slotCount += picture.Slots.Count;
            flight.PictureBackground = picture.Slots.Count > 0 ? AddBackground(s_slots[flight.PictureSlots], rect, pixels) : -1;
        }

        // The slot an end's own background is drawn in: the flight's rect, unscaled, as opaque, filtered and cut as the
        // end's root.
        private static int AddBackground(NodeSlot root, Rect rect, float pixels) => AddSlot(new NodeSlot
        {
            Row0 = new Vector4(1f, 0f, rect.x, root.Row0.w),
            Row1 = new Vector4(0f, 1f, rect.y, root.Row1.w),
            SizeOpacity = new Vector4(rect.width, rect.height, root.SizeOpacity.z, 1f / Mathf.Max(1e-4f, pixels)),
            Clips = root.Clips,
        });

        // The clips of a node in a flight: those made inside the flying node (not those around it, which a flight is out
        // of), through the flight's fit.
        private static Vector4 FlightClips(List<ClipShape> shapes, Vector4 chain, Vector4 outside, Affine2 fromFlight)
        {
            var clips = new Vector4(-1f, -1f, -1f, -1f);
            int n = 0;
            for (int c = 0; c < 4; c++)
            {
                int index = (int)chain[c];
                if (index < 0) break;
                if (index >= shapes.Count || index == (int)outside.x || index == (int)outside.y || index == (int)outside.z
                    || index == (int)outside.w) continue;
                var shape = shapes[index];
                clips[n++] = AddClip(Picture.InverseOf(shape) * fromFlight, shape.Size, shape.Radii);
            }
            return clips;
        }

        private static int AddSlot(in NodeSlot slot)
        {
            EnsureSlots(s_slotCount + 1);
            s_slots[s_slotCount] = slot;
            return s_slotCount++;
        }

        // A clip from its transform from its root's space into its own, its size and its corners.
        private static int AddClip(Affine2 inverse, Vector2 size, Vector4 radii)
        {
            EnsureClips(s_clipCount + 1);
            s_clips[s_clipCount] = new ClipShape
            {
                Inverse0 = new Vector4(inverse.A, inverse.B, inverse.Tx, 0f),
                Inverse1 = new Vector4(inverse.C, inverse.D, inverse.Ty, 0f),
                Size = new Vector4(size.x, size.y, 0f, 0f),
                Radii = radii,
            };
            return s_clipCount++;
        }

        // A chain with one more clip, keeping the innermost four.
        private static Vector4 Append(Vector4 chain, int clip)
        {
            for (int c = 0; c < 4; c++)
            {
                if (chain[c] >= 0f) continue;
                chain[c] = clip;
                return chain;
            }
            return new Vector4(chain.y, chain.z, chain.w, clip);
        }

        // The flights drawn at the l-th level of a tree, above everything in it: each at the higher of the levels its two
        // ends were drawn at (one flying home from a presentation going away is drawn over that presentation), with the
        // named parts inside it after it.
        private static void EmitFlights(UITree tree, int level)
        {
            foreach (var flight in MatchedGeometry.Flights)
            {
                if (flight.Outer != null && flight.Outer.InBuild) continue;
                if (!flight.InBuild || flight.Tree != tree) continue;
                int at = s_levels.IndexOf(MatchedGeometry.LevelOf(flight.Destination));
                if (flight.SourceLevel != null)
                    at = Mathf.Max(at, s_levels.IndexOf(flight.SourceLevel));
                if (at == level)
                    EmitFlight(tree, flight);
            }
        }

        // A flight's pair: its picture's and its destination's images, summed in a quad over its rect cut to its shape.
        private static void EmitFlight(UITree tree, Flight flight)
        {
            var root = tree.Root;
            float scale = root.PixelsPerPoint;
            var rect = flight.Rect;
            int x = Mathf.FloorToInt(Mathf.Max(0f, rect.xMin * scale));
            int y = Mathf.FloorToInt(Mathf.Max(0f, rect.yMin * scale));
            int xMax = Mathf.CeilToInt(Mathf.Min(root.Size.x * scale, rect.xMax * scale));
            int yMax = Mathf.CeilToInt(Mathf.Min(root.Size.y * scale, rect.yMax * scale));
            // Off the screen: nothing of it, or of the parts inside it, is seen.
            if (xMax <= x || yMax <= y) return;
            var picture = GroupTarget(Groups.Count, xMax - x, yMax - y);
            var region = new Rect(x / scale, y / scale, picture.width / scale, picture.height / scale);
            Groups.Add(new Group { Tree = tree, Target = picture, Region = region, Flight = flight, End = 0 });
            var live = GroupTarget(Groups.Count, xMax - x, yMax - y);
            Groups.Add(new Group { Tree = tree, Target = live, Region = region, Flight = flight, End = 1 });

            EnsureInstances(s_instanceCount + 1);
            s_instances[s_instanceCount] = new PrimitiveInstance
            {
                Rect = new Vector4(rect.x, rect.y, rect.width, rect.height),
                Radii = flight.Radii,
                Color = new Color(1f, 1f, 1f, flight.Progress),
                // The flight's rect in the images' region; textures run up from their bottom.
                Uv = new Vector4((rect.x - region.x) / region.width, 1f - (rect.yMax - region.y) / region.height,
                    rect.width / region.width, rect.height / region.height),
                Slot = (uint)flight.PairSlot,
                Type = (uint)PrimitiveType.Pair,
            };
            s_textures[s_instanceCount] = picture;
            s_textures2[s_instanceCount] = live;
            s_instanceCount++;

            foreach (var part in MatchedGeometry.Flights)
                if (part.Outer == flight && part.InBuild)
                    EmitFlight(tree, part);
        }

        // One end of a flight, into its image: the picture (0) or the destination (1), leaving out the named parts that
        // fly on their own inside it.
        private static void EmitFlightEnd(UITree tree, Flight flight, int end)
        {
            if (end == 1)
            {
                WalkLive(tree, flight, flight.Destination);
                return;
            }
            var picture = flight.Picture;
            s_skip.Clear();
            for (int k = 0; k < picture.Slots.Count; k++)
            {
                var drawn = picture.Slots[k];
                s_skip.Add((drawn.Parent >= 0 && s_skip[drawn.Parent]) || (k > 0 && IsPart(flight, drawn.Node)));
            }
            EnsureInstances(s_instanceCount + picture.Instances.Count);
            for (int i = 0; i < picture.Instances.Count; i++)
            {
                var instance = picture.Instances[i];
                int k = (int)instance.Slot;
                if (k >= s_skip.Count || s_skip[k]) continue;
                // The source's own background is laid out at the flight's size; the rest is fitted.
                instance.Slot = k == 0 && (instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0
                    ? (uint)flight.PictureBackground
                    : (uint)(flight.PictureSlots + k);
                s_instances[s_instanceCount] = instance;
                s_textures[s_instanceCount] = picture.Textures[i];
                s_textures2[s_instanceCount] = null;
                s_instanceCount++;
            }
        }

        // Whether a node of a flight's picture is the source of a named part flying on its own inside the flight.
        private static bool IsPart(Flight flight, UINode node)
        {
            foreach (var part in MatchedGeometry.Flights)
                if (part.Outer == flight && part.Source == node)
                    return true;
            return false;
        }

        // A flight's destination and what is inside it, as the walk draws them, in the flight's slots; what flies on its
        // own (a named part) and what is presented above it are left out.
        private static void WalkLive(UITree tree, Flight flight, UINode node)
        {
            int i = node.Index;
            if (i < 0 || node.Tree != tree || !tree.Shown[i]) return;
            if (node != flight.Destination && (node.PresentationLevel.Peek != 0 || node.FlightIn != null || node.FlightOut != null)) return;
            int slot = flight.LiveSlots + i - flight.Destination.Index;
            if (s_slots[slot].SizeOpacity.z <= 0f) return;
            // The destination's own background is laid out at the flight's size; the rest is fitted.
            uint relative = node == flight.Destination ? (uint)flight.LiveBackground : (uint)slot;
            Emit(node.Instances, node.InstanceTextures, (uint)slot, relative);

            int first = s_children.Count;
            int end = tree.SubtreeEnd[i];
            for (int c = i + 1; c < end; c = tree.SubtreeEnd[c])
                s_children.Add(tree.Nodes[c]);
            int count = s_children.Count - first;
            if (count > 1)
                SortByZIndex(first, count);
            for (int c = 0; c < count; c++)
                WalkLive(tree, flight, s_children[first + c]);
            s_children.RemoveRange(first, count);
            Emit(node.OverlayInstances, node.OverlayTextures, (uint)slot, relative);
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
            if (group.Mask != null)
                DrawMask(cmd, group, matrix);
        }

        private static readonly MaterialPropertyBlock s_maskProperties = new();
        private static readonly int s_maskTexture = Shader.PropertyToID("_MaskTexture");
        private static readonly int s_maskMatrix = Shader.PropertyToID("_MaskMatrix");
        private static readonly int s_maskRegion = Shader.PropertyToID("_MaskRegion");
        private static readonly int s_maskInverse0 = Shader.PropertyToID("_MaskInverse0");
        private static readonly int s_maskInverse1 = Shader.PropertyToID("_MaskInverse1");
        private static readonly int s_maskSize = Shader.PropertyToID("_MaskSize");
        private static Material s_maskMaterial;

        // Multiplies a group's image by its mask's alpha, over the whole image.
        private static void DrawMask(RasterCommandBuffer cmd, in Group group, Matrix4x4 matrix)
        {
            if (s_maskMaterial == null)
            {
                var shader = Resources.Load<Shader>("UINext/Mask");
                if (shader == null) return;
                s_maskMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            var inverse = group.MaskInverse;
            var region = group.Region;
            s_maskProperties.Clear();
            s_maskProperties.SetTexture(s_maskTexture, group.Mask);
            s_maskProperties.SetMatrix(s_maskMatrix, matrix);
            s_maskProperties.SetVector(s_maskRegion, new Vector4(region.x, region.y, region.width, region.height));
            s_maskProperties.SetVector(s_maskInverse0, new Vector4(inverse.A, inverse.B, inverse.Tx, 0f));
            s_maskProperties.SetVector(s_maskInverse1, new Vector4(inverse.C, inverse.D, inverse.Ty, 0f));
            s_maskProperties.SetVector(s_maskSize, new Vector4(group.MaskSize.x, group.MaskSize.y, 0f, 0f));
            cmd.DrawProcedural(Matrix4x4.identity, s_maskMaterial, 0, MeshTopology.Triangles, 6, 1, s_maskProperties);
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
