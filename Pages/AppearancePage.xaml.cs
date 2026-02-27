using Microsoft.Maui.Storage;
using E_Book.Data;

namespace E_Book.Pages
{
    public partial class AppearancePage : ContentPage
    {
        private readonly Database db = new();

        public AppearancePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1) Keep Screen On：从DB读
            var user = await db.GetUserSettingsAsync();
            DeviceDisplay.KeepScreenOn = user.KeepScreenOn;
            KeepScreenOnSwitch.IsToggled = user.KeepScreenOn;

            // 2) Theme：从DB读（ReadingSettings.BackgroundColor 存 Light/Dark）
            var reading = await db.GetReadingSettingsAsync();
            var theme = NormalizeTheme(reading.BackgroundColor);

            Application.Current!.UserAppTheme = theme == "Dark" ? AppTheme.Dark : AppTheme.Light;
        }

        private static string NormalizeTheme(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return "Light";
            stored = stored.Trim();

            // 兼容旧值：如果是颜色值（#xxxxxx）就当 Light
            if (stored.StartsWith("#")) return "Light";

            if (stored.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
            return "Light";
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            // 兼容 Shell Modal / Navigation Modal
            try { await Shell.Current.GoToAsync(".."); return; } catch { }
            await Navigation.PopModalAsync();
        }

        private async void OnLightClicked(object sender, EventArgs e)
        {
            Application.Current!.UserAppTheme = AppTheme.Light;

            var r = await db.GetReadingSettingsAsync();
            await db.SaveReadingSettingsAsync(r.FontSize, "Light");
        }

        private async void OnDarkClicked(object sender, EventArgs e)
        {
            Application.Current!.UserAppTheme = AppTheme.Dark;

            var r = await db.GetReadingSettingsAsync();
            await db.SaveReadingSettingsAsync(r.FontSize, "Dark");
        }

        private async void OnKeepScreenOnToggled(object sender, ToggledEventArgs e)
        {
            DeviceDisplay.KeepScreenOn = e.Value;

            var s = await db.GetUserSettingsAsync();
            await db.SaveSettingsAsync(
                keepScreenOn: e.Value,
                startupPassword: s.StartupPasswordEnabled,
                exitLock: s.ExitLockEnabled
            );
        }
    }
}