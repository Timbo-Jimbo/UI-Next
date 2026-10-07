using UnityEngine;

namespace TimboJimbo.UINext.Demos.Showcase
{
    /// <summary>
    /// Bindings: a model value (an Observable) and the UI bound to it. Labels, a bar's width and a badge's colour are
    /// expressions over it; each runs again only when what it read changes, however the value is changed. The
    /// buttons change the model inside UI.Animate, so everything bound to it springs.
    /// </summary>
    public sealed class BindingsDemo : MonoBehaviour
    {
        [SerializeField] private Pressable _minus;
        [SerializeField] private Pressable _plus;
        [SerializeField] private Label _count;
        [SerializeField] private Label _level;
        [SerializeField] private UINode _bar;
        [SerializeField] private Box _badge;

        private const int Max = 10;
        private readonly Observable<int> _value = new(3);

        public void Setup(Pressable minus, Pressable plus, Label count, Label level, UINode bar, Box badge)
        {
            _minus = minus;
            _plus = plus;
            _count = count;
            _level = level;
            _bar = bar;
            _badge = badge;
        }

        private void OnEnable()
        {
            _count.Text.Bind(() => _value.Value.ToString());
            _level.Text.Bind(() => _value.Value switch
            {
                0 => "Empty",
                < 4 => "Low",
                < 8 => "Good",
                < Max => "High",
                _ => "**Full**",
            });
            _bar.Width.Bind(() => Sizing.Percent(_value.Value / (float)Max));
            _badge.Fill.Bind(() => Color.Lerp(new Color(1f, 0.42f, 0.42f), new Color(0.2f, 0.78f, 0.35f), _value.Value / (float)Max));
            _minus.Interactable.Bind(() => _value.Value > 0);
            _plus.Interactable.Bind(() => _value.Value < Max);
            _minus.Tapped += () => Change(-1);
            _plus.Tapped += () => Change(1);
        }

        private void OnDisable()
        {
            _count.Text.Unbind();
            _level.Text.Unbind();
            _bar.Width.Unbind();
            _badge.Fill.Unbind();
            _minus.Interactable.Unbind();
            _plus.Interactable.Unbind();
        }

        private void Change(int by) => UI.Animate(UIAnimation.Snappy, () => _value.Value = Mathf.Clamp(_value.Peek + by, 0, Max));
    }
}
