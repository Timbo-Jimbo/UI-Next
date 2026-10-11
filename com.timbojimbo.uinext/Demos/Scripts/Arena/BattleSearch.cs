using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>
    /// What Battle (and an event's Play) opens: looking for an opponent, its swords turning and the seconds counting,
    /// until one is found a few seconds in (there is no game to play, so it closes again); Cancel stops looking.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class BattleSearch : MonoBehaviour
    {
        [SerializeField] private List<Pressable> _openers = new();
        [SerializeField] private Pressable _cancel;
        [SerializeField] private UINode _swords;
        [SerializeField] private Label _status;
        [SerializeField] private Label _time;

        // Found after this long, and closed this long after that, in seconds.
        private const float FindAfter = 4f;
        private const float CloseAfter = 1.6f;

        // How fast the swords turn, in degrees a second.
        private const float Turn = 140f;

        private static readonly UIAnimation Change = new(0.35f, 0.3f);

        private Presentation _presentation;
        private float _elapsed;
        private bool _found;

        public void Setup(List<Pressable> openers, Pressable cancel, UINode swords, Label status, Label time)
        {
            _openers = openers;
            _cancel = cancel;
            _swords = swords;
            _status = status;
            _time = time;
        }

        private void OnEnable()
        {
            _presentation = GetComponent<Presentation>();
            foreach (var opener in _openers)
                opener.Tapped += Open;
            _cancel.Tapped += Close;
        }

        private void OnDisable()
        {
            foreach (var opener in _openers)
                opener.Tapped -= Open;
            _cancel.Tapped -= Close;
        }

        private void Open()
        {
            if (_presentation.IsPresented) return;
            _elapsed = 0f;
            _found = false;
            _status.Text.Value = "Looking for an opponent…";
            _time.Text.Value = "0:00";
            _cancel.Node.Opacity.Value = 1f;
            _presentation.Present();
        }

        private void Close() => _presentation.Dismiss();

        private void Update()
        {
            if (!_presentation.IsPresented) return;
            float before = _elapsed;
            _elapsed += Time.unscaledDeltaTime;
            if (!_found)
            {
                _swords.Rotation.Value = _elapsed * Turn;
                if (Mathf.FloorToInt(_elapsed) != Mathf.FloorToInt(before))
                    _time.Text.Value = "0:" + Mathf.FloorToInt(_elapsed).ToString("00");
                if (_elapsed >= FindAfter)
                {
                    _found = true;
                    UI.Animate(Change, () =>
                    {
                        _status.Text.Value = "Opponent found!";
                        _swords.Rotation.Value = 0f;
                        _swords.Scale.Value = 1.3f;
                        _cancel.Node.Opacity.Value = 0f;
                    }).Finished += () => UI.Animate(Change, () => _swords.Scale.Value = 1f);
                }
            }
            else if (_elapsed >= FindAfter + CloseAfter)
            {
                Close();
            }
        }
    }
}
