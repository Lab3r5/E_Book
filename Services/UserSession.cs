using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class UserSession
    {
        private const string KeyUserId = "session_user_id";
        private const string KeyIsGuest = "session_is_guest";
        private const string KeyDisplayName = "session_display_name";
        private const string KeyQuickLoginRemaining = "session_quick_login_remaining";

        public const string GuestUserId = "guest";

        public static bool HasSession =>
            Preferences.ContainsKey(KeyUserId) ||
            Preferences.ContainsKey(KeyIsGuest) ||
            Preferences.ContainsKey(KeyDisplayName);

        public static string UserId => Preferences.Get(KeyUserId, GuestUserId);

        public static string UserEmail => Preferences.Get(KeyUserId, GuestUserId);

        public static bool IsGuest => Preferences.Get(KeyIsGuest, true);

        public static string DisplayName => Preferences.Get(KeyDisplayName, "Guest");

        public static bool IsLoggedIn => HasSession && !IsGuest;

        public static int QuickLoginRemaining => Preferences.Get(KeyQuickLoginRemaining, 0);

        public static bool CanQuickLogin =>
            !IsGuest &&
            !string.IsNullOrWhiteSpace(UserId) &&
            QuickLoginRemaining > 0;

        public static void SetGuest()
        {
            Preferences.Set(KeyUserId, GuestUserId);
            Preferences.Set(KeyIsGuest, true);
            Preferences.Set(KeyDisplayName, "Guest");
            Preferences.Set(KeyQuickLoginRemaining, 0);
        }

        public static void SetUser(string userId, string displayName)
        {
            Preferences.Set(KeyUserId, userId);
            Preferences.Set(KeyIsGuest, false);
            Preferences.Set(KeyDisplayName, displayName);
        }

        public static void Logout()
        {
            Preferences.Remove(KeyUserId);
            Preferences.Remove(KeyIsGuest);
            Preferences.Remove(KeyDisplayName);
            Preferences.Set(KeyQuickLoginRemaining, 0);
        }

        public static void LogoutKeepIdentity()
        {
            Preferences.Set(KeyIsGuest, false);
        }

        public static void UpdateDisplayName(string displayName)
        {
            Preferences.Set(KeyDisplayName, displayName);
        }

        public static void SetQuickLoginCount(int count)
        {
            if (count < 1 || count > 5)
                throw new ArgumentOutOfRangeException(nameof(count), "Quick login count must be between 1 and 5.");

            Preferences.Set(KeyQuickLoginRemaining, count);
        }

        public static void ClearQuickLoginCount()
        {
            Preferences.Set(KeyQuickLoginRemaining, 0);
        }

        public static bool TryConsumeQuickLogin()
        {
            if (!CanQuickLogin)
                return false;

            int remaining = QuickLoginRemaining - 1;
            Preferences.Set(KeyQuickLoginRemaining, Math.Max(remaining, 0));
            return true;
        }
    }
}