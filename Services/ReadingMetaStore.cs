using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Maui.Storage;
using E_Book.Models;

namespace E_Book.Services
{
    public static class ReadingMetaStore
    {
        private static string MetaKey => $"ebook_reading_meta_{UserSession.StorageKey}_v4";

        private static Dictionary<string, ReadingMeta>? _cache;

        private class ReadingMeta
        {
            public double Progress { get; set; }
            public long LastOpenedTicks { get; set; }
            public int LastReadPage { get; set; }
            public int TotalPages { get; set; }
            public long TotalReadingSeconds { get; set; }
        }

        public sealed class ReadingMetaChangedEventArgs : EventArgs
        {
            public string FullPath { get; init; } = string.Empty;
            public double Progress { get; init; }
            public long LastOpenedTicks { get; init; }
            public int LastReadPage { get; init; }
            public int TotalPages { get; init; }
            public long TotalReadingSeconds { get; init; }
            public bool Removed { get; init; }
        }

        public readonly struct ReadingMetaSnapshot
        {
            public ReadingMetaSnapshot(
                double progress,
                long lastOpenedTicks,
                int lastReadPage,
                int totalPages,
                long totalReadingSeconds)
            {
                Progress = progress;
                LastOpenedTicks = lastOpenedTicks;
                LastReadPage = lastReadPage;
                TotalPages = totalPages;
                TotalReadingSeconds = totalReadingSeconds;
            }

            public double Progress { get; }
            public long LastOpenedTicks { get; }
            public int LastReadPage { get; }
            public int TotalPages { get; }
            public long TotalReadingSeconds { get; }
        }

        public static event EventHandler<ReadingMetaChangedEventArgs>? MetaChanged;

        // -----------------------------
        // Core API
        // -----------------------------

        public static void UpdateLastOpened(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;

            SaveAll(all);

            RaiseMetaChanged(fullPath, all[key], false);
        }

        public static void UpdateProgress(string fullPath, int currentPage, int totalPages)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || totalPages <= 0)
                return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            double progress = (double)currentPage / totalPages;
            progress = Math.Max(0, Math.Min(1, progress));

            all[key].Progress = progress;
            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;
            all[key].LastReadPage = Math.Max(1, currentPage);
            all[key].TotalPages = Math.Max(1, totalPages);

            SaveAll(all);

            RaiseMetaChanged(fullPath, all[key], false);
        }

        public static void AddReadingDuration(string fullPath, long secondsToAdd)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || secondsToAdd <= 0)
                return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            all[key].TotalReadingSeconds += secondsToAdd;
            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;

            SaveAll(all);

            RaiseMetaChanged(fullPath, all[key], false);
        }

        public static void ApplyToBook(BookItem book)
        {
            if (book == null || string.IsNullOrWhiteSpace(book.FullPath))
                return;

            var all = LoadAll();
            var key = Normalize(book.FullPath);

            if (all.TryGetValue(key, out var meta))
            {
                book.ReadingProgress = meta.Progress;
                book.LastOpenedTicks = meta.LastOpenedTicks;
                book.LastReadPage = meta.LastReadPage;
                book.TotalPages = meta.TotalPages;
                book.TotalReadingSeconds = meta.TotalReadingSeconds;
            }
            else
            {
                book.ReadingProgress = 0;
                book.LastOpenedTicks = 0;
                book.LastReadPage = 0;
                book.TotalPages = 0;
                book.TotalReadingSeconds = 0;
            }

            book.RefreshVisualMeta();
        }

        // -----------------------------
        // Snapshot API
        // -----------------------------

        public static bool TryGetSnapshot(string fullPath, out ReadingMetaSnapshot snapshot)
        {
            snapshot = default;

            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.TryGetValue(key, out var meta))
                return false;

            snapshot = new ReadingMetaSnapshot(
                meta.Progress,
                meta.LastOpenedTicks,
                meta.LastReadPage,
                meta.TotalPages,
                meta.TotalReadingSeconds);

            return true;
        }

        public static Dictionary<string, ReadingMetaSnapshot> LoadSnapshotMap()
        {
            var all = LoadAll();
            var result = new Dictionary<string, ReadingMetaSnapshot>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in all)
            {
                var meta = pair.Value;
                if (meta == null) continue;

                result[pair.Key] = new ReadingMetaSnapshot(
                    meta.Progress,
                    meta.LastOpenedTicks,
                    meta.LastReadPage,
                    meta.TotalPages,
                    meta.TotalReadingSeconds);
            }

            return result;
        }

        // -----------------------------
        // Delete
        // -----------------------------

        public static void Remove(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (all.Remove(key))
            {
                SaveAll(all);

                MetaChanged?.Invoke(null, new ReadingMetaChangedEventArgs
                {
                    FullPath = fullPath,
                    Removed = true
                });
            }
        }

        public static void ClearCurrentUser()
        {
            Preferences.Default.Remove(MetaKey);
            _cache = null;
        }

        // -----------------------------
        // Storage
        // -----------------------------

        private static Dictionary<string, ReadingMeta> LoadAll()
        {
            if (_cache != null)
                return _cache;

            try
            {
                var json = Preferences.Default.Get(MetaKey, string.Empty);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _cache = new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);
                    return _cache;
                }

                _cache = JsonSerializer.Deserialize<Dictionary<string, ReadingMeta>>(json)
                         ?? new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);

                return _cache;
            }
            catch
            {
                _cache = new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);
                return _cache;
            }
        }

        private static void SaveAll(Dictionary<string, ReadingMeta> all)
        {
            try
            {
                var json = JsonSerializer.Serialize(all);
                Preferences.Default.Set(MetaKey, json);
                _cache = all;
            }
            catch
            {
            }
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        private static void RaiseMetaChanged(string fullPath, ReadingMeta meta, bool removed)
        {
            MetaChanged?.Invoke(null, new ReadingMetaChangedEventArgs
            {
                FullPath = fullPath,
                Progress = meta.Progress,
                LastOpenedTicks = meta.LastOpenedTicks,
                LastReadPage = meta.LastReadPage,
                TotalPages = meta.TotalPages,
                TotalReadingSeconds = meta.TotalReadingSeconds,
                Removed = removed
            });
        }

        private static string Normalize(string fullPath)
            => fullPath.Trim().ToLowerInvariant();
    }
}