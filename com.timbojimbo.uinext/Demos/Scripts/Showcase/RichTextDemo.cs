using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>Rich text: says what was tapped in a label read with handlers of its own, a mention or a tag.</summary>
    public sealed class RichTextDemo : MonoBehaviour
    {
        [SerializeField] private Label _text;
        [SerializeField] private Label _readout;

        public void Setup(Label text, Label readout)
        {
            _text = text;
            _readout = readout;
        }

        private void OnEnable() => _text.MatchTapped += Tapped;

        private void OnDisable() => _text.MatchTapped -= Tapped;

        // What was tapped, as it is written in the text.
        private void Tapped(TextMatch match)
        {
            var source = _text.Document.Text;
            _readout.Text.Value = "Tapped " + source.Substring(match.ContentStart, match.ContentLength);
        }
    }
}
