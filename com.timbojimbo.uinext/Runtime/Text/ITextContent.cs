using TimboJimbo.UINext.Text;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Told what a node made from a handler's prefab stands for: the match (its text and argument, see
    /// <see cref="TextMatch"/>) and the label it is in, each time the label reads its text again. Any component of the
    /// prefab may be one, at its root or deeper down (a <see cref="TextSlot"/> on a label inside it), but not one inside
    /// the inline content of another label within it, which that label tells.
    /// </summary>
    public interface ITextContent
    {
        void Bind(in TextMatch match, Label label);
    }
}
