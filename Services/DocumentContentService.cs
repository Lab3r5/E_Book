using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using E_Book.Models;
using RtfPipe;
using VersOne.Epub;

namespace E_Book.Services
{
    public static class DocumentContentService
    {
        #region Entry

        public static async Task<ParsedReadingContent> ParseAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is empty.", nameof(filePath));

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                ".txt" => await ParseTxtAsync(filePath, cancellationToken),
                ".html" or ".htm" => await ParseHtmlAsync(filePath, cancellationToken),
                ".epub" => await ParseEpubAsync(filePath, cancellationToken),
                ".docx" => await ParseDocxAsync(filePath, cancellationToken),
                ".rtf" => await ParseRtfAsync(filePath, cancellationToken),
                _ => throw new NotSupportedException($"Unsupported format: {ext}")
            };
        }

        #endregion

        #region TXT

        private static async Task<ParsedReadingContent> ParseTxtAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            var paragraphs = new List<string>();

            using var reader = new StreamReader(filePath);
            var englishBuffer = new StringBuilder();

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string line = await reader.ReadLineAsync() ?? string.Empty;
                string raw = line.Replace("\uFEFF", "");
                string trimmed = raw.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    FlushEnglishBuffer(paragraphs, englishBuffer);

                    if (paragraphs.Count == 0 || paragraphs[^1] != string.Empty)
                        paragraphs.Add(string.Empty);

                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^[_\-─—=·•\.]{5,}$"))
                {
                    FlushEnglishBuffer(paragraphs, englishBuffer);
                    paragraphs.Add(trimmed);
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*#{1,6}\s+"))
                {
                    FlushEnglishBuffer(paragraphs, englishBuffer);
                    paragraphs.Add(trimmed);
                    continue;
                }

                if (IsChineseChapterTitle(trimmed) || IsEnglishChapterTitle(trimmed))
                {
                    FlushEnglishBuffer(paragraphs, englishBuffer);
                    paragraphs.Add(trimmed);
                    continue;
                }

                bool looksEnglish = LooksLikeEnglishText(trimmed);

                if (looksEnglish)
                {
                    if (englishBuffer.Length > 0)
                        englishBuffer.Append(' ');

                    englishBuffer.Append(trimmed);
                }
                else
                {
                    FlushEnglishBuffer(paragraphs, englishBuffer);
                    paragraphs.Add(raw.TrimEnd());
                }
            }

            FlushEnglishBuffer(paragraphs, englishBuffer);
            return BuildTxtContent(Path.GetFileNameWithoutExtension(filePath), filePath, paragraphs, "(Empty TXT)");
        }

        private static void FlushEnglishBuffer(List<string> paragraphs, StringBuilder buffer)
        {
            if (buffer.Length <= 0)
                return;

            paragraphs.Add(buffer.ToString().Trim());
            buffer.Clear();
        }

        #endregion

        #region HTML

        private static async Task<ParsedReadingContent> ParseHtmlAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string html = await File.ReadAllTextAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string title = ExtractBestTitleFromHtml(html);
            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(filePath);

            var paragraphs = ConvertHtmlToParagraphs(html);
            return BuildTxtContent(title, filePath, paragraphs, "(Empty HTML)");
        }

        #endregion

        #region EPUB

        private static async Task<ParsedReadingContent> ParseEpubAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var book = await EpubReader.ReadBookAsync(filePath);
            cancellationToken.ThrowIfCancellationRequested();

            var readingOrder = book.ReadingOrder?.ToList() ?? new List<EpubLocalTextContentFile>();

            var sections = new List<string>();
            var keys = new List<string>();
            var titles = new List<string>();

            foreach (var item in readingOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var content = item?.Content;
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                string sanitized = SimplifyForReader(content, removeImages: true);

                if (string.IsNullOrWhiteSpace(StripHtmlTags(sanitized)))
                    continue;

                string key =
                    GetStringProperty(item!, "Href") ??
                    GetStringProperty(item!, "FileName") ??
                    GetStringProperty(item!, "FilePath") ??
                    GetStringProperty(item!, "Path") ??
                    string.Empty;

                string title = ExtractDocumentHeadingForService(sanitized);

                if (string.IsNullOrWhiteSpace(title))
                    title = ExtractBestTitleFromHtml(sanitized);

                if (string.IsNullOrWhiteSpace(title))
                    title = string.Empty;

                sections.Add(sanitized);
                keys.Add(NormalizeKey(key));
                titles.Add(title);
            }

            if (sections.Count == 0)
            {
                sections.Add("<p>(No readable chapters found)</p>");
                keys.Add(string.Empty);
                titles.Add("Start");
            }

            string displayTitle = book.Title ?? string.Empty;

            if (IsLikelyGarbledTitle(displayTitle))
                displayTitle = Path.GetFileNameWithoutExtension(filePath);

            return new ParsedReadingContent
            {
                Title = displayTitle,
                Author = book.Author ?? string.Empty,
                SourcePath = filePath,
                ContentKind = "html",
                RawHtmlChapters = sections,
                RawHtmlChapterKeys = keys,
                RawHtmlChapterTitles = titles
            };
        }

        #endregion

        #region DOCX

        private static async Task<ParsedReadingContent> ParseDocxAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extracted = await Task.Run(() => ExtractDocxParagraphs(filePath, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return BuildTxtContent(extracted.Title, filePath, extracted.Paragraphs, "(Empty DOCX)");
        }

        private static (string Title, List<string> Paragraphs) ExtractDocxParagraphs(
            string filePath,
            CancellationToken cancellationToken)
        {
            using var archive = ZipFile.OpenRead(filePath);
            var documentEntry = archive.GetEntry("word/document.xml")
                ?? throw new InvalidDataException("DOCX file does not contain word/document.xml.");

            XDocument documentXml;
            using (var stream = documentEntry.Open())
            {
                documentXml = XDocument.Load(stream, LoadOptions.None);
            }

            XDocument? coreXml = null;
            var coreEntry = archive.GetEntry("docProps/core.xml");
            if (coreEntry != null)
            {
                using var stream = coreEntry.Open();
                coreXml = XDocument.Load(stream, LoadOptions.None);
            }

            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XNamespace dc = "http://purl.org/dc/elements/1.1/";

            var paragraphs = new List<string>();

            foreach (var paragraph in documentXml.Descendants(w + "p"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string text = ExtractDocxParagraphText(paragraph, w);
                text = NormalizeExtractedParagraph(text);

                if (string.IsNullOrWhiteSpace(text))
                {
                    if (paragraphs.Count == 0 || paragraphs[^1] != string.Empty)
                        paragraphs.Add(string.Empty);

                    continue;
                }

                paragraphs.Add(text);
            }

            string title = coreXml?.Descendants(dc + "title").FirstOrDefault()?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                title = paragraphs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(filePath);

            return (title, paragraphs);
        }

        private static string ExtractDocxParagraphText(XElement paragraph, XNamespace wordNs)
        {
            var sb = new StringBuilder();

            foreach (var node in paragraph.Descendants())
            {
                if (node.Name == wordNs + "t")
                {
                    sb.Append(node.Value);
                }
                else if (node.Name == wordNs + "tab")
                {
                    sb.Append(' ');
                }
                else if (node.Name == wordNs + "br" || node.Name == wordNs + "cr")
                {
                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        #endregion

        #region RTF

        private static async Task<ParsedReadingContent> ParseRtfAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string rtfText = await File.ReadAllTextAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string html = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Rtf.ToHtml(new RtfSource(new StringReader(rtfText)), new RtfHtmlSettings());
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            html = string.IsNullOrWhiteSpace(html) ? "<p>(Empty RTF)</p>" : html;

            string title = ExtractBestTitleFromHtml(html);
            if (string.IsNullOrWhiteSpace(title))
                title = Path.GetFileNameWithoutExtension(filePath);

            var paragraphs = ConvertHtmlToParagraphs(html);
            return BuildTxtContent(title, filePath, paragraphs, "(Empty RTF)");
        }

        #endregion

        #region Text Detection / Chapter Detection

        private static bool LooksLikeEnglishText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int latin = 0;
            int cjk = 0;

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                    continue;

                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    latin++;
                else if (c >= 0x4E00 && c <= 0x9FFF)
                    cjk++;
            }

            return latin > cjk;
        }

        private static bool IsChineseChapterTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(
                text.Trim(),
                @"^第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇](\s|　|:|：|\.|、|-|—|_)*.{0,40}$");
        }

        private static bool IsEnglishChapterTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(
                text.Trim(),
                @"^(chapter|ch|part|section|prologue|epilogue)\s+(\d+|[ivxlcdm]+)\b.*$",
                RegexOptions.IgnoreCase);
        }

        #endregion

        #region Fast Text Conversion Helpers

        private static ParsedReadingContent BuildTxtContent(
            string title,
            string sourcePath,
            IEnumerable<string> paragraphs,
            string emptyPlaceholder)
        {
            var normalized = NormalizeParagraphs(paragraphs, emptyPlaceholder);

            return new ParsedReadingContent
            {
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(sourcePath) : title,
                SourcePath = sourcePath,
                ContentKind = "txt",
                TxtParagraphs = normalized
            };
        }

        private static List<string> NormalizeParagraphs(IEnumerable<string> paragraphs, string emptyPlaceholder)
        {
            var normalized = new List<string>();

            foreach (string paragraph in paragraphs)
            {
                string cleaned = NormalizeExtractedParagraph(paragraph);

                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    if (normalized.Count == 0 || normalized[^1] != string.Empty)
                        normalized.Add(string.Empty);

                    continue;
                }

                normalized.Add(cleaned);
            }

            while (normalized.Count > 0 && string.IsNullOrWhiteSpace(normalized[^1]))
                normalized.RemoveAt(normalized.Count - 1);

            if (normalized.Count == 0)
                normalized.Add(emptyPlaceholder);

            return normalized;
        }

        private static string NormalizeExtractedParagraph(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Replace("\r", "\n");
            text = HtmlEntityDecodeLite(text);
            text = Regex.Replace(text, @"\u00A0", " ");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n{2,}", "\n");
            return text.Trim();
        }

        private static List<string> ConvertHtmlToParagraphs(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return new List<string>();

            html = html.Replace("\r", "\n");
            html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<style\b[^<]*(?:(?!</style>)<[^<]*)*</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<head\b[^<]*(?:(?!</head>)<[^<]*)*</head>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</(p|div|section|article|blockquote|li|ul|ol|table|tr|td|h1|h2|h3|h4|h5|h6)>", "\n\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<(li|h1|h2|h3|h4|h5|h6)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<[^>]+>", " ", RegexOptions.Singleline);
            html = WebUtility.HtmlDecode(html);
            html = Regex.Replace(html, @"\u00A0", " ");
            html = Regex.Replace(html, @"[ \t]+", " ");
            html = Regex.Replace(html, @"\n[ \t]+", "\n");
            html = Regex.Replace(html, @"\n{3,}", "\n\n");

            return html
                .Split(new[] { "\n\n" }, StringSplitOptions.None)
                .Select(part => NormalizeExtractedParagraph(part))
                .ToList();
        }

        private static List<string> SplitPlainTextIntoParagraphs(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            text = text.Replace("\r", "\n");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text
                .Split(new[] { "\n\n" }, StringSplitOptions.None)
                .Select(part => NormalizeExtractedParagraph(part))
                .ToList();
        }

        private static string ConvertRtfToPlainText(string rtf, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rtf))
                return string.Empty;

            TryRegisterCodePages();

            var output = new StringBuilder(rtf.Length);
            var skipStack = new Stack<(bool SkipGroup, int UnicodeSkipCount, Encoding AnsiEncoding)>();
            bool skipGroup = false;
            bool markNextDestination = false;
            int unicodeSkipCount = 1;
            int pendingFallbackSkip = 0;
            Encoding ansiEncoding = GetEncodingOrFallback(1252);

            for (int i = 0; i < rtf.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                char c = rtf[i];

                if (pendingFallbackSkip > 0 && c != '{' && c != '}')
                {
                    pendingFallbackSkip--;

                    if (c == '\\')
                    {
                        i = SkipRtfControlToken(rtf, i);
                        continue;
                    }

                    continue;
                }

                if (c == '{')
                {
                    skipStack.Push((skipGroup, unicodeSkipCount, ansiEncoding));
                    continue;
                }

                if (c == '}')
                {
                    if (skipStack.Count > 0)
                    {
                        var state = skipStack.Pop();
                        skipGroup = state.SkipGroup;
                        unicodeSkipCount = state.UnicodeSkipCount;
                        ansiEncoding = state.AnsiEncoding;
                    }

                    markNextDestination = false;
                    continue;
                }

                if (c != '\\')
                {
                    if (!skipGroup)
                        output.Append(c);

                    continue;
                }

                if (i == rtf.Length - 1)
                    break;

                char next = rtf[i + 1];
                if (next == '\\' || next == '{' || next == '}')
                {
                    if (!skipGroup)
                        output.Append(next);

                    i++;
                    continue;
                }

                if (next == '\'')
                {
                    if (i + 3 < rtf.Length && !skipGroup)
                    {
                        string hex = rtf.Substring(i + 2, 2);
                        if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out byte value))
                            output.Append(ansiEncoding.GetString(new[] { value }));
                    }

                    i += 3;
                    continue;
                }

                if (next == '*')
                {
                    markNextDestination = true;
                    i++;
                    continue;
                }

                if (!char.IsLetter(next))
                {
                    if (!skipGroup)
                    {
                        switch (next)
                        {
                            case '~':
                                output.Append(' ');
                                break;
                            case '-':
                                output.Append('-');
                                break;
                            case '_':
                                output.Append('-');
                                break;
                        }
                    }

                    i++;
                    continue;
                }

                int wordStart = i + 1;
                int wordEnd = wordStart;
                while (wordEnd < rtf.Length && char.IsLetter(rtf[wordEnd]))
                    wordEnd++;

                string controlWord = rtf.Substring(wordStart, wordEnd - wordStart);
                int valueStart = wordEnd;
                bool hasNumericValue = false;
                int numericValue = 0;

                if (valueStart < rtf.Length && (rtf[valueStart] == '-' || char.IsDigit(rtf[valueStart])))
                {
                    hasNumericValue = true;
                    int sign = 1;
                    if (rtf[valueStart] == '-')
                    {
                        sign = -1;
                        valueStart++;
                    }

                    int digitsStart = valueStart;
                    while (valueStart < rtf.Length && char.IsDigit(rtf[valueStart]))
                        valueStart++;

                    if (valueStart > digitsStart)
                        numericValue = int.Parse(rtf.Substring(digitsStart, valueStart - digitsStart), System.Globalization.CultureInfo.InvariantCulture) * sign;
                }

                bool hasDelimiter = valueStart < rtf.Length && rtf[valueStart] == ' ';
                i = hasDelimiter ? valueStart : valueStart - 1;

                if (markNextDestination)
                {
                    skipGroup = true;
                    markNextDestination = false;
                }

                if (string.IsNullOrEmpty(controlWord))
                    continue;

                switch (controlWord)
                {
                    case "par":
                    case "line":
                        if (!skipGroup)
                            output.Append("\n\n");
                        break;
                    case "tab":
                        if (!skipGroup)
                            output.Append('\t');
                        break;
                    case "emdash":
                    case "endash":
                        if (!skipGroup)
                            output.Append('-');
                        break;
                    case "bullet":
                        if (!skipGroup)
                            output.Append("* ");
                        break;
                    case "lquote":
                    case "rquote":
                        if (!skipGroup)
                            output.Append('\'');
                        break;
                    case "ldblquote":
                    case "rdblquote":
                        if (!skipGroup)
                            output.Append('"');
                        break;
                    case "u":
                        if (!skipGroup && hasNumericValue)
                        {
                            int codePoint = numericValue < 0 ? numericValue + 65536 : numericValue;
                            output.Append(char.ConvertFromUtf32(Math.Clamp(codePoint, 0, 0x10FFFF)));
                            pendingFallbackSkip = unicodeSkipCount;
                        }
                        break;
                    case "uc":
                        if (hasNumericValue)
                            unicodeSkipCount = Math.Max(0, numericValue);
                        break;
                    case "ansicpg":
                        if (hasNumericValue)
                            ansiEncoding = GetEncodingOrFallback(numericValue, ansiEncoding);
                        break;
                    case "ansi":
                        ansiEncoding = GetEncodingOrFallback(1252, ansiEncoding);
                        break;
                    case "fonttbl":
                    case "colortbl":
                    case "stylesheet":
                    case "info":
                    case "pict":
                    case "object":
                    case "header":
                    case "footer":
                    case "footnote":
                    case "annotation":
                        skipGroup = true;
                        break;
                }
            }

            return WebUtility.HtmlDecode(output.ToString());
        }

        private static int SkipRtfControlToken(string rtf, int index)
        {
            if (index < 0 || index >= rtf.Length - 1 || rtf[index] != '\\')
                return index;

            int i = index + 1;

            if (i < rtf.Length && rtf[i] == '\'')
                return Math.Min(i + 2, rtf.Length - 1);

            if (i < rtf.Length && !char.IsLetter(rtf[i]))
                return i;

            while (i < rtf.Length && char.IsLetter(rtf[i]))
                i++;

            if (i < rtf.Length && (rtf[i] == '-' || char.IsDigit(rtf[i])))
            {
                if (rtf[i] == '-')
                    i++;

                while (i < rtf.Length && char.IsDigit(rtf[i]))
                    i++;
            }

            if (i < rtf.Length && rtf[i] == ' ')
                return i;

            return i - 1;
        }

        private static void TryRegisterCodePages()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
            }
        }

        private static Encoding GetEncodingOrFallback(int codePage, Encoding? fallback = null)
        {
            try
            {
                return Encoding.GetEncoding(codePage);
            }
            catch
            {
                return fallback ?? Encoding.UTF8;
            }
        }

        #endregion

        #region Reflection / Key Helpers

        private static string? GetStringProperty(object obj, string propName)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null)
                    return null;

                var v = p.GetValue(obj);
                return v?.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            s = s.Trim();

            try
            {
                s = Uri.UnescapeDataString(s);
            }
            catch
            {
            }

            s = s.Replace('\\', '/');

            int hash = s.IndexOf('#');
            if (hash >= 0)
                s = s[..hash];

            while (s.StartsWith("./", StringComparison.Ordinal))
                s = s[2..];

            while (s.StartsWith("../", StringComparison.Ordinal))
                s = s[3..];

            return s.Trim();
        }

        #endregion

        #region HTML / Entity Helpers

        private static string HtmlEntityDecodeLite(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&apos;", "'")
                .Replace("&#160;", " ")
                .Replace("&#xa0;", " ");
        }

        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return Regex.Replace(input, "<.*?>", string.Empty, RegexOptions.Singleline).Trim();
        }

        #endregion

        #region HTML Simplify

        private static string SimplifyForReader(string html, bool removeImages)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "<p>(Empty)</p>";

            html = html.Replace("\r", "\n");

            html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);
            html = Regex.Replace(html, @"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<style\b[^<]*(?:(?!</style>)<[^<]*)*</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<iframe\b[^<]*(?:(?!</iframe>)<[^<]*)*</iframe>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<svg\b[^<]*(?:(?!</svg>)<[^<]*)*</svg>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<canvas\b[^<]*(?:(?!</canvas>)<[^<]*)*</canvas>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<video\b[^<]*(?:(?!</video>)<[^<]*)*</video>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<audio\b[^<]*(?:(?!</audio>)<[^<]*)*</audio>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<form\b[^<]*(?:(?!</form>)<[^<]*)*</form>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<xml[^>]*>.*?</xml>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<meta\b[^>]*>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<link\b[^>]*>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<o:p>\s*</o:p>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<o:p>.*?</o:p>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<w:[^>]+>.*?</w:[^>]+>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            html = Regex.Replace(html, @"<v:[^>]+>.*?</v:[^>]+>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (removeImages)
                html = Regex.Replace(html, @"<img\b[^>]*>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"on\w+\s*=\s*['""][^'""]*['""]", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"javascript\s*:", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"style\s*=\s*['""][^'""]*['""]", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"class\s*=\s*['""][^'""]*['""]", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"\s+xmlns(:\w+)?\s*=\s*['""][^'""]*['""]", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"\s+lang\s*=\s*['""][^'""]*['""]", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"\s+dir\s*=\s*['""][^'""]*['""]", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<span>\s*</span>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<div>\s*</div>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<(span|font)[^>]*>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</(span|font)>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<(p|div|span)\b[^>]*>\s*</\1>", "", RegexOptions.IgnoreCase);

            html = Regex.Replace(html, @"<br\s*/?>", "</p><p>", RegexOptions.IgnoreCase);

            html = UnwrapSingleContainer(html).Trim();

            if (!Regex.IsMatch(html, @"<(p|h1|h2|h3|blockquote|ul|ol|table|hr)\b", RegexOptions.IgnoreCase))
            {
                string plain = Regex.Replace(html, "<.*?>", " ", RegexOptions.Singleline);
                plain = HtmlEntityDecodeLite(plain);
                plain = Regex.Replace(plain, @"[ \t]+", " ");
                plain = Regex.Replace(plain, @"\n{2,}", "\n\n").Trim();

                if (string.IsNullOrWhiteSpace(plain))
                    return "<p>(Empty)</p>";

                var paras = plain
                    .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => $"<p>{WebUtility.HtmlEncode(x.Trim())}</p>");

                return string.Join(Environment.NewLine, paras);
            }

            return html.Trim();
        }

        #endregion

        #region Title / Garbled Detection

        private static bool IsLikelyGarbledTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return true;

            title = title.Trim();

            if (title.Length > 80)
                return true;

            if (Regex.IsMatch(title, @"^[a-fA-F0-9]{16,}$"))
                return true;

            if (title.Contains("aceb48bf8d26f"))
                return true;

            return false;
        }

        #endregion

        #region Title Extraction

        private static string ExtractDocumentHeadingForService(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            string[] patterns =
            {
                @"<h1[^>]*>\s*(?<t>.*?)\s*</h1>",
                @"<h2[^>]*>\s*(?<t>.*?)\s*</h2>",
                @"<h3[^>]*>\s*(?<t>.*?)\s*</h3>",
                @"<p[^>]*>\s*(?<t>第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇](?:[\s　:：、\.\-—_]*[^<]{0,30})?)\s*</p>",
                @"<p[^>]*>\s*(?<t>(chapter|part|section)\s+(\d+|[ivxlcdm]+)(?:[:：\.\- ]*[^<]{0,30})?)\s*</p>"
            };

            foreach (var pat in patterns)
            {
                var m = Regex.Match(html, pat, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success)
                    continue;

                string t = Regex.Replace(m.Groups["t"].Value, "<.*?>", string.Empty);
                t = HtmlEntityDecodeLite(t).Trim();

                if (!string.IsNullOrWhiteSpace(t) && t.Length <= 60)
                    return t;
            }

            return string.Empty;
        }

        private static string ExtractBestTitleFromHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            string[] patterns =
            {
                @"<title[^>]*>\s*(?<t>.*?)\s*</title>",
                @"<h1[^>]*>\s*(?<t>.*?)\s*</h1>",
                @"<h2[^>]*>\s*(?<t>.*?)\s*</h2>",
                @"<h3[^>]*>\s*(?<t>.*?)\s*</h3>"
            };

            foreach (var pat in patterns)
            {
                var m = Regex.Match(html, pat, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success)
                    continue;

                string t = Regex.Replace(m.Groups["t"].Value, "<.*?>", string.Empty);
                t = HtmlEntityDecodeLite(t).Trim();

                if (!string.IsNullOrWhiteSpace(t))
                {
                    if (t.Length > 60)
                        t = t.Substring(0, 60).Trim() + "…";

                    return t;
                }
            }

            return string.Empty;
        }

        private static string UnwrapSingleContainer(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return html;

            string trimmed = html.Trim();

            var htmlMatch = Regex.Match(
                trimmed,
                @"^\s*<html[^>]*>.*?<body[^>]*>(?<inner>.*)</body>.*?</html>\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (htmlMatch.Success)
                trimmed = htmlMatch.Groups["inner"].Value.Trim();

            var bodyMatch = Regex.Match(
                trimmed,
                @"^\s*<body[^>]*>(?<inner>.*)</body>\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (bodyMatch.Success)
                trimmed = bodyMatch.Groups["inner"].Value.Trim();

            bool changed = true;

            while (changed)
            {
                changed = false;

                var singleContainer = Regex.Match(
                    trimmed,
                    @"^\s*<(div|section|article)[^>]*>(?<inner>.*)</\1>\s*$",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (singleContainer.Success)
                {
                    string inner = singleContainer.Groups["inner"].Value.Trim();

                    int blockCount = Regex.Matches(
                        inner,
                        @"<(p|div|blockquote|ul|ol|table|hr|h1|h2|h3)\b",
                        RegexOptions.IgnoreCase).Count;

                    if (blockCount >= 3)
                    {
                        trimmed = inner;
                        changed = true;
                    }
                }
            }

            return trimmed;
        }

        #endregion
    }
}



