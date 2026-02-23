using E_Book.Pages;

namespace E_Book
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for sub pages
            // These pages are NOT part of the TabBar
            // They will be navigated using Shell.Current.GoToAsync("route")

            Routing.RegisterRoute("signup", typeof(SignUpPage));
            Routing.RegisterRoute("edit-profile", typeof(EditProfilePage));
            Routing.RegisterRoute("appearance", typeof(AppearancePage));
            Routing.RegisterRoute("help", typeof(HelpSupportPage));
            Routing.RegisterRoute("password", typeof(PasswordPage));
            Routing.RegisterRoute("reading", typeof(ReadingPage));
        }
    }
}