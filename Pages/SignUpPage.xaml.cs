using E_Book.Services;
using E_Book.Data;

namespace E_Book.Pages;

public partial class SignUpPage : ContentPage
{
    private readonly Database _database = new();

    private bool _passwordVisible;
    private bool _hasAnimatedIn;
    private bool _isLogoBreathing;
    private bool _isThemeAnimating;
    private bool _isNavigatingBack;

    public SignUpPage()
    {
        InitializeComponent();

        NameEntry.Focused += OnEntryFocused;
        NameEntry.Unfocused += OnEntryUnfocused;

        EmailEntry.Focused += OnEntryFocused;
        EmailEntry.Unfocused += OnEntryUnfocused;

        PasswordEntry.Focused += OnEntryFocused;
        PasswordEntry.Unfocused += OnEntryUnfocused;

        UpdateThemeIcon();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        UpdateThemeIcon();

        if (!_hasAnimatedIn)
        {
            _hasAnimatedIn = true;

            LogoFrame.Opacity = 0;
            LogoFrame.TranslationY = -10;
            LogoFrame.Scale = 0.94;

            SignUpCard.Opacity = 0;
            SignUpCard.TranslationY = 32;
            SignUpCard.Scale = 0.98;

            await Task.WhenAll(
                LogoFrame.FadeTo(1, 280, Easing.CubicOut),
                LogoFrame.TranslateTo(0, -22, 280, Easing.CubicOut),
                LogoFrame.ScaleTo(1, 280, Easing.CubicOut)
            );

            await Task.WhenAll(
                SignUpCard.FadeTo(1, 380, Easing.CubicOut),
                SignUpCard.TranslateTo(0, 0, 380, Easing.SpringOut),
                SignUpCard.ScaleTo(1, 380, Easing.SpringOut)
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
        if (_isLogoBreathing) return;

        _isLogoBreathing = true;

        while (_isLogoBreathing)
        {
            await LogoFrame.ScaleTo(1.02, 1400, Easing.SinInOut);
            if (!_isLogoBreathing) break;
            await LogoFrame.ScaleTo(1.00, 1400, Easing.SinInOut);
        }
    }

    private void UpdateThemeIcon()
    {
        if (ThemeIconLabel == null) return;

        ThemeIconLabel.Text =
            Application.Current?.RequestedTheme == AppTheme.Dark ? "☀" : "🌙";
    }

    private async void OnThemeToggleTapped(object sender, TappedEventArgs e)
    {
        if (Application.Current == null || _isThemeAnimating)
            return;

        _isThemeAnimating = true;

        try
        {
            var pressAnim = Task.WhenAll(
                ThemeToggleBorder.ScaleTo(0.90, 90, Easing.CubicOut),
                ThemeIconLabel.RotateTo(90, 120, Easing.CubicOut)
            );

            var fadeOutAnim = SignUpCard.FadeTo(0.82, 120, Easing.CubicOut);

            await Task.WhenAll(pressAnim, fadeOutAnim);

            Application.Current.UserAppTheme =
                Application.Current.RequestedTheme == AppTheme.Dark
                    ? AppTheme.Light
                    : AppTheme.Dark;

            UpdateThemeIcon();

            ThemeIconLabel.Rotation = -90;

            await Task.WhenAll(
                ThemeToggleBorder.ScaleTo(1.00, 140, Easing.SpringOut),
                ThemeIconLabel.RotateTo(0, 180, Easing.SpringOut),
                SignUpCard.FadeTo(1.00, 180, Easing.CubicInOut)
            );
        }
        finally
        {
            _isThemeAnimating = false;
        }
    }

    private void OnNameCompleted(object sender, EventArgs e)
    {
        EmailEntry.Focus();
    }

    private void OnEmailCompleted(object sender, EventArgs e)
    {
        PasswordEntry.Focus();
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        await PerformSignUpAsync();
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordEntry.IsPassword = !_passwordVisible;
        TogglePasswordButton.Text = _passwordVisible ? "🙈" : "👁";
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        await PerformSignUpAsync();
    }

    private async Task PerformSignUpAsync()
    {
        await SignUpButton.ScaleTo(0.96, 70);
        await SignUpButton.ScaleTo(1, 90);

        var name = NameEntry.Text?.Trim() ?? "";
        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Error", "All fields are required.", "OK");
            return;
        }

        SignUpButton.Text = "";
        SignUpButton.IsEnabled = false;

        SignUpLoading.IsVisible = true;
        SignUpLoading.IsRunning = true;

        try
        {
            await Task.Delay(500);

            string normalizedEmail = UserSession.NormalizeUserId(email);

            var result = await _database.RegisterUserAsync(normalizedEmail, name, password);
            if (!result.Success)
            {
                await DisplayAlert("Sign up failed", result.Message, "OK");
                return;
            }

            UserSession.SetUser(normalizedEmail, name);
            LibraryService.EnsureLibraryExists();
            ThemeScheduler.StartTimer();
            ThemeScheduler.ApplyNow();

            await Shell.Current.GoToAsync($"//{AppShell.RouteTabs}/{AppShell.RouteBookshelf}");
        }
        finally
        {
            SignUpLoading.IsRunning = false;
            SignUpLoading.IsVisible = false;

            SignUpButton.Text = "Sign Up";
            SignUpButton.IsEnabled = true;
        }
    }

    private async Task AnimateBackAndGoAsync()
    {
        if (_isNavigatingBack)
            return;

        _isNavigatingBack = true;
        _isLogoBreathing = false;

        try
        {
            await Task.WhenAll(
                SignUpCard.FadeTo(0, 180, Easing.CubicIn),
                SignUpCard.TranslateTo(0, 26, 180, Easing.CubicIn),
                LogoFrame.FadeTo(0, 160, Easing.CubicIn),
                LogoFrame.ScaleTo(0.94, 160, Easing.CubicIn)
            );

            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isNavigatingBack = false;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await AnimateBackAndGoAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await AnimateBackAndGoAsync();
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        if (sender == NameEntry)
            NameBorder.Stroke = Color.FromArgb("#A48BFF");

        if (sender == EmailEntry)
            EmailBorder.Stroke = Color.FromArgb("#A48BFF");

        if (sender == PasswordEntry)
            PasswordBorder.Stroke = Color.FromArgb("#A48BFF");
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        NameBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2D2D35")
            : Color.FromArgb("#E8E0F8");

        EmailBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2D2D35")
            : Color.FromArgb("#E8E0F8");

        PasswordBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2D2D35")
            : Color.FromArgb("#E8E0F8");
    }
}