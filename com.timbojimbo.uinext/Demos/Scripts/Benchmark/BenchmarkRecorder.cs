using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Benchmark
{
    /// <summary>
    /// Measures frames for the benchmark: every marker and counter in UI Next's Profiler category (each marker's time and
    /// calls a frame), a few of the engine's own (the player loop, what a frame allocated), and the frame's timings (its
    /// CPU time on the main and render threads, and the GPU's), and any other marker <c>include</c> takes. Markers are
    /// recorded as they are when it is made, so it is made once what is measured has run. Recording a marker needs a
    /// development player (or the editor).
    /// </summary>
    internal sealed class BenchmarkRecorder : IDisposable
    {
        // The engine's own measures it records too, beside UI Next's.
        private static readonly string[] EngineMeasures = { "PlayerLoop", "GC Allocated In Frame", "GC.Collect" };

        private struct Measure
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public bool Time;
        }

        private readonly List<Measure> _measures = new();

        // The frame's timings, in milliseconds, a frame at a time.
        private readonly List<double> _frame = new(), _main = new(), _render = new(), _gpu = new(), _delta = new();
        private readonly FrameTiming[] _timing = new FrameTiming[1];

        private readonly List<double> _values = new();
        private readonly List<double> _calls = new();

        public BenchmarkRecorder(int capacity, Func<string, bool> include = null)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Category.Name != "UI Next" && Array.IndexOf(EngineMeasures, description.Name) < 0
                    && (include == null || !include(description.Name))) continue;
                _measures.Add(new Measure
                {
                    // Counters are told apart from markers by a # (UI Next's markers are all "UINext.").
                    Name = description.Category.Name == "UI Next" && !description.Name.StartsWith("UINext.", StringComparison.Ordinal)
                        ? "#" + description.Name
                        : description.Name,
                    // A sample a frame, each the frame's calls summed; started only once measuring begins.
                    Recorder = new ProfilerRecorder(handle, capacity,
                        ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached),
                    Time = description.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds,
                });
            }
            _measures.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        /// <summary>Starts measuring, from the next frame on.</summary>
        public void Begin()
        {
            foreach (var measure in _measures)
            {
                measure.Recorder.Reset();
                measure.Recorder.Start();
            }
            _frame.Clear();
            _main.Clear();
            _render.Clear();
            _gpu.Clear();
            _delta.Clear();
        }

        /// <summary>Takes the frame's timings (as the frame timing manager has them, a few frames behind).</summary>
        public void Frame()
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                var timing = _timing[0];
                _frame.Add(timing.cpuFrameTime);
                _main.Add(timing.cpuMainThreadFrameTime);
                _render.Add(timing.cpuRenderThreadFrameTime);
                _gpu.Add(timing.gpuFrameTime);
            }
            _delta.Add(Time.unscaledDeltaTime * 1000.0);
        }

        public void End()
        {
            foreach (var measure in _measures)
                measure.Recorder.Stop();
        }

        /// <summary>
        /// Reports what was measured since <see cref="Begin"/>, a line a measure, each after <paramref name="prefix"/>: its
        /// unit, the frames it covers, and its mean, median, 90th percentile and highest a frame (times in microseconds,
        /// the frame's timings in milliseconds), with its calls a frame for a marker. Measures that came to nothing are
        /// left out.
        /// </summary>
        public void Report(string prefix, Action<string> log)
        {
            log(Line(prefix, "frame.delta", "ms", _delta, null));
            log(Line(prefix, "frame.cpu", "ms", _frame, null));
            log(Line(prefix, "frame.main", "ms", _main, null));
            log(Line(prefix, "frame.render", "ms", _render, null));
            log(Line(prefix, "frame.gpu", "ms", _gpu, null));
            foreach (var measure in _measures)
            {
                var recorder = measure.Recorder;
                _values.Clear();
                _calls.Clear();
                bool any = false;
                for (int i = 0; i < recorder.Count; i++)
                {
                    var sample = recorder.GetSample(i);
                    double value = measure.Time ? sample.Value / 1000.0 : sample.Value;
                    any |= value != 0.0;
                    _values.Add(value);
                    _calls.Add(sample.Count);
                }
                if (any)
                    log(Line(prefix, measure.Name, measure.Time ? "us" : "n", _values, measure.Time ? _calls : null));
            }
        }

        private static string Line(string prefix, string name, string unit, List<double> values, List<double> calls)
        {
            if (values.Count == 0) return prefix + name + "|" + unit + "|n=0";
            var sorted = values.ToArray();
            Array.Sort(sorted);
            double sum = 0.0;
            foreach (var value in sorted)
                sum += value;
            var line = string.Format(CultureInfo.InvariantCulture, "{0}{1}|{2}|n={3}|mean={4:0.###}|med={5:0.###}|p90={6:0.###}|max={7:0.###}",
                prefix, name, unit, sorted.Length, sum / sorted.Length, Percentile(sorted, 0.5), Percentile(sorted, 0.9), sorted[^1]);
            if (calls == null || calls.Count == 0) return line;
            double total = 0.0;
            foreach (var count in calls)
                total += count;
            return line + string.Format(CultureInfo.InvariantCulture, "|calls={0:0.##}", total / calls.Count);
        }

        private static double Percentile(double[] sorted, double at)
        {
            double position = at * (sorted.Length - 1);
            int below = (int)Math.Floor(position);
            int above = Math.Min(sorted.Length - 1, below + 1);
            return sorted[below] + (sorted[above] - sorted[below]) * (position - below);
        }

        public void Dispose()
        {
            foreach (var measure in _measures)
                measure.Recorder.Dispose();
            _measures.Clear();
        }
    }
}
