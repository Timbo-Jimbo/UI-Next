using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text its trigger finds becomes a node made from <see cref="Prefab"/>, sitting in its line as a big character
    /// would and wrapping with it: a mention chip, a tag pill, an image. The node is as big as its own layout makes it,
    /// and its components implementing <see cref="ITextContent"/> are told the match (a <see cref="TextSlot"/> on a label
    /// in it shows its text). With no prefab it is a chip: a pill with the match as written. A tappable one raises the
    /// label's <see cref="Label.MatchTapped"/> when it is tapped, and is Pressed while it is held.
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Text/Element Handler", fileName = "Element Handler")]
    public class ElementHandler : TextRule
    {
        [Tooltip("A prefab with a UINode at its root, made into a node in the line for each match, as big as its own layout makes it. Empty for a chip: a pill with the match as written.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("How it sits in its line: its middle on the middle of the text, or its bottom on the baseline.")]
        [SerializeField] private ElementAlignment _alignment = ElementAlignment.Center;

        [Tooltip("A tap on it raises the label's Match Tapped, and it is Pressed while it is held.")]
        [SerializeField] private bool _tappable;

        public override TextRole Role => TextRole.Element;
        public override ElementAlignment Alignment => _alignment;
        public override bool Tappable => _tappable;

        /// <summary>The prefab made into a node for each match; none for a chip.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>
        /// The prefab a match is made from: <see cref="Prefab"/>, or with none the built-in chip, unless a handler of your
        /// own picks one per match.
        /// </summary>
        public virtual GameObject PrefabFor(in TextMatch match) => _prefab != null ? _prefab : BuiltInText.Chip;

        /// <summary>
        /// A tap on a match, when it is tappable: raises the label's <see cref="Label.MatchTapped"/>. Override to handle it
        /// in the handler.
        /// </summary>
        public virtual void Tapped(in TextMatch match, Label label) => label.RaiseTapped(in match);

        /// <summary>An element handler made in code; a null prefab makes chips.</summary>
        public static ElementHandler Create(TextTrigger trigger, GameObject prefab = null, ElementAlignment alignment = ElementAlignment.Center,
            bool tappable = false)
        {
            var handler = CreateInstance<ElementHandler>();
            handler.Configure(trigger, prefab, alignment, tappable);
            return handler;
        }

        /// <summary>Sets up a handler made in code, before any text is read with it.</summary>
        protected void Configure(TextTrigger trigger, GameObject prefab, ElementAlignment alignment, bool tappable)
        {
            Configure(trigger);
            _prefab = prefab;
            _alignment = alignment;
            _tappable = tappable;
        }

        // A new handler makes @mentions chips.
        protected virtual void Reset() => Configure(TextTrigger.Prefix("@"));
    }
}
