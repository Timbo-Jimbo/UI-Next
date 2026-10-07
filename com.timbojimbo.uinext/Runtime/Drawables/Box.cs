using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// A rounded rect filling its node: a fill (a colour or a gradient), a border inside its edge, and a soft shadow.
    /// Drawn as signed distance fields, so its edges and corners stay crisp at any size and scale, and it follows its
    /// node's drawn size as it springs without drawing again.
    /// </summary>
    [AddComponentMenu("Timbo Jimbo/UI Next/Box")]
    public sealed class Box : UIDrawable
    {
        [SerializeField] private UIValue<Color> _fill = Color.white;
        [SerializeField] private UIValue<CornerRadii> _cornerRadii = new CornerRadii();

        [Tooltip("Above 0, the fill is a gradient from Fill to Gradient To.")]
        [SerializeField] private UIValue<bool> _gradient = false;
        [SerializeField] private UIValue<Color> _gradientTo = Color.black;
        [Tooltip("Degrees: 0 left to right, 90 top to bottom.")]
        [SerializeField] private UIValue<float> _gradientAngle = 90f;

        [SerializeField] private UIValue<float> _borderWidth = 0f;
        [SerializeField] private UIValue<Color> _borderColor = Color.black;

        [SerializeField] private UIValue<Color> _shadowColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private UIValue<float> _shadowBlur = 8f;
        [SerializeField] private UIValue<Vector2> _shadowOffset = new Vector2(0f, 2f);
        [SerializeField] private UIValue<float> _shadowSpread = 0f;

        public UIValue<Color> Fill => _fill;
        public UIValue<CornerRadii> CornerRadii => _cornerRadii;
        public UIValue<bool> Gradient => _gradient;
        public UIValue<Color> GradientTo => _gradientTo;
        public UIValue<float> GradientAngle => _gradientAngle;
        public UIValue<float> BorderWidth => _borderWidth;
        public UIValue<Color> BorderColor => _borderColor;
        public UIValue<Color> ShadowColor => _shadowColor;
        public UIValue<float> ShadowBlur => _shadowBlur;
        public UIValue<Vector2> ShadowOffset => _shadowOffset;
        public UIValue<float> ShadowSpread => _shadowSpread;

        protected internal override void Draw(Painter painter)
        {
            var radii = _cornerRadii.Value;
            var shadow = _shadowColor.Value;
            if (shadow.a > 0f)
                painter.ShadowNode(shadow, radii, _shadowBlur.Value, _shadowOffset.Value, _shadowSpread.Value);
            if (_gradient.Value)
                painter.GradientNode(_fill.Value, _gradientTo.Value, _gradientAngle.Value, radii);
            else
                painter.FillNode(_fill.Value, radii, default, _borderWidth.Value, _borderColor.Value);
            if (_gradient.Value && _borderWidth.Value > 0f)
                painter.FillNode(Color.clear, radii, default, _borderWidth.Value, _borderColor.Value);
        }
    }
}
