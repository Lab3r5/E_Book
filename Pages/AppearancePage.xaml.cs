using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class AppearancePage : ContentPage
    {
        private const string ThemeKey = "app_theme";

        public AppearancePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Keep Screen On（你之前在SettingPage里用了DeviceDisplay.KeepScreenOn）
            KeepScreenOnSwitch.IsToggled = DeviceDisplay.KeepScreenOn;

            // Theme（简单存Preferences）
            var theme = Preferences.Get(ThemeKey, "Light");
            Application.Current!.UserAppTheme = theme == "Dark" ? AppTheme.Dark : AppTheme.Light;
        }

        private void OnLightClicked(object sender, EventArgs e)
        {
            Preferences.Set(ThemeKey, "Light");
            Application.Current!.UserAppTheme = AppTheme.Light;
        }

        private void OnDarkClicked(object sender, EventArgs e)
        {
            Preferences.Set(ThemeKey, "Dark");
            Application.Current!.UserAppTheme = AppTheme.Dark;
        }

        private void OnKeepScreenOnToggled(object sender, ToggledEventArgs e)
        {
            DeviceDisplay.KeepScreenOn = e.Value;
        }
    }
}