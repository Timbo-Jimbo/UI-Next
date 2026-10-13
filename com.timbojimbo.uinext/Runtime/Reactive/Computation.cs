using System;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// The stages of a frame, in the order they run. A computation belongs to one; the scheduler runs every invalid
    /// computation of an earlier phase before any of a later one.
    /// </summary>
    public enum Phase
    {
        /// <summary>Resolving values: bindings, tokens, states and variants.</summary>
        Model = 0,

        /// <summary>Laying trees out. Reads see where values are going (their model).</summary>
        Layout = 1,

        /// <summary>Drawing nodes. Reads see where values are drawn (their presentation).</summary>
        Draw = 2,
    }

    /// <summary>
    /// Work that records what it reads and runs again when any of it changes: a binding, a value's resolution, a tree's
    /// layout, a node's drawing. Reads are tracked for the length of <see cref="Execute"/>, however deep they happen,
    /// and recorded afresh on every run, so a branch not taken is never watched.
    /// </summary>
    public abstract class Computation
    {
        // The computation running now, which reads are recorded against.
        internal static Computation Current;

        // A number unique to each run, for the read marks.
        private static int s_runs;

        private Observable[] _reads = new Observable[4];
        private int _readCount;
        private Observable[] _next = new Observable[4];
        private int _nextCount;
        private int _run;

        internal bool Dirty;
        internal bool Disposed;

        // Its runs' marker in the Profiler, made the first time it runs.
        private Unity.Profiling.ProfilerMarker _marker;
        private bool _hasMarker;

        protected Computation(Phase phase) => Phase = phase;

        /// <summary>What its runs are called in the Profiler, after "UINext.": its type's name unless it says otherwise.</summary>
        protected virtual string ProfilerName => GetType().Name;

        // Its runs' marker in the Profiler.
        internal Unity.Profiling.ProfilerMarker Marker
        {
            get
            {
                if (_hasMarker) return _marker;
                _marker = UIProfiling.MarkerFor(ProfilerName);
                _hasMarker = true;
                return _marker;
            }
        }

        /// <summary>The phase it runs in.</summary>
        public Phase Phase { get; }

        /// <summary>Whether a computation is running now.</summary>
        public static bool IsTracking => Current != null;

        /// <summary>The phase of the computation running now, or <see cref="Phase.Model"/> outside any.</summary>
        public static Phase CurrentPhase => Current?.Phase ?? Phase.Model;

        /// <summary>The work. Every observable read during it is recorded.</summary>
        protected abstract void Execute();

        /// <summary>Marks it to run again in the next flush (or the current one, if its phase has not passed).</summary>
        public void Invalidate()
        {
            if (Dirty || Disposed) return;
            Dirty = true;
            UIScheduler.Enqueue(this);
        }

        /// <summary>Runs it now, recording its reads.</summary>
        public void Run()
        {
            if (Disposed) return;
            Dirty = false;
            var previous = Current;
            Current = this;
            _run = ++s_runs;
            _nextCount = 0;
            try
            {
                Execute();
            }
            finally
            {
                Current = previous;
                Commit();
            }
        }

        /// <summary>Stops it for good: it lets go of what it read and never runs again.</summary>
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Dirty = false;
            for (int i = 0; i < _readCount; i++)
            {
                _reads[i].Readers.Remove(this);
                _reads[i] = null;
            }
            _readCount = 0;
        }

        /// <summary>Runs <paramref name="action"/> without recording what it reads.</summary>
        public static void Untracked(Action action)
        {
            var previous = Current;
            Current = null;
            try { action(); }
            finally { Current = previous; }
        }

        internal void Read(Observable observable)
        {
            if (observable.ReadMark == _run) return;
            observable.ReadMark = _run;
            if (_nextCount == _next.Length)
                Array.Resize(ref _next, _nextCount * 2);
            _next[_nextCount++] = observable;
        }

        // Swaps in this run's reads: subscribes to what is new, lets go of what was not read again. With the same reads
        // as last time, which is the usual case, nothing is subscribed or let go of.
        private void Commit()
        {
            int run = _run;
            for (int i = 0; i < _nextCount; i++)
                _next[i].CommitMark = run;
            for (int i = 0; i < _readCount; i++)
            {
                var observable = _reads[i];
                if (observable.CommitMark == run)
                    observable.KeptMark = run;
                else
                    observable.Readers.Remove(this);
                _reads[i] = null;
            }
            for (int i = 0; i < _nextCount; i++)
            {
                var observable = _next[i];
                if (observable.KeptMark == run) continue;
                observable.KeptMark = run;
                if (!Disposed)
                    observable.Readers.Add(this);
            }
            (_reads, _next) = (_next, _reads);
            _readCount = _nextCount;
            _nextCount = 0;
            if (Disposed)
                Dispose();
        }
    }

    /// <summary>A computation that runs a delegate: the simplest way to react to tracked values.</summary>
    public sealed class Effect : Computation
    {
        private readonly Action _action;

        public Effect(Action action, Phase phase = Phase.Model) : base(phase)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        protected override void Execute() => _action();

        // Named by what it runs: "Effect.Image.Load".
        protected override string ProfilerName => "Effect." + UIProfiling.NameOf(_action);
    }
}
