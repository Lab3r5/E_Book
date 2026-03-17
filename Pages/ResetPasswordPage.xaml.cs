using E_Book.Data;

namespace E_Book.Pages;

[QueryProperty(nameof(Email), "email")]
public partial class ResetPasswordPage : ContentPage
{
    private readonly Database _database = new();

    private bool _passwordVisible;
    private bool _confirmVisible;
    private bool _hasAnimatedIn;
    private bool _isLogoBreathing;
    private bool _isThemeAnimating;
    private bool _isNavigatingBack;

    public string Email { get; set; } = string.Empty;

    public ResetPasswordPage()
    {
        InitializeComponent();

        PasswordEntry.Focused += OnEntryFocused;
        PasswordEntry.Unfocused += OnEntryUnfocused;

        ConfirmEntry.Focused += OnEntryFocused;
        ConfirmEntry.Unfocused += OnEntryUnfocused;

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

            ResetCard.Opacity = 0;
            ResetCard.TranslationY = 32;
            ResetCard.Scale = 0.98;

            await Task.WhenAll(
                LogoFrame.FadeTo(1, 280, Easing.CubicOut),
                LogoFrame.TranslateTo(0, -22, 280, Easing.CubicOut),
                LogoFrame.ScaleTo(1, 280, Easing.CubicOut)
            );

            await Task.WhenAll(
                ResetCard.FadeTo(1, 380, Easing.CubicOut),
                ResetCard.TranslateTo(0, 0, 380, Easing.SpringOut),
                ResetCard.ScaleTo(1, 380, Easing.SpringOut)
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

            var fadeOutAnim = ResetCard.FadeTo(0.82, 120, Easing.CubicOut);

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
                ResetCard.FadeTo(1.00, 180, Easing.CubicInOut)
            );
        }
        finally
        {
            _isThemeAnimating = false;
        }
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        ConfirmEntry.Focus();
        await ScrollEntryIntoViewAsync(ConfirmEntry);
    }

    private async void OnConfirmCompleted(object sender, EventArgs e)
    {
        await ResetPasswordAsync();
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordEntry.IsPassword = !_passwordVisible;
        TogglePasswordButton.Text = _passwordVisible ? "🙈" : "👁";
    }

    private void OnToggleConfirmClicked(object sender, EventArgs e)
    {
        _confirmVisible = !_confirmVisible;
        ConfirmEntry.IsPassword = !_confirmVisible;
        ToggleConfirmButton.Text = _confirmVisible ? "🙈" : "👁";
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        await ResetPasswordAsync();
    }

    private async Task ResetPasswordAsync()
    {
        await ResetButton.ScaleTo(0.96, 70, Easing.CubicIn);
        await ResetButton.ScaleTo(1.00, 90, Easing.CubicOut);

        string password = PasswordEntry.Text?.Trim() ?? "";
        string confirm = ConfirmEntry.Text?.Trim() ?? "";
        string normalizedEmail = Uri.UnescapeDataString(Email ?? string.Empty);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            await DisplayAlert("Error", "Invalid reset request.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirm))
        {
            await DisplayAlert("Error", "Please complete all fields.", "OK");
            return;
        }

        if (password.Length < 4)
        {
            await DisplayAlert("Error", "Password must be at least 4 characters.", "OK");
            return;
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            await ConfirmBorder.TranslateTo(-8, 0, 40);
            await ConfirmBorder.TranslateTo(8, 0, 40);
            await ConfirmBorder.TranslateTo(-5, 0, 40);
            await ConfirmBorder.TranslateTo(5, 0, 40);
            await ConfirmBorder.TranslateTo(0, 0, 40);

            await DisplayAlert("Error", "Passwords do not match.", "OK");
            return;
        }

        ResetButton.Text = "";
        ResetButton.IsEnabled = false;
        ResetLoading.IsVisible = true;
        ResetLoading.IsRunning = true;

        try
        {
            await Task.Delay(450);

            await _database.UpdatePasswordAsync(normalizedEmail, password);

            await DisplayAlert("Success", "Your password has been updated.", "OK");
            await Shell.Current.GoToAsync($"//{AppShell.RouteLogin}");
        }
        finally
        {
            ResetLoading.IsRunning = false;
            ResetLoading.IsVisible = false;
            ResetButton.Text = "Update Password";
            ResetButton.IsEnabled = true;
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
                ResetCard.FadeTo(0, 180, Easing.CubicIn),
                ResetCard.TranslateTo(0, 26, 180, Easing.CubicIn),
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

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        if (sender == PasswordEntry)
            PasswordBorder.Stroke = Color.FromArgb("#A48BFF");

        if (sender == ConfirmEntry)
            ConfirmBorder.Stroke = Color.FromArgb("#A48BFF");
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        PasswordBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2D2D35")
            : Color.FromArgb("#E8E0F8");

        ConfirmBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2D2D35")
            : Color.FromArgb("#E8E0F8");
    }

    private async void OnInputFocused(object sender, FocusEventArgs e)
    {
        await ScrollEntryIntoViewAsync(sender as VisualElement);
    }

    private async Task ScrollEntryIntoViewAsync(VisualElement? target)
    {
        if (target == null || PageScrollView == null)
            return;

        await Task.Delay(250);

        try
        {
            await PageScrollView.ScrollToAsync(target, ScrollToPosition.Center, true);
        }
        catch
        {
        }
    }
}