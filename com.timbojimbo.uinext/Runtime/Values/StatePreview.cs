using System.Collections.Generic;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// States the editor previews everywhere (the UI States toolbar): every node in every tree has them on, over what it
    /// has itself, and a state previewed turns the others of its group off. Nothing on a node changes, so nothing is
    /// saved. What reads states reads them again as the preview changes.
    /// </summary>
    internal static class StatePreview
    {
        private static readonly Observable<int> s_revision = new();
        private static readonly List<UIState> s_states = new();
        private static InteractionStates s_interaction;

        /// <summary>The interaction states previewed. Tracked.</summary>
        public static InteractionStates Interaction
        {
            get
            {
                _ = s_revision.Value;
                return s_interaction;
            }
        }

        /// <summary>
        /// Whether the preview decides <paramref name="state"/>, and how: on while it is previewed, off while another of
        /// its group is. Tracked.
        /// </summary>
        public static bool Decides(UIState state, out bool on)
        {
            _ = s_revision.Value;
            on = false;
            if (s_states.Count == 0) return false;
            if (s_states.Contains(state))
            {
                on = true;
                return true;
            }
            if (string.IsNullOrEmpty(state.Group)) return false;
            foreach (var previewed in s_states)
                if (previewed != null && previewed.Group == state.Group)
                    return true;
            return false;
        }

        /// <summary>Previews these states everywhere, in place of those before.</summary>
        public static void Set(InteractionStates interaction, IEnumerable<UIState> states)
        {
            s_interaction = interaction;
            s_states.Clear();
            if (states != null)
                s_states.AddRange(states);
            s_revision.Value++;
        }
    }
}
