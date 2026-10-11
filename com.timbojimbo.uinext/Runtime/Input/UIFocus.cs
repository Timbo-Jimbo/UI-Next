using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimboJimbo.UINext
{
    /// <summary>What a node is to keyboard and gamepad focus, over what is inside it (<see cref="UINode.FocusScope"/>).</summary>
    public enum FocusScope
    {
        /// <summary>Nothing: what is inside it takes focus as if it were not there.</summary>
        None,

        /// <summary>
        /// A part of the screen focus moves in and out of freely, as SwiftUI's focusSection: a move towards it from outside
        /// comes into it as one, even where nothing inside it lies in line, landing on what is nearest that way inside it,
        /// or where focus last was (<see cref="FocusEntry.Remembered"/>); Tab goes through everything inside it before going
        /// on. A sidebar, a list, a tab bar.
        /// </summary>
        Section,

        /// <summary>
        /// One stop until it is entered, as Xbox's focus engagement: Submit on it (Enter, a gamepad's A) enters it, where
        /// focus last was inside it, else at what prefers focus, else its first; inside, focus cannot leave it, and Back
        /// (Escape, a gamepad's B) leaves it, focus going back to it. A tap on a button inside it enters it. Groups nest:
        /// inside one, a group in it is a stop of its own. A grid of cards, a toolbar.
        /// </summary>
        Group,
    }

    /// <summary>Whether a node takes keyboard and gamepad focus (<see cref="UINode.Focusable"/>).</summary>
    public enum Focusability
    {
        /// <summary>If it is a button that takes presses (a <see cref="Pressable"/>).</summary>
        Auto,

        /// <summary>Button or not, as SwiftUI's focusable(): a slider, a stepper's row, what takes keys of its own.</summary>
        Always,

        /// <summary>Not even as a button: a tap area a key need not stop at (a story's sides, a backdrop).</summary>
        Never,
    }

    /// <summary>
    /// Takes keyboard and gamepad moves while its node has focus, as SwiftUI's onMoveCommand: a slider stepping along its
    /// track, a stepper's row stepping its value, a story stepping on and back. Asked before focus moves, held keys
    /// repeating; a move it takes moves nothing else.
    /// </summary>
    public interface IFocusMoveHandler
    {
        /// <summary>
        /// A move <paramref name="direction"/> (a step along one axis, x right, y down) while focus shows on its node:
        /// true if it took it, so focus stays where it is; false to let focus move on.
        /// </summary>
        bool OnFocusMove(Vector2 direction);
    }

    /// <summary>Where a move into a section from outside it lands (<see cref="UINode.FocusEntry"/>).</summary>
    public enum FocusEntry
    {
        /// <summary>On whatever inside it is nearest the way the move goes, as anywhere else: lists side by side.</summary>
        Nearest,

        /// <summary>
        /// On where focus last was inside it, else on what prefers focus inside it (<see cref="UINode.PrefersFocus"/>), else
        /// its first in reading order, as UIKit's remembersLastFocusedIndexPath: a sidebar, a tab bar, the page beside them.
        /// </summary>
        Remembered,
    }

    /// <summary>
    /// Keyboard and gamepad focus, as UIKit's focus engine and the web's: arrows, the d-pad or the left stick move it to the
    /// nearest that way, as drawn; Tab and Shift Tab go through in reading order (along rows the way
    /// <see cref="UI.ReadingDirection"/> reads); Enter, Space or a gamepad's South button (A) press what has it. Buttons
    /// take it (<see cref="Pressable"/>), and anything a node's <see cref="UINode.Focusable"/> says, and groups until
    /// they are entered; sections and groups (<see cref="UINode.FocusScope"/>) shape how it moves, and what has it can
    /// take the arrows itself (<see cref="IFocusMoveHandler"/>: a slider). Only the topmost presentation takes it: one coming up takes
    /// focus, at what prefers it, and one going gives it back to what had it in the level it uncovers (the button that
    /// opened it). Lost (hidden, disabled, gone), it goes to what is nearest where it was. A tap on a button moves it there,
    /// unseen, as a click focuses a button on the web.
    /// <para>
    /// It shows only while keys move it (focus-visible): what has it is Focused while a key or a gamepad moved it there,
    /// until a pointer is used, and the first key pressed after that only shows it, where it is. Every node it is in, up to
    /// its presentation, is FocusWithin meanwhile. What takes focus inside a scroll view is scrolled into view. While
    /// something is typed into (<see cref="FirstResponder"/>), the keys are its own.
    /// </para>
    /// </summary>
    public static class UIFocus
    {
        // Held, a direction repeats after this long, then this often.
        private const double RepeatDelay = 0.4;
        private const double RepeatInterval = 0.1;

        private static UINode s_focused;
        private static bool s_visible;
        private static UINode s_firstResponder;

        // The tree and level focus is in (a root's node, or a presented node) and the level's place in its tree's stack,
        // and the groups entered in it, outermost first: kept when focus is let go of, so a presentation coming up or
        // going is still noticed.
        private static UITree s_tree;
        private static UINode s_level;
        private static int s_levelNumber = -1;
        private static readonly List<UINode> s_entered = new();

        // Whether focus is held in its level with nothing there that can take it (a presentation with nothing to focus,
        // or everything that could gone): it comes back out with the presentation, showing as it did, and goes to what
        // can take it in a presentation as soon as there is something.
        private static bool s_parked;

        // Where what has focus was last drawn, and its parent, for where focus goes once it is lost.
        private static Rect s_lastRect;
        private static UINode s_lastParent;

        // The nodes FocusWithin is on: what has focus, and every node it is in up to its level, while it shows.
        private static readonly List<UINode> s_within = new();
        private static readonly List<UINode> s_path = new();

        // The stops focus can go to (buttons, and groups not entered, as one) in the container (the innermost group
        // entered, else the topmost level), with where each is drawn: gathered as keys move focus, not every frame.
        private static readonly List<UINode> s_stops = new();
        private static readonly List<Rect> s_rects = new();
        private static readonly HashSet<UINode> s_stopSet = new();
        private static UINode s_container;

        // What has focus that takes moves of its own, asked as a key moves.
        private static readonly List<IFocusMoveHandler> s_moveHandlers = new();

        // What a move can go to (stops, and sections focus is not in, as one), and the list a tie is ranked in.
        private static readonly List<UINode> s_units = new();
        private static readonly List<Rect> s_unitRects = new();
        private static List<UINode> s_ranking;
        private static readonly Func<int, int> s_rank = i => Rank(s_ranking[i]);

        private static Vector2 s_heldDirection;
        private static double s_nextRepeat;
        private static bool s_submitting;

        // What a submit key pressed, until it is let go: tapped then, if it still has focus.
        private static UINode s_submitted;

        // A submit key held since something was typed into, kept from pressing until it is let go.
        private static bool s_swallowing;

        // The frame something typed into last let go of the keys: they were its own all that frame.
        private static int s_resignedFrame = -1;

        /// <summary>Raised when focus moves, with the node it moved to (null when it is let go of).</summary>
        public static event Action<UINode> Changed;

        /// <summary>The node that has focus (a button, or a group not entered), or null.</summary>
        public static UINode Focused => s_focused != null ? s_focused : null;

        /// <summary>Whether focus shows: a key or a gamepad moved it, and no pointer has been used since.</summary>
        public static bool IsVisible => s_visible && Focused != null;

        /// <summary>
        /// What is being typed into, as UIKit's first responder: the node of a text field being edited, or null. Setting it
        /// focuses it. While there is one, the keys are its own: the arrows, Enter and Space neither move nor press focus,
        /// and Escape does not go back (the field ends its editing). Tab still moves focus on, and focus moving off it, or a
        /// gamepad's East button (B), lets it go. It shows as Focused while it is typed into, whatever is used meanwhile,
        /// as a browser shows a text box's focus.
        /// </summary>
        public static UINode FirstResponder
        {
            get => s_firstResponder != null ? s_firstResponder : null;
            set
            {
                var previous = FirstResponder;
                if (value == previous) return;
                s_firstResponder = value;
                if (previous != null)
                {
                    s_resignedFrame = Time.frameCount;
                    SetFocusedState(previous, previous == Focused && s_visible, false);
                }
                if (value != null)
                    SetFocus(value, s_visible, scroll: true);
                else
                    UpdateWithin();
            }
        }

        // Whether the keys are something's to type with this frame: it is typed into, or let go of them this frame.
        internal static bool KeysTaken => FirstResponder != null || s_resignedFrame == Time.frameCount;

        // Which way rows read, for Tab and for telling equally near candidates apart.
        private static bool RightToLeft => UI.ReadingDirection == ReadingDirection.RightToLeft;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_focused = null;
            s_visible = false;
            s_firstResponder = null;
            s_tree = null;
            s_level = null;
            s_levelNumber = -1;
            s_entered.Clear();
            s_parked = false;
            s_lastParent = null;
            s_within.Clear();
            s_stops.Clear();
            s_rects.Clear();
            s_stopSet.Clear();
            s_container = null;
            s_heldDirection = Vector2.zero;
            s_submitting = false;
            s_submitted = null;
            s_swallowing = false;
            s_resignedFrame = -1;
        }

        /// <summary>
        /// Focuses <paramref name="node"/> (a button, or a group as one stop), showing it unless told not to. A node inside
        /// a group enters the group.
        /// </summary>
        public static void Focus(UINode node, bool visible = true)
        {
            if (node != null && (node.Tree == null || !(IsItem(node) || IsGroup(node)))) return;
            SetFocus(node, visible, visible);
        }

        /// <summary>Lets go of focus.</summary>
        public static void Clear() => SetFocus(null, false, false);

        /// <summary>
        /// Moves focus to the nearest stop in <paramref name="direction"/> (x right, y down, along its stronger axis) from
        /// what has it, as drawn, a section focus is not in counting as one; with nothing focused, to where the container's
        /// focus starts. Shows focus. False if there is nowhere to go.
        /// </summary>
        public static bool Move(Vector2 direction)
        {
            if (direction == Vector2.zero) return false;
            var container = Collect(FocusTree());
            if (container == null || s_stops.Count == 0) return false;
            direction = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
            int current = CurrentStop();
            if (current < 0)
            {
                int entry = Entry(container, false);
                if (entry < 0) return false;
                SetFocus(s_stops[entry], true, true);
                return true;
            }

            // What it can go to: every other stop, or the outermost section it is in that focus is not, as one.
            var at = s_stops[current];
            s_units.Clear();
            s_unitRects.Clear();
            for (int i = 0; i < s_stops.Count; i++)
            {
                if (i == current) continue;
                var section = SectionOutside(container, s_stops[i], at);
                var unit = section != null ? section : s_stops[i];
                if (s_units.Contains(unit)) continue;
                s_units.Add(unit);
                s_unitRects.Add(section != null ? section.DrawnRect : s_rects[i]);
            }
            s_ranking = s_units;
            int picked = FocusNavigation.Pick(s_rects[current], direction, s_unitRects, s_rank, RightToLeft);
            var target = picked >= 0 ? Land(s_units[picked], s_rects[current], direction) : null;
            // Nowhere to go: it shows where it is.
            SetFocus(target != null ? target : at, true, true);
            return target != null;
        }

        /// <summary>
        /// Moves focus to the next stop in Tab order (or the previous one), wrapping round: in reading order, each section's
        /// stops all in their turn. Shows focus.
        /// </summary>
        public static bool Next(bool backwards = false)
        {
            var container = Collect(FocusTree());
            if (container == null || s_stops.Count == 0) return false;
            var order = TabOrder(container);
            int at = order.IndexOf(CurrentStop());
            at = at < 0 ? (backwards ? order.Count - 1 : 0) : (at + (backwards ? -1 : 1) + order.Count) % order.Count;
            SetFocus(s_stops[order[at]], true, true);
            return true;
        }

        // ── Moving focus ─────────────────────────────────────────────────────────

        // Moves focus to `node` (null to let go of it), showing it or not, and scrolling it into view if told to.
        private static void SetFocus(UINode node, bool visible, bool scroll)
        {
            s_parked = false;
            // Let go of, a node destroyed while it had focus is forgotten too.
            bool moved = node != Focused || (node == null && !ReferenceEquals(s_focused, null));
            if (moved)
            {
                // Focus moving off what is typed into lets it go.
                var responder = FirstResponder;
                if (responder != null && node != responder)
                {
                    s_firstResponder = null;
                    s_resignedFrame = Time.frameCount;
                    SetFocusedState(responder, false, false);
                }
                SetShown(false);
                s_focused = node;
                if (node != null)
                {
                    s_tree = node.Tree;
                    s_level = LevelOf(node);
                    s_levelNumber = Number(s_level);
                    s_lastParent = node.ParentNode;
                    s_lastRect = node.DrawnRect;
                    Remember(node);
                    Align(node);
                }
                else
                {
                    s_entered.Clear();
                }
                if (scroll && node != null)
                    ScrollIntoView(node);
            }
            else if (scroll && node != null && visible && !s_visible)
            {
                // Shown again where it is: brought into view, wherever a pointer scrolled it meanwhile.
                ScrollIntoView(node);
            }
            s_visible = visible && node != null;
            SetShown(s_visible);
            UpdateWithin();
            if (moved)
                Changed?.Invoke(node);
        }

        // Holds focus in the level it is in, with nothing there that can take it: what had it lets go, and whether focus
        // shows is kept for when it comes back.
        private static void Park()
        {
            bool visible = s_visible;
            SetFocus(null, false, false);
            s_visible = visible;
            s_parked = true;
        }

        // Where a move coming to `unit` lands: the stop itself; for a section, where focus last was inside it if it
        // remembers, else what is nearest the way the move goes inside it, its own sections as one again (or the nearest
        // at all, with none that way).
        private static UINode Land(UINode unit, Rect from, Vector2 direction)
        {
            if (!IsSection(unit)) return unit;
            if (unit.FocusEntry.Peek == FocusEntry.Remembered)
            {
                int entry = Entry(unit, true);
                if (entry >= 0) return s_stops[entry];
            }
            var units = new List<UINode>();
            var rects = new List<Rect>();
            for (int i = 0; i < s_stops.Count; i++)
            {
                if (!IsInside(s_stops[i], unit)) continue;
                var section = SectionBelow(unit, s_stops[i]);
                var inner = section != null ? section : s_stops[i];
                if (units.Contains(inner)) continue;
                units.Add(inner);
                rects.Add(section != null ? section.DrawnRect : s_rects[i]);
            }
            if (units.Count == 0) return null;
            s_ranking = units;
            int picked = FocusNavigation.Pick(from, direction, rects, s_rank, RightToLeft);
            if (picked < 0) picked = FocusNavigation.Nearest(from, rects);
            return Land(units[picked], from, direction);
        }

        // The stop focus takes inside `scope` as it comes to it (a presentation coming up, a group entered, a section a
        // move comes into): where focus last was inside it, if it remembers or `remembered` says to; else what prefers
        // focus inside it; else its first in Tab order. -1 for none.
        private static int Entry(UINode scope, bool remembered)
        {
            if (remembered || scope.FocusEntry.Peek == FocusEntry.Remembered)
            {
                int last = StopOf(scope.FocusMemory, scope);
                if (last >= 0) return last;
            }
            var order = TabOrder(scope);
            foreach (int i in order)
            {
                var preferring = Preferring(scope, s_stops[i]);
                if (preferring == null) continue;
                if (preferring == s_stops[i]) return i;
                int inside = Entry(preferring, false);
                if (inside >= 0) return inside;
            }
            return order.Count > 0 ? order[0] : -1;
        }

        // The stops inside `scope`, as indices into s_stops, in Tab order.
        private static List<int> TabOrder(UINode scope)
        {
            var inside = new List<int>();
            for (int i = 0; i < s_stops.Count; i++)
                if (IsInside(s_stops[i], scope))
                    inside.Add(i);
            var order = new List<int>(inside.Count);
            TabOrder(scope, inside, order);
            return order;
        }

        // Adds `stops` (stops inside `scope`) to `order` in Tab order: what is directly in `scope` (a stop, or a section,
        // as one) in reading order, a section's own stops in its turn.
        private static void TabOrder(UINode scope, List<int> stops, List<int> order)
        {
            var units = new List<UINode>();
            var rects = new List<Rect>();
            var members = new List<List<int>>();
            foreach (int i in stops)
            {
                var section = SectionBelow(scope, s_stops[i]);
                var unit = section != null ? section : s_stops[i];
                int k = units.IndexOf(unit);
                if (k < 0)
                {
                    k = units.Count;
                    units.Add(unit);
                    rects.Add(section != null ? section.DrawnRect : s_rects[i]);
                    members.Add(new List<int>());
                }
                members[k].Add(i);
            }
            foreach (int k in new List<int>(FocusNavigation.ReadingOrder(rects, RightToLeft)))
            {
                if (units[k] == s_stops[members[k][0]])
                    order.Add(members[k][0]);
                else
                    TabOrder(units[k], members[k], order);
            }
        }

        // The stop standing for `node` (itself, or the group it is in that is not entered) when it is inside `scope`, as
        // an index into s_stops, or -1.
        private static int StopOf(UINode node, UINode scope)
        {
            if (node == null || s_container == null || !IsInside(node, scope) || !IsInside(node, s_container)) return -1;
            var stop = StopFor(node, s_container);
            return IsInside(stop, scope) ? s_stops.IndexOf(stop) : -1;
        }

        // Equally near candidates are told apart by where focus was: one its parent last had focus in first, so focus goes
        // back to the tab or the row it left; then one that prefers focus.
        private static int Rank(UINode unit)
        {
            var parent = unit.ParentNode;
            if (parent != null && parent.FocusMemory != null && (parent.FocusMemory == unit || IsInside(parent.FocusMemory, unit)))
                return 0;
            return unit.PrefersFocus.Peek ? 1 : 2;
        }

        // Every node it is in, up to its level, remembers it as where focus last was inside it.
        private static void Remember(UINode node)
        {
            for (var n = node.ParentNode; n != null; n = n.ParentNode)
            {
                n.FocusMemory = node;
                if (n.PresentationLevel.Peek != 0) break;
            }
        }

        // Groups entered follow what has focus: those it is not in are left, and those it is in, below them, are entered,
        // outermost first. A group that has focus as a stop is not entered.
        private static void Align(UINode node)
        {
            var level = LevelOf(node);
            if (level == null)
            {
                s_entered.Clear();
                return;
            }
            int kept = 0;
            while (kept < s_entered.Count && s_entered[kept] != null && LevelOf(s_entered[kept]) == level && IsInside(node, s_entered[kept]))
                kept++;
            s_entered.RemoveRange(kept, s_entered.Count - kept);
            var outer = kept > 0 ? s_entered[kept - 1] : level;
            int at = s_entered.Count;
            for (var n = node.ParentNode; n != null && n != outer; n = n.ParentNode)
            {
                if (IsGroup(n))
                    s_entered.Insert(at, n);
                if (n == level) break;
            }
        }

        // Enters a group that has focus: focus goes inside it, where it last was, else to what prefers it, else to its
        // first. With nothing inside that can take it, it stays shut.
        private static void Enter(UINode group)
        {
            s_entered.Add(group);
            var container = Collect(group.Tree);
            int at = container == group ? Entry(group, true) : -1;
            if (at < 0)
            {
                s_entered.Remove(group);
                return;
            }
            SetFocus(s_stops[at], true, true);
        }

        // The first key pressed while focus does not show only shows it: where it is, or with nothing focused, where the
        // container's focus starts.
        private static void Reveal()
        {
            var focused = Focused;
            if (focused != null && IsStop(focused))
            {
                SetFocus(focused, true, true);
                return;
            }
            var container = Collect();
            if (container == null || s_stops.Count == 0) return;
            int entry = Entry(container, false);
            if (entry >= 0)
                SetFocus(s_stops[entry], true, true);
        }

        // Scrolls every scroll view the node is in, up to its presentation, until it is in view: the innermost to show it,
        // with room round it for its focus ring, and each one further out to show the one inside it.
        private static void ScrollIntoView(UINode node)
        {
            if (node.PresentationLevel.Peek != 0) return;
            var root = node.Root;
            float margin = root != null && node.FocusRing.Peek ? Mathf.Max(0f, root.FocusRingOffset.Peek + root.FocusRingWidth.Peek) : 0f;
            var target = node;
            for (var parent = node.ParentNode; parent != null; parent = parent.ParentNode)
            {
                if (parent.Scroller != null && parent.Scroller.isActiveAndEnabled)
                {
                    parent.Scroller.ScrollIntoView(target, margin: margin);
                    margin = 0f;
                    target = parent;
                }
                if (parent.PresentationLevel.Peek != 0) break;
            }
        }

        // ── Showing focus ────────────────────────────────────────────────────────

        // Shows or hides focus on what has it: Focused while it shows, or while it is typed into.
        private static void SetShown(bool shown)
        {
            var focused = Focused;
            if (focused != null)
                SetFocusedState(focused, shown, focused == FirstResponder);
        }

        // A node is Focused while focus shows on it or it is typed into; its focus ring is drawn only while focus shows, as
        // a key or a gamepad moved it (a field tapped into shows its focus its own way).
        private static void SetFocusedState(UINode node, bool shown, bool typedInto)
        {
            node.SetInteraction(InteractionStates.Focused, shown || typedInto);
            node.FocusVisible.Value = shown;
        }

        /// <summary>
        /// Draws the focus ring round <paramref name="node"/>, on which focus shows, as iPadOS's focus halo and a browser's
        /// focus outline: a band as thick as its root's <see cref="UIRoot.FocusRingWidth"/>, out past its edge by
        /// <see cref="UIRoot.FocusRingOffset"/>, its corners as round as <see cref="UIRoot.FocusRingRadius"/> whatever the
        /// node's shape, over what is inside it, so it follows the node as it moves, springs and is clipped.
        /// </summary>
        internal static void DrawRing(UINode node, Painter painter)
        {
            var root = node.Root;
            if (root == null) return;
            var color = root.FocusRingColor.Value;
            float width = root.FocusRingWidth.Value;
            if (width <= 0f || color.a <= 0f) return;
            float reach = root.FocusRingOffset.Value + width;
            float radius = Mathf.Max(0f, root.FocusRingRadius.Value);
            painter.Add(new PrimitiveInstance
            {
                Rect = new Vector4(-reach, -reach, -reach, -reach),
                Radii = new Vector4(radius, radius, radius, radius),
                Color = Color.clear,
                Color2 = color,
                Params = new Vector4(width, 0f, 0f, 0f),
                Type = (uint)PrimitiveType.Shape,
                Flags = (uint)PrimitiveFlags.RelativeToNode,
            }, null);
        }

        // FocusWithin on what has focus and on every node it is in, up to its level, while it shows (or is typed into); off
        // everywhere else it was.
        private static void UpdateWithin()
        {
            s_path.Clear();
            var focused = Focused;
            if (focused != null && (s_visible || focused == FirstResponder))
            {
                for (var n = focused; n != null; n = n.ParentNode)
                {
                    s_path.Add(n);
                    if (n.PresentationLevel.Peek != 0) break;
                }
            }
            foreach (var node in s_within)
                if (node != null && !s_path.Contains(node))
                    node.SetInteraction(InteractionStates.FocusWithin, false);
            foreach (var node in s_path)
                node.SetInteraction(InteractionStates.FocusWithin, true);
            s_within.Clear();
            s_within.AddRange(s_path);
        }

        // ── Where focus can be ───────────────────────────────────────────────────

        // Gathers the stops focus can go to (s_stops, with s_rects) in the topmost tree that has any or is presenting
        // something (a presentation keeps focus inside itself, with or without anything to take it), or in `only`: inside
        // its container, each button, or for one in a group not entered, the group, once. Returns the container, or null.
        private static UINode Collect(UITree only = null)
        {
            s_stops.Clear();
            s_rects.Clear();
            s_stopSet.Clear();
            s_container = null;
            var trees = UITree.Trees;
            for (int t = trees.Count - 1; t >= 0; t--)
            {
                var tree = trees[t];
                if ((only != null && tree != only) || tree.Root == null || !tree.Root.isActiveAndEnabled) continue;
                int level = TopLevelIndex(tree);
                if (level < 0) continue;
                var top = tree.Levels[level];
                var container = ContainerOf(tree, top);
                int start = tree.LevelStarts[level];
                int end = level + 1 < tree.LevelStarts.Count ? tree.LevelStarts[level + 1] : tree.DrawOrder.Count;
                for (int k = start; k < end; k++)
                {
                    int index = tree.DrawOrder[k];
                    if (index >= tree.Nodes.Count) continue;
                    var node = tree.Nodes[index];
                    if (node == null || !IsItem(node) || !InReach(node, true) || !IsInside(node, container)) continue;
                    var stop = StopFor(node, container);
                    if (!s_stopSet.Add(stop)) continue;
                    s_stops.Add(stop);
                    s_rects.Add(stop.DrawnRect);
                }
                if (s_stops.Count > 0 || top != tree.RootNode || only != null)
                {
                    s_container = container;
                    return container;
                }
            }
            return null;
        }

        // Where focus moves in a tree whose topmost level is `top`: inside the innermost group entered there, else the
        // level.
        private static UINode ContainerOf(UITree tree, UINode top)
        {
            if (s_entered.Count > 0 && tree == s_tree)
            {
                var group = s_entered[s_entered.Count - 1];
                if (group != null && LevelOf(group) == top) return group;
            }
            return top;
        }

        // Where what has focus is among the stops, or -1.
        private static int CurrentStop()
        {
            var focused = Focused;
            if (focused == null || s_container == null || !IsInside(focused, s_container)) return -1;
            return s_stops.IndexOf(StopFor(focused, s_container));
        }

        // Whether `node` can have focus where it is: a button, or a group with a button in it, drawn at the topmost level
        // of its tree, inside the group entered there (if any), and not inside a group that is not entered.
        private static bool IsStop(UINode node)
        {
            if (!CanHold(node)) return false;
            var tree = node.Tree;
            var container = ContainerOf(tree, TopLevel(tree));
            return IsInside(node, container) && StopFor(node, container) == node;
        }

        // Whether `node` can take focus at all: a button, or a group with a button inside it that can, drawn where focus is.
        private static bool CanHold(UINode node)
        {
            if (node == null) return false;
            bool group = IsGroup(node);
            if (!InReach(node, !group)) return false;
            return IsItem(node) || (group && HasItemInside(node));
        }

        // Whether a button inside a group can take focus.
        private static bool HasItemInside(UINode group)
        {
            var tree = group.Tree;
            int end = tree.SubtreeEnd[group.Index];
            for (int i = group.Index + 1; i < end; i++)
            {
                var node = tree.Nodes[i];
                if (node != null && IsItem(node) && InReach(node, true))
                    return true;
            }
            return false;
        }

        // Whether a node takes focus of itself: a button that takes presses, unless it says never; anything that says
        // always.
        private static bool IsItem(UINode node)
        {
            var focusable = node.Focusable.Peek;
            if (focusable != Focusability.Auto) return focusable == Focusability.Always;
            return node.TryGetComponent(out Pressable pressable) && pressable.isActiveAndEnabled && pressable.Interactable.Peek;
        }

        private static bool IsGroup(UINode node) => node.FocusScope.Peek == FocusScope.Group;

        // A section, unless it is a button too: a button is one stop, whatever is inside it.
        private static bool IsSection(UINode node) => node.FocusScope.Peek == FocusScope.Section && !IsItem(node);

        // Whether a node is where focus can reach it: shown and drawn (one faded to nothing is neither drawn nor hit, so it
        // takes no focus either), not on its way out (nor anything it is in), at the topmost level of its tree that takes
        // pointers; taking the pointer too, when `hit` says so (a button).
        private static bool InReach(UINode node, bool hit)
        {
            var tree = node.Tree;
            int i = node.Index;
            if (tree == null || i < 0 || i >= tree.Nodes.Count || tree.Nodes[i] != node) return false;
            if (!tree.Shown[i] || tree.Opacities[i] <= 0f || (hit && !node.HitTestable.Peek)) return false;
            var top = TopLevel(tree);
            for (var n = node; n != null; n = n.ParentNode)
            {
                if (n.Leaving.Peek) return false;
                if (n.PresentationLevel.Peek != 0) return n == top;
            }
            return tree.RootNode == top;
        }

        // What stands for `node` inside `container`: the outermost group between them (not entered), or the node itself.
        private static UINode StopFor(UINode node, UINode container)
        {
            var stop = node;
            for (var n = node.ParentNode; n != null && n != container; n = n.ParentNode)
                if (IsGroup(n))
                    stop = n;
            return stop;
        }

        // The outermost section between `container` and `stop` (both left out) that `at` is not in, or null.
        private static UINode SectionOutside(UINode container, UINode stop, UINode at)
        {
            UINode found = null;
            for (var n = stop.ParentNode; n != null && n != container; n = n.ParentNode)
                if (IsSection(n) && !IsInside(at, n))
                    found = n;
            return found;
        }

        // The outermost section between `scope` and `node` (both left out), or null.
        private static UINode SectionBelow(UINode scope, UINode node)
        {
            UINode found = null;
            for (var n = node.ParentNode; n != null && n != scope; n = n.ParentNode)
                if (IsSection(n))
                    found = n;
            return found;
        }

        // The outermost node between `scope` (left out) and `stop` that prefers focus, or null.
        private static UINode Preferring(UINode scope, UINode stop)
        {
            UINode found = null;
            for (var n = stop; n != null && n != scope; n = n.ParentNode)
                if (n.PrefersFocus.Peek)
                    found = n;
            return found;
        }

        // Whether `node` is inside `scope`, below it.
        private static bool IsInside(UINode node, UINode scope)
        {
            if (node == null || scope == null) return false;
            for (var n = node.ParentNode; n != null; n = n.ParentNode)
                if (n == scope)
                    return true;
            return false;
        }

        // The level a node is drawn at: the presented node it is in, or its root's node.
        private static UINode LevelOf(UINode node) => MatchedGeometry.LevelOf(node);

        // A level's place in its tree's stack of presentations: 0 for its root's.
        private static int Number(UINode level) => level != null ? Mathf.Abs(level.PresentationLevel.Peek) : -1;

        // The topmost level of a tree that takes pointers (a presented node, or its root's node), or null.
        private static UINode TopLevel(UITree tree)
        {
            int level = TopLevelIndex(tree);
            return level >= 0 ? tree.Levels[level] : null;
        }

        private static int TopLevelIndex(UITree tree)
        {
            for (int l = Mathf.Min(tree.Levels.Count, tree.LevelInteractive.Count) - 1; l >= 0; l--)
                if (tree.LevelInteractive[l])
                    return l;
            return -1;
        }

        // The tree focus moves in: the one it is in, while it can be there; with nothing focused, none, for the topmost
        // that has anything.
        private static UITree FocusTree()
        {
            var focused = Focused;
            return focused != null && IsStop(focused) ? focused.Tree : null;
        }

        // The tree focus goes to with nothing focused: the topmost presenting something, else the topmost.
        private static UITree TopTree()
        {
            UITree first = null;
            var trees = UITree.Trees;
            for (int t = trees.Count - 1; t >= 0; t--)
            {
                var tree = trees[t];
                if (tree.Root == null || !tree.Root.isActiveAndEnabled || tree.Levels.Count == 0) continue;
                if (first == null) first = tree;
                var top = TopLevel(tree);
                if (top != null && top != tree.RootNode) return tree;
            }
            return first;
        }

        // ── Keeping focus where it belongs ───────────────────────────────────────

        // Once a frame: moves focus into a presentation that comes up over it and back out of one that goes, and puts it
        // back where it belongs when what has it can no longer take it.
        private static void Maintain()
        {
            var focused = Focused;
            bool held = !ReferenceEquals(s_focused, null) || s_parked;
            var tree = focused != null && focused.Tree != null ? focused.Tree
                : held && s_tree != null && s_tree.Root != null && s_tree.Root.isActiveAndEnabled ? s_tree
                : TopTree();
            if (tree == null)
            {
                if (held) SetFocus(null, false, false);
                return;
            }

            var top = TopLevel(tree);
            UINode level = null;
            int number = -1;
            if (focused != null && focused.Tree == tree)
            {
                level = LevelOf(focused);
                number = Number(level);
            }
            else if (tree == s_tree)
            {
                level = s_level;
                number = s_levelNumber;
            }
            if (top != level)
            {
                // A presentation came up over the level focus is in, or the one it was in went. One coming up takes focus
                // (even with nothing focused before), at what prefers it; one going gives it back to what had it in the
                // level it uncovers.
                bool up = top != null && Number(top) > number;
                s_tree = tree;
                s_level = top;
                s_levelNumber = Number(top);
                s_entered.Clear();
                if (top == null)
                {
                    if (held) SetFocus(null, false, false);
                    return;
                }
                if (!held && (!up || top == tree.RootNode)) return;
                var memory = top.FocusMemory;
                if (!up && CanHold(memory) && IsInside(memory, top))
                {
                    SetFocus(memory, s_visible, s_visible);
                    return;
                }
                var container = Collect(tree);
                int entry = container != null ? Entry(container, false) : -1;
                if (entry >= 0) SetFocus(s_stops[entry], s_visible, s_visible);
                else Park();
                return;
            }
            if (!held) return;

            PruneEntered(top);
            if (focused != null && IsStop(focused))
            {
                s_lastRect = focused.DrawnRect;
                return;
            }
            if (focused == null && s_parked)
            {
                // Held in a presentation with nothing to take it: focus goes to the first thing there that can, once there
                // is one. (In a root's level, the next key finds it.)
                if (top == tree.RootNode) return;
                var container = Collect(tree);
                int entry = container != null && s_stops.Count > 0 ? Entry(container, false) : -1;
                if (entry >= 0) SetFocus(s_stops[entry], s_visible, s_visible);
                return;
            }
            // Lost: hidden, disabled, gone, or in a group no longer entered. It goes to what is nearest where it was, in
            // the innermost scope it was in that has anything; a group entered with nothing left inside is left.
            Collect(tree);
            while (s_stops.Count == 0 && s_entered.Count > 0)
            {
                s_entered.RemoveAt(s_entered.Count - 1);
                Collect(tree);
            }
            int nearest = Recover(focused);
            if (nearest >= 0) SetFocus(s_stops[nearest], s_visible, s_visible);
            else Park();
        }

        // Lets go of groups entered that no longer can be: gone, no longer groups, not drawn, or not in the level focus is
        // in.
        private static void PruneEntered(UINode top)
        {
            for (int g = 0; g < s_entered.Count; g++)
            {
                var group = s_entered[g];
                if (group != null && IsGroup(group) && InReach(group, false) && LevelOf(group) == top
                    && (g == 0 || IsInside(group, s_entered[g - 1])))
                    continue;
                s_entered.RemoveRange(g, s_entered.Count - g);
                return;
            }
        }

        // The stop nearest where `lost` was drawn, in the innermost section or group it was in that has any, else anywhere
        // in the container; -1 for none.
        private static int Recover(UINode lost)
        {
            if (s_stops.Count == 0) return -1;
            var from = lost != null && lost.ParentNode != null ? lost.ParentNode : s_lastParent;
            for (var scope = from; scope != null && scope != s_container; scope = scope.ParentNode)
            {
                if (!IsSection(scope) && !IsGroup(scope)) continue;
                int found = NearestInside(scope);
                if (found >= 0) return found;
            }
            return NearestInside(null);
        }

        // The stop inside `scope` (anywhere, with none) whose centre is nearest where focus was last drawn, or -1.
        private static int NearestInside(UINode scope)
        {
            int nearest = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < s_stops.Count; i++)
            {
                if (scope != null && !IsInside(s_stops[i], scope)) continue;
                float distance = (s_rects[i].center - s_lastRect.center).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = i;
            }
            return nearest;
        }

        // ── Input ────────────────────────────────────────────────────────────────

        /// <summary>A pointer was used: focus stops showing.</summary>
        internal static void PointerUsed()
        {
            if (!s_visible) return;
            s_visible = false;
            SetShown(false);
            UpdateWithin();
        }

        /// <summary>
        /// A pointer tapped a button: focus moves to it, unseen, as a click focuses a button on the web. Not while something
        /// is typed into: a tap on a button does not take its keys, as a tap on a UIKit button leaves the first responder.
        /// </summary>
        internal static void Tapped(UINode node)
        {
            if (node == null || node.Tree == null || !IsItem(node)) return;
            var responder = FirstResponder;
            if (responder != null && node != responder) return;
            SetFocus(node, false, false);
        }

        /// <summary>
        /// Back, while focus shows inside a group: the group is left, focus going back to it. True if it was; otherwise back
        /// goes on to <see cref="UIInput.Back"/>.
        /// </summary>
        internal static bool Back()
        {
            if (!IsVisible || s_entered.Count == 0) return false;
            var group = s_entered[s_entered.Count - 1];
            s_entered.RemoveAt(s_entered.Count - 1);
            if (group == null || !CanHold(group)) return false;
            SetFocus(group, true, true);
            return true;
        }

        /// <summary>
        /// Once a frame in play mode: focus kept where it belongs, then directions (repeating while held), Tab, and submit.
        /// </summary>
        internal static void Poll(double now)
        {
            Maintain();
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool taken = KeysTaken;
            var direction = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) direction.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) direction.x += 1f;
                if (keyboard.upArrowKey.isPressed) direction.y -= 1f;
                if (keyboard.downArrowKey.isPressed) direction.y += 1f;
                // Out of what is typed into, Tab moves on at once; otherwise, while focus does not show, it only shows it.
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    if (IsVisible || FirstResponder != null) Next(keyboard.shiftKey.isPressed);
                    else Reveal();
                }
            }
            if (gamepad != null)
            {
                var stick = gamepad.leftStick.ReadValue() + gamepad.dpad.ReadValue();
                if (stick.magnitude > 0.5f)
                    direction += new Vector2(stick.x, -stick.y);
            }
            // One way at a time: the stronger axis.
            if (direction != Vector2.zero)
                direction = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0f) : new Vector2(0f, Mathf.Sign(direction.y));

            bool submit = (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.spaceKey.isPressed))
                          || (gamepad != null && gamepad.buttonSouth.isPressed);
            // While something is typed into, the keys are its own; those held as it lets go stay its own until they
            // are let go of too (the Enter that submitted a field does not press it again, the arrow that moved its
            // caret does not move focus).
            if (taken)
            {
                s_heldDirection = direction;
                s_nextRepeat = double.MaxValue;
                s_swallowing = submit;
                s_submitting = false;
                Release(tap: false);
                return;
            }
            if (s_swallowing)
            {
                s_swallowing = submit;
                submit = false;
            }

            if (direction != s_heldDirection)
            {
                s_heldDirection = direction;
                s_nextRepeat = now + RepeatDelay;
                if (direction != Vector2.zero)
                {
                    if (IsVisible)
                    {
                        Step(direction);
                    }
                    else
                    {
                        // The first press only shows focus, where it is; held, it moves nothing until it is let go.
                        Reveal();
                        s_nextRepeat = double.MaxValue;
                    }
                }
            }
            else if (direction != Vector2.zero && now >= s_nextRepeat && IsVisible)
            {
                // Held, it repeats while focus shows: a pointer used meanwhile stops it, until a key is pressed again.
                s_nextRepeat = now + RepeatInterval;
                Step(direction);
            }

            if (submit && !s_submitting)
            {
                s_submitting = true;
                var focused = Focused;
                if (!IsVisible)
                {
                    // The first press only shows focus, as the first arrow does: what a pointer last pressed is not pressed
                    // again unseen (Space jumping in a game).
                    Reveal();
                }
                else if (focused != null && IsGroup(focused) && IsStop(focused))
                {
                    // Submit on a group enters it.
                    Enter(focused);
                }
                else if (focused != null && focused.TryGetComponent(out Pressable pressable) && pressable.Interactable.Peek)
                {
                    s_submitted = focused;
                    focused.SetInteraction(InteractionStates.Pressed, true);
                }
            }
            else if (!submit && s_submitting)
            {
                s_submitting = false;
                Release(tap: true);
            }
        }

        // A move by keys: what has focus takes it, if it takes moves of its own (a slider stepping); else focus moves.
        private static void Step(Vector2 direction)
        {
            var focused = Focused;
            if (focused != null)
            {
                focused.GetComponents(s_moveHandlers);
                bool taken = false;
                foreach (var handler in s_moveHandlers)
                {
                    if (handler is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                    if (!handler.OnFocusMove(direction)) continue;
                    taken = true;
                    break;
                }
                s_moveHandlers.Clear();
                if (taken) return;
            }
            Move(direction);
        }

        // Lets go of what a submit key pressed: tapped, if it still has focus and the key was let go of (rather than taken
        // by something typed into).
        private static void Release(bool tap)
        {
            var submitted = s_submitted;
            s_submitted = null;
            if (submitted == null) return;
            submitted.SetInteraction(InteractionStates.Pressed, false);
            if (tap && submitted == Focused && submitted.TryGetComponent(out Pressable pressable))
                pressable.Tap();
        }
    }
}
