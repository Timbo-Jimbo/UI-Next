using System.Collections.Generic;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Something computations can read and be told about when it changes. A read made while a
    /// <see cref="Computation"/> runs is recorded; a change invalidates every computation that read it in its last run.
    /// Serializable only so that a <see cref="UIValue{T}"/>'s hierarchy is (Unity's analyzer asks); nothing here is saved.
    /// </summary>
    [System.Serializable]
    public abstract class Observable
    {
        // The computations that read this in their last run.
        internal readonly List<Computation> Readers = new();

        // Scratch marks a computation uses to commit its reads without allocating (see Computation.Commit).
        internal int ReadMark;
        internal int CommitMark;
        internal int KeptMark;

        /// <summary>Records a read by the computation running now, if any.</summary>
        protected void Track()
        {
            var current = Computation.Current;
            if (current != null)
                current.Read(this);
        }

        /// <summary>Invalidates every computation that read this in its last run.</summary>
        protected internal void NotifyChanged()
        {
            // Invalidating only marks and queues, so the list does not change under the loop.
            for (int i = Readers.Count - 1; i >= 0; i--)
                Readers[i].Invalidate();
        }
    }

    /// <summary>A value computations can depend on: reading <see cref="Value"/> inside one tracks it.</summary>
    public sealed class Observable<T> : Observable
    {
        private T _value;
        private readonly IEqualityComparer<T> _comparer;

        public Observable(T value = default, IEqualityComparer<T> comparer = null)
        {
            _value = value;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        /// <summary>The value. Reading it inside a computation tracks it; setting it to something new invalidates readers.</summary>
        public T Value
        {
            get
            {
                Track();
                return _value;
            }
            set
            {
                if (_comparer.Equals(_value, value)) return;
                _value = value;
                NotifyChanged();
            }
        }

        /// <summary>The value without tracking the read.</summary>
        public T Peek => _value;

        public static implicit operator T(Observable<T> observable) => observable.Value;
    }
}
