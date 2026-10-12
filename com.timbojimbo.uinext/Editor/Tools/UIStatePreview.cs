using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// States previewed in the editor (the UI Next toolbar's States): interaction states and states of your own turned
    /// on for a node and everything inside it, so values with variants for them show as they would, in the Scene and
    /// Game views. Only what a node provides at run time is changed, so nothing is saved, and <see cref="Clear"/> takes
    /// back everything the preview turned on.
    /// </summary>
    public static class UIStatePreview
    {
        // What the preview turned on, to take back: states set on nodes, interaction states set on providers, and nodes
        // made providers for the preview (a node with no button above it).
        private static readonly List<(UINode Node, UIState State)> s_states = new();
        private static readonly List<(UINode Node, InteractionStates State)> s_interaction = new();
        private static readonly List<UINode> s_providers = new();

        /// <summary>Raised when what is previewed changes.</summary>
        public static event System.Action Changed;

        /// <summary>Whether anything is being previewed.</summary>
        public static bool IsPreviewing => s_states.Count > 0 || s_interaction.Count > 0 || s_providers.Count > 0;

        /// <summary>How many states are being previewed: interaction states, and states and modes turned on.</summary>
        public static int Count
        {
            get
            {
                int count = s_interaction.Count;
                foreach (var (node, state) in s_states)
                    if (node != null && node.IsStateActive(state))
                        count++;
                return count;
            }
        }

        /// <summary>Whether an interaction state is active for <paramref name="node"/>, from the button providing it.</summary>
        public static bool IsActive(UINode node, InteractionStates state) => node != null && (node.ActiveInteraction() & state) != 0;

        /// <summary>Whether a state of your own is active for <paramref name="node"/>.</summary>
        public static bool IsActive(UINode node, UIState state) => node != null && state != null && node.IsStateActive(state);

        /// <summary>
        /// Turns an interaction state on or off for <paramref name="node"/>: on the node providing its interaction states
        /// (its button), or on the node itself, made to provide them for the preview, when nothing does.
        /// </summary>
        public static void Toggle(UINode node, InteractionStates state)
        {
            if (node == null) return;
            var provider = ProviderOf(node);
            if (provider == null)
            {
                provider = node;
                provider.AddInteractionProvider();
                s_providers.Add(provider);
            }
            bool on = (provider.Interaction & state) == 0;
            provider.SetInteraction(state, on);
            if (on)
                s_interaction.Add((provider, state));
            else
                s_interaction.RemoveAll(entry => entry.Node == provider && entry.State == state);
            Refresh();
        }

        /// <summary>Turns a state of your own (or a mode) on or off for <paramref name="node"/> and everything inside it.</summary>
        public static void Toggle(UINode node, UIState state)
        {
            if (node == null || state == null) return;
            bool on = !node.IsStateActive(state);
            node.SetState(state, on);
            if (!s_states.Contains((node, state)))
                s_states.Add((node, state));
            Refresh();
        }

        /// <summary>Takes back everything the preview turned on.</summary>
        public static void Clear()
        {
            foreach (var (node, state) in s_states)
                if (node != null)
                    node.ClearState(state);
            foreach (var (node, state) in s_interaction)
                if (node != null)
                    node.SetInteraction(state, false);
            foreach (var node in s_providers)
                if (node != null)
                    node.RemoveInteractionProvider();
            s_states.Clear();
            s_interaction.Clear();
            s_providers.Clear();
            Refresh();
        }

        // The node at or above it providing its interaction states, if any.
        private static UINode ProviderOf(UINode node)
        {
            for (var n = node; n != null; n = n.Parent)
                if (n.ProvidesInteraction)
                    return n;
            return null;
        }

        private static void Refresh()
        {
            UI.RequestRepaint();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Changed?.Invoke();
        }
    }
}
