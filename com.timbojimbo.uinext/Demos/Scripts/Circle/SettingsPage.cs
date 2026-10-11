using UnityEngine;

namespace TimboJimbo.UINext.Demos.Circle
{
    /// <summary>
    /// The settings page, pushed over Me from its row, whose title rises out of the row's (<see cref="PresentFrom"/>): what
    /// its settings do. The theme puts the app in Dark mode or takes it out, quiet hours show when they are as they are
    /// turned on, and Clear cache empties the storage bar.
    /// </summary>
    [RequireComponent(typeof(Presentation))]
    public sealed class SettingsPage : MonoBehaviour
    {
        [SerializeField] private CircleApp _app;
        [SerializeField] private SingleChoice _theme;
        [SerializeField] private ToggleSwitch _quietHours;
        [SerializeField] private UINode _quietTimes;
        [SerializeField] private Pressable _clearCache;
        [SerializeField] private UINode _used;
        [SerializeField] private Label _usedLabel;

        /// <summary>
        /// The app; the theme's control, quiet hours' switch and the row it shows; Clear cache, the storage bar's fill and
        /// the label saying how much is used.
        /// </summary>
        public void Setup(CircleApp app, SingleChoice theme, ToggleSwitch quietHours, UINode quietTimes, Pressable clearCache, UINode used,
            Label usedLabel)
        {
            _app = app;
            _theme = theme;
            _quietHours = quietHours;
            _quietTimes = quietTimes;
            _clearCache = clearCache;
            _used = used;
            _usedLabel = usedLabel;
        }

        private void OnEnable()
        {
            _theme.Changed += OnTheme;
            _quietHours.Changed += OnQuietHours;
            _clearCache.Tapped += ClearCache;
        }

        private void OnDisable()
        {
            _theme.Changed -= OnTheme;
            _quietHours.Changed -= OnQuietHours;
            _clearCache.Tapped -= ClearCache;
        }

        private void OnTheme(int theme) => _app.SetDark(theme == 1);

        private void OnQuietHours(bool on) =>
            UI.Animate(UIAnimation.Smooth, () => _quietTimes.Display.Value = on ? DisplayMode.Visible : DisplayMode.None);

        private void ClearCache() => UI.Animate(UIAnimation.Smooth, () =>
        {
            _used.Width.Value = Sizing.Percent(0.02f);
            _usedLabel.Text.Value = "0.1 GB of 5 GB";
        });
    }
}
