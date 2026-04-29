using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class DocumentPaginationCacheService
    {
        private const int CacheFormatVersion = 2;

        public sealed class CachedTocItem
        {
            public string Title { get; set; } = string.Empty;
            public int PageIndex { get; set; }
        }

        public sealed class DocumentPaginationCacheEntry
        {
            public List<string> HtmlPages { get; set; } = new();
            public List<int> ChapterStartPageIndices { get; set; } = new();
            public List<CachedTocItem> TocItems { get; set; } = new();
        }

        private sealed class DiskCacheEnvelope
        {
            public int Version { get; set; }
            public string SourcePath { get; set; } = string.Empty;
            public DateTime LastWriteUtc { get; set; }
            public long Length { get; set; }
            public string LayoutKey { get; set; } = string.Empty;
            public DocumentPaginationCacheEntry Entry { get; set; } = new();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private static string CacheDirectoryPath =>
            Path.Combine(FileSystem.CacheDirectory, "document-pagination-cache");

        public static async Task<DocumentPaginationCacheEntry?> TryReadAsync(
            string filePath,
            DateTime lastWriteUtc,
            long length,
            string layoutKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string cachePath = GetCacheFilePath(filePath, layoutKey);
                if (!File.Exists(cachePath))
                    return null;

                await using var fileStream = File.OpenRead(cachePath);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                var envelope = await JsonSerializer.DeserializeAsync<DiskCacheEnvelope>(gzipStream, JsonOptions, cancellationToken);

                if (envelope?.Entry == null)
                    return null;

                if (envelope.Version != CacheFormatVersion ||
                    envelope.LastWriteUtc != lastWriteUtc ||
                    envelope.Length != length ||
                    !string.Equals(envelope.LayoutKey, layoutKey, StringComparison.Ordinal))
                {
                    return null;
                }

                return envelope.Entry;
            }
            catch
            {
                return null;
            }
        }

        public static async Task WriteAsync(
            string filePath,
            DateTime lastWriteUtc,
            long length,
            string layoutKey,
            DocumentPaginationCacheEntry entry,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectoryPath);

                string cachePath = GetCacheFilePath(filePath, layoutKey);
                string tempPath = cachePath + ".tmp";

                var envelope = new DiskCacheEnvelope
                {
                    Version = CacheFormatVersion,
                    SourcePath = Path.GetFullPath(filePath),
                    LastWriteUtc = lastWriteUtc,
                    Length = length,
                    LayoutKey = layoutKey,
                    Entry = entry
                };

                await using (var fileStream = File.Create(tempPath))
                await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Fastest))
                {
                    await JsonSerializer.SerializeAsync(gzipStream, envelope, JsonOptions, cancellationToken);
                }

                if (File.Exists(cachePath))
                    File.Delete(cachePath);

                File.Move(tempPath, cachePath);
            }
            catch
            {
            }
        }

        private static string GetCacheFilePath(string filePath, string layoutKey)
        {
            Directory.CreateDirectory(CacheDirectoryPath);
            string key = Path.GetFullPath(filePath) + "|" + layoutKey;
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
    }
}
