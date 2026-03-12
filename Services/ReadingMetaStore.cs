using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Maui.Storage;
using E_Book.Models;

namespace E_Book.Services
{
    public static class ReadingMetaStore
    {
        private static string MetaKey => $"ebook_reading_meta_{UserSession.StorageKey}_v2";

        private class ReadingMeta
        {
            public double Progress { get; set; }
            public long LastOpenedTicks { get; set; }
        }

        public static void UpdateLastOpened(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;
            SaveAll(all);
        }

        public static void UpdateProgress(string fullPath, int currentPage, int totalPages)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || totalPages <= 0) return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            double progress = (double)currentPage / totalPages;
            progress = Math.Max(0, Math.Min(1, progress));

            all[key].Progress = progress;
            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;

            SaveAll(all);
        }

        public static void ApplyToBook(BookItem book)
        {
            if (book == null || string.IsNullOrWhiteSpace(book.FullPath)) return;

            var all = LoadAll();
            var key = Normalize(book.FullPath);

            if (all.TryGetValue(key, out var meta))
            {
                book.ReadingProgress = meta.Progress;
                book.LastOpenedTicks = meta.LastOpenedTicks;
            }
            else
            {
                book.ReadingProgress = 0;
                book.LastOpenedTicks = 0;
            }

            book.RefreshVisualMeta();
        }

        public static void Remove(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (all.Remove(key))
                SaveAll(all);
        }

        public static void ClearCurrentUser()
        {
            Preferences.Default.Remove(MetaKey);
        }

        private static Dictionary<string, ReadingMeta> LoadAll()
        {
            try
            {
                var json = Preferences.Default.Get(MetaKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                    return new Dictionary<string, ReadingMeta>();

                return JsonSerializer.Deserialize<Dictionary<string, ReadingMeta>>(json)
                       ?? new Dictionary<string, ReadingMeta>();
            }
            catch
            {
                return new Dictionary<string, ReadingMeta>();
            }
        }

        private static void SaveAll(Dictionary<string, ReadingMeta> all)
        {
            var json = JsonSerializer.Serialize(all);
            Preferences.Default.Set(MetaKey, json);
        }

        private static string Normalize(string fullPath)
            => fullPath.Trim().ToLowerInvariant();
    }
}