using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class ThemeScheduler
    {
        public enum ThemeMode
        {
            Auto = 0,
            Light = 1,
            Dark = 2
        }

        private const string KeyMode = "theme_mode";                   // int
        private const string KeyLightStart = "auto_theme_light_start"; // "HH:mm"
        private const string KeyDarkStart = "auto_theme_dark_start";  // "HH:mm"

        private static bool _timerStarted;

        public static ThemeMode Mode
        {
            get => (ThemeMode)Preferences.Get(KeyMode, (int)ThemeMode.Light);
            set => Preferences.Set(KeyMode, (int)value);
        }

        public static TimeSpan LightStart
        {
            get => ParseTime(Preferences.Get(KeyLightStart, "06:00"), new TimeSpan(6, 0, 0));
            set => Preferences.Set(KeyLightStart, value.ToString(@"hh\:mm"));
        }

        public static TimeSpan DarkStart
        {
            get => ParseTime(Preferences.Get(KeyDarkStart, "22:00"), new TimeSpan(22, 0, 0));
            set => Preferences.Set(KeyDarkStart, value.ToString(@"hh\:mm"));
        }

        public static void StartTimer()
        {
            if (_timerStarted) return;
            _timerStarted = true;

            Device.StartTimer(TimeSpan.FromMinutes(1), () =>
            {
                if (Mode == ThemeMode.Auto)
                    ApplyNow();

                return true;
            });
        }

        public static void ApplyNow()
        {
            var theme = ResolveTheme(DateTime.Now.TimeOfDay);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var app = Application.Current;
                if (app == null) return;

                if (app.UserAppTheme != theme)
                    app.UserAppTheme = theme;
            });
        }

        public static AppTheme ResolveTheme(TimeSpan now)
        {
            return Mode switch
            {
                ThemeMode.Dark => AppTheme.Dark,
                ThemeMode.Light => AppTheme.Light,
                _ => GetThemeForTime(now, LightStart, DarkStart),
            };
        }

        /// <summary>
        /// Light: [lightStart, darkStart) ; Dark: else (自动跨天)
        /// 例：lightStart=06:00 darkStart=22:00
        /// Dark: 22:00-24:00 + 00:00-06:00
        /// </summary>
        public static AppTheme GetThemeForTime(TimeSpan now, TimeSpan lightStart, TimeSpan darkStart)
        {
            bool isLight;
            if (lightStart < darkStart)
                isLight = now >= lightStart && now < darkStart;
            else
                isLight = now >= lightStart || now < darkStart;

            return isLight ? AppTheme.Light : AppTheme.Dark;
        }

        private static TimeSpan ParseTime(string s, TimeSpan fallback)
        {
            if (TimeSpan.TryParseExact(s, @"hh\:mm", null, out var t))
                return t;
            return fallback;
        }
    }
}