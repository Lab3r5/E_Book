using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using EZ_Read.Data;

namespace EZ_Read.Pages
{
    public partial class PasswordPage : ContentPage
    {
        private readonly Database dbHelper = new();

        public PasswordPage()
        {
            InitializeComponent();
        }

        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(entryPassword.Text) || string.IsNullOrEmpty(entryConfirmPassword.Text))
            {
                await DisplayAlert("Wrong", "The password cannot be empty!", "Confirm");
                return;
            }

            if (entryPassword.Text != entryConfirmPassword.Text)
            {
                await DisplayAlert("Wrong", "The passwords entered twice do not match!", "Confirm");
                return;
            }

            await dbHelper.SavePasswordAsync(entryPassword.Text);
            await DisplayAlert("Success", "The password has been set.", "Confirm");
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}
