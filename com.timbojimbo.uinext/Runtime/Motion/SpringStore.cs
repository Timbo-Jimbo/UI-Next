using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>What a spring reports to as it moves and settles.</summary>
    internal interface ISpringOwner
    {
        /// <summary>Its value was stepped this frame.</summary>
        void OnSpringMoved();

        /// <summary>It came to rest at its target (or was stopped) and its slot is freed.</summary>
        void OnSpringSettled();
    }

    /// <summary>
    /// Every moving value in one place, as flat arrays of up to four components: where each is drawn, how fast it is
    /// moving, where it is going, and on what spring. Stepped once a frame, exactly, in closed form, so it plays the same
    /// at any frame rate and can be given somewhere new at any moment, carrying on at the speed it has. A value at rest
    /// has no slot.
    /// </summary>
    internal static class SpringStore
    {
        private static Vector4[] s_value = new Vector4[64];
        private static Vector4[] s_velocity = new Vector4[64];
        private static Vector4[] s_target = new Vector4[64];
        private static float[] s_omega = new float[64];
        private static float[] s_zeta = new float[64];
        private static float[] s_delay = new float[64];
        private static float[] s_rest = new float[64];
        private static int[] s_dims = new int[64];
        private static int[] s_setOff = new int[64];
        private static int[] s_activeIndex = new int[64];
        private static UITransaction[] s_transaction = new UITransaction[64];
        private static ISpringOwner[] s_owner = new ISpringOwner[64];

        private static readonly Stack<int> s_free = new();
        private static readonly List<int> s_active = new();
        private static int s_count;

        // Slots settled in the step, reported once the step is over.
        private static readonly List<int> s_settled = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_free.Clear();
            s_active.Clear();
            s_settled.Clear();
            s_count = 0;
            Array.Clear(s_owner, 0, s_owner.Length);
            Array.Clear(s_transaction, 0, s_transaction.Length);
        }

        /// <summary>How many values are moving.</summary>
        public static int ActiveCount => s_active.Count;

        /// <summary>A slot for a value that starts moving from <paramref name="value"/>, at rest there.</summary>
        public static int Allocate(ISpringOwner owner, int dims, float rest, Vector4 value)
        {
            int slot;
            if (s_free.Count > 0)
            {
                slot = s_free.Pop();
            }
            else
            {
                slot = s_count++;
                if (slot == s_value.Length)
                    Grow(slot * 2);
            }
            s_owner[slot] = owner;
            s_dims[slot] = dims;
            s_rest[slot] = rest;
            s_value[slot] = value;
            s_velocity[slot] = Vector4.zero;
            s_target[slot] = value;
            s_delay[slot] = 0f;
            s_omega[slot] = float.PositiveInfinity;
            s_zeta[slot] = 1f;
            s_setOff[slot] = Time.frameCount;
            s_transaction[slot] = null;
            s_activeIndex[slot] = s_active.Count;
            s_active.Add(slot);
            return slot;
        }

        public static Vector4 Value(int slot) => s_value[slot];

        public static Vector4 Velocity(int slot) => s_velocity[slot];

        public static Vector4 Target(int slot) => s_target[slot];

        public static UITransaction TransactionOf(int slot) => s_transaction[slot];

        /// <summary>
        /// Sends a slot to <paramref name="target"/> on <paramref name="animation"/>, from where it is at the speed it
        /// has, held by <paramref name="transaction"/> (letting go of the one it was held by).
        /// </summary>
        public static void Retarget(int slot, Vector4 target, UIAnimation animation, UITransaction transaction)
        {
            s_target[slot] = target;
            animation.Parameters(out s_omega[slot], out s_zeta[slot]);
            s_delay[slot] = Mathf.Max(0f, animation.Delay);
            Hold(slot, transaction);
        }

        /// <summary>Gives a moving slot somewhere new, keeping its spring, its speed and the change it moves for.</summary>
        public static void SetTarget(int slot, Vector4 target) => s_target[slot] = target;

        /// <summary>Adds to a slot's velocity (a fling, a scroll handing on its speed).</summary>
        public static void AddVelocity(int slot, Vector4 velocity) => s_velocity[slot] += velocity;

        /// <summary>Puts a slot's value somewhere at once, keeping its velocity and target.</summary>
        public static void SetValue(int slot, Vector4 value) => s_value[slot] = value;

        /// <summary>
        /// Stops a slot where it is going, at rest, and frees it. <paramref name="arrived"/> says whether it got there
        /// (stepped or skipped there) rather than being cut short; a change cut short does not complete.
        /// </summary>
        public static void Stop(int slot, bool arrived)
        {
            var owner = s_owner[slot];
            Release(slot, arrived);
            FreeSlot(slot);
            owner?.OnSpringSettled();
        }

        /// <summary>Moves every slot <paramref name="dt"/> seconds on, bar those that set off this frame.</summary>
        public static void Step(float dt)
        {
            int frame = Time.frameCount;
            for (int i = 0; i < s_active.Count; i++)
            {
                int slot = s_active[i];
                if (s_setOff[slot] == frame) continue;
                if (StepSlot(slot, dt))
                    s_settled.Add(slot);
                else
                    s_owner[slot]?.OnSpringMoved();
            }
            for (int i = 0; i < s_settled.Count; i++)
            {
                int slot = s_settled[i];
                s_value[slot] = s_target[slot];
                Stop(slot, arrived: true);
            }
            s_settled.Clear();
        }

        /// <summary>Puts everything <paramref name="transaction"/> moves where it is going, as if it had got there.</summary>
        public static void Skip(UITransaction transaction)
        {
            for (int i = 0; i < s_active.Count; i++)
            {
                int slot = s_active[i];
                if (s_transaction[slot] == transaction)
                    s_settled.Add(slot);
            }
            for (int i = 0; i < s_settled.Count; i++)
            {
                int slot = s_settled[i];
                s_value[slot] = s_target[slot];
                Stop(slot, arrived: true);
            }
            s_settled.Clear();
        }

        /// <summary>Puts every moving slot where it is going (leaving play mode, or edit mode, where nothing animates).</summary>
        public static void StopAll()
        {
            while (s_active.Count > 0)
            {
                int slot = s_active[s_active.Count - 1];
                s_value[slot] = s_target[slot];
                Stop(slot, arrived: true);
            }
        }

        private static bool StepSlot(int slot, float dt)
        {
            if (s_delay[slot] > 0f)
            {
                if (dt <= s_delay[slot])
                {
                    s_delay[slot] -= dt;
                    return false;
                }
                dt -= s_delay[slot];
                s_delay[slot] = 0f;
            }

            float omega = s_omega[slot];
            if (float.IsPositiveInfinity(omega))
            {
                s_velocity[slot] = Vector4.zero;
                return true;
            }

            float zeta = s_zeta[slot];
            float rest = s_rest[slot];
            float slow = rest * omega;
            var value = s_value[slot];
            var velocity = s_velocity[slot];
            var target = s_target[slot];
            bool settled = true;
            int dims = s_dims[slot];
            for (int c = 0; c < dims; c++)
            {
                float x = value[c] - target[c], v = velocity[c];
                Evolve(omega, zeta, dt, ref x, ref v);
                value[c] = target[c] + x;
                velocity[c] = v;
                if (Mathf.Abs(x) >= rest || Mathf.Abs(v) >= slow)
                    settled = false;
            }
            s_value[slot] = value;
            s_velocity[slot] = velocity;
            return settled;
        }

        /// <summary>
        /// A damped spring <paramref name="t"/> seconds after it is <paramref name="x"/> from rest moving at
        /// <paramref name="v"/>, exactly: critically damped and underdamped are different curves.
        /// </summary>
        public static void Evolve(float omega, float zeta, float t, ref float x, ref float v)
        {
            float x0 = x, v0 = v;
            if (zeta >= 0.9999f)
            {
                float decay = Mathf.Exp(-omega * t);
                float b = v0 + omega * x0;
                x = (x0 + b * t) * decay;
                v = (b - omega * (x0 + b * t)) * decay;
                return;
            }
            float damped = omega * Mathf.Sqrt(1f - zeta * zeta);
            float envelope = Mathf.Exp(-zeta * omega * t);
            float cos = Mathf.Cos(damped * t), sin = Mathf.Sin(damped * t);
            float k = (v0 + zeta * omega * x0) / damped;
            x = envelope * (x0 * cos + k * sin);
            v = envelope * ((k * damped - zeta * omega * x0) * cos - (zeta * omega * k + x0 * damped) * sin);
        }

        private static void Hold(int slot, UITransaction transaction)
        {
            var current = s_transaction[slot];
            if (current == transaction) return;
            Release(slot, arrived: false);
            s_transaction[slot] = transaction;
            transaction?.Hold();
        }

        private static void Release(int slot, bool arrived)
        {
            var transaction = s_transaction[slot];
            if (transaction == null) return;
            s_transaction[slot] = null;
            transaction.Release(arrived);
        }

        private static void FreeSlot(int slot)
        {
            int index = s_activeIndex[slot];
            int last = s_active[s_active.Count - 1];
            s_active[index] = last;
            s_activeIndex[last] = index;
            s_active.RemoveAt(s_active.Count - 1);
            s_owner[slot] = null;
            s_free.Push(slot);
        }

        private static void Grow(int size)
        {
            Array.Resize(ref s_value, size);
            Array.Resize(ref s_velocity, size);
            Array.Resize(ref s_target, size);
            Array.Resize(ref s_omega, size);
            Array.Resize(ref s_zeta, size);
            Array.Resize(ref s_delay, size);
            Array.Resize(ref s_rest, size);
            Array.Resize(ref s_dims, size);
            Array.Resize(ref s_setOff, size);
            Array.Resize(ref s_activeIndex, size);
            Array.Resize(ref s_transaction, size);
            Array.Resize(ref s_owner, size);
        }
    }
}
