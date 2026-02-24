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

            // Load saved name (per user)
            var savedName = Preferences.Get(ProfileKey, UserSession.DisplayName);

            NameEntry.Text = savedName;
            EmailEntry.Text = UserSession.IsGuest ? "" : UserSession.UserId;

            // Guest: hide account-related UI
            EmailEntry.IsVisible = !UserSession.IsGuest;
            ChangePasswordButton.IsVisible = !UserSession.IsGuest;
            SaveButton.IsVisible = !UserSession.IsGuest;

            // Guest: lock editing
            if (UserSession.IsGuest)
            {
                NameEntry.IsEnabled = false;
                NameEntry.Text = "Guest";
            }
        }

        // Close this modal page
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            // Push into the modal NavigationPage stack
            await Navigation.PushAsync(new PasswordPage());
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var name = NameEntry.Text?.Trim() ?? "";
            if (name.Length == 0)
            {
                await DisplayAlert("Error", "Name cannot be empty.", "OK");
                return;
            }

            Preferences.Set(ProfileKey, name);

            // Sync display name in session
            UserSession.SetUser(UserSession.UserId, name);

            await DisplayAlert("Saved", "Profile updated.", "OK");

            // Close this modal page
            await Navigation.PopModalAsync();
        }
    }
}