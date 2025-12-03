using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using EZ_Read.Data;

namespace EZ_Read.Pages
{
    public partial class SettingPage : ContentPage
    {
        private readonly Database dbHelper = new();

        public SettingPage()
        {
            InitializeComponent();
        }

        // Load user settings when the page appears
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Add a small delay to prevent UI flickering
            await Task.Delay(50);

            var settings = await dbHelper.GetUserSettingsAsync();
            StartupPasswordSwitch.IsToggled = settings.StartupPasswordEnabled;
            ExitLockSwitch.IsToggled = settings.ExitLockEnabled;
            KeepScreenOnSwitch.IsToggled = settings.KeepScreenOn;
        }
        //

        // Toggle startup password switch
        private async void OnStartupPasswordToggled(object sender, ToggledEventArgs e)
        {
            var storedPassword = await dbHelper.GetPasswordAsync();

            if (e.Value && string.IsNullOrEmpty(storedPassword))
            {
                await DisplayAlert("Warning", "No password set. Cannot enable startup password!", "OK");
                StartupPasswordSwitch.IsToggled = false;
                return;
            }

            var settings = await dbHelper.GetUserSettingsAsync();

            // Prevent unnecessary database writes if the value hasn't changed
            if (settings.StartupPasswordEnabled == e.Value) return;

            await Task.Delay(50); // Smooth UI update
            await dbHelper.SaveSettingsAsync(settings.KeepScreenOn, e.Value, settings.ExitLockEnabled);
        }
        //

        // Toggle exit lock switch
        private async void OnExitLockToggled(object sender, ToggledEventArgs e)
        {
            var settings = await dbHelper.GetUserSettingsAsync();

            if (e.Value && !settings.StartupPasswordEnabled)
            {
                await DisplayAlert("Warning", "Startup password must be enabled first!", "OK");
                ExitLockSwitch.IsToggled = false;
                return;
            }

            // Prevent unnecessary database writes if the value hasn't changed
            if (settings.ExitLockEnabled == e.Value) return;

            await dbHelper.SaveSettingsAsync(settings.KeepScreenOn, settings.StartupPasswordEnabled, e.Value);
        }
        //

        // Toggle screen keep-on switch
        private async void OnKeepScreenOnToggled(object sender, ToggledEventArgs e)
        {
            bool isEnabled = e.Value;

            var settings = await dbHelper.GetUserSettingsAsync();

            // Prevent unnecessary database writes if the value hasn't changed
            if (settings.KeepScreenOn == isEnabled) return;

            DeviceDisplay.KeepScreenOn = isEnabled;
            await dbHelper.SaveSettingsAsync(isEnabled, StartupPasswordSwitch.IsToggled, ExitLockSwitch.IsToggled);
        }
        //

        // Navigate to PasswordPage
        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new PasswordPage());
        }
        //

        // Navigate back
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
        //

        // Bottom navigation bar
        private async void OnBookshelfClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync(); // Return to Homepage
        }
        //

    }
}
