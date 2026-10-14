using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Turns every tree's nodes into what the GPU draws: one buffer of primitives in draw order, one of node slots
    /// (where each node is drawn), one of clip shapes, and the batches to draw them in (split where the texture changes or
    /// a new root starts). Each frame it does only what changed asks for: nodes that moved or faded update the slots
    /// alone; a node drawn again with as many primitives, sampling what its batch has room for, is patched where it is;
    /// the draw order is made again only when what is drawn, or in what order, changes (a node shown or hidden, a group,
    /// a z-index, a presentation, a drawing of a new length). Only what changed goes to the GPU. A matched flight is drawn
    /// above everything at its level as a pair: its picture and its destination each drawn to an image of their own (their
    /// own drawing laid out at the flight's rect, what is inside them fitted and cut to its window), and one quad summing
    /// them.
    /// </summary>
    internal static partial class Compositor
    {
        // A batch's textures: up to this many, each primitive sampling one of them by index.
        private const int TexturesPerBatch = 8;

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
        // Where the textures of each primitive's batch start among the batches' textures.
        private static int[] s_textureBase = new int[256];
        private static int s_instanceCount;

        // A material a batch, holding its textures and the buffers: the draws of a batch's runs then carry no properties
        // of their own, only where in the instances each starts (a global). The groups' batches' follow the main pass's.
        private static readonly List<Material> s_batchMaterials = new();

        // Draw orders made so far: the nodes each one drew carry its number.
        private static int s_builds;

        // Nodes drawn again since the last build, and the primitives patched since (to go to the GPU).
        private static readonly List<UINode> s_redrawn = new();
        private static int s_patchedFrom, s_patchedTo;

        // Whether the slots and clips hold every tree's (each copied whole once), and those copied since the last upload.
        private static bool s_copied;
        private static int s_slotsFrom = int.MaxValue, s_slotsTo, s_clipsFrom = int.MaxValue, s_clipsTo;

        // Scratch for a flight's picture: whether each of its nodes is left out (a named part flying on its own).
        private static readonly List<bool> s_skip = new();
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
        // The two triangles every quad draws as (0 1 2, 0 2 3): four vertices a quad rather than six.
        private static GraphicsBuffer s_indexBuffer;

        private static bool s_orderDirty = true;
        private static bool s_slotsDirty = true;

        // Scratch for drawing order: one node's children sorted by ZIndex, the top layer of the level being walked, and
        // the presented nodes of the tree being walked, in level order.
        private static readonly List<UINode> s_children = new();
        private static readonly List<UINode> s_topLayer = new();
        private static readonly List<UINode> s_levels = new();

        private static Material s_material;

        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int OrderId = Shader.PropertyToID("_Order");
        private static readonly int SlotsId = Shader.PropertyToID("_Slots");
        private static readonly int ClipsId = Shader.PropertyToID("_Clips");
        private static readonly int InstanceOffsetId = Shader.PropertyToID("_InstanceOffset");
        private static readonly int MatrixId = Shader.PropertyToID("_UIMatrix");
        private static readonly int OcclusionId = Shader.PropertyToID("_UINextOcclusion");
        private static readonly int InstanceTotalId = Shader.PropertyToID("_InstanceTotal");
        private static readonly int[] TextureIds =
        {
            Shader.PropertyToID("_Texture0"), Shader.PropertyToID("_Texture1"), Shader.PropertyToID("_Texture2"), Shader.PropertyToID("_Texture3"),
            Shader.PropertyToID("_Texture4"), Shader.PropertyToID("_Texture5"), Shader.PropertyToID("_Texture6"), Shader.PropertyToID("_Texture7"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Batches.Clear();
            s_runs.Clear();
            s_spans.Clear();
            s_segments.Clear();
            s_mainRuns = s_shapeRuns = 0;
            s_offscreenSlots = 0;
            s_classified = 0;
            s_slotBase.Clear();
            s_redrawn.Clear();
            s_orderDirty = s_slotsDirty = true;
            s_copied = false;
            s_instanceCount = s_slotCount = s_clipCount = 0;
        }

        public static bool HasBatches => Batches.Count > 0;

        // What the last build made, for the editor's readout: primitives (groups' included), draw calls in the main pass
        // and in groups' images, and groups.
        internal static int PrimitiveCount => s_instanceCount;
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
            s_indexBuffer?.Release();
            s_orderBuffer?.Release();
            s_instanceBuffer = s_slotBuffer = s_clipBuffer = s_indexBuffer = s_orderBuffer = null;
            foreach (var target in s_groupTargets)
            {
                if (target == null) continue;
                target.Release();
                Object.DestroyImmediate(target);
            }
            s_groupTargets.Clear();
            Groups.Clear();
            Batches.Clear();
            s_runs.Clear();
            s_spans.Clear();
            s_segments.Clear();
            s_mainRuns = s_shapeRuns = 0;
            if (s_material != null)
                Object.DestroyImmediate(s_material);
            s_material = null;
            foreach (var material in s_batchMaterials)
                if (material != null)
                    Object.DestroyImmediate(material);
            s_batchMaterials.Clear();
            if (s_maskMaterial != null)
                Object.DestroyImmediate(s_maskMaterial);
            s_maskMaterial = null;
            UIRenderPass.ReleaseResources();
            s_orderDirty = s_slotsDirty = true;
            s_copied = false;
        }

        /// <summary>
        /// <paramref name="node"/> was drawn again: its primitives are patched where they are if it draws as many, or the
        /// draw order is made again if not.
        /// </summary>
        public static void MarkRedrawn(UINode node)
        {
            if (node.Redrawn) return;
            node.Redrawn = true;
            s_redrawn.Add(node);
        }

        /// <summary>What is drawn, or in what order, changed: the draw order is made again.</summary>
        public static void MarkOrderDirty() => s_orderDirty = true;

        /// <summary>Where nodes are drawn, how opaque, or what clips them changed: the slots and clips go to the GPU again.</summary>
        public static void MarkSlotsDirty() => s_slotsDirty = true;

        /// <summary>Brings the buffers and batches up to date with the trees, doing only what changed asks for.</summary>
        public static void Build()
        {
            using var build = UIProfiling.Composite.Auto();
            using var time = UIProfiling.Measure(UIProfiling.CompositorTime);
            // A flight moves every frame it flies. The focus path is drawn again as focus moves.
            if (MatchedGeometry.Flights.Count > 0)
                s_orderDirty = s_slotsDirty = true;
            if (UIDebug.FocusPath && UIFocus.Focused != s_debugFocused)
                s_orderDirty = true;
            if (!s_orderDirty && !s_slotsDirty && s_redrawn.Count == 0) return;
            if (MatchedGeometry.Flights.Count > 0)
                MatchedGeometry.Update();
            var trees = UITree.Trees;
            if (s_orderDirty || s_slotsDirty)
                CopySlots(trees);
            s_patchedFrom = int.MaxValue;
            s_patchedTo = 0;
            if (!s_orderDirty)
                CheckCovers(trees);
            if (!s_orderDirty)
                PatchRedrawn();
            bool rebuilt = s_orderDirty;
            if (rebuilt)
                Rebuild(trees);
            foreach (var node in s_redrawn)
                if (node != null)
                    node.Redrawn = false;
            s_redrawn.Clear();
            MakeRuns();
            Upload(rebuilt);
            s_orderDirty = false;
            s_slotsDirty = false;
        }

        // The material a batch draws with: its textures and the buffers set on it, so its draws carry no properties.
        private static Material MaterialFor(int index, in Batch batch, Matrix4x4 matrix, bool occlusion)
        {
            while (s_batchMaterials.Count <= index)
                s_batchMaterials.Add(new Material(Material) { hideFlags = HideFlags.HideAndDontSave });
            var material = s_batchMaterials[index];
            material.SetBuffer(InstancesId, s_instanceBuffer);
            material.SetBuffer(OrderId, s_orderBuffer);
            material.SetBuffer(SlotsId, s_slotBuffer);
            material.SetBuffer(ClipsId, s_clipBuffer);
            material.SetMatrix(MatrixId, matrix);
            material.SetFloat(OcclusionId, occlusion ? 1f : 0f);
            material.SetInt(InstanceTotalId, s_instanceCount);
            for (int t = 0; t < TexturesPerBatch; t++)
            {
                var texture = s_batchTextures[batch.Textures + t];
                material.SetTexture(TextureIds[t], texture != null ? texture : Texture2D.whiteTexture);
            }
            return material;
        }

        // Slots and clips: every tree's, one after another, and the flights'. A tree's are copied where compositing changed
        // them since the last copy, or whole when they moved in the buffers (a tree before it grew or shrank), it has more or
        // fewer than it had, or nothing was copied yet. What was copied is sent to the GPU.
        private static void CopySlots(IReadOnlyList<UITree> trees)
        {
            using var copy = UIProfiling.CopySlots.Auto();
            s_slotCount = 0;
            s_clipCount = 0;
            s_slotBase.Clear();
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                int nodes = tree.Nodes.Count;
                int clips = tree.ClipShapes.Count;
                int slotBase = s_slotCount, clipBase = s_clipCount;
                s_slotBase[tree] = slotBase;
                EnsureSlots(slotBase + nodes);
                EnsureClips(clipBase + clips);
                bool whole = !s_copied || tree.CopiedSlotBase != slotBase || tree.CopiedClipBase != clipBase
                             || tree.CopiedSlots != nodes || tree.CopiedClips != clips;
                int from = whole ? 0 : tree.ChangedSlotsFrom, to = whole ? nodes : Mathf.Min(tree.ChangedSlotsTo, nodes);
                if (to > from)
                {
                    for (int i = from; i < to; i++)
                    {
                        var slot = tree.Slots[i];
                        slot.Clips = OffsetChain(slot.Clips, clipBase);
                        s_slots[slotBase + i] = slot;
                    }
                    SentSlots(slotBase + from, slotBase + to);
                }
                int slotsFrom = from, slotsTo = to;
                from = whole ? 0 : tree.ChangedClipsFrom;
                to = whole ? clips : Mathf.Min(tree.ChangedClipsTo, clips);
                if (to > from)
                {
                    for (int c = from; c < to; c++)
                        s_clips[clipBase + c] = tree.ClipShapes[c];
                    SentClips(clipBase + from, clipBase + to);
                }
                // Classified once their clips are in place.
                for (int i = slotsFrom; i < slotsTo; i++)
                    ClassifySlot(slotBase + i, tree.Offscreen[i]);
                tree.ChangedSlotsFrom = tree.ChangedClipsFrom = int.MaxValue;
                tree.ChangedSlotsTo = tree.ChangedClipsTo = 0;
                tree.CopiedSlotBase = slotBase;
                tree.CopiedClipBase = clipBase;
                tree.CopiedSlots = nodes;
                tree.CopiedClips = clips;
                s_slotCount += nodes;
                s_clipCount += clips;
            }
            s_copied = true;
            int treeSlots = s_slotCount, treeClips = s_clipCount;
            AddFlightSlots();
            SentSlots(treeSlots, s_slotCount);
            SentClips(treeClips, s_clipCount);
            // Slots gone since the last copy (a tree removed, a flight over) count for nothing.
            for (int i = s_slotCount; i < s_classified; i++)
            {
                if (s_slotClipState[i] == ClipsOutside) s_offscreenSlots--;
                s_slotClipState[i] = ClipsNone;
            }
            s_classified = s_slotCount;
        }

        // How many slots have been classified: those past the count are cleared as the count falls.
        private static int s_classified;

        // Slots and clips copied since the last upload, to send with the next.
        private static void SentSlots(int from, int to)
        {
            if (to <= from) return;
            s_slotsFrom = Mathf.Min(s_slotsFrom, from);
            s_slotsTo = Mathf.Max(s_slotsTo, to);
        }

        private static void SentClips(int from, int to)
        {
            if (to <= from) return;
            s_clipsFrom = Mathf.Min(s_clipsFrom, from);
            s_clipsTo = Mathf.Max(s_clipsTo, to);
        }

        // Makes the draw order again: every tree walked into primitives in drawing order, batched, its groups after it.
        private static void Rebuild(IReadOnlyList<UITree> trees)
        {
            using var order = UIProfiling.Order.Auto();
            UIProfiling.OrderRebuilds.Value++;
            s_builds++;
            s_instanceCount = 0;
            Batches.Clear();
            s_batchTextures.Clear();
            Groups.Clear();
            GroupBatches.Clear();
            s_debugFocused = UIFocus.Focused;
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                if (tree.Nodes.Count == 0) continue;
                int start = s_instanceCount;
                SubtreeBounds(tree);
                BeginRange();
                if (UIDebug.Overdraw)
                    EmitOverdrawBackdrop(tree);
                tree.DrawOrder.Clear();
                tree.Levels.Clear();
                tree.LevelStarts.Clear();
                tree.LevelInteractive.Clear();
                CollectLevels(tree);
                // From the topmost cover that is in place: nothing under it shows.
                int from = TopCover(tree, s_levels);
                tree.AllLevels.Clear();
                tree.AllLevels.AddRange(s_levels);
                tree.CoverFrom = from;
                tree.CoverCheck = false;
                for (int l = from; l < s_levels.Count; l++)
                {
                    var level = s_levels[l];
                    tree.Levels.Add(level);
                    tree.LevelStarts.Add(tree.DrawOrder.Count);
                    tree.LevelInteractive.Add(level.PresentationLevel.Peek >= 0);
                    s_topLayer.Clear();
                    // Each level above the last; what is lifted into the top layer above the level, then its flights.
                    s_layerTop = Mathf.Max(s_layerTop, Walk(tree, level, s_slotBase[tree], true, level, s_layerTop + 1));
                    for (int i = 0; i < s_topLayer.Count; i++)
                        s_layerTop = Mathf.Max(s_layerTop, Walk(tree, s_topLayer[i], s_slotBase[tree], true, level, s_layerTop + 1));
                    EmitFlights(tree, l);
                }
                LayerCount = s_layerTop + 1;
                EndRange(Batches, tree, start);
                WalkGroups(tree);
                // Over all of it, once its groups (inner ones included) are known: the debug views' outlines.
                int debug = s_instanceCount;
                BeginRange();
                EmitDebugOutlines(tree);
                if (s_instanceCount > debug)
                    EndRange(Batches, tree, debug);
            }
        }

        // Which of a tree's levels (its root, then each presented node, lowest first) it is drawn from: the topmost cover in
        // place, which hides everything below it, or the root.
        private static int TopCover(UITree tree, List<UINode> levels)
        {
            for (int l = levels.Count - 1; l >= 1; l--)
            {
                if (Presentation.CoversWhatIsBelow(tree, levels[l]))
                    return l;
            }
            return 0;
        }

        // For each tree a presented screen or what is on it moved or faded in: the draw order is made again if a different
        // cover is in place now (or none is), as what is drawn below it changes with it.
        private static void CheckCovers(IReadOnlyList<UITree> trees)
        {
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                if (!tree.CoverCheck) continue;
                tree.CoverCheck = false;
                if (TopCover(tree, tree.AllLevels) != tree.CoverFrom)
                    s_orderDirty = true;
            }
        }

        // The nodes drawn again since the last build, patched where their primitives are: each must draw as many as it did,
        // each sampling nothing or a texture its batch has or has room for, and not be in a group (whose image is as big as
        // what is drawn in it). Failing that, the draw order is made again. A node not drawn now has nothing to patch: it is
        // drawn afresh once it is drawn at all, which makes the draw order again.
        private static void PatchRedrawn()
        {
            using var patch = UIProfiling.Patch.Auto();
            foreach (var node in s_redrawn)
            {
                if (node == null || node.Tree == null || node.EmittedBuild != s_builds) continue;
                if (node.EmittedInGroup || node.Instances.Count != node.EmittedCount
                                        || node.OverlayInstances.Count != node.EmittedOverlayCount
                                        || !s_slotBase.TryGetValue(node.Tree, out int slotBase))
                {
                    s_orderDirty = true;
                    return;
                }
                uint slot = (uint)(slotBase + node.Index);
                if (!Patch(node.Instances, node.InstanceTextures, node.EmittedAt, slot)
                    || !Patch(node.OverlayInstances, node.OverlayTextures, node.EmittedOverlayAt, slot))
                {
                    s_orderDirty = true;
                    return;
                }
            }
        }

        // One run of a node's primitives, put back where they were drawn from `at` on.
        private static bool Patch(List<PrimitiveInstance> instances, List<Texture> textures, int at, uint slot)
        {
            int count = instances.Count;
            if (count == 0) return true;
            // The last texture looked up, in which batch, and where: a label's glyphs all sample one.
            Texture last = null;
            int lastFirst = -1, lastIndex = 0;
            for (int i = 0; i < count; i++)
            {
                var texture = textures[i];
                int first = s_textureBase[at + i];
                int index = 0;
                if (texture is not null)
                {
                    if (ReferenceEquals(texture, last) && first == lastFirst)
                    {
                        index = lastIndex;
                    }
                    else
                    {
                        index = BatchTextureOf(texture, first);
                        if (index < 0) return false;
                        last = texture;
                        lastFirst = first;
                        lastIndex = index;
                    }
                }
                var instance = instances[i];
                instance.Slot = slot;
                instance.Texture = (uint)index;
                // Drawn with another pass than before: its run must be made again.
                if (KindOf(instance) != s_kinds[at + i]) return false;
                s_instances[at + i] = instance;
                s_textures[at + i] = texture;
            }
            s_patchedFrom = Mathf.Min(s_patchedFrom, at);
            s_patchedTo = Mathf.Max(s_patchedTo, at + count);
            UIProfiling.Patched.Value += count;
            return true;
        }

        // Where a texture is among its batch's (which start at `first`), taking a slot the batch has free if it is not
        // there; -1 if the batch has none free.
        private static int BatchTextureOf(Texture texture, int first)
        {
            int slot = FindBatchTexture(texture, first, TexturesPerBatch);
            if (slot >= 0) return slot;
            slot = FindBatchTexture(null, first, TexturesPerBatch);
            if (slot >= 0) s_batchTextures[first + slot] = texture;
            return slot;
        }

        // Where a texture is among the `count` batch textures from `first`, or -1. Compared as references: a batch holds
        // the very textures its primitives sample, and Unity's equality would ask the engine whether each is still alive.
        private static int FindBatchTexture(Texture texture, int first, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(s_batchTextures[first + i], texture))
                    return i;
            }
            return -1;
        }

        // ── Debug views ──────────────────────────────────────────────────────────

        // What had focus when the focus path was last drawn.
        private static UINode s_debugFocused;

        private static readonly Color DebugFocused = Color.white;
        private static readonly Color DebugSection = new(0.2f, 0.85f, 0.75f, 1f);
        private static readonly Color DebugGroup = new(1f, 0.6f, 0.15f, 1f);
        private static readonly Color DebugFading = new(0.25f, 0.85f, 1f, 1f);
        private static readonly Color DebugBlurred = new(1f, 0.3f, 0.9f, 1f);
        private static readonly Color DebugMasked = new(1f, 0.9f, 0.2f, 1f);
        private static readonly Color DebugFlight = new(0.4f, 1f, 0.4f, 1f);

        // Under a tree's UI in the overdraw view: its root darkened, so the heat reads over whatever is behind.
        private static void EmitOverdrawBackdrop(UITree tree)
        {
            EmitDebug(new PrimitiveInstance
            {
                Color = new Color(0f, 0f, 0f, 0.85f),
                Slot = (uint)s_slotBase[tree],
                Type = (uint)PrimitiveType.Shape,
                Flags = (uint)(PrimitiveFlags.RelativeToNode | PrimitiveFlags.Debug),
            });
        }

        // Over a tree's UI: each layer's bounds, and the focus path, as the debug views ask.
        private static void EmitDebugOutlines(UITree tree)
        {
            int slotBase = s_slotBase[tree];
            if (UIDebug.LayerBounds)
            {
                foreach (var group in Groups)
                {
                    if (group.Tree != tree) continue;
                    var colour = group.Flight != null ? DebugFlight : group.Mask != null ? DebugMasked
                        : group.Blur > 0.01f ? DebugBlurred : DebugFading;
                    var region = group.Region;
                    EmitOutline(new Vector4(region.x, region.y, region.width, region.height), colour, 2f, (uint)slotBase, relative: false);
                }
            }
            var focused = UIFocus.Focused;
            if (UIDebug.FocusPath && focused != null && focused.Tree == tree && focused.Index >= 0)
            {
                // Each outline a little further in than the last, so ones round the same rect all show.
                EmitOutline(Vector4.zero, DebugFocused, 2f, (uint)(slotBase + focused.Index), relative: true);
                var level = MatchedGeometry.LevelOf(focused);
                for (var n = focused.ParentNode; n != null; n = n.ParentNode)
                {
                    var scope = n.FocusScope.Peek;
                    if (scope != FocusScope.None && n.Index >= 0)
                        EmitOutline(Vector4.one * 2f, scope == FocusScope.Group ? DebugGroup : DebugSection, 1.5f,
                            (uint)(slotBase + n.Index), relative: true);
                    if (n == level) break;
                }
            }
        }

        // An outline `width` wide inside a rect: insets from a node's edges (`relative`), or a rect in its slot's space.
        private static void EmitOutline(Vector4 rect, Color colour, float width, uint slot, bool relative)
        {
            EmitDebug(new PrimitiveInstance
            {
                Rect = rect,
                Radii = Vector4.one * 4f,
                Color = colour,
                Color2 = colour,
                Params = new Vector4(width, 0f, 0f, 0f),
                Slot = slot,
                Type = (uint)PrimitiveType.Shape,
                Flags = (uint)(PrimitiveFlags.Ring | PrimitiveFlags.Debug | (relative ? PrimitiveFlags.RelativeToNode : PrimitiveFlags.None)),
            });
        }

        // A debug primitive, in a layer of its own above everything in its range so far (the backdrop is the first thing
        // in its tree's).
        private static void EmitDebug(in PrimitiveInstance instance) => PutOnTop(instance, null, null);

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

        // A group's quad: covering its root (its slot is the root's), sampling the group's image at the group's opacity.
        private static void EmitGroupQuad(UITree tree, UINode node, int slotBase, int layer)
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
                Mask = node.CompositeGroup.Peek ? node.Mask.Peek : null,
                MaskSize = tree.Slots[node.Index].SizeOpacity,
                MaskInverse = tree.World[node.Index].Inverse,
            });
            Put(new PrimitiveInstance
            {
                Rect = new Vector4(region.x, region.y, region.width, region.height),
                Color = new Color(1f, 1f, 1f, tree.GroupOpacities[node.Index]),
                Uv = new Vector4(0f, 0f, 1f, 1f),
                Slot = (uint)slotBase,
                Type = (uint)PrimitiveType.Group,
            }, (byte)PrimitiveKind.Group, layer, target, null);
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
                Include(instances[k], world, size, ref min, ref max);
        }

        // Grows the bounds by one primitive of a node `size` big at `world`, as the shader draws it.
        private static void Include(in PrimitiveInstance instance, Affine2 world, Vector2 size, ref Vector2 min, ref Vector2 max)
        {
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
                BeginRange();
                if (group.Flight != null)
                {
                    EmitFlightEnd(tree, group.Flight, group.End);
                }
                else
                {
                    s_groupRoot = group.Node;
                    try
                    {
                        Walk(tree, group.Node, s_slotBase[tree], true, group.Node, 0);
                    }
                    finally
                    {
                        s_groupRoot = null;
                    }
                }
                group.FirstBatch = GroupBatches.Count;
                EndRange(GroupBatches, tree, start);
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

        private static Vector4 OffsetChain(Vector4 chain, int clipBase)
        {
            for (int c = 0; c < 4; c++)
                if (chain[c] >= 0f)
                    chain[c] += clipBase;
            return chain;
        }

        // Sends the GPU what changed: every primitive after a new draw order (or those patched since the last build), and
        // the slots and clips copied since the last upload. A buffer made anew is sent whole.
        private static void Upload(bool rebuilt)
        {
            using var upload = UIProfiling.Upload.Auto();
            long bytes = 0;
            if (s_instanceCount > 0 || s_slotCount > 0)
            {
                bool newInstances = Ensure(ref s_instanceBuffer, Mathf.Max(1, s_instanceCount), PrimitiveInstance.Stride);
                bool newSlots = Ensure(ref s_slotBuffer, Mathf.Max(1, s_slotCount), NodeSlot.Stride);
                bool newClips = Ensure(ref s_clipBuffer, Mathf.Max(1, s_clipCount), ClipShape.Stride);
                bytes += Send(s_instanceBuffer, s_instances, rebuilt || newInstances ? 0 : s_patchedFrom,
                    rebuilt || newInstances ? s_instanceCount : s_patchedTo, PrimitiveInstance.Stride);
                bytes += Send(s_slotBuffer, s_slots, newSlots ? 0 : s_slotsFrom, newSlots ? s_slotCount : Mathf.Min(s_slotsTo, s_slotCount), NodeSlot.Stride);
                bytes += Send(s_clipBuffer, s_clips, newClips ? 0 : s_clipsFrom, newClips ? s_clipCount : Mathf.Min(s_clipsTo, s_clipCount), ClipShape.Stride);
                bool newOrder = Ensure(ref s_orderBuffer, Mathf.Max(1, s_instanceCount), sizeof(int));
                if (rebuilt || newOrder)
                    bytes += Send(s_orderBuffer, s_order, 0, s_instanceCount, sizeof(int));
                if (s_indexBuffer == null || !s_indexBuffer.IsValid())
                {
                    s_indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, 6, sizeof(ushort));
                    s_indexBuffer.SetData(new ushort[] { 0, 1, 2, 0, 2, 3 });
                }
            }
            s_slotsFrom = s_clipsFrom = int.MaxValue;
            s_slotsTo = s_clipsTo = 0;
            UIProfiling.Uploaded.Value += (int)(bytes / 1024);
        }

        // Sends one buffer's run [from, to) to the GPU; how many bytes that was.
        private static long Send<T>(GraphicsBuffer buffer, T[] data, int from, int to, int stride) where T : struct
        {
            if (to <= from) return 0;
            buffer.SetData(data, from, from, to - from);
            return (long)(to - from) * stride;
        }

        // A buffer big enough for `count`, made anew (true) if the one there is not.
        private static bool Ensure(ref GraphicsBuffer buffer, int count, int stride)
        {
            if (buffer != null && buffer.IsValid() && buffer.count >= count) return false;
            buffer?.Release();
            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(count), stride);
            return true;
        }

        private static void EnsureInstances(int count)
        {
            if (s_instances.Length >= count) return;
            int size = Mathf.NextPowerOfTwo(count);
            System.Array.Resize(ref s_instances, size);
            System.Array.Resize(ref s_textures, size);
            System.Array.Resize(ref s_textures2, size);
            System.Array.Resize(ref s_textureBase, size);
            System.Array.Resize(ref s_kinds, size);
            System.Array.Resize(ref s_layers, size);
        }

        private static void EnsureSlots(int count)
        {
            if (s_slots.Length < count) System.Array.Resize(ref s_slots, Mathf.NextPowerOfTwo(count));
            EnsureSlotStates(count);
        }

        private static void EnsureClips(int count)
        {
            if (s_clips.Length < count) System.Array.Resize(ref s_clips, Mathf.NextPowerOfTwo(Mathf.Max(count, 16)));
        }

        // ── Flights ──────────────────────────────────────────────────────────────

        // Slots and clips for every flight placed, after the trees': its pair's, its window (which cuts what is inside each
        // end), and each end's: what is inside it fitted into the flight, and its own drawing laid out at the flight's rect.
        private static void AddFlightSlots()
        {
            foreach (var flight in MatchedGeometry.Flights)
                AddFlightSlots(flight);
        }

        private static void AddFlightSlots(Flight flight)
        {
            var tree = flight.Tree;
            var destination = flight.Destination;
            flight.InBuild = flight.Placed && tree != null && tree.Root != null && s_slotBase.ContainsKey(tree)
                             && destination != null && destination.Tree == tree && destination.Index >= 0;
            if (!flight.InBuild) return;

            var rect = flight.Rect;
            float pixels = tree.Root.PixelsPerPoint;
            // Its window: its rect, with its corners, which cuts what is inside each end. Each end's own drawing is not cut,
            // so a shadow falls past it.
            flight.Window = AddClip(Affine2.Translate(-rect.position), rect.size, flight.Radii);
            flight.PairSlot = AddSlot(new NodeSlot
            {
                Row0 = new Vector4(1f, 0f, 0f, 1f),
                Row1 = new Vector4(0f, 1f, 0f, 1f),
                SizeOpacity = new Vector4(rect.width, rect.height, 1f, 1f / Mathf.Max(1e-4f, pixels)),
                Clips = new Vector4(-1f, -1f, -1f, -1f),
            });

            // The destination, live: what is in it fitted into the flight, at its own opacity (its transition's left out:
            // the flight fades it in), cut by the clips inside it and by the window.
            int d = destination.Index, end = tree.SubtreeEnd[d];
            flight.LiveSlots = s_slotCount;
            EnsureSlots(s_slotCount + end - d);
            var fromFlight = flight.FitMap.Inverse;
            var outside = tree.Slots[d].Clips;
            for (int i = d; i < end; i++)
            {
                var slot = tree.Slots[i];
                var world = flight.FitMap * tree.World[i];
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
                // A node already under four clips inside the end gives up the outermost of them to the window.
                var clips = Append(FlightClips(tree.ClipShapes, slot.Clips, outside, fromFlight), flight.Window);
                s_slots[flight.LiveSlots + i - d] = new NodeSlot
                {
                    Row0 = new Vector4(world.A, world.B, world.Tx, slot.Row0.w),
                    Row1 = new Vector4(world.C, world.D, world.Ty, slot.Row1.w),
                    SizeOpacity = new Vector4(slot.SizeOpacity.x, slot.SizeOpacity.y, opacity, 1f / Mathf.Max(1e-4f, pixels * world.ScaleFactor)),
                    Clips = clips,
                };
            }
            s_slotCount += end - d;
            for (int i = flight.LiveSlots; i < s_slotCount; i++)
                ClassifySlot(i, false);
            flight.LiveOwn = AddOwn(s_slots[flight.LiveSlots], rect, pixels);

            // The picture, as the source was drawn, fitted the same way.
            var picture = flight.Picture;
            flight.PictureSlots = s_slotCount;
            EnsureSlots(s_slotCount + picture.Slots.Count);
            var fromPicture = flight.PictureMap.Inverse;
            for (int k = 0; k < picture.Slots.Count; k++)
            {
                var drawn = picture.Slots[k];
                var world = flight.PictureMap * drawn.Transform;
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
                    SizeOpacity = new Vector4(drawn.Size.x, drawn.Size.y, drawn.Opacity, 1f / Mathf.Max(1e-4f, pixels * world.ScaleFactor)),
                    Clips = Append(clips, flight.Window),
                };
            }
            s_slotCount += picture.Slots.Count;
            for (int i = flight.PictureSlots; i < s_slotCount; i++)
                ClassifySlot(i, false);
            flight.PictureOwn = picture.Slots.Count > 0 ? AddOwn(s_slots[flight.PictureSlots], rect, pixels) : -1;
        }

        // The slot an end's own drawing (what its root draws relative to its rect: a box, its border and shadow, an image
        // filling it) is laid out in: the flight's rect, unscaled, as opaque and filtered as the end's root and cut by
        // nothing, so its corners and borders keep their points and its shadow falls past the flight's edges.
        private static int AddOwn(NodeSlot root, Rect rect, float pixels) => AddSlot(new NodeSlot
        {
            Row0 = new Vector4(1f, 0f, rect.x, root.Row0.w),
            Row1 = new Vector4(0f, 1f, rect.y, root.Row1.w),
            SizeOpacity = new Vector4(rect.width, rect.height, root.SizeOpacity.z, 1f / Mathf.Max(1e-4f, pixels)),
            Clips = new Vector4(-1f, -1f, -1f, -1f),
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
            int index = s_slotCount++;
            s_slots[index] = slot;
            ClassifySlot(index, false);
            return index;
        }

        // A clip from its transform from its root's space into its own, its size and its corners.
        private static int AddClip(Affine2 inverse, Vector2 size, Vector4 radii)
        {
            EnsureClips(s_clipCount + 1);
            s_clips[s_clipCount] = ClipShape.Of(inverse, size, radii);
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
        // ends were drawn at (one flying home from a presentation going away is drawn over that presentation). Named parts
        // set off after the pair they are in, so they are drawn over it.
        private static void EmitFlights(UITree tree, int level)
        {
            foreach (var flight in MatchedGeometry.Flights)
            {
                if (!flight.InBuild || flight.Tree != tree) continue;
                int at = s_levels.IndexOf(MatchedGeometry.LevelOf(flight.Destination));
                if (flight.SourceLevel != null)
                    at = Mathf.Max(at, s_levels.IndexOf(flight.SourceLevel));
                if (at == level)
                    EmitFlight(tree, flight);
            }
        }

        // A flight's pair: its picture's image and its destination's, each holding its whole end (what is inside it cut to
        // the window, its own drawing reaching past it), added together in one quad as they cross-fade.
        private static void EmitFlight(UITree tree, Flight flight)
        {
            var root = tree.Root;
            float scale = root.PixelsPerPoint;
            // What the two cover: the flight's rect, and whatever the ends' own drawing reaches past it (a shadow).
            var rect = flight.Rect;
            var min = rect.min;
            var max = rect.max;
            var own = Affine2.Translate(rect.position);
            IncludeOwn(flight.Destination.Instances, own, rect.size, ref min, ref max);
            IncludeOwn(flight.Destination.OverlayInstances, own, rect.size, ref min, ref max);
            var pictured = flight.Picture.Instances;
            for (int i = 0; i < pictured.Count; i++)
                if (pictured[i].Slot == 0)
                    IncludeOwn(pictured[i], own, rect.size, ref min, ref max);
            const float smoothing = 2f;
            int x = Mathf.FloorToInt(Mathf.Max(0f, min.x * scale - smoothing));
            int y = Mathf.FloorToInt(Mathf.Max(0f, min.y * scale - smoothing));
            int xMax = Mathf.CeilToInt(Mathf.Min(root.Size.x * scale, max.x * scale + smoothing));
            int yMax = Mathf.CeilToInt(Mathf.Min(root.Size.y * scale, max.y * scale + smoothing));
            // Off the screen: nothing of it is seen.
            if (xMax <= x || yMax <= y) return;
            var picture = GroupTarget(Groups.Count, xMax - x, yMax - y);
            var region = new Rect(x / scale, y / scale, picture.width / scale, picture.height / scale);
            Groups.Add(new Group { Tree = tree, Target = picture, Region = region, Flight = flight, End = 0 });
            // At least as big as the picture's image, so both cover the region as sharply.
            var live = GroupTarget(Groups.Count, picture.width, picture.height);
            Groups.Add(new Group { Tree = tree, Target = live, Region = region, Flight = flight, End = 1 });

            // Above everything at its level so far.
            PutOnTop(new PrimitiveInstance
            {
                Rect = new Vector4(region.x, region.y, region.width, region.height),
                Color = new Color(1f, 1f, 1f, flight.Progress),
                Uv = new Vector4(0f, 0f, 1f, 1f),
                Slot = (uint)flight.PairSlot,
                Type = (uint)PrimitiveType.Pair,
            }, picture, live);
        }

        // Grows the bounds by what an end's root draws relative to its rect (a box, a shadow's blur past it), laid out at
        // the flight's rect by `own`.
        private static void IncludeOwn(List<PrimitiveInstance> instances, Affine2 own, Vector2 size, ref Vector2 min, ref Vector2 max)
        {
            for (int i = 0; i < instances.Count; i++)
                IncludeOwn(instances[i], own, size, ref min, ref max);
        }

        private static void IncludeOwn(in PrimitiveInstance instance, Affine2 own, Vector2 size, ref Vector2 min, ref Vector2 max)
        {
            if ((instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0)
                Include(instance, own, size, ref min, ref max);
        }

        // One end of a flight, into its image: the picture (0) or the destination (1), its own drawing laid out at the
        // flight's rect and the rest fitted, leaving out the named parts that fly on their own inside it.
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
                s_skip.Add((drawn.Parent >= 0 && s_skip[drawn.Parent]) || (k > 0 && (FliesOnItsOwn(drawn.Node) || SetsOff(drawn.Node))));
            }
            // As it was drawn: a layer each keeps its order.
            for (int i = 0; i < picture.Instances.Count; i++)
            {
                var instance = picture.Instances[i];
                int k = (int)instance.Slot;
                if (k >= s_skip.Count || s_skip[k]) continue;
                instance.Slot = k == 0 && (instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0
                    ? (uint)flight.PictureOwn
                    : (uint)(flight.PictureSlots + k);
                PutOnTop(instance, picture.Textures[i], null);
            }
        }

        // Whether a node inside a flight's end flies on its own: a named part, taken over or flying in.
        private static bool FliesOnItsOwn(UINode node) => node != null && (node.FlightIn != null || node.FlightOut != null);

        // Whether a flight sets off from a node: one that stays, a copy flying out of it, as well as one taken over.
        private static bool SetsOff(UINode node)
        {
            foreach (var flight in MatchedGeometry.Flights)
                if (flight.Source == node)
                    return true;
            return false;
        }

        // A flight's destination and what is inside it, as the walk draws them, in the flight's slots: its own drawing laid
        // out at the flight's rect, the rest fitted; what flies on its own (a named part) and what is presented above it
        // are left out.
        private static void WalkLive(UITree tree, Flight flight, UINode node)
        {
            int i = node.Index;
            if (i < 0 || node.Tree != tree || !tree.Shown[i]) return;
            bool root = node == flight.Destination;
            if (!root && (node.PresentationLevel.Peek != 0 || FliesOnItsOwn(node))) return;
            int slot = flight.LiveSlots + i - flight.Destination.Index;
            if (s_slots[slot].SizeOpacity.z <= 0f) return;
            uint own = root ? (uint)flight.LiveOwn : (uint)slot;
            EmitLive(node.Instances, node.InstanceTextures, (uint)slot, own);

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
            EmitLive(node.OverlayInstances, node.OverlayTextures, (uint)slot, own);
        }

        // A node's primitives in a flight: those it draws relative to its rect in `own` (the destination's own drawing,
        // laid out at the flight's rect; for the nodes inside it, their slot), the rest in `slot`.
        private static void EmitLive(List<PrimitiveInstance> instances, List<Texture> textures, uint slot, uint own)
        {
            // As the walk draws them: a layer each keeps their order.
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                instance.Slot = (instance.Flags & (uint)PrimitiveFlags.RelativeToNode) != 0 ? own : slot;
                PutOnTop(instance, textures[i], null);
            }
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
        /// Draws every batch with <paramref name="cmd"/>. In the game view a root covers the screen, and the occluders are
        /// drawn first, into the pass's depth buffer, so what an opaque shape covers is not shaded (see Primitive.shader);
        /// in the scene view a root is drawn where its RectTransform is, seen by <paramref name="camera"/>.
        /// </summary>
        public static void Render(RasterCommandBuffer cmd, Camera camera, bool sceneView, bool flipped)
        {
            using var render = UIProfiling.Render.Auto();
            bool occlusion = !sceneView;
            if (occlusion && UIDebug.Occluders)
                DrawOccluders(cmd, camera, flipped);
            for (int r = 0; r < UIDebug.Repeat; r++)
                Draw(cmd, Batches, 0, Batches.Count, camera, sceneView, flipped, occlusion: occlusion);
        }

        // The occluders: a draw for each root's run of batches, which lie one after another in the buffer.
        private static void DrawOccluders(RasterCommandBuffer cmd, Camera camera, bool flipped)
        {
            if (Material == null || s_instanceBuffer == null) return;
            for (int b = 0; b < Batches.Count; b++)
            {
                var batch = Batches[b];
                if (batch.Tree.Root == null) continue;
                var material = MaterialFor(b, batch, MatrixFor(batch.Tree.Root, camera, false, flipped), true);
                for (int s = batch.FirstSpan; s < batch.FirstSpan + batch.SpanCount; s++)
                {
                    var span = s_spans[s];
                    cmd.SetGlobalInt(InstanceOffsetId, span.Start);
                    cmd.DrawProcedural(s_indexBuffer, Matrix4x4.identity, material, OccludersPass, MeshTopology.Triangles, 6, span.Count);
                }
            }
        }

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

        // Draws batches [first, first + count) of `batches`: the main pass's (whose materials come first) or the groups'.
        private static void Draw(RasterCommandBuffer cmd, List<Batch> batches, int first, int count, Camera camera, bool sceneView, bool flipped,
            Matrix4x4? region = null, bool occlusion = false)
        {
            if (Material == null || count == 0 || s_instanceBuffer == null) return;
            int materials = ReferenceEquals(batches, Batches) ? 0 : Batches.Count;
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
                var material = MaterialFor(materials + b, batch, matrix, occlusion);
                // Each run with its pass, from where it starts.
                for (int r = batch.FirstRun; r < batch.FirstRun + batch.RunCount; r++)
                {
                    var run = s_runs[r];
                    cmd.SetGlobalInt(InstanceOffsetId, run.Start);
                    cmd.DrawProcedural(s_indexBuffer, Matrix4x4.identity, material, run.Pass, MeshTopology.Triangles, 6, run.Count * PartsOf(run.Pass));
                    // A shape's or an image's bands after its corners: within a layer nothing overlaps, so the order is free.
                    int bands = BandPassOf(run.Pass);
                    if (bands >= 0)
                        cmd.DrawProcedural(s_indexBuffer, Matrix4x4.identity, material, bands, MeshTopology.Triangles, 6, run.Count * 3);
                }
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
