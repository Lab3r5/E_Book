using System.Xml;
using E_Book.Services;

namespace E_Book.Pages
{
    public partial class SettingsHomePage : ContentPage
    {
        public SettingsHomePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            NameLabel.Text = UserSession.DisplayName;

            // Guest不显示账号信息
            EmailLabel.Text = UserSession.IsGuest ? "" : UserSession.UserId;
            EmailLabel.IsVisible = !UserSession.IsGuest;
        }

        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("edit-profile");
        }

        private async void OnAppearanceClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("appearance");
        }

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("help");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            UserSession.Logout();
            await Shell.Current.GoToAsync("//login");
        }
    }
}