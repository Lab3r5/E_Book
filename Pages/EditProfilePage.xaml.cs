using E_Book.Services;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class EditProfilePage : ContentPage
    {
        private string ProfileKey => $"profile_name_{UserSession.UserId}";
        private string _originalName = string.Empty;

        public EditProfilePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            _originalName = Preferences.Get(ProfileKey, UserSession.DisplayName)?.Trim() ?? string.Empty;

            var currentName = UserSession.IsGuest ? "Guest" : _originalName;
            NameEntry.Text = currentName;
            EmailLabel.Text = UserSession.IsGuest ? "Guest account" : UserSession.UserId;

            AccountTypeLabel.Text = UserSession.IsGuest ? "Guest account" : "Registered account";
            AvatarLetter.Text = GetAvatarLetter(currentName);

            if (UserSession.IsGuest)
            {
                NameEntry.IsEnabled = false;
                GuestNotice.IsVisible = true;

                SaveLabel.Opacity = 0.35;
                ChangePasswordLabel.Opacity = 0.35;
            }
            else
            {
                NameEntry.IsEnabled = true;
                GuestNotice.IsVisible = false;

                SaveLabel.Opacity = 0.35;
                ChangePasswordLabel.Opacity = 1.0;
            }
            UpdateQuickLoginUI();
        }

        private string GetAvatarLetter(string? name)
        {
            var text = (name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text))
                return "U";

            return text.Substring(0, 1).ToUpperInvariant();
        }

        private void UpdateSaveState()
        {
            if (UserSession.IsGuest)
            {
                SaveLabel.Opacity = 0.35;
                return;
            }

            var currentName = NameEntry.Text?.Trim() ?? string.Empty;
            bool changed = !string.Equals(currentName, _originalName, StringComparison.Ordinal);

            SaveLabel.Opacity = changed ? 1.0 : 0.35;
            AvatarLetter.Text = GetAvatarLetter(currentName);
        }

        private async Task PressAnim(VisualElement view)
        {
            if (view == null) return;

            await view.ScaleTo(0.96, 80, Easing.CubicOut);
            await view.ScaleTo(1.0, 120, Easing.CubicOut);
        }

        private async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            catch
            {
            }

            try
            {
                await Navigation.PopAsync();
                return;
            }
            catch
            {
            }

            await Navigation.PopModalAsync();
        }

        private void OnNameChanged(object sender, TextChangedEventArgs e)
        {
            if (UserSession.IsGuest) return;
            UpdateSaveState();
        }

        private async void OnCancelTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);
            await GoBackAsync();
        }

        private async void OnSaveTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            if (UserSession.IsGuest) return;

            var name = NameEntry.Text?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                await DisplayAlert("Error", "Name cannot be empty.", "OK");
                return;
            }

            if (string.Equals(name, _originalName, StringComparison.Ordinal))
                return;

            Preferences.Set(ProfileKey, name);
            UserSession.UpdateDisplayName(name);
            _originalName = name;

            UpdateSaveState();

            await DisplayAlert("Saved", "Profile updated.", "OK");
            await GoBackAsync();
        }

        private async void OnChangePasswordTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            if (UserSession.IsGuest) return;

            await Shell.Current.GoToAsync("password");
        }

        private async void OnLogoutTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            bool ok = await DisplayAlert("Log Out", "Are you sure you want to log out?", "Log Out", "Cancel");
            if (!ok) return;
            if (UserSession.CanQuickLogin)
            {
                UserSession.LogoutKeepIdentity();
            }
            else
            {
                UserSession.Logout();
            }
            await Shell.Current.GoToAsync("//login");
        }
        private async void OnQuickLoginTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            if (UserSession.IsGuest)
            {
                await DisplayAlert("Unavailable", "Quick Login is only available for registered users.", "OK");
                return;
            }

            bool proceed = await DisplayAlert(
                "Quick Login",
                "Enabling this feature allows this device to enter your account without login verification for the next 1 to 5 app launches after logout. Only use this on your personal device.",
                "Continue",
                "Cancel");

            if (!proceed)
                return;

            string action = await DisplayActionSheet(
                "Choose how many times to skip login",
                "Cancel",
                null,
                "1 time",
                "2 times",
                "3 times",
                "4 times",
                "5 times",
                "Turn Off");

            if (action == "Cancel")
                return;

            if (action == "Turn Off")
            {
                UserSession.ClearQuickLoginCount();
                UpdateQuickLoginUI();
                await DisplayAlert("Updated", "Quick Login has been turned off.", "OK");
                return;
            }

            int count = action switch
            {
                "1 time" => 1,
                "2 times" => 2,
                "3 times" => 3,
                "4 times" => 4,
                "5 times" => 5,
                _ => 0
            };

            if (count >= 1 && count <= 5)
            {
                UserSession.SetQuickLoginCount(count);
                UpdateQuickLoginUI();
                await DisplayAlert("Enabled", $"Quick Login is enabled for the next {count} app launch(es).", "OK");
            }
        }
        private void UpdateQuickLoginUI()
        {
            if (UserSession.IsGuest)
            {
                QuickLoginValueLabel.Text = "Unavailable";
                QuickLoginValueLabel.Opacity = 0.35;
                return;
            }

            int remaining = UserSession.QuickLoginRemaining;

            if (remaining <= 0)
            {
                QuickLoginValueLabel.Text = "Off";
                QuickLoginValueLabel.Opacity = 0.6;
            }
            else
            {
                QuickLoginValueLabel.Text = $"{remaining} left";
                QuickLoginValueLabel.Opacity = 1.0;
            }
        }
    }
}