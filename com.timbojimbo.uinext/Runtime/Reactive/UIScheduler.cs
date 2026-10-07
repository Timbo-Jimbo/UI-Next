using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Runs invalid computations, earliest phase first. A write only marks; the work happens once per flush, so writing
    /// fifty values costs fifty marks and one run of each computation that read them.
    /// </summary>
    internal static class UIScheduler
    {
        // A flush running this many computations is caught in a loop: something writes a value it reads, directly or
        // through others, every time it runs. Reported, as React reports a maximum update depth, rather than hung on.
        private const int RunLimit = 100000;

        private static readonly Queue<Computation>[] s_queues = { new(), new(), new() };
        private static bool s_flushing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (var queue in s_queues)
                queue.Clear();
            s_flushing = false;
        }

        internal static void Enqueue(Computation computation) => s_queues[(int)computation.Phase].Enqueue(computation);

        /// <summary>Whether anything up to and including <paramref name="upTo"/> waits to run.</summary>
        public static bool HasPending(Phase upTo)
        {
            for (int p = 0; p <= (int)upTo; p++)
                if (s_queues[p].Count > 0)
                    return true;
            return false;
        }

        /// <summary>
        /// Runs everything invalid up to and including <paramref name="upTo"/>, earliest phase first: a run that
        /// invalidates an earlier phase sends the flush back to it. A flush started inside one does nothing; the outer
        /// one picks up what was invalidated.
        /// </summary>
        public static void Flush(Phase upTo)
        {
            if (s_flushing) return;
            s_flushing = true;
            try
            {
                int runs = 0;
                while (TryNext(upTo, out var computation))
                {
                    if (!computation.Dirty || computation.Disposed) continue;
                    computation.Run();
                    if (++runs <= RunLimit) continue;
                    Debug.LogError($"UINext: a flush ran {RunLimit} computations without settling. Something writes a value it reads every time it runs; the last to run was a {computation.GetType().Name}.");
                    foreach (var queue in s_queues)
                    {
                        foreach (var waiting in queue)
                            waiting.Dirty = false;
                        queue.Clear();
                    }
                    return;
                }
            }
            finally
            {
                s_flushing = false;
            }
        }

        private static bool TryNext(Phase upTo, out Computation computation)
        {
            for (int p = 0; p <= (int)upTo; p++)
            {
                if (s_queues[p].Count == 0) continue;
                computation = s_queues[p].Dequeue();
                return true;
            }
            computation = null;
            return false;
        }
    }
}
