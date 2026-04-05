using System.Windows.Input;
using E_Book.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class NotificationsPage : ContentPage
    {
        private bool _isInitializing;

        public ICommand BackCommand { get; }

        public NotificationsPage()
        {
            InitializeComponent();

            BackCommand = new Command(async () => await GoBackAsync());
            BindingContext = this;

#if DEBUG
            DebugSection.IsVisible = true;
#endif
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            _isInitializing = true;

            NotificationsSwitch.IsToggled = Preferences.Get(NotificationPrefs.NotificationsEnabled, true);
            ContinueReadingSwitch.IsToggled = Preferences.Get(NotificationPrefs.ContinueReadingEnabled, true);
            ImportAlertSwitch.IsToggled = Preferences.Get(NotificationPrefs.ImportAlertEnabled, true);

            _isInitializing = false;
        }

        private async Task GoBackAsync()
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                try
                {
                    await Navigation.PopAsync();
                }
                catch
                {
                }
            }
        }

        private async void OnNotificationsToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(NotificationPrefs.NotificationsEnabled, e.Value);

            if (_isInitializing || !e.Value)
                return;

            try
            {
                var notificationService = Application.Current?
                    .Handler?
                    .MauiContext?
                    .Services
                    .GetService<INotificationService>();

                if (notificationService != null)
                    await notificationService.RequestPermissionAsync();
            }
            catch
            {
            }
        }

        private void OnContinueReadingToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(NotificationPrefs.ContinueReadingEnabled, e.Value);
        }

        private void OnImportAlertToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(NotificationPrefs.ImportAlertEnabled, e.Value);
        }
#if DEBUG

        private async void OnDebugImportNotificationClicked(object sender, EventArgs e)
        {
            try
            {
                var service = Application.Current?
                    .Handler?
                    .MauiContext?
                    .Services
                    .GetService<INotificationService>();

                if (service == null)
                    return;

                await service.ShowNowAsync(
                    "Book imported successfully",
                    "\"Debug_Book.pdf\" has been added to your library.");
            }
            catch
            {
            }
        }

        private async void OnDebugContinueReadingClicked(object sender, EventArgs e)
        {
            try
            {
                var service = Application.Current?
                    .Handler?
                    .MauiContext?
                    .Services
                    .GetService<INotificationService>();

                if (service == null)
                    return;

                await service.ShowNowAsync(
                    "Continue reading",
                    "Resume \"Debug Book\" at page 18.");
            }
            catch
            {
            }
        }

#endif
    }
}