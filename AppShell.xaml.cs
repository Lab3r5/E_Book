using E_Book.Pages;
using Microsoft.Maui.ApplicationModel;

namespace E_Book
{
    public partial class AppShell : Shell
    {
#if ANDROID
        private bool _tabsBoundOnce = false;
#endif

        public AppShell()
        {
            InitializeComponent();

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

            // 只在进入 tabs 的时候触发一次
            if (loc.StartsWith("//tabs"))
            {
                if (_tabsBoundOnce) return;
                _tabsBoundOnce = true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainActivity.Instance?.RebindBottomTabBar();
                });
            }
            else
            {
                // 离开 tabs（例如去 login）后，允许下次再触发
                _tabsBoundOnce = false;
            }
#endif
        }
    }
}