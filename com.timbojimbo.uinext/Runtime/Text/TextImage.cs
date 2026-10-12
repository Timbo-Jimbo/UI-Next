using TimboJimbo.UINext.Text;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Shows, in its node's image, the image an <see cref="ImageHandler"/> has for the match its prefab stands for,
    /// its node as big as the handler says, in em of the text around it, following the label's size. No code needed.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Timbo Jimbo/UI Next/Text Image")]
    public sealed class TextImage : MonoBehaviour, ITextContent
    {
        public void Bind(in TextMatch match, Label label)
        {
            var images = match.Rule as ImageHandler;
            GetComponent<Image>().Sprite.Value = images != null ? images.Find(match.Text) : null;
            if (images == null || label == null || !TryGetComponent(out UINode node)) return;
            var around = match;
            float em = images.SizeEm;
            node.Width.Bind(() => Sizing.Fixed(label.FontSizeAround(in around) * em));
            node.Height.Bind(() => Sizing.Fixed(label.FontSizeAround(in around) * em));
        }
    }
}
