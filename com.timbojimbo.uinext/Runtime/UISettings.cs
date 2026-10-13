using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Project-wide UI Next settings, edited under Edit > Project Settings > Timbo Jimbo > UI Next. The asset lives
    /// anywhere under Assets; the settings page registers it as an EditorBuildSettings config object (stored in
    /// ProjectSettings/EditorBuildSettings.asset) and builds preload it, as UI Next Text's settings are.
    /// </summary>
    public sealed class UISettings : ScriptableObject
    {
        /// <summary>The EditorBuildSettings config object key the active asset is registered under.</summary>
        public const string ConfigObjectKey = "com.timbojimbo.uinext.settings";

        internal const int DefaultImageCacheMegabytes = 64;

        [Tooltip("How images with no loader of their own ask for what they load from URLs (a CDN's resizing). Empty: they load their URLs as they are.")]
        [SerializeField] private ImageLoader _defaultImageLoader;

        [Tooltip("How much of the images loaded from URLs that nothing shows is kept for the next time one is asked for, in megabytes as drawn.")]
        [SerializeField, Min(0)] private int _imageCacheMegabytes = DefaultImageCacheMegabytes;

        private static UISettings s_instance;
        private static readonly Observable<int> s_changes = new();

        /// <summary>The active settings asset, or null when the project has none.</summary>
        public static UISettings Instance
        {
            get
            {
#if UNITY_EDITOR
                if (s_instance == null)
                    UnityEditor.EditorBuildSettings.TryGetConfigObject(ConfigObjectKey, out s_instance);
#endif
                return s_instance;
            }
        }

        /// <summary>How images with no loader of their own ask for what they load; null to load their URLs as they are.</summary>
        public ImageLoader DefaultImageLoader => _defaultImageLoader;

        /// <summary>How much of the images loaded from URLs that nothing shows is kept, in megabytes as drawn.</summary>
        public int ImageCacheMegabytes => _imageCacheMegabytes;

        /// <summary>A tracked read of the settings and the loaders they lead to: what reads it runs again as they change.</summary>
        internal static void Watch() => _ = s_changes.Value;

        /// <summary>Tells what reads the settings that they, or a loader, have changed.</summary>
        internal static void NotifyChanged() => s_changes.Value = s_changes.Peek + 1;

#if UNITY_EDITOR
        /// <summary>Called by the settings page after it changes the registered asset.</summary>
        internal static void SetActive(UISettings settings)
        {
            s_instance = settings;
            NotifyChanged();
        }

        // Edits apply live.
        private void OnValidate()
        {
            if (this == s_instance)
                NotifyChanged();
        }
#else
        // A player has the asset as a preloaded asset, loaded before the first scene; nothing else references it, so it
        // makes itself the instance here.
        private void OnEnable() => s_instance = this;
#endif
    }
}
