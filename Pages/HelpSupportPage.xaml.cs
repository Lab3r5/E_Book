using System.Windows.Input;

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
                try { await Shell.Current.GoToAsync(".."); return; } catch { }
                await Navigation.PopModalAsync();
            });

            BindingContext = this;
        }

        private async Task PressAnim(VisualElement view)
        {
            if (view == null) return;
            await view.ScaleTo(0.96, 80, Easing.CubicOut);
            await view.ScaleTo(1.00, 120, Easing.CubicOut);
        }

        private async void OnFaqTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);
            await DisplayAlert("FAQs", "Coming soon.", "OK");
        }

        private async void OnPrivacyTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);
            await DisplayAlert("Privacy Policy", "Coming soon.", "OK");
        }
    }
}