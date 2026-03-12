using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;

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
                    return;
                }
                catch
                {
                }

                try
                {
                    await Navigation.PopAsync();
                    return;
                }
                catch
                {
                }

                await Navigation.PopModalAsync();
            });

            BindingContext = this;
        }

        private async Task PressAnim(VisualElement view)
        {
            if (view == null) return;

            await view.ScaleTo(0.97, 70, Easing.CubicOut);
            await view.ScaleTo(1.00, 110, Easing.CubicOut);
        }

        private async void OnHelpCenterTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

            await Navigation.PushAsync(new HelpCenterPage());
        }

        private async void OnContactTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

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

        private async void OnPrivacyTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await PressAnim(v);

            await Navigation.PushAsync(new PrivacyPolicyPage());
        }
    }
}