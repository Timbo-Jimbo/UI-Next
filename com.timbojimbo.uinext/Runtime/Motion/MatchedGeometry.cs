using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>What a node is matched by: its name, and the id it has or the nearest node above it has.</summary>
    internal readonly struct MatchKey : IEquatable<MatchKey>
    {
        public readonly string Name;
        public readonly string Id;

        public MatchKey(string name, string id)
        {
            Name = name ?? "";
            Id = id ?? "";
        }

        public bool Equals(MatchKey other) =>
            string.Equals(Name, other.Name, StringComparison.Ordinal) && string.Equals(Id, other.Id, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MatchKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, Id);
    }

    /// <summary>
    /// Matched geometry, as SwiftUI's matchedGeometryEffect, drawn as the web's view transitions draw it. As a change made
    /// with <see cref="UI.Animate(UIAnimation, Action)"/> begins, the named nodes shown are noted, where each is drawn
    /// and how fast; once its update and layout are done, a node it showed with the name and id of one it hid takes over
    /// from that one, and a node it showed while a namesake stays flies out of that one: a <see cref="Flight"/> from a
    /// picture of the other node, as it was drawn, to the shown node itself, live, on the change's animation. Each end's
    /// own drawing (what it draws relative to its rect: a box, its border, its shadow) is laid out at the flight's rect;
    /// what is inside it is scaled evenly into the flight along the axis the two ends are most alike in, lined up along
    /// the other by its node's <see cref="UINode.MatchAnchor"/>, and cut to the flight's window, whose corners go from
    /// one end's <see cref="UINode.ClipRadii"/> to the other's. The two ends are added together as they cross-fade, so
    /// where both are opaque nothing behind shows through.
    /// </summary>
    internal static class MatchedGeometry
    {
        // A named node shown as a change began: where it was drawn (its root's points), how fast that was moving, its
        // shape's corners, and the flight it was flying in, if any.
        private struct Candidate
        {
            public MatchKey Key;
            public UINode Node;
            public UITree Tree;
            public int Pass;
            public UINode Level;
            public Rect Rect;
            public Vector4 Velocity;
            public Vector4 Radii;
            public Vector4 RadiiVelocity;
            public Flight Flight;
            public bool Shown;
            public bool Taken;
        }

        // How far ahead, in seconds, where a node is drawn is looked at for how fast it moves.
        private const float Ahead = 1f / 240f;

        /// <summary>Every flight under way.</summary>
        internal static readonly List<Flight> Flights = new();

        private static readonly List<Candidate> s_before = new();
        private static readonly HashSet<UINode> s_beforeNodes = new();
        private static readonly List<UINode> s_shown = new();
        private static readonly List<Flight> s_started = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Flights.Clear();
            s_before.Clear();
            s_beforeNodes.Clear();
            s_shown.Clear();
            s_started.Clear();
        }

        /// <summary>As a change's update is about to run: notes the named nodes shown, where each is drawn and how fast.</summary>
        internal static void Begin()
        {
            s_before.Clear();
            s_beforeNodes.Clear();
            var trees = UITree.Trees;
            for (int t = 0; t < trees.Count; t++)
            {
                var tree = trees[t];
                var nodes = tree.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node == null) continue;
                    var name = node.MatchName.Peek;
                    if (string.IsNullOrEmpty(name) || !IsShown(node)) continue;
                    s_before.Add(Note(node, tree, name));
                    s_beforeNodes.Add(node);
                }
            }
        }

        private static Candidate Note(UINode node, UITree tree, string name)
        {
            var candidate = new Candidate
            {
                Key = KeyOf(node, name),
                Node = node,
                Tree = tree,
                Pass = tree.CompositePass,
                Level = LevelOf(node),
            };
            var flight = node.FlightIn;
            if (flight != null && flight.Placed)
            {
                // Flying: it is drawn where its flight is.
                candidate.Flight = flight;
                candidate.Rect = flight.Rect;
                candidate.Velocity = DrawnVelocity(tree, node) + flight.Offset.Velocity;
                candidate.Radii = flight.Radii;
                candidate.RadiiVelocity = flight.Corners.Velocity;
                return candidate;
            }
            var world = tree.WorldAhead(node, 0f, out var size);
            candidate.Rect = Inside(node, Bounds(world, size));
            candidate.Velocity = DrawnVelocity(tree, node);
            float scale = world.ScaleFactor;
            candidate.Radii = node.ClipRadii.Drawn.ToVector() * scale;
            candidate.RadiiVelocity = node.ClipRadii.Velocity * scale;
            return candidate;
        }

        /// <summary>
        /// Once the change's update and layout are done: pairs the named nodes it showed with the ones it hid (or left
        /// shown), and sets their flights off on <paramref name="transaction"/>'s animation, held by it.
        /// </summary>
        internal static void End(UITransaction transaction)
        {
            s_shown.Clear();
            var trees = UITree.Trees;
            for (int t = 0; t < trees.Count; t++)
            {
                var nodes = trees[t].Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node != null && !string.IsNullOrEmpty(node.MatchName.Peek) && IsShown(node))
                        s_shown.Add(node);
                }
            }
            for (int c = 0; c < s_before.Count; c++)
            {
                var candidate = s_before[c];
                candidate.Shown = IsShown(candidate.Node);
                s_before[c] = candidate;
            }

            s_started.Clear();
            if (!transaction.Animation.AtOnce)
            {
                foreach (var node in s_shown)
                {
                    if (s_beforeNodes.Contains(node)) continue;
                    var key = KeyOf(node, node.MatchName.Peek);
                    // One the change hid is taken over; failing that, one that stays is flown out of.
                    int from = Find(key, shown: false);
                    bool takesOver = from >= 0;
                    if (!takesOver)
                        from = Find(key, shown: true);
                    if (from < 0) continue;
                    var candidate = s_before[from];
                    if (takesOver)
                    {
                        candidate.Taken = true;
                        s_before[from] = candidate;
                    }
                    s_started.Add(SetOff(candidate, node, takesOver, transaction));
                }
            }

            // A flight whose destination the change hid lands no more: it was taken back (a new flight turns round from
            // where it is drawn) or simply hidden.
            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                var flight = Flights[i];
                if (!IsShown(flight.Destination))
                    Drop(flight);
            }
            foreach (var flight in s_started)
            {
                Flights.Add(flight);
                Compositor.MarkOrderDirty();
            }
            s_started.Clear();
            s_before.Clear();
            s_beforeNodes.Clear();
        }

        // The candidate of `key` that is shown now (or hidden now) and not yet taken over; -1 for none.
        private static int Find(in MatchKey key, bool shown)
        {
            for (int c = 0; c < s_before.Count; c++)
            {
                var candidate = s_before[c];
                if (candidate.Shown == shown && !candidate.Taken && candidate.Key.Equals(key) && candidate.Node != null)
                    return c;
            }
            return -1;
        }

        // A flight from `candidate`, as it was drawn, to `destination`, set off now on the change's animation, from where
        // the candidate was drawn at the speed it moved.
        private static Flight SetOff(in Candidate candidate, UINode destination, bool takesOver, UITransaction transaction)
        {
            var source = candidate.Node;
            var picture = Picture.Of(source, candidate.Tree, candidate.Pass);
            var flight = new Flight(candidate.Key, source, destination, takesOver, candidate.Level, picture)
            {
                From = candidate.Rect,
                FromRadii = candidate.Radii,
                SourceAnchor = source.MatchAnchor.Peek,
                Transaction = transaction,
            };
            // Turned round, it fades on from where it had faded, the other way.
            float fade = 0f, fadeSpeed = 0f;
            var turned = candidate.Flight;
            if (turned != null && turned.Source == destination)
            {
                fade = 1f - Mathf.Clamp01(turned.Fade.Drawn);
                fadeSpeed = -turned.Fade.Velocity.x;
            }
            var animation = transaction.Animation;
            // Drawn at its destination's place plus its offset: the offset sets off at the source's speed less the speed
            // the destination is already drawn moving at, so the pair moves on as the source did.
            var velocity = candidate.Velocity;
            if (destination.Tree != null)
                velocity -= DrawnVelocity(destination.Tree, destination);
            flight.Offset.SpringTo(Vector4.zero, velocity, animation, transaction);
            flight.Corners.SpringTo(Vector4.zero, candidate.RadiiVelocity, animation, transaction);
            flight.Fade.Jump(fade);
            flight.Fade.SpringTo(1f, new Vector4(fadeSpeed, 0f, 0f, 0f), animation, transaction);

            // A flight still under way into the destination gives way to this one.
            var older = destination.FlightIn;
            if (older != null && older != turned && Flights.Contains(older))
                Drop(older);
            destination.FlightIn = flight;
            if (takesOver && source != null)
            {
                source.FlightOut = flight;
                // Its picture flies instead of it: it does not also leave on its own.
                if (source.Leaving.Peek)
                {
                    source.Leaving.Value = false;
                    source.Transition?.Set(Vector2.zero, 1f, 1f);
                }
            }
            if (turned != null && Flights.Contains(turned))
                Drop(turned);
            return flight;
        }

        /// <summary>
        /// Once a frame while flights are under way, before compositing: lets go of those that have landed or lost their
        /// destination, and works out where each of the rest is drawn this frame.
        /// </summary>
        internal static void Update()
        {
            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                if (i >= Flights.Count) continue;
                var flight = Flights[i];
                var destination = flight.Destination;
                bool gone = destination == null || !destination.isActiveAndEnabled || destination.Tree == null || destination.Index < 0;
                // Landed once its springs are at rest and the change it set off in has finished: until then a node it took
                // over stays undrawn, however long the rest of that change takes (a presentation hides its page only as
                // the change finishes, so letting go sooner drew the page again for a frame or two).
                bool landed = !flight.Offset.IsAnimating && !flight.Corners.IsAnimating && !flight.Fade.IsAnimating
                              && (flight.Transaction == null || flight.Transaction.IsFinished);
                // Hidden outside a change, its destination lands no more.
                bool hidden = !gone && !IsShown(destination);
                if (gone || hidden || landed)
                    Drop(flight);
            }
            foreach (var flight in Flights)
                Measure(flight);
        }

        // Where a flight is drawn this frame: where its destination is drawn and how far from that it still is; and how what
        // is inside each end is fitted into it.
        private static void Measure(Flight flight)
        {
            var destination = flight.Destination;
            var tree = destination.Tree;
            int d = destination.Index;
            flight.Tree = tree;
            var world = tree.World[d];
            var size = (Vector2)tree.Slots[d].SizeOpacity;
            var drawn = Bounds(world, size);
            var radii = destination.ClipRadii.Drawn.ToVector() * world.ScaleFactor;
            if (!flight.Placed)
            {
                // Its first frame: it sets off from where the other node was drawn. Both ends are fitted along the axis their
                // sizes are most alike in, so what is inside them is scaled least: their widths for a row growing into a
                // page, their heights for a line of text growing into a longer one.
                flight.Placed = true;
                flight.Offset.Displace(RectVector(flight.From) - RectVector(drawn));
                flight.Corners.Displace(flight.FromRadii - radii);
                flight.ByHeight = Unlike(drawn.height, flight.From.height) < Unlike(drawn.width, flight.From.width);
            }
            var offset = flight.Offset.Drawn;
            var rect = new Rect(drawn.x + offset.x, drawn.y + offset.y, Mathf.Max(1f, drawn.width + offset.z), Mathf.Max(1f, drawn.height + offset.w));
            flight.Rect = rect;
            flight.Radii = Vector4.Max(Vector4.zero, radii + flight.Corners.Drawn);
            flight.Progress = Mathf.Clamp01(flight.Fade.Drawn);
            // What is inside each end is scaled evenly to the flight along that axis and lined up with it along the other as
            // its node's anchor says; each end's own drawing is laid out at the flight's rect (the compositor's).
            flight.FitMap = Fit(rect, size, flight.ByHeight, destination.MatchAnchor.Peek) * world.Inverse;
            flight.PictureMap = Fit(rect, flight.Picture.Size, flight.ByHeight, flight.SourceAnchor);
        }

        // How unlike two lengths are, either way round.
        private static float Unlike(float a, float b) => Mathf.Abs(Mathf.Log(Mathf.Max(a, 1e-3f) / Mathf.Max(b, 1e-3f)));

        // What is inside something `size` big, scaled evenly into `rect` along one axis (its height, or else its width) and
        // lined up with it along the other at `anchor` (0 at its start, 1 at its end).
        private static Affine2 Fit(Rect rect, Vector2 size, bool byHeight, Vector2 anchor)
        {
            float scale = byHeight ? rect.height / Mathf.Max(1e-3f, size.y) : rect.width / Mathf.Max(1e-3f, size.x);
            var position = rect.position + Vector2.Scale(rect.size - size * scale, anchor);
            return new Affine2 { A = scale, D = scale, Tx = position.x, Ty = position.y };
        }

        // Lets go of a flight: one that landed, or one cut short (its change does not complete).
        private static void Drop(Flight flight)
        {
            flight.Offset.Settle();
            flight.Corners.Settle();
            flight.Fade.Settle();
            if (flight.Destination != null && flight.Destination.FlightIn == flight)
                flight.Destination.FlightIn = null;
            if (flight.Source != null && flight.Source.FlightOut == flight)
                flight.Source.FlightOut = null;
            Flights.Remove(flight);
            Compositor.MarkOrderDirty();
        }

        // ── Nodes ────────────────────────────────────────────────────────────────

        /// <summary>Whether a node is shown, where it is going: in a tree, it and everything above it visible, no presentation it is in going away.</summary>
        internal static bool IsShown(UINode node)
        {
            if (node == null || !node.isActiveAndEnabled || node.Tree == null || node.Index < 0) return false;
            for (var n = node; n != null; n = n.ParentNode)
            {
                if (n.Display.Peek != DisplayMode.Visible || n.PresentationLevel.Peek < 0)
                    return false;
            }
            return true;
        }

        /// <summary>The level a node is drawn at: the presented node it is in, or its root's node.</summary>
        internal static UINode LevelOf(UINode node)
        {
            for (var n = node; n != null; n = n.ParentNode)
            {
                if (n.PresentationLevel.Peek != 0)
                    return n;
            }
            return node != null && node.Tree != null ? node.Tree.RootNode : null;
        }

        private static MatchKey KeyOf(UINode node, string name)
        {
            for (var n = node; n != null; n = n.ParentNode)
            {
                var id = n.MatchId.Peek;
                if (!string.IsNullOrEmpty(id))
                    return new MatchKey(name, id);
            }
            return new MatchKey(name, "");
        }

        // How fast a node's drawn rect moves (x, y, width, height, its root's points a second), by everything moving it.
        private static Vector4 DrawnVelocity(UITree tree, UINode node)
        {
            var now = Bounds(tree.WorldAhead(node, 0f, out var size), size);
            var later = Bounds(tree.WorldAhead(node, Ahead, out var sizeLater), sizeLater);
            return (RectVector(later) - RectVector(now)) / Ahead;
        }

        // A rect in its root's points as it is drawn, through the flight of a node it is inside, if any.
        private static Rect Inside(UINode node, Rect rect)
        {
            for (var n = node.ParentNode; n != null; n = n.ParentNode)
            {
                var flight = n.FlightIn;
                if (flight != null && flight.Placed)
                    return Bounds(flight.FitMap, rect);
            }
            return rect;
        }

        internal static Rect Bounds(Affine2 world, Vector2 size)
        {
            Vector2 a = world.Apply(Vector2.zero), b = world.Apply(new Vector2(size.x, 0f));
            Vector2 c = world.Apply(size), d = world.Apply(new Vector2(0f, size.y));
            var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            return new Rect(min, max - min);
        }

        private static Rect Bounds(Affine2 map, Rect rect) =>
            Bounds(map * Affine2.Translate(rect.position), rect.size);

        private static Vector4 RectVector(Rect rect) => new(rect.x, rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// One matched pair under way: a picture of the node it flies from, as that was drawn, and the node it flies to, live,
    /// each laid out in its rect and cut to its window, cross-fading as one image. What springs is how far it is from where
    /// its destination is drawn (closing to nothing, so whatever moves the destination meanwhile carries it), its window's
    /// corners from the destination's, and how far the destination has faded in.
    /// </summary>
    internal sealed class Flight
    {
        public readonly MatchKey Key;

        // The node it flies from (hidden, its picture flying instead; or staying), the node it flies to, whether it took
        // over from the first, and the level the first was drawn at (a presentation going away).
        public readonly UINode Source;
        public readonly UINode Destination;
        public readonly bool TakesOver;
        public readonly UINode SourceLevel;
        public readonly Picture Picture;

        // The change it set off in, which it lands with.
        public UITransaction Transaction;

        // Where what is inside the source lines up with the flight: its node's anchor as the flight set off.
        public Vector2 SourceAnchor;

        // Whether its ends are fitted to its height, the axis they are most alike in, rather than to its width.
        public bool ByHeight;

        // How far it is from where its destination is drawn (x, y, width, height), its corners from the destination's,
        // and how far the destination has faded in over the picture.
        public readonly AnimatedValue<Vector4> Offset = new(Vector4.zero, 0.25f, true);
        public readonly AnimatedValue<Vector4> Corners = new(Vector4.zero, 0.1f, true);
        public readonly AnimatedValue<float> Fade = new(0f, 0.002f, true);

        // Where the source was drawn as it set off, in its root's points, and its corners; placed on its first frame.
        public Rect From;
        public Vector4 FromRadii;
        public bool Placed;

        // This frame: its destination's tree, where it is drawn (its root's points), its window's corners and how far it
        // has faded; and the maps that fit what is inside the destination (its root's space to the flight) and inside the
        // picture (the source's own space to the flight) into it.
        public UITree Tree;
        public Rect Rect;
        public Vector4 Radii;
        public float Progress;
        public Affine2 FitMap;
        public Affine2 PictureMap;

        // The compositor's, for the build under way: whether it is drawn, its pair's slot, its window as a clip, where its
        // ends' slots start, and the slots its ends' own drawing is laid out in, at its rect (-1: none).
        internal bool InBuild;
        internal int PairSlot;
        internal int Window;
        internal int LiveSlots;
        internal int PictureSlots;
        internal int LiveOwn = -1;
        internal int PictureOwn = -1;

        public Flight(MatchKey key, UINode source, UINode destination, bool takesOver, UINode sourceLevel, Picture picture)
        {
            Key = key;
            Source = source;
            Destination = destination;
            TakesOver = takesOver;
            SourceLevel = sourceLevel;
            Picture = picture;
        }
    }

    /// <summary>One node of a <see cref="Picture"/>: where it was drawn in the source's own space, and how.</summary>
    internal struct PictureSlot
    {
        public UINode Node;
        public int Parent;
        public Affine2 Transform;
        public Vector2 Size;
        public float Opacity;
        public float Saturation;
        public float Brightness;
        public Vector4 Clips;
    }

    /// <summary>
    /// A picture of a node as it was last drawn, for a flight from it: its primitives and its nodes', in drawing order,
    /// each node where it was drawn in the source's own space, how opaque under the source, and the clips inside the
    /// source that cut it. Taken from what the node and everything inside it kept of their last drawing, so it holds
    /// whatever the change did to them since.
    /// </summary>
    internal sealed class Picture
    {
        private static readonly List<UINode> s_children = new();

        // The source's size as drawn, its nodes, and their primitives (each one's Slot the index of its node here), the
        // textures they sample, and the clips inside it (from the source's own space to each clip's).
        public Vector2 Size;
        public readonly List<PictureSlot> Slots = new();
        public readonly List<PrimitiveInstance> Instances = new();
        public readonly List<Texture> Textures = new();
        public readonly List<ClipShape> Clips = new();

        private Affine2 _world;
        private Affine2 _inverse;
        private Vector4 _outside;
        private List<ClipShape> _treeClips;
        private int _pass;

        /// <summary>A picture of <paramref name="source"/> as <paramref name="tree"/>'s compositing pass <paramref name="pass"/> drew it (empty if it did not).</summary>
        public static Picture Of(UINode source, UITree tree, int pass)
        {
            var picture = new Picture();
            if (source == null || tree == null || pass < 0 || source.DrawnPass != pass) return picture;
            var drawn = source.DrawnSlot;
            picture.Size = drawn.SizeOpacity;
            picture._world = WorldOf(drawn);
            picture._inverse = picture._world.Inverse;
            picture._outside = drawn.Clips;
            picture._treeClips = tree.ClipShapes;
            picture._pass = pass;
            picture.Add(source, -1, 1f);
            return picture;
        }

        // A node and what is inside it, in drawing order: its primitives, its children's by ZIndex, its overlay.
        private void Add(UINode node, int parent, float parentOpacity)
        {
            if (node == null || node.DrawnPass != _pass || !node.DrawnShown) return;
            if (parent >= 0 && node.PresentationLevel.Peek != 0) return;
            float opacity = parentOpacity * node.DrawnOpacity;
            if (opacity <= 0f) return;
            var drawn = node.DrawnSlot;
            int slot = Slots.Count;
            Slots.Add(new PictureSlot
            {
                Node = node,
                Parent = parent,
                Transform = _inverse * WorldOf(drawn),
                Size = drawn.SizeOpacity,
                Opacity = opacity,
                Saturation = drawn.Row0.w,
                Brightness = drawn.Row1.w,
                Clips = InsideClips(drawn.Clips),
            });
            Copy(node.Instances, node.InstanceTextures, slot);

            int first = s_children.Count;
            Children(node.transform);
            int count = s_children.Count - first;
            SortByZIndex(first, count);
            for (int i = 0; i < count; i++)
                Add(s_children[first + i], slot, opacity);
            s_children.RemoveRange(first, count);

            Copy(node.OverlayInstances, node.OverlayTextures, slot);
        }

        // The nodes under a transform, through plain objects, not into other roots.
        private static void Children(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.TryGetComponent(out UINode node))
                {
                    if (!child.TryGetComponent(out UIRoot _))
                        s_children.Add(node);
                }
                else
                {
                    Children(child);
                }
            }
        }

        // A stable insertion sort of one node's children by ZIndex, as the compositor orders them.
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

        private void Copy(List<PrimitiveInstance> instances, List<Texture> textures, int slot)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                instance.Slot = (uint)slot;
                Instances.Add(instance);
                Textures.Add(i < textures.Count ? textures[i] : null);
            }
        }

        // The clips of a node's chain made inside the source (not those around it), from the source's own space.
        private Vector4 InsideClips(Vector4 chain)
        {
            var inside = new Vector4(-1f, -1f, -1f, -1f);
            int n = 0;
            for (int c = 0; c < 4; c++)
            {
                int index = (int)chain[c];
                if (index < 0) break;
                if (IsOutside(index) || _treeClips == null || index >= _treeClips.Count) continue;
                var shape = _treeClips[index];
                var fromSource = InverseOf(shape) * _world;
                inside[n++] = Clips.Count;
                Clips.Add(new ClipShape
                {
                    Inverse0 = new Vector4(fromSource.A, fromSource.B, fromSource.Tx, 0f),
                    Inverse1 = new Vector4(fromSource.C, fromSource.D, fromSource.Ty, 0f),
                    Size = shape.Size,
                    Radii = shape.Radii,
                });
            }
            return inside;
        }

        private bool IsOutside(int clip)
        {
            for (int c = 0; c < 4; c++)
                if ((int)_outside[c] == clip)
                    return true;
            return false;
        }

        /// <summary>A slot's transform from its node's space to its root's.</summary>
        internal static Affine2 WorldOf(in NodeSlot slot) =>
            new() { A = slot.Row0.x, B = slot.Row0.y, Tx = slot.Row0.z, C = slot.Row1.x, D = slot.Row1.y, Ty = slot.Row1.z };

        /// <summary>A clip's transform from its root's space to its own.</summary>
        internal static Affine2 InverseOf(in ClipShape clip) =>
            new() { A = clip.Inverse0.x, B = clip.Inverse0.y, Tx = clip.Inverse0.z, C = clip.Inverse1.x, D = clip.Inverse1.y, Ty = clip.Inverse1.z };
    }
}
