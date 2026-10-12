using System;
using System.Collections.Generic;
using TimboJimbo.UINext;
using UnityEditor;

namespace TimboJimboEditor.UINext
{
    /// <summary>One state the preview turns on: an interaction state, or a state of your own (a mode is one too).</summary>
    public readonly struct PreviewState : IEquatable<PreviewState>
    {
        public readonly InteractionStates Interaction;
        public readonly UIState State;

        public PreviewState(InteractionStates interaction)
        {
            Interaction = interaction;
            State = null;
        }

        public PreviewState(UIState state)
        {
            Interaction = InteractionStates.None;
            State = state;
        }

        /// <summary>The variant condition for it alone.</summary>
        public StateCondition Condition => State != null ? new StateCondition(InteractionStates.None, State) : new StateCondition(Interaction);

        public string Name => State != null ? State.name : ObjectNames.NicifyVariableName(Interaction.ToString());

        public bool Equals(PreviewState other) => Interaction == other.Interaction && State == other.State;

        public override bool Equals(object obj) => obj is PreviewState other && Equals(other);

        public override int GetHashCode() => ((int)Interaction * 397) ^ (State != null ? State.GetHashCode() : 0);
    }

    /// <summary>
    /// States previewed in the editor (the UI States toolbar): interaction states, modes and states of your own turned on
    /// everywhere, for every node in every tree, so values with variants for them show as they would, in the Scene and
    /// Game views. Nothing on a node changes, so nothing is saved, and <see cref="Clear"/> turns them all off.
    /// While <see cref="Recording"/>, an edit to a value in the inspector becomes its variant for <see cref="Target"/>:
    /// the state turned on last, unless another that is on was picked since.
    /// </summary>
    public static class UIStatePreview
    {
        // What is previewed, in the order it was turned on.
        private static readonly List<PreviewState> s_on = new();
        private static PreviewState? s_target;
        private static bool s_recording;

        /// <summary>Raised when what is previewed, or recorded, changes.</summary>
        public static event Action Changed;

        /// <summary>Whether anything is being previewed.</summary>
        public static bool IsPreviewing => s_on.Count > 0;

        /// <summary>How many states are being previewed.</summary>
        public static int Count => s_on.Count;

        /// <summary>Whether a state is previewed.</summary>
        public static bool IsOn(PreviewState state) => s_on.Contains(state);

        /// <summary>
        /// Whether edits in the inspector are recorded: each value edited takes it as its variant for
        /// <see cref="Target"/>, or, with nothing previewed, as its own value, as ever.
        /// </summary>
        public static bool Recording
        {
            get => s_recording;
            set
            {
                if (s_recording == value) return;
                s_recording = value;
                Changed?.Invoke();
            }
        }

        /// <summary>The state edits are recorded for: the one turned on last, or picked since. None with nothing previewed.</summary>
        public static PreviewState? Target => s_target;

        /// <summary>The variant condition recorded into now: while recording a state, its own; otherwise none.</summary>
        public static StateCondition? RecordTarget => s_recording && s_target is { } target ? target.Condition : null;

        /// <summary>Turns a state on everywhere (it is then what is recorded), or off.</summary>
        public static void Toggle(PreviewState state)
        {
            if (s_on.Remove(state))
            {
                if (s_target is { } target && target.Equals(state))
                    s_target = s_on.Count > 0 ? s_on[s_on.Count - 1] : null;
            }
            else
            {
                s_on.Add(state);
                s_target = state;
            }
            Push();
        }

        /// <summary>Records into a state that is on, rather than the one turned on last.</summary>
        public static void SetTarget(PreviewState state)
        {
            if (!s_on.Contains(state) || (s_target is { } target && target.Equals(state))) return;
            s_target = state;
            Changed?.Invoke();
        }

        /// <summary>Turns every state the preview turned on off.</summary>
        public static void Clear()
        {
            s_on.Clear();
            s_target = null;
            Push();
        }

        // Hands what is on to the stack, which every node reads, and redraws.
        private static void Push()
        {
            var interaction = InteractionStates.None;
            var states = new List<UIState>();
            foreach (var state in s_on)
            {
                interaction |= state.Interaction;
                if (state.State != null)
                    states.Add(state.State);
            }
            StatePreview.Set(interaction, states);
            UI.RequestRepaint();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Changed?.Invoke();
        }
    }
}
