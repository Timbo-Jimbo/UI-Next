using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Finds, among copies of a thing named alike (<see cref="UINode.SharedDrawing"/>), what they draw the same. A copy
    /// is seen as the primitives its subtree drew in the compositor's walk, each with the layer it was drawn in counting
    /// from the copy's own, and a key: every field of the primitive, its texture, its layer, and where its node sits in
    /// the copy (frames, offsets, scales, turns, sizes, opacities, colour filters, clips and scrolling, all from each
    /// node's own inputs down from the copy, so the same wherever the copy is). A named node inside a copy is a copy of
    /// its own: what it draws is not the outer copy's to share. Copies of a name that draw the same bottom layer, at the
    /// same scale and turn, are one variant (a list's yellow cards; the purple ones are another). A variant with enough
    /// copies shares an image holding every primitive all its copies have, as deep as they go before they differ, but any
    /// that lies on a primitive that differs in some copy (a primitive on an icon that differs is drawn after it, so cannot
    /// be in an image drawn before it): the rest of each copy is drawn live over its image.
    /// </summary>
    internal static class SharedCopies
    {
        /// <summary>
        /// A copy in a variant: its node, its variant (counting from 0 each find), the variant's hash, how many primitives
        /// its image holds, and which of the copy's primitives they are (ImageCount of them from ImageFirst in the image
        /// list).
        /// </summary>
        public struct Copy
        {
            public int Node;
            public int Variant;
            public ulong Hash;
            public int Primitives;
            public int ImageFirst;
            public int ImageCount;
        }

        /// <summary>A primitive of a copy that its variant's image holds: its node, and its index in the node's primitives, those under its children then those over them.</summary>
        public struct ImagePrimitive
        {
            public int Node;
            public int Index;
        }

        // A primitive of a copy: its key, its layer counting from the copy's, its bounds in the root's space (with room for
        // its smoothed edge, as the compositor's layers test overlaps), its node and index there, whether it is the copy's
        // own (not a named node's inside it), and whether its variant's image holds it.
        private struct Primitive
        {
            public ulong Key;
            public int Layer;
            public Rect Bounds;
            public int Node;
            public int Index;
            public bool Own;
            public bool Shared;
        }

        // Per node: whether everything its subtree draws (but presented nodes, drawn at levels of their own) was drawn by
        // the walk into layers it can be told by, so it can be shared (a group's quad, a flight or a lifted node inside it
        // cannot); and, while one copy is gathered, where it sits in the copy.
        private static bool[] s_whole = new bool[64];
        private static ulong[] s_path = new ulong[64];
        // Per copy: its primitives (s_primitives from s_first[i], s_count[i] of them, by layer).
        private static int[] s_first = new int[64];
        private static int[] s_count = new int[64];
        private static readonly List<Primitive> s_primitives = new();
        // The copies, sorted by their variant's hash.
        private static int[] s_copies = new int[64];
        private static ulong[] s_variants = new ulong[64];
        // How many of each key a variant's image holds; how many are left while one copy's are marked; one copy's keys.
        private static readonly Dictionary<ulong, int> s_quota = new();
        private static readonly Dictionary<ulong, int> s_left = new();
        private static readonly Dictionary<ulong, int> s_own = new();
        private static readonly List<ulong> s_quotaKeys = new();

        private static readonly IComparer<Primitive> ByLayer = Comparer<Primitive>.Create((a, b) => a.Layer.CompareTo(b.Layer));

        /// <summary>
        /// Every copy of every variant in <paramref name="tree"/> with at least <paramref name="minCopies"/> copies, and
        /// the primitives each copy's image holds, from what the compositor's last walk (build <paramref name="build"/>)
        /// drew: each node's primitives from its EmittedAt, in the layers <paramref name="layers"/> holds for them,
        /// counted from its EmittedLayer. Fills <paramref name="owner"/> with each node's nearest named node, itself
        /// included (-1 for none, and for what is presented apart from it): the copy whose drawing it is part of.
        /// </summary>
        public static void Find(UITree tree, int build, int[] layers, int minCopies, List<Copy> copies, List<ImagePrimitive> image, int[] owner)
        {
            copies.Clear();
            image.Clear();
            minCopies = Mathf.Max(2, minCopies);
            int count = tree.Nodes.Count;
            if (s_whole.Length < count)
            {
                int size = Mathf.NextPowerOfTwo(count);
                s_whole = new bool[size];
                s_path = new ulong[size];
                s_first = new int[size];
                s_count = new int[size];
                s_copies = new int[size];
                s_variants = new ulong[size];
            }

            // Each node's nearest named node; parents come before their children.
            for (int i = 0; i < count; i++)
            {
                var node = tree.Nodes[i];
                if (!string.IsNullOrEmpty(node.SharedDrawing.Peek))
                    owner[i] = i;
                else if (i == 0 || node.Composited.Presented || node.ParentNode == null)
                    owner[i] = -1;
                else
                    owner[i] = owner[node.ParentNode.Index];
            }

            // Children come after their parent, so backwards each child is settled first. A node lifted into the top layer
            // is drawn apart in the walk but in place in an image, so what holds one cannot be shared.
            for (int i = count - 1; i >= 0; i--)
            {
                if (!Seen(tree, i))
                {
                    s_whole[i] = true;
                    continue;
                }
                bool whole = tree.Nodes[i].EmittedBuild == build;
                int end = tree.SubtreeEnd[i];
                for (int c = i + 1; c < end && whole; c = tree.SubtreeEnd[c])
                {
                    var child = tree.Nodes[c];
                    whole = child.Composited.Presented || (!child.Composited.Lifted && s_whole[c]);
                }
                s_whole[i] = whole;
            }

            s_primitives.Clear();
            int found = 0;
            for (int i = 0; i < count; i++)
            {
                if (owner[i] != i || !s_whole[i] || !Seen(tree, i)) continue;
                var slot = tree.Slots[i];
                if (slot.Row0.w != 1f || slot.Row1.w != 1f) continue;
                if (!Gather(tree, i, layers, owner, out s_variants[found])) continue;
                s_copies[found++] = i;
            }

            Array.Sort(s_variants, s_copies, 0, found);
            int variants = 0;
            for (int c = 0; c < found;)
            {
                int run = c + 1;
                while (run < found && s_variants[run] == s_variants[c])
                    run++;
                int shared = run - c >= minCopies ? Share(c, run) : 0;
                if (shared > 0)
                {
                    for (int m = c; m < run; m++)
                    {
                        int node = s_copies[m];
                        int imageFirst = image.Count;
                        for (int p = s_first[node]; p < s_first[node] + s_count[node]; p++)
                            if (s_primitives[p].Shared)
                                image.Add(new ImagePrimitive { Node = s_primitives[p].Node, Index = s_primitives[p].Index });
                        copies.Add(new Copy
                        {
                            Node = node, Variant = variants, Hash = s_variants[c], Primitives = shared,
                            ImageFirst = imageFirst, ImageCount = image.Count - imageFirst,
                        });
                    }
                    variants++;
                }
                c = run;
            }
        }

        /// <summary>The colour a variant is shown in by the debug view: the same for the same hash.</summary>
        public static Color ColourOf(ulong hash) => Color.HSVToRGB((hash >> 40 & 0xFFFF) / 65535f, 0.75f, 1f);

        // Whether node `i` is drawn at all: shown, at some opacity.
        private static bool Seen(UITree tree, int i) => tree.Shown[i] && tree.Opacities[i] > 0f;

        // The image of copies from..to of s_copies: the keys every copy has of its own (as many of each as the fewest),
        // less any a copy draws on a primitive the image does not hold, until none is. Marks each copy's primitives the
        // image holds and returns how many that is a copy.
        private static int Share(int from, int to)
        {
            s_quota.Clear();
            CountKeys(s_copies[from], s_quota);
            for (int c = from + 1; c < to; c++)
            {
                CountKeys(s_copies[c], s_own);
                s_quotaKeys.Clear();
                s_quotaKeys.AddRange(s_quota.Keys);
                foreach (var key in s_quotaKeys)
                    s_quota[key] = Mathf.Min(s_quota[key], s_own.TryGetValue(key, out int own) ? own : 0);
            }
            bool removed = true;
            while (removed)
            {
                removed = false;
                for (int c = from; c < to; c++)
                    removed |= Mark(s_copies[c]);
            }
            int shared = 0;
            foreach (var quota in s_quota.Values)
                shared += quota;
            return shared;
        }

        // How many of each key a copy has of its own.
        private static void CountKeys(int copy, Dictionary<ulong, int> into)
        {
            into.Clear();
            for (int p = s_first[copy]; p < s_first[copy] + s_count[copy]; p++)
            {
                if (!s_primitives[p].Own) continue;
                into.TryGetValue(s_primitives[p].Key, out int seen);
                into[s_primitives[p].Key] = seen + 1;
            }
        }

        // Marks which of a copy's primitives the image holds, bottom layer first: its own with a key the image still has
        // room for, but not one that lies on a primitive the image does not hold. A key found lying on one is taken out of
        // the image. Returns whether any was.
        private static bool Mark(int copy)
        {
            s_left.Clear();
            foreach (var pair in s_quota)
                s_left[pair.Key] = pair.Value;
            bool removed = false;
            int first = s_first[copy], end = first + s_count[copy];
            for (int p = first; p < end; p++)
            {
                var primitive = s_primitives[p];
                int left = 0;
                primitive.Shared = primitive.Own && s_left.TryGetValue(primitive.Key, out left) && left > 0;
                if (primitive.Shared)
                {
                    for (int q = first; q < end && s_primitives[q].Layer < primitive.Layer; q++)
                    {
                        if (s_primitives[q].Shared || !Compositor.Overlaps(s_primitives[q].Bounds, primitive.Bounds)) continue;
                        primitive.Shared = false;
                        s_quota[primitive.Key] = 0;
                        removed = true;
                        break;
                    }
                }
                if (primitive.Shared)
                    s_left[primitive.Key] = left - 1;
                s_primitives[p] = primitive;
            }
            return removed;
        }

        // Gathers copy `i`'s primitives, by layer: each node of its subtree (but what is drawn apart) where it sits in the
        // copy, each of its primitives with the layer it was drawn in, counted from the copy's; a named node's inside it
        // keyed as its alone, so never shared. And its variant: its name, the scale and turn it is drawn at, and the
        // bottom layer of what it draws of its own. Returns whether it draws anything of its own.
        private static bool Gather(UITree tree, int i, int[] layers, int[] owner, out ulong variant)
        {
            variant = 0;
            int first = s_primitives.Count;
            int based = tree.Nodes[i].EmittedLayer;
            int end = tree.SubtreeEnd[i];
            for (int j = i; j < end;)
            {
                var node = tree.Nodes[j];
                if (j != i && (node.Composited.Presented || !Seen(tree, j)))
                {
                    j = tree.SubtreeEnd[j];
                    continue;
                }
                s_path[j] = j == i ? RootPath(tree, i) : PathOf(node, s_path[node.ParentNode.Index]);
                bool own = owner[j] == i;
                var world = tree.World[j];
                var size = (Vector2)tree.Slots[j].SizeOpacity;
                Add(node.Instances, node.InstanceTextures, node.EmittedAt, node.EmittedCount, 0, j, own, world, size, layers, based);
                Add(node.OverlayInstances, node.OverlayTextures, node.EmittedOverlayAt, node.EmittedOverlayCount, node.Instances.Count, j, own,
                    world, size, layers, based);
                j++;
            }
            s_first[i] = first;
            s_count[i] = s_primitives.Count - first;
            s_primitives.Sort(first, s_count[i], ByLayer);

            // The bottom layer of its own: the sum of its keys (nothing within a layer overlaps, so their order there does
            // not matter).
            int bottom = -1;
            ulong sum = 0;
            uint inBottom = 0;
            for (int p = first; p < s_primitives.Count; p++)
            {
                var primitive = s_primitives[p];
                if (!primitive.Own) continue;
                if (bottom < 0) bottom = primitive.Layer;
                if (primitive.Layer != bottom) break;
                sum += primitive.Key;
                inBottom++;
            }
            if (bottom < 0) return false;
            var hash = new Hasher();
            hash.Add((uint)tree.Nodes[i].SharedDrawing.Peek.GetHashCode());
            var placed = tree.World[i];
            hash.Add(placed.A);
            hash.Add(placed.B);
            hash.Add(placed.C);
            hash.Add(placed.D);
            hash.Add((uint)bottom);
            hash.Add(inBottom);
            hash.Add(sum);
            variant = hash.Value;
            return true;
        }

        // Where a copy sits in itself: its size, opacity and colour filters (its place in its parent aside).
        private static ulong RootPath(UITree tree, int i)
        {
            ref readonly var inputs = ref tree.Nodes[i].Composited;
            var hash = new Hasher();
            hash.Add(inputs.Frame.size);
            hash.Add(inputs.Opacity);
            hash.Add(inputs.Saturation);
            hash.Add(inputs.Brightness);
            return hash.Value;
        }

        // Where a node sits in a copy, from where its parent does: its parent's scrolling and clip, then its own frame,
        // offset, scale, turn, opacity and colour filters, as compositing reads them.
        private static ulong PathOf(UINode node, ulong parentPath)
        {
            ref readonly var parent = ref node.ParentNode.Composited;
            ref readonly var inputs = ref node.Composited;
            var hash = new Hasher();
            hash.Add(parentPath);
            hash.Add(parent.Scrolled);
            hash.Add(parent.Clips ? 1u : 0u);
            if (parent.Clips)
                hash.Add(parent.ClipRadii);
            hash.Add(inputs.Frame.position);
            hash.Add(inputs.Frame.size);
            hash.Add(inputs.Offset);
            hash.Add(inputs.Scale);
            hash.Add(inputs.Rotation);
            hash.Add(inputs.Opacity);
            hash.Add(inputs.Saturation);
            hash.Add(inputs.Brightness);
            return hash.Value;
        }

        // Adds a node's primitives to the copy being gathered: each keyed by every field but where the compositor puts it
        // (its slot, its batch's texture index), its texture, its layer, and where its node sits; one that is not the
        // copy's own keyed by its node too, so no other copy has it.
        private static void Add(List<PrimitiveInstance> instances, List<Texture> textures, int at, int emitted, int offset, int node, bool own,
            Affine2 world, Vector2 size, int[] layers, int based)
        {
            int count = Mathf.Min(instances.Count, emitted);
            for (int k = 0; k < count; k++)
            {
                int layer = Mathf.Max(0, layers[at + k] - based);
                var instance = instances[k];
                var hash = new Hasher();
                hash.Add(s_path[node]);
                if (!own)
                    hash.Add((uint)node);
                hash.Add((uint)layer);
                hash.Add(instance.Rect);
                hash.Add(instance.Radii);
                hash.Add(instance.Color);
                hash.Add(instance.Color2);
                hash.Add(instance.Params);
                hash.Add(instance.Uv);
                hash.Add(instance.Type);
                hash.Add(instance.Flags);
                var texture = textures[k];
                hash.Add(texture != null ? (uint)texture.GetEntityId().GetHashCode() : 0u);
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                Compositor.Include(instance, world, size, ref min, ref max);
                s_primitives.Add(new Primitive
                {
                    Key = hash.Value,
                    Layer = layer,
                    Bounds = Rect.MinMaxRect(min.x - 2f, min.y - 2f, max.x + 2f, max.y + 2f),
                    Node = node,
                    Index = offset + k,
                    Own = own,
                });
            }
        }

        // A 64-bit hash of a run of words: each mixed in by xor and multiply, the whole finished by splitmix64's finaliser.
        private struct Hasher
        {
            private ulong _state;

            public void Add(uint value)
            {
                _state = (_state ^ value) * 0x9E3779B97F4A7C15UL;
                _state ^= _state >> 32;
            }

            public void Add(ulong value)
            {
                Add((uint)value);
                Add((uint)(value >> 32));
            }

            public void Add(float value) => Add((uint)BitConverter.SingleToInt32Bits(value));

            public void Add(Vector2 value)
            {
                Add(value.x);
                Add(value.y);
            }

            public void Add(Vector4 value)
            {
                Add(value.x);
                Add(value.y);
                Add(value.z);
                Add(value.w);
            }

            public ulong Value
            {
                get
                {
                    ulong z = _state;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    z ^= z >> 31;
                    return z;
                }
            }
        }
    }
}
