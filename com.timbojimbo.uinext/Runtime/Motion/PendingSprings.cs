using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>A value holding a change made on an animation before anything drew it (see <see cref="PendingSprings"/>).</summary>
    internal interface IPendingSpring
    {
        /// <summary>
        /// The frame <paramref name="frame"/> has been drawn: a change held since before it that its drawing did not read
        /// lands where it goes. True once it holds none.
        /// </summary>
        bool LandUnread(int frame);
    }

    /// <summary>
    /// Values changed on an animation while nothing drew them yet, as a node made this frame and animated in is: the
    /// first drawing to read one springs it from where it was, as Core Animation animates a layer added and changed in
    /// one go. Those the frame's drawing does not read land where they go once it is drawn.
    /// </summary>
    internal static class PendingSprings
    {
        private static readonly List<IPendingSpring> s_values = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_values.Clear();

        public static void Add(IPendingSpring value) => s_values.Add(value);

        /// <summary>Once the frame <paramref name="frame"/> is drawn.</summary>
        public static void LandUnread(int frame)
        {
            for (int i = s_values.Count - 1; i >= 0; i--)
            {
                if (s_values[i].LandUnread(frame))
                    s_values.RemoveAt(i);
            }
        }
    }
}
