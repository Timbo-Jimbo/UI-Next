using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A reaction under a post: tapping it toggles yours (its Pressable toggles, so it is Selected and its fill follows),
    /// its count is a binding that reads that state, and it pops as it changes.
    /// </summary>
    [RequireComponent(typeof(Pressable))]
    public sealed class ReactionChip : MonoBehaviour
    {
        [SerializeField] private Label _count;
        [SerializeField] private int _others;

        private Pressable _pressable;

        public void Setup(Label count, int others)
        {
            _count = count;
            _others = others;
        }

        private void OnEnable()
        {
            _pressable = GetComponent<Pressable>();
            var node = _pressable.Node;
            // Read inside the binding, the node's states are tracked: toggling it counts it again.
            _count.Text.Bind(() => (_others + ((node.ActiveInteraction() & InteractionStates.Selected) != 0 ? 1 : 0)).ToString());
            _pressable.Toggled += Pop;
        }

        private void OnDisable()
        {
            _pressable.Toggled -= Pop;
            _count.Text.Unbind();
        }

        private void Pop(bool on)
        {
            var node = _pressable.Node;
            node.Scale.Value = on ? 1.25f : 0.85f;
            node.Scale.Settle();
            UI.Animate(UIAnimation.Bouncy, () => node.Scale.Value = 1f);
        }
    }
}
