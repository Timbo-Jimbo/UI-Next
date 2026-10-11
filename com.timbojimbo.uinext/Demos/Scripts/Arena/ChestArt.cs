using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>
    /// A drawable of the demo's own, standing in for a chest's artwork: a lid and a body in the chest's colours, a band
    /// down the middle, a lock, and a shadow under it, drawn with the painter's rounded rects at any size.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next Demos/Chest Art")]
    public sealed class ChestArt : UIDrawable
    {
        [SerializeField] private UIValue<ChestKind> _kind = ChestKind.Silver;

        public UIValue<ChestKind> Kind => _kind;

        protected override void Draw(Painter painter)
        {
            var size = painter.Size;
            float w = size.x, h = size.y;
            if (w <= 0f || h <= 0f) return;
            var (body, trim) = ArenaData.ChestColours(_kind.Value);
            var lid = Color.Lerp(body, Color.white, 0.25f);
            float edge = Mathf.Max(1.5f, w * 0.035f);

            painter.Rect(new Rect(w * 0.1f, h * 0.86f, w * 0.8f, h * 0.12f), new Color(0f, 0f, 0f, 0.25f), h * 0.06f);
            painter.Rect(new Rect(w * 0.08f, h * 0.42f, w * 0.84f, h * 0.5f), body, w * 0.08f, edge, trim);
            painter.Rect(new Rect(w * 0.05f, h * 0.12f, w * 0.9f, h * 0.36f), lid,
                new CornerRadii(w * 0.24f, w * 0.24f, w * 0.05f, w * 0.05f), edge, trim);
            painter.Rect(new Rect(w * 0.42f, h * 0.12f, w * 0.16f, h * 0.8f), trim, w * 0.03f);
            painter.Rect(new Rect(w * 0.38f, h * 0.38f, w * 0.24f, h * 0.2f), new Color32(0xFF, 0xE0, 0x7A, 0xFF), w * 0.05f, edge * 0.8f, trim);
        }
    }
}
