using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using E_Book.Models;
using Mammoth;
using RtfPipe;
using VersOne.Epub;

namespace E_Book.Services
{
    public static class DocumentContentService
    {
        public static async Task<ParsedReadingContent> ParseAsync(string filePath, CancellationToken cancellationToken = default)
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

        private static async Task<ParsedReadingContent> ParseTxtAsync(string filePath, CancellationToken cancellationToken)
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

            while (paragraphs.Count > 0 && string.IsNullOrWhiteSpace(paragraphs[^1]))
                paragraphs.RemoveAt(paragraphs.Count - 1);

            if (paragraphs.Count == 0)
                paragraphs.Add("(Empty TXT)");

            return new ParsedReadingContent
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                SourcePath = filePath,
                ContentKind = "txt",
                TxtParagraphs = paragraphs
            };
        }

        private static async Task<ParsedReadingContent> ParseHtmlAsync(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string html = await File.ReadAllTextAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            html = SimplifyForReader(html, removeImages: true);

            string displayTitle = Path.GetFileNameWithoutExtension(filePath);
            var sections = SplitHtmlIntoSections(html);

            if (sections.Count == 0)
                sections.Add("<p>(Empty HTML)</p>");

            var titles = sections
                .Select(ExtractDocumentHeadingForService)
                .ToList();

            return new ParsedReadingContent
            {
                Title = displayTitle,
                SourcePath = filePath,
                ContentKind = "html",
                RawHtmlChapters = sections,
                RawHtmlChapterKeys = sections
                    .Select((_, i) => $"{NormalizeKey(Path.GetFileName(filePath))}#sec{i}")
                    .ToList(),
                RawHtmlChapterTitles = titles
            };
        }

        private static async Task<ParsedReadingContent> ParseEpubAsync(string filePath, CancellationToken cancellationToken)
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

                string key =
                    GetStringProperty(item!, "Href") ??
                    GetStringProperty(item!, "FileName") ??
                    GetStringProperty(item!, "FilePath") ??
                    GetStringProperty(item!, "Path") ??
                    string.Empty;

                string title = ExtractBestTitleFromHtml(sanitized);

                if (string.IsNullOrWhiteSpace(title))
                    title = ExtractDocumentHeadingForService(sanitized);

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Chapter {sections.Count + 1}";
                }
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

        private static async Task<ParsedReadingContent> ParseDocxAsync(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string html = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var converter = new DocumentConverter();
                var result = converter.ConvertToHtml(filePath);
                return result?.Value ?? "<p>(Empty DOCX)</p>";
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            html = SimplifyForReader(html, removeImages: true);

            var sections = SplitHtmlIntoSections(html);
            if (sections.Count == 0)
                sections.Add("<p>(Empty DOCX)</p>");

            var titles = sections
                .Select(ExtractDocumentHeadingForService)
                .ToList();

            return new ParsedReadingContent
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                SourcePath = filePath,
                ContentKind = "html",
                RawHtmlChapters = sections,
                RawHtmlChapterKeys = sections
                    .Select((_, i) => $"{NormalizeKey(Path.GetFileName(filePath))}#sec{i}")
                    .ToList(),
                RawHtmlChapterTitles = titles
            };
        }

        private static async Task<ParsedReadingContent> ParseRtfAsync(string filePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string rtfText = await File.ReadAllTextAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            string html = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Rtf.ToHtml(rtfText);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            html = string.IsNullOrWhiteSpace(html) ? "<p>(Empty RTF)</p>" : html;
            html = SimplifyForReader(html, removeImages: true);

            var sections = SplitHtmlIntoSections(html);
            if (sections.Count == 0)
                sections.Add("<p>(Empty RTF)</p>");

            var titles = sections
                .Select(ExtractDocumentHeadingForService)
                .ToList();

            return new ParsedReadingContent
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                SourcePath = filePath,
                ContentKind = "html",
                RawHtmlChapters = sections,
                RawHtmlChapterKeys = sections
                    .Select((_, i) => $"{NormalizeKey(Path.GetFileName(filePath))}#sec{i}")
                    .ToList(),
                RawHtmlChapterTitles = titles
            };
        }

        private static void FlushEnglishBuffer(List<string> paragraphs, StringBuilder buffer)
        {
            if (buffer.Length <= 0)
                return;

            paragraphs.Add(buffer.ToString().Trim());
            buffer.Clear();
        }

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

        private static string? GetStringProperty(object obj, string propName)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null) return null;
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

            try { s = Uri.UnescapeDataString(s); } catch { }

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

        private static List<string> SplitHtmlIntoSections(string html)
        {
            var sections = new List<string>();

            if (string.IsNullOrWhiteSpace(html))
            {
                sections.Add("<p>(Empty)</p>");
                return sections;
            }

            html = UnwrapSingleContainer(html);

            var headingMatches = Regex.Matches(
                html,
                @"<h[1-2][^>]*>.*?</h[1-2]>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (headingMatches.Count >= 2)
            {
                for (int i = 0; i < headingMatches.Count; i++)
                {
                    int start = headingMatches[i].Index;
                    int end = (i < headingMatches.Count - 1)
                        ? headingMatches[i + 1].Index
                        : html.Length;

                    string section = html[start..end].Trim();
                    if (!string.IsNullOrWhiteSpace(section))
                        sections.Add(section);
                }

                return SplitLargeSections(sections);
            }

            var blocks = Regex.Matches(
                html,
                @"(<p[^>]*>.*?</p>|<blockquote[^>]*>.*?</blockquote>|<ul[^>]*>.*?</ul>|<ol[^>]*>.*?</ol>|<table[^>]*>.*?</table>|<hr[^>]*?/?>)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Select(m => m.Value.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (blocks.Count == 0)
            {
                blocks = Regex.Matches(
                    html,
                    @"(<div[^>]*>.*?</div>)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline)
                    .Select(m => m.Value.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            if (blocks.Count == 0)
            {
                sections.Add(html);
                return SplitLargeSections(sections);
            }

            const int blocksPerSection = 32;

            for (int i = 0; i < blocks.Count; i += blocksPerSection)
            {
                string section = string.Join(Environment.NewLine, blocks.Skip(i).Take(blocksPerSection));
                if (!string.IsNullOrWhiteSpace(section))
                    sections.Add(section);
            }

            return SplitLargeSections(sections);
        }

        private static List<string> SplitLargeSections(List<string> input)
        {
            var output = new List<string>();

            foreach (var section in input)
            {
                if (string.IsNullOrWhiteSpace(section))
                    continue;

                if (section.Length <= 7800)
                {
                    output.Add(section);
                    continue;
                }

                var blocks = Regex.Matches(
                    section,
                    @"(<p[^>]*>.*?</p>|<blockquote[^>]*>.*?</blockquote>|<ul[^>]*>.*?</ul>|<ol[^>]*>.*?</ol>|<table[^>]*>.*?</table>|<hr[^>]*?/?>)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline)
                    .Select(m => m.Value.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (blocks.Count == 0)
                {
                    output.Add(section);
                    continue;
                }

                var sb = new StringBuilder();

                foreach (var block in blocks)
                {
                    if (sb.Length > 0 && sb.Length + block.Length > 6200)
                    {
                        output.Add(sb.ToString().Trim());
                        sb.Clear();
                    }

                    sb.AppendLine(block);
                }

                if (sb.Length > 0)
                    output.Add(sb.ToString().Trim());
            }

            return output;
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
    }
}