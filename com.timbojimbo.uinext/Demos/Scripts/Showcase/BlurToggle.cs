using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>Tapping its button blurs a node in or out of focus, the blur springing.</summary>
    [RequireComponent(typeof(Pressable))]
    public sealed class BlurToggle : MonoBehaviour
    {
        [SerializeField] private UINode _target;
        [SerializeField] private float _blur = 10f;

        public void Setup(UINode target, float blur)
        {
            _target = target;
            _blur = blur;
        }

        private void OnEnable() => GetComponent<Pressable>().Tapped += Toggle;

        private void OnDisable() => GetComponent<Pressable>().Tapped -= Toggle;

        private void Toggle() => UI.Animate(UIAnimation.Smooth, () => _target.Blur.Value = _target.Blur.Peek > 0f ? 0f : _blur);
    }
}
