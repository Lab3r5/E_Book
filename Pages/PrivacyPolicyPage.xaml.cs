using System.Windows.Input;

namespace E_Book.Pages
{
    public partial class PrivacyPolicyPage : ContentPage
    {
        public ICommand BackCommand { get; }

        public PrivacyPolicyPage()
        {
            InitializeComponent();

            BackCommand = new Command(async () => await GoBackAsync());

            BindingContext = this;
        }

        private async Task GoBackAsync()
        {
            try
            {
                var state = Shell.Current?.CurrentState?.Location?.ToString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(state) && state.Contains('/'))
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.GoToAsync($"//{AppShell.RouteTabs}/{AppShell.RouteSettings}");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Navigation Error", ex.Message, "OK");
            }
        }
    }
}