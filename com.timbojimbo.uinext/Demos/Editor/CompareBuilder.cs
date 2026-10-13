#if UINEXT_UGUI
using TimboJimbo.UINext.Demos.Compare;
using UnityEditor;
using UnityEngine;

namespace TimboJimboEditor.UINext.Demos
{
    /// <summary>
    /// Builds the comparison of UI Next with UGUI: its scene (a camera, a UI Next root, and a <see cref="CompareApp"/>
    /// that builds one or the other as it starts) and an Android development player of it, an app of its own.
    /// </summary>
    public static class CompareBuilder
    {
        public const string ScenePath = DemoKit.DemosFolder + "/Scenes/Compare.unity";

        private const string ApplicationId = "com.timbojimbo.uinext.compare";
        private const string ProductName = "UI Next vs UGUI";

        [MenuItem("Tools/UI Next/Demos/Build Compare")]
        private static void BuildAndOpen() => Build(open: true);

        public static void Build(bool open) => DemoKit.BuildScene(ScenePath, _ => BuildScene(), open);

        private static void BuildScene()
        {
            DemoKit.Camera(Color.black);
            var root = DemoKit.Root("UI Next");
            var app = new GameObject("Compare").AddComponent<CompareApp>();
            var serialized = new SerializedObject(app);
            serialized.FindProperty("_root").objectReferenceValue = root;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds its Android player to <paramref name="apk"/>, its scene first if it has none: a development player, whose
        /// markers are recorded, or with <paramref name="development"/> off a release one, which records the frame's timings
        /// alone, as the app would run.
        /// </summary>
        public static string BuildPlayer(string apk, bool scriptsOnly = false, bool development = true)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                Build(open: false);
            return BenchmarkBuilder.BuildAndroid(ScenePath, ApplicationId, ProductName, apk, scriptsOnly, development);
        }
    }
}
#endif
