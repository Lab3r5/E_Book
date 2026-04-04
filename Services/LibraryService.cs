using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Maui.Storage;
using E_Book.Models;

namespace E_Book.Services
{
    public static class LibraryService
    {
        private const string LegacyMigrationFlag = "library_migrated_to_multi_user_v1";

        public static readonly string[] SupportedExtensions =
        {
            ".txt", ".epub", ".pdf", ".html", ".htm", ".docx", ".rtf",
            ".jpg", ".jpeg", ".png", ".webp"
        };

        public static string UsersRootPath =>
            Path.Combine(FileSystem.AppDataDirectory, "Users");

        public static string CurrentUserRootPath =>
            Path.Combine(UsersRootPath, UserSession.StorageKey);

        public static string LibraryPath =>
            Path.Combine(CurrentUserRootPath, "Library");

        public static string GuestLibraryPath =>
            Path.Combine(UsersRootPath, UserSession.BuildSafeStorageKey(UserSession.GuestUserId), "Library");

        public static string LegacySharedLibraryPath =>
            Path.Combine(FileSystem.AppDataDirectory, "Library");

        public static void EnsureLibraryExists()
        {
            EnsureLegacySharedLibraryMigratedToGuest();
            EnsureCurrentUserLibraryExists();
        }

        public static void EnsureCurrentUserLibraryExists()
        {
            if (!Directory.Exists(UsersRootPath))
                Directory.CreateDirectory(UsersRootPath);

            if (!Directory.Exists(CurrentUserRootPath))
                Directory.CreateDirectory(CurrentUserRootPath);

            if (!Directory.Exists(LibraryPath))
                Directory.CreateDirectory(LibraryPath);
        }

        private static void EnsureLegacySharedLibraryMigratedToGuest()
        {
            if (Preferences.Get(LegacyMigrationFlag, false))
                return;

            try
            {
                if (!Directory.Exists(UsersRootPath))
                    Directory.CreateDirectory(UsersRootPath);

                if (!Directory.Exists(GuestLibraryPath))
                    Directory.CreateDirectory(GuestLibraryPath);

                if (Directory.Exists(LegacySharedLibraryPath))
                {
                    var files = Directory.GetFiles(LegacySharedLibraryPath)
                        .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        .ToList();

                    foreach (var sourceFile in files)
                    {
                        string fileName = Path.GetFileName(sourceFile);
                        string targetFile = Path.Combine(GuestLibraryPath, fileName);

                        if (!File.Exists(targetFile))
                            File.Copy(sourceFile, targetFile, overwrite: false);
                    }
                }

                Preferences.Set(LegacyMigrationFlag, true);
            }
            catch
            {
            }
        }

        public static List<BookItem> LoadBooks()
        {
            EnsureLibraryExists();

            var files = Directory.GetFiles(LibraryPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            return files.Select(f => new BookItem
            {
                FileName = Path.GetFileName(f),
                FullPath = f,
                Format = GetFormatTag(f)
            }).ToList();
        }

        public static List<BookItem> SearchFromCache(IEnumerable<BookItem> books, string keyword)
        {
            keyword = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<BookItem>();

            var all = books?.ToList() ?? new List<BookItem>();

            return all
                .Where(b =>
                {
                    string title = Path.GetFileNameWithoutExtension(b.FileName ?? "");
                    string format = b.Format ?? "";

                    return title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                           || format.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(b =>
                    Path.GetFileNameWithoutExtension(b.FileName ?? "")
                        .Equals(keyword, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(b =>
                    Path.GetFileNameWithoutExtension(b.FileName ?? "")
                        .StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(b => b.LastOpenedTicks)
                .ThenBy(b => b.DisplayFileName)
                .ToList();
        }

        public static List<BookItem> Search(string keyword)
        {
            var all = LoadBooks();
            return SearchFromCache(all, keyword);
        }

        public static string GetFormatTag(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".txt" => "TXT",
                ".epub" => "EPUB",
                ".pdf" => "PDF",
                ".html" or ".htm" => "HTML",
                ".docx" => "DOCX",
                ".rtf" => "RTF",
                ".jpg" or ".jpeg" or ".png" or ".webp" => "IMAGE",
                _ => "FILE"
            };
        }
    }
}