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
    /// picture of the other node, as it was drawn, to the shown node itself, live, on the change's animation.
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
            foreach (var flight in s_started)
                flight.Outer = OuterOf(flight);
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

        // The innermost flight whose two ends hold both of `flight`'s: a named part inside a pair, which flies inside
        // that pair's shape.
        private static Flight OuterOf(Flight flight)
        {
            Flight outer = null;
            foreach (var other in Flights)
            {
                if (other == flight || !IsInside(flight.Destination, other.Destination) || !IsInside(flight.Source, other.Source))
                    continue;
                if (outer == null || IsInside(other.Destination, outer.Destination))
                    outer = other;
            }
            return outer;
        }

        private static bool IsInside(UINode node, UINode ancestor) =>
            node != null && ancestor != null && node != ancestor && node.transform.IsChildOf(ancestor.transform);

        /// <summary>
        /// Once a frame while flights are under way, before compositing: lets go of those that have landed or lost their
        /// destination, and works out where each of the rest is drawn this frame, outer ones before their parts.
        /// </summary>
        internal static void Update()
        {
            int frame = UIFrame.FrameIndex;
            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                if (i >= Flights.Count) continue;
                var flight = Flights[i];
                var destination = flight.Destination;
                bool gone = destination == null || !destination.isActiveAndEnabled || destination.Tree == null || destination.Index < 0;
                bool landed = !flight.Offset.IsAnimating && !flight.Corners.IsAnimating && !flight.Fade.IsAnimating;
                // Hidden outside a change, its destination lands no more.
                bool hidden = !gone && !IsShown(destination);
                if (gone || hidden || landed)
                    Drop(flight);
            }
            foreach (var flight in Flights)
                Measure(flight, frame);
        }

        // Where a flight is drawn this frame: where its destination is drawn (inside its outer flight, for a part) and
        // how far from that it still is.
        private static void Measure(Flight flight, int frame)
        {
            if (flight.MeasuredFrame == frame) return;
            flight.MeasuredFrame = frame;
            var outer = flight.Outer;
            if (outer != null)
                Measure(outer, frame);
            var destination = flight.Destination;
            var tree = destination.Tree;
            int d = destination.Index;
            flight.Tree = tree;
            var world = tree.World[d];
            var size = (Vector2)tree.Slots[d].SizeOpacity;
            var placed = outer != null && outer.Placed ? outer.FitMap * world : world;
            var drawn = Bounds(placed, size);
            var radii = destination.ClipRadii.Drawn.ToVector() * placed.ScaleFactor;
            if (!flight.Placed)
            {
                // Its first frame: it sets off from where the other node was drawn.
                flight.Placed = true;
                flight.Offset.Displace(RectVector(flight.From) - RectVector(drawn));
                flight.Corners.Displace(flight.FromRadii - radii);
            }
            var offset = flight.Offset.Drawn;
            var rect = new Rect(drawn.x + offset.x, drawn.y + offset.y, Mathf.Max(1f, drawn.width + offset.z), Mathf.Max(1f, drawn.height + offset.w));
            flight.Rect = rect;
            flight.Radii = Vector4.Max(Vector4.zero, radii + flight.Corners.Drawn);
            flight.Progress = Mathf.Clamp01(flight.Fade.Drawn);
            // What is in the destination is fitted to the flight's width from its top, text to its height, so its glyphs
            // never swell with the width; its own background stretches.
            flight.LiveScale = flight.Text ? rect.height / Mathf.Max(1e-3f, size.y) : rect.width / Mathf.Max(1e-3f, size.x);
            flight.FitMap = Scaled(rect.position, flight.LiveScale) * world.Inverse;
            flight.PictureScale = flight.Text
                ? rect.height / Mathf.Max(1e-3f, flight.Picture.Size.y)
                : rect.width / Mathf.Max(1e-3f, flight.Picture.Size.x);
            flight.PictureMap = Scaled(rect.position, flight.PictureScale);
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
            foreach (var other in Flights)
                if (other.Outer == flight)
                    other.Outer = null;
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

        // Scaled by `scale` from the origin, then moved to `position`.
        private static Affine2 Scaled(Vector2 position, float scale) =>
            new() { A = scale, D = scale, Tx = position.x, Ty = position.y };
    }

    /// <summary>
    /// One matched pair under way: a picture of the node it flies from, as that was drawn, and the node it flies to, live,
    /// cross-fading as one image in one rounded shape. What springs is how far it is from where its destination is drawn
    /// (closing to nothing, so whatever moves the destination meanwhile carries it), its corners from the destination's,
    /// and how far the destination has faded in.
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

        // Whether its ends are text, fitted to the flight's height rather than its width.
        public readonly bool Text;

        // The flight whose pair holds this one's ends, which it flies inside the shape of (null at the top).
        public Flight Outer;

        // How far it is from where its destination is drawn (x, y, width, height), its corners from the destination's,
        // and how far the destination has faded in over the picture.
        public readonly AnimatedValue<Vector4> Offset = new(Vector4.zero, 0.25f, true);
        public readonly AnimatedValue<Vector4> Corners = new(Vector4.zero, 0.1f, true);
        public readonly AnimatedValue<float> Fade = new(0f, 0.002f, true);

        // Where the source was drawn as it set off, in its root's points, and its corners; placed on its first frame.
        public Rect From;
        public Vector4 FromRadii;
        public bool Placed;
        public int MeasuredFrame = -1;

        // This frame: its destination's tree, where it is drawn (its root's points), its corners and how far it has
        // faded; and the maps that fit the destination (its root's space to the flight) and the picture (the source's
        // own space to the flight) into it, at their scales.
        public UITree Tree;
        public Rect Rect;
        public Vector4 Radii;
        public float Progress;
        public Affine2 FitMap;
        public float LiveScale = 1f;
        public Affine2 PictureMap;
        public float PictureScale = 1f;

        // The compositor's, for the build under way: the build it was last laid out for, whether it is drawn, its pair's
        // slot and clips, its shape as a clip, and where its ends' slots start.
        internal int SlotsBuild = -1;
        internal bool InBuild;
        internal int PairSlot;
        internal Vector4 PairClips;
        internal int ShapeClip;
        internal int LiveSlots;
        internal int PictureSlots;

        public Flight(MatchKey key, UINode source, UINode destination, bool takesOver, UINode sourceLevel, Picture picture)
        {
            Key = key;
            Source = source;
            Destination = destination;
            TakesOver = takesOver;
            SourceLevel = sourceLevel;
            Picture = picture;
            Text = destination != null && destination.TryGetComponent(out Label _);
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
