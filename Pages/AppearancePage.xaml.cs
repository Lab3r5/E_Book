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

            // Sync Keep Screen On state
            KeepScreenOnSwitch.IsToggled = DeviceDisplay.KeepScreenOn;

            // Sync Theme state (simple preference)
            var theme = Preferences.Get(ThemeKey, "Light");
            Application.Current!.UserAppTheme = theme == "Dark" ? AppTheme.Dark : AppTheme.Light;
        }

        // Close this modal page
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
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