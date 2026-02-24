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

            // Update profile summary
            NameLabel.Text = UserSession.DisplayName;

            // Guest does not show account info
            EmailLabel.Text = UserSession.IsGuest ? "" : UserSession.UserId;
            EmailLabel.IsVisible = !UserSession.IsGuest;
        }

        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            // Open as MODAL so TabBar will NOT appear on the sub page
            await Navigation.PushModalAsync(new NavigationPage(new EditProfilePage()));
        }

        private async void OnAppearanceClicked(object sender, EventArgs e)
        {
            // Open as MODAL so TabBar will NOT appear on the sub page
            await Navigation.PushModalAsync(new NavigationPage(new AppearancePage()));
        }

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            // Open as MODAL so TabBar will NOT appear on the sub page
            await Navigation.PushModalAsync(new NavigationPage(new HelpSupportPage()));
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            UserSession.Logout();

            // If any modal pages are open, close them first (safety net)
            if (Application.Current?.MainPage is Page mainPage)
            {
                while (mainPage.Navigation.ModalStack.Count > 0)
                {
                    await mainPage.Navigation.PopModalAsync(false);
                }
            }

            // Go to login (Shell absolute route)
            await Shell.Current.GoToAsync("//login");
        }
    }
}