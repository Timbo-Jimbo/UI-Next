using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    // Shared primitives (SubtreeMatch) drawn once. With subtree matching on, the draw order is made twice: walked once as
    // it is, which tells each node's primitives' layers; then the sets found from those, and walked again. One copy of each
    // set, its representative, has the primitives its set's image holds drawn to that image, in its own space; every copy
    // (the representative too) is drawn as a quad in its first layer showing the image, and only its other primitives are
    // drawn live, each in its own layer, over it.
    internal static partial class Compositor
    {
        /// <summary>One set of copies drawn partly from one image.</summary>
        private struct SharedImage
        {
            public UITree Tree;
            // The copy drawn to the image (-1: none can be), and how many copies show it.
            public int Representative;
            public int Copies;
            // What the image covers in the copies' own space, in points, and its pixels to a point.
            public Rect Bounds;
            public float Scale;
            // The image, once a copy has been emitted showing it (null until then), and the part of their space it covers.
            public RenderTexture Target;
            public Rect Region;
        }

        /// <summary>
        /// A tree's copies as last found, the primitives their images hold, each node's set (-1 for none), and for each node
        /// in a copy whether its set's image holds each of its primitives (from ImageAt, -1 for a node in no copy).
        /// </summary>
        private sealed class TreeShares
        {
            public readonly List<SubtreeMatch.Match> Matches = new();
            public readonly List<SubtreeMatch.ImagePrimitive> Image = new();
            public readonly List<bool> InImage = new();
            public int[] SetOf = new int[32];
            public int[] ImageAt = new int[32];
        }

        // What a walk draws of a node's primitives: all of them, a copy's live ones (those its set's image does not hold),
        // or an image's (those it holds).
        private enum Drawing : byte
        {
            All,
            Live,
            Image,
        }

        private static readonly Dictionary<UITree, TreeShares> s_treeShares = new();
        private static readonly List<SharedImage> s_shared = new();
        private static readonly List<RenderTexture> s_matchTargets = new();
        // The tree being walked's shares (null: none), whether copies are drawn from images in this walk, how many quads
        // showing an image it emitted, and what of the node being walked it draws.
        private static TreeShares s_currentShares;
        private static bool s_drawMatches;
        private static int s_matchQuads;
        private static Drawing s_drawing;

        /// <summary>How many copies were drawn from a set's image in the last build.</summary>
        internal static int MatchQuads => s_matchQuads;

        // What the last build found: copies, sets, and the primitives their images hold (a copy's, summed).
        internal static (int Subtrees, int Sets, int Primitives) Matched;

        // Forgets the last build's sets.
        private static void ClearShares()
        {
            s_shared.Clear();
            foreach (var shares in s_treeShares.Values)
            {
                shares.Matches.Clear();
                shares.Image.Clear();
                shares.InImage.Clear();
            }
            s_currentShares = null;
            s_drawMatches = false;
            Matched = default;
        }

        // After the walk as it is: each tree's sets, from the layers its primitives were drawn in; which primitives of each
        // copy its set's image holds; and for each set the first copy that can be drawn to the image for the rest. A set
        // with no copy to draw from is drawn as it is.
        private static void FindShares(IReadOnlyList<UITree> trees)
        {
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                if (tree.Nodes.Count == 0) continue;
                if (!s_treeShares.TryGetValue(tree, out var shares))
                    s_treeShares.Add(tree, shares = new TreeShares());
                SubtreeMatch.Find(tree, s_builds, s_layers, shares.Matches, shares.Image);
                int count = tree.Nodes.Count;
                if (shares.SetOf.Length < count)
                {
                    shares.SetOf = new int[Mathf.NextPowerOfTwo(count)];
                    shares.ImageAt = new int[shares.SetOf.Length];
                }
                System.Array.Fill(shares.SetOf, -1, 0, count);
                System.Array.Fill(shares.ImageAt, -1, 0, count);
                int first = s_shared.Count;
                foreach (var match in shares.Matches)
                {
                    // Every node of the copy gets a flag a primitive, the image's set.
                    for (int j = match.Node; j < tree.SubtreeEnd[match.Node]; j++)
                    {
                        var node = tree.Nodes[j];
                        shares.ImageAt[j] = shares.InImage.Count;
                        for (int k = node.Instances.Count + node.OverlayInstances.Count; k > 0; k--)
                            shares.InImage.Add(false);
                    }
                    for (int p = match.ImageFirst; p < match.ImageFirst + match.ImageCount; p++)
                    {
                        var primitive = shares.Image[p];
                        shares.InImage[shares.ImageAt[primitive.Node] + primitive.Index] = true;
                    }

                    int set = first + match.Set;
                    while (s_shared.Count <= set)
                        s_shared.Add(new SharedImage { Tree = tree, Representative = -1 });
                    var shared = s_shared[set];
                    shared.Copies++;
                    if (shared.Representative < 0 && CanRepresent(tree, match, shares.Image, out shared.Bounds, out shared.Scale))
                        shared.Representative = match.Node;
                    s_shared[set] = shared;
                    shares.SetOf[match.Node] = set;
                    Matched.Subtrees++;
                    Matched.Primitives += match.Primitives;
                }
                Matched.Sets += s_shared.Count - first;
                foreach (var match in shares.Matches)
                    if (s_shared[first + match.Set].Representative < 0)
                        shares.SetOf[match.Node] = -1;
            }
            s_drawMatches = s_shared.Count > 0;
        }

        // Whether a copy can have its image drawn from it for the others: drawn at its full opacity (what is above it fades
        // its quad instead), and what its image holds wholly inside every clip over it (the image must not be cut). Gives
        // what the image covers in the copy's own space, in points (smoothed edges and shadows included), and pixels to a
        // point at the scale it is drawn on screen. Where it is drawn upright, the image's pixels line up with the screen's
        // there.
        private static bool CanRepresent(UITree tree, in SubtreeMatch.Match match, List<SubtreeMatch.ImagePrimitive> image, out Rect bounds, out float scale)
        {
            bounds = default;
            int i = match.Node;
            var slot = tree.Slots[i];
            var world = tree.World[i];
            float pixels = tree.Root.PixelsPerPoint;
            scale = pixels * world.ScaleFactor;
            if (slot.SizeOpacity.z < 1f || scale <= 0f) return false;

            var toLocal = world.Inverse;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int p = match.ImageFirst; p < match.ImageFirst + match.ImageCount; p++)
            {
                var primitive = image[p];
                var node = tree.Nodes[primitive.Node];
                int own = node.Instances.Count;
                var instance = primitive.Index < own ? node.Instances[primitive.Index] : node.OverlayInstances[primitive.Index - own];
                Include(instance, toLocal * tree.World[primitive.Node], tree.Slots[primitive.Node].SizeOpacity, ref min, ref max);
            }
            if (min.x > max.x) return false;
            const float smoothing = 2f;
            float pad = smoothing / scale;
            min -= new Vector2(pad, pad);
            max += new Vector2(pad, pad);
            if (world.B == 0f && world.C == 0f && world.A > 0f && world.D > 0f)
            {
                min.x = (Mathf.Floor((world.A * min.x + world.Tx) * pixels) / pixels - world.Tx) / world.A;
                min.y = (Mathf.Floor((world.D * min.y + world.Ty) * pixels) / pixels - world.Ty) / world.D;
            }
            bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            for (int c = 0; c < 4; c++)
            {
                int clip = (int)slot.Clips[c];
                if (clip < 0 || clip >= tree.ClipShapes.Count) continue;
                var shape = tree.ClipShapes[clip];
                if (!Inside(shape, world.Apply(bounds.min)) || !Inside(shape, world.Apply(bounds.max))
                    || !Inside(shape, world.Apply(new Vector2(bounds.xMin, bounds.yMax)))
                    || !Inside(shape, world.Apply(new Vector2(bounds.xMax, bounds.yMin))))
                    return false;
            }
            return true;
        }

        // Whether a point of the root's space is inside a clip, clear of its corners' rounding.
        private static bool Inside(in ClipShape shape, Vector2 point)
        {
            float x = shape.Inverse0.x * point.x + shape.Inverse0.y * point.y + shape.Inverse0.z;
            float y = shape.Inverse1.x * point.x + shape.Inverse1.y * point.y + shape.Inverse1.z;
            var r = shape.Radii;
            float round = Mathf.Max(Mathf.Max(r.x, r.y), Mathf.Max(r.z, r.w));
            return x >= round && y >= round && x <= shape.Size.x - round && y <= shape.Size.y - round;
        }

        // Whether `node` is a copy drawn partly from its set's image, while it is walked (not while its own image is).
        private static bool IsCopy(UINode node) =>
            s_drawMatches && node != s_groupRoot && s_currentShares != null && s_currentShares.SetOf[node.Index] >= 0;

        // Whether the walk draws a node's index-th primitive (those under its children, then those over them).
        private static bool Draws(UINode node, int index)
        {
            if (s_drawing == Drawing.All) return true;
            int at = s_currentShares.ImageAt[node.Index];
            bool inImage = at >= 0 && s_currentShares.InImage[at + index];
            return inImage == (s_drawing == Drawing.Image);
        }

        // A copy's quad: on its own node in its first layer, showing its set's image (made as the first copy is emitted)
        // at full opacity, its node's fading it.
        private static void EmitMatchQuad(UITree tree, UINode node, int slotBase, int layer)
        {
            int set = s_currentShares.SetOf[node.Index];
            var shared = s_shared[set];
            if (shared.Target == null)
            {
                var bounds = shared.Bounds;
                while (s_matchTargets.Count <= set)
                    s_matchTargets.Add(null);
                var target = s_matchTargets[set];
                ImageTarget(ref target, "UINext Match " + set, Mathf.Max(1, Mathf.CeilToInt(bounds.width * shared.Scale)),
                    Mathf.Max(1, Mathf.CeilToInt(bounds.height * shared.Scale)));
                s_matchTargets[set] = target;
                shared.Target = target;
                shared.Region = new Rect(bounds.x, bounds.y, target.width / shared.Scale, target.height / shared.Scale);
                s_shared[set] = shared;
            }
            var region = shared.Region;
            s_matchQuads++;
            Put(new PrimitiveInstance
            {
                Rect = new Vector4(region.x, region.y, region.width, region.height),
                Color = Color.white,
                Uv = new Vector4(0f, 0f, 1f, 1f),
                Slot = (uint)(slotBase + node.Index),
                Type = (uint)PrimitiveType.Group,
            }, (byte)PrimitiveKind.Group, layer, shared.Target, null);
        }

        // After the tree's groups: a group for each of its sets a copy was emitted showing, drawing what its image holds of
        // its representative, in its own space. Last in the groups, so drawn first: copies inside other groups' images
        // show them.
        private static void AddMatchGroups(UITree tree)
        {
            if (!s_drawMatches) return;
            int first = Groups.Count;
            foreach (var shared in s_shared)
            {
                if (shared.Tree != tree || shared.Target == null) continue;
                Groups.Add(new Group
                {
                    Tree = tree,
                    Node = tree.Nodes[shared.Representative],
                    Target = shared.Target,
                    Region = shared.Region,
                    InNodeSpace = true,
                    FromRoot = tree.World[shared.Representative].Inverse,
                    MatchImage = true,
                });
            }
            WalkGroups(tree, first);
        }
    }
}
