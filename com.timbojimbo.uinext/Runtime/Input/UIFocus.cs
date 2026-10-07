using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Keyboard and gamepad focus, as UIKit's focus engine: arrows, the d-pad or the left stick move it to the nearest
    /// button that way, as drawn; Tab and Shift Tab step through them in order; Enter, Space or a gamepad's South button
    /// (A) press the focused one. Only the topmost presentation's buttons can take it (a dialog keeps focus inside
    /// itself). It shows only while it is being used (focus-visible): the focused button is Focused while a key or a
    /// gamepad moved it there, and stops being so when a pointer is used. A button moved to inside a scroll view is
    /// scrolled into view. While something is typed into (<see cref="FirstResponder"/>), the keys are its own.
    /// </summary>
    public static class UIFocus
    {
        // Held, a direction repeats after this long, then this often.
        private const double RepeatDelay = 0.4;
        private const double RepeatInterval = 0.1;

        private static readonly List<Pressable> s_candidates = new();
        private static Pressable s_focused;
        private static bool s_visible;
        private static Vector2 s_heldDirection;
        private static double s_nextRepeat;
        private static bool s_submitting;
        private static Pressable s_firstResponder;

        // A submit key held since something was typed into, kept from pressing until it is let go.
        private static bool s_swallowing;

        // The frame something typed into last let go of the keys: they were its own all that frame.
        private static int s_resignedFrame = -1;

        /// <summary>Raised when focus moves, with the button it moved to (null when it is cleared).</summary>
        public static event Action<Pressable> Changed;

        /// <summary>The focused button, or null.</summary>
        public static Pressable Focused => s_focused != null ? s_focused : null;

        /// <summary>Whether focus is showing: a key or a gamepad moved it, and no pointer has been used since.</summary>
        public static bool IsVisible => s_visible && Focused != null;

        /// <summary>
        /// What is being typed into, as UIKit's first responder: the button of a text field being edited, or null.
        /// Setting it focuses it. While there is one, the keys are its own: the arrows, Enter and Space neither move nor
        /// press focus, and Escape does not go back (the field ends its editing). Tab still moves focus on, and focus
        /// moving off it, or a gamepad's East button (B), lets it go. It shows as Focused while it is typed into,
        /// whatever is used meanwhile, as a browser shows a text box's focus.
        /// </summary>
        public static Pressable FirstResponder
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
                    previous.Node.SetInteraction(InteractionStates.Focused, previous == Focused && s_visible);
                }
                if (value != null)
                    Focus(value, s_visible);
            }
        }

        // Whether the keys are something's to type with this frame: it is typed into, or let go of them this frame.
        internal static bool KeysTaken => FirstResponder != null || s_resignedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_focused = null;
            s_visible = false;
            s_heldDirection = Vector2.zero;
            s_submitting = false;
            s_firstResponder = null;
            s_swallowing = false;
            s_resignedFrame = -1;
        }

        /// <summary>Focuses <paramref name="target"/>, showing it unless told not to.</summary>
        public static void Focus(Pressable target, bool visible = true)
        {
            if (Focused != target)
            {
                // Focus moving off what is typed into lets it go.
                if (FirstResponder != null && target != FirstResponder)
                {
                    var responder = FirstResponder;
                    s_firstResponder = null;
                    s_resignedFrame = Time.frameCount;
                    responder.Node.SetInteraction(InteractionStates.Focused, false);
                }
                SetShown(false);
                s_focused = target;
                if (target != null)
                    ScrollIntoView(target.Node);
                Changed?.Invoke(target);
            }
            s_visible = visible && target != null;
            SetShown(s_visible);
        }

        /// <summary>Lets go of focus.</summary>
        public static void Clear() => Focus(null, false);

        /// <summary>
        /// Moves focus to the nearest button in <paramref name="direction"/> (x right, y down) from the focused one, as
        /// drawn; with none focused, to the first. False if there is nowhere to go.
        /// </summary>
        public static bool Move(Vector2 direction)
        {
            Collect();
            if (s_candidates.Count == 0) return false;
            var current = Focused;
            if (current == null || !s_candidates.Contains(current))
            {
                Focus(s_candidates[0]);
                return true;
            }
            var from = current.Node.DrawnRect;
            Pressable best = null;
            float bestScore = float.MaxValue;
            foreach (var candidate in s_candidates)
            {
                if (candidate == current) continue;
                float score = Score(from, candidate.Node.DrawnRect, direction);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            if (best == null)
            {
                // Nowhere to go: it shows where it is.
                Focus(current);
                return false;
            }
            Focus(best);
            return true;
        }

        /// <summary>Moves focus to the next button in order (or the previous one), wrapping round.</summary>
        public static bool Next(bool backwards = false)
        {
            Collect();
            if (s_candidates.Count == 0) return false;
            int index = Focused != null ? s_candidates.IndexOf(Focused) : -1;
            index = index < 0 ? (backwards ? s_candidates.Count - 1 : 0)
                : (index + (backwards ? -1 : 1) + s_candidates.Count) % s_candidates.Count;
            Focus(s_candidates[index]);
            return true;
        }

        // How far a candidate is that way: along the direction, and (counting double) across it; out of the running if
        // it is not that way at all. Rects overlapping across the direction count as straight ahead.
        private static float Score(Rect from, Rect to, Vector2 direction)
        {
            var delta = to.center - from.center;
            float along = Vector2.Dot(delta, direction);
            if (along <= 1f) return float.MaxValue;
            bool horizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            float across = horizontal ? Mathf.Abs(delta.y) : Mathf.Abs(delta.x);
            bool overlaps = horizontal
                ? to.yMin < from.yMax && to.yMax > from.yMin
                : to.xMin < from.xMax && to.xMax > from.xMin;
            return along + (overlaps ? 0f : across * 2f);
        }

        // The buttons focus can go to: those drawn in the topmost level of the topmost root that has any, taking
        // presses, in drawing order.
        private static void Collect()
        {
            s_candidates.Clear();
            var trees = UITree.Trees;
            for (int t = trees.Count - 1; t >= 0 && s_candidates.Count == 0; t--)
            {
                var tree = trees[t];
                if (tree.Root == null || !tree.Root.isActiveAndEnabled) continue;
                for (int level = tree.LevelStarts.Count - 1; level >= 0 && s_candidates.Count == 0; level--)
                {
                    if (level < tree.LevelInteractive.Count && !tree.LevelInteractive[level]) continue;
                    int start = tree.LevelStarts[level];
                    int end = level + 1 < tree.LevelStarts.Count ? tree.LevelStarts[level + 1] : tree.DrawOrder.Count;
                    for (int k = start; k < end; k++)
                    {
                        int index = tree.DrawOrder[k];
                        if (index >= tree.Nodes.Count) continue;
                        var node = tree.Nodes[index];
                        if (node == null || !node.TryGetComponent(out Pressable pressable)) continue;
                        if (pressable.isActiveAndEnabled && pressable.Interactable.Peek && node.HitTestable.Peek)
                            s_candidates.Add(pressable);
                    }
                }
            }
        }

        private static void SetShown(bool shown)
        {
            var focused = Focused;
            if (focused != null)
                focused.Node.SetInteraction(InteractionStates.Focused, shown || focused == FirstResponder);
        }

        // Scrolls every scroll view the node is inside, innermost first, until it is in view.
        private static void ScrollIntoView(UINode node)
        {
            UINode child = node;
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
            {
                if (parent.Scroller != null && parent.Scroller.isActiveAndEnabled)
                    parent.Scroller.ScrollIntoView(child);
                child = parent;
            }
        }

        // ── Input ────────────────────────────────────────────────────────────────

        /// <summary>A pointer was used: focus stops showing.</summary>
        internal static void PointerUsed()
        {
            if (!s_visible) return;
            s_visible = false;
            SetShown(false);
        }

        /// <summary>Once a frame in play mode: directions (repeating while held), Tab, and submit.</summary>
        internal static void Poll(double now)
        {
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
                if (keyboard.tabKey.wasPressedThisFrame)
                    Next(keyboard.shiftKey.isPressed);
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
                if (s_submitting && Focused != null)
                    Focused.Node.SetInteraction(InteractionStates.Pressed, false);
                s_submitting = false;
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
                    Move(direction);
            }
            else if (direction != Vector2.zero && now >= s_nextRepeat)
            {
                s_nextRepeat = now + RepeatInterval;
                Move(direction);
            }

            var focused = Focused;
            if (submit && !s_submitting)
            {
                s_submitting = true;
                if (focused != null && focused.Interactable.Peek)
                {
                    s_visible = true;
                    SetShown(true);
                    focused.Node.SetInteraction(InteractionStates.Pressed, true);
                }
            }
            else if (!submit && s_submitting)
            {
                s_submitting = false;
                if (focused != null)
                {
                    focused.Node.SetInteraction(InteractionStates.Pressed, false);
                    focused.Tap();
                }
            }
        }
    }
}
