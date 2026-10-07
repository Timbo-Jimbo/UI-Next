using UnityEngine;

namespace TimboJimbo.UINext
{
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

        /// <summary>How values move as the state comes on, outside any Animate; null for at once.</summary>
        public UIAnimation? Enter
        {
            get => _enter.Value;
            set => _enter = new OptionalAnimation(value);
        }

        /// <summary>How values move as the state goes off, outside any Animate; null for at once.</summary>
        public UIAnimation? Exit
        {
            get => _exit.Value;
            set => _exit = new OptionalAnimation(value);
        }

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
}
