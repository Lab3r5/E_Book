using E_Book.Services;

namespace E_Book.Pages
{
    public partial class SignUpPage : ContentPage
    {
        public SignUpPage()
        {
            InitializeComponent();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnSignUpClicked(object sender, EventArgs e)
        {
            var name = NameEntry.Text?.Trim() ?? "";
            var email = EmailEntry.Text?.Trim() ?? "";
            var pwd = PasswordEntry.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
            {
                await DisplayAlert("Error", "All fields are required.", "OK");
                return;
            }

            // MVP：直接注册即登录（后续我们做真实账户存储/校验）
            UserSession.SetUser(email.ToLowerInvariant(), name);

            await Shell.Current.GoToAsync("//tabs/bookshelf");
        }
    }
}