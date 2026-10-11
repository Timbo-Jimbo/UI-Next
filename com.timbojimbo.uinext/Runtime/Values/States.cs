using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The interaction states the stack's own components provide (Pressable, focus), weakest first but for FocusWithin,
    /// the weakest of all: when several variants could apply, one for a stronger state wins, so Disabled beats Pressed
    /// beats Hovered.
    /// </summary>
    [Flags]
    public enum InteractionStates
    {
        None = 0,
        Selected = 1,
        Hovered = 2,
        Focused = 4,
        Pressed = 8,
        Disabled = 16,

        /// <summary>
        /// Focus is on it or inside it, and shows, as CSS's :focus-within: a section or a group lit while focus is in it.
        /// Provided by focus scopes (<see cref="UINode.FocusScope"/>) and buttons. The weakest of them all.
        /// </summary>
        FocusWithin = 32,
    }

    /// <summary>What kind of condition a state is, which decides its priority against others.</summary>
    public enum StateKind
    {
        /// <summary>An environment mode (light or dark, a size class): lowest priority, and what tokens switch on.</summary>
        Mode,

        /// <summary>A game state (locked, owned, a rarity): above modes, below interaction states.</summary>
        State,
    }

    /// <summary>An animation that may be left unset.</summary>
    [Serializable]
    public struct OptionalAnimation
    {
        [SerializeField] private bool _set;
        [SerializeField] private UIAnimation _animation;

        public OptionalAnimation(UIAnimation? animation)
        {
            _set = animation.HasValue;
            _animation = animation ?? UIAnimation.Default;
        }

        public UIAnimation? Value => _set ? _animation : null;
    }

    /// <summary>The states a variant applies in: all of its interaction states and all of its custom states at once.</summary>
    [Serializable]
    public struct StateCondition
    {
        public InteractionStates Interaction;
        public UIState[] States;

        public StateCondition(InteractionStates interaction, params UIState[] states)
        {
            Interaction = interaction;
            States = states;
        }

        public bool IsEmpty => Interaction == InteractionStates.None && (States == null || States.Length == 0);

        // Its priority: the highest level among its conditions (modes 0, states 1, interaction states 10 up from the
        // weakest), then how many conditions it has, so a combination beats a single state at the same level.
        internal int Level
        {
            get
            {
                int level = -1;
                if (States != null)
                {
                    foreach (var state in States)
                        if (state != null)
                            level = Mathf.Max(level, state.Kind == StateKind.Mode ? 0 : 1);
                }
                int rank = InteractionAnimations.Rank(Interaction);
                if (rank >= 0)
                    level = Mathf.Max(level, 10 + rank);
                return level;
            }
        }

        internal int Count
        {
            get
            {
                int count = 0;
                for (int bits = (int)Interaction; bits != 0; bits &= bits - 1)
                    count++;
                if (States != null)
                {
                    foreach (var state in States)
                        if (state != null)
                            count++;
                }
                return count;
            }
        }
    }

    /// <summary>One per-state value of a <see cref="UIValue{T}"/>: in <see cref="When"/>, the value is this.</summary>
    [Serializable]
    public struct Variant<T>
    {
        public StateCondition When;
        public T Value;

        [Tooltip("Optional: a token to take the value from instead.")]
        public TokenAsset Token;

        public Variant(StateCondition when, T value)
        {
            When = when;
            Value = value;
            Token = null;
        }
    }

    /// <summary>The animations interaction states move values on outside any Animate: a press lands at once and springs back out.</summary>
    internal static class InteractionAnimations
    {
        public static UIAnimation? Enter(InteractionStates state) => state switch
        {
            InteractionStates.Pressed => null,
            InteractionStates.Hovered => new UIAnimation(0.12f),
            InteractionStates.Focused => new UIAnimation(0.15f),
            InteractionStates.FocusWithin => new UIAnimation(0.15f),
            InteractionStates.Selected => new UIAnimation(0.25f),
            InteractionStates.Disabled => new UIAnimation(0.2f),
            _ => null,
        };

        public static UIAnimation? Exit(InteractionStates state) => state switch
        {
            InteractionStates.Pressed => new UIAnimation(0.25f),
            InteractionStates.Hovered => new UIAnimation(0.2f),
            InteractionStates.Focused => new UIAnimation(0.2f),
            InteractionStates.FocusWithin => new UIAnimation(0.2f),
            InteractionStates.Selected => new UIAnimation(0.25f),
            InteractionStates.Disabled => new UIAnimation(0.2f),
            _ => null,
        };

        // The interaction states, weakest first: a variant for a later one wins over one for an earlier.
        private static readonly InteractionStates[] s_order =
        {
            InteractionStates.FocusWithin, InteractionStates.Selected, InteractionStates.Hovered, InteractionStates.Focused,
            InteractionStates.Pressed, InteractionStates.Disabled,
        };

        // Where the strongest interaction state in a set stands among them (0 the weakest), or -1 for none.
        public static int Rank(InteractionStates states)
        {
            for (int rank = s_order.Length - 1; rank >= 0; rank--)
                if ((states & s_order[rank]) != 0)
                    return rank;
            return -1;
        }

        // The strongest interaction state in a set.
        public static InteractionStates Highest(InteractionStates states)
        {
            int rank = Rank(states);
            return rank >= 0 ? s_order[rank] : InteractionStates.None;
        }
    }
}
