using System;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// A switch: its Pressable toggles it, its track's fill follows Selected, and its knob springs to the other end as
    /// the node's alignment changes inside an Animate.
    /// </summary>
    [RequireComponent(typeof(Pressable))]
    public sealed class ToggleSwitch : MonoBehaviour
    {
        private Pressable _pressable;

        /// <summary>Raised as it is turned on or off.</summary>
        public event Action<bool> Changed;

        public bool IsOn => _pressable != null && _pressable.IsSelected;

        private void OnEnable()
        {
            _pressable = GetComponent<Pressable>();
            _pressable.Toggles = true;
            _pressable.Toggled += OnToggled;
        }

        private void OnDisable() => _pressable.Toggled -= OnToggled;

        private void OnToggled(bool on)
        {
            var node = _pressable.Node;
            UI.Animate(UIAnimation.Snappy, () => node.AlignX.Value = on ? AlignX.Right : AlignX.Left);
            Changed?.Invoke(on);
        }
    }
}
