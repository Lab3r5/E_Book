using Microsoft.Maui.Controls;
using E_Book.Services;

namespace E_Book.Pages
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage()
        {
            InitializeComponent();
        }

        private async void OnGuestClicked(object sender, EventArgs e)
        {
            UserSession.SetGuest();
            // 进入主Tab
            await Shell.Current.GoToAsync("//tabs/bookshelf");
        }

        private async void OnSignUpClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("signup");
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            // 先用“本地模拟登录”（后面你要真注册系统我们再升级）
            var email = EmailEntry.Text?.Trim() ?? "";
            var pwd = PasswordEntry.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
            {
                await DisplayAlert("Error", "Email and password are required.", "OK");
                return;
            }

            // ✅ MVP：暂时认为输入即登录成功
            // 如果你之后要做真正注册验证，我会帮你把账号保存到Preferences/SQLite
            UserSession.SetUser(userId: email.ToLowerInvariant(), displayName: email.Split('@')[0]);

            await Shell.Current.GoToAsync("//tabs/bookshelf");
        }
    }
}