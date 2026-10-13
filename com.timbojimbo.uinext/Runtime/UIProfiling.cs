using System.Diagnostics;
using Unity.Profiling;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// What UI Next shows in the Unity Profiler, in its own category, "UI Next". Markers, all named UINext.*: the frame and
    /// its steps, each stretch of a flush spent in one phase, layout, the compositor, input, labels reading their text, and
    /// images loading (the disk cache's work on its own thread, "UI Next / Image disk cache"). Counters, which the UI Next
    /// and UI Next Counts modules chart: where each frame's time goes, and what it drew and loaded. They cost next to
    /// nothing while the Profiler isn't recording.
    /// </summary>
    internal static class UIProfiling
    {
        internal const string CategoryName = "UI Next";

        /// <summary>The category of UI Next's markers and counters, which the CPU module can show, hide and colour.</summary>
        public static readonly ProfilerCategory Category = new(CategoryName);

        // ── Markers ──────────────────────────────────────────────────────────────

        public static readonly ProfilerMarker Frame = new(Category, "UINext.Frame");
        public static readonly ProfilerMarker Roots = new(Category, "UINext.Roots");
        public static readonly ProfilerMarker Lists = new(Category, "UINext.Lists");
        public static readonly ProfilerMarker Step = new(Category, "UINext.Step");

        /// <summary>A flush's runs in each phase (Model, Layout, Draw): one sample for each stretch spent in one.</summary>
        public static readonly ProfilerMarker[] Phases =
        {
            new(Category, "UINext.Model"), new(Category, "UINext.Layout"), new(Category, "UINext.Draw"),
        };

        /// <summary>
        /// A layout's steps: the layout inputs of nodes whose values changed read again, the solver's nodes gathered from
        /// every node's, the solve (its widths, text wrapped, heights, then places), its results put on the nodes.
        /// </summary>
        public static readonly ProfilerMarker LayoutInputs = new(Category, "UINext.Layout.Inputs");
        public static readonly ProfilerMarker Gather = new(Category, "UINext.Layout.Gather");
        public static readonly ProfilerMarker Solve = new(Category, "UINext.Layout.Solve");
        public static readonly ProfilerMarker SolveWidths = new(Category, "UINext.Layout.Widths");
        public static readonly ProfilerMarker SolveWrap = new(Category, "UINext.Layout.Wrap");
        public static readonly ProfilerMarker SolveHeights = new(Category, "UINext.Layout.Heights");
        public static readonly ProfilerMarker SolvePlace = new(Category, "UINext.Layout.Place");
        public static readonly ProfilerMarker Apply = new(Category, "UINext.Layout.Apply");

        /// <summary>The compositor's steps: slots and clips copied, drawings patched, the draw order made again (its batches made).</summary>
        public static readonly ProfilerMarker Composite = new(Category, "UINext.Compositor.Build");
        public static readonly ProfilerMarker CopySlots = new(Category, "UINext.Compositor.Copy");
        public static readonly ProfilerMarker Patch = new(Category, "UINext.Compositor.Patch");
        public static readonly ProfilerMarker Order = new(Category, "UINext.Compositor.Order");
        public static readonly ProfilerMarker Batch = new(Category, "UINext.Compositor.Batch");
        public static readonly ProfilerMarker Upload = new(Category, "UINext.Compositor.Upload");
        public static readonly ProfilerMarker Render = new(Category, "UINext.Compositor.Render");
        public static readonly ProfilerMarker Input = new(Category, "UINext.Input");
        public static readonly ProfilerMarker LabelRead = new(Category, "UINext.Label.Read");
        public static readonly ProfilerMarker CrossFade = new(Category, "UINext.Draw.CrossFade");

        public static readonly ProfilerMarker ImageLoad = new(Category, "UINext.Image.Load");
        public static readonly ProfilerMarker ImageStart = new(Category, "UINext.Images.Start");
        public static readonly ProfilerMarker ImageTake = new(Category, "UINext.Images.Take");
        public static readonly ProfilerMarker ImageTexture = new(Category, "UINext.Images.Texture");
        public static readonly ProfilerMarker ImageKeep = new(Category, "UINext.Images.KeepOnDisk");
        public static readonly ProfilerMarker ImageEvict = new(Category, "UINext.Images.Evict");
        public static readonly ProfilerMarker Atlas = new(Category, "UINext.Images.Atlas");
        public static readonly ProfilerMarker AtlasPage = new(Category, "UINext.Images.Atlas.NewPage");
        public static readonly ProfilerMarker AtlasConvert = new(Category, "UINext.Images.Atlas.Convert");
        public static readonly ProfilerMarker Disk = new(Category, "UINext.Images.Disk");

        private static readonly System.Collections.Generic.Dictionary<string, ProfilerMarker> s_named = new();

        /// <summary>A marker named "UINext." and <paramref name="name"/>, made once (a computation's runs, a drawable's drawing).</summary>
        public static ProfilerMarker MarkerFor(string name)
        {
            if (!s_named.TryGetValue(name, out var marker))
                s_named[name] = marker = new ProfilerMarker(Category, "UINext." + name);
            return marker;
        }

        /// <summary>
        /// A delegate's name for the Profiler: its class and method ("Image.Load"), a lambda by the class and method it is
        /// written in ("Label.Bind lambda").
        /// </summary>
        public static string NameOf(System.Delegate action)
        {
            var method = action.Method;
            var type = method.DeclaringType;
            // The compiler puts lambdas in classes of its own, nested in the class they are written in.
            while (type != null && type.Name.StartsWith("<", System.StringComparison.Ordinal) && type.DeclaringType != null)
                type = type.DeclaringType;
            string name = method.Name;
            if (name.StartsWith("<", System.StringComparison.Ordinal))
            {
                int end = name.IndexOf('>');
                name = end > 1 ? name.Substring(1, end - 1) + " lambda" : "lambda";
            }
            return (type != null ? type.Name : "?") + "." + name;
        }

        // ── Counters: where each frame's time goes (the UI Next module) ───────────

        internal const string InputTimeName = "Input";
        internal const string ModelTimeName = "Model";
        internal const string LayoutTimeName = "Layout";
        internal const string DrawTimeName = "Draw";
        internal const string MotionTimeName = "Motion";
        internal const string CompositorTimeName = "Compositor";
        internal const string ImagesTimeName = "Images";

        public static readonly ProfilerCounterValue<long> InputTime = Time(InputTimeName);

        /// <summary>The time a flush spent in each phase (Model, Layout, Draw).</summary>
        public static readonly ProfilerCounterValue<long>[] PhaseTimes = { Time(ModelTimeName), Time(LayoutTimeName), Time(DrawTimeName) };

        public static readonly ProfilerCounterValue<long> MotionTime = Time(MotionTimeName);
        public static readonly ProfilerCounterValue<long> CompositorTime = Time(CompositorTimeName);
        public static readonly ProfilerCounterValue<long> ImagesTime = Time(ImagesTimeName);

        // ── Counters: what each frame drew and loaded (the UI Next Counts module) ─

        internal const string DrawCallsName = "Draw Calls";
        internal const string PrimitivesName = "Primitives";
        internal const string ImagesLoadingName = "Images Loading";
        internal const string ImagesArrivedName = "Images Arrived";
        internal const string ImagesInUseName = "Images In Use";
        internal const string ImageCacheName = "Image Cache (MB)";
        internal const string AtlasPagesName = "Atlas Pages";
        internal const string OrderRebuildsName = "Draw Order Rebuilds";
        internal const string PatchedName = "Primitives Patched";
        internal const string UploadedName = "Uploaded (KB)";
        internal const string CompositedName = "Nodes Composited";
        internal const string InputsReadName = "Composite Inputs Read";
        internal const string LayoutInputsReadName = "Layout Inputs Read";
        internal const string LaidOutName = "Nodes Laid Out";

        public static readonly ProfilerCounterValue<int> DrawCalls = Count(DrawCallsName);
        public static readonly ProfilerCounterValue<int> Primitives = Count(PrimitivesName);
        public static readonly ProfilerCounterValue<int> ImagesLoading = Count(ImagesLoadingName);
        public static readonly ProfilerCounterValue<int> ImagesArrived = Count(ImagesArrivedName, perFrame: true);
        public static readonly ProfilerCounterValue<int> ImagesInUse = Count(ImagesInUseName);
        public static readonly ProfilerCounterValue<int> ImageCache = Count(ImageCacheName);
        public static readonly ProfilerCounterValue<int> AtlasPages = Count(AtlasPagesName);

        /// <summary>The compositor's work each frame: draw orders made again, primitives patched where they were, and what went to the GPU.</summary>
        public static readonly ProfilerCounterValue<int> OrderRebuilds = Count(OrderRebuildsName, perFrame: true);
        public static readonly ProfilerCounterValue<int> Patched = Count(PatchedName, perFrame: true);
        public static readonly ProfilerCounterValue<int> Uploaded = Count(UploadedName, perFrame: true);

        /// <summary>Compositing's work each frame: nodes composited again, and nodes whose values were read again for it.</summary>
        public static readonly ProfilerCounterValue<int> Composited = Count(CompositedName, perFrame: true);
        public static readonly ProfilerCounterValue<int> InputsRead = Count(InputsReadName, perFrame: true);

        /// <summary>
        /// Layout's work each frame: nodes whose layout values were read again for it, and nodes the solver was given
        /// (each node taken whole counting as one).
        /// </summary>
        public static readonly ProfilerCounterValue<int> LayoutInputsRead = Count(LayoutInputsReadName, perFrame: true);
        public static readonly ProfilerCounterValue<int> LaidOut = Count(LaidOutName, perFrame: true);

        private static readonly double s_nanosecondsPerTick = 1e9 / Stopwatch.Frequency;

        /// <summary>Now, for <see cref="AddSince"/>.</summary>
        public static long Now => Stopwatch.GetTimestamp();

        /// <summary>Adds the time since <paramref name="start"/> (from <see cref="Now"/>) to a time counter.</summary>
        public static void AddSince(ProfilerCounterValue<long> counter, long start) =>
            counter.Value += (long)((Stopwatch.GetTimestamp() - start) * s_nanosecondsPerTick);

        /// <summary>Times what runs until it is disposed, adding it to a time counter, however it returns.</summary>
        public static Timing Measure(ProfilerCounterValue<long> counter) => new(counter, Stopwatch.GetTimestamp());

        public readonly struct Timing : System.IDisposable
        {
            private readonly ProfilerCounterValue<long> _counter;
            private readonly long _start;

            internal Timing(ProfilerCounterValue<long> counter, long start)
            {
                _counter = counter;
                _start = start;
            }

            public void Dispose() => AddSince(_counter, _start);
        }

        /// <summary>Publishes what the frame drew and loaded, as it ends.</summary>
        public static void CountFrame()
        {
            DrawCalls.Value = Compositor.DrawCalls;
            Primitives.Value = Compositor.PrimitiveCount;
            ImagesLoading.Value = RemoteImages.LoadingCount;
            ImagesInUse.Value = RemoteImages.InUseCount;
            ImageCache.Value = (int)(RemoteImages.UnusedBytes / (1024 * 1024));
            AtlasPages.Value = ImageAtlas.PageCount;
        }

        // A frame's time in nanoseconds, back to 0 as the next begins.
        private static ProfilerCounterValue<long> Time(string name) => new(Category, name, ProfilerMarkerDataUnit.TimeNanoseconds,
            ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        private static ProfilerCounterValue<int> Count(string name, bool perFrame = false) => new(Category, name, ProfilerMarkerDataUnit.Count,
            perFrame ? ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush : ProfilerCounterOptions.FlushOnEndOfFrame);
    }
}
