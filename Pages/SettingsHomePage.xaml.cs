using E_Book.Services;
using Microsoft.Maui.ApplicationModel;

namespace E_Book.Pages
{
    public partial class SettingsHomePage : ContentPage
    {
        private bool _entrancePlayed;

        public SettingsHomePage()
        {
            InitializeComponent();

            EditProfileButton.Pressed += async (_, __) => await PressDown(EditProfileButton);
            EditProfileButton.Released += async (_, __) => await PressUp(EditProfileButton);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            NameLabel.Text = UserSession.DisplayName;
            EmailLabel.Text = UserSession.IsGuest ? "" : UserSession.UserId;
            EmailLabel.IsVisible = !UserSession.IsGuest;

            await RunEntranceFlagship();
        }

        // =========================
        // ULTRA Entrance
        // =========================
        private async Task RunEntranceFlagship()
        {
            if (_entrancePlayed) return;
            _entrancePlayed = true;

            TitleLabel.Opacity = 0; TitleLabel.TranslationY = 10;
            ProfileCard.Opacity = 0; ProfileCard.TranslationY = 18;
            MenuCard.Opacity = 0; MenuCard.TranslationY = 18;
            AppearanceRow.Opacity = 0; AppearanceRow.TranslationY = 14;
            HelpRow.Opacity = 0; HelpRow.TranslationY = 14;

            await Task.WhenAll(
                TitleLabel.FadeTo(1, 180, Easing.CubicOut),
                TitleLabel.TranslateTo(0, 0, 220, Easing.CubicOut)
            );

            await Task.Delay(40);

            await Task.WhenAll(
                ProfileCard.FadeTo(1, 240, Easing.CubicOut),
                ProfileCard.TranslateTo(0, 0, 300, Easing.CubicOut)
            );

            await Task.Delay(50);

            await Task.WhenAll(
                MenuCard.FadeTo(1, 240, Easing.CubicOut),
                MenuCard.TranslateTo(0, 0, 300, Easing.CubicOut)
            );

            await Task.Delay(60);

            await Task.WhenAll(
                AppearanceRow.FadeTo(1, 200, Easing.CubicOut),
                AppearanceRow.TranslateTo(0, 0, 260, Easing.CubicOut)
            );

            await Task.Delay(50);

            await Task.WhenAll(
                HelpRow.FadeTo(1, 200, Easing.CubicOut),
                HelpRow.TranslateTo(0, 0, 260, Easing.CubicOut)
            );
        }

        // =========================
        // Button feedback
        // =========================
        private static async Task PressDown(VisualElement v)
        {
            await Task.WhenAll(
                v.ScaleTo(0.965, 70, Easing.CubicOut),
                v.FadeTo(0.93, 70, Easing.CubicOut)
            );
        }

        private static async Task PressUp(VisualElement v)
        {
            await Task.WhenAll(
                v.ScaleTo(1.0, 140, Easing.CubicOut),
                v.FadeTo(1.0, 140, Easing.CubicOut)
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
            catch { }
        }

        // =========================
        // Ripple
        // =========================
        private async Task RippleAsync(Border ripple)
        {
            ripple.Opacity = 0;
            ripple.Scale = 0.85;

            await Task.WhenAll(
                ripple.FadeTo(0.95, 90, Easing.CubicOut),
                ripple.ScaleTo(1.02, 140, Easing.CubicOut)
            );

            await Task.WhenAll(
                ripple.FadeTo(0, 200, Easing.CubicOut),
                ripple.ScaleTo(1.08, 200, Easing.CubicOut)
            );
        }

        // =========================
        // ICON POP + ROTATE
        // =========================
        private async Task IconPopAsync(VisualElement bubble, VisualElement icon)
        {
            if (bubble == null || icon == null) return;

            // bubble pop
            var pop1 = bubble.ScaleTo(1.12, 120, Easing.CubicOut);
            var pop2 = bubble.ScaleTo(1.0, 180, Easing.CubicOut);

            // icon micro rotate
            var rotate1 = icon.RotateTo(6, 120, Easing.CubicOut);
            var rotate2 = icon.RotateTo(0, 180, Easing.CubicOut);

            await Task.WhenAll(pop1, rotate1);
            await Task.WhenAll(pop2, rotate2);
        }

        // =========================
        // Row tap master animation
        // =========================
        private async Task RowTapAsync(
            VisualElement row,
            Border ripple,
            VisualElement chevron,
            VisualElement bubble,
            VisualElement icon)
        {
            TryHaptic();

            var rippleTask = RippleAsync(ripple);

            // micro lift illusion
            var lift = row.TranslateTo(0, -1, 80, Easing.CubicOut);
            var shrink = row.ScaleTo(0.988, 70, Easing.CubicOut);

            // chevron micro slide
            var slide = chevron.TranslateTo(3, 0, 120, Easing.CubicOut);

            await Task.WhenAll(lift, shrink);

            await row.ScaleTo(1.0, 140, Easing.CubicOut);
            await row.TranslateTo(0, 0, 140, Easing.CubicOut);
            await chevron.TranslateTo(0, 0, 180, Easing.CubicOut);

            await rippleTask;

            // icon pop after ripple
            await IconPopAsync(bubble, icon);
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
                AppearanceBubble,
                AppearanceIcon);

            await Shell.Current.GoToAsync("appearance");
        }

        private async void OnHelpTapped(object sender, TappedEventArgs e)
        {
            await RowTapAsync(
                HelpRow,
                HelpRipple,
                HelpChevron,
                HelpBubble,
                HelpIcon);

            await Shell.Current.GoToAsync("help");
        }
    }
}