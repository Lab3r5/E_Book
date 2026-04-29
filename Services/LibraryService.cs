using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using E_Book.Models;
using Microsoft.Maui.Storage;

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

        private static readonly HashSet<string> SupportedExtensionSet =
            new(SupportedExtensions, StringComparer.OrdinalIgnoreCase);

        private static readonly object CacheLock = new();
        private static List<BookItem>? _cachedBooks;
        private static string _cachedLibraryPath = string.Empty;
        private static DateTime _cachedDirectoryLastWriteUtc;

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

        public static void InvalidateCache()
        {
            lock (CacheLock)
            {
                _cachedBooks = null;
                _cachedLibraryPath = string.Empty;
                _cachedDirectoryLastWriteUtc = DateTime.MinValue;
            }
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
                    foreach (var sourceFile in Directory.EnumerateFiles(LegacySharedLibraryPath).Where(IsSupportedFile))
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

        public static List<BookItem> LoadBooks(bool forceRefresh = false)
        {
            EnsureLibraryExists();

            string libraryPath = LibraryPath;
            var directory = new DirectoryInfo(libraryPath);
            DateTime directoryLastWriteUtc = directory.Exists ? directory.LastWriteTimeUtc : DateTime.MinValue;

            lock (CacheLock)
            {
                if (!forceRefresh &&
                    _cachedBooks != null &&
                    string.Equals(_cachedLibraryPath, libraryPath, StringComparison.OrdinalIgnoreCase) &&
                    _cachedDirectoryLastWriteUtc == directoryLastWriteUtc)
                {
                    return CloneBooks(_cachedBooks);
                }
            }

            var files = Directory.EnumerateFiles(libraryPath)
                .Where(IsSupportedFile)
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var books = files.Select(f => new BookItem
            {
                FileName = Path.GetFileName(f),
                FullPath = f,
                Format = GetFormatTag(f)
            }).ToList();

            lock (CacheLock)
            {
                _cachedBooks = CloneBooks(books);
                _cachedLibraryPath = libraryPath;
                _cachedDirectoryLastWriteUtc = directoryLastWriteUtc;
            }

            return books;
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
                    string title = Path.GetFileNameWithoutExtension(b.FileName ?? string.Empty);
                    string format = b.Format ?? string.Empty;

                    return title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                           || format.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(b =>
                    Path.GetFileNameWithoutExtension(b.FileName ?? string.Empty)
                        .Equals(keyword, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(b =>
                    Path.GetFileNameWithoutExtension(b.FileName ?? string.Empty)
                        .StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(b => b.LastOpenedTicks)
                .ThenBy(b => b.DisplayFileName, StringComparer.OrdinalIgnoreCase)
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

        private static List<BookItem> CloneBooks(IEnumerable<BookItem> books)
        {
            return books.Select(book => new BookItem
            {
                FileName = book.FileName,
                FullPath = book.FullPath,
                Format = book.Format
            }).ToList();
        }

        private static bool IsSupportedFile(string path) =>
            SupportedExtensionSet.Contains(Path.GetExtension(path));
    }
}