using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The states UI Next sets itself, for variants to follow: assets in the package, so variants set in the inspector
    /// can name them too.
    /// </summary>
    public static class UIStates
    {
        private static UIState s_loading;
        private static UIState s_failed;

        /// <summary>On an image's node, and everything inside it, while the image at its URL loads.</summary>
        public static UIState Loading => s_loading != null ? s_loading : s_loading = Load("Loading");

        /// <summary>On an image's node, and everything inside it, when the image at its URL could not be loaded.</summary>
        public static UIState Failed => s_failed != null ? s_failed : s_failed = Load("Failed");

        private static UIState Load(string name) => Resources.Load<UIState>("UINext/States/" + name);
    }
}
