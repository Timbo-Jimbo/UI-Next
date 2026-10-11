using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// A rounded rect filling its node: a fill, a border inside its edge, and a soft shadow, each a colour or a gradient.
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

        [Tooltip("The border is a gradient from Border Color to Border Color To: a bevel, light above and dark below.")]
        [SerializeField] private UIValue<bool> _borderGradient = false;
        [SerializeField] private UIValue<Color> _borderColorTo = Color.black;
        [Tooltip("Degrees: 0 left to right, 90 top to bottom.")]
        [SerializeField] private UIValue<float> _borderGradientAngle = 90f;

        [SerializeField] private UIValue<Color> _shadowColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private UIValue<float> _shadowBlur = 8f;
        [SerializeField] private UIValue<Vector2> _shadowOffset = new Vector2(0f, 2f);
        [SerializeField] private UIValue<float> _shadowSpread = 0f;

        [Tooltip("The shadow is a gradient from Shadow Color to Shadow Color To: a glow that fades or shifts in colour.")]
        [SerializeField] private UIValue<bool> _shadowGradient = false;
        [SerializeField] private UIValue<Color> _shadowColorTo = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Degrees: 0 left to right, 90 top to bottom.")]
        [SerializeField] private UIValue<float> _shadowGradientAngle = 90f;

        public UIValue<Color> Fill => _fill;
        public UIValue<CornerRadii> CornerRadii => _cornerRadii;
        public UIValue<bool> Gradient => _gradient;
        public UIValue<Color> GradientTo => _gradientTo;
        public UIValue<float> GradientAngle => _gradientAngle;
        public UIValue<float> BorderWidth => _borderWidth;
        public UIValue<Color> BorderColor => _borderColor;

        /// <summary>
        /// Whether the border is a gradient from <see cref="BorderColor"/> to <see cref="BorderColorTo"/>, at
        /// <see cref="BorderGradientAngle"/> (with the fill a gradient or not): a game button's bevel, light above, dark
        /// below.
        /// </summary>
        public UIValue<bool> BorderGradient => _borderGradient;

        public UIValue<Color> BorderColorTo => _borderColorTo;

        /// <summary>The border gradient's angle in degrees: 0 left to right, 90 top to bottom.</summary>
        public UIValue<float> BorderGradientAngle => _borderGradientAngle;

        public UIValue<Color> ShadowColor => _shadowColor;
        public UIValue<float> ShadowBlur => _shadowBlur;
        public UIValue<Vector2> ShadowOffset => _shadowOffset;
        public UIValue<float> ShadowSpread => _shadowSpread;

        /// <summary>
        /// Whether the shadow is a gradient from <see cref="ShadowColor"/> to <see cref="ShadowColorTo"/>, across the
        /// shadow at <see cref="ShadowGradientAngle"/>: a glow fading out, or shifting colour.
        /// </summary>
        public UIValue<bool> ShadowGradient => _shadowGradient;

        public UIValue<Color> ShadowColorTo => _shadowColorTo;

        /// <summary>The shadow gradient's angle in degrees: 0 left to right, 90 top to bottom.</summary>
        public UIValue<float> ShadowGradientAngle => _shadowGradientAngle;

        protected internal override void Draw(Painter painter)
        {
            var radii = _cornerRadii.Value;
            var shadow = _shadowColor.Value;
            bool shadowGradient = _shadowGradient.Value;
            var shadowTo = shadowGradient ? _shadowColorTo.Value : shadow;
            if (shadow.a > 0f || shadowTo.a > 0f)
                painter.ShadowNode(shadow, radii, _shadowBlur.Value, _shadowOffset.Value, _shadowSpread.Value,
                    shadowGradient ? shadowTo : null, shadowGradient ? _shadowGradientAngle.Value : 90f);
            // A solid fill takes a solid border with it; a gradient fill or border has the border drawn over it on its own.
            bool gradient = _gradient.Value, borderGradient = _borderGradient.Value;
            float border = _borderWidth.Value;
            bool separate = gradient || borderGradient;
            if (gradient)
                painter.GradientNode(_fill.Value, _gradientTo.Value, _gradientAngle.Value, radii);
            else
                painter.FillNode(_fill.Value, radii, default, separate ? 0f : border, _borderColor.Value);
            if (separate && border > 0f)
                painter.BorderNode(_borderColor.Value, border, radii, borderGradient ? _borderColorTo.Value : null,
                    borderGradient ? _borderGradientAngle.Value : 90f);
        }
    }
}
