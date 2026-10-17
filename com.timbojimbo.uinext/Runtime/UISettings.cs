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
        internal const int DefaultSharedMinCopies = 2;

        [Tooltip("How images with no loader of their own ask for what they load from URLs (a CDN's resizing). Empty: they load their URLs as they are.")]
        [SerializeField] private ImageLoader _defaultImageLoader;

        [Tooltip("How much of the images loaded from URLs that nothing shows is kept in memory for the next time one is asked for, in megabytes as drawn.")]
        [FormerlySerializedAs("_imageCacheMegabytes")]
        [SerializeField, Min(0)] private int _imageMemoryCacheMegabytes = DefaultImageMemoryCacheMegabytes;

        [Tooltip("How much of the images loaded from URLs is kept on disk between runs, in megabytes, the files used longest ago going first; 0 keeps none.")]
        [SerializeField, Min(0)] private int _imageDiskCacheMegabytes = DefaultImageDiskCacheMegabytes;

        [Tooltip("URP draws screen-space UGUI and UI Toolkit in a pass of its own after its final blit, which loads and stores the whole screen each frame even with nothing to draw (0.3 ms of GPU time on a Pixel 8). On, UI Next tells URP not to, and the engine draws any such UI after URP as it used to. Leave off in a project that draws UGUI or UI Toolkit overlays: drawn that way they are slower.")]
        [SerializeField] private bool _skipOverlayUIPass;

        [Tooltip("In the game view, the depth of every large opaque shape's inside is laid down before the UI is drawn, so what such a shape covers is not shaded. Off, everything is shaded. The command line's -uinext-noocc turns them off whatever this says.")]
        [SerializeField] private bool _occluders = true;

        [Header("Shared drawing")]
        [Tooltip("Copies of a thing named alike (UINode.SharedDrawing: a list's item cards) have what they draw the same, up to where they differ, drawn once: one copy's to an image, every copy as a quad showing it, with the rest (an icon, a label) drawn live over it. While on, the draw order is made twice, and again whenever anything is drawn again or moves. The command line's -uinext-noshare turns it off whatever this says.")]
        [SerializeField] private bool _sharedDrawing;

        [Tooltip("How many copies drawn alike there must be for them to share an image.")]
        [SerializeField, Min(2)] private int _sharedMinCopies = DefaultSharedMinCopies;

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

        /// <summary>
        /// Whether URP is told each frame not to record its screen-space overlay UI pass (for UGUI and UI Toolkit), which
        /// loads and stores the whole screen even with nothing to draw; the engine draws any such UI after URP instead.
        /// </summary>
        public bool SkipOverlayUIPass => _skipOverlayUIPass;

        /// <summary>Whether the occluders are drawn in the game view, so what a large opaque shape covers is not shaded.</summary>
        public bool Occluders => _occluders;

        /// <summary>Whether copies of a thing named alike (<see cref="UINode.SharedDrawing"/>) have what they draw the same drawn once, from an image.</summary>
        public bool SharedDrawing => _sharedDrawing;

        /// <summary>How many copies drawn alike there must be for them to share an image.</summary>
        public int SharedMinCopies => _sharedMinCopies;

        /// <summary>Whether the occluders are drawn: as the active settings say (on with no settings asset), unless the command line has -uinext-noocc.</summary>
        internal static bool Occluding => !CommandLine.NoOccluders && (Instance == null || Instance._occluders);

        /// <summary>Whether drawing is shared: as the active settings say (off with no settings asset), unless the command line has -uinext-noshare.</summary>
        internal static bool Sharing => !CommandLine.NoSharing && Instance != null && Instance._sharedDrawing;

        // Features turned off from the command line (on Android, the "unity" extra), read once: for measuring what one is
        // worth by turns in one build.
        private static class CommandLine
        {
            public static readonly bool NoOccluders = Has("-uinext-noocc");
            public static readonly bool NoSharing = Has("-uinext-noshare");

            private static bool Has(string flag) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), flag) >= 0;
        }

        /// <summary>Whether a field of the settings only means anything while drawing is shared (the settings page greys it out otherwise).</summary>
        internal static bool IsSharingField(string field) => field == nameof(_sharedMinCopies);

        /// <summary>The settings page's path in Project Settings.</summary>
        internal const string SettingsPath = "Project/Timbo Jimbo/UI Next";

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

        // Edits apply live; what is shared is found as the draw order is made.
        private void OnValidate()
        {
            if (this != s_instance) return;
            NotifyChanged();
            Compositor.MarkOrderDirty();
        }
#else
        // A player has the asset as a preloaded asset, loaded before the first scene; nothing else references it, so it
        // makes itself the instance here.
        private void OnEnable() => s_instance = this;
#endif
    }
}
