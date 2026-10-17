using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    // Shared drawing (UINode.SharedDrawing, SharedCopies). With it on, the draw order is made twice: walked once as it is,
    // which tells each node's primitives' layers; then the copies of each name sorted into variants and what each
    // variant's copies draw the same found, and walked again. One copy of each variant, its representative, has what its
    // variant's image holds drawn to that image, in its own space; every copy (the representative too) is drawn as a quad
    // in its first layer showing the image, and only its other primitives are drawn live, each in its own layer, over it.
    internal static partial class Compositor
    {
        /// <summary>One variant's copies, drawn partly from one image.</summary>
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
        /// A tree's copies as last found, the primitives their images hold, each node's variant (-1 for none) and nearest
        /// named node (its owner), and for each node a copy owns whether its variant's image holds each of its primitives
        /// (from ImageAt, -1 for a node in no copy).
        /// </summary>
        private sealed class TreeShares
        {
            public readonly List<SharedCopies.Copy> Copies = new();
            public readonly List<SharedCopies.ImagePrimitive> Image = new();
            public readonly List<bool> InImage = new();
            public int[] VariantOf = new int[32];
            public int[] Owner = new int[32];
            public int[] ImageAt = new int[32];
        }

        // What a walk draws of a node's primitives: all of them, a copy's live ones (those its variant's image does not
        // hold), or an image's (those it holds).
        private enum Drawing : byte
        {
            All,
            Live,
            Image,
        }

        private static readonly Dictionary<UITree, TreeShares> s_treeShares = new();
        private static readonly List<SharedImage> s_shared = new();
        private static readonly List<RenderTexture> s_sharedTargets = new();
        // The tree being walked's shares (null: none), whether copies are drawn from images in this walk, how many quads
        // showing an image it emitted, and what of the node being walked it draws.
        private static TreeShares s_currentShares;
        private static bool s_drawShared;
        private static int s_sharedQuads;
        private static Drawing s_drawing;

        /// <summary>How many copies were drawn from a variant's image in the last build.</summary>
        internal static int SharedQuads => s_sharedQuads;

        // What the last build found: copies, variants, and the primitives their images hold (a copy's, summed).
        internal static (int Copies, int Variants, int Primitives) Shared;

        // Forgets the last build's variants.
        private static void ClearShares()
        {
            s_shared.Clear();
            foreach (var shares in s_treeShares.Values)
            {
                shares.Copies.Clear();
                shares.Image.Clear();
                shares.InImage.Clear();
            }
            s_currentShares = null;
            s_drawShared = false;
            Shared = default;
        }

        // After the walk as it is: each tree's variants, from the layers its primitives were drawn in; which primitives of
        // each copy its variant's image holds; and for each variant the first copy that can be drawn to the image for the
        // rest. A variant with no copy to draw from is drawn as it is.
        private static void FindShares(IReadOnlyList<UITree> trees)
        {
            var settings = UISettings.Instance;
            int minCopies = settings != null ? settings.SharedMinCopies : UISettings.DefaultSharedMinCopies;
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                if (tree.Nodes.Count == 0) continue;
                if (!s_treeShares.TryGetValue(tree, out var shares))
                    s_treeShares.Add(tree, shares = new TreeShares());
                int count = tree.Nodes.Count;
                if (shares.VariantOf.Length < count)
                {
                    int size = Mathf.NextPowerOfTwo(count);
                    shares.VariantOf = new int[size];
                    shares.Owner = new int[size];
                    shares.ImageAt = new int[size];
                }
                SharedCopies.Find(tree, s_builds, s_layers, minCopies, shares.Copies, shares.Image, shares.Owner);
                System.Array.Fill(shares.VariantOf, -1, 0, count);
                System.Array.Fill(shares.ImageAt, -1, 0, count);
                int first = s_shared.Count;
                foreach (var copy in shares.Copies)
                {
                    // A flag a primitive for every node the copy owns, set where the image holds it.
                    for (int j = copy.Node; j < tree.SubtreeEnd[copy.Node]; j++)
                    {
                        if (shares.Owner[j] != copy.Node) continue;
                        var node = tree.Nodes[j];
                        shares.ImageAt[j] = shares.InImage.Count;
                        for (int k = node.Instances.Count + node.OverlayInstances.Count; k > 0; k--)
                            shares.InImage.Add(false);
                    }
                    for (int p = copy.ImageFirst; p < copy.ImageFirst + copy.ImageCount; p++)
                    {
                        var primitive = shares.Image[p];
                        shares.InImage[shares.ImageAt[primitive.Node] + primitive.Index] = true;
                    }

                    int variant = first + copy.Variant;
                    while (s_shared.Count <= variant)
                        s_shared.Add(new SharedImage { Tree = tree, Representative = -1 });
                    var shared = s_shared[variant];
                    shared.Copies++;
                    if (shared.Representative < 0 && CanRepresent(tree, copy, shares.Image, out shared.Bounds, out shared.Scale))
                        shared.Representative = copy.Node;
                    s_shared[variant] = shared;
                    shares.VariantOf[copy.Node] = variant;
                    Shared.Copies++;
                    Shared.Primitives += copy.Primitives;
                }
                Shared.Variants += s_shared.Count - first;
                foreach (var copy in shares.Copies)
                    if (s_shared[first + copy.Variant].Representative < 0)
                        shares.VariantOf[copy.Node] = -1;
            }
            s_drawShared = s_shared.Count > 0;
        }

        // Whether a copy can have its image drawn from it for the others: drawn at its full opacity (what is above it fades
        // its quad instead), and what its image holds wholly inside every clip over it (the image must not be cut). Gives
        // what the image covers in the copy's own space, in points (smoothed edges and shadows included), and pixels to a
        // point at the scale it is drawn on screen. Where it is drawn upright, the image's pixels line up with the screen's
        // there.
        private static bool CanRepresent(UITree tree, in SharedCopies.Copy copy, List<SharedCopies.ImagePrimitive> image, out Rect bounds, out float scale)
        {
            bounds = default;
            int i = copy.Node;
            var slot = tree.Slots[i];
            var world = tree.World[i];
            float pixels = tree.Root.PixelsPerPoint;
            scale = pixels * world.ScaleFactor;
            if (slot.SizeOpacity.z < 1f || scale <= 0f) return false;

            var toLocal = world.Inverse;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int p = copy.ImageFirst; p < copy.ImageFirst + copy.ImageCount; p++)
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

        // Whether `node` is a copy drawn partly from its variant's image, while the main walk or a group's reaches it (an
        // image's walk draws its own copy's primitives alone).
        private static bool IsCopy(UINode node) =>
            s_drawShared && node != s_groupRoot && s_drawing != Drawing.Image && s_currentShares != null
            && s_currentShares.VariantOf[node.Index] >= 0;

        // Whether the walk draws a node's index-th primitive (those under its children, then those over them): a copy's
        // live ones, or an image's own.
        private static bool Draws(UINode node, int index)
        {
            if (s_drawing == Drawing.All) return true;
            int at = s_currentShares.ImageAt[node.Index];
            bool inImage = at >= 0 && s_currentShares.InImage[at + index];
            if (s_drawing == Drawing.Live) return !inImage;
            return inImage && s_currentShares.Owner[node.Index] == s_groupRoot.Index;
        }

        // A copy's quad: on its own node in its first layer, showing its variant's image (made as the first copy is
        // emitted) at full opacity, its node's fading it.
        private static void EmitSharedQuad(UITree tree, UINode node, int slotBase, int layer)
        {
            int variant = s_currentShares.VariantOf[node.Index];
            var shared = s_shared[variant];
            if (shared.Target == null)
            {
                var bounds = shared.Bounds;
                while (s_sharedTargets.Count <= variant)
                    s_sharedTargets.Add(null);
                var target = s_sharedTargets[variant];
                ImageTarget(ref target, "UINext Shared " + variant, Mathf.Max(1, Mathf.CeilToInt(bounds.width * shared.Scale)),
                    Mathf.Max(1, Mathf.CeilToInt(bounds.height * shared.Scale)));
                s_sharedTargets[variant] = target;
                shared.Target = target;
                shared.Region = new Rect(bounds.x, bounds.y, target.width / shared.Scale, target.height / shared.Scale);
                s_shared[variant] = shared;
            }
            var region = shared.Region;
            s_sharedQuads++;
            Put(new PrimitiveInstance
            {
                Rect = new Vector4(region.x, region.y, region.width, region.height),
                Color = Color.white,
                Uv = new Vector4(0f, 0f, 1f, 1f),
                Slot = (uint)(slotBase + node.Index),
                Type = (uint)PrimitiveType.Group,
            }, (byte)PrimitiveKind.Group, layer, shared.Target, null);
        }

        // After the tree's groups: a group for each of its variants a copy was emitted showing, drawing what its image holds
        // of its representative, in its own space. Last in the groups, so drawn first: copies inside other groups' images
        // show them, and an image holds no other.
        private static void AddSharedGroups(UITree tree)
        {
            if (!s_drawShared) return;
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
                    IsSharedImage = true,
                });
            }
            WalkGroups(tree, first);
        }
    }
}
