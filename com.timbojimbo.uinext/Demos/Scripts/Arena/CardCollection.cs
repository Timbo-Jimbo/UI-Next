using System;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>
    /// The cards you have: each card's level and how many copies are waiting towards its next one, starting from
    /// <see cref="ArenaData.Cards"/>. Chests and the shop add copies; an upgrade spends them for a level.
    /// </summary>
    public sealed class CardCollection : MonoBehaviour
    {
        private int[] _levels;
        private int[] _counts;

        /// <summary>Raised as a card's level or count changes, with the card.</summary>
        public event Action<int> Changed;

        private void Awake() => Load();

        private void Load()
        {
            if (_levels != null) return;
            int n = ArenaData.Cards.Length;
            _levels = new int[n];
            _counts = new int[n];
            for (int i = 0; i < n; i++)
            {
                _levels[i] = ArenaData.Cards[i].Level;
                _counts[i] = ArenaData.Cards[i].Count;
            }
        }

        public int Level(int card)
        {
            Load();
            return _levels[card];
        }

        public int Count(int card)
        {
            Load();
            return _counts[card];
        }

        /// <summary>How many copies the next level takes.</summary>
        public int Needed(int card) => ArenaData.Cards[card].Needed;

        /// <summary>Whether it has the copies for its next level.</summary>
        public bool CanUpgrade(int card) => Count(card) >= Needed(card);

        public void Add(int card, int count)
        {
            Load();
            _counts[card] += Mathf.Max(0, count);
            Changed?.Invoke(card);
        }

        /// <summary>Spends a card's copies for a level: false if it has too few.</summary>
        public bool Upgrade(int card)
        {
            if (!CanUpgrade(card)) return false;
            _counts[card] -= Needed(card);
            _levels[card]++;
            Changed?.Invoke(card);
            return true;
        }
    }
}
