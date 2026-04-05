using E_Book.Services;
using Microsoft.Maui.ApplicationModel;

namespace E_Book
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            ThemeScheduler.StartTimer();
            ThemeScheduler.ApplyNow();

#if ANDROID
            RequestedThemeChanged += OnRequestedThemeChanged;
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Created += async (_, __) =>
            {
                await TryShowContinueReadingReminderAsync();
            };

            window.Activated += async (_, __) =>
            {
                await TryShowContinueReadingReminderAsync();
            };

            return window;
        }

        private async Task TryShowContinueReadingReminderAsync()
        {
            try
            {
                bool enabled = Preferences.Get(NotificationPrefs.NotificationsEnabled, true);
                bool continueEnabled = Preferences.Get(NotificationPrefs.ContinueReadingEnabled, true);

                if (!enabled || !continueEnabled)
                    return;

                string title = Preferences.Get(NotificationPrefs.PendingBookTitle, "");
                int page = Preferences.Get(NotificationPrefs.PendingBookPage, 0);
                string time = Preferences.Get(NotificationPrefs.PendingBookTime, "");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(time))
                    return;

                if (!DateTime.TryParse(time, out var lastTime))
                    return;

                if (DateTime.Now - lastTime < TimeSpan.FromHours(12))
                    return;

                var service = Current?.Handler?.MauiContext?.Services.GetService<INotificationService>();

                if (service != null)
                {
                    await service.ShowNowAsync(
                        "Continue reading",
                        $"Resume \"{title}\" at page {Math.Max(1, page)}.");
                }

                Preferences.Set(NotificationPrefs.PendingBookTime, DateTime.Now.ToString("O"));
            }
            catch
            {
            }
        }

#if ANDROID
        private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MainActivity.Instance?.ApplySystemBarTheme();
            });
        }
#endif
    }
}