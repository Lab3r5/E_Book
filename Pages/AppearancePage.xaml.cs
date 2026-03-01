using System.Windows.Input;
using E_Book.Data;
using E_Book.Services;

namespace E_Book.Pages
{
    public partial class AppearancePage : ContentPage
    {
        private readonly Database db = new();

        public ICommand BackCommand { get; }

        private int _currentIndex = -1;
        private bool _layoutReady = false;

        public AppearancePage()
        {
            InitializeComponent();

            BackCommand = new Command(async () =>
            {
                try { await Shell.Current.GoToAsync(".."); return; } catch { }
                await Navigation.PopModalAsync();
            });

            BindingContext = this;

            SizeChanged += (_, __) =>
            {
                if (_layoutReady) return;
                if (SegmentHost.Width <= 0) return;

                _layoutReady = true;
                _ = SyncFromStoredMode(animated: false);
            };
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var user = await db.GetUserSettingsAsync();
            DeviceDisplay.KeepScreenOn = user.KeepScreenOn;
            KeepScreenOnSwitch.IsToggled = user.KeepScreenOn;

            LightStartPicker.Time = ThemeScheduler.LightStart;
            DarkStartPicker.Time = ThemeScheduler.DarkStart;

            ThemeScheduler.StartTimer();
            ThemeScheduler.ApplyNow();

            _ = SyncFromStoredMode(animated: false);
        }

        private Color GetUnselectedTextColor()
        {
            return (Application.Current?.UserAppTheme == AppTheme.Dark)
                ? Colors.White
                : Color.FromArgb("#221551");
        }

        private void UpdateSegmentTextColors(int index)
        {
            var unselected = GetUnselectedTextColor();

            AutoLabel.TextColor = index == 0 ? Colors.White : unselected;
            LightLabel.TextColor = index == 1 ? Colors.White : unselected;
            DarkLabel.TextColor = index == 2 ? Colors.White : unselected;
        }

        private int ModeToIndex(ThemeScheduler.ThemeMode mode) => mode switch
        {
            ThemeScheduler.ThemeMode.Auto => 0,
            ThemeScheduler.ThemeMode.Light => 1,
            ThemeScheduler.ThemeMode.Dark => 2,
            _ => 0
        };

        private async Task MoveSegmentAsync(int index, bool animated = true)
        {
            if (SegmentHost.Width <= 0) return;

            double cellWidth = SegmentHost.Width / 3.0;
            double targetX = cellWidth * index;

            if (!animated)
            {
                SegmentHighlight.TranslationX = targetX;
                _currentIndex = index;
                UpdateSegmentTextColors(index);
                return;
            }

            if (_currentIndex == index) return;

            await Task.WhenAll(
                SegmentHighlight.TranslateTo(targetX, 0, 240, Easing.CubicOut),
                SegmentHighlight.FadeTo(0.92, 120, Easing.CubicOut)
            );
            await SegmentHighlight.FadeTo(1.0, 120, Easing.CubicOut);

            _currentIndex = index;
            UpdateSegmentTextColors(index);
        }

        private async Task SyncFromStoredMode(bool animated)
        {
            if (SegmentHost.Width <= 0) return;

            int idx = ModeToIndex(ThemeScheduler.Mode);

            AutoThemeSettings.IsVisible = ThemeScheduler.Mode == ThemeScheduler.ThemeMode.Auto;

            await MoveSegmentAsync(idx, animated);
            UpdateSegmentTextColors(idx);
        }

        private async void OnAutoTapped(object sender, TappedEventArgs e)
        {
            ThemeScheduler.Mode = ThemeScheduler.ThemeMode.Auto;
            AutoThemeSettings.IsVisible = true;

            await MoveSegmentAsync(0, animated: true);
            ThemeScheduler.StartTimer();
            ThemeScheduler.ApplyNow();

            UpdateSegmentTextColors(0);
        }

        private async void OnLightTapped(object sender, TappedEventArgs e)
        {
            ThemeScheduler.Mode = ThemeScheduler.ThemeMode.Light;
            AutoThemeSettings.IsVisible = false;

            await MoveSegmentAsync(1, animated: true);
            ThemeScheduler.ApplyNow();

            UpdateSegmentTextColors(1);

            // 兼容旧 DB（可删）
            var r = await db.GetReadingSettingsAsync();
            await db.SaveReadingSettingsAsync(r.FontSize, "Light");
        }

        private async void OnDarkTapped(object sender, TappedEventArgs e)
        {
            ThemeScheduler.Mode = ThemeScheduler.ThemeMode.Dark;
            AutoThemeSettings.IsVisible = false;

            await MoveSegmentAsync(2, animated: true);
            ThemeScheduler.ApplyNow();

            UpdateSegmentTextColors(2);

            // 兼容旧 DB（可删）
            var r = await db.GetReadingSettingsAsync();
            await db.SaveReadingSettingsAsync(r.FontSize, "Dark");
        }

        private void OnAutoThemeTimeChanged(object sender, TimeChangedEventArgs e)
        {
            ThemeScheduler.LightStart = LightStartPicker.Time;
            ThemeScheduler.DarkStart = DarkStartPicker.Time;

            if (ThemeScheduler.Mode == ThemeScheduler.ThemeMode.Auto)
                ThemeScheduler.ApplyNow();
        }

        private async void OnKeepScreenOnToggled(object sender, ToggledEventArgs e)
        {
            DeviceDisplay.KeepScreenOn = e.Value;

            var s = await db.GetUserSettingsAsync();
            await db.SaveSettingsAsync(
                keepScreenOn: e.Value,
                startupPassword: s.StartupPasswordEnabled,
                exitLock: s.ExitLockEnabled
            );
        }
    }
}