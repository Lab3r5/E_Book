using System.Windows.Input;

namespace E_Book.Pages
{
    public partial class PrivacyPolicyPage : ContentPage
    {
        public ICommand BackCommand { get; }

        public PrivacyPolicyPage()
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
    }
}