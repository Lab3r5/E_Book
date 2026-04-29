using E_Book.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class SettingsHomePage : ContentPage
    {
        private bool _entrancePlayed;
        private bool _isRowAnimating;

        public SettingsHomePage()
        {
            InitializeComponent();

            EditProfileButton.Pressed += async (_, __) => await UIAnimationService.PressAsync(EditProfileButton);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            BindProfileInfo();

            if (!_entrancePlayed)
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

        private async void OnNotificationsToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(NotificationPrefs.NotificationsEnabled, e.Value);

            if (e.Value)
            {
                try
                {
                    var notificationService = Application.Current?.Handler?.MauiContext?.Services.GetService<INotificationService>();
                    if (notificationService != null)
                        await notificationService.RequestPermissionAsync();
                }
                catch
                {
                }
            }
        }

        private async void OnNotificationsTapped(object sender, TappedEventArgs e)
        {
            await RowTapAsync(
                NotificationsRow,
                NotificationsRipple,
                NotificationsChevron,
                NotificationsBubble);

            await Shell.Current.GoToAsync("notifications");
        }

        private void OnImportAlertToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(NotificationPrefs.ImportAlertEnabled, e.Value);
        }

        private void OnContinueReadingToggled(object sender, ToggledEventArgs e)
        {
            Preferences.Set(NotificationPrefs.ContinueReadingEnabled, e.Value);
        }

        private static string GetAvatarLetter(string? name)
        {
            var text = (name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(text))
                return "U";

            return text.Substring(0, 1).ToUpperInvariant();
        }

        private async Task RunEntranceFlagship()
        {
            if (_entrancePlayed) return;
            _entrancePlayed = true;

            await UIAnimationService.FadeSlideInAsync(TitleLabel, 8, 180);
            await UIAnimationService.FadeSlideInAsync(HeaderSubTitle, 8, 200);
            await UIAnimationService.FadeScaleCardInAsync(ProfileCard, 14, 0.995, 240, 20);
            await UIAnimationService.FadeSlideInAsync(SectionLabel, 8, 180, 20);
            await UIAnimationService.FadeScaleCardInAsync(MenuCard, 14, 0.995, 240, 10);
            await UIAnimationService.FadeSlideInAsync(AppearanceRow, 8, 190, 10);
            await UIAnimationService.FadeSlideInAsync(HelpRow, 8, 190, 20);
            await UIAnimationService.FadeSlideInAsync(NotificationsRow, 8, 190, 30);
            await UIAnimationService.FadeSlideInAsync(FooterLabel, 6, 190, 20);
        }

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
                var highlightTask = UIAnimationService.HighlightAsync(ripple, 0.32);

                await Task.WhenAll(
                    row.ScaleTo(0.994, 65, Easing.CubicOut),
                    chevron.TranslateTo(2, 0, 100, Easing.CubicOut)
                );

                await Task.WhenAll(
                    row.ScaleTo(1.0, 120, Easing.CubicOut),
                    chevron.TranslateTo(0, 0, 150, Easing.CubicOut)
                );

                await highlightTask;
                await UIAnimationService.PopAsync(bubble);
            }
            finally
            {
                _isRowAnimating = false;
            }
        }

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