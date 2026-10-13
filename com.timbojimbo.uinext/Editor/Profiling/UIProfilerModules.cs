using System;
using TimboJimbo.UINext;
using Unity.Profiling.Editor;

namespace TimboJimboEditor.UINext
{
    /// <summary>
    /// The Profiler's UI Next module: where each frame's UI Next time goes, stacked: input, values and states resolving
    /// (Model), layout, drawing, motion (scroll views, presses, springs), the compositor, and images loaded (turned into
    /// textures, put on atlas pages). Pick it from the Profiler's module list.
    /// </summary>
    [Serializable, ProfilerModuleMetadata("UI Next")]
    public sealed class UIProfilerModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] s_counters =
        {
            new(UIProfiling.InputTimeName, UIProfiling.CategoryName),
            new(UIProfiling.ModelTimeName, UIProfiling.CategoryName),
            new(UIProfiling.LayoutTimeName, UIProfiling.CategoryName),
            new(UIProfiling.DrawTimeName, UIProfiling.CategoryName),
            new(UIProfiling.MotionTimeName, UIProfiling.CategoryName),
            new(UIProfiling.CompositorTimeName, UIProfiling.CategoryName),
            new(UIProfiling.ImagesTimeName, UIProfiling.CategoryName),
        };

        public UIProfilerModule() : base(s_counters, ProfilerModuleChartType.StackedTimeArea) { }
    }

    /// <summary>
    /// The Profiler's UI Next Counts module: what each frame drew (draw calls, primitives) and what images were doing
    /// (loading, arriving, in use, the memory the cache keeps of those nothing shows, atlas pages).
    /// </summary>
    [Serializable, ProfilerModuleMetadata("UI Next Counts")]
    public sealed class UIProfilerCountsModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] s_counters =
        {
            new(UIProfiling.DrawCallsName, UIProfiling.CategoryName),
            new(UIProfiling.PrimitivesName, UIProfiling.CategoryName),
            new(UIProfiling.ImagesLoadingName, UIProfiling.CategoryName),
            new(UIProfiling.ImagesArrivedName, UIProfiling.CategoryName),
            new(UIProfiling.ImagesInUseName, UIProfiling.CategoryName),
            new(UIProfiling.ImageCacheName, UIProfiling.CategoryName),
            new(UIProfiling.AtlasPagesName, UIProfiling.CategoryName),
        };

        public UIProfilerCountsModule() : base(s_counters) { }
    }

    /// <summary>
    /// The Profiler's UI Next Updates module: what each frame did again to bring the UI up to date: nodes composited again
    /// and those whose values were read for it, draw orders made again, primitives patched where they were, and what went
    /// to the GPU. (A module charts ten counters at most, so these are a module of their own.)
    /// </summary>
    [Serializable, ProfilerModuleMetadata("UI Next Updates")]
    public sealed class UIProfilerUpdatesModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] s_counters =
        {
            new(UIProfiling.CompositedName, UIProfiling.CategoryName),
            new(UIProfiling.InputsReadName, UIProfiling.CategoryName),
            new(UIProfiling.OrderRebuildsName, UIProfiling.CategoryName),
            new(UIProfiling.PatchedName, UIProfiling.CategoryName),
            new(UIProfiling.UploadedName, UIProfiling.CategoryName),
        };

        public UIProfilerUpdatesModule() : base(s_counters) { }
    }
}
