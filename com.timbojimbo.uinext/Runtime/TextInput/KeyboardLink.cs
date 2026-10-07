using TimboJimbo.UI.TextInput;
using UnityEngine;

namespace TimboJimbo.UINext
{
    // Hands the platform's keyboard height (UI Text Input's, following the keyboard as it slides) to layout, where nodes
    // that avoid the keyboard keep their content above it.
    internal static class KeyboardLink
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            TextInputSystem.KeyboardChanged -= OnKeyboardChanged;
            TextInputSystem.KeyboardChanged += OnKeyboardChanged;
            UIRoot.KeyboardHeight = TextInputSystem.KeyboardHeight;
        }

        private static void OnKeyboardChanged() => UIRoot.KeyboardHeight = TextInputSystem.KeyboardHeight;
    }
}
