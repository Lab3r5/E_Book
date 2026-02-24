using E_Book.Data;

namespace E_Book.Pages
{
    public partial class PasswordPage : ContentPage
    {
        private readonly Database dbHelper = new();

        public PasswordPage()
        {
            InitializeComponent();
        }

        // Close this page (inside modal NavigationPage stack)
        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(entryPassword.Text) ||
                string.IsNullOrEmpty(entryConfirmPassword.Text))
            {
                await DisplayAlert("Error", "Password cannot be empty.", "OK");
                return;
            }

            if (entryPassword.Text != entryConfirmPassword.Text)
            {
                await DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            await dbHelper.SavePasswordAsync(entryPassword.Text);

            await DisplayAlert("Success", "Password has been set.", "OK");

            // After saving, go back to EditProfilePage
            await Navigation.PopAsync();
        }
    }
}