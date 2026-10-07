using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// A value with two faces: its model, where it is going, and its presentation, where it is drawn right now, as
    /// Core Animation's model and presentation layers. Code running in the draw phase sees the presentation; everything
    /// else (layout, bindings, game code) sees the model. A change made inside <see cref="UI.Animate(UIAnimation, System.Action)"/>
    /// springs the presentation from where it is drawn to the new model; any other change puts it there at once.
    /// </summary>
    public class AnimatedValue<T> : Observable, ISpringOwner, IPendingSpring
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
        private UIAnimation _pendingAnimation;
        private UITransaction _pendingTransaction;
        private int _pendingFrame;

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

        /// <summary>How fast it is moving, in converted units a second (zero at rest).</summary>
        public Vector4 Velocity => _slot >= 0 ? SpringStore.Velocity(_slot) : Vector4.zero;

        // Whether anything draws it, so springs run only for values someone sees.
        internal bool HasPresentationReaders => _presentation.Readers.Count > 0;

        /// <summary>Brings the model up to date before it is read (a value with a pending resolution resolves it now).</summary>
        private protected virtual void EnsureResolved() { }

        /// <summary>
        /// Gives it a new model. Inside a change (<see cref="UITransaction.Current"/>) its presentation springs there on
        /// the change's animation; outside one, <paramref name="implicitAnimation"/> (a state's) is used if given; a value
        /// already moving with neither heads there keeping its speed; otherwise it lands at once.
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
        /// Moves its presentation the way it would have moved had the model just changed from <paramref name="drawnFrom"/>,
        /// with a velocity (a fling): for gestures handing their speed on.
        /// </summary>
        internal void Fling(Vector4 velocity, UIAnimation animation)
        {
            if (s_converter == null || !UIFrame.Live) return;
            DropPending(arrived: false);
            if (_slot < 0)
                _slot = SpringStore.Allocate(this, s_converter.Dimensions, _rest, s_converter.ToVector(_model));
            SpringStore.Retarget(_slot, s_converter.ToVector(_model), animation, null);
            SpringStore.AddVelocity(_slot, velocity);
            _presentation.Notify();
        }

        /// <summary>
        /// Gives it a new model and springs its presentation there on <paramref name="animation"/> from where it is
        /// drawn, moving at <paramref name="velocity"/> (converted units a second): a scroll bouncing back from past its
        /// edge, a dragged sheet let go of. Part of no change.
        /// </summary>
        internal void SpringTo(T value, Vector4 velocity, UIAnimation animation)
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
            SpringStore.Retarget(_slot, s_converter.ToVector(value), animation, null);
            SpringStore.SetVelocity(_slot, velocity);
            _presentation.Notify();
        }

        /// <summary>Puts it at <paramref name="value"/> at once, model and presentation, whatever change is being made.</summary>
        internal void Jump(T value)
        {
            DropPending(arrived: false);
            bool changed = !s_comparer.Equals(_model, value);
            _model = value;
            if (changed) NotifyChanged();
            if (_slot >= 0)
                SpringStore.Stop(_slot, arrived: false);
            else if (changed)
                _presentation.Notify();
        }

        /// <summary>Puts its presentation where its model is, at rest, cutting short any change moving it.</summary>
        public void Settle()
        {
            if (DropPending(arrived: false))
                _presentation.Notify();
            if (_slot < 0) return;
            SpringStore.Stop(_slot, arrived: false);
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
                    var from = _pending ? _pendingFrom : s_converter.ToVector(previous);
                    _slot = SpringStore.Allocate(this, s_converter.Dimensions, _rest, from);
                }
                SpringStore.Retarget(_slot, s_converter.ToVector(_model), animation.Value, transaction);
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
            if (_slot >= 0 && transaction == null && implicitAnimation == null)
            {
                // Moving, and changed outside any change: it heads there, keeping its speed, spring and change.
                SpringStore.SetTarget(_slot, s_converter.ToVector(_model));
                return;
            }
            if (_slot >= 0)
                SpringStore.Stop(_slot, arrived: transaction != null && SpringStore.TransactionOf(_slot) == transaction);
            _presentation.Notify();
        }

        // The first drawing to read a change held for it: it springs from where it was drawn.
        private void StartPending()
        {
            var transaction = _pendingTransaction;
            _pending = false;
            _pendingTransaction = null;
            if (_slot < 0 && UIFrame.Live)
            {
                _slot = SpringStore.Allocate(this, s_converter.Dimensions, _rest, _pendingFrom);
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
