using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>A value a drawable draws from: a change of it that cannot interpolate cross-fades the drawing.</summary>
    internal interface IDrawnValue
    {
        bool CrossFades { set; }
    }

    /// <summary>The non-generic face of every <see cref="UIValue{T}"/>, for the components that own them.</summary>
    public interface IUIValue
    {
        /// <summary>Ties it to the node whose states and modes it reads, and resolves it.</summary>
        void Attach(UINode owner);

        /// <summary>Lets go of its node and stops resolving.</summary>
        void Detach();

        /// <summary>Takes up an edit made to its serialized fields (the inspector, an undo).</summary>
        void Refresh();
    }

    /// <summary>What the editor reads of a value's variants: which gives its value now, and the states each is for.</summary>
    internal interface IVariantSource
    {
        /// <summary>The variant giving its value now (its index in the list), or -1 for its base.</summary>
        int Winner { get; }

        /// <summary>The states the variant at <paramref name="index"/> applies in.</summary>
        StateCondition When(int index);
    }

    /// <summary>
    /// A styleable, animatable property: every property in the stack is one. Its value is resolved from, in rising
    /// priority, its literal or token or binding, then the variant for the most important of the states active on its
    /// node. It has two faces (<see cref="AnimatedValue{T}"/>): layout and game code see where it is going, drawing sees
    /// where it is drawn, and a change inside <see cref="UI.Animate(UIAnimation, Action)"/> springs between them.
    /// </summary>
    [Serializable]
    public class UIValue<T> : AnimatedValue<T>, IUIValue, IDrawnValue, IVariantSource, ISerializationCallbackReceiver
    {
        [SerializeField] private T _value;
        [SerializeField] private TokenAsset _token;
        [SerializeField] private List<Variant<T>> _variants;

        [NonSerialized] private Func<T> _binding;
        [NonSerialized] private UINode _owner;
        [NonSerialized] private Resolver _resolver;
        [NonSerialized] private int _winner = -1;
        [NonSerialized] private bool _crossFades;

        public UIValue() { }

        public UIValue(T value) : base(value) => _value = value;

        public static implicit operator UIValue<T>(T value) => new(value);

        /// <summary>
        /// The face for whoever is reading (see <see cref="AnimatedValue{T}.Value"/>). Setting it sets the literal: inside
        /// <see cref="UI.Animate(UIAnimation, Action)"/> it springs there, otherwise it lands at once.
        /// </summary>
        public new T Value
        {
            get => base.Value;
            set => Literal = value;
        }

        /// <summary>Its own value, used when no token, binding or active variant gives another.</summary>
        public T Literal
        {
            get => _value;
            set
            {
                _value = value;
                Resolve();
            }
        }

        /// <summary>The token it takes its value from, or null for its literal.</summary>
        public TokenAsset Token
        {
            get => _token;
            set
            {
                _token = value;
                Resolve();
            }
        }

        /// <summary>Its per-state values, in no particular order (priority decides).</summary>
        public IReadOnlyList<Variant<T>> Variants => (IReadOnlyList<Variant<T>>)_variants ?? Array.Empty<Variant<T>>();

        /// <summary>Gives it a value for <paramref name="when"/>.</summary>
        public UIValue<T> When(StateCondition when, T value)
        {
            _variants ??= new List<Variant<T>>();
            for (int i = 0; i < _variants.Count; i++)
            {
                if (!SameCondition(_variants[i].When, when)) continue;
                _variants[i] = new Variant<T>(when, value);
                Resolve();
                return this;
            }
            _variants.Add(new Variant<T>(when, value));
            Resolve();
            return this;
        }

        /// <summary>Gives it a value for an interaction state.</summary>
        public UIValue<T> When(InteractionStates state, T value) => When(new StateCondition(state), value);

        /// <summary>Gives it a value for a custom state.</summary>
        public UIValue<T> When(UIState state, T value) => When(new StateCondition(InteractionStates.None, state), value);

        /// <summary>
        /// Takes its base value from <paramref name="expression"/>, run again whenever an observable it reads changes,
        /// as a binding. Active variants still win over it.
        /// </summary>
        public void Bind(Func<T> expression)
        {
            _binding = expression ?? throw new ArgumentNullException(nameof(expression));
            Resolve();
        }

        /// <summary>Goes back to its literal or token.</summary>
        public void Unbind()
        {
            if (_binding == null) return;
            _binding = null;
            Resolve();
        }

        // ── IUIValue ─────────────────────────────────────────────────────────────

        void IUIValue.Attach(UINode owner)
        {
            _owner = owner;
            Resolve();
        }

        void IUIValue.Detach()
        {
            _owner = null;
            _resolver?.Dispose();
            _resolver = null;
        }

        // Deserialization has written the model already (OnAfterDeserialize), so the change must be forced through
        // to what reads it.
        void IUIValue.Refresh() => Resolve(force: true);

        bool IDrawnValue.CrossFades
        {
            set => _crossFades = value;
        }

        int IVariantSource.Winner
        {
            get
            {
                EnsureResolved();
                return _winner;
            }
        }

        StateCondition IVariantSource.When(int index) => _variants[index].When;

        // A drawable's value that cannot interpolate, changed on an animation (a label's text, an image's sprite):
        // its node's old drawing fades out as the new one fades in.
        private protected override void OnSnap(UIAnimation animation)
        {
            if (_crossFades && _owner != null)
                _owner.CrossFadeDrawing(animation);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // May run off the main thread: only fields are touched.
            _model = _value;
        }

        // ── Resolution ───────────────────────────────────────────────────────────

        private protected override void EnsureResolved()
        {
            if (_resolver != null && _resolver.Dirty)
                _resolver.Run();
        }

        private bool NeedsResolver => _binding != null || _token != null || (_variants != null && _variants.Count > 0);

        // Resolves now. A plain literal needs no computation; anything that reads states, tokens or a binding gets one,
        // which runs again whenever what it read changes.
        // Whether the next resolution counts as a change even if the value is the same (an inspector edit).
        [NonSerialized] private bool _force;

        private void Resolve(bool force = false)
        {
            _force = force;
            if (!NeedsResolver)
            {
                _resolver?.Dispose();
                _resolver = null;
                _winner = -1;
                SetModel(_value, null, force);
                _force = false;
                return;
            }
            _resolver ??= new Resolver(this);
            _resolver.Run();
        }

        private void Recompute()
        {
            T resolved = _binding != null ? _binding() : _token is UIToken<T> token ? token.Resolve(_owner) : _value;
            int winner = Winner();
            if (winner >= 0)
            {
                var variant = _variants[winner];
                resolved = variant.Token is UIToken<T> variantToken ? variantToken.Resolve(_owner) : variant.Value;
            }
            var animation = winner != _winner ? StateAnimation(winner, _winner) : null;
            _winner = winner;
            SetModel(resolved, animation, _force);
            _force = false;
        }

        // The active variant with the highest priority: level, then how many conditions, then the later in the list.
        private int Winner()
        {
            if (_variants == null || _owner == null) return -1;
            int best = -1, bestLevel = int.MinValue, bestCount = -1;
            for (int i = 0; i < _variants.Count; i++)
            {
                var when = _variants[i].When;
                if (when.IsEmpty || !IsActive(when)) continue;
                int level = when.Level, count = when.Count;
                if (level < bestLevel || (level == bestLevel && count < bestCount)) continue;
                best = i;
                bestLevel = level;
                bestCount = count;
            }
            return best;
        }

        private bool IsActive(in StateCondition when)
        {
            if (when.Interaction != InteractionStates.None && (_owner.ActiveInteraction() & when.Interaction) != when.Interaction)
                return false;
            if (when.States != null)
            {
                foreach (var state in when.States)
                    if (state != null && !_owner.IsStateActive(state))
                        return false;
            }
            return true;
        }

        // The animation a change of winning variant moves on outside any Animate: the entering variant's state's enter
        // animation, or else the leaving one's exit animation.
        private UIAnimation? StateAnimation(int entering, int leaving)
        {
            if (entering >= 0 && EnterOf(_variants[entering].When) is { } enter) return enter;
            if (leaving >= 0 && leaving < (_variants?.Count ?? 0) && ExitOf(_variants[leaving].When) is { } exit) return exit;
            return null;
        }

        private static UIAnimation? EnterOf(in StateCondition when)
        {
            if (when.Interaction != InteractionStates.None)
                return InteractionAnimations.Enter(InteractionAnimations.Highest(when.Interaction));
            if (when.States != null)
                foreach (var state in when.States)
                    if (state != null && state.Enter is { } enter)
                        return enter;
            return null;
        }

        private static UIAnimation? ExitOf(in StateCondition when)
        {
            if (when.Interaction != InteractionStates.None)
                return InteractionAnimations.Exit(InteractionAnimations.Highest(when.Interaction));
            if (when.States != null)
                foreach (var state in when.States)
                    if (state != null && state.Exit is { } exit)
                        return exit;
            return null;
        }

        private static bool SameCondition(in StateCondition a, in StateCondition b)
        {
            if (a.Interaction != b.Interaction) return false;
            int countA = a.States?.Length ?? 0, countB = b.States?.Length ?? 0;
            if (countA != countB) return false;
            for (int i = 0; i < countA; i++)
                if (Array.IndexOf(b.States, a.States[i]) < 0)
                    return false;
            return true;
        }

        private sealed class Resolver : Computation
        {
            private readonly UIValue<T> _value;

            public Resolver(UIValue<T> value) : base(Phase.Model) => _value = value;

            protected override void Execute() => _value.Recompute();
        }
    }
}
