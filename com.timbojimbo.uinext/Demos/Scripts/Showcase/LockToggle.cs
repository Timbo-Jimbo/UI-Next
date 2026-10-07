using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>Tapping it locks or unlocks it: grey and dimmed while locked, the filters springing.</summary>
    [RequireComponent(typeof(Pressable))]
    public sealed class LockToggle : MonoBehaviour
    {
        private bool _locked;

        private void OnEnable() => GetComponent<Pressable>().Tapped += Toggle;

        private void OnDisable() => GetComponent<Pressable>().Tapped -= Toggle;

        private void Toggle()
        {
            _locked = !_locked;
            var node = GetComponent<UINode>();
            UI.Animate(UIAnimation.Smooth, () =>
            {
                node.Saturation.Value = _locked ? 0f : 1f;
                node.Brightness.Value = _locked ? 0.7f : 1f;
            });
        }
    }
}
