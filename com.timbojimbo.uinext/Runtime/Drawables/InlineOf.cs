using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Marks a node a <see cref="Label"/> made from an inline handler's prefab (an emote, a mention chip), so the label
    /// knows its own among its children, and finds those a script reload left behind.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class InlineOf : MonoBehaviour
    {
        internal Label Label;
    }
}
