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

            RequestedThemeChanged += OnRequestedThemeChanged;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
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