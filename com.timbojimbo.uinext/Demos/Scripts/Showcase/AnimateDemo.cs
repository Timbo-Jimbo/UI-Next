using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Animate: each track's ball goes to the other end on its own spring when the track is tapped (its alignment
    /// changes inside UI.Animate, and layout moves it); tapped again mid-flight, it turns round from where it is, at
    /// the speed it has. The card grows and shrinks, and what is below it moves with it.
    /// </summary>
    public sealed class AnimateDemo : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _tracks = new();
        [SerializeField] private List<UIAnimation> _animations = new();
        [SerializeField] private Pressable _card;

        public void Setup(List<Pressable> tracks, List<UIAnimation> animations, Pressable card)
        {
            _tracks = tracks;
            _animations = animations;
            _card = card;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _tracks.Count; i++)
            {
                var track = _tracks[i].Node;
                var animation = _animations[i];
                _tracks[i].Tapped += () => UI.Animate(animation, () =>
                    track.AlignX.Value = track.AlignX.Peek == AlignX.Left ? AlignX.Right : AlignX.Left);
            }
            var card = _card.Node;
            _card.Tapped += () => UI.Animate(UIAnimation.Bouncy, () =>
                card.Height.Value = card.Height.Peek.Value > 100f ? Sizing.Fixed(72f) : Sizing.Fixed(180f));
        }
    }
}
