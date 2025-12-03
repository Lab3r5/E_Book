using EZ_Read.Data;

namespace EZ_Read
{
    public partial class MainPage : ContentPage
    {
        private readonly Database dbHelper = new();

        public MainPage()
        {
            InitializeComponent();
            CheckIfPasswordRequired();
        }

        // Check if the startup password is enabled. If not, jump directly.
        private async void CheckIfPasswordRequired()
        {
            var settings = await dbHelper.GetUserSettingsAsync();
            var savedPassword = await dbHelper.GetPasswordAsync();

            bool needPassword = settings.StartupPasswordEnabled && !string.IsNullOrEmpty(savedPassword);

            if (!needPassword)
            {
                // No need to enter a password, jump directly to the home page.
                await Navigation.PushAsync(new Pages.Homepage());
            }
            else
            {
                // Otherwise, display the password input interface.
                PasswordEntry.IsVisible = true;
                EnterButton.IsVisible = true;
            }
        }
        //

        private async void OnEnterClicked(object sender, EventArgs e)
        {
            string inputPassword = PasswordEntry.Text?.Trim() ?? string.Empty;
            string? savedPassword = await dbHelper.GetPasswordAsync();

            if (inputPassword == savedPassword)
            {
                await Navigation.PushAsync(new Pages.Homepage());
            }
            else
            {
                await DisplayAlert("Error", "Wrong password, please re-enter the password", "Confirm");
                PasswordEntry.Text = string.Empty;
            }
        }
    }
}
