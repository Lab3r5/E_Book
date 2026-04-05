using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using E_Book.Models;

namespace E_Book.Services
{
    public static class ImageViewerService
    {
        private static readonly string[] ImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        public static bool IsImageFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ImageExtensions.Contains(ext);
        }

        public static void ShowGallery(string currentFilePath, IEnumerable<BookItem>? sourceItems = null)
        {
            if (string.IsNullOrWhiteSpace(currentFilePath) || !File.Exists(currentFilePath))
                return;

            List<string> imagePaths;

            if (sourceItems != null)
            {
                imagePaths = sourceItems
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.FullPath))
                    .Select(x => x.FullPath)
                    .Where(IsImageFile)
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                imagePaths = LibraryService.LoadBooks()
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.FullPath))
                    .Select(x => x.FullPath)
                    .Where(IsImageFile)
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (imagePaths.Count == 0)
            {
                ShowSingle(currentFilePath);
                return;
            }

            if (!imagePaths.Contains(currentFilePath, StringComparer.OrdinalIgnoreCase))
                imagePaths.Insert(0, currentFilePath);

            int startIndex = imagePaths.FindIndex(x =>
                string.Equals(x, currentFilePath, StringComparison.OrdinalIgnoreCase));

            if (startIndex < 0)
                startIndex = 0;

            var photos = imagePaths
                .Select(path => new global::PhotoBrowsers.Photo
                {
                    URL = new Uri(path).AbsoluteUri,
                    Title = Path.GetFileName(path)
                })
                .ToList();

            var browser = new global::PhotoBrowsers.PhotoBrowser
            {
                Photos = photos,
                StartIndex = startIndex
            };

            browser.Show();
        }

        public static void ShowSingle(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            var browser = new global::PhotoBrowsers.PhotoBrowser
            {
                Photos = new List<global::PhotoBrowsers.Photo>
                {
                    new global::PhotoBrowsers.Photo
                    {
                        URL = new Uri(filePath).AbsoluteUri,
                        Title = Path.GetFileName(filePath)
                    }
                },
                StartIndex = 0
            };

            browser.Show();
        }
    }
}