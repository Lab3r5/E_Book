using E_Book.Pages;
using E_Book.Services;
using Microsoft.Maui.ApplicationModel;

namespace E_Book
{
    public partial class AppShell : Shell
    {
        public const string RouteLogin = "login";
        public const string RouteTabs = "tabs";
        public const string RouteBookshelf = "bookshelf";
        public const string RouteSearch = "search";
        public const string RouteSettings = "settings";

        public const string RouteSignup = "signup";
        public const string RouteEditProfile = "edit-profile";
        public const string RouteAppearance = "appearance";
        public const string RouteHelp = "help";
        public const string RouteNotifications = "notifications";
        public const string RouteHelpCenter = "help-center";
        public const string RoutePrivacyPolicy = "privacy-policy";
        public const string RoutePassword = "password";
        public const string RouteReading = "reading";
        public const string RouteForgotPassword = "forgot-password";
        public const string RouteResetPassword = "reset-password";
        public const string RoutePdfReader = "pdf-reader";
        public const string RouteImageReader = "image-reader";

#if ANDROID
        private bool _tabsBoundOnce;
#endif

        private bool _startupRedirectDone;

        public AppShell()
        {
            InitializeComponent();

            RegisterRoutes();
            Navigated += OnShellNavigated;
            Loaded += OnShellLoaded;
        }

        private void RegisterRoutes()
        {
            Routing.RegisterRoute(RouteSignup, typeof(SignUpPage));
            Routing.RegisterRoute(RouteEditProfile, typeof(EditProfilePage));
            Routing.RegisterRoute(RouteAppearance, typeof(AppearancePage));
            Routing.RegisterRoute(RouteHelp, typeof(HelpSupportPage));
            Routing.RegisterRoute(RouteHelpCenter, typeof(HelpCenterPage));
            Routing.RegisterRoute(RoutePrivacyPolicy, typeof(PrivacyPolicyPage));
            Routing.RegisterRoute(RouteNotifications, typeof(NotificationsPage));
            Routing.RegisterRoute(RoutePassword, typeof(PasswordPage));
            Routing.RegisterRoute(RouteReading, typeof(ReadingPage));
            Routing.RegisterRoute(RoutePdfReader, typeof(PdfReaderPage));
            Routing.RegisterRoute(RouteForgotPassword, typeof(ForgotPasswordPage));
            Routing.RegisterRoute(RouteResetPassword, typeof(ResetPasswordPage));
            Routing.RegisterRoute(RouteImageReader, typeof(ImageReaderPage));
        }

        private async void OnShellLoaded(object? sender, EventArgs e)
        {
            if (_startupRedirectDone)
                return;

            _startupRedirectDone = true;
            Loaded -= OnShellLoaded;

            try
            {
                await GoToAsync($"//{RouteLogin}");
            }
            catch
            {
            }
        }

        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
#if ANDROID
            HandleAndroidTabRebind();
            HandleAndroidKeyboardByRoute();
#endif
        }

#if ANDROID
        private void HandleAndroidTabRebind()
        {
            string location = CurrentState?.Location?.ToString() ?? string.Empty;
            bool isInTabs = location.StartsWith($"//{RouteTabs}", StringComparison.OrdinalIgnoreCase);

            if (isInTabs)
            {
                if (_tabsBoundOnce)
                    return;

                _tabsBoundOnce = true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainActivity.Instance?.RebindBottomTabBar();
                });
            }
            else
            {
                _tabsBoundOnce = false;
            }
        }
#endif

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler == null)
            {
                Navigated -= OnShellNavigated;
                Loaded -= OnShellLoaded;
            }
        }
#if ANDROID
private void HandleAndroidKeyboardByRoute()
{
    string location = CurrentState?.Location?.ToString() ?? string.Empty;

    bool isOnSearchTab =
        location.StartsWith($"//{RouteTabs}/{RouteSearch}", StringComparison.OrdinalIgnoreCase) ||
        location.Equals($"//{RouteTabs}/{RouteSearch}", StringComparison.OrdinalIgnoreCase);

    if (!isOnSearchTab)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MainActivity.Instance?.HideSoftKeyboard();
        });
    }
}
#endif
    }
}