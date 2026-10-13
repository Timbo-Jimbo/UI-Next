using System.IO;
using TimboJimbo.UINext;
using TimboJimbo.UINext.Demos.Benchmark;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>
    /// Builds the benchmark: its scene (a camera, and a root whose <see cref="BenchmarkApp"/> builds the rest as it starts),
    /// and an Android development player of that scene alone, installed as an app of its own beside the project's, with
    /// frame timings on so the GPU's time is measured too.
    /// </summary>
    public static class BenchmarkBuilder
    {
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Benchmark.unity";

        // The player's own app, so installing it replaces nothing else.
        private const string ApplicationId = "com.timbojimbo.uinext.benchmark";
        private const string ProductName = "UI Next Benchmark";

        [MenuItem("Tools/UI Next/Demos/Build Benchmark")]
        private static void BuildAndOpen() => Build(open: true);

        public static void Build(bool open) => DemoKit.BuildScene(ScenePath, _ => BuildScene(), open);

        private static void BuildScene()
        {
            DemoKit.Camera(Color.black);
            DemoKit.Root("Benchmark").Node.Add<BenchmarkApp>();
        }

        [MenuItem("Tools/UI Next/Demos/Build Benchmark Player (Android)")]
        private static void BuildPlayerFromMenu()
        {
            var apk = EditorUtility.SaveFilePanel("Build the benchmark player", "", "UINextBenchmark", "apk");
            if (!string.IsNullOrEmpty(apk))
                Debug.Log("UI Next benchmark: " + BuildPlayer(apk));
        }

        /// <summary>
        /// Builds the benchmark's Android development player to <paramref name="apk"/>, its scene first if it has none:
        /// with <paramref name="scriptsOnly"/>, only its scripts, over a player built there before. The project's own app id,
        /// name and frame timing setting are put back (and saved) however it ends. Returns what the build came to.
        /// </summary>
        public static string BuildPlayer(string apk, bool scriptsOnly = false)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                Build(open: false);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(apk)));

            var target = NamedBuildTarget.Android;
            string applicationId = PlayerSettings.GetApplicationIdentifier(target);
            string productName = PlayerSettings.productName;
            bool frameTimings = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
                PlayerSettings.productName = ProductName;
                PlayerSettings.enableFrameTimingStats = true;
                var options = BuildOptions.Development;
                if (scriptsOnly) options |= BuildOptions.BuildScriptsOnly;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = apk,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options,
                });
                var summary = report.summary;
                return $"{summary.result} in {summary.totalTime.TotalSeconds:F0}s, {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize / (1024 * 1024)} MB";
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(target, applicationId);
                PlayerSettings.productName = productName;
                PlayerSettings.enableFrameTimingStats = frameTimings;
                // The build saved the project's settings with the benchmark's in them: saved again, they are its own.
                AssetDatabase.SaveAssets();
            }
        }
    }
}
