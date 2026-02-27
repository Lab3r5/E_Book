using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class UserSession
    {
        private const string KeyUserId = "session_user_id";
        private const string KeyIsGuest = "session_is_guest";
        private const string KeyDisplayName = "session_display_name";

        public const string GuestUserId = "guest";

        public static string UserId => Preferences.Get(KeyUserId, GuestUserId);
        public static bool IsGuest => Preferences.Get(KeyIsGuest, true);
        public static string DisplayName => Preferences.Get(KeyDisplayName, "Guest");

        public static void SetGuest()
        {
            Preferences.Set(KeyUserId, GuestUserId);
            Preferences.Set(KeyIsGuest, true);
            Preferences.Set(KeyDisplayName, "Guest");
        }

        public static void SetUser(string userId, string displayName)
        {
            Preferences.Set(KeyUserId, userId);
            Preferences.Set(KeyIsGuest, false);
            Preferences.Set(KeyDisplayName, displayName);
        }

        public static void Logout()
        {
            // 注意：不删除Guest数据，只是回到Guest session 或回到Login
            Preferences.Remove(KeyUserId);
            Preferences.Remove(KeyIsGuest);
            Preferences.Remove(KeyDisplayName);
        }

        public static void UpdateDisplayName(string displayName)
        {
            Preferences.Set(KeyDisplayName, displayName);
        }
    }
}