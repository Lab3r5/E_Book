using SQLite;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using E_Book.Services;

namespace E_Book.Data
{
    public class Database
    {
        private const string NewDbName = "E_Book.db";
        private const string OldDbName = "userData.db";
        private const string MigrationFlag = "db_migrated_to_multi_user_v1";

        private static readonly string newDbPath =
            Path.Combine(FileSystem.AppDataDirectory, NewDbName);

        private static readonly string oldDbPath =
            Path.Combine(FileSystem.AppDataDirectory, OldDbName);

        private readonly SQLiteAsyncConnection database;
        private readonly Task initializeTask;

        public Database()
        {
            MigrateOldDbIfNeeded();
            database = new SQLiteAsyncConnection(newDbPath);
            initializeTask = InitializeDatabaseAsync();
        }

        private static void MigrateOldDbIfNeeded()
        {
            try
            {
                if (File.Exists(oldDbPath) && !File.Exists(newDbPath))
                    File.Copy(oldDbPath, newDbPath);
            }
            catch
            {
            }
        }

        private Task EnsureInitializedAsync() => initializeTask;

        private async Task InitializeDatabaseAsync()
        {
            await database.CreateTableAsync<AppUser>();
            await database.CreateTableAsync<UserSettings>();
            await database.CreateTableAsync<ReadingSettings>();
            await database.CreateTableAsync<ReadingProgress>();

            await EnsureGuestUserExistsAsync();
            await MigrateLegacySingleUserDataToGuestAsync();
            await EnsureDefaultRowsForUserAsync(UserSession.UserId);
        }

        private async Task EnsureGuestUserExistsAsync()
        {
            var guest = await database.Table<AppUser>()
                                      .FirstOrDefaultAsync(x => x.UserId == UserSession.GuestUserId);

            if (guest == null)
            {
                await database.InsertAsync(new AppUser
                {
                    UserId = UserSession.GuestUserId,
                    DisplayName = "Guest",
                    Password = string.Empty,
                    IsGuest = true
                });
            }
        }

        private async Task EnsureDefaultRowsForUserAsync(string? userId)
        {
            string normalizedUserId = UserSession.NormalizeUserId(userId);

            var settings = await database.Table<UserSettings>()
                                         .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);

            if (settings == null)
            {
                await database.InsertAsync(new UserSettings
                {
                    UserId = normalizedUserId
                });
            }

            var reading = await database.Table<ReadingSettings>()
                                        .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);

            if (reading == null)
            {
                await database.InsertAsync(new ReadingSettings
                {
                    UserId = normalizedUserId
                });
            }
        }

        private async Task<bool> TableExistsAsync(string tableName)
        {
            var result = await database.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?",
                tableName);

            return result > 0;
        }

        private async Task MigrateLegacySingleUserDataToGuestAsync()
        {
            if (Preferences.Get(MigrationFlag, false))
                return;

            try
            {
                string guestId = UserSession.GuestUserId;

                if (await TableExistsAsync("UserSettings"))
                {
                    var oldSettings = await database.Table<LegacyUserSettings>().FirstOrDefaultAsync();
                    if (oldSettings != null)
                    {
                        var existing = await database.Table<UserSettings>()
                                                     .FirstOrDefaultAsync(x => x.UserId == guestId);

                        if (existing == null)
                        {
                            await database.InsertAsync(new UserSettings
                            {
                                UserId = guestId,
                                StartupPasswordEnabled = oldSettings.StartupPasswordEnabled,
                                ExitLockEnabled = oldSettings.ExitLockEnabled,
                                KeepScreenOn = oldSettings.KeepScreenOn
                            });
                        }
                        else
                        {
                            existing.StartupPasswordEnabled = oldSettings.StartupPasswordEnabled;
                            existing.ExitLockEnabled = oldSettings.ExitLockEnabled;
                            existing.KeepScreenOn = oldSettings.KeepScreenOn;
                            await database.UpdateAsync(existing);
                        }
                    }
                }

                if (await TableExistsAsync("ReadingSettings"))
                {
                    var oldReading = await database.Table<LegacyReadingSettings>().FirstOrDefaultAsync();
                    if (oldReading != null)
                    {
                        var existing = await database.Table<ReadingSettings>()
                                                     .FirstOrDefaultAsync(x => x.UserId == guestId);

                        if (existing == null)
                        {
                            await database.InsertAsync(new ReadingSettings
                            {
                                UserId = guestId,
                                FontSize = oldReading.FontSize,
                                BackgroundColor = oldReading.BackgroundColor,
                                LineSpacing = oldReading.LineSpacing
                            });
                        }
                        else
                        {
                            existing.FontSize = oldReading.FontSize;
                            existing.BackgroundColor = oldReading.BackgroundColor;
                            existing.LineSpacing = oldReading.LineSpacing;
                            await database.UpdateAsync(existing);
                        }
                    }
                }

                if (await TableExistsAsync("ReadingProgress"))
                {
                    var oldProgressList = await database.Table<LegacyReadingProgress>().ToListAsync();

                    foreach (var oldItem in oldProgressList.Where(x => !string.IsNullOrWhiteSpace(x.FileName)))
                    {
                        string key = BuildProgressKey(guestId, oldItem.FileName);

                        var existing = await database.Table<ReadingProgress>()
                                                     .FirstOrDefaultAsync(x => x.ProgressKey == key);

                        if (existing == null)
                        {
                            await database.InsertAsync(new ReadingProgress
                            {
                                ProgressKey = key,
                                UserId = guestId,
                                FileName = oldItem.FileName,
                                LastPage = oldItem.LastPage,
                                TotalPages = oldItem.TotalPages
                            });
                        }
                    }
                }

                Preferences.Set(MigrationFlag, true);
            }
            catch
            {
            }
        }

        private static string BuildProgressKey(string userId, string fileName)
        {
            return $"{UserSession.NormalizeUserId(userId)}|{fileName}";
        }

        // ================= User Account =================

        public async Task<bool> UserExistsAsync(string userId)
        {
            await EnsureInitializedAsync();

            string normalizedUserId = UserSession.NormalizeUserId(userId);

            var user = await database.Table<AppUser>()
                                     .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);

            return user != null;
        }

        public async Task<(bool Success, string Message)> RegisterUserAsync(string userId, string displayName, string password)
        {
            await EnsureInitializedAsync();

            string normalizedUserId = UserSession.NormalizeUserId(userId);

            if (normalizedUserId == UserSession.GuestUserId)
                return (false, "This user id is reserved.");

            var existing = await database.Table<AppUser>()
                                         .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);

            if (existing != null)
                return (false, "This email is already registered.");

            var user = new AppUser
            {
                UserId = normalizedUserId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedUserId : displayName.Trim(),
                Password = password ?? string.Empty,
                IsGuest = false
            };

            await database.InsertAsync(user);
            await EnsureDefaultRowsForUserAsync(normalizedUserId);

            return (true, "Registered successfully.");
        }

        public async Task<AppUser?> ValidateUserAsync(string userId, string password)
        {
            await EnsureInitializedAsync();

            string normalizedUserId = UserSession.NormalizeUserId(userId);

            var user = await database.Table<AppUser>()
                                     .FirstOrDefaultAsync(x => x.UserId == normalizedUserId && !x.IsGuest);

            if (user == null)
                return null;

            return string.Equals(user.Password, password ?? string.Empty, StringComparison.Ordinal)
                ? user
                : null;
        }

        public async Task<AppUser?> GetUserAsync(string userId)
        {
            await EnsureInitializedAsync();

            string normalizedUserId = UserSession.NormalizeUserId(userId);

            return await database.Table<AppUser>()
                                 .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);
        }

        public async Task UpdateUserDisplayNameAsync(string userId, string displayName)
        {
            await EnsureInitializedAsync();

            string normalizedUserId = UserSession.NormalizeUserId(userId);

            var user = await database.Table<AppUser>()
                                     .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);

            if (user == null)
                return;

            user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.DisplayName : displayName.Trim();
            await database.UpdateAsync(user);
        }

        public async Task UpdatePasswordAsync(string userId, string password)
        {
            await EnsureInitializedAsync();

            string normalizedUserId = UserSession.NormalizeUserId(userId);

            var user = await database.Table<AppUser>()
                                     .FirstOrDefaultAsync(x => x.UserId == normalizedUserId);

            if (user == null)
                return;

            user.Password = password ?? string.Empty;
            await database.UpdateAsync(user);
        }

        // ================= Password (compatibility) =================

        public async Task SavePasswordAsync(string password)
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;

            var user = await database.Table<AppUser>()
                                     .FirstOrDefaultAsync(x => x.UserId == userId);

            if (user == null)
            {
                user = new AppUser
                {
                    UserId = userId,
                    DisplayName = UserSession.DisplayName,
                    Password = password ?? string.Empty,
                    IsGuest = UserSession.IsGuest
                };

                await database.InsertAsync(user);
            }
            else
            {
                user.Password = password ?? string.Empty;
                await database.UpdateAsync(user);
            }
        }

        public async Task<string?> GetPasswordAsync()
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;

            var user = await database.Table<AppUser>()
                                     .FirstOrDefaultAsync(x => x.UserId == userId);

            return user?.Password;
        }

        // ================= Settings =================

        public async Task SaveSettingsAsync(bool keepScreenOn, bool startupPassword, bool exitLock)
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;

            var existingSettings = await database.Table<UserSettings>()
                                                 .FirstOrDefaultAsync(x => x.UserId == userId);

            if (existingSettings != null)
            {
                existingSettings.KeepScreenOn = keepScreenOn;
                existingSettings.StartupPasswordEnabled = startupPassword;
                existingSettings.ExitLockEnabled = exitLock;
                await database.UpdateAsync(existingSettings);
            }
            else
            {
                await database.InsertAsync(new UserSettings
                {
                    UserId = userId,
                    KeepScreenOn = keepScreenOn,
                    StartupPasswordEnabled = startupPassword,
                    ExitLockEnabled = exitLock
                });
            }
        }

        public async Task<UserSettings> GetUserSettingsAsync()
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;

            var settings = await database.Table<UserSettings>()
                                         .FirstOrDefaultAsync(x => x.UserId == userId);

            if (settings != null)
                return settings;

            settings = new UserSettings { UserId = userId };
            await database.InsertAsync(settings);
            return settings;
        }

        // ================= Reading Settings =================

        public async Task SaveReadingSettingsAsync(int fontSize, string backgroundColor, double lineSpacing)
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;

            var existingReading = await database.Table<ReadingSettings>()
                                                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (existingReading != null)
            {
                existingReading.FontSize = fontSize;
                existingReading.BackgroundColor = backgroundColor;
                existingReading.LineSpacing = lineSpacing;
                await database.UpdateAsync(existingReading);
            }
            else
            {
                await database.InsertAsync(new ReadingSettings
                {
                    UserId = userId,
                    FontSize = fontSize,
                    BackgroundColor = backgroundColor,
                    LineSpacing = lineSpacing
                });
            }
        }

        public async Task SaveReadingSettingsAsync(int fontSize, string backgroundColor)
        {
            await SaveReadingSettingsAsync(fontSize, backgroundColor, 1.65);
        }

        public async Task<ReadingSettings> GetReadingSettingsAsync()
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;

            var settings = await database.Table<ReadingSettings>()
                                         .FirstOrDefaultAsync(x => x.UserId == userId);

            if (settings != null)
                return settings;

            settings = new ReadingSettings
            {
                UserId = userId
            };

            await database.InsertAsync(settings);
            return settings;
        }

        // ================= Reading Progress =================

        public async Task SaveReadingProgressAsync(string fileName, int page, int totalPages)
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;
            string key = BuildProgressKey(userId, fileName);

            var existing = await database.Table<ReadingProgress>()
                                         .FirstOrDefaultAsync(p => p.ProgressKey == key);

            if (existing != null)
            {
                existing.LastPage = page;
                existing.TotalPages = totalPages;
                await database.UpdateAsync(existing);
            }
            else
            {
                await database.InsertAsync(new ReadingProgress
                {
                    ProgressKey = key,
                    UserId = userId,
                    FileName = fileName,
                    LastPage = page,
                    TotalPages = totalPages
                });
            }
        }

        public async Task SaveReadingProgressAsync(string fileName, int page)
        {
            await SaveReadingProgressAsync(fileName, page, 0);
        }

        public async Task<int> GetReadingProgressAsync(string fileName)
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;
            string key = BuildProgressKey(userId, fileName);

            var existing = await database.Table<ReadingProgress>()
                                         .FirstOrDefaultAsync(p => p.ProgressKey == key);

            return existing?.LastPage ?? 0;
        }

        public async Task<ReadingProgress> GetReadingProgressRecordAsync(string fileName)
        {
            await EnsureInitializedAsync();

            string userId = UserSession.UserId;
            string key = BuildProgressKey(userId, fileName);

            var existing = await database.Table<ReadingProgress>()
                                         .FirstOrDefaultAsync(p => p.ProgressKey == key);

            return existing ?? new ReadingProgress
            {
                ProgressKey = key,
                UserId = userId,
                FileName = fileName,
                LastPage = 0,
                TotalPages = 0
            };
        }
    }

    [Table("AppUsers")]
    public class AppUser
    {
        [PrimaryKey]
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool IsGuest { get; set; } = false;
    }

    [Table("UserSettingsV2")]
    public class UserSettings
    {
        [PrimaryKey]
        public string UserId { get; set; } = string.Empty;

        public bool StartupPasswordEnabled { get; set; } = false;

        public bool ExitLockEnabled { get; set; } = false;

        public bool KeepScreenOn { get; set; } = false;
    }

    [Table("ReadingSettingsV2")]
    public class ReadingSettings
    {
        [PrimaryKey]
        public string UserId { get; set; } = string.Empty;

        public int FontSize { get; set; } = 18;

        public string BackgroundColor { get; set; } = "Light";

        public double LineSpacing { get; set; } = 1.65;
    }

    [Table("ReadingProgressV2")]
    public class ReadingProgress
    {
        [PrimaryKey]
        public string ProgressKey { get; set; } = string.Empty;

        [Indexed]
        public string UserId { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public int LastPage { get; set; } = 0;

        public int TotalPages { get; set; } = 0;
    }

    [Table("UserSettings")]
    internal class LegacyUserSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public bool StartupPasswordEnabled { get; set; }
        public bool ExitLockEnabled { get; set; }
        public bool KeepScreenOn { get; set; }
    }

    [Table("ReadingSettings")]
    internal class LegacyReadingSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int FontSize { get; set; }
        public string BackgroundColor { get; set; } = "Light";
        public double LineSpacing { get; set; } = 1.65;
    }

    [Table("ReadingProgress")]
    internal class LegacyReadingProgress
    {
        [PrimaryKey]
        public string FileName { get; set; } = string.Empty;

        public int LastPage { get; set; }

        public int TotalPages { get; set; }
    }
}