using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The order things are drawn in. As the trees are walked, every primitive is given a layer such that nothing within a
    /// layer overlaps, and put straight into that layer's bucket for its kind; the buckets laid end to end, layer by layer
    /// and kind by kind, are the order the GPU goes through the primitives in (the primitives themselves stay recorded in
    /// tree order, so a node drawn again is patched in place). The order is cut into batches by texture, into segments by
    /// node, and each build into runs by pass, skipping the nodes scrolled out from under their clips. Where nothing
    /// overlaps the order between primitives is free, so a layer draws each kind in one call, and a shape's corners
    /// before its bands.
    /// </summary>
    internal static partial class Compositor
    {
        /// <summary>A range of the order drawn with one set of textures, and its segments, runs and spans.</summary>
        internal struct Batch
        {
            public UITree Tree;
            public int Start;
            public int Count;
            public int Textures;
            public int FirstSegment;
            public int SegmentCount;
            public int FirstRun;
            public int RunCount;
            public int FirstSpan;
            public int SpanCount;
        }

        // A node's primitives of one kind in one layer, in a row in the order: what a build decides on at once (its node
        // is drawn or scrolled away, its clips need testing or not).
        private struct Segment
        {
            public int Start;
            public int Count;
            public int Layer;
            public uint Slot;
            public byte Kind;
        }

        // A run of the order drawn in one call with one pass; a span: a run of the order whose occluders are drawn.
        private struct Run
        {
            public int Start;
            public int Count;
            public int Pass;
        }

        // The kinds of primitive, each with a pass of its own in the primitive shader (two: plain, and clipped), in the
        // order the passes come.
        private enum PrimitiveKind : byte
        {
            Shape = 0,
            Shadow = 1,
            Image = 2,
            Sliced = 3,
            Glyph = 4,
            Bitmap = 5,
            Group = 6,
            Pair = 7,
        }

        // What a primitive is drawn with (s_kinds): its kind in the low bits; whether its pixels must test its clips
        // whatever they are (an italic glyph: the vertex shader's cut is not exact for a skewed quad); whether a debug view
        // drew it (drawn as it is in the overdraw view).
        private const byte KindMask = 15;
        private const byte KindSkewed = 16;
        private const byte KindDebug = 32;
        private const int Ranks = 8;

        // Whether a slot's clips need testing a pixel (s_slotClipState): none, all exact (the node and its clips upright,
        // the clips square-cornered, so the vertex shader's cut leaves nothing to test), or some to test; or the node is
        // scrolled out from under one of its clips, so nothing of it is drawn at all.
        private const byte ClipsNone = 0, ClipsExact = 1, ClipsTested = 2, ClipsOutside = 3;

        // The primitive shader's passes after the kinds' (two a kind): the occluders, the overdraw view's heat, and a
        // shape's bands (plain and clipped), drawn after its corners.
        private const int OccludersPass = 16;
        private const int OverdrawPass = 17;
        private const int FlatPass = 18;
        private const int ImageFlatPass = 20;

        // Per primitive: what it is drawn with, and its layer.
        private static byte[] s_kinds = new byte[256];
        private static int[] s_layers = new int[256];

        // The order: per position, the primitive drawn there; a batch, a segment, a run and a span are ranges of it.
        private static int[] s_order = new int[256];
        private static GraphicsBuffer s_orderBuffer;

        // The buckets of the range being walked, one a layer and kind rank, each the primitives put in it in walk order;
        // how many layers' worth are in use.
        private static readonly List<List<int>> s_buckets = new();
        private static int s_bucketLayers;
        // The highest layer given out so far in the range being walked (a tree's main range, or a group's).
        private static int s_layerTop;

        private static readonly List<Segment> s_segments = new();
        private static readonly List<Run> s_runs = new();
        private static readonly List<Run> s_spans = new();
        private static int s_mainRuns, s_shapeRuns;

        // Each slot's clips, as its primitives' pixels must treat them, kept up to date as slots are copied (ClassifySlot).
        private static byte[] s_slotClipState = new byte[64];
        private static int s_offscreenSlots;

        // Per node of the tree being walked, the box round its subtree as drawn, in its root's space (SubtreeBounds).
        private static Rect[] s_subtreeBounds = new Rect[32];
        // Beside each child while its parent is walked: the highest layer in its subtree as drawn (-1: nothing drawn).
        private static readonly List<int> s_childTops = new();
        // Scratch for a node's own primitives: per kind rank, whether one was seen, the box round those seen and their
        // highest layer; and each shape's box and layer.
        private static readonly bool[] s_rankSeen = new bool[Ranks];
        private static readonly Rect[] s_rankBounds = new Rect[Ranks];
        private static readonly int[] s_rankLayer = new int[Ranks];
        private static readonly List<Rect> s_nodeShapes = new();
        private static readonly List<int> s_nodeShapeLayers = new();
        private static readonly Rect Nowhere = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        /// <summary>How many layers the last build drew the main pass in.</summary>
        public static int LayerCount { get; private set; }

        /// <summary>How many nodes are scrolled out from under a clip over them, so not drawn.</summary>
        public static int OffscreenNodes => s_offscreenSlots;

        // What the last build made, for the editor's readout: draw calls in the main pass and in groups' images.
        internal static int DrawCalls => s_runs.Count + s_shapeRuns;
        internal static int MainDrawCalls => s_mainRuns;

        /// <summary>The layer a node's index-th primitive was drawn in by the last build, -1 if it drew none.</summary>
        internal static int LayerOf(UINode node, int index = 0) =>
            node.EmittedBuild == s_builds && index < node.EmittedCount ? s_layers[node.EmittedAt + index] : -1;

        /// <summary>How many draw orders have been made: one more after a rebuild, the same after a patch.</summary>
        internal static int Builds => s_builds;

        // ── Kinds ────────────────────────────────────────────────────────────────

        // What a primitive is drawn with: its kind, from its type and flags; whether its pixels must test its clips
        // whatever they are; whether a debug view drew it.
        private static byte KindOf(in PrimitiveInstance instance)
        {
            byte kind;
            switch ((PrimitiveType)instance.Type)
            {
                case PrimitiveType.Shadow: kind = (byte)PrimitiveKind.Shadow; break;
                case PrimitiveType.Image:
                    kind = (byte)((instance.Flags & (uint)PrimitiveFlags.Sliced) != 0 ? PrimitiveKind.Sliced : PrimitiveKind.Image);
                    break;
                case PrimitiveType.GlyphSdf: kind = (byte)PrimitiveKind.Glyph; break;
                case PrimitiveType.GlyphBitmap: kind = (byte)PrimitiveKind.Bitmap; break;
                case PrimitiveType.Group: kind = (byte)PrimitiveKind.Group; break;
                case PrimitiveType.Pair: kind = (byte)PrimitiveKind.Pair; break;
                default: kind = (byte)PrimitiveKind.Shape; break;
            }
            if (instance.Type >= (uint)PrimitiveType.GlyphSdf && instance.Type <= (uint)PrimitiveType.GlyphBitmap && instance.Params.z != 0f)
                kind |= KindSkewed;
            if ((instance.Flags & (uint)PrimitiveFlags.Debug) != 0)
                kind |= KindDebug;
            return kind;
        }

        // The order kinds draw in within a layer: as a node draws them (its shadow, its shape, its image, its text), so a
        // node's own primitives keep their order without a layer each.
        private static int RankOf(byte kind)
        {
            switch ((PrimitiveKind)(kind & KindMask))
            {
                case PrimitiveKind.Shadow: return 0;
                case PrimitiveKind.Shape: return 1;
                case PrimitiveKind.Image: return 2;
                case PrimitiveKind.Sliced: return 3;
                case PrimitiveKind.Glyph: return 4;
                case PrimitiveKind.Bitmap: return 5;
                case PrimitiveKind.Group: return 6;
                default: return 7;
            }
        }

        // How many quads a pass draws a primitive as (PrimitiveKinds.hlsl's PARTS): a shape's four corners (its three bands
        // are the flat pass's), a nine-slice's nine slices, one for the rest.
        private static int PartsOf(int pass)
        {
            if (pass >= OccludersPass) return 1;
            switch ((PrimitiveKind)(pass / 2))
            {
                case PrimitiveKind.Shape:
                case PrimitiveKind.Image: return 4;
                case PrimitiveKind.Sliced: return 9;
                default: return 1;
            }
        }

        private static bool Overlaps(in Rect a, in Rect b) =>
            a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        private static Rect Union(in Rect a, in Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        // ── Slots' clips ─────────────────────────────────────────────────────────

        // A slot's clips, as its primitives' pixels must treat them: none; all exact, where the node and every clip over
        // it are upright and the clips square-cornered, so the vertex shader cuts the quad to them exactly and no pixel
        // need test them; or some to test a pixel. A node its tree found scrolled out from under a clip over it (what it
        // paints wholly outside the clip: UITree.IsOffscreen) is not drawn at all. Done as a slot is copied, so a build
        // classifies only what changed.
        private static void ClassifySlot(int i, bool offscreen)
        {
            byte was = s_slotClipState[i];
            ref var slot = ref s_slots[i];
            var chain = slot.Clips;
            byte state;
            if (chain.x < 0f)
            {
                state = ClipsNone;
            }
            else if (offscreen)
            {
                state = ClipsOutside;
            }
            else
            {
                bool exact = Upright(slot.Row0, slot.Row1);
                for (int c = 0; c < 4 && exact; c++)
                {
                    if (chain[c] < 0f) break;
                    ref var clip = ref s_clips[(int)chain[c]];
                    exact = Upright(clip.Inverse0, clip.Inverse1) && clip.Radii == Vector4.zero;
                }
                state = exact ? ClipsExact : ClipsTested;
            }
            if (was == ClipsOutside) s_offscreenSlots--;
            if (state == ClipsOutside) s_offscreenSlots++;
            s_slotClipState[i] = state;
        }

        // Whether a 2x3 transform's rows (xy the matrix, z the offset) keep a rect upright, as the shader decides it.
        private static bool Upright(Vector4 row0, Vector4 row1) =>
            Mathf.Abs(row0.y) < 1e-6f && Mathf.Abs(row1.x) < 1e-6f && Mathf.Abs(row0.x) > 1e-6f && Mathf.Abs(row1.y) > 1e-6f;

        private static void EnsureSlotStates(int count)
        {
            if (s_slotClipState.Length >= count) return;
            int from = s_slotClipState.Length;
            System.Array.Resize(ref s_slotClipState, Mathf.NextPowerOfTwo(count));
            for (int i = from; i < s_slotClipState.Length; i++)
                s_slotClipState[i] = ClipsNone;
        }

        // ── Layers ───────────────────────────────────────────────────────────────

        // Per node of a tree, the box round its subtree's surfaces in its root's space: what it paints but its shadows, with
        // no room for the smoothed edge, and its children's subtrees' but those drawn elsewhere (presented, or lifted into
        // the top layer, each walked on its own). What the sibling rule tests: a sibling's shadow reaching onto another
        // does not force them apart (a layer's shadows all lie under its surfaces), and edges that merely touch do not
        // either. Nothing for a node not shown. Walked from the last node up, so each child's is known before its parent's.
        private static void SubtreeBounds(UITree tree)
        {
            int count = tree.Nodes.Count;
            if (s_subtreeBounds.Length < count)
                System.Array.Resize(ref s_subtreeBounds, Mathf.NextPowerOfTwo(count));
            for (int i = count - 1; i >= 0; i--)
            {
                var node = tree.Nodes[i];
                if (node == null || !tree.Shown[i] || tree.Opacities[i] <= 0f)
                {
                    s_subtreeBounds[i] = Nowhere;
                    continue;
                }
                var bounds = SurfaceBounds(tree, node, i);
                int end = tree.SubtreeEnd[i];
                for (int c = i + 1; c < end; c = tree.SubtreeEnd[c])
                {
                    var child = tree.Nodes[c];
                    if (child == null || child.PresentationLevel.Peek != 0 || child.TopLayer.Peek) continue;
                    var cb = s_subtreeBounds[c];
                    if (cb.xMin <= cb.xMax)
                        bounds = Union(bounds, cb);
                }
                s_subtreeBounds[i] = bounds;
            }
        }

        // The box round what a node itself paints but its shadows, in its root's space, as the shader places it.
        private static Rect SurfaceBounds(UITree tree, UINode node, int i)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var world = tree.World[i];
            var size = (Vector2)tree.Slots[i].SizeOpacity;
            IncludeSurfaces(node.Instances, world, size, ref min, ref max);
            IncludeSurfaces(node.OverlayInstances, world, size, ref min, ref max);
            return min.x > max.x ? Nowhere : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void IncludeSurfaces(List<PrimitiveInstance> instances, Affine2 world, Vector2 size, ref Vector2 min, ref Vector2 max)
        {
            for (int k = 0; k < instances.Count; k++)
                if (instances[k].Type != (uint)PrimitiveType.Shadow)
                    Include(instances[k], world, size, ref min, ref max);
        }

        // Adds a node's primitives, then its children's in ZIndex order (siblings with equal ZIndex in hierarchy order);
        // a node in the top layer is put off until the end of its level, and a presented node to its own level, unless
        // it is the one being drawn there now. The node's primitives go in `layer` (or above it, where they overlap an
        // earlier one of its own: EmitNode); each child's in the layer above the node's surfaces, or in the node's own
        // layer when it paints no surface (a layout container, or a shadow alone, which draws first in any case), and
        // past any earlier sibling's subtree it overlaps; its overlay primitives above everything inside it. Returns the
        // highest layer given out in its subtree, -1 for nothing drawn.
        private static int Walk(UITree tree, UINode node, int slotBase, bool lifted, UINode level, int layer)
        {
            if (node.Index < 0 || node.Tree != tree) return -1;
            if (node != level && node.PresentationLevel.Peek != 0) return -1;
            // Inside a group it is drawn in place: a group is one image.
            if (!lifted && s_groupRoot == null && node.TopLayer.Peek)
            {
                s_topLayer.Add(node);
                return -1;
            }
            // Hidden, or drawn at no opacity: nothing in it is seen.
            if (!tree.Shown[node.Index] || tree.Opacities[node.Index] <= 0f) return -1;

            // Flying to its place: drawn in its flight, above everything at its level, while it is hit where it is laid
            // out. Taken over by a node matched to it: its picture flies instead.
            var flying = node.FlightIn;
            if (flying != null && flying.InBuild)
            {
                if (s_groupRoot == null)
                    AddDrawOrder(tree, node);
                return -1;
            }
            if (node.FlightOut != null) return -1;

            // A group: a quad over the root draws its image here; what is in it is walked into a range of its own
            // later. It still takes its place in drawing order for hit testing.
            if (node != s_groupRoot && tree.GroupOpacities[node.Index] > 0f)
            {
                if (s_groupRoot == null)
                    AddDrawOrder(tree, node);
                EmitGroupQuad(tree, node, slotBase, layer);
                return layer;
            }

            if (s_groupRoot == null)
                tree.DrawOrder.Add(node.Index);
            uint slot = (uint)(slotBase + node.Index);
            // Where its primitives go, so a drawing of as many is patched there.
            node.EmittedBuild = s_builds;
            node.EmittedInGroup = s_groupRoot != null;
            node.EmittedAt = s_instanceCount;
            node.EmittedCount = node.Instances.Count;
            int top = EmitNode(tree, node, node.Instances, node.InstanceTextures, slot, layer, out bool surfaces);
            // What is inside it lies above its surfaces; above nothing, if it has none.
            int inside = surfaces ? top + 1 : layer;

            int first = s_children.Count;
            int end = tree.SubtreeEnd[node.Index];
            for (int i = node.Index + 1; i < end; i = tree.SubtreeEnd[i])
            {
                s_children.Add(tree.Nodes[i]);
                s_childTops.Add(-1);
            }
            int count = s_children.Count - first;
            if (count > 1)
                SortByZIndex(first, count);
            for (int i = 0; i < count; i++)
            {
                var child = s_children[first + i];
                // Above its parent's surfaces, and past any earlier sibling's subtree it overlaps.
                int childLayer = inside;
                if (child.Index >= 0 && child.Index < s_subtreeBounds.Length)
                {
                    var bounds = s_subtreeBounds[child.Index];
                    for (int e = 0; e < i; e++)
                    {
                        int earlierTop = s_childTops[first + e];
                        var earlier = s_children[first + e];
                        if (earlierTop >= childLayer && earlier.Index >= 0 && Overlaps(bounds, s_subtreeBounds[earlier.Index]))
                            childLayer = earlierTop + 1;
                    }
                }
                int childTop = Walk(tree, child, slotBase, false, level, childLayer);
                s_childTops[first + i] = childTop;
                top = Mathf.Max(top, childTop);
            }
            s_children.RemoveRange(first, count);
            s_childTops.RemoveRange(first, count);
            node.EmittedOverlayAt = s_instanceCount;
            node.EmittedOverlayCount = node.OverlayInstances.Count;
            // Over everything inside it.
            top = Mathf.Max(top, EmitNode(tree, node, node.OverlayInstances, node.OverlayTextures, slot, Mathf.Max(layer, top + 1), out _));
            return top;
        }

        // A node's primitives, from `layer` up: one goes past an earlier one of the node it overlaps whose kind would
        // otherwise draw after it (a shape over an image), and a shape past any earlier shape it overlaps (a layer's
        // shapes draw their corners then their bands). Each into its layer's bucket for its kind. Returns the highest
        // layer given out, -1 for none; and whether any of them is a surface (anything but a shadow, with any alpha).
        private static int EmitNode(UITree tree, UINode node, List<PrimitiveInstance> instances, List<Texture> textures, uint slot, int layer, out bool surfaces)
        {
            surfaces = false;
            int count = instances.Count;
            if (count == 0) return -1;
            EnsureInstances(s_instanceCount + count);
            var world = tree.World[node.Index];
            var size = (Vector2)tree.Slots[node.Index].SizeOpacity;
            for (int r = 0; r < Ranks; r++)
                s_rankSeen[r] = false;
            s_nodeShapes.Clear();
            s_nodeShapeLayers.Clear();
            int top = layer;
            for (int i = 0; i < count; i++)
            {
                var instance = instances[i];
                instance.Slot = slot;
                byte kind = KindOf(instance);
                int rank = RankOf(kind);
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                Include(instance, world, size, ref min, ref max);
                var bounds = Rect.MinMaxRect(min.x - 2f, min.y - 2f, max.x + 2f, max.y + 2f);
                int own = layer;
                for (int r = rank + 1; r < Ranks; r++)
                    if (s_rankSeen[r] && s_rankLayer[r] >= own && Overlaps(bounds, s_rankBounds[r]))
                        own = s_rankLayer[r] + 1;
                if ((kind & KindMask) == (byte)PrimitiveKind.Shape)
                {
                    for (int s = 0; s < s_nodeShapes.Count; s++)
                        if (s_nodeShapeLayers[s] >= own && Overlaps(bounds, s_nodeShapes[s]))
                            own = s_nodeShapeLayers[s] + 1;
                    s_nodeShapes.Add(bounds);
                    s_nodeShapeLayers.Add(own);
                }
                if (s_rankSeen[rank])
                {
                    s_rankBounds[rank] = Union(s_rankBounds[rank], bounds);
                    s_rankLayer[rank] = Mathf.Max(s_rankLayer[rank], own);
                }
                else
                {
                    s_rankSeen[rank] = true;
                    s_rankBounds[rank] = bounds;
                    s_rankLayer[rank] = own;
                }
                Put(instance, kind, own, textures[i], null);
                top = Mathf.Max(top, own);
                // A surface: anything but a shadow that shows at all (a fill at no alpha, a hit area, is none; its second
                // colour shows only as a gradient or a border).
                bool second = (instance.Flags & (uint)PrimitiveFlags.Gradient) != 0 || (instance.Type == (uint)PrimitiveType.Shape && instance.Params.x > 0f);
                surfaces |= rank != 0 && (instance.Color.w > 0f || (second && instance.Color2.w > 0f));
            }
            return top;
        }

        // Records a primitive, in its layer's bucket for its kind.
        private static void Put(in PrimitiveInstance instance, byte kind, int layer, Texture texture, Texture second)
        {
            EnsureInstances(s_instanceCount + 1);
            s_instances[s_instanceCount] = instance;
            s_kinds[s_instanceCount] = kind;
            s_layers[s_instanceCount] = layer;
            s_textures[s_instanceCount] = texture;
            s_textures2[s_instanceCount] = second;
            Bucket(layer, RankOf(kind)).Add(s_instanceCount);
            s_instanceCount++;
            s_layerTop = Mathf.Max(s_layerTop, layer);
        }

        // A primitive drawn above everything in the range so far, in a layer of its own.
        private static void PutOnTop(in PrimitiveInstance instance, Texture texture, Texture second)
        {
            Put(instance, KindOf(instance), s_layerTop + 1, texture, second);
        }

        // The bucket for a layer and kind rank, made the first time it is used (a flight's end gives every primitive a
        // layer of its own, so most of a layer's buckets are never touched).
        private static List<int> Bucket(int layer, int rank)
        {
            int at = layer * Ranks + rank;
            while (s_buckets.Count <= at)
                s_buckets.Add(null);
            s_bucketLayers = Mathf.Max(s_bucketLayers, layer + 1);
            return s_buckets[at] ??= new List<int>();
        }

        // Begins a range of the order (a tree's main pass, a group's image): its buckets empty, its layers from 0.
        private static void BeginRange()
        {
            for (int i = 0; i < s_bucketLayers * Ranks && i < s_buckets.Count; i++)
                s_buckets[i]?.Clear();
            s_bucketLayers = 0;
            s_layerTop = -1;
        }

        // Ends a range: the buckets laid end to end, layer by layer and kind by kind, as the order of the primitives
        // recorded since `start` (every one of them is in exactly one bucket); then cut into batches by texture, and
        // into segments by node.
        private static void EndRange(List<Batch> batches, UITree tree, int start)
        {
            int end = s_instanceCount;
            if (s_order.Length < end)
                System.Array.Resize(ref s_order, Mathf.NextPowerOfTwo(end));
            int at = start;
            for (int b = 0; b < s_bucketLayers * Ranks && b < s_buckets.Count; b++)
            {
                var bucket = s_buckets[b];
                if (bucket == null) continue;
                for (int i = 0; i < bucket.Count; i++)
                    s_order[at++] = bucket[i];
            }
            int firstBatch = batches.Count;
            MakeBatches(batches, tree, start, end);
            for (int b = firstBatch; b < batches.Count; b++)
            {
                var batch = batches[b];
                batch.FirstSegment = s_segments.Count;
                MakeSegments(batch.Start, batch.Start + batch.Count);
                batch.SegmentCount = s_segments.Count - batch.FirstSegment;
                batches[b] = batch;
            }
        }

        // The segments of a range of the order: each a node's primitives of one kind in one layer, in a row.
        private static void MakeSegments(int start, int end)
        {
            int segmentStart = start;
            uint slot = 0;
            byte kind = 0;
            int layer = -1;
            for (int i = start; i < end; i++)
            {
                int id = s_order[i];
                uint s = s_instances[id].Slot;
                byte k = s_kinds[id];
                int l = s_layers[id];
                if (i > start && s == slot && k == kind && l == layer) continue;
                if (i > segmentStart)
                    s_segments.Add(new Segment { Start = segmentStart, Count = i - segmentStart, Layer = layer, Slot = slot, Kind = kind });
                segmentStart = i;
                slot = s;
                kind = k;
                layer = l;
            }
            if (end > segmentStart)
                s_segments.Add(new Segment { Start = segmentStart, Count = end - segmentStart, Layer = layer, Slot = slot, Kind = kind });
        }

        // ── Batches ──────────────────────────────────────────────────────────────

        // Runs of the order drawn in one call, each with up to eight textures that its primitives say which of they
        // sample: a batch ends only when a ninth texture turns up. A primitive that samples nothing (a shape, a shadow)
        // goes with any batch; one that samples two (a matched pair) has both in its batch, the second's index in the
        // second four bits of its texture index.
        private static void MakeBatches(List<Batch> batches, UITree tree, int start, int end)
        {
            using var batch = UIProfiling.Batch.Auto();
            int batchStart = start;
            int first = s_batchTextures.Count;
            int used = 0;
            // The last texture looked up, and where it is in the batch: a label's glyphs all sample one.
            Texture last = null;
            int lastIndex = 0;
            for (int i = start; i < end; i++)
            {
                int id = s_order[i];
                var texture = s_textures[id];
                var second = s_textures2[id];
                s_textureBase[id] = first;
                if (texture is null && second is null)
                {
                    s_instances[id].Texture = 0;
                    continue;
                }
                if (second is null && ReferenceEquals(texture, last))
                {
                    s_instances[id].Texture = (uint)lastIndex;
                    continue;
                }
                // Room for what it samples that the batch does not have yet.
                int at = texture is null ? -1 : FindBatchTexture(texture, first, used);
                int at2 = second is null || ReferenceEquals(second, texture) ? -1 : FindBatchTexture(second, first, used);
                int needed = (texture is not null && at < 0 ? 1 : 0) + (second is not null && !ReferenceEquals(second, texture) && at2 < 0 ? 1 : 0);
                if (used + needed > TexturesPerBatch)
                {
                    CloseBatch(batches, tree, batchStart, i, first);
                    batchStart = i;
                    first = s_batchTextures.Count;
                    used = 0;
                    at = at2 = -1;
                    last = null;
                    s_textureBase[id] = first;
                }
                int index = 0;
                if (texture is not null)
                {
                    if (at < 0)
                    {
                        s_batchTextures.Add(texture);
                        at = used++;
                    }
                    index = at;
                }
                if (second is not null)
                {
                    if (ReferenceEquals(second, texture))
                    {
                        at2 = at;
                    }
                    else if (at2 < 0)
                    {
                        s_batchTextures.Add(second);
                        at2 = used++;
                    }
                    index |= at2 << 4;
                }
                s_instances[id].Texture = (uint)index;
                if (second is null)
                {
                    last = texture;
                    lastIndex = index;
                }
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

        // ── Runs ─────────────────────────────────────────────────────────────────

        // Each batch's runs, from its segments: those in a row drawn with one pass, a kind's plain pass where no pixel of
        // theirs need test a clip, its clipped pass otherwise, and never across a layer (a shape run draws its corners
        // then its bands, which only a layer makes safe); in the overdraw view, every primitive but a debug view's own is
        // drawn as heat. And its spans: the shapes in a row that are drawn at all, whose occluders are drawn. A node
        // scrolled out from under its clips is left out of both.
        private static void MakeRuns()
        {
            using var runs = UIProfiling.Batch.Auto();
            s_runs.Clear();
            s_spans.Clear();
            s_shapeRuns = 0;
            MakeRuns(Batches);
            s_mainRuns = s_runs.Count;
            MakeRuns(GroupBatches);
        }

        private static void MakeRuns(List<Batch> batches)
        {
            bool overdraw = UIDebug.Overdraw;
            for (int b = 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                batch.FirstRun = s_runs.Count;
                batch.FirstSpan = s_spans.Count;
                int runStart = 0, runCount = 0, runPass = -1, runLayer = -1;
                int spanStart = 0, spanCount = 0;
                for (int s = batch.FirstSegment; s < batch.FirstSegment + batch.SegmentCount; s++)
                {
                    var segment = s_segments[s];
                    byte clips = s_slotClipState[(int)segment.Slot];
                    bool shape = (segment.Kind & KindMask) == (byte)PrimitiveKind.Shape;
                    if (clips == ClipsOutside)
                    {
                        // Not drawn: what came before it ends here.
                        if (runCount > 0) AddRun(runStart, runCount, runPass);
                        runCount = 0;
                        runPass = -1;
                        if (spanCount > 0) s_spans.Add(new Run { Start = spanStart, Count = spanCount });
                        spanCount = 0;
                        continue;
                    }
                    int pass;
                    if (overdraw && (segment.Kind & KindDebug) == 0)
                    {
                        pass = OverdrawPass;
                    }
                    else
                    {
                        bool tested = clips == ClipsTested || (clips == ClipsExact && (segment.Kind & KindSkewed) != 0);
                        pass = (segment.Kind & KindMask) * 2 + (tested ? 1 : 0);
                    }
                    if (runCount > 0 && pass == runPass && segment.Layer == runLayer && segment.Start == runStart + runCount)
                    {
                        runCount += segment.Count;
                    }
                    else
                    {
                        if (runCount > 0) AddRun(runStart, runCount, runPass);
                        runStart = segment.Start;
                        runCount = segment.Count;
                        runPass = pass;
                        runLayer = segment.Layer;
                    }
                    if (shape && spanCount > 0 && segment.Start == spanStart + spanCount)
                    {
                        spanCount += segment.Count;
                    }
                    else if (shape)
                    {
                        if (spanCount > 0) s_spans.Add(new Run { Start = spanStart, Count = spanCount });
                        spanStart = segment.Start;
                        spanCount = segment.Count;
                    }
                }
                if (runCount > 0) AddRun(runStart, runCount, runPass);
                if (spanCount > 0) s_spans.Add(new Run { Start = spanStart, Count = spanCount });
                batch.RunCount = s_runs.Count - batch.FirstRun;
                batch.SpanCount = s_spans.Count - batch.FirstSpan;
                batches[b] = batch;
            }
        }

        // The pass that draws a run's bands after its corners, for a shape's or an image's run; -1 for the rest.
        private static int BandPassOf(int pass) =>
            pass < 2 ? FlatPass + pass : pass == (int)PrimitiveKind.Image * 2 || pass == (int)PrimitiveKind.Image * 2 + 1 ? ImageFlatPass + pass - (int)PrimitiveKind.Image * 2 : -1;

        private static void AddRun(int start, int count, int pass)
        {
            s_runs.Add(new Run { Start = start, Count = count, Pass = pass });
            if (BandPassOf(pass) >= 0) s_shapeRuns++;
        }
    }
}
