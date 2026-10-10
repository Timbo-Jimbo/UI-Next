using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>A value a gesture samples as its change callbacks set it (see <see cref="GestureVelocity"/>).</summary>
    internal interface ISpeedSampled
    {
        /// <summary>Takes a sample of where it is now, at <paramref name="time"/> (seconds, the pointer's clock).</summary>
        void AddSample(double time);

        /// <summary>Forgets its samples: once its gesture is over it has no speed but a spring's.</summary>
        void ForgetSamples();
    }

    /// <summary>
    /// The speed a gesture hands on, as SwiftUI's tracksVelocity: while its change callbacks run, the values they set
    /// directly are sampled at the pointer's time, so each knows the speed the finger moves it at, and the animated change
    /// made as the gesture lets go starts each at that speed. Once the gesture is over, whatever it moved that nothing took
    /// over lands still. The gesture's own lifetime decides, never a clock: the last sample is taken as it lets go, so a
    /// finger held still before lifting hands on no speed.
    /// </summary>
    internal sealed class GestureVelocity
    {
        /// <summary>The gesture whose change callback is running: what is set directly is sampled for it.</summary>
        internal static GestureVelocity Current { get; private set; }

        private readonly List<ISpeedSampled> _values = new();
        private double _time;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        /// <summary>Runs a change callback with <paramref name="argument"/>, sampling what it sets directly at <paramref name="time"/>.</summary>
        public void Run<T>(Action<T> callback, T argument, double time)
        {
            if (callback == null) return;
            var previous = Current;
            Current = this;
            _time = time;
            try
            {
                callback(argument);
            }
            finally
            {
                Current = previous;
            }
        }

        /// <summary>A value its callback set directly: sampled where it is now.</summary>
        internal void Sample(ISpeedSampled value)
        {
            value.AddSample(_time);
            if (!_values.Contains(value))
                _values.Add(value);
        }

        /// <summary>It lets go at <paramref name="time"/>: what it moved takes a last sample where it is.</summary>
        public void LetGo(double time)
        {
            foreach (var value in _values)
                value.AddSample(time);
        }

        /// <summary>It is over: what it moved that no spring took over lands still.</summary>
        public void Forget()
        {
            foreach (var value in _values)
                value.ForgetSamples();
            _values.Clear();
        }
    }
}
