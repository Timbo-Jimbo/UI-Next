using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// A value with two faces: its model, where it is going, and its presentation, where it is drawn right now, as
    /// Core Animation's model and presentation layers. Code running in the draw phase sees the presentation; everything
    /// else (layout, bindings, game code) sees the model. A change made inside <see cref="UI.Animate(UIAnimation, System.Action)"/>
    /// springs the presentation from where it is drawn to the new model. Any other change puts it there at once, still,
    /// except that one already moving heads there keeping its speed, unless a gesture's callbacks set it, which lands it
    /// under the finger.
    /// It knows the speed it moves at however it is moved (<see cref="Velocity"/>), and a spring set off on it starts at
    /// that speed: a value a gesture was moving carries on at the gesture's speed (see <see cref="Draggable.TracksVelocity"/>).
    /// Serializable only so that a <see cref="UIValue{T}"/>'s hierarchy is (Unity's analyzer asks); nothing here is saved.
    /// </summary>
    [System.Serializable]
    public class AnimatedValue<T> : Observable, ISpringOwner, IPendingSpring, ISpeedSampled
    {
        private static readonly IValueConverter<T> s_converter = ValueConverters.Get<T>();
        private static readonly EqualityComparer<T> s_comparer = EqualityComparer<T>.Default;

        // The model, as resolved.
        private protected T _model;

        // The presentation face, which drawing reads; and the spring slot while it moves.
        private readonly PresentationFace _presentation = new();
        private int _slot = -1;

        // How close a spring of it has to be to its target to count as there, in converted units (the type's default when
        // not given).
        private readonly float _rest;

        // Whether a change animates it even before anything has drawn it: for values made to be animated, such as a
        // presentation's transition, set and animated before their first frame.
        private readonly bool _alwaysAnimates;

        // A change made on an animation while nothing drew it yet (a node made this frame and animated in, as UIKit's
        // are): where it was drawn from and the change it is part of, held until the first drawing to read it springs it,
        // or the frame after ends and it lands where it goes. The change is held open meanwhile.
        private bool _pending;
        private Vector4 _pendingFrom;
        private Vector4 _pendingVelocity;
        private UIAnimation _pendingAnimation;
        private UITransaction _pendingTransaction;
        private int _pendingFrame;

        // How fast it moves while no spring moves it, in converted units a second: sampled as a gesture's change callbacks
        // set it, until the gesture is over; or given by what moves it and knows its speed (a scroll dragged or gliding).
        // Set any other way, it is still.
        private VelocityTracker _samples;
        private Vector4 _movingAt;
        private int _movingFrame;

        public AnimatedValue() : this(default) { }

        public AnimatedValue(T value, float rest = -1f)
        {
            _model = value;
            _rest = rest >= 0f ? rest : s_converter?.Rest ?? 0f;
        }

        internal AnimatedValue(T value, float rest, bool alwaysAnimates) : this(value, rest) => _alwaysAnimates = alwaysAnimates;

        /// <summary>Whether values of this type interpolate.</summary>
        public static bool Interpolates => s_converter != null;

        /// <summary>
        /// The face for whoever is reading: in the draw phase where it is drawn, otherwise where it is going. Reading it
        /// inside a computation tracks it.
        /// </summary>
        public T Value
        {
            get => Computation.CurrentPhase == Phase.Draw ? Presented : Model;
            set => SetModel(value, null);
        }

        /// <summary>Where it is going. Tracked as a read of the model.</summary>
        public T Model
        {
            get
            {
                Track();
                EnsureResolved();
                return _model;
            }
        }

        /// <summary>Where it is going, without tracking the read.</summary>
        public T Peek
        {
            get
            {
                EnsureResolved();
                return _model;
            }
        }

        /// <summary>Where it is drawn right now. Tracked as a read of the presentation.</summary>
        public T Presented
        {
            get
            {
                if (_pending)
                    StartPending();
                _presentation.TrackRead();
                EnsureResolved();
                return _slot >= 0 ? s_converter.FromVector(SpringStore.Value(_slot)) : _model;
            }
        }

        /// <summary>Whether it is moving towards its model (or about to, as it is first drawn).</summary>
        public bool IsAnimating => _slot >= 0 || _pending;

        /// <summary>
        /// How fast it is moving, in converted units a second (the units its <see cref="IValueConverter{T}"/> turns it into:
        /// points for positions and sizes, OkLab for colours): on a spring, the spring's speed; while a gesture's change
        /// callbacks set it, the speed they move it at, until the gesture is over; zero once it lands or is set any other
        /// way. A value that does not interpolate has no speed.
        /// </summary>
        public Vector4 Velocity =>
            _slot >= 0 ? SpringStore.Velocity(_slot) : _samples != null && _samples.Count > 0 ? _samples.Velocity : MovingAt;

        // The speed what moves it frame by frame (a scroll's drag or glide) last gave it, while that was this frame or the
        // last: not moved since (a finger held still, a glide stopped), it is still.
        private Vector4 MovingAt => UIFrame.FrameIndex - _movingFrame <= 1 ? _movingAt : Vector4.zero;

        // Where it is drawn now, without tracking the read or setting off a change held for its first drawing.
        internal T Drawn
        {
            get
            {
                if (_slot >= 0) return s_converter.FromVector(SpringStore.Value(_slot));
                if (_pending) return s_converter.FromVector(_pendingFrom);
                return _model;
            }
        }

        // Where it will be drawn `seconds` from now if it keeps the speed it has.
        internal T DrawnAhead(float seconds) =>
            s_converter == null ? Drawn : s_converter.FromVector(s_converter.ToVector(Drawn) + Velocity * seconds);

        // Whether anything draws it, so springs run only for values someone sees.
        internal bool HasPresentationReaders => _presentation.Readers.Count > 0;

        /// <summary>Brings the model up to date before it is read (a value with a pending resolution resolves it now).</summary>
        private protected virtual void EnsureResolved() { }

        /// <summary>
        /// Gives it a new model. Inside a change (<see cref="UITransaction.Current"/>) its presentation springs there on
        /// the change's animation, from where it is drawn at the speed it has; outside one, <paramref name="implicitAnimation"/>
        /// (a state's) is used if given; otherwise a value already moving heads there keeping its speed, spring and change,
        /// so a frame laid out again mid-flight glides on to its new place, and one at rest lands there at once. Set directly
        /// while a gesture's change callback runs, it lands at once, under the finger, cutting short any spring moving it,
        /// and is sampled for the speed the gesture hands on.
        /// </summary>
        internal void SetModel(T value, UIAnimation? implicitAnimation, bool force = false)
        {
            if (!force && s_comparer.Equals(_model, value)) return;
            var previous = _model;
            _model = value;
            NotifyChanged();
            Present(previous, implicitAnimation);
        }

        /// <summary>
        /// Gives it a new model and springs its presentation there on <paramref name="animation"/> from where it is
        /// drawn, moving at <paramref name="velocity"/> (converted units a second): a scroll bouncing back from past its
        /// edge, a flight setting off. Held by <paramref name="transaction"/>, if given, as a change's springs are.
        /// </summary>
        internal void SpringTo(T value, Vector4 velocity, UIAnimation animation, UITransaction transaction = null)
        {
            if (s_converter == null || !UIFrame.Live || animation.AtOnce)
            {
                SetModel(value, null);
                Settle();
                return;
            }
            var from = _slot >= 0 ? SpringStore.Value(_slot) : _pending ? _pendingFrom : s_converter.ToVector(_model);
            DropPending(arrived: false);
            bool changed = !s_comparer.Equals(_model, value);
            _model = value;
            if (changed) NotifyChanged();
            if (_slot < 0)
                _slot = SpringStore.Allocate(this, s_converter.Dimensions, _rest, from);
            SpringStore.Retarget(_slot, s_converter.ToVector(value), animation, transaction);
            SpringStore.SetVelocity(_slot, velocity);
            ForgetSpeed();
            _presentation.Notify();
        }

        /// <summary>
        /// Puts it at <paramref name="value"/> at once, model and presentation, moving at <paramref name="velocity"/>
        /// (converted units a second) until it is next set: for what moves it on its own and knows its speed (a scroll
        /// dragged or gliding), so a change made meanwhile sets off at that speed.
        /// </summary>
        internal void MoveAt(T value, Vector4 velocity)
        {
            SetModel(value, null);
            if (_slot < 0 && s_converter != null)
            {
                _movingAt = velocity;
                _movingFrame = UIFrame.FrameIndex;
            }
        }

        /// <summary>
        /// Moves where it is drawn by <paramref name="by"/> (converted units), keeping where it is going, its speed and its
        /// change: a spring set off before anything says where it starts (a flight, placed on its first frame).
        /// </summary>
        internal void Displace(Vector4 by)
        {
            if (_slot < 0) return;
            SpringStore.SetValue(_slot, SpringStore.Value(_slot) + by);
            _presentation.Notify();
        }

        /// <summary>
        /// Moves it by <paramref name="delta"/>, model and presentation alike, keeping any motion under way (a spring heads
        /// on to its moved target at the speed it had): what a scroll offset does as the content under it moves.
        /// </summary>
        internal void Shift(T delta)
        {
            if (s_converter == null) return;
            var d = s_converter.ToVector(delta);
            _model = s_converter.FromVector(s_converter.ToVector(_model) + d);
            NotifyChanged();
            if (_slot >= 0)
            {
                SpringStore.SetValue(_slot, SpringStore.Value(_slot) + d);
                SpringStore.SetTarget(_slot, SpringStore.Target(_slot) + d);
            }
            if (_pending)
                _pendingFrom += d;
            _presentation.Notify();
        }

        /// <summary>
        /// Puts it at <paramref name="value"/> at once, model and presentation, still, whatever change is being made. A
        /// spring the change being made set off on it ends as arrived: the change itself put it here, so it still completes.
        /// </summary>
        internal void Jump(T value)
        {
            DropPending(arrived: false);
            ForgetSpeed();
            bool changed = !s_comparer.Equals(_model, value);
            _model = value;
            if (changed) NotifyChanged();
            if (_slot >= 0)
                SpringStore.Stop(_slot, arrived: UITransaction.Current != null && SpringStore.TransactionOf(_slot) == UITransaction.Current);
            else if (changed)
                _presentation.Notify();
        }

        /// <summary>Puts its presentation where its model is, still, cutting short any change moving it.</summary>
        public void Settle()
        {
            ForgetSpeed();
            if (DropPending(arrived: false))
                _presentation.Notify();
            if (_slot < 0) return;
            SpringStore.Stop(_slot, arrived: false);
        }

        // It has no speed but a spring's: what a gesture sampled, or what moved it, is forgotten.
        private void ForgetSpeed()
        {
            _samples?.Clear();
            _movingAt = Vector4.zero;
        }

        /// <summary>A change of a value that cannot interpolate was made on <paramref name="animation"/>.</summary>
        private protected virtual void OnSnap(UIAnimation animation) { }

        private void Present(T previous, UIAnimation? implicitAnimation)
        {
            var transaction = UITransaction.Current;
            var animation = transaction?.Animation ?? implicitAnimation;
            if (s_converter == null && UIFrame.Live && animation is { AtOnce: false } snapped)
                OnSnap(snapped);
            bool animated = s_converter != null && UIFrame.Live && animation.HasValue && !animation.Value.AtOnce;
            if (animated && (_slot >= 0 || HasPresentationReaders || _alwaysAnimates))
            {
                if (_slot < 0)
                {
                    // From where it is drawn, at the speed it is moving: a value a gesture moved carries on at its speed.
                    var from = _pending ? _pendingFrom : s_converter.ToVector(previous);
                    var velocity = _pending ? _pendingVelocity : Velocity;
                    _slot = SpringStore.Allocate(this, s_converter.Dimensions, _rest, from);
                    SpringStore.SetVelocity(_slot, velocity);
                }
                SpringStore.Retarget(_slot, s_converter.ToVector(_model), animation.Value, transaction);
                ForgetSpeed();
                DropPending(arrived: _pendingTransaction == transaction);
                return;
            }
            if (animated && _slot < 0)
            {
                // Nothing draws it yet: the first drawing to read it this frame springs it from where it was.
                if (!_pending)
                {
                    _pending = true;
                    _pendingFrom = s_converter.ToVector(previous);
                    _pendingVelocity = Velocity;
                    ForgetSpeed();
                    PendingSprings.Add(this);
                }
                if (_pendingTransaction != transaction)
                {
                    _pendingTransaction?.Release(arrived: false);
                    transaction?.Hold();
                    _pendingTransaction = transaction;
                }
                _pendingAnimation = animation.Value;
                _pendingFrame = UIFrame.FrameIndex;
                _presentation.Notify();
                return;
            }
            DropPending(arrived: false);
            bool direct = transaction == null && implicitAnimation == null;
            if (_slot >= 0 && direct && GestureVelocity.Current == null)
            {
                // Moving, and set directly by anything but a gesture: it heads there, keeping its speed, spring and change.
                ForgetSpeed();
                SpringStore.SetTarget(_slot, s_converter.ToVector(_model));
                return;
            }
            // Set by a gesture, at rest, or in a change that lands at once: it lands where it is set.
            if (_slot >= 0)
                SpringStore.Stop(_slot, arrived: transaction != null && SpringStore.TransactionOf(_slot) == transaction);
            Land(direct);
            _presentation.Notify();
        }

        // It landed where it was set: still, unless a gesture's change callback set it, which samples it for the speed the
        // gesture hands on.
        private void Land(bool direct)
        {
            var gesture = GestureVelocity.Current;
            if (direct && gesture != null && gesture.Samples && s_converter != null)
            {
                _movingAt = Vector4.zero;
                gesture.Sample(this);
                return;
            }
            ForgetSpeed();
        }

        // The first drawing to read a change held for it: it springs from where it was drawn, at the speed it had.
        private void StartPending()
        {
            var transaction = _pendingTransaction;
            _pending = false;
            _pendingTransaction = null;
            if (_slot < 0 && UIFrame.Live)
            {
                _slot = SpringStore.Allocate(this, s_converter.Dimensions, _rest, _pendingFrom);
                SpringStore.SetVelocity(_slot, _pendingVelocity);
                SpringStore.Retarget(_slot, s_converter.ToVector(_model), _pendingAnimation, transaction);
            }
            transaction?.Release(arrived: true);
        }

        // Lets go of a change held for drawing; false if there was none. `arrived`: whether its change carries on (or
        // landed), rather than being cut short.
        private bool DropPending(bool arrived)
        {
            if (!_pending) return false;
            _pending = false;
            _pendingTransaction?.Release(arrived);
            _pendingTransaction = null;
            return true;
        }

        bool IPendingSpring.LandUnread(int frame)
        {
            if (!_pending) return true;
            if (_pendingFrame >= frame) return false;
            DropPending(arrived: true);
            _presentation.Notify();
            return true;
        }

        void ISpeedSampled.AddSample(double time)
        {
            if (s_converter == null) return;
            (_samples ??= new VelocityTracker()).Add(s_converter.ToVector(_model), time);
        }

        void ISpeedSampled.ForgetSamples() => _samples?.Clear();

        void ISpringOwner.OnSpringMoved() => _presentation.Notify();

        void ISpringOwner.OnSpringSettled()
        {
            _slot = -1;
            _presentation.Notify();
        }

        // The presentation face: an observable of its own, so drawing that reads it is told when it moves while layout,
        // which reads the model, is not.
        private sealed class PresentationFace : Observable
        {
            public void TrackRead() => Track();

            public void Notify() => NotifyChanged();
        }

        public override string ToString() => _model?.ToString() ?? "null";
    }
}
