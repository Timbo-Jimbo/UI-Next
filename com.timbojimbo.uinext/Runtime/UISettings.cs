using UnityEngine;
using UnityEngine.Serialization;

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

        internal const int DefaultImageMemoryCacheMegabytes = 64;
        internal const int DefaultImageDiskCacheMegabytes = 256;

        [Tooltip("How images with no loader of their own ask for what they load from URLs (a CDN's resizing). Empty: they load their URLs as they are.")]
        [SerializeField] private ImageLoader _defaultImageLoader;

        [Tooltip("How much of the images loaded from URLs that nothing shows is kept in memory for the next time one is asked for, in megabytes as drawn.")]
        [FormerlySerializedAs("_imageCacheMegabytes")]
        [SerializeField, Min(0)] private int _imageMemoryCacheMegabytes = DefaultImageMemoryCacheMegabytes;

        [Tooltip("How much of the images loaded from URLs is kept on disk between runs, in megabytes, the files used longest ago going first; 0 keeps none.")]
        [SerializeField, Min(0)] private int _imageDiskCacheMegabytes = DefaultImageDiskCacheMegabytes;

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

        /// <summary>How much of the images loaded from URLs that nothing shows is kept in memory, in megabytes as drawn.</summary>
        public int ImageMemoryCacheMegabytes => _imageMemoryCacheMegabytes;

        /// <summary>How much of the images loaded from URLs is kept on disk between runs, in megabytes; 0 keeps none.</summary>
        public int ImageDiskCacheMegabytes => _imageDiskCacheMegabytes;

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
