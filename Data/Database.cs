using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace E_Book.Data
{
    public class Database
    {
        private const string NewDbName = "E_Book.db";
        private const string OldDbName = "userData.db";

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
                // 如果旧数据库存在，并且新数据库不存在，就复制迁移
                if (File.Exists(oldDbPath) && !File.Exists(newDbPath))
                {
                    File.Copy(oldDbPath, newDbPath);
                }
            }
            catch
            {
                // 迁移失败不影响程序启动
            }
        }

        private Task EnsureInitializedAsync()
        {
            return initializeTask;
        }

        private async Task InitializeDatabaseAsync()
        {
            await database.CreateTableAsync<UserPassword>();
            await database.CreateTableAsync<UserSettings>();
            await database.CreateTableAsync<ReadingSettings>();
            await database.CreateTableAsync<ReadingProgress>();

            // 如果没有设置记录，插入默认值
            var existingSettings = await database.Table<UserSettings>().FirstOrDefaultAsync();
            if (existingSettings == null)
            {
                await database.InsertAsync(new UserSettings());
            }

            var existingReading = await database.Table<ReadingSettings>().FirstOrDefaultAsync();
            if (existingReading == null)
            {
                await database.InsertAsync(new ReadingSettings());
            }
        }

        // ================= Password =================
        public async Task SavePasswordAsync(string password)
        {
            await EnsureInitializedAsync();
            await database.DeleteAllAsync<UserPassword>();
            await database.InsertAsync(new UserPassword { Password = password });
        }

        public async Task<string?> GetPasswordAsync()
        {
            await EnsureInitializedAsync();
            var userPassword = await database.Table<UserPassword>().FirstOrDefaultAsync();
            return userPassword?.Password;
        }

        // ================= Settings =================
        public async Task SaveSettingsAsync(bool keepScreenOn, bool startupPassword, bool exitLock)
        {
            await EnsureInitializedAsync();
            var existingSettings = await database.Table<UserSettings>().FirstOrDefaultAsync();
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
                    KeepScreenOn = keepScreenOn,
                    StartupPasswordEnabled = startupPassword,
                    ExitLockEnabled = exitLock,
                });
            }
        }

        public async Task<UserSettings> GetUserSettingsAsync()
        {
            await EnsureInitializedAsync();
            var settings = await database.Table<UserSettings>().FirstOrDefaultAsync();
            return settings ?? new UserSettings();
        }

        // ================= Reading Settings =================
        public async Task SaveReadingSettingsAsync(int fontSize, string backgroundColor)
        {
            await EnsureInitializedAsync();
            var existingReading = await database.Table<ReadingSettings>().FirstOrDefaultAsync();
            if (existingReading != null)
            {
                existingReading.FontSize = fontSize;
                existingReading.BackgroundColor = backgroundColor;
                await database.UpdateAsync(existingReading);
            }
            else
            {
                await database.InsertAsync(new ReadingSettings
                {
                    FontSize = fontSize,
                    BackgroundColor = backgroundColor
                });
            }
        }

        public async Task<ReadingSettings> GetReadingSettingsAsync()
        {
            await EnsureInitializedAsync();
            var settings = await database.Table<ReadingSettings>().FirstOrDefaultAsync();
            return settings ?? new ReadingSettings();
        }

        // ================= Reading Progress =================
        public async Task SaveReadingProgressAsync(string fileName, int page)
        {
            await EnsureInitializedAsync();
            var existing = await database.Table<ReadingProgress>()
                                          .FirstOrDefaultAsync(p => p.FileName == fileName);
            if (existing != null)
            {
                existing.LastPage = page;
                await database.UpdateAsync(existing);
            }
            else
            {
                await database.InsertAsync(new ReadingProgress
                {
                    FileName = fileName,
                    LastPage = page
                });
            }
        }

        public async Task<int> GetReadingProgressAsync(string fileName)
        {
            await EnsureInitializedAsync();
            var existing = await database.Table<ReadingProgress>()
                                          .FirstOrDefaultAsync(p => p.FileName == fileName);
            return existing?.LastPage ?? 0;
        }
    }

    // ================= Tables =================

    public class UserPassword
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class UserSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public bool StartupPasswordEnabled { get; set; } = false;
        public bool ExitLockEnabled { get; set; } = false;
        public bool KeepScreenOn { get; set; } = false;
    }

    public class ReadingSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int FontSize { get; set; } = 18;
        public string BackgroundColor { get; set; } = "#FFF8E8";
    }

    public class ReadingProgress
    {
        [PrimaryKey]
        public string FileName { get; set; } = string.Empty;
        public int LastPage { get; set; } = 0;
    }
}
