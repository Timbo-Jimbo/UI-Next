using System;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// How a change gets where it is going: a spring given as SwiftUI gives one, a perceptual duration and a bounce,
    /// after a delay. Values set off from where they are drawn, at the speed they are moving. A duration of 0 is no
    /// animation: the change lands at once, after its delay.
    /// </summary>
    [Serializable]
    public struct UIAnimation
    {
        [Tooltip("Seconds: about how long it takes to settle. 0 is no animation.")]
        [Min(0f)] public float Duration;

        [Tooltip("How far it overshoots and swings back: 0 none.")]
        [Range(0f, 0.9f)] public float Bounce;

        [Tooltip("Seconds to wait before setting off.")]
        [Min(0f)] public float Delay;

        public UIAnimation(float duration, float bounce = 0f, float delay = 0f)
        {
            Duration = duration;
            Bounce = bounce;
            Delay = delay;
        }

        /// <summary>A smooth spring of about a third of a second.</summary>
        public static UIAnimation Default => new(0.35f);

        /// <summary>Settles without overshooting.</summary>
        public static UIAnimation Smooth => new(0.35f);

        /// <summary>A small overshoot, quick to settle.</summary>
        public static UIAnimation Snappy => new(0.3f, 0.15f);

        /// <summary>A lively overshoot and swing back.</summary>
        public static UIAnimation Bouncy => new(0.45f, 0.3f);

        /// <summary>No animation: there at once.</summary>
        public static UIAnimation None => new(0f);

        /// <summary>The same spring after <paramref name="seconds"/> more.</summary>
        public UIAnimation Delayed(float seconds) => new(Duration, Bounce, Delay + seconds);

        /// <summary>Whether it lands at once, with no duration and no delay.</summary>
        public bool AtOnce => Duration <= 0f && Delay <= 0f;

        /// <summary>
        /// The spring's angular frequency and damping ratio: omega from the perceptual duration (infinite for none), and
        /// a damping ratio of 1 less the bounce.
        /// </summary>
        public void Parameters(out float omega, out float zeta)
        {
            omega = Duration > 0f ? 2f * Mathf.PI / Mathf.Max(Duration, 0.01f) : float.PositiveInfinity;
            zeta = 1f - Mathf.Clamp(Bounce, 0f, 0.9f);
        }
    }
}
