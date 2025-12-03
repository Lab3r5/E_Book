using SQLite;
using System;
using System.IO;
using System.Threading.Tasks;

namespace EZ_Read.Data
{
    public class Database
    {
        private static readonly string dbPath = Path.Combine(FileSystem.AppDataDirectory, "userData.db");
        private readonly SQLiteAsyncConnection database;

        public Database()
        {
            database = new SQLiteAsyncConnection(dbPath);
            InitializeDatabaseAsync().ConfigureAwait(false); // Initialize the database asynchronously
        }

        private async Task InitializeDatabaseAsync()
        {
            await database.CreateTableAsync<UserPassword>();
            await database.CreateTableAsync<UserSettings>();
            await database.CreateTableAsync<ReadingSettings>();
            await database.CreateTableAsync<ReadingProgress>();

            // If no record is set, insert default settings
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

        //Password Related Methods
        public async Task SavePasswordAsync(string password)
        {
            await database.DeleteAllAsync<UserPassword>(); // Store only one password
            await database.InsertAsync(new UserPassword { Password = password });
        }

        public async Task<string?> GetPasswordAsync()
        {
            var userPassword = await database.Table<UserPassword>().FirstOrDefaultAsync();
            return userPassword?.Password;
        }
        //

        //SettingPage related methods
        public async Task SaveSettingsAsync(bool keepScreenOn, bool startupPassword, bool exitLock)
        {
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
            var settings = await database.Table<UserSettings>().FirstOrDefaultAsync();
            return settings ?? new UserSettings();
        }
        //

        //ReadingPage related methods
        public async Task SaveReadingSettingsAsync(int fontSize, string backgroundColor)
        {
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
            var settings = await database.Table<ReadingSettings>().FirstOrDefaultAsync();
            return settings ?? new ReadingSettings();
        }
        //

        //ReadingProgress Methods
        public async Task SaveReadingProgressAsync(string fileName, int page)
        {
            var existing = await database.Table<ReadingProgress>().FirstOrDefaultAsync(p => p.FileName == fileName);
            if (existing != null)
            {
                existing.LastPage = page;
                await database.UpdateAsync(existing);
            }
            else
            {
                await database.InsertAsync(new ReadingProgress { FileName = fileName, LastPage = page });
            }
        }

        public async Task<int> GetReadingProgressAsync(string fileName)
        {
            var existing = await database.Table<ReadingProgress>().FirstOrDefaultAsync(p => p.FileName == fileName);
            return existing?.LastPage ?? 0;
        }
        //
    }

    //Password Data Table
    public class UserPassword
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Password { get; set; } = string.Empty;
    }
    //

    //UserSettings Data Table (SettingPage)
    public class UserSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public bool StartupPasswordEnabled { get; set; } = false;
        public bool ExitLockEnabled { get; set; } = false;
        public bool KeepScreenOn { get; set; } = false;
    }
    //

    //ReadingSettings Data Table (ReadingPage)
    public class ReadingSettings
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int FontSize { get; set; } = 18;
        public string BackgroundColor { get; set; } = "#FFF8E8";
    }
    //

    //ReadingProgress Data Table (per file)
    public class ReadingProgress
    {
        [PrimaryKey]
        public string FileName { get; set; } = string.Empty;
        public int LastPage { get; set; } = 0;
    }
    //
}