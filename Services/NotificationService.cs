using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
#endif

namespace E_Book.Services
{
    public class NotificationService : INotificationService
    {
#if ANDROID
        private const string ChannelId = "ebook_general";
        private const string ChannelName = "E_Book Notifications";
        private const string ChannelDescription = "Reading reminders and library updates";
#endif

        public Task<bool> RequestPermissionAsync()
        {
#if ANDROID
            return RequestPermissionInternalAsync();
#else
            return Task.FromResult(true);
#endif
        }

        public Task ShowNowAsync(string title, string message)
        {
#if ANDROID
            return ShowNowInternalAsync(title, message);
#else
            return Task.CompletedTask;
#endif
        }

#if ANDROID
        private async Task<bool> RequestPermissionInternalAsync()
        {
            CreateNotificationChannel();

            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
                return true;

            var status = await Permissions.CheckStatusAsync<NotificationPermission>();
            if (status == PermissionStatus.Granted)
                return true;

            status = await Permissions.RequestAsync<NotificationPermission>();
            return status == PermissionStatus.Granted;
        }

        private async Task ShowNowInternalAsync(string title, string message)
        {
            bool granted = await RequestPermissionInternalAsync();
            if (!granted)
                return;

            var context = Platform.AppContext;

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle(title)
                .SetContentText(message)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(message))
                .SetPriority(NotificationCompat.PriorityDefault)
                .SetAutoCancel(true);

            NotificationManagerCompat.From(context)
                .Notify((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), builder.Build());
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = Platform.AppContext.GetSystemService(Context.NotificationService) as NotificationManager;
            if (manager == null)
                return;

            var existing = manager.GetNotificationChannel(ChannelId);
            if (existing != null)
                return;

            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.Default)
            {
                Description = ChannelDescription
            };

            manager.CreateNotificationChannel(channel);
        }
#endif
    }

#if ANDROID
    public class NotificationPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            OperatingSystem.IsAndroidVersionAtLeast(33)
                ? new[] { (Android.Manifest.Permission.PostNotifications, true) }
                : Array.Empty<(string, bool)>();
    }
#endif
}
