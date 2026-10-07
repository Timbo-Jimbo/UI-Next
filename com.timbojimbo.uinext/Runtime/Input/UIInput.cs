using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Pointer input, straight from the Input System: the mouse, pens and every finger. A pointer going down is hit
    /// tested against what was drawn (presented transforms, clips, opacity, draw order, roots in their order) and the
    /// node it lands on, with its ancestors, hears it through their <see cref="IPointerHandler"/>s, deepest first,
    /// until one claims it. The mouse also hovers (<see cref="IHoverHandler"/>) and scrolls (<see cref="IScrollHandler"/>).
    /// Runs once a frame, after the Input System updates and before scripts' Update, in play mode.
    /// </summary>
    public static class UIInput
    {
        /// <summary>The mouse's pointer id; a pen's is <see cref="PenId"/>, a finger's its touch id plus <see cref="TouchIdBase"/>.</summary>
        public const int MouseId = 0;
        public const int PenId = 1;
        public const int TouchIdBase = 100;

        // How far a scroll wheel's notch scrolls, in points.
        private const float PointsPerNotch = 48f;

        private sealed class Track
        {
            public int PointerId;
            public PointerKind Kind;
            public UIRoot Root;
            public Vector2 Start;
            public Vector2 Last;
            public double LastTime;
            public UINode Node;
            public readonly List<IPointerHandler> Handlers = new();
            public IPointerHandler Owner;
            public object Tapper;
            public readonly VelocityTracker Velocity = new();

            public PointerEvent Event(Vector2 point, double time) =>
                new(PointerId, Kind, Root, point, Start, time, Velocity.Velocity, Node);
        }

        private static readonly Dictionary<int, Track> s_tracks = new();
        private static readonly List<int> s_trackIds = new();
        private static readonly List<IPointerHandler> s_handlers = new();
        private static readonly List<IPointerHandler> s_calling = new();

        // Lists of handlers being called, lent out so a handler that sets off more calls (a claim, a move) gets its own,
        // and given back for the next pointer event: moves come every frame a pointer is down.
        private static readonly Stack<List<IPointerHandler>> s_lists = new();

        private static List<IPointerHandler> Borrow(List<IPointerHandler> from)
        {
            var list = s_lists.Count > 0 ? s_lists.Pop() : new List<IPointerHandler>();
            list.AddRange(from);
            return list;
        }

        private static void GiveBack(List<IPointerHandler> list)
        {
            list.Clear();
            s_lists.Push(list);
        }
        private static readonly List<IHoverHandler> s_hovered = new();
        private static readonly List<IHoverHandler> s_hovering = new();
        private static readonly List<IHoverHandler> s_hoverScratch = new();
        private static readonly List<IScrollHandler> s_scrollScratch = new();
        private static bool s_mouseDown;
        private static bool s_penDown;

        /// <summary>
        /// Raised when back is asked for: Escape, a gamepad's East button (B), Android's back. Handlers that act on it
        /// (the topmost presentation dismissing) set the flag so the rest leave it; the latest added hears it first.
        /// </summary>
        public static event Func<bool> Back;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_tracks.Clear();
            s_hovered.Clear();
            s_mouseDown = s_penDown = false;
        }

        // Hooked again on every entry to play mode, as well as when the first root wakes in the editor: without a domain
        // reload, nothing else says the Input System still holds the callback.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Hook()
        {
            InputSystem.onAfterUpdate -= OnAfterUpdate;
            InputSystem.onAfterUpdate += OnAfterUpdate;
        }

        /// <summary>Whether a pointer at <paramref name="screen"/> (pixels, y up) is over something the UI draws and takes the pointer.</summary>
        public static bool IsOverUI(Vector2 screen) => HitTest(screen, out _, out _, out _);

        /// <summary>Whether the pointer <paramref name="pointerId"/> went down on the UI and is still down.</summary>
        public static bool IsTracking(int pointerId) => s_tracks.ContainsKey(pointerId);

        /// <summary>
        /// Takes the pointer for <paramref name="handler"/>: every other handler hearing it is cancelled, and only this
        /// one hears it from now on. False if another already has it.
        /// </summary>
        public static bool Claim(IPointerHandler handler, int pointerId)
        {
            if (!s_tracks.TryGetValue(pointerId, out var track)) return false;
            if (track.Owner != null) return track.Owner == handler;
            if (!track.Handlers.Contains(handler)) return false;
            track.Owner = handler;
            // A list of its own: a claim is often made from inside a handler Down is calling, from s_calling.
            var others = Borrow(track.Handlers);
            others.Remove(handler);
            track.Handlers.Clear();
            track.Handlers.Add(handler);
            var e = track.Event(track.Last, track.LastTime);
            foreach (var other in others)
                if (Alive(other))
                    other.OnPointerCancel(in e);
            GiveBack(others);
            return true;
        }

        /// <summary>
        /// Makes <paramref name="handler"/> the one a tap of this pointer goes to, unless one deeper already is: so a
        /// button inside a button takes the tap, not both.
        /// </summary>
        public static bool TakeTap(object handler, int pointerId)
        {
            if (!s_tracks.TryGetValue(pointerId, out var track)) return false;
            track.Tapper ??= handler;
            return track.Tapper == handler;
        }

        /// <summary>
        /// Whether a pointer at <paramref name="point"/> (its root's points) is over <paramref name="node"/> as drawn,
        /// give or take <paramref name="slop"/> points.
        /// </summary>
        public static bool IsOver(UINode node, Vector2 point, float slop = 0f) =>
            node != null && node.Tree != null && node.Index >= 0 && node.Tree.Contains(node.Index, point, slop);

        /// <summary>A movement in a root's points as a movement in <paramref name="node"/>'s own space, as it is drawn.</summary>
        public static Vector2 ToNodeVector(UINode node, Vector2 rootVector)
        {
            if (node == null || node.Tree == null || node.Index < 0) return rootVector;
            return node.Tree.World[node.Index].Inverse.ApplyVector(rootVector);
        }

        // ── Polling ──────────────────────────────────────────────────────────────

        private static void OnAfterUpdate()
        {
            // Once a frame (or each fixed step, if the Input System runs there), not for the editor's own updates.
            var update = InputState.currentUpdateType;
            if (!Application.isPlaying || update == InputUpdateType.Editor || update == InputUpdateType.BeforeRender) return;
            Poll();
        }

        private static void Poll()
        {
            double now = Time.realtimeSinceStartupAsDouble;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var screen = mouse.position.ReadValue();
                bool down = mouse.leftButton.isPressed;
                if (down && !s_mouseDown) Down(MouseId, PointerKind.Mouse, screen, now);
                else if (down) Move(MouseId, screen, now);
                else if (s_mouseDown) Up(MouseId, screen, now);
                s_mouseDown = down;
                Hover(screen, OnScreen(screen));
                var scroll = mouse.scroll.ReadValue();
                if (scroll != Vector2.zero && OnScreen(screen))
                    Scroll(screen, scroll);
            }

            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool east = gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
            // While something is typed into, Escape is its own (it ends the editing) and B lets it go, as Escape would.
            if (UIFocus.KeysTaken)
            {
                if (east)
                    UIFocus.FirstResponder = null;
            }
            else if (escape || east)
            {
                GoBack();
            }
            UIFocus.Poll(now);

            var pen = Pen.current;
            if (pen != null)
            {
                var screen = pen.position.ReadValue();
                bool down = pen.tip.isPressed;
                if (down && !s_penDown) Down(PenId, PointerKind.Pen, screen, now);
                else if (down) Move(PenId, screen, now);
                else if (s_penDown) Up(PenId, screen, now);
                s_penDown = down;
            }

            if (Touchscreen.current != null)
            {
                if (!EnhancedTouchSupport.enabled)
                    EnhancedTouchSupport.Enable();
                foreach (var touch in Touch.activeTouches)
                {
                    int id = TouchIdBase + touch.touchId;
                    var screen = touch.screenPosition;
                    switch (touch.phase)
                    {
                        case UnityEngine.InputSystem.TouchPhase.Began:
                            Down(id, PointerKind.Touch, screen, touch.time);
                            break;
                        case UnityEngine.InputSystem.TouchPhase.Moved:
                        case UnityEngine.InputSystem.TouchPhase.Stationary:
                            Move(id, screen, touch.time);
                            break;
                        case UnityEngine.InputSystem.TouchPhase.Ended:
                            Up(id, screen, touch.time);
                            break;
                        case UnityEngine.InputSystem.TouchPhase.Canceled:
                            Cancel(id, touch.time);
                            break;
                    }
                }
            }
        }

        private static bool OnScreen(Vector2 screen)
        {
            var size = UIFrame.ScreenPixelSize();
            return screen.x >= 0f && screen.y >= 0f && screen.x <= size.x && screen.y <= size.y;
        }

        // ── Pointers ─────────────────────────────────────────────────────────────

        internal static void Down(int pointerId, PointerKind kind, Vector2 screen, double time)
        {
            UIFocus.PointerUsed();
            if (s_tracks.ContainsKey(pointerId))
                Cancel(pointerId, time);
            if (!HitTest(screen, out var tree, out var node, out var point)) return;

            var track = new Track
            {
                PointerId = pointerId,
                Kind = kind,
                Root = tree.Root,
                Start = point,
                Last = point,
                LastTime = time,
                Node = node,
            };
            for (var n = node; n != null; n = n.ParentNode)
            {
                n.GetComponents(s_handlers);
                foreach (var handler in s_handlers)
                    if (Alive(handler))
                        track.Handlers.Add(handler);
            }
            s_handlers.Clear();
            if (track.Handlers.Count == 0) return;

            s_tracks[pointerId] = track;
            track.Velocity.Add(point, time);
            var e = track.Event(point, time);
            s_calling.Clear();
            s_calling.AddRange(track.Handlers);
            foreach (var handler in s_calling)
            {
                // A handler that claimed it as it went down (a gliding scroll caught) takes it from the rest unheard.
                if (track.Owner != null && track.Owner != handler) break;
                if (Alive(handler) && track.Handlers.Contains(handler))
                    handler.OnPointerDown(in e);
                if (!s_tracks.ContainsKey(pointerId)) break;
            }
            s_calling.Clear();
        }

        internal static void Move(int pointerId, Vector2 screen, double time)
        {
            if (!s_tracks.TryGetValue(pointerId, out var track)) return;
            if (track.Root == null || !track.Root.isActiveAndEnabled)
            {
                Cancel(pointerId, time);
                return;
            }
            var point = track.Root.ScreenToRoot(screen);
            if (point == track.Last) return;
            track.Last = point;
            track.LastTime = time;
            track.Velocity.Add(point, time);
            var e = track.Event(point, time);
            var calling = Borrow(track.Handlers);
            foreach (var handler in calling)
            {
                if (!s_tracks.ContainsKey(pointerId) || !track.Handlers.Contains(handler)) continue;
                if (Alive(handler))
                    handler.OnPointerMove(in e);
            }
            GiveBack(calling);
        }

        internal static void Up(int pointerId, Vector2 screen, double time)
        {
            if (!s_tracks.TryGetValue(pointerId, out var track)) return;
            s_tracks.Remove(pointerId);
            var point = track.Root != null ? track.Root.ScreenToRoot(screen) : track.Last;
            track.Velocity.Add(point, time);
            var e = track.Event(point, time);
            var calling = Borrow(track.Handlers);
            foreach (var handler in calling)
                if (Alive(handler))
                    handler.OnPointerUp(in e);
            GiveBack(calling);
        }

        internal static void Cancel(int pointerId, double time)
        {
            if (!s_tracks.TryGetValue(pointerId, out var track)) return;
            s_tracks.Remove(pointerId);
            var e = track.Event(track.Last, time);
            var calling = Borrow(track.Handlers);
            foreach (var handler in calling)
                if (Alive(handler))
                    handler.OnPointerCancel(in e);
            GiveBack(calling);
        }

        /// <summary>Asks for back, as Escape does: the latest handler that takes it acts on it.</summary>
        public static bool GoBack()
        {
            if (Back == null) return false;
            var handlers = Back.GetInvocationList();
            for (int i = handlers.Length - 1; i >= 0; i--)
                if (((Func<bool>)handlers[i])())
                    return true;
            return false;
        }

        /// <summary>Cuts short every pointer that is down (a screen covering everything, focus lost).</summary>
        public static void CancelAll()
        {
            s_trackIds.Clear();
            s_trackIds.AddRange(s_tracks.Keys);
            foreach (int id in s_trackIds)
                Cancel(id, Time.realtimeSinceStartupAsDouble);
        }

        private static bool Alive(object handler) => handler is not Behaviour behaviour || (behaviour != null && behaviour.isActiveAndEnabled);

        // ── Hover and wheel ──────────────────────────────────────────────────────

        internal static void Hover(Vector2 screen, bool present)
        {
            s_hovering.Clear();
            if (present && HitTest(screen, out _, out var node, out _))
            {
                for (var n = node; n != null; n = n.ParentNode)
                {
                    n.GetComponents(s_hoverScratch);
                    foreach (var handler in s_hoverScratch)
                        if (Alive(handler))
                            s_hovering.Add(handler);
                }
                s_hoverScratch.Clear();
            }
            foreach (var handler in s_hovered)
                if (!s_hovering.Contains(handler) && Alive(handler))
                    handler.OnHoverChanged(false);
            foreach (var handler in s_hovering)
                if (!s_hovered.Contains(handler))
                    handler.OnHoverChanged(true);
            s_hovered.Clear();
            s_hovered.AddRange(s_hovering);
        }

        internal static void Scroll(Vector2 screen, Vector2 wheel)
        {
            if (!HitTest(screen, out _, out var node, out _)) return;
            // Notches (or the platform's own units, a notch being 120), turned to points; up the wheel scrolls back up.
            var notches = InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms
                ? wheel
                : wheel / 120f;
            var delta = new Vector2(notches.x, -notches.y) * PointsPerNotch;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.shiftKey.isPressed && delta.x == 0f)
                delta = new Vector2(delta.y, 0f);
            for (var n = node; n != null; n = n.ParentNode)
            {
                n.GetComponents(s_scrollScratch);
                foreach (var handler in s_scrollScratch)
                {
                    if (!Alive(handler) || !handler.OnScroll(delta)) continue;
                    s_scrollScratch.Clear();
                    return;
                }
            }
            s_scrollScratch.Clear();
        }

        // ── Hit testing ──────────────────────────────────────────────────────────

        /// <summary>The node drawn topmost at <paramref name="screen"/> that takes the pointer, over every root.</summary>
        internal static bool HitTest(Vector2 screen, out UITree tree, out UINode node, out Vector2 point)
        {
            var trees = UITree.Trees;
            for (int t = trees.Count - 1; t >= 0; t--)
            {
                var candidate = trees[t];
                if (candidate.Root == null || !candidate.Root.isActiveAndEnabled) continue;
                var p = candidate.Root.ScreenToRoot(screen);
                int index = candidate.HitTest(p);
                if (index < 0) continue;
                tree = candidate;
                node = candidate.Nodes[index];
                point = p;
                return true;
            }
            tree = null;
            node = null;
            point = default;
            return false;
        }
    }
}
