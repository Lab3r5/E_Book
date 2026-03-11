using System.Windows.Input;
using E_Book.Data;

namespace E_Book.Pages
{
    public partial class PasswordPage : ContentPage
    {
        private readonly Database dbHelper = new();

        public ICommand BackCommand { get; }

        public PasswordPage()
        {
            InitializeComponent();

            BackCommand = new Command(async () => await Navigation.PopAsync());
            BindingContext = this;

            UpdateValidationState();
        }

        private void OnTogglePasswordTapped(object sender, TappedEventArgs e)
        {
            entryPassword.IsPassword = !entryPassword.IsPassword;
            TogglePasswordButton.Text = entryPassword.IsPassword ? "Show" : "Hide";
        }

        private void OnToggleConfirmPasswordTapped(object sender, TappedEventArgs e)
        {
            entryConfirmPassword.IsPassword = !entryConfirmPassword.IsPassword;
            ToggleConfirmPasswordButton.Text = entryConfirmPassword.IsPassword ? "Show" : "Hide";
        }

        private void OnPasswordFieldsChanged(object sender, TextChangedEventArgs e)
        {
            UpdateValidationState();
        }

        private void UpdateValidationState()
        {
            string password = entryPassword?.Text?.Trim() ?? string.Empty;
            string confirmPassword = entryConfirmPassword?.Text?.Trim() ?? string.Empty;

            bool hasPassword = !string.IsNullOrWhiteSpace(password);
            bool hasConfirmPassword = !string.IsNullOrWhiteSpace(confirmPassword);
            bool bothFilled = hasPassword && hasConfirmPassword;
            bool matches = password == confirmPassword;

            ResetInputBorders();

            if (!bothFilled)
            {
                ValidationMessageLabel.IsVisible = false;
                ValidationMessageLabel.Text = string.Empty;

                ConfirmButton.IsEnabled = false;
                ConfirmButton.Opacity = 0.45;
                return;
            }

            if (!matches)
            {
                ShowMismatchState();
                ConfirmButton.IsEnabled = false;
                ConfirmButton.Opacity = 0.45;
                return;
            }

            ValidationMessageLabel.IsVisible = false;
            ValidationMessageLabel.Text = string.Empty;

            ConfirmButton.IsEnabled = true;
            ConfirmButton.Opacity = 1.0;
        }

        private void ResetInputBorders()
        {
            PasswordInputBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2D2D35")
                : Color.FromArgb("#F1ECFB");

            ConfirmPasswordInputBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2D2D35")
                : Color.FromArgb("#F1ECFB");
        }

        private void ShowMismatchState()
        {
            var errorColor = Color.FromArgb("#D95757");

            PasswordInputBorder.Stroke = errorColor;
            ConfirmPasswordInputBorder.Stroke = errorColor;

            ValidationMessageLabel.Text = "Passwords do not match.";
            ValidationMessageLabel.IsVisible = true;
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            string password = entryPassword.Text?.Trim() ?? string.Empty;
            string confirmPassword = entryConfirmPassword.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                UpdateValidationState();
                await DisplayAlert("Error", "Password cannot be empty.", "OK");
                return;
            }

            if (password != confirmPassword)
            {
                UpdateValidationState();
                await DisplayAlert("Error", "Passwords do not match.", "OK");
                return;
            }

            try
            {
                ConfirmButton.IsEnabled = false;
                ConfirmButton.Text = "Saving...";
                ConfirmButton.Opacity = 1.0;

                await dbHelper.SavePasswordAsync(password);

                await DisplayAlert("Success", "Password has been set.", "OK");
                await Navigation.PopAsync();
            }
            catch
            {
                await DisplayAlert("Error", "Failed to save password. Please try again.", "OK");
            }
            finally
            {
                ConfirmButton.Text = "Confirm";
                UpdateValidationState();
            }
        }
    }
}