using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Text its trigger finds becomes a node made from <see cref="Prefab"/>, across the label's width between the lines
    /// above and below it: a code block, a quote, a list item, a callout. The node is as wide as the label's text and as
    /// tall as its own layout makes it there, and its components implementing <see cref="ITextContent"/> are told the
    /// match (a <see cref="TextSlot"/> on a label in it shows its text, Markdown and all).
    /// </summary>
    [CreateAssetMenu(menuName = "Timbo Jimbo/UI Next/Text/Block Handler", fileName = "Block Handler")]
    public class BlockHandler : TextRule
    {
        [Tooltip("A prefab with a UINode at its root, made into a node across the label's width for each match, as tall as its own layout makes it.")]
        [SerializeField] private GameObject _prefab;

        public override TextRole Role => TextRole.Block;

        /// <summary>The prefab made into a node for each match.</summary>
        public GameObject Prefab => _prefab;

        /// <summary>The prefab a match is made from: <see cref="Prefab"/>, unless a handler of your own picks one per match.</summary>
        public virtual GameObject PrefabFor(in TextMatch match) => _prefab;

        /// <summary>A block handler made in code, as the built-in Markdown's are.</summary>
        public static BlockHandler Create(TextTrigger trigger, GameObject prefab)
        {
            var handler = CreateInstance<BlockHandler>();
            handler.Configure(trigger);
            handler._prefab = prefab;
            return handler;
        }

        // A new handler takes fenced blocks.
        private void Reset() => Configure(TextTrigger.Fenced());
    }
}
