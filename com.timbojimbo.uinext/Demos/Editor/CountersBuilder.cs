using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Counters;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>
    /// Builds the counters scene (a camera, and a root whose <see cref="CountersApp"/> builds labels changing every frame
    /// as it starts), for profiling what a change of text costs, and an Android development player of it with deep
    /// profiling support that connects to the Profiler, an app of its own.
    /// </summary>
    public static class CountersBuilder
    {
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Counters.unity";

        private const string ApplicationId = "com.timbojimbo.uinext.counters";
        private const string ProductName = "UI Next Counters";

        [MenuItem("Tools/UI Next/Demos/Build Counters")]
        private static void BuildAndOpen() => Build(open: true);

        public static void Build(bool open) => DemoKit.BuildScene(ScenePath, _ => BuildScene(), open);

        private static void BuildScene()
        {
            DemoKit.Camera(Color.white);
            DemoKit.Root("Counters").Node.Add<CountersApp>();
        }

        [MenuItem("Tools/UI Next/Demos/Build Counters Player (Android, deep profiling)")]
        private static void BuildPlayerFromMenu()
        {
            var apk = EditorUtility.SaveFilePanel("Build the counters player", "", "UINextCounters", "apk");
            if (!string.IsNullOrEmpty(apk))
                Debug.Log("UI Next counters: " + BuildPlayer(apk));
        }

        /// <summary>Builds its Android development player to <paramref name="apk"/>, with deep profiling, its scene first if it has none.</summary>
        public static string BuildPlayer(string apk)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                Build(open: false);
            return BenchmarkBuilder.BuildAndroid(ScenePath, ApplicationId, ProductName, apk, scriptsOnly: false, development: true,
                BuildOptions.EnableDeepProfilingSupport | BuildOptions.ConnectWithProfiler);
        }
    }
}
