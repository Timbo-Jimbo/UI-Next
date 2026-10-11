using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// One of a few options, chosen by tapping it: a segmented control, a row of swatches, a list with a check mark. The
    /// chosen option's Pressable is Selected, which its values follow (a fill, a border, a check mark's opacity), and a
    /// pill, if it has one, floats on the chosen option, springing across as the choice changes.
    /// </summary>
    public sealed class SingleChoice : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _options = new();
        [SerializeField] private int _chosen;
        [SerializeField] private UINode _pill;

        /// <summary>Raised as another option is chosen, with its index.</summary>
        public event Action<int> Changed;

        /// <summary>The index of the option chosen.</summary>
        public int Chosen => _chosen;

        /// <summary>The options, the one chosen, and a pill that floats on the one chosen (null for none).</summary>
        public void Setup(List<Pressable> options, int chosen, UINode pill = null)
        {
            _options = options;
            _chosen = chosen;
            _pill = pill;
            Show(chosen);
        }

        private void OnEnable()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                int index = i;
                _options[i].Tapped += () => Choose(index);
            }
        }

        /// <summary>Chooses the <paramref name="index"/>th option, on a spring.</summary>
        public void Choose(int index)
        {
            if (index == _chosen || index < 0 || index >= _options.Count) return;
            _chosen = index;
            UI.Animate(UIAnimation.Snappy, () => Show(index));
            Changed?.Invoke(index);
        }

        private void Show(int index)
        {
            for (int i = 0; i < _options.Count; i++)
                _options[i].IsSelected = i == index;
            if (_pill == null) return;
            var floating = _pill.Floating.Peek;
            floating.Element = _options[index].Node;
            _pill.Floating.Value = floating;
        }
    }
}
