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
        private static string? _cachedMetaKey;

        private class ReadingMeta
        {
            public double Progress { get; set; }
            public long LastOpenedTicks { get; set; }
            public int LastReadPage { get; set; }
            public int TotalPages { get; set; }
            public bool HasReliableTotalPages { get; set; }
            public long TotalReadingSeconds { get; set; }
        }

        public sealed class ReadingMetaChangedEventArgs : EventArgs
        {
            public string FullPath { get; init; } = string.Empty;
            public double Progress { get; init; }
            public long LastOpenedTicks { get; init; }
            public int LastReadPage { get; init; }
            public int TotalPages { get; init; }
            public bool HasReliableTotalPages { get; init; }
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
                bool hasReliableTotalPages,
                long totalReadingSeconds)
            {
                Progress = progress;
                LastOpenedTicks = lastOpenedTicks;
                LastReadPage = lastReadPage;
                TotalPages = totalPages;
                HasReliableTotalPages = hasReliableTotalPages;
                TotalReadingSeconds = totalReadingSeconds;
            }

            public double Progress { get; }
            public long LastOpenedTicks { get; }
            public int LastReadPage { get; }
            public int TotalPages { get; }
            public bool HasReliableTotalPages { get; }
            public long TotalReadingSeconds { get; }
        }

        public static event EventHandler<ReadingMetaChangedEventArgs>? MetaChanged;

        public static void ResetCache()
        {
            _cache = null;
            _cachedMetaKey = null;
        }

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

        public static void UpdateProgress(string fullPath, int currentPage, int totalPages, bool hasReliableTotalPages = true)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || currentPage <= 0)
                return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            int normalizedTotalPages = Math.Max(0, totalPages);
            int effectiveTotalPages = 0;

            if (hasReliableTotalPages)
            {
                all[key].TotalPages = Math.Max(all[key].TotalPages, Math.Max(1, normalizedTotalPages));
                all[key].HasReliableTotalPages = true;
                effectiveTotalPages = all[key].TotalPages;
            }
            else if (all[key].HasReliableTotalPages && all[key].TotalPages > 0)
            {
                effectiveTotalPages = all[key].TotalPages;
            }

            if (effectiveTotalPages > 0)
            {
                double progress = (double)currentPage / effectiveTotalPages;
                progress = Math.Max(0, Math.Min(1, progress));
                all[key].Progress = progress;
            }

            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;
            all[key].LastReadPage = Math.Max(1, currentPage);
            all[key].TotalReadingSeconds = Math.Max(0, all[key].TotalReadingSeconds);

            SaveAll(all);

            RaiseMetaChanged(fullPath, all[key], false);
        }

        public static void InvalidateReliableTotalPages(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.TryGetValue(key, out var meta))
                return;

            meta.TotalPages = 0;
            meta.HasReliableTotalPages = false;

            SaveAll(all);
            RaiseMetaChanged(fullPath, meta, false);
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
                book.HasReliableTotalPages = meta.HasReliableTotalPages;
                book.TotalReadingSeconds = meta.TotalReadingSeconds;
            }
            else
            {
                book.ReadingProgress = 0;
                book.LastOpenedTicks = 0;
                book.LastReadPage = 0;
                book.TotalPages = 0;
                book.HasReliableTotalPages = false;
                book.TotalReadingSeconds = 0;
            }

            book.RefreshVisualMeta();
        }

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
                meta.HasReliableTotalPages,
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
                    meta.HasReliableTotalPages,
                    meta.TotalReadingSeconds);
            }

            return result;
        }

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
            ResetCache();
        }

        private static Dictionary<string, ReadingMeta> LoadAll()
        {
            string currentKey = MetaKey;

            if (_cache != null && string.Equals(_cachedMetaKey, currentKey, StringComparison.Ordinal))
                return _cache;

            try
            {
                var json = Preferences.Default.Get(currentKey, string.Empty);

                if (string.IsNullOrWhiteSpace(json))
                {
                    _cache = new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);
                    _cachedMetaKey = currentKey;
                    return _cache;
                }

                _cache = JsonSerializer.Deserialize<Dictionary<string, ReadingMeta>>(json)
                         ?? new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);

                _cachedMetaKey = currentKey;
                return _cache;
            }
            catch
            {
                _cache = new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);
                _cachedMetaKey = currentKey;
                return _cache;
            }
        }

        private static void SaveAll(Dictionary<string, ReadingMeta> all)
        {
            try
            {
                var currentKey = MetaKey;
                var json = JsonSerializer.Serialize(all);
                Preferences.Default.Set(currentKey, json);
                _cache = all;
                _cachedMetaKey = currentKey;
            }
            catch
            {
            }
        }

        private static void RaiseMetaChanged(string fullPath, ReadingMeta meta, bool removed)
        {
            MetaChanged?.Invoke(null, new ReadingMetaChangedEventArgs
            {
                FullPath = fullPath,
                Progress = meta.Progress,
                LastOpenedTicks = meta.LastOpenedTicks,
                LastReadPage = meta.LastReadPage,
                TotalPages = meta.TotalPages,
                HasReliableTotalPages = meta.HasReliableTotalPages,
                TotalReadingSeconds = meta.TotalReadingSeconds,
                Removed = removed
            });
        }

        private static string Normalize(string fullPath)
            => fullPath.Trim().ToLowerInvariant();
    }
}