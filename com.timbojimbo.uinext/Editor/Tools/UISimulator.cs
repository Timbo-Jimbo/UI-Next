using TimboJimbo.UINext;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// Plays the UI frame by frame outside play mode, as play mode would: changes animate, springs and glides step
    /// only when told to, and pointers are pressed, moved and let go of from code. For tests, and for capturing a
    /// change as a strip of frames. Begin, drive it, End.
    /// </summary>
    public static class UISimulator
    {
        private static double s_time;
        private static Vector2 s_size;

        /// <summary>The seconds simulated since <see cref="Begin"/>.</summary>
        public static double Time => s_time;

        /// <summary>Starts simulating, with the screen <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
        public static void Begin(int width = 720, int height = 1280)
        {
            s_time = 0;
            s_size = new Vector2(width, height);
            UIFrame.Simulating = true;
            UIFrame.ScreenSizeOverride = s_size;
            UIFrame.Advance(0f);
        }

        /// <summary>Stops simulating: whatever is moving lands, and the editor draws the UI as it is.</summary>
        public static void End()
        {
            UIInput.CancelAll();
            UIFrame.Simulating = false;
            UIFrame.ScreenSizeOverride = null;
            UIFrame.Advance(0f);
            UI.RequestRepaint();
        }

        /// <summary>Runs frames for <paramref name="seconds"/>, <paramref name="step"/> seconds apart.</summary>
        public static void Advance(float seconds, float step = 1f / 60f)
        {
            if (seconds <= 0f)
            {
                UIFrame.Advance(0f);
                return;
            }
            while (seconds > 1e-5f)
            {
                float dt = Mathf.Min(step, seconds);
                s_time += dt;
                UIFrame.Advance(dt);
                seconds -= dt;
            }
        }

        /// <summary>Where <paramref name="node"/>'s centre is drawn, in its root's points.</summary>
        public static Vector2 Center(UINode node)
        {
            var size = node.PresentedRect.size;
            var tree = node.Tree;
            if (tree == null || node.Index < 0) return node.LaidOutRect.center;
            return tree.World[node.Index].Apply(size * 0.5f);
        }

        /// <summary>Presses at <paramref name="point"/>: a finger, or the mouse or a pen for their ids (UIInput.MouseId, PenId).</summary>
        public static void Press(UIRoot root, Vector2 point, int pointer = UIInput.TouchIdBase) =>
            UIInput.Down(pointer, KindOf(pointer), ToScreen(root, point), s_time);

        private static PointerKind KindOf(int pointer) =>
            pointer == UIInput.MouseId ? PointerKind.Mouse : pointer == UIInput.PenId ? PointerKind.Pen : PointerKind.Touch;

        public static void Move(UIRoot root, Vector2 point, int pointer = UIInput.TouchIdBase) =>
            UIInput.Move(pointer, ToScreen(root, point), s_time);

        public static void Release(UIRoot root, Vector2 point, int pointer = UIInput.TouchIdBase) =>
            UIInput.Up(pointer, ToScreen(root, point), s_time);

        /// <summary>Hovers the mouse at <paramref name="point"/>.</summary>
        public static void Hover(UIRoot root, Vector2 point) => UIInput.Hover(ToScreen(root, point), true);

        /// <summary>Scrolls the mouse wheel at <paramref name="point"/> by <paramref name="notches"/> (positive scrolls back up).</summary>
        public static void Wheel(UIRoot root, Vector2 point, Vector2 notches) => UIInput.Scroll(ToScreen(root, point), notches * WheelUnit());

        /// <summary>Presses at <paramref name="point"/> and lets go a moment later.</summary>
        public static void Tap(UIRoot root, Vector2 point)
        {
            Press(root, point);
            Advance(0.05f);
            Release(root, point);
        }

        /// <summary>Drags from <paramref name="from"/> to <paramref name="to"/> over <paramref name="seconds"/>, then lets go.</summary>
        public static void Drag(UIRoot root, Vector2 from, Vector2 to, float seconds, bool release = true)
        {
            Press(root, from);
            int frames = Mathf.Max(1, Mathf.RoundToInt(seconds * 60f));
            for (int i = 1; i <= frames; i++)
            {
                Advance(1f / 60f);
                // Eased in and out, as a finger moves.
                float t = i / (float)frames;
                Move(root, Vector2.Lerp(from, to, t * t * (3f - 2f * t)));
            }
            if (release)
                Release(root, to);
        }

        /// <summary>Captures the frame as it is now to <paramref name="path"/>.</summary>
        public static void Capture(string path, Color background) => UICapture.Capture(path, background, (int)s_size.x, (int)s_size.y);

        private static Vector2 ToScreen(UIRoot root, Vector2 point)
        {
            float scale = root.PixelsPerPoint;
            return new Vector2(point.x * scale, root.PixelSize.y - point.y * scale);
        }

        private static float WheelUnit() =>
            UnityEngine.InputSystem.InputSystem.settings.scrollDeltaBehavior == UnityEngine.InputSystem.InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms ? 1f : 120f;
    }
}
