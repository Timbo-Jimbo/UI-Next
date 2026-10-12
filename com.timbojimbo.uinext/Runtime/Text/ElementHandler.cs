using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text its trigger finds becomes a node made from <see cref="Prefab"/>, sitting in its line as a big character
    /// would and wrapping with it: a mention chip, a tag pill, an image. The node is as big as its own layout makes it,
    /// and its components implementing <see cref="ITextContent"/> are told the match (a <see cref="TextSlot"/> on a label
    /// in it shows its text).
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Text/Element Handler", fileName = "Element Handler")]
    public class ElementHandler : TextRule
    {
        [Tooltip("A prefab with a UINode at its root, made into a node in the line for each match, as big as its own layout makes it.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("How it sits in its line: its middle on the middle of the text, or its bottom on the baseline.")]
        [SerializeField] private ElementAlignment _alignment = ElementAlignment.Center;

        public override TextRole Role => TextRole.Element;
        public override ElementAlignment Alignment => _alignment;

        /// <summary>The prefab made into a node for each match.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>The prefab a match is made from: <see cref="Prefab"/>, unless a handler of your own picks one per match.</summary>
        public virtual GameObject PrefabFor(in TextMatch match) => _prefab;

        /// <summary>An element handler made in code.</summary>
        public static ElementHandler Create(TextTrigger trigger, GameObject prefab, ElementAlignment alignment = ElementAlignment.Center)
        {
            var handler = CreateInstance<ElementHandler>();
            handler.Configure(trigger);
            handler._prefab = prefab;
            handler._alignment = alignment;
            return handler;
        }

        // A new handler looks for @mentions.
        private void Reset() => Configure(TextTrigger.Prefix("@"));
    }
}
