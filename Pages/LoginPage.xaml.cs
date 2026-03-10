using E_Book.Services;

namespace E_Book.Pages;

public partial class LoginPage : ContentPage
{
    private bool _passwordVisible;
    private bool _hasAnimatedIn;
    private bool _isLogoBreathing;

    public LoginPage()
    {
        InitializeComponent();

        EmailEntry.Focused += OnEntryFocused;
        EmailEntry.Unfocused += OnEntryUnfocused;

        PasswordEntry.Focused += OnEntryFocused;
        PasswordEntry.Unfocused += OnEntryUnfocused;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasAnimatedIn)
        {
            _hasAnimatedIn = true;

            LogoFrame.Opacity = 0;
            LogoFrame.TranslationY = -10;
            LogoFrame.Scale = 0.94;

            LoginCard.Opacity = 0;
            LoginCard.TranslationY = 32;
            LoginCard.Scale = 0.98;

            await Task.WhenAll(
                LogoFrame.FadeTo(1, 280, Easing.CubicOut),
                LogoFrame.TranslateTo(0, -22, 280, Easing.CubicOut),
                LogoFrame.ScaleTo(1, 280, Easing.CubicOut)
            );

            await Task.WhenAll(
                LoginCard.FadeTo(1, 380, Easing.CubicOut),
                LoginCard.TranslateTo(0, 0, 380, Easing.SpringOut),
                LoginCard.ScaleTo(1, 380, Easing.SpringOut)
            );
        }

        StartLogoBreathing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isLogoBreathing = false;
    }

    private async void StartLogoBreathing()
    {
        if (_isLogoBreathing)
            return;

        _isLogoBreathing = true;

        while (_isLogoBreathing)
        {
            await LogoFrame.ScaleTo(1.02, 1400, Easing.SinInOut);
            if (!_isLogoBreathing) break;
            await LogoFrame.ScaleTo(1.00, 1400, Easing.SinInOut);
        }
    }

    private void OnEmailCompleted(object sender, EventArgs e)
    {
        PasswordEntry.Focus();
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        await PerformLoginAsync();
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordEntry.IsPassword = !_passwordVisible;
        TogglePasswordButton.Text = _passwordVisible ? "🙈" : "👁";
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await PerformLoginAsync();
    }

    private async Task PerformLoginAsync()
    {
        await LoginButton.ScaleTo(0.96, 70, Easing.CubicIn);
        await LoginButton.ScaleTo(1.00, 90, Easing.CubicOut);

        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "Email and password are required.", "OK");
            return;
        }

        LoginButton.Text = "";
        LoginButton.IsEnabled = false;
        LoginLoading.IsVisible = true;
        LoginLoading.IsRunning = true;

        await Task.Delay(700);

        var displayName = email.Contains('@') ? email.Split('@')[0] : email;
        UserSession.SetUser(email.ToLowerInvariant(), displayName);

        LoginLoading.IsRunning = false;
        LoginLoading.IsVisible = false;
        LoginButton.Text = "Log In";
        LoginButton.IsEnabled = true;

        await Shell.Current.GoToAsync($"//{AppShell.RouteTabs}/{AppShell.RouteBookshelf}");
    }

    private async void OnGuestClicked(object sender, EventArgs e)
    {
        if (sender is VisualElement view)
        {
            await view.ScaleTo(0.97, 60, Easing.CubicIn);
            await view.ScaleTo(1, 80, Easing.CubicOut);
        }

        UserSession.SetGuest();
        await Shell.Current.GoToAsync($"//{AppShell.RouteTabs}/{AppShell.RouteBookshelf}");
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        if (sender is VisualElement view)
        {
            await view.ScaleTo(0.96, 60, Easing.CubicIn);
            await view.ScaleTo(1, 80, Easing.CubicOut);
        }

        await Shell.Current.GoToAsync("signup");
    }

    private async void OnForgetTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Reset Password", "Feature coming soon.", "OK");
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        if (sender == EmailEntry)
            EmailBorder.Stroke = Color.FromArgb("#A48BFF");

        if (sender == PasswordEntry)
            PasswordBorder.Stroke = Color.FromArgb("#A48BFF");
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        EmailBorder.Stroke = Color.FromArgb("#E8E0F8");
        PasswordBorder.Stroke = Color.FromArgb("#E8E0F8");
    }
}