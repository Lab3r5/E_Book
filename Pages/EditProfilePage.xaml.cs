using E_Book.Services;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class EditProfilePage : ContentPage
    {
        private string ProfileKey => $"profile_name_{UserSession.UserId}";

        public EditProfilePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var savedName = Preferences.Get(ProfileKey, UserSession.DisplayName);

            NameEntry.Text = savedName;
            EmailEntry.Text = UserSession.IsGuest ? "" : UserSession.UserId;

            EmailEntry.IsVisible = !UserSession.IsGuest;
            ChangePasswordButton.IsVisible = !UserSession.IsGuest;
            SaveButton.IsVisible = !UserSession.IsGuest;

            if (UserSession.IsGuest)
            {
                NameEntry.IsEnabled = false;
                NameEntry.Text = "Guest";
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            // ✅ Prefer Shell back
            try { await Shell.Current.GoToAsync(".."); return; } catch { }
            await Navigation.PopModalAsync();
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            // ✅ Use Shell route (PasswordPage is a ShellContent route)
            await Shell.Current.GoToAsync("password");
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (UserSession.IsGuest)
                return;

            var name = NameEntry.Text?.Trim() ?? "";
            if (name.Length == 0)
            {
                await DisplayAlert("Error", "Name cannot be empty.", "OK");
                return;
            }

            Preferences.Set(ProfileKey, name);

            // ✅ Only update display name, don't flip guest flag
            UserSession.UpdateDisplayName(name);

            await DisplayAlert("Saved", "Profile updated.", "OK");

            try { await Shell.Current.GoToAsync(".."); return; } catch { }
            await Navigation.PopModalAsync();
        }
    }
}