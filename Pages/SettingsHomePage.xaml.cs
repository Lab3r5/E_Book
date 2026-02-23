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
            await Navigation.PushModalAsync(new NavigationPage(new EditProfilePage()));
        }

        private async void OnAppearanceClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new NavigationPage(new AppearancePage()));
        }

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new NavigationPage(new HelpSupportPage()));
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            UserSession.Logout();
            await Shell.Current.GoToAsync("//login");
        }
    }
}