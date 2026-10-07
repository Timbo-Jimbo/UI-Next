using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// A named shared value, as Figma's variables and CSS custom properties: a default, and a value per mode (a
    /// <see cref="UIState"/> of kind Mode, such as dark). A property pointing at a token takes the value for the modes
    /// active on its node; change the token and everything pointing at it updates, animating inside a change.
    /// </summary>
    public abstract class TokenAsset : ScriptableObject
    {
        // Bumped whenever the asset is edited, so what resolved it resolves again.
        [NonSerialized] private Observable<int> _revision;

        private protected Observable<int> Revision => _revision ??= new Observable<int>();

        protected virtual void OnValidate() => Revision.Value++;
    }

    /// <summary>One mode's value of a token.</summary>
    [Serializable]
    public struct TokenMode<T>
    {
        public UIState Mode;
        public T Value;
    }

    /// <summary>A token holding values of <typeparamref name="T"/>.</summary>
    public abstract class UIToken<T> : TokenAsset
    {
        [SerializeField] private T _default;
        [SerializeField] private List<TokenMode<T>> _modes = new();

        /// <summary>The value with no mode active.</summary>
        public T Default
        {
            get => _default;
            set
            {
                _default = value;
                Revision.Value++;
            }
        }

        /// <summary>The value for the first of its modes active on <paramref name="node"/>, or its default. Tracked.</summary>
        public T Resolve(UINode node)
        {
            _ = Revision.Value;
            if (node != null)
            {
                for (int i = 0; i < _modes.Count; i++)
                {
                    var mode = _modes[i];
                    if (mode.Mode != null && node.IsStateActive(mode.Mode))
                        return mode.Value;
                }
            }
            return _default;
        }

        /// <summary>Sets the value for <paramref name="mode"/>.</summary>
        public void SetMode(UIState mode, T value)
        {
            for (int i = 0; i < _modes.Count; i++)
            {
                if (_modes[i].Mode != mode) continue;
                _modes[i] = new TokenMode<T> { Mode = mode, Value = value };
                Revision.Value++;
                return;
            }
            _modes.Add(new TokenMode<T> { Mode = mode, Value = value });
            Revision.Value++;
        }
    }
}
