using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Order: overlapping cards, the one tapped raised above its siblings by its ZIndex (layout never moves); and a
    /// tooltip inside a clipped box that the switch lifts into the top layer, out of the clip, while it stays laid out
    /// where it is.
    /// </summary>
    public sealed class OrderDemo : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _cards = new();
        [SerializeField] private Pressable _lift;
        [SerializeField] private UINode _tooltip;

        private int _top;

        public void Setup(List<Pressable> cards, Pressable lift, UINode tooltip)
        {
            _cards = cards;
            _lift = lift;
            _tooltip = tooltip;
        }

        private void OnEnable()
        {
            foreach (var card in _cards)
            {
                var node = card.Node;
                card.Tapped += () =>
                {
                    node.ZIndex.Value = ++_top;
                    node.Scale.Value = 1.08f;
                    node.Scale.Settle();
                    UI.Animate(UIAnimation.Bouncy, () => node.Scale.Value = 1f);
                };
            }
            _lift.Toggled += on => _tooltip.TopLayer.Value = on;
        }
    }
}
