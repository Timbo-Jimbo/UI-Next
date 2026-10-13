using Unity.Profiling;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The markers UI Next shows in the Unity Profiler, all named UINext.*: the frame and its steps, each stretch of a
    /// flush spent in one phase, layout, the compositor, input, labels reading their text, and images loading (the disk
    /// cache's work on its own thread, "UI Next / Image disk cache"). They cost next to nothing while the Profiler isn't
    /// recording.
    /// </summary>
    internal static class UIProfiling
    {
        public static readonly ProfilerMarker Frame = Marker("UINext.Frame");
        public static readonly ProfilerMarker Roots = Marker("UINext.Roots");
        public static readonly ProfilerMarker Lists = Marker("UINext.Lists");
        public static readonly ProfilerMarker Step = Marker("UINext.Step");

        /// <summary>A flush's runs in each phase (Model, Layout, Draw): one sample for each stretch spent in one.</summary>
        public static readonly ProfilerMarker[] Phases = { Marker("UINext.Model"), Marker("UINext.Layout"), Marker("UINext.Draw") };

        public static readonly ProfilerMarker Solve = Marker("UINext.Layout.Solve");
        public static readonly ProfilerMarker Composite = Marker("UINext.Compositor.Build");
        public static readonly ProfilerMarker Upload = Marker("UINext.Compositor.Upload");
        public static readonly ProfilerMarker Render = new(ProfilerCategory.Render, "UINext.Compositor.Render");
        public static readonly ProfilerMarker Input = Marker("UINext.Input");
        public static readonly ProfilerMarker LabelRead = Marker("UINext.Label.Read");

        public static readonly ProfilerMarker ImageLoad = Marker("UINext.Image.Load");
        public static readonly ProfilerMarker ImageStart = Marker("UINext.Images.Start");
        public static readonly ProfilerMarker ImageTake = Marker("UINext.Images.Take");
        public static readonly ProfilerMarker ImageTexture = Marker("UINext.Images.Texture");
        public static readonly ProfilerMarker ImageKeep = Marker("UINext.Images.KeepOnDisk");
        public static readonly ProfilerMarker ImageEvict = Marker("UINext.Images.Evict");
        public static readonly ProfilerMarker Atlas = Marker("UINext.Images.Atlas");
        public static readonly ProfilerMarker AtlasPage = Marker("UINext.Images.Atlas.NewPage");
        public static readonly ProfilerMarker AtlasConvert = Marker("UINext.Images.Atlas.Convert");
        public static readonly ProfilerMarker Disk = Marker("UINext.Images.Disk");

        private static ProfilerMarker Marker(string name) => new(ProfilerCategory.Gui, name);
    }
}
