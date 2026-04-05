namespace E_Book.Services
{
    public interface INotificationService
    {
        Task<bool> RequestPermissionAsync();
        Task ShowNowAsync(string title, string message);
    }
}