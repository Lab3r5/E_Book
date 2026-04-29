using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using E_Book.Models;
using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class CachedDocumentContentService
    {
        private const int CacheFormatVersion = 3;
        private sealed class CacheEntry
        {
            public ParsedReadingContent Content { get; init; } = new();
            public DateTime LastWriteUtc { get; init; }
            public long Length { get; init; }
            public DateTime LastAccessUtc { get; set; }
        }

        private sealed class DiskCacheEnvelope
        {
            public int Version { get; set; }
            public string SourcePath { get; set; } = string.Empty;
            public DateTime LastWriteUtc { get; set; }
            public long Length { get; set; }
            public ParsedReadingContent Content { get; set; } = new();
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private static string CacheDirectoryPath =>
            Path.Combine(FileSystem.CacheDirectory, "document-content-cache");

        public static async Task<ParsedReadingContent> ParseAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var info = new FileInfo(filePath);
            string key = Path.GetFullPath(filePath);
            DateTime lastWriteUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
            long length = info.Exists ? info.Length : 0;

            if (Cache.TryGetValue(key, out var entry) &&
                entry.LastWriteUtc == lastWriteUtc &&
                entry.Length == length)
            {
                entry.LastAccessUtc = DateTime.UtcNow;
                return Clone(entry.Content);
            }

            var diskCached = await TryReadDiskCacheAsync(key, lastWriteUtc, length, cancellationToken);
            if (diskCached != null)
            {
                Cache[key] = new CacheEntry
                {
                    Content = Clone(diskCached),
                    LastWriteUtc = lastWriteUtc,
                    Length = length,
                    LastAccessUtc = DateTime.UtcNow
                };

                TrimMemoryCache(key);
                return Clone(diskCached);
            }

            var parsed = await DocumentContentService.ParseAsync(filePath, cancellationToken);
            var clone = Clone(parsed);

            Cache[key] = new CacheEntry
            {
                Content = clone,
                LastWriteUtc = lastWriteUtc,
                Length = length,
                LastAccessUtc = DateTime.UtcNow
            };

            TrimMemoryCache(key);
            await WriteDiskCacheAsync(key, lastWriteUtc, length, clone, cancellationToken);
            return Clone(clone);
        }

        private static async Task<ParsedReadingContent?> TryReadDiskCacheAsync(
            string key,
            DateTime lastWriteUtc,
            long length,
            CancellationToken cancellationToken)
        {
            try
            {
                string cachePath = GetCacheFilePath(key);
                if (!File.Exists(cachePath))
                    return null;

                await using var fileStream = File.OpenRead(cachePath);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                var envelope = await JsonSerializer.DeserializeAsync<DiskCacheEnvelope>(gzipStream, JsonOptions, cancellationToken);

                if (envelope?.Content == null)
                    return null;

                if (envelope.Version != CacheFormatVersion || envelope.LastWriteUtc != lastWriteUtc || envelope.Length != length)
                    return null;

                return Clone(envelope.Content);
            }
            catch
            {
                return null;
            }
        }

        private static async Task WriteDiskCacheAsync(
            string key,
            DateTime lastWriteUtc,
            long length,
            ParsedReadingContent content,
            CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectoryPath);

                string cachePath = GetCacheFilePath(key);
                string tempPath = cachePath + ".tmp";

                var envelope = new DiskCacheEnvelope
                {
                    Version = CacheFormatVersion,
                    SourcePath = key,
                    LastWriteUtc = lastWriteUtc,
                    Length = length,
                    Content = Clone(content)
                };

                await using (var fileStream = File.Create(tempPath))
                await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Fastest))
                {
                    await JsonSerializer.SerializeAsync(gzipStream, envelope, JsonOptions, cancellationToken);
                }

                if (File.Exists(cachePath))
                    File.Delete(cachePath);

                File.Move(tempPath, cachePath);
                TrimDiskCache(cachePath);
            }
            catch
            {
            }
        }

        private static void TrimMemoryCache(string latestKey)
        {
            const int maxEntries = 12;
            if (Cache.Count <= maxEntries)
                return;

            foreach (var key in Cache.Keys)
            {
                if (string.Equals(key, latestKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                Cache.TryRemove(key, out _);

                if (Cache.Count <= maxEntries)
                    break;
            }
        }

        private static void TrimDiskCache(string latestPath)
        {
            try
            {
                var directory = new DirectoryInfo(CacheDirectoryPath);
                if (!directory.Exists)
                    return;

                const int maxFiles = 24;
                var files = directory.GetFiles("*.json.gz");
                if (files.Length <= maxFiles)
                    return;

                foreach (var file in files)
                {
                    if (string.Equals(file.FullName, latestPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                    }

                    files = directory.GetFiles("*.json.gz");
                    if (files.Length <= maxFiles)
                        break;
                }
            }
            catch
            {
            }
        }

        private static string GetCacheFilePath(string key)
        {
            Directory.CreateDirectory(CacheDirectoryPath);
            return Path.Combine(CacheDirectoryPath, $"{ComputeStableHash(key)}.json.gz");
        }

        private static string ComputeStableHash(string input)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in input)
                    hash = hash * 31 + c;

                return Math.Abs(hash).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static ParsedReadingContent Clone(ParsedReadingContent source)
        {
            return new ParsedReadingContent
            {
                Title = source.Title,
                SourcePath = source.SourcePath,
                ContentKind = source.ContentKind,
                Author = source.Author,
                TxtParagraphs = new List<string>(source.TxtParagraphs),
                RawHtmlChapters = new List<string>(source.RawHtmlChapters),
                RawHtmlChapterKeys = new List<string>(source.RawHtmlChapterKeys),
                RawHtmlChapterTitles = new List<string>(source.RawHtmlChapterTitles)
            };
        }
    }
}



