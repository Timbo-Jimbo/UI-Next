using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>Opens a label's links in the browser as they are tapped.</summary>
    [RequireComponent(typeof(Label))]
    public sealed class OpenLinks : MonoBehaviour
    {
        private void OnEnable() => GetComponent<Label>().LinkClicked += Application.OpenURL;

        private void OnDisable() => GetComponent<Label>().LinkClicked -= Application.OpenURL;
    }
}
