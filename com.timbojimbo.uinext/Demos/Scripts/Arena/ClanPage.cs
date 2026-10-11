using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>A request for cards in the clan's chat, as the builder made it.</summary>
    [Serializable]
    public sealed class RequestParts
    {
        public int Message;
        public Pressable Donate;
        public Label DonateLabel;
        public UINode Fill;
        public Label Progress;
    }

    /// <summary>
    /// The Clan page's chat: a clanmate's request for cards fills as you donate, one copy a tap, each earning a little
    /// gold, its bar springing; full, its button says so and takes no more.
    /// </summary>
    public sealed class ClanPage : MonoBehaviour
    {
        [SerializeField] private List<RequestParts> _requests = new();
        [SerializeField] private ArenaApp _app;

        // The gold a donated copy earns.
        private const int Reward = 10;

        private static readonly UIAnimation Fill = new(0.35f, 0.3f);

        private int[] _have;

        public void Setup(List<RequestParts> requests, ArenaApp app)
        {
            _requests = requests;
            _app = app;
        }

        private void Awake()
        {
            _have = new int[_requests.Count];
            for (int r = 0; r < _requests.Count; r++)
                _have[r] = ArenaData.Chat[_requests[r].Message].Have;
        }

        private void OnEnable()
        {
            for (int r = 0; r < _requests.Count; r++)
            {
                int index = r;
                _requests[r].Donate.Tapped += () => Donate(index);
            }
        }

        private void Start()
        {
            for (int r = 0; r < _requests.Count; r++)
                Show(r);
        }

        private void Donate(int r)
        {
            int need = ArenaData.Chat[_requests[r].Message].Need;
            if (_have[r] >= need) return;
            _have[r]++;
            _app.Earn(Reward, 0);
            UI.Animate(Fill, () => Show(r));
        }

        private void Show(int r)
        {
            var request = _requests[r];
            int need = ArenaData.Chat[request.Message].Need;
            request.Fill.Width.Value = Sizing.Percent(Mathf.Clamp01((float)_have[r] / need));
            request.Progress.Text.Value = _have[r] + "/" + need;
            bool full = _have[r] >= need;
            request.Donate.Interactable.Value = !full;
            request.DonateLabel.Text.Value = full ? "Done" : "Donate";
        }
    }
}
