using E_Book.Data;

namespace E_Book.Pages;

public partial class ForgotPasswordPage : ContentPage
{
    private readonly Database _database = new();

    private bool _hasAnimatedIn;
    private bool _isLogoBreathing;
    private bool _isThemeAnimating;
    private bool _isNavigatingBack;

    private AppUser? _currentUser;

    public ForgotPasswordPage()
    {
        InitializeComponent();

        EmailEntry.Focused += OnEntryFocused;
        EmailEntry.Unfocused += OnEntryUnfocused;

        AnswerEntry.Focused += OnEntryFocused;
        AnswerEntry.Unfocused += OnEntryUnfocused;

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

            ForgotCard.Opacity = 0;
            ForgotCard.TranslationY = 32;
            ForgotCard.Scale = 0.98;

            await Task.WhenAll(
                LogoFrame.FadeTo(1, 280, Easing.CubicOut),
                LogoFrame.TranslateTo(0, -22, 280, Easing.CubicOut),
                LogoFrame.ScaleTo(1, 280, Easing.CubicOut)
            );

            await Task.WhenAll(
                ForgotCard.FadeTo(1, 380, Easing.CubicOut),
                ForgotCard.TranslateTo(0, 0, 380, Easing.SpringOut),
                ForgotCard.ScaleTo(1, 380, Easing.SpringOut)
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

            var fadeOutAnim = ForgotCard.FadeTo(0.82, 120, Easing.CubicOut);

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
                ForgotCard.FadeTo(1.00, 180, Easing.CubicInOut)
            );
        }
        finally
        {
            _isThemeAnimating = false;
        }
    }

    private async void OnEmailCompleted(object sender, EventArgs e)
    {
        await FindAccountAsync();
    }

    private async void OnAnswerCompleted(object sender, EventArgs e)
    {
        await VerifyAnswerAsync();
    }

    private async void OnFindAccountClicked(object sender, EventArgs e)
    {
        await FindAccountAsync();
    }

    private async void OnVerifyClicked(object sender, EventArgs e)
    {
        await VerifyAnswerAsync();
    }

    private async Task FindAccountAsync()
    {
        await FindAccountButton.ScaleTo(0.96, 70, Easing.CubicIn);
        await FindAccountButton.ScaleTo(1.00, 90, Easing.CubicOut);

        var email = EmailEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(email))
        {
            await DisplayAlert("Error", "Please enter your email.", "OK");
            return;
        }

        FindAccountButton.Text = "";
        FindAccountButton.IsEnabled = false;
        FindLoading.IsVisible = true;
        FindLoading.IsRunning = true;

        try
        {
            await Task.Delay(450);

            string normalizedEmail = E_Book.Services.UserSession.NormalizeUserId(email);
            var user = await _database.GetUserAsync(normalizedEmail);

            if (user == null || user.IsGuest)
            {
                await DisplayAlert("Not found", "No account was found for this email.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(user.SecurityQuestion) ||
                string.IsNullOrWhiteSpace(user.SecurityAnswer))
            {
                await DisplayAlert("Unavailable", "This account does not have a security question set.", "OK");
                return;
            }

            _currentUser = user;

            QuestionLabel.Text = user.SecurityQuestion;
            QuestionPanel.IsVisible = true;
            AnswerBorder.IsVisible = true;
            VerifyGrid.IsVisible = true;

            await QuestionPanel.FadeTo(1, 180, Easing.CubicOut);
            await ScrollEntryIntoViewAsync(AnswerEntry);

            AnswerEntry.Focus();
        }
        finally
        {
            FindLoading.IsRunning = false;
            FindLoading.IsVisible = false;
            FindAccountButton.Text = "Find Account";
            FindAccountButton.IsEnabled = true;
        }
    }

    private async Task VerifyAnswerAsync()
    {
        if (_currentUser == null)
        {
            await DisplayAlert("Error", "Please find your account first.", "OK");
            return;
        }

        await VerifyButton.ScaleTo(0.96, 70, Easing.CubicIn);
        await VerifyButton.ScaleTo(1.00, 90, Easing.CubicOut);

        var answer = AnswerEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(answer))
        {
            await DisplayAlert("Error", "Please enter your security answer.", "OK");
            return;
        }

        VerifyButton.Text = "";
        VerifyButton.IsEnabled = false;
        VerifyLoading.IsVisible = true;
        VerifyLoading.IsRunning = true;

        try
        {
            await Task.Delay(350);

            bool isCorrect = string.Equals(
                _currentUser.SecurityAnswer?.Trim(),
                answer,
                StringComparison.OrdinalIgnoreCase);

            if (!isCorrect)
            {
                await AnswerBorder.TranslateTo(-8, 0, 40);
                await AnswerBorder.TranslateTo(8, 0, 40);
                await AnswerBorder.TranslateTo(-5, 0, 40);
                await AnswerBorder.TranslateTo(5, 0, 40);
                await AnswerBorder.TranslateTo(0, 0, 40);

                await DisplayAlert("Verification failed", "Incorrect security answer.", "OK");
                return;
            }

            string targetEmail = Uri.EscapeDataString(_currentUser.UserId);
            await Shell.Current.GoToAsync($"{AppShell.RouteResetPassword}?email={targetEmail}");
        }
        finally
        {
            VerifyLoading.IsRunning = false;
            VerifyLoading.IsVisible = false;
            VerifyButton.Text = "Verify Answer";
            VerifyButton.IsEnabled = true;
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
                ForgotCard.FadeTo(0, 180, Easing.CubicIn),
                ForgotCard.TranslateTo(0, 26, 180, Easing.CubicIn),
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
        if (sender == EmailEntry)
            EmailBorder.Stroke = Color.FromArgb("#A48BFF");

        if (sender == AnswerEntry)
            AnswerBorder.Stroke = Color.FromArgb("#A48BFF");
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        EmailBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2D2D35")
            : Color.FromArgb("#E8E0F8");

        AnswerBorder.Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
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