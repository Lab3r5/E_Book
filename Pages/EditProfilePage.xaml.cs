using E_Book.Services;
using E_Book.Data;

namespace E_Book.Pages
{
    public partial class EditProfilePage : ContentPage
    {
        private readonly Database _database = new();
        private string _originalName = string.Empty;
        private bool _isSaving;
        private bool _hasChanges;

        public EditProfilePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (UserSession.IsGuest)
            {
                _originalName = "Guest";
            }
            else
            {
                var user = await _database.GetUserAsync(UserSession.UserId);
                _originalName = user?.DisplayName?.Trim() ?? UserSession.DisplayName;
            }

            var currentName = UserSession.IsGuest ? "Guest" : _originalName;

            NameEntry.Text = currentName;
            EmailLabel.Text = UserSession.IsGuest ? "Guest account" : UserSession.UserId;

            AccountTypeLabel.Text = UserSession.IsGuest ? "Guest account" : "Registered account";
            AvatarLetter.Text = GetAvatarLetter(currentName);

            ApplyGuestModeUI();
            UpdateQuickLoginUI();
            UpdateSaveState();
        }

        private void ApplyGuestModeUI()
        {
            if (UserSession.IsGuest)
            {
                NameEntry.IsEnabled = false;
                GuestNotice.IsVisible = true;

                SaveLabel.Opacity = 0.35;
                ChangePasswordLabel.Opacity = 0.35;
                QuickLoginValueLabel.Opacity = 0.35;
            }
            else
            {
                NameEntry.IsEnabled = true;
                GuestNotice.IsVisible = false;

                ChangePasswordLabel.Opacity = 1.0;
            }
        }

        private static string GetAvatarLetter(string? name)
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
                SaveLabel.TextColor = Color.FromArgb("#C9BEF7");
                SaveLabel.Text = "Save";
                _hasChanges = false;
                return;
            }

            var currentName = NameEntry.Text?.Trim() ?? string.Empty;
            _hasChanges = !string.Equals(currentName, _originalName, StringComparison.Ordinal);

            AvatarLetter.Text = GetAvatarLetter(currentName);

            if (_isSaving)
            {
                SaveLabel.Text = "Saving...";
                SaveLabel.Opacity = 1.0;
                SaveLabel.TextColor = Color.FromArgb("#7A63FF");
                return;
            }

            SaveLabel.Text = "Save";
            SaveLabel.Opacity = _hasChanges ? 1.0 : 0.35;
            SaveLabel.TextColor = _hasChanges
                ? Color.FromArgb("#7A63FF")
                : Color.FromArgb("#C9BEF7");
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
                QuickLoginValueLabel.Opacity = 0.75;
            }
            else
            {
                QuickLoginValueLabel.Text = $"{remaining} left";
                QuickLoginValueLabel.Opacity = 1.0;
            }
        }

        private static async Task PressAnim(VisualElement view)
        {
            if (view == null)
                return;

            await view.ScaleTo(0.96, 80, Easing.CubicOut);
            await view.ScaleTo(1.0, 120, Easing.CubicOut);
        }

        private async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                if (Navigation?.ModalStack?.Count > 0)
                    await Navigation.PopModalAsync();
            }
        }

        private void OnNameChanged(object sender, TextChangedEventArgs e)
        {
            if (UserSession.IsGuest)
                return;

            UpdateSaveState();
        }

        private async void OnCancelTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

            await GoBackAsync();
        }

        private async void OnSaveTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

            if (UserSession.IsGuest || _isSaving)
                return;

            var name = NameEntry.Text?.Trim() ?? string.Empty;

            if (name.Length == 0)
            {
                await DisplayAlert("Error", "Name cannot be empty.", "OK");
                return;
            }

            if (string.Equals(name, _originalName, StringComparison.Ordinal))
                return;

            try
            {
                _isSaving = true;
                UpdateSaveState();

                await _database.UpdateUserDisplayNameAsync(UserSession.UserId, name);
                UserSession.UpdateDisplayName(name);

                _originalName = name;
                AvatarLetter.Text = GetAvatarLetter(name);

                _isSaving = false;
                UpdateSaveState();

                await DisplayAlert("Saved", "Profile updated.", "OK");
            }
            catch (Exception ex)
            {
                _isSaving = false;
                UpdateSaveState();
                await DisplayAlert("Error", $"Failed to save profile: {ex.Message}", "OK");
            }
        }

        private async void OnChangePasswordTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

            if (UserSession.IsGuest)
                return;

            await Shell.Current.GoToAsync($"{AppShell.RoutePassword}?email={Uri.EscapeDataString(UserSession.UserId)}");
        }

        private async void OnQuickLoginTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

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

        private async void OnLogoutTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

            bool ok = await DisplayAlert("Log Out", "Are you sure you want to log out?", "Log Out", "Cancel");
            if (!ok)
                return;

            if (UserSession.CanQuickLogin)
                UserSession.LogoutKeepIdentity();
            else
                UserSession.Logout();

            Application.Current!.MainPage = new AppShell();
        }
    }
}