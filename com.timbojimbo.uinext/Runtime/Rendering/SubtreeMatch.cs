using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Finds what copies of a thing draw the same, primitive by primitive. Every node is a candidate, seen as the
    /// primitives its subtree drew in the compositor's walk, each with the layer it was drawn in counting from the node's
    /// own, and a key: every field of the primitive, its texture, and where its node sits in the candidate (frames,
    /// offsets, scales, turns, sizes, opacities, colour filters, clips and scrolling, all from each node's own inputs down
    /// from the candidate, so the same wherever the candidate is). Candidates that draw the same bottom layer are copies of
    /// one thing (a list's yellow cards; the purple ones are another). A set's image holds every primitive all its copies
    /// have, but any that lies on one that differs in some copy (a primitive on an icon that differs is drawn after it, so
    /// cannot be in an image drawn before it): the rest of each copy is drawn live over its image. Sets then claim their
    /// copies, those saving most first; a copy inside or round one claimed already is left out.
    /// </summary>
    internal static class SubtreeMatch
    {
        /// <summary>
        /// A copy in a set: its node, its set (counting from 0 each find), the set's hash, how many primitives its image
        /// holds, and which of the copy's primitives they are (ImageCount of them from ImageFirst in the image list).
        /// </summary>
        public struct Match
        {
            public int Node;
            public int Set;
            public ulong Hash;
            public int Primitives;
            public int ImageFirst;
            public int ImageCount;
        }

        /// <summary>A primitive of a copy that its set's image holds: its node, and its index in the node's primitives, those under its children then those over them.</summary>
        public struct ImagePrimitive
        {
            public int Node;
            public int Index;
        }

        // A primitive of a candidate: its key, its layer counting from the candidate's, its bounds in the root's space (with
        // room for its smoothed edge, as the compositor's layers test overlaps), its node and index there, and whether
        // its set's image holds it.
        private struct Primitive
        {
            public ulong Key;
            public int Layer;
            public Rect Bounds;
            public int Node;
            public int Index;
            public bool Shared;
        }

        // A set as found, before it claims its copies: its candidates (s_members from First, Count of them), its hash, how
        // many primitives its image holds, and what it saves (those drawn once rather than once a copy).
        private struct Found
        {
            public int First;
            public int Count;
            public ulong Hash;
            public int Primitives;
            public long Saves;
        }

        // Per node: whether everything its subtree draws (but presented nodes, drawn at levels of their own) was drawn by
        // the walk into layers it can be told by, so it can be a candidate (a group's quad, a flight or a lifted node
        // inside it cannot); and, while one candidate is gathered, where it sits in the candidate.
        private static bool[] s_whole = new bool[64];
        private static ulong[] s_path = new ulong[64];
        // Per candidate: its primitives (s_primitives from s_first[i], s_count[i] of them, by layer), and its bottom
        // layer's hash.
        private static int[] s_first = new int[64];
        private static int[] s_count = new int[64];
        private static ulong[] s_bottom = new ulong[64];
        private static readonly List<Primitive> s_primitives = new();
        // The candidates, sorted by their bottom layer's hash; the sets found; their members.
        private static int[] s_candidates = new int[64];
        private static ulong[] s_keys = new ulong[64];
        private static readonly List<Found> s_found = new();
        private static readonly List<int> s_members = new();
        // How many of each key a set's image holds; how many are left while one copy's are marked; one copy's keys.
        private static readonly Dictionary<ulong, int> s_quota = new();
        private static readonly Dictionary<ulong, int> s_left = new();
        private static readonly Dictionary<ulong, int> s_own = new();
        private static readonly List<ulong> s_quotaKeys = new();
        // Per node: whether it is in a claimed copy, or holds one.
        private static bool[] s_claimed = new bool[64];
        private static bool[] s_holds = new bool[64];

        private static readonly IComparer<Primitive> ByLayer = Comparer<Primitive>.Create((a, b) => a.Layer.CompareTo(b.Layer));

        /// <summary>
        /// Every copy of every set in <paramref name="tree"/>, and the primitives each copy's image holds, from what the
        /// compositor's last walk (build <paramref name="build"/>) drew: each node's primitives from its EmittedAt, in the
        /// layers <paramref name="layers"/> holds for them, counted from its EmittedLayer. Within the matching settings
        /// (<see cref="UISettings"/>): copies drawn in view (or out of it too) at neutral colour filters, enough of them,
        /// their image holding enough primitives.
        /// </summary>
        public static void Find(UITree tree, int build, int[] layers, List<Match> matches, List<ImagePrimitive> image)
        {
            matches.Clear();
            image.Clear();
            var settings = UISettings.Instance;
            int minCopies = Mathf.Max(2, settings != null ? settings.MatchMinCopies : UISettings.DefaultMatchMinCopies);
            int minPrimitives = settings != null ? settings.MatchMinPrimitives : UISettings.DefaultMatchMinPrimitives;
            bool countsOffscreen = settings != null && settings.MatchCountsOffscreen;
            bool sameScale = settings != null && settings.MatchSameScale;

            int count = tree.Nodes.Count;
            if (s_whole.Length < count)
            {
                int size = Mathf.NextPowerOfTwo(count);
                s_whole = new bool[size];
                s_path = new ulong[size];
                s_first = new int[size];
                s_count = new int[size];
                s_bottom = new ulong[size];
                s_candidates = new int[size];
                s_keys = new ulong[size];
                s_claimed = new bool[size];
                s_holds = new bool[size];
            }

            // Children come after their parent, so backwards each child is settled first. A node lifted into the top layer
            // is drawn apart in the walk but in place in an image, so what holds one cannot be a candidate.
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
            int candidates = 0;
            for (int i = 0; i < count; i++)
            {
                if (!s_whole[i] || !Seen(tree, i) || (!countsOffscreen && tree.Offscreen[i])) continue;
                var slot = tree.Slots[i];
                if (slot.Row0.w != 1f || slot.Row1.w != 1f) continue;
                if (!Gather(tree, i, layers, sameScale)) continue;
                s_keys[candidates] = s_bottom[i];
                s_candidates[candidates++] = i;
            }

            // Copies of one thing draw the same bottom layer.
            s_found.Clear();
            s_members.Clear();
            Array.Sort(s_keys, s_candidates, 0, candidates);
            for (int c = 0; c < candidates;)
            {
                int run = c + 1;
                while (run < candidates && s_keys[run] == s_keys[c])
                    run++;
                if (run - c >= minCopies)
                {
                    int shared = Share(c, run);
                    if (shared >= minPrimitives)
                    {
                        s_found.Add(new Found { First = s_members.Count, Count = run - c, Hash = s_keys[c], Primitives = shared, Saves = (long)shared * (run - c - 1) });
                        for (int m = c; m < run; m++)
                            s_members.Add(s_candidates[m]);
                    }
                }
                c = run;
            }

            // Those saving most claim their copies first; between two saving as much, the one round more nodes.
            s_found.Sort((a, b) => a.Saves != b.Saves ? b.Saves.CompareTo(a.Saves)
                : (tree.SubtreeEnd[s_members[b.First]] - s_members[b.First]).CompareTo(tree.SubtreeEnd[s_members[a.First]] - s_members[a.First]));
            Array.Clear(s_claimed, 0, count);
            Array.Clear(s_holds, 0, count);
            int sets = 0;
            foreach (var found in s_found)
            {
                int free = 0;
                for (int m = found.First; m < found.First + found.Count; m++)
                    if (!s_claimed[s_members[m]] && !s_holds[s_members[m]])
                        free++;
                if (free < minCopies) continue;
                for (int m = found.First; m < found.First + found.Count; m++)
                {
                    int node = s_members[m];
                    if (s_claimed[node] || s_holds[node]) continue;
                    for (int j = node; j < tree.SubtreeEnd[node]; j++)
                        s_claimed[j] = true;
                    for (var up = tree.Nodes[node].ParentNode; up != null && up.Tree == tree && up.Index >= 0; up = up.ParentNode)
                        s_holds[up.Index] = true;
                    int imageFirst = image.Count;
                    for (int p = s_first[node]; p < s_first[node] + s_count[node]; p++)
                        if (s_primitives[p].Shared)
                            image.Add(new ImagePrimitive { Node = s_primitives[p].Node, Index = s_primitives[p].Index });
                    matches.Add(new Match
                    {
                        Node = node, Set = sets, Hash = found.Hash, Primitives = found.Primitives,
                        ImageFirst = imageFirst, ImageCount = image.Count - imageFirst,
                    });
                }
                sets++;
            }
        }

        /// <summary>The colour a set is shown in by the debug view: the same for the same hash.</summary>
        public static Color ColourOf(ulong hash) => Color.HSVToRGB((hash >> 40 & 0xFFFF) / 65535f, 0.75f, 1f);

        // Whether node `i` is drawn at all: shown, at some opacity.
        private static bool Seen(UITree tree, int i) => tree.Shown[i] && tree.Opacities[i] > 0f;

        // The image of copies from..to of s_candidates: the keys every copy has (as many of each as the fewest), less any
        // a copy draws on a primitive the image does not hold, until none is. Marks each copy's primitives the image holds
        // and returns how many that is a copy.
        private static int Share(int from, int to)
        {
            s_quota.Clear();
            CountKeys(s_candidates[from], s_quota);
            for (int c = from + 1; c < to; c++)
            {
                CountKeys(s_candidates[c], s_own);
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
                    removed |= Mark(s_candidates[c]);
            }
            int shared = 0;
            foreach (var quota in s_quota.Values)
                shared += quota;
            return shared;
        }

        private static void CountKeys(int candidate, Dictionary<ulong, int> into)
        {
            into.Clear();
            for (int p = s_first[candidate]; p < s_first[candidate] + s_count[candidate]; p++)
            {
                into.TryGetValue(s_primitives[p].Key, out int seen);
                into[s_primitives[p].Key] = seen + 1;
            }
        }

        // Marks which of a candidate's primitives the image holds, bottom layer first: those with a key the image still
        // has room for, but not one that lies on a primitive it does not hold. A key found lying on one is taken out of
        // the image. Returns whether any was.
        private static bool Mark(int candidate)
        {
            s_left.Clear();
            foreach (var pair in s_quota)
                s_left[pair.Key] = pair.Value;
            bool removed = false;
            int first = s_first[candidate], end = first + s_count[candidate];
            for (int p = first; p < end; p++)
            {
                var primitive = s_primitives[p];
                primitive.Shared = s_left.TryGetValue(primitive.Key, out int left) && left > 0;
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

        // Gathers candidate `i`'s primitives, by layer: each node of its subtree (but what is drawn apart) where it sits in
        // the candidate, each of its primitives with the layer it was drawn in, counted from the candidate's. And its
        // bottom layer's hash. Returns whether it drew anything.
        private static bool Gather(UITree tree, int i, int[] layers, bool sameScale)
        {
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
                s_path[j] = j == i ? RootPath(tree, i, sameScale) : PathOf(node, s_path[node.ParentNode.Index]);
                var world = tree.World[j];
                var size = (Vector2)tree.Slots[j].SizeOpacity;
                Add(node.Instances, node.InstanceTextures, node.EmittedAt, node.EmittedCount, 0, j, world, size, layers, based);
                Add(node.OverlayInstances, node.OverlayTextures, node.EmittedOverlayAt, node.EmittedOverlayCount, node.Instances.Count, j, world, size, layers, based);
                j++;
            }
            s_first[i] = first;
            s_count[i] = s_primitives.Count - first;
            if (s_count[i] == 0) return false;
            s_primitives.Sort(first, s_count[i], ByLayer);
            // The bottom layer: the sum of its keys (nothing within a layer overlaps, so their order there does not matter).
            ulong sum = 0;
            uint bottom = 0;
            for (int p = first; p < s_primitives.Count && s_primitives[p].Layer == s_primitives[first].Layer; p++)
            {
                sum += s_primitives[p].Key;
                bottom++;
            }
            var hash = new Hasher();
            hash.Add(bottom);
            hash.Add(sum);
            s_bottom[i] = hash.Value;
            return true;
        }

        // Where a candidate sits in itself: its size, opacity and colour filters (its place in its parent aside), and the
        // scale and turn it is drawn at when copies must share them.
        private static ulong RootPath(UITree tree, int i, bool sameScale)
        {
            ref readonly var inputs = ref tree.Nodes[i].Composited;
            var hash = new Hasher();
            hash.Add(inputs.Frame.size);
            hash.Add(inputs.Opacity);
            hash.Add(inputs.Saturation);
            hash.Add(inputs.Brightness);
            if (sameScale)
            {
                var world = tree.World[i];
                hash.Add(world.A);
                hash.Add(world.B);
                hash.Add(world.C);
                hash.Add(world.D);
            }
            return hash.Value;
        }

        // Where a node sits in a candidate, from where its parent does: its parent's scrolling and clip, then its own frame,
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

        // Adds a node's primitives to the candidate being gathered: each keyed by every field but where the compositor puts
        // it (its slot, its batch's texture index), its texture, its layer, and where its node sits.
        private static void Add(List<PrimitiveInstance> instances, List<Texture> textures, int at, int emitted, int offset, int node,
            Affine2 world, Vector2 size, int[] layers, int based)
        {
            int count = Mathf.Min(instances.Count, emitted);
            for (int k = 0; k < count; k++)
            {
                int layer = Mathf.Max(0, layers[at + k] - based);
                var instance = instances[k];
                var hash = new Hasher();
                hash.Add(s_path[node]);
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
