using E_Book.Services;
using Microsoft.Maui.ApplicationModel;

namespace E_Book.Pages
{
    public partial class SettingsHomePage : ContentPage
    {
        private bool _entrancePlayed;
        private bool _isRowAnimating;

        public SettingsHomePage()
        {
            InitializeComponent();

            EditProfileButton.Pressed += async (_, __) => await PressDown(EditProfileButton);
            EditProfileButton.Released += async (_, __) => await PressUp(EditProfileButton);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            BindProfileInfo();
            await RunEntranceFlagship();
        }

        private void BindProfileInfo()
        {
            var displayName = string.IsNullOrWhiteSpace(UserSession.DisplayName)
                ? "Guest"
                : UserSession.DisplayName.Trim();

            NameLabel.Text = displayName;

            if (UserSession.IsGuest)
            {
                EmailLabel.Text = "Guest account";
            }
            else
            {
                EmailLabel.Text = string.IsNullOrWhiteSpace(UserSession.UserId)
                    ? "Signed in"
                    : UserSession.UserId;
            }

            AvatarLetterLabel.Text = GetAvatarLetter(displayName);
        }

        private static string GetAvatarLetter(string? name)
        {
            var text = (name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text))
                return "U";

            return text.Substring(0, 1).ToUpperInvariant();
        }

        // =========================
        // Entrance Animation
        // =========================
        private async Task RunEntranceFlagship()
        {
            if (_entrancePlayed) return;
            _entrancePlayed = true;

            TitleLabel.Opacity = 0;
            TitleLabel.TranslationY = 8;

            HeaderSubTitle.Opacity = 0;
            HeaderSubTitle.TranslationY = 8;

            ProfileCard.Opacity = 0;
            ProfileCard.TranslationY = 14;
            ProfileCard.Scale = 0.995;

            SectionLabel.Opacity = 0;
            SectionLabel.TranslationY = 8;

            MenuCard.Opacity = 0;
            MenuCard.TranslationY = 14;
            MenuCard.Scale = 0.995;

            AppearanceRow.Opacity = 0;
            AppearanceRow.TranslationY = 8;

            HelpRow.Opacity = 0;
            HelpRow.TranslationY = 8;

            FooterLabel.Opacity = 0;
            FooterLabel.TranslationY = 6;

            await Task.WhenAll(
                TitleLabel.FadeTo(1, 180, Easing.CubicOut),
                TitleLabel.TranslateTo(0, 0, 220, Easing.CubicOut),
                HeaderSubTitle.FadeTo(1, 200, Easing.CubicOut),
                HeaderSubTitle.TranslateTo(0, 0, 240, Easing.CubicOut)
            );

            await Task.Delay(30);

            await Task.WhenAll(
                ProfileCard.FadeTo(1, 220, Easing.CubicOut),
                ProfileCard.TranslateTo(0, 0, 260, Easing.CubicOut),
                ProfileCard.ScaleTo(1.0, 240, Easing.CubicOut)
            );

            await Task.Delay(24);

            await Task.WhenAll(
                SectionLabel.FadeTo(1, 160, Easing.CubicOut),
                SectionLabel.TranslateTo(0, 0, 200, Easing.CubicOut)
            );

            await Task.Delay(24);

            await Task.WhenAll(
                MenuCard.FadeTo(1, 220, Easing.CubicOut),
                MenuCard.TranslateTo(0, 0, 260, Easing.CubicOut),
                MenuCard.ScaleTo(1.0, 240, Easing.CubicOut)
            );

            await Task.Delay(34);

            await Task.WhenAll(
                AppearanceRow.FadeTo(1, 170, Easing.CubicOut),
                AppearanceRow.TranslateTo(0, 0, 220, Easing.CubicOut)
            );

            await Task.Delay(28);

            await Task.WhenAll(
                HelpRow.FadeTo(1, 170, Easing.CubicOut),
                HelpRow.TranslateTo(0, 0, 220, Easing.CubicOut)
            );

            await Task.Delay(60);

            await Task.WhenAll(
                FooterLabel.FadeTo(1, 200, Easing.CubicOut),
                FooterLabel.TranslateTo(0, 0, 220, Easing.CubicOut)
            );
        }

        // =========================
        // Button Feedback
        // =========================
        private static async Task PressDown(VisualElement view)
        {
            if (view == null) return;

            await Task.WhenAll(
                view.ScaleTo(0.975, 70, Easing.CubicOut),
                view.FadeTo(0.95, 70, Easing.CubicOut)
            );
        }

        private static async Task PressUp(VisualElement view)
        {
            if (view == null) return;

            await Task.WhenAll(
                view.ScaleTo(1.0, 130, Easing.CubicOut),
                view.FadeTo(1.0, 130, Easing.CubicOut)
            );
        }

        // =========================
        // Haptic
        // =========================
        private static void TryHaptic()
        {
            try
            {
                HapticFeedback.Default.Perform(HapticFeedbackType.Click);
            }
            catch
            {
            }
        }

        // =========================
        // Soft Highlight
        // =========================
        private async Task HighlightAsync(Border ripple)
        {
            if (ripple == null) return;

            ripple.Opacity = 0;
            ripple.Scale = 0.96;

            await Task.WhenAll(
                ripple.FadeTo(0.32, 70, Easing.CubicOut),
                ripple.ScaleTo(1.0, 110, Easing.CubicOut)
            );

            await Task.WhenAll(
                ripple.FadeTo(0, 180, Easing.CubicOut),
                ripple.ScaleTo(1.01, 180, Easing.CubicOut)
            );
        }

        // =========================
        // Bubble Micro Pop
        // =========================
        private static async Task BubblePopAsync(VisualElement bubble)
        {
            if (bubble == null) return;

            await bubble.ScaleTo(1.045, 90, Easing.CubicOut);
            await bubble.ScaleTo(1.0, 140, Easing.CubicOut);
        }

        // =========================
        // Row Tap Animation
        // =========================
        private async Task RowTapAsync(
            VisualElement row,
            Border ripple,
            VisualElement chevron,
            VisualElement bubble)
        {
            if (_isRowAnimating) return;
            _isRowAnimating = true;

            TryHaptic();

            try
            {
                var highlightTask = HighlightAsync(ripple);

                await Task.WhenAll(
                    row.ScaleTo(0.994, 65, Easing.CubicOut),
                    chevron.TranslateTo(2, 0, 100, Easing.CubicOut)
                );

                await Task.WhenAll(
                    row.ScaleTo(1.0, 120, Easing.CubicOut),
                    chevron.TranslateTo(0, 0, 150, Easing.CubicOut)
                );

                await highlightTask;
                await BubblePopAsync(bubble);
            }
            finally
            {
                _isRowAnimating = false;
            }
        }

        // =========================
        // Navigation
        // =========================
        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            TryHaptic();
            await Shell.Current.GoToAsync("edit-profile");
        }

        private async void OnAppearanceTapped(object sender, TappedEventArgs e)
        {
            await RowTapAsync(
                AppearanceRow,
                AppearanceRipple,
                AppearanceChevron,
                AppearanceBubble);

            await Shell.Current.GoToAsync("appearance");
        }

        private async void OnHelpTapped(object sender, TappedEventArgs e)
        {
            await RowTapAsync(
                HelpRow,
                HelpRipple,
                HelpChevron,
                HelpBubble);

            await Shell.Current.GoToAsync("help");
        }
    }
}