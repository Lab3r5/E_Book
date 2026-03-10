using E_Book.Services;

namespace E_Book.Pages;

public partial class SignUpPage : ContentPage
{
    private bool _passwordVisible;
    private bool _hasAnimatedIn;
    private bool _isLogoBreathing;

    public SignUpPage()
    {
        InitializeComponent();

        NameEntry.Focused += OnEntryFocused;
        NameEntry.Unfocused += OnEntryUnfocused;

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

        await Task.Delay(700);

        UserSession.SetUser(email.ToLowerInvariant(), name);

        SignUpLoading.IsRunning = false;
        SignUpLoading.IsVisible = false;

        SignUpButton.Text = "Sign Up";
        SignUpButton.IsEnabled = true;

        await Shell.Current.GoToAsync($"//{AppShell.RouteTabs}/{AppShell.RouteBookshelf}");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
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
        NameBorder.Stroke = Color.FromArgb("#E8E0F8");
        EmailBorder.Stroke = Color.FromArgb("#E8E0F8");
        PasswordBorder.Stroke = Color.FromArgb("#E8E0F8");
    }
}