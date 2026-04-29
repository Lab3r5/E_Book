using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using E_Book.Services;

namespace E_Book.Pages
{
    public partial class HelpSupportPage : ContentPage
    {
        public ICommand BackCommand { get; }

        public HelpSupportPage()
        {
            InitializeComponent();

            BackCommand = new Command(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync("..");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Navigation Error", ex.Message, "OK");
                }
            });

            BindingContext = this;
        }

        private async void OnHelpCenterTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await UIAnimationService.PressAsync(v, 0.975, 0.98, 70, 110);

            try
            {
                await Shell.Current.GoToAsync(AppShell.RouteHelpCenter);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Open Page Failed", ex.Message, "OK");
            }
        }

        private async void OnContactTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await UIAnimationService.PressAsync(v, 0.975, 0.98, 70, 110);

            try
            {
                var subject = Uri.EscapeDataString("E_Book Support Request");
                var body = Uri.EscapeDataString(
                    "Hello Support,\n\n" +
                    "Please describe your issue below.\n\n" +
                    "Issue:\n" +
                    "Device / Platform:\n" +
                    "App Version:\n" +
                    "Steps to reproduce:\n\n" +
                    "Thank you."
                );

                var mailto = $"mailto:support@ebookapp.com?subject={subject}&body={body}";
                await Launcher.OpenAsync(mailto);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Contact Failed", ex.Message, "OK");
            }
        }

        private async void OnPrivacyTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await UIAnimationService.PressAsync(v, 0.975, 0.98, 70, 110);

            try
            {
                await Shell.Current.GoToAsync(AppShell.RoutePrivacyPolicy);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Open Page Failed", ex.Message, "OK");
            }
        }
    }
}