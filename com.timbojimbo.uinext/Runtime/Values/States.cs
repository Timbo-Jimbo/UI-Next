using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The interaction states the stack's own components provide (Pressable, Focusable), lowest priority first: when
    /// several variants could apply, one for a later state wins, so Disabled beats Pressed beats Hovered.
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
    }

    /// <summary>What kind of condition a state is, which decides its priority against others.</summary>
    public enum StateKind
    {
        /// <summary>An environment mode (light or dark, a size class): lowest priority, and what tokens switch on.</summary>
        Mode,

        /// <summary>A game state (locked, owned, a rarity): above modes, below interaction states.</summary>
        State,
    }

    /// <summary>
    /// A named condition a node provides to itself and everything inside it, as UIKit's traits and SwiftUI's
    /// environment: set it with <see cref="UINode.SetState"/>, and variants and tokens of nodes inside see it.
    /// States in the same <see cref="Group"/> are exclusive on a node (a rarity: common, rare, epic).
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/State", fileName = "State")]
    public sealed class UIState : ScriptableObject
    {
        [SerializeField] private StateKind _kind = StateKind.State;

        [Tooltip("States sharing a group are exclusive on a node: setting one clears the others there.")]
        [SerializeField] private string _group;

        [Tooltip("How values move as the state comes on, outside any Animate.")]
        [SerializeField] private OptionalAnimation _enter;

        [Tooltip("How values move as the state goes off, outside any Animate.")]
        [SerializeField] private OptionalAnimation _exit;

        public StateKind Kind => _kind;

        public string Group => _group;

        public UIAnimation? Enter => _enter.Value;

        public UIAnimation? Exit => _exit.Value;

        /// <summary>Makes a state in code (for states that need no asset).</summary>
        public static UIState Create(string name, StateKind kind = StateKind.State, string group = null,
            UIAnimation? enter = null, UIAnimation? exit = null)
        {
            var state = CreateInstance<UIState>();
            state.name = name;
            state.hideFlags = HideFlags.DontSave;
            state._kind = kind;
            state._group = group;
            state._enter = new OptionalAnimation(enter);
            state._exit = new OptionalAnimation(exit);
            return state;
        }
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

        // Its priority: the highest level among its conditions (modes 0, states 1, interaction states 10 up in their
        // order), then how many conditions it has, so a combination beats a single state at the same level.
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
                for (int bit = 4; bit >= 0; bit--)
                {
                    if (((int)Interaction & (1 << bit)) == 0) continue;
                    level = Mathf.Max(level, 10 + bit);
                    break;
                }
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
            InteractionStates.Selected => new UIAnimation(0.25f),
            InteractionStates.Disabled => new UIAnimation(0.2f),
            _ => null,
        };

        public static UIAnimation? Exit(InteractionStates state) => state switch
        {
            InteractionStates.Pressed => new UIAnimation(0.25f),
            InteractionStates.Hovered => new UIAnimation(0.2f),
            InteractionStates.Focused => new UIAnimation(0.2f),
            InteractionStates.Selected => new UIAnimation(0.25f),
            InteractionStates.Disabled => new UIAnimation(0.2f),
            _ => null,
        };

        // The highest interaction state in a set.
        public static InteractionStates Highest(InteractionStates states)
        {
            for (int bit = 4; bit >= 0; bit--)
                if (((int)states & (1 << bit)) != 0)
                    return (InteractionStates)(1 << bit);
            return InteractionStates.None;
        }
    }
}
