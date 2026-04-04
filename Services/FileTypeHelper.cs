using System;
using System.IO;

namespace E_Book.Services
{
    public static class FileTypeHelper
    {
        public static bool IsPdf(string? path)
        {
            var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext == ".pdf";
        }

        public static bool IsImage(string? path)
        {
            var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".webp";
        }

        public static bool IsTextReaderType(string? path)
        {
            var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext is ".txt" or ".epub" or ".html" or ".htm" or ".docx" or ".rtf";
        }

        public static string GetRouteByPath(string? path)
        {
            if (IsImage(path))
                return AppShell.RouteImageReader;

            if (IsPdf(path))
                return AppShell.RoutePdfReader;

            return AppShell.RouteReading;
        }
    }
}