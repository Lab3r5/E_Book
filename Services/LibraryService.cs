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
        public static readonly string LibraryPath =
            Path.Combine(FileSystem.AppDataDirectory, "Library");

        public static readonly string[] SupportedExtensions =
        {
            ".txt", ".epub", ".pdf", ".html", ".htm", ".docx", ".rtf"
        };

        public static void EnsureLibraryExists()
        {
            if (!Directory.Exists(LibraryPath))
                Directory.CreateDirectory(LibraryPath);
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
            keyword = (keyword ?? "").Trim();
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<BookItem>();

            var all = books?.ToList() ?? new List<BookItem>();
            string lowerKeyword = keyword.ToLowerInvariant();

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
                _ => "FILE"
            };
        }
    }
}