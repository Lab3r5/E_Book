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

            // 加载已保存姓名（按用户隔离，Guest也有自己的）
            var savedName = Preferences.Get(ProfileKey, UserSession.DisplayName);

            NameEntry.Text = savedName;
            EmailEntry.Text = UserSession.IsGuest ? "" : UserSession.UserId;

            // Guest：不显示账号/密码修改，不允许保存
            EmailEntry.IsVisible = !UserSession.IsGuest;
            ChangePasswordButton.IsVisible = !UserSession.IsGuest;
            SaveButton.IsVisible = !UserSession.IsGuest;

            if (UserSession.IsGuest)
            {
                NameEntry.IsEnabled = false;
                NameEntry.Text = "Guest";
            }
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            // 先跳你已有 PasswordPage（你后面再把它升级成“账户密码/Pin”）
            await Shell.Current.GoToAsync("password");
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

            // 同步到Session显示名（简单做法：直接覆盖DisplayName）
            UserSession.SetUser(UserSession.UserId, name);

            await DisplayAlert("Saved", "Profile updated.", "OK");
            await Shell.Current.GoToAsync("..");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            UserSession.Logout();
            await Shell.Current.GoToAsync("//login");
        }
    }
}