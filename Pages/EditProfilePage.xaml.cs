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

            UserSession.Logout();
            await Shell.Current.GoToAsync("//login");
        }
    }
}