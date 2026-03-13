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

        public sealed class ReadingMetaChangedEventArgs : EventArgs
        {
            public string FullPath { get; init; } = string.Empty;
            public double Progress { get; init; }
            public long LastOpenedTicks { get; init; }
            public bool Removed { get; init; }
        }

        public readonly struct ReadingMetaSnapshot
        {
            public ReadingMetaSnapshot(double progress, long lastOpenedTicks)
            {
                Progress = progress;
                LastOpenedTicks = lastOpenedTicks;
            }

            public double Progress { get; }
            public long LastOpenedTicks { get; }
        }

        public static event EventHandler<ReadingMetaChangedEventArgs>? MetaChanged;

        public static void UpdateLastOpened(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.ContainsKey(key))
                all[key] = new ReadingMeta();

            all[key].LastOpenedTicks = DateTime.UtcNow.Ticks;
            SaveAll(all);

            RaiseMetaChanged(fullPath, all[key].Progress, all[key].LastOpenedTicks, removed: false);
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

            RaiseMetaChanged(fullPath, all[key].Progress, all[key].LastOpenedTicks, removed: false);
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

        public static Dictionary<string, ReadingMetaSnapshot> LoadSnapshotMap()
        {
            var all = LoadAll();
            var result = new Dictionary<string, ReadingMetaSnapshot>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in all)
            {
                if (pair.Value == null) continue;

                result[pair.Key] = new ReadingMetaSnapshot(
                    pair.Value.Progress,
                    pair.Value.LastOpenedTicks);
            }

            return result;
        }

        public static bool TryGetSnapshot(string fullPath, out ReadingMetaSnapshot snapshot)
        {
            snapshot = default;

            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (!all.TryGetValue(key, out var meta) || meta == null)
                return false;

            snapshot = new ReadingMetaSnapshot(meta.Progress, meta.LastOpenedTicks);
            return true;
        }

        public static void Remove(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;

            var all = LoadAll();
            var key = Normalize(fullPath);

            if (all.Remove(key))
            {
                SaveAll(all);
                RaiseMetaChanged(fullPath, 0, 0, removed: true);
            }
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
                    return new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);

                return JsonSerializer.Deserialize<Dictionary<string, ReadingMeta>>(json)
                       ?? new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, ReadingMeta>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveAll(Dictionary<string, ReadingMeta> all)
        {
            var json = JsonSerializer.Serialize(all);
            Preferences.Default.Set(MetaKey, json);
        }

        private static void RaiseMetaChanged(string fullPath, double progress, long lastOpenedTicks, bool removed)
        {
            MetaChanged?.Invoke(null, new ReadingMetaChangedEventArgs
            {
                FullPath = fullPath,
                Progress = progress,
                LastOpenedTicks = lastOpenedTicks,
                Removed = removed
            });
        }

        private static string Normalize(string fullPath)
            => fullPath.Trim().ToLowerInvariant();
    }
}