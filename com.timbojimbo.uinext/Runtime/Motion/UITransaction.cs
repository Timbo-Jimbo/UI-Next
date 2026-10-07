using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// One change made with <see cref="UI.Animate(UIAnimation, Action)"/>: everything it set moving, and when that has
    /// all landed. It finishes once nothing it moved is still moving; it completes only if nothing was cut short on its
    /// way (taken over by another change, or stopped).
    /// </summary>
    public sealed class UITransaction
    {
        // Changes whose last value has landed, finished once the frame's flush is over (a handler may start another).
        private static readonly List<UITransaction> s_finishing = new();

        private Action _finished;

        internal UITransaction(UIAnimation animation) => Animation = animation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_finishing.Clear();
            Current = null;
        }

        /// <summary>The change being made: set while an <see cref="UI.Animate(UIAnimation, Action)"/> update runs.</summary>
        public static UITransaction Current { get; internal set; }

        /// <summary>The animation what it changes moves on.</summary>
        public UIAnimation Animation { get; }

        /// <summary>How many values it is still moving.</summary>
        public int Moving { get; private set; }

        /// <summary>Whether everything it moved has landed.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Whether it finished with nothing cut short on the way.</summary>
        public bool Completed { get; private set; } = true;

        /// <summary>Raised once everything it moved has landed. Added after that, it runs at once.</summary>
        public event Action Finished
        {
            add
            {
                if (IsFinished) value?.Invoke();
                else _finished += value;
            }
            remove => _finished -= value;
        }

        /// <summary>Puts everything it moves where it is going, as if it had got there, and finishes it.</summary>
        public void Skip()
        {
            if (IsFinished) return;
            SpringStore.Skip(this);
            Finish();
        }

        internal void Hold() => Moving++;

        internal void Release(bool arrived)
        {
            if (!arrived) Completed = false;
            if (--Moving > 0) return;
            Moving = 0;
            s_finishing.Add(this);
        }

        internal void Finish()
        {
            if (IsFinished) return;
            IsFinished = true;
            var handlers = _finished;
            _finished = null;
            handlers?.Invoke();
        }

        /// <summary>Finishes the changes that have landed. Returns whether it finished any.</summary>
        internal static bool FlushFinished()
        {
            bool any = false;
            while (s_finishing.Count > 0)
            {
                var transaction = s_finishing[0];
                s_finishing.RemoveAt(0);
                if (transaction.Moving > 0) continue;
                transaction.Finish();
                any = true;
            }
            return any;
        }
    }
}
