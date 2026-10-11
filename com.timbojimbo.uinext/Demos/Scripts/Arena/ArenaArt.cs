using UnityEngine;

namespace TimboJimbo.UINext.Demos.Arena
{
    /// <summary>
    /// A drawable of the demo's own, standing in for the arena's artwork: a grass field with two lanes, a river with a
    /// bridge in each lane, and three towers at each end, red at the top and blue at the bottom.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next Demos/Arena Art")]
    public sealed class ArenaArt : UIDrawable
    {
        [SerializeField] private UIValue<float> _cornerRadius = 18f;

        public UIValue<float> CornerRadius => _cornerRadius;

        private static readonly Color Lane = new(1f, 1f, 1f, 0.12f);
        private static readonly Color Bridge = new Color32(0xB0, 0x7A, 0x45, 0xFF);
        private static readonly Color Stone = new Color32(0xC9, 0xC2, 0xB6, 0xFF);
        private static readonly Color StoneEdge = new Color32(0x6E, 0x66, 0x5A, 0xFF);
        private static readonly Color Red = new Color32(0xE8, 0x4A, 0x4A, 0xFF);
        private static readonly Color Blue = new Color32(0x3E, 0x8B, 0xF0, 0xFF);

        protected override void Draw(Painter painter)
        {
            var size = painter.Size;
            float w = size.x, h = size.y;
            if (w <= 0f || h <= 0f) return;
            painter.GradientNode(new Color32(0x8F, 0xD6, 0x77, 0xFF), new Color32(0x4F, 0x9E, 0x4B, 0xFF), 90f, _cornerRadius.Value);

            // The two lanes, the river across the middle, and a bridge in each lane.
            foreach (float x in new[] { 0.2f, 0.8f })
                painter.Rect(new Rect(w * (x - 0.07f), h * 0.04f, w * 0.14f, h * 0.92f), Lane, w * 0.03f);
            painter.Rect(new Rect(0f, h * 0.45f, w, h * 0.1f), new Color32(0x5C, 0xC6, 0xF0, 0xFF), 0f);
            painter.Rect(new Rect(0f, h * 0.465f, w, h * 0.02f), new Color(1f, 1f, 1f, 0.35f), 0f);
            foreach (float x in new[] { 0.2f, 0.8f })
                painter.Rect(new Rect(w * (x - 0.08f), h * 0.43f, w * 0.16f, h * 0.14f), Bridge, w * 0.02f, 2f, StoneEdge);

            // The towers: a princess tower in each lane and the king's between, at each end.
            Tower(painter, w * 0.2f, h * 0.2f, w * 0.15f, Red);
            Tower(painter, w * 0.8f, h * 0.2f, w * 0.15f, Red);
            Tower(painter, w * 0.5f, h * 0.09f, w * 0.2f, Red);
            Tower(painter, w * 0.2f, h * 0.8f, w * 0.15f, Blue);
            Tower(painter, w * 0.8f, h * 0.8f, w * 0.15f, Blue);
            Tower(painter, w * 0.5f, h * 0.91f, w * 0.2f, Blue);
        }

        // A tower centred on (x, y), `side` wide: a stone base with a coloured roof and a shadow.
        private static void Tower(Painter painter, float x, float y, float side, Color roof)
        {
            float half = side * 0.5f;
            painter.Rect(new Rect(x - half, y - half + side * 0.12f, side, side), new Color(0f, 0f, 0f, 0.2f), side * 0.2f);
            painter.Rect(new Rect(x - half, y - half, side, side), Stone, side * 0.18f, 2f, StoneEdge);
            float inner = side * 0.56f;
            painter.Rect(new Rect(x - inner * 0.5f, y - inner * 0.5f, inner, inner), roof, inner * 0.25f, 2f, Color.Lerp(roof, Color.black, 0.4f));
        }
    }
}
