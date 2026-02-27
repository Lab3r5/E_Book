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

        public static List<BookItem> Search(string keyword)
        {
            keyword = (keyword ?? "").Trim();
            if (keyword.Length == 0) return new List<BookItem>();

            var all = LoadBooks();

            return all.Where(b =>
                    b.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
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