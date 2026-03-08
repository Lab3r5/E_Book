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
                    await Navigation.PopAsync();
                    return;
                }
                catch
                {
                }

                try
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }
                catch
                {
                }

                await Navigation.PopModalAsync();
            });

            BindingContext = this;
        }
    }
}