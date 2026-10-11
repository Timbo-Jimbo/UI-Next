using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>A chest slot on the Battle page, as the builder made it.</summary>
    [Serializable]
    public sealed class ChestSlotParts
    {
        public Pressable Button;
        public UINode Art;
        public ChestArt Chest;
        public Label Status;
        public Label Time;
        public UINode Glow;
    }

    /// <summary>The chest popup, as the builder made it: its details and buttons, and where rewards are revealed.</summary>
    [Serializable]
    public sealed class ChestPopupParts
    {
        public Presentation Presentation;
        public Label Title;
        public UINode Art;
        public ChestArt Chest;
        public UINode Details;
        public Label Contents;
        public Label UnlockTime;
        public Pressable Primary;
        public Label PrimaryLabel;
        public Pressable Gems;
        public Label GemsLabel;
        public Pressable Close;
        public UINode Reward;
        public Label RewardIcon;
        public Label RewardName;
        public Label RewardCount;
        public Pressable Next;
        public Label NextLabel;
    }

    /// <summary>
    /// The four chest slots and the popup a chest opens into: a slot's chest grows into the popup (they share a match
    /// name) and goes back into it. A locked chest starts unlocking, one at a time, its time counting down in its slot (a
    /// minute a second, so one opens while you watch); gems open one at once; one ready opens, the chest bouncing and its
    /// rewards (gold, then cards) revealed one tap at a time, each popping in and going into the wallet or the collection.
    /// </summary>
    public sealed class ChestSlots : MonoBehaviour
    {
        [SerializeField] private List<ChestSlotParts> _slots = new();
        [SerializeField] private ChestPopupParts _popup = new();
        [SerializeField] private ArenaApp _app;
        [SerializeField] private CardCollection _cards;

        // Seconds of unlocking time that pass each real second.
        private const float TimeScale = 60f;

        private static readonly Color Ready = new Color32(0x7C, 0xE8, 0x5A, 0xFF);
        private static readonly UIAnimation Pop = new(0.45f, 0.45f);
        private static readonly UIAnimation Swap = new(0.3f);

        private ChestKind[] _kinds;
        private ChestState[] _states;
        private float[] _remaining;
        private int _open = -1;
        // The chest the popup shows, and what it grew out of (a slot's chest, or a chest bought in the shop).
        private ChestKind _openKind;
        private UINode _from;
        private readonly List<(string Icon, string Name, int Count, int Card)> _rewards = new();
        private int _reward;

        public void Setup(List<ChestSlotParts> slots, ChestPopupParts popup, ArenaApp app, CardCollection cards)
        {
            _slots = slots;
            _popup = popup;
            _app = app;
            _cards = cards;
        }

        private void Awake()
        {
            int n = _slots.Count;
            _kinds = new ChestKind[n];
            _states = new ChestState[n];
            _remaining = new float[n];
            for (int i = 0; i < n && i < ArenaData.Slots.Length; i++)
            {
                var (kind, state, hours) = ArenaData.Slots[i];
                _kinds[i] = kind;
                _states[i] = state;
                _remaining[i] = hours * 3600f;
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                int index = i;
                _slots[i].Button.Tapped += () => Open(index);
            }
            _popup.Primary.Tapped += OnPrimary;
            _popup.Gems.Tapped += OnGems;
            _popup.Close.Tapped += Close;
            _popup.Next.Tapped += NextReward;
            _popup.Presentation.Dismissing += OnDismissing;
        }

        private void OnDisable()
        {
            _popup.Primary.Tapped -= OnPrimary;
            _popup.Gems.Tapped -= OnGems;
            _popup.Close.Tapped -= Close;
            _popup.Next.Tapped -= NextReward;
            _popup.Presentation.Dismissing -= OnDismissing;
        }

        private void Start()
        {
            for (int i = 0; i < _slots.Count; i++)
                Show(i);
        }

        private void Update()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_states[i] != ChestState.Unlocking) continue;
                int before = Mathf.CeilToInt(_remaining[i]);
                _remaining[i] = Mathf.Max(0f, _remaining[i] - Time.unscaledDeltaTime * TimeScale);
                if (_remaining[i] <= 0f)
                {
                    UI.Animate(Swap, () =>
                    {
                        _states[i] = ChestState.Ready;
                        Show(i);
                    });
                    if (_open == i && _rewards.Count == 0) FillPopup();
                }
                else if (Mathf.CeilToInt(_remaining[i]) != before)
                {
                    _slots[i].Time.Text.Value = Clock(_remaining[i]);
                    if (_open == i && _rewards.Count == 0) FillPopup();
                }
            }
        }

        // A slot as its chest is now: empty, waiting with its unlock time, counting down, or ready with a glow.
        private void Show(int i)
        {
            var slot = _slots[i];
            var state = _states[i];
            slot.Chest.Kind.Value = _kinds[i];
            if (_open != i)
                slot.Art.Display.Value = state == ChestState.Empty ? DisplayMode.Hidden : DisplayMode.Visible;
            slot.Glow.Opacity.Value = state == ChestState.Ready ? 1f : 0f;
            slot.Button.Interactable.Value = state != ChestState.Empty;
            slot.Status.Color.Value = state == ChestState.Ready ? Ready : Color.white;
            slot.Status.Text.Value = state switch
            {
                ChestState.Empty => "Chest Slot",
                ChestState.Locked => "Tap to Unlock",
                ChestState.Unlocking => "Unlocking",
                _ => "Open!",
            };
            slot.Time.Text.Value = state switch
            {
                ChestState.Locked => Mathf.RoundToInt(_remaining[i] / 3600f) + "h",
                ChestState.Unlocking => Clock(_remaining[i]),
                _ => "",
            };
        }

        /// <summary>Opens the <paramref name="i"/>th slot's chest into the popup: its chest grows out of the slot.</summary>
        public void Open(int i)
        {
            if (_popup.Presentation.IsPresented || _states[i] == ChestState.Empty) return;
            _open = i;
            _openKind = _kinds[i];
            _from = _slots[i].Art;
            _rewards.Clear();
            _popup.Art.MatchId.Value = _from.MatchId.Peek;
            _popup.Art.Scale.Value = 1f;
            FillPopup();
            UI.Animate(_popup.Presentation.Animation, () =>
            {
                _slots[i].Art.Display.Value = DisplayMode.Hidden;
                _popup.Details.Display.Value = DisplayMode.Visible;
                _popup.Reward.Display.Value = DisplayMode.None;
                _popup.Presentation.Present();
            });
        }

        /// <summary>
        /// Opens a chest bought in the shop: it grows out of <paramref name="from"/> (the offer's chest, named as the
        /// popup's) and is opened at once.
        /// </summary>
        public void OpenBought(ChestKind kind, UINode from)
        {
            if (_popup.Presentation.IsPresented) return;
            _open = -1;
            _openKind = kind;
            _from = from;
            _popup.Art.MatchId.Value = from.MatchId.Peek;
            _popup.Art.Scale.Value = 1f;
            _popup.Title.Text.Value = ArenaData.ChestName(kind);
            _popup.Chest.Kind.Value = kind;
            UI.Animate(_popup.Presentation.Animation, () =>
            {
                from.Display.Value = DisplayMode.Hidden;
                _popup.Presentation.Present();
            });
            Reveal();
        }

        private void Close() => _popup.Presentation.Dismiss();

        // Inside the change that takes the popup away: what the chest grew out of comes back for it to go into, unless
        // it was a slot's chest, opened.
        private void OnDismissing()
        {
            if (_from != null && (_open < 0 || _states[_open] != ChestState.Empty))
                _from.Display.Value = DisplayMode.Visible;
            _open = -1;
            _from = null;
        }

        // The popup's details for the open chest, as it is now.
        private void FillPopup()
        {
            int i = _open;
            if (i < 0) return;
            var kind = _kinds[i];
            var state = _states[i];
            var (goldMin, goldMax, cards) = Contents(kind);
            _popup.Title.Text.Value = ArenaData.ChestName(kind);
            _popup.Chest.Kind.Value = kind;
            _popup.Contents.Text.Value = "💰 " + goldMin + "–" + goldMax + "     🃏 x" + cards;
            _popup.UnlockTime.Text.Value = state switch
            {
                ChestState.Locked => "Unlock time: " + Mathf.RoundToInt(_remaining[i] / 3600f) + "h",
                ChestState.Unlocking => "Unlocking: " + Clock(_remaining[i]),
                _ => "Ready to open!",
            };
            bool another = state == ChestState.Locked && Array.IndexOf(_states, ChestState.Unlocking) >= 0;
            _popup.Primary.Node.Display.Value = state == ChestState.Unlocking ? DisplayMode.None : DisplayMode.Visible;
            _popup.Primary.Interactable.Value = !another;
            _popup.PrimaryLabel.Text.Value = state == ChestState.Ready ? "Open" : another ? "Another chest is unlocking" : "Start Unlock";
            _popup.Gems.Node.Display.Value = state == ChestState.Ready ? DisplayMode.None : DisplayMode.Visible;
            _popup.GemsLabel.Text.Value = "Open now  💎 " + GemCost(i);
        }

        private void OnPrimary()
        {
            int i = _open;
            if (i < 0) return;
            if (_states[i] == ChestState.Ready)
            {
                Reveal();
                return;
            }
            if (_states[i] != ChestState.Locked) return;
            _states[i] = ChestState.Unlocking;
            Show(i);
            Close();
        }

        private void OnGems()
        {
            int i = _open;
            if (i < 0 || _states[i] == ChestState.Ready) return;
            if (!_app.Spend(0, GemCost(i)))
            {
                UI.Animate(Swap, () => _popup.GemsLabel.Text.Value = "Not enough gems");
                return;
            }
            _states[i] = ChestState.Ready;
            Show(i);
            Reveal();
        }

        // Opens the chest in the popup: it bounces, and its rewards are drawn and revealed one at a time.
        private void Reveal()
        {
            var (goldMin, goldMax, cards) = Contents(_openKind);
            _rewards.Clear();
            int gold = UnityEngine.Random.Range(goldMin, goldMax + 1);
            _rewards.Add(("💰", "Gold", gold, -1));
            int left = cards;
            for (int k = 0; k < 3 && left > 0; k++)
            {
                int card = UnityEngine.Random.Range(0, ArenaData.Cards.Length);
                int count = k == 2 ? left : Mathf.Max(1, left / 2);
                left -= count;
                _rewards.Add((ArenaData.Cards[card].Emoji, ArenaData.Cards[card].Name, count, card));
            }
            _reward = 0;
            var art = _popup.Art;
            UI.Animate(new UIAnimation(0.12f), () => art.Scale.Value = 1.2f).Finished += () => UI.Animate(Pop, () => art.Scale.Value = 1f);
            UI.Animate(Swap, () =>
            {
                _popup.Details.Display.Value = DisplayMode.None;
                _popup.Reward.Display.Value = DisplayMode.Visible;
            });
            ShowReward();
        }

        // The current reward pops in, and goes where it goes.
        private void ShowReward()
        {
            var (icon, name, count, card) = _rewards[_reward];
            _popup.RewardIcon.Text.Value = icon;
            _popup.RewardName.Text.Value = name;
            _popup.RewardCount.Text.Value = "x" + ArenaData.Number(count);
            _popup.NextLabel.Text.Value = _reward < _rewards.Count - 1 ? "Next" : "Collect";
            var reward = _popup.Reward;
            reward.Scale.Value = 0.6f;
            UI.Animate(Pop, () => reward.Scale.Value = 1f);
            if (card < 0) _app.Earn(count, 0);
            else _cards.Add(card, count);
        }

        private void NextReward()
        {
            if (_rewards.Count == 0) return;
            _reward++;
            if (_reward < _rewards.Count)
            {
                ShowReward();
                return;
            }
            _rewards.Clear();
            if (_open >= 0)
            {
                _states[_open] = ChestState.Empty;
                Show(_open);
            }
            Close();
        }

        // What a chest holds: gold from and to, and how many cards.
        private static (int GoldMin, int GoldMax, int Cards) Contents(ChestKind kind) => kind switch
        {
            ChestKind.Wooden => (40, 60, 8),
            ChestKind.Silver => (120, 160, 24),
            ChestKind.Golden => (400, 520, 90),
            _ => (1200, 1400, 260),
        };

        // Gems to open a chest at once: one for every ten minutes left, at least one.
        private int GemCost(int i) => Mathf.Max(1, Mathf.CeilToInt(_remaining[i] / 600f));

        private static string Clock(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return s / 3600 + ":" + (s / 60 % 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
    }
}
