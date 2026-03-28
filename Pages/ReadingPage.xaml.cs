using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using E_Book.Data;

using VersOne.Epub;
using Mammoth;
using RtfPipe;

namespace E_Book.Pages
{
    public partial class ReadingPage : ContentPage
    {
        private int currentPage = 0;

        // TXT mode
        private string[] lines = Array.Empty<string>();
        private int linesPerPage = 16;

        // HTML-based mode
        private List<string> htmlPages = new();
        private readonly List<string> htmlPageKeys = new(); // for epub href mapping

        public string FilePath { get; set; }
        private readonly Database dbHelper = new();

        // Font presets
        private readonly int[] fontSizes = new[] { 18, 22, 26 };
        private int fontIndex = 1;
        private int currentFontSize = 22;

        private string themeMode = "Light"; // Light | Dark

        // UI colors
        private static readonly Color DeepBlue = Color.FromArgb("#24145A");
        private static readonly Color OffWhite = Color.FromArgb("#FFFFFF");
        private static readonly Color SoftGray = Color.FromArgb("#ECEAF6");

        // Animation flags/params
        private bool isAnimating = false;
        private bool chromeVisible = true; // immersive mode toggle

        private const uint PageAnimMs = 180;
        private const double SlideDistance = 60;

        private const uint MenuAnimMs = 170;
        private const double MenuRestY = -68;
        private const double MenuHiddenY = -30;

        private const uint ChromeAnimMs = 160;

        private enum ReaderMode { TxtPaged, HtmlPaged, PdfExternal, Unknown }
        private ReaderMode mode = ReaderMode.Unknown;

        // ===================== TOC Model =====================

        private sealed class TocItem
        {
            public string Title { get; set; } = "";
            public int PageIndex { get; set; }
        }
        private readonly List<TocItem> tocItems = new();

        public ReadingPage(string filePath)
        {
            InitializeComponent();
            FilePath = filePath;

            // Swipe paging (guarded to avoid conflict with menu/popup)
            var swipeLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
            swipeLeft.Swiped += async (_, __) =>
            {
                if (isAnimating || Overlay.IsVisible || MenuPopup.IsVisible) return;
                await NextPageAsync();
            };

            var swipeRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
            swipeRight.Swiped += async (_, __) =>
            {
                if (isAnimating || Overlay.IsVisible || MenuPopup.IsVisible) return;
                await PrevPageAsync();
            };

            ReadingArea.GestureRecognizers.Add(swipeLeft);
            ReadingArea.GestureRecognizers.Add(swipeRight);
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            TitleLabel.Text = string.IsNullOrEmpty(FilePath)
                ? "Document"
                : Path.GetFileNameWithoutExtension(FilePath);

            // Load settings
            var readingSettings = await dbHelper.GetReadingSettingsAsync();

            int idx = Array.IndexOf(fontSizes, readingSettings.FontSize);
            fontIndex = idx >= 0 ? idx : 1;

            currentFontSize = fontSizes[fontIndex];
            fileContentLabel.FontSize = currentFontSize;

            UpdateLinesPerPage();
            UpdateFontSizeLabel();
            FontSlider.Value = fontIndex;

            themeMode = NormalizeTheme(readingSettings.BackgroundColor);
            ApplyTheme(themeMode);

            // Load file + progress
            if (!string.IsNullOrEmpty(FilePath))
            {
                await ShowLoadingAsync();

                try
                {
                    await LoadByTypeAsync(FilePath);

                    string fileName = Path.GetFileName(FilePath);
                    currentPage = await dbHelper.GetReadingProgressAsync(fileName);

                    ClampCurrentPage();
                    DisplayPage();
                    UpdateProgressUI();
                }
                finally
                {
                    await HideLoadingAsync();
                }
            }
        }

        // ===================== Loading overlay =====================

        private async Task ShowLoadingAsync()
        {
            LoadingIndicator.IsRunning = true;
            LoadingLayer.IsVisible = true;
            LoadingLayer.Opacity = 0;
            try { await LoadingLayer.FadeTo(1, 140, Easing.CubicOut); } catch { }
        }

        private async Task HideLoadingAsync()
        {
            try { await LoadingLayer.FadeTo(0, 140, Easing.CubicIn); } catch { }
            LoadingLayer.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }

        // ===================== Immersive toggle =====================

        private async void OnReadingAreaTapped(object sender, EventArgs e)
        {
            // If menu is open, tap should close it (keep consistent)
            if (Overlay.IsVisible || MenuPopup.IsVisible)
            {
                if (!isAnimating) await HideMenuAsync();
                return;
            }

            if (isAnimating) return;
            await ToggleChromeAsync();
        }

        private async Task ToggleChromeAsync()
        {
            isAnimating = true;
            try
            {
                chromeVisible = !chromeVisible;

                if (!chromeVisible)
                {
                    // Hide: fade out + slide a bit
                    await Task.WhenAll(
                        TopBar.FadeTo(0, ChromeAnimMs, Easing.CubicIn),
                        TopBar.TranslateTo(0, -10, ChromeAnimMs, Easing.CubicIn),

                        BottomBar.FadeTo(0, ChromeAnimMs, Easing.CubicIn),
                        BottomBar.TranslateTo(0, 10, ChromeAnimMs, Easing.CubicIn),

                        TocButtonContainer.FadeTo(0, ChromeAnimMs, Easing.CubicIn),
                        TocButtonContainer.TranslateTo(0, 10, ChromeAnimMs, Easing.CubicIn)
                    );

                    TopBar.IsVisible = false;
                    BottomBar.IsVisible = false;
                    TocButtonContainer.IsVisible = false;
                }
                else
                {
                    // Show
                    TopBar.IsVisible = true;
                    BottomBar.IsVisible = true;
                    TocButtonContainer.IsVisible = true;

                    TopBar.Opacity = 0; TopBar.TranslationY = -10;
                    BottomBar.Opacity = 0; BottomBar.TranslationY = 10;
                    TocButtonContainer.Opacity = 0; TocButtonContainer.TranslationY = 10;

                    await Task.WhenAll(
                        TopBar.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        TopBar.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut),

                        BottomBar.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        BottomBar.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut),

                        TocButtonContainer.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        TocButtonContainer.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut)
                    );
                }
            }
            finally
            {
                isAnimating = false;
            }
        }

        // ===================== Multi-format router =====================

        private async Task LoadByTypeAsync(string filePath)
        {
            lines = Array.Empty<string>();
            htmlPages.Clear();
            htmlPageKeys.Clear();
            tocItems.Clear();
            currentPage = 0;

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";

            try
            {
                switch (ext)
                {
                    case ".txt":
                        mode = ReaderMode.TxtPaged;
                        await LoadTxtAsync(filePath);
                        ShowTxtView();
                        BuildTxtToc();
                        break;

                    case ".html":
                    case ".htm":
                        mode = ReaderMode.HtmlPaged;
                        await LoadHtmlFileAsync(filePath);
                        ShowWebView();
                        BuildHtmlTocFromHtmlTitles();
                        break;

                    case ".epub":
                        mode = ReaderMode.HtmlPaged;
                        await LoadEpubAsync(filePath);
                        ShowWebView();
                        BuildEpubTocPreferNcxOrNav(filePath);
                        break;

                    case ".docx":
                        mode = ReaderMode.HtmlPaged;
                        await LoadDocxAsync(filePath);
                        ShowWebView();
                        BuildHtmlTocFromHtmlTitles();
                        break;

                    case ".rtf":
                        mode = ReaderMode.HtmlPaged;
                        await LoadRtfAsync(filePath);
                        ShowWebView();
                        BuildHtmlTocFromHtmlTitles();
                        break;

                    case ".pdf":
                        mode = ReaderMode.PdfExternal;
                        await OpenPdfExternalAsync(filePath);
                        ShowTxtView();
                        fileContentLabel.Text = "PDF opened in an external viewer.\n\nUse the Back button to return.";
                        break;

                    default:
                        mode = ReaderMode.Unknown;
                        ShowTxtView();
                        fileContentLabel.Text = $"Unsupported format: {ext}";
                        break;
                }
            }
            catch (Exception ex)
            {
                mode = ReaderMode.Unknown;
                ShowTxtView();
                fileContentLabel.Text = "Unable to load file: " + ex.Message;
            }
        }

        private void ShowTxtView()
        {
            fileContentLabel.IsVisible = true;
            ContentWebView.IsVisible = false;
        }

        private void ShowWebView()
        {
            fileContentLabel.IsVisible = false;
            ContentWebView.IsVisible = true;
        }

        // ===================== TXT =====================

        private async Task LoadTxtAsync(string filePath)
        {
            string fileContent = await File.ReadAllTextAsync(filePath);
            lines = Regex.Split(fileContent, @"\r\n|\n|\r"); // robust split
        }

        private void UpdateLinesPerPage()
        {
            if (currentFontSize == 18) linesPerPage = 20;
            else if (currentFontSize == 22) linesPerPage = 16;
            else linesPerPage = 12;
        }

        // ===================== HTML file =====================

        private async Task LoadHtmlFileAsync(string filePath)
        {
            var html = await File.ReadAllTextAsync(filePath);
            htmlPages = new List<string> { html };
            htmlPageKeys.Clear();
            htmlPageKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
        }

        // ===================== EPUB =====================

        private async Task LoadEpubAsync(string filePath)
        {
            var book = await EpubReader.ReadBookAsync(filePath);

            var readingOrder = book.ReadingOrder?.ToList() ?? new List<EpubLocalTextContentFile>();

            var chapters = new List<string>();
            htmlPageKeys.Clear();

            foreach (var item in readingOrder)
            {
                var content = item?.Content;
                if (string.IsNullOrWhiteSpace(content)) continue;

                chapters.Add(content);

                string key =
                    GetStringProperty(item!, "Href") ??
                    GetStringProperty(item!, "FileName") ??
                    GetStringProperty(item!, "FilePath") ??
                    GetStringProperty(item!, "Path") ??
                    "";

                htmlPageKeys.Add(NormalizeKey(key));
            }

            if (chapters.Count == 0)
            {
                chapters.Add("<p>(No readable chapters found)</p>");
                htmlPageKeys.Add("");
            }

            htmlPages = chapters;
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
            catch { return null; }
        }

        // ===================== DOCX =====================

        private Task LoadDocxAsync(string filePath)
        {
            var converter = new DocumentConverter();
            var result = converter.ConvertToHtml(filePath);

            var html = result?.Value;
            htmlPages = new List<string> { string.IsNullOrWhiteSpace(html) ? "<p>(Empty DOCX)</p>" : html! };

            htmlPageKeys.Clear();
            htmlPageKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
            return Task.CompletedTask;
        }

        // ===================== RTF =====================

        private async Task LoadRtfAsync(string filePath)
        {
            var rtfText = await File.ReadAllTextAsync(filePath);
            var html = Rtf.ToHtml(rtfText);
            htmlPages = new List<string> { string.IsNullOrWhiteSpace(html) ? "<p>(Empty RTF)</p>" : html };

            htmlPageKeys.Clear();
            htmlPageKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
        }

        // ===================== PDF (MVP: external open) =====================

        private async Task OpenPdfExternalAsync(string filePath)
        {
            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch { }
        }

        // ===================== Paging =====================

        private int GetTotalPages()
        {
            return mode switch
            {
                ReaderMode.TxtPaged => (lines == null || lines.Length == 0) ? 0
                    : (int)Math.Ceiling(lines.Length / (double)linesPerPage),

                ReaderMode.HtmlPaged => (htmlPages == null || htmlPages.Count == 0) ? 0 : htmlPages.Count,

                _ => 0
            };
        }

        private void ClampCurrentPage()
        {
            int total = GetTotalPages();
            if (total <= 0) { currentPage = 0; return; }
            if (currentPage < 0) currentPage = 0;
            if (currentPage > total - 1) currentPage = total - 1;
        }

        private void DisplayPage()
        {
            ClampCurrentPage();

            if (mode == ReaderMode.TxtPaged) { DisplayTxtPage(); return; }
            if (mode == ReaderMode.HtmlPaged) { DisplayHtmlPage(); return; }
        }

        private void DisplayTxtPage()
        {
            if (lines == null || lines.Length == 0) { fileContentLabel.Text = ""; return; }

            int start = Math.Max(0, currentPage * linesPerPage);
            if (start >= lines.Length)
            {
                currentPage = Math.Max(0, GetTotalPages() - 1);
                start = currentPage * linesPerPage;
                if (start >= lines.Length) start = Math.Max(0, lines.Length - 1);
            }

            int end = Math.Min(start + linesPerPage, lines.Length);
            int count = Math.Max(0, end - start);

            var pageLines = new string[count];
            Array.Copy(lines, start, pageLines, 0, count);

            fileContentLabel.Text = string.Join(Environment.NewLine, pageLines);
        }

        private void DisplayHtmlPage()
        {
            if (htmlPages == null || htmlPages.Count == 0)
            {
                ContentWebView.Source = new HtmlWebViewSource { Html = WrapHtml("<p>(Empty)</p>") };
                return;
            }

            var html = htmlPages[Math.Clamp(currentPage, 0, htmlPages.Count - 1)];
            ContentWebView.Source = new HtmlWebViewSource { Html = WrapHtml(html) };
        }

        private string WrapHtml(string bodyHtml)
        {
            bool dark = themeMode == "Dark";

            string bg = dark ? "#0B0B0F" : "#FFFFFF";
            string fg = dark ? "#EAE8F5" : "#2B2B33";

            int px = currentFontSize switch
            {
                18 => 16,
                22 => 18,
                _ => 20
            };

            return $@"
<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<style>
    body {{
        margin: 0;
        padding: 0;
        background: {bg};
        color: {fg};
        font-size: {px}px;
        line-height: 1.6;
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
        word-wrap: break-word;
        overflow-wrap: anywhere;
        word-break: break-word;
    }}
    .wrap {{ padding: 8px 2px; }}
    img {{ max-width: 100%; height: auto; }}
    a {{ color: #7B6CFF; }}
</style>
</head>
<body>
<div class='wrap'>
{bodyHtml}
</div>
</body>
</html>";
        }

        private void UpdateProgressUI()
        {
            int total = GetTotalPages();
            int current = total <= 0 ? 1 : currentPage + 1;

            ProgressText.Text = $"{Math.Max(1, current)} of {Math.Max(1, total)}";
            ReadingProgressBar.Progress = total <= 0 ? 0 : current / (double)total;
        }

        // ===================== TOC Builders =====================

        private void BuildTxtToc()
        {
            tocItems.Clear();
            if (lines == null || lines.Length == 0) return;

            var rxMd = new Regex(@"^\s*#{1,6}\s+(?<t>.+?)\s*$", RegexOptions.Compiled);
            var rxEn = new Regex(@"^\s*(chapter|ch)\s+(\d+|[ivxlcdm]+)\b[:.\- ]*\s*(?<t>.*)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var rxCn = new Regex(@"^\s*第\s*(?<n>[0-9一二三四五六七八九十百千两]+)\s*(?<u>章|节|回|卷|部|篇)\s*(?<t>.*)$",
                RegexOptions.Compiled);
            var rxNum = new Regex(@"^\s*(?<n>(\d+|[IVXLCDM]+))\s*([.)、])\s*(?<t>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var rxUnderline = new Regex(@"^\s*[-=]{3,}\s*$", RegexOptions.Compiled);

            int lastPage = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? title = null;

                // underline title
                if (i + 1 < lines.Length && rxUnderline.IsMatch((lines[i + 1] ?? "").Trim()) && line.Length >= 2)
                    title = line;

                if (title == null)
                {
                    var m = rxMd.Match(line);
                    if (m.Success) title = m.Groups["t"].Value.Trim();
                }

                if (title == null)
                {
                    var m = rxEn.Match(line);
                    if (m.Success)
                    {
                        var tail = m.Groups["t"].Value.Trim();
                        title = string.IsNullOrWhiteSpace(tail)
                            ? $"Chapter {m.Groups[2].Value}"
                            : $"Chapter {m.Groups[2].Value}: {tail}";
                    }
                }

                if (title == null)
                {
                    var m = rxCn.Match(line);
                    if (m.Success)
                    {
                        var n = m.Groups["n"].Value.Trim();
                        var u = m.Groups["u"].Value.Trim();
                        var tail = m.Groups["t"].Value.Trim();
                        title = string.IsNullOrWhiteSpace(tail)
                            ? $"第{n}{u}"
                            : $"第{n}{u} {tail}";
                    }
                }

                if (title == null)
                {
                    var m = rxNum.Match(line);
                    if (m.Success)
                    {
                        var n = m.Groups["n"].Value.Trim();
                        var t = m.Groups["t"].Value.Trim();
                        title = $"{n}. {t}";
                    }
                }

                if (title == null) continue;

                title = CleanTocTitle(title);

                int pageIndex = Math.Max(0, i / Math.Max(1, linesPerPage));
                if (pageIndex == lastPage) continue;

                tocItems.Add(new TocItem { Title = title, PageIndex = pageIndex });
                lastPage = pageIndex;
            }

            if (tocItems.Count == 0)
            {
                int total = GetTotalPages();
                for (int p = 0; p < total; p++)
                    tocItems.Add(new TocItem { Title = $"Page {p + 1}", PageIndex = p });
            }
        }

        private static string CleanTocTitle(string title)
        {
            title = HtmlEntityDecodeLite(title).Trim();
            title = Regex.Replace(title, @"\s{2,}", " ");
            if (title.Length > 60) title = title.Substring(0, 60).Trim() + "…";
            return title;
        }

        private void BuildHtmlTocFromHtmlTitles()
        {
            tocItems.Clear();
            if (htmlPages == null || htmlPages.Count == 0) return;

            for (int i = 0; i < htmlPages.Count; i++)
            {
                string html = htmlPages[i] ?? "";
                string title = ExtractHtmlTitle(html);
                if (string.IsNullOrWhiteSpace(title)) title = $"Chapter {i + 1}";
                tocItems.Add(new TocItem { Title = title, PageIndex = i });
            }
        }

        private void BuildEpubTocPreferNcxOrNav(string epubPath)
        {
            tocItems.Clear();

            var rawEntries = TryParseEpubTocFromZip(epubPath);

            if (rawEntries.Count > 0 && htmlPageKeys.Count == htmlPages.Count)
            {
                foreach (var (title, href) in rawEntries)
                {
                    int idx = MapHrefToPageIndex(href);
                    if (idx < 0) continue;

                    var cleaned = CleanTocTitle(title);
                    if (tocItems.Any(x => x.PageIndex == idx && x.Title.Equals(cleaned, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    tocItems.Add(new TocItem { Title = cleaned, PageIndex = idx });
                }
            }

            if (tocItems.Count == 0)
                BuildHtmlTocFromHtmlTitles();

            if (tocItems.Count == 0)
            {
                int total = GetTotalPages();
                for (int i = 0; i < total; i++)
                    tocItems.Add(new TocItem { Title = $"Chapter {i + 1}", PageIndex = i });
            }
        }

        // ✅ Enhanced epub href mapping
        private int MapHrefToPageIndex(string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return -1;

            // Normalize / decode / strip fragments
            string key = NormalizeKey(href);
            string fileOnly = NormalizeKey(Path.GetFileName(key));

            // 1) Prefer exact filename match
            if (!string.IsNullOrWhiteSpace(fileOnly))
            {
                for (int i = 0; i < htmlPageKeys.Count; i++)
                {
                    var kFile = NormalizeKey(Path.GetFileName(htmlPageKeys[i] ?? ""));
                    if (fileOnly.Equals(kFile, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            // 2) EndsWith / Contains match (handles OEBPS/Text/.. prefixes)
            for (int i = 0; i < htmlPageKeys.Count; i++)
            {
                var k = NormalizeKey(htmlPageKeys[i] ?? "");
                if (string.IsNullOrWhiteSpace(k)) continue;

                if (key.EndsWith(k, StringComparison.OrdinalIgnoreCase) || k.EndsWith(key, StringComparison.OrdinalIgnoreCase))
                    return i;

                if (!string.IsNullOrWhiteSpace(fileOnly) && k.IndexOf(fileOnly, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();

            // decode url encoding if any
            try { s = Uri.UnescapeDataString(s); } catch { }

            s = s.Replace('\\', '/');

            // remove fragment
            int hash = s.IndexOf('#');
            if (hash >= 0) s = s.Substring(0, hash);

            // remove leading ./ and ../ segments roughly
            while (s.StartsWith("./", StringComparison.Ordinal)) s = s.Substring(2);
            while (s.StartsWith("../", StringComparison.Ordinal)) s = s.Substring(3);

            return s.Trim();
        }

        private static List<(string title, string href)> TryParseEpubTocFromZip(string epubPath)
        {
            var result = new List<(string title, string href)>();

            try
            {
                using var zip = ZipFile.OpenRead(epubPath);

                // 1) toc.ncx (EPUB2)
                var ncxEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase));
                if (ncxEntry != null)
                {
                    using var s = ncxEntry.Open();
                    var doc = XDocument.Load(s);

                    var navPoints = doc.Descendants().Where(x => x.Name.LocalName == "navPoint").ToList();
                    foreach (var np in navPoints)
                    {
                        var textEl = np.Descendants().FirstOrDefault(x => x.Name.LocalName == "text");
                        var contentEl = np.Descendants().FirstOrDefault(x => x.Name.LocalName == "content");
                        var srcAttr = contentEl?.Attributes().FirstOrDefault(a => a.Name.LocalName == "src");

                        var title = (textEl?.Value ?? "").Trim();
                        var href = (srcAttr?.Value ?? "").Trim();

                        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(href))
                            result.Add((title, href));
                    }

                    if (result.Count > 0) return result;
                }

                // 2) nav.xhtml/nav.html (EPUB3)
                var navEntry = zip.Entries.FirstOrDefault(e =>
                    (e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase) ||
                     e.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                     e.FullName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                    && e.FullName.IndexOf("nav", StringComparison.OrdinalIgnoreCase) >= 0);

                if (navEntry != null)
                {
                    using var s = navEntry.Open();
                    using var sr = new StreamReader(s);
                    string navHtml = sr.ReadToEnd();

                    var navBlock = Regex.Match(navHtml,
                        @"<nav[^>]*(epub:type\s*=\s*['""]toc['""]|role\s*=\s*['""]doc-toc['""])[^>]*>(?<b>.*?)</nav>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    string block = navBlock.Success ? navBlock.Groups["b"].Value : navHtml;

                    var linkRx = new Regex(@"<a[^>]*href\s*=\s*['""](?<h>[^'""]+)['""][^>]*>(?<t>.*?)</a>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    foreach (Match m in linkRx.Matches(block))
                    {
                        string href = m.Groups["h"].Value.Trim();
                        string title = StripHtmlTags(m.Groups["t"].Value);
                        title = HtmlEntityDecodeLite(title).Trim();

                        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(href))
                            result.Add((title, href));
                    }
                }
            }
            catch { }

            return result;
        }

        private static string ExtractHtmlTitle(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";

            string[] patterns =
            {
                @"<h1[^>]*>(?<t>.*?)</h1>",
                @"<h2[^>]*>(?<t>.*?)</h2>",
                @"<h3[^>]*>(?<t>.*?)</h3>",
                @"<title[^>]*>(?<t>.*?)</title>"
            };

            foreach (var pat in patterns)
            {
                var m = Regex.Match(html, pat, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success) continue;

                string t = StripHtmlTags(m.Groups["t"].Value);
                t = HtmlEntityDecodeLite(t).Trim();

                if (t.Length > 0)
                {
                    if (t.Length > 60) t = t.Substring(0, 60).Trim() + "…";
                    return t;
                }
            }

            return "";
        }

        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return Regex.Replace(input, "<.*?>", string.Empty, RegexOptions.Singleline).Trim();
        }

        private static string HtmlEntityDecodeLite(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&#39;", "'")
                .Replace("&apos;", "'");
        }

        // ===================== TOC Click =====================

        private async void OnTocClicked(object sender, EventArgs e)
        {
            if (isAnimating) return;

            int total = GetTotalPages();
            if (total <= 1)
            {
                await DisplayAlert("Table of Contents", "No additional pages/chapters available.", "OK");
                return;
            }

            // Ensure TOC exists
            if (tocItems.Count == 0)
            {
                if (mode == ReaderMode.TxtPaged) BuildTxtToc();
                else if (mode == ReaderMode.HtmlPaged)
                {
                    if (Path.GetExtension(FilePath)?.Equals(".epub", StringComparison.OrdinalIgnoreCase) == true)
                        BuildEpubTocPreferNcxOrNav(FilePath);
                    else
                        BuildHtmlTocFromHtmlTitles();
                }
            }

            if (tocItems.Count == 0)
            {
                await DisplayAlert("Table of Contents", "No TOC items found.", "OK");
                return;
            }

            const int MaxQuickItems = 22;
            var show = tocItems.Take(MaxQuickItems).ToList();

            var optionToPage = new Dictionary<string, int>();
            var options = new List<string>();

            for (int i = 0; i < show.Count; i++)
            {
                var item = show[i];
                string opt = $"{i + 1}. {item.Title}  (p.{item.PageIndex + 1}/{total})";
                optionToPage[opt] = item.PageIndex;
                options.Add(opt);
            }

            options.Add("More / Search / Go to…");

            string choice = await DisplayActionSheet($"Table of Contents (Current: p.{currentPage + 1}/{total})", "Cancel", null, options.ToArray());
            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            int targetIndex = -1;

            if (choice == "More / Search / Go to…")
            {
                string? keyword = await DisplayPromptAsync(
                    "Search TOC (optional)",
                    "Enter a keyword to search chapters, or leave blank to go to a page number:",
                    "Next",
                    "Cancel");

                if (keyword == null) return;

                keyword = keyword.Trim();

                if (keyword.Length > 0)
                {
                    var matches = tocItems
                        .Where(x => x.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        .Take(25)
                        .ToList();

                    if (matches.Count == 0)
                    {
                        await DisplayAlert("No match", "No chapters matched your keyword.", "OK");
                        return;
                    }

                    var matchOptions = new List<string>();
                    var matchMap = new Dictionary<string, int>();

                    for (int i = 0; i < matches.Count; i++)
                    {
                        var it = matches[i];
                        string opt = $"{i + 1}. {it.Title}  (p.{it.PageIndex + 1}/{total})";
                        matchMap[opt] = it.PageIndex;
                        matchOptions.Add(opt);
                    }

                    string picked = await DisplayActionSheet("Search results", "Cancel", null, matchOptions.ToArray());
                    if (string.IsNullOrWhiteSpace(picked) || picked == "Cancel") return;

                    if (matchMap.TryGetValue(picked, out int pidx))
                        targetIndex = Math.Clamp(pidx, 0, total - 1);
                }
                else
                {
                    string label = (mode == ReaderMode.HtmlPaged) ? "chapter" : "page";
                    string? input = await DisplayPromptAsync(
                        "Go to",
                        $"Enter a {label} number (1 - {total})",
                        "Go",
                        "Cancel",
                        keyboard: Keyboard.Numeric);

                    if (string.IsNullOrWhiteSpace(input)) return;
                    if (!int.TryParse(input.Trim(), out int num))
                    {
                        await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
                        return;
                    }

                    num = Math.Clamp(num, 1, total);
                    targetIndex = num - 1;
                }
            }
            else
            {
                if (optionToPage.TryGetValue(choice, out int idx))
                    targetIndex = Math.Clamp(idx, 0, total - 1);
            }

            if (targetIndex < 0 || targetIndex == currentPage) return;

            currentPage = targetIndex;
            DisplayPage();
            UpdateProgressUI();
            await SaveReadingProgress();
        }

        // ===================== Persistence =====================

        private async Task SaveReadingProgress()
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            string fileName = Path.GetFileName(FilePath);
            await dbHelper.SaveReadingProgressAsync(fileName, currentPage);
        }

        private async Task SaveCurrentReadingSettings()
        {
            await dbHelper.SaveReadingSettingsAsync(currentFontSize, themeMode);
        }

        // ===================== Animations =====================

        private async Task AnimatePageChangeAsync(int direction)
        {
            if (isAnimating) return;
            isAnimating = true;

            try
            {
                VisualElement target = (mode == ReaderMode.HtmlPaged) ? (VisualElement)ContentWebView : fileContentLabel;

                double outX = direction > 0 ? -SlideDistance : SlideDistance;

                await Task.WhenAll(
                    target.TranslateTo(outX, 0, PageAnimMs, Easing.CubicIn),
                    target.FadeTo(0, PageAnimMs, Easing.CubicIn)
                );

                DisplayPage();
                UpdateProgressUI();

                double inX = direction > 0 ? SlideDistance : -SlideDistance;
                target.TranslationX = inX;

                await Task.WhenAll(
                    target.TranslateTo(0, 0, PageAnimMs, Easing.CubicOut),
                    target.FadeTo(1, PageAnimMs, Easing.CubicOut)
                );
            }
            finally
            {
                fileContentLabel.TranslationX = 0;
                fileContentLabel.Opacity = 1;

                ContentWebView.TranslationX = 0;
                ContentWebView.Opacity = 1;

                isAnimating = false;
            }
        }

        private async Task NextPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0) return;
            if (currentPage >= total - 1) return;

            currentPage++;
            await AnimatePageChangeAsync(+1);
            await SaveReadingProgress();
        }

        private async Task PrevPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0) return;
            if (currentPage <= 0) return;

            currentPage--;
            await AnimatePageChangeAsync(-1);
            await SaveReadingProgress();
        }

        // ===================== UI interactions =====================

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnButtonPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
            {
                try { await v.ScaleTo(0.96, 80, Easing.CubicOut); } catch { }
            }
        }

        private async void OnButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
            {
                try { await v.ScaleTo(1.0, 110, Easing.CubicOut); } catch { }
            }
        }

        private async void OnMenuClicked(object sender, EventArgs e)
        {
            if (isAnimating) return;

            if (!MenuPopup.IsVisible)
                await ShowMenuAsync();
            else
                await HideMenuAsync();
        }

        private async void OnDismissTapped(object sender, EventArgs e)
        {
            if (isAnimating) return;
            await HideMenuAsync();
        }

        private async Task ShowMenuAsync()
        {
            if (isAnimating) return;
            isAnimating = true;

            try
            {
                FontSlider.Value = fontIndex;
                UpdateFontSizeLabel();
                UpdateFontButtonStyles();
                UpdateThemeButtonStyles();

                Overlay.IsVisible = true;
                Overlay.Opacity = 0;

                MenuPopup.IsVisible = true;
                MenuPopup.Opacity = 0;
                MenuPopup.Scale = 0.98;
                MenuPopup.TranslationY = MenuHiddenY;

                await Task.WhenAll(
                    Overlay.FadeTo(1, 160, Easing.CubicOut),
                    MenuPopup.FadeTo(1, MenuAnimMs, Easing.CubicOut),
                    MenuPopup.ScaleTo(1.0, MenuAnimMs, Easing.CubicOut),
                    MenuPopup.TranslateTo(0, MenuRestY, MenuAnimMs, Easing.CubicOut)
                );
            }
            finally
            {
                isAnimating = false;
            }
        }

        private async Task HideMenuAsync()
        {
            if (!MenuPopup.IsVisible)
            {
                Overlay.IsVisible = false;
                Overlay.Opacity = 0;
                return;
            }

            if (isAnimating) return;
            isAnimating = true;

            try
            {
                await Task.WhenAll(
                    Overlay.FadeTo(0, 140, Easing.CubicIn),
                    MenuPopup.FadeTo(0, 140, Easing.CubicIn),
                    MenuPopup.ScaleTo(0.98, 140, Easing.CubicIn),
                    MenuPopup.TranslateTo(0, MenuHiddenY, 140, Easing.CubicIn)
                );

                MenuPopup.IsVisible = false;
                Overlay.IsVisible = false;
            }
            finally
            {
                Overlay.Opacity = 0;
                MenuPopup.Opacity = 0;
                MenuPopup.Scale = 0.98;
                MenuPopup.TranslationY = MenuRestY;
                isAnimating = false;
            }
        }

        // ===================== Font controls =====================

        private async void OnFontPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out var idx))
            {
                fontIndex = Math.Clamp(idx, 0, fontSizes.Length - 1);
                FontSlider.Value = fontIndex;
                await ApplyFontAsync();
            }
        }

        private async void OnFontSliderChanged(object sender, ValueChangedEventArgs e)
        {
            int idx = (int)Math.Round(e.NewValue);
            idx = Math.Clamp(idx, 0, fontSizes.Length - 1);
            if (idx == fontIndex) return;

            fontIndex = idx;
            await ApplyFontAsync();
        }

        private async Task ApplyFontAsync()
        {
            currentFontSize = fontSizes[fontIndex];
            fileContentLabel.FontSize = currentFontSize;

            UpdateLinesPerPage();

            if (mode == ReaderMode.TxtPaged)
                BuildTxtToc();

            ClampCurrentPage();
            DisplayPage();
            UpdateProgressUI();
            UpdateFontSizeLabel();

            UpdateFontButtonStyles();

            if (mode == ReaderMode.HtmlPaged && htmlPages.Count > 0)
                DisplayHtmlPage();

            await SaveCurrentReadingSettings();
        }

        private void UpdateFontSizeLabel()
        {
            FontSizeLabel.Text = fontIndex switch
            {
                0 => "Small",
                1 => "Medium",
                _ => "Large"
            };
        }

        private void UpdateFontButtonStyles()
        {
            bool dark = themeMode == "Dark";

            StyleFontButton(SmallBtn, fontIndex == 0, dark);
            StyleFontButton(MediumBtn, fontIndex == 1, dark);
            StyleFontButton(LargeBtn, fontIndex == 2, dark);
        }

        private void StyleFontButton(Button b, bool selected, bool dark)
        {
            b.BorderWidth = 1;
            b.BorderColor = dark ? Color.FromArgb("#3B3560") : Color.FromArgb("#D6D2EA");

            if (!dark)
            {
                b.BackgroundColor = selected ? DeepBlue : OffWhite;
                b.TextColor = selected ? OffWhite : DeepBlue;
            }
            else
            {
                b.BackgroundColor = selected ? OffWhite : DeepBlue;
                b.TextColor = selected ? DeepBlue : OffWhite;
            }
        }

        // ===================== Theme =====================

        private static string NormalizeTheme(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return "Light";
            stored = stored.Trim();

            if (stored.Equals("Light", StringComparison.OrdinalIgnoreCase)) return "Light";
            if (stored.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
            return "Light";
        }

        private void ApplyTheme(string mode)
        {
            bool dark = mode == "Dark";

            var pageBg = dark ? Color.FromArgb("#0B0B0F") : Colors.White;

            this.BackgroundColor = pageBg;
            ReadingArea.BackgroundColor = pageBg;

            BackButton.TextColor = dark ? OffWhite : Color.FromArgb("#6C6883");
            MenuButton.TextColor = dark ? OffWhite : Color.FromArgb("#6C6883");
            TitleLabel.TextColor = dark ? OffWhite : Color.FromArgb("#2B2B33");

            fileContentLabel.TextColor = dark ? Color.FromArgb("#EAE8F5") : Color.FromArgb("#4B475A");
            ProgressText.TextColor = dark ? Color.FromArgb("#C9C7D6") : Color.FromArgb("#8C8A9A");

            if (dark)
            {
                ReadingProgressBar.ProgressColor = Color.FromArgb("#C7B9FF");
                ReadingProgressBar.BackgroundColor = Color.FromArgb("#33FFFFFF");
            }
            else
            {
                ReadingProgressBar.ProgressColor = Color.FromArgb("#5E4DB2");
                ReadingProgressBar.BackgroundColor = Color.FromArgb("#E5E3F2");
            }

            // TOC floating button theme
            TocButtonContainer.BackgroundColor = dark
                ? Color.FromArgb("#2B2B33CC")
                : Color.FromArgb("#FFFFFFCC");
            TocButton.TextColor = dark ? OffWhite : DeepBlue;

            // Loading overlay theme
            LoadingLayer.BackgroundColor = dark ? Color.FromArgb("#660B0B0F") : Color.FromArgb("#55FFFFFF");
            LoadingText.TextColor = dark ? OffWhite : Color.FromArgb("#2B2B33");

            MenuPopup.BackgroundColor = dark ? Color.FromArgb("#2B2B33") : OffWhite;
            MenuPopup.Stroke = dark ? Color.FromArgb("#33FFFFFF") : Color.FromArgb("#22000000");

            var popupText = dark ? OffWhite : Color.FromArgb("#2B2B33");
            var popupSub = dark ? Color.FromArgb("#C9C7D6") : Color.FromArgb("#8C8A9A");

            PopupTitle1.TextColor = popupText;
            PopupTitle2.TextColor = popupText;
            FontSizeLabel.TextColor = popupSub;
            SmallLabel.TextColor = popupSub;
            LargeLabel.TextColor = popupSub;

            if (dark)
            {
                FontSlider.MinimumTrackColor = Color.FromArgb("#C7B9FF");
                FontSlider.MaximumTrackColor = Color.FromArgb("#8F8AA8");
                FontSlider.ThumbColor = Colors.White;
            }
            else
            {
                FontSlider.MinimumTrackColor = Color.FromArgb("#5E4DB2");
                FontSlider.MaximumTrackColor = Color.FromArgb("#D8D5EA");
                FontSlider.ThumbColor = Color.FromArgb("#5E4DB2");
            }

            if (this.mode == ReaderMode.HtmlPaged && htmlPages.Count > 0)
                DisplayHtmlPage();

            UpdateFontButtonStyles();
            UpdateThemeButtonStyles();
        }

        private async void OnThemeClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string mode)
            {
                themeMode = mode == "Dark" ? "Dark" : "Light";
                ApplyTheme(themeMode);
                await SaveCurrentReadingSettings();
            }
        }

        private void UpdateThemeButtonStyles()
        {
            bool dark = themeMode == "Dark";

            if (!dark)
            {
                LightBtn.BackgroundColor = DeepBlue;
                LightBtn.TextColor = OffWhite;

                DarkBtn.BackgroundColor = SoftGray;
                DarkBtn.TextColor = DeepBlue;

                LightBtn.BorderWidth = 0;
                DarkBtn.BorderWidth = 1;
                DarkBtn.BorderColor = Color.FromArgb("#D6D2EA");
            }
            else
            {
                DarkBtn.BackgroundColor = OffWhite;
                DarkBtn.TextColor = DeepBlue;

                LightBtn.BackgroundColor = Color.FromArgb("#3B3560");
                LightBtn.TextColor = OffWhite;

                DarkBtn.BorderWidth = 0;
                LightBtn.BorderWidth = 1;
                LightBtn.BorderColor = Color.FromArgb("#4B446E");
            }
        }
    }
}