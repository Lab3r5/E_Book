using E_Book.Pages;
using Microsoft.Maui.ApplicationModel;

namespace E_Book
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Sub pages (not in TabBar)
            Routing.RegisterRoute("signup", typeof(SignUpPage));
            Routing.RegisterRoute("edit-profile", typeof(EditProfilePage));
            Routing.RegisterRoute("appearance", typeof(AppearancePage));
            Routing.RegisterRoute("help", typeof(HelpSupportPage));
            Routing.RegisterRoute("password", typeof(PasswordPage));
            Routing.RegisterRoute("reading", typeof(ReadingPage));

            Navigated += OnShellNavigated;
        }

        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
#if ANDROID
            var loc = CurrentState?.Location?.ToString() ?? "";
            if (loc.StartsWith("//tabs"))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainActivity.Instance?.RebindBottomTabBar();
                });
            }
#endif
        }
    }
}