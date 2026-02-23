namespace E_Book.Pages
{
    public partial class HelpSupportPage : ContentPage
    {
        public HelpSupportPage()
        {
            InitializeComponent();
        }

        private async void OnFaqClicked(object sender, EventArgs e)
        {
            await DisplayAlert("FAQs", "Coming soon.", "OK");
        }

        private async void OnPrivacyClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Privacy Policy", "Coming soon.", "OK");
        }
    }
}