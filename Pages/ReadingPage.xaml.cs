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
    // ✅ Shell route: reading?filePath=...
    [QueryProperty(nameof(FilePath), "filePath")]
    public partial class ReadingPage : ContentPage
    {
        // -----------------------------
        // Paging state
        // -----------------------------
        private int currentPage = 0;

        // TXT mode paging
        private string[] lines = Array.Empty<string>();
        private int linesPerPage = 16;

        // HTML-based mode paging (EPUB/DOCX/RTF/HTML)
        private List<string> htmlPages = new();
        private readonly List<string> htmlPageKeys = new(); // Used for EPUB href mapping

        // ✅ QueryProperty target
        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set => _filePath = Uri.UnescapeDataString(value ?? string.Empty);
        }

        private readonly Database dbHelper = new();

        // Font presets
        private readonly int[] fontSizes = new[] { 18, 22, 26 };
        private int fontIndex = 1;
        private int currentFontSize = 22;

        private string themeMode = "Light"; // Light | Dark

        // Button styling colors (match your design)
        private static readonly Color DeepBlue = Color.FromArgb("#24145A");
        private static readonly Color OffWhite = Color.FromArgb("#FFFFFF");

        // Animation flags
        private bool isAnimating = false;
        private bool chromeVisible = true;

        // Page animation
        private const uint PageAnimMs = 180;
        private const double SlideDistance = 60;

        // Menu animation
        private const uint MenuAnimMs = 170;
        private const double MenuRestY = -68;
        private const double MenuHiddenY = -30;

        // Chrome animation
        private const uint ChromeAnimMs = 160;

        private enum ReaderMode { TxtPaged, HtmlPaged, PdfExternal, Unknown }
        private ReaderMode mode = ReaderMode.Unknown;

        // Avoid double-loading when page re-appears
        private bool _initialized = false;

        // -----------------------------
        // TOC model
        // -----------------------------
        private sealed class TocItem
        {
            public string Title { get; set; } = "";
            public int PageIndex { get; set; }
        }
        private readonly List<TocItem> tocItems = new();

        // -----------------------------
        // Constructor
        // -----------------------------
        public ReadingPage()
        {
            InitializeComponent();
            SetupGestures();
        }

        private void SetupGestures()
        {
            // Swipe paging (guarded to avoid conflict with popup/menu)
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

        // -----------------------------
        // Lifecycle
        // -----------------------------
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized) return;
            _initialized = true;

            // Title
            TitleLabel.Text = string.IsNullOrEmpty(FilePath)
                ? "Document"
                : Path.GetFileNameWithoutExtension(FilePath);

            // Load saved settings
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
                try
                {
                    await LoadByTypeAsync(FilePath);

                    string fileName = Path.GetFileName(FilePath);
                    currentPage = await dbHelper.GetReadingProgressAsync(fileName);

                    ClampCurrentPage();
                    DisplayPage();
                    UpdateProgressUI();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                }
            }
            else
            {
                ShowTxtView();
                fileContentLabel.Text = "No file selected.";
                UpdateProgressUI();
            }
        }

        // -----------------------------
        // Immersive toggle (Tap)
        // -----------------------------
        private async void OnReadingAreaTapped(object sender, EventArgs e)
        {
            // If menu is open, tap closes it
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

        // -----------------------------
        // Multi-format router
        // -----------------------------
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

        // -----------------------------
        // TXT
        // -----------------------------
        private async Task LoadTxtAsync(string filePath)
        {
            string fileContent = await File.ReadAllTextAsync(filePath);
            lines = Regex.Split(fileContent, @"\r\n|\n|\r");
        }

        private void UpdateLinesPerPage()
        {
            if (currentFontSize == 18) linesPerPage = 20;
            else if (currentFontSize == 22) linesPerPage = 16;
            else linesPerPage = 12;
        }

        // -----------------------------
        // HTML file
        // -----------------------------
        private async Task LoadHtmlFileAsync(string filePath)
        {
            var html = await File.ReadAllTextAsync(filePath);
            htmlPages = new List<string> { html };
            htmlPageKeys.Clear();
            htmlPageKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
        }

        // -----------------------------
        // EPUB
        // -----------------------------
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

        // -----------------------------
        // DOCX
        // -----------------------------
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

        // -----------------------------
        // RTF
        // -----------------------------
        private async Task LoadRtfAsync(string filePath)
        {
            var rtfText = await File.ReadAllTextAsync(filePath);
            var html = Rtf.ToHtml(rtfText);

            htmlPages = new List<string> { string.IsNullOrWhiteSpace(html) ? "<p>(Empty RTF)</p>" : html };

            htmlPageKeys.Clear();
            htmlPageKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
        }

        // -----------------------------
        // PDF external
        // -----------------------------
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

        // -----------------------------
        // Paging
        // -----------------------------
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
            currentPage = Math.Clamp(currentPage, 0, total - 1);
        }

        private void DisplayPage()
        {
            ClampCurrentPage();

            if (mode == ReaderMode.TxtPaged) { DisplayTxtPage(); return; }
            if (mode == ReaderMode.HtmlPaged) { DisplayHtmlPage(); return; }
        }

        private void DisplayTxtPage()
        {
            if (lines == null || lines.Length == 0)
            {
                fileContentLabel.Text = "";
                return;
            }

            int start = Math.Max(0, currentPage * linesPerPage);
            int end = Math.Min(start + linesPerPage, lines.Length);

            if (start >= lines.Length)
            {
                currentPage = Math.Max(0, GetTotalPages() - 1);
                start = currentPage * linesPerPage;
                end = Math.Min(start + linesPerPage, lines.Length);
            }

            var pageLines = lines.Skip(start).Take(Math.Max(0, end - start));
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

            string bg = dark ? "#0F0F12" : "#F7F6FB";
            string fg = dark ? "#EDEAF6" : "#3E3A4A";

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
            ReadingProgressBar.Progress = total <= 1 ? 1 : (current / (double)total);
        }

        // -----------------------------
        // TOC (ActionSheet)
        // -----------------------------
        private async void OnTocClicked(object sender, EventArgs e)
        {
            int total = GetTotalPages();
            if (total <= 1)
            {
                await DisplayAlert("TOC", "No additional pages/chapters available.", "OK");
                return;
            }

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
                await DisplayAlert("TOC", "No TOC items found.", "OK");
                return;
            }

            const int MaxItems = 22;
            var show = tocItems.Take(MaxItems).ToList();

            var map = new Dictionary<string, int>();
            var options = new List<string>();

            for (int i = 0; i < show.Count; i++)
            {
                var item = show[i];
                string opt = $"{i + 1}. {item.Title}  (p.{item.PageIndex + 1}/{total})";
                map[opt] = item.PageIndex;
                options.Add(opt);
            }

            options.Add("Go to page…");

            string choice = await DisplayActionSheet($"TOC (Current: p.{currentPage + 1}/{total})", "Cancel", null, options.ToArray());
            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            int target = -1;

            if (choice == "Go to page…")
            {
                // Compatible prompt
                string? input = await DisplayPromptAsync(
                    title: "Go to",
                    message: $"Enter page/chapter number (1 - {total})",
                    accept: "Go",
                    cancel: "Cancel"
                );

                if (string.IsNullOrWhiteSpace(input)) return;

                if (!int.TryParse(input.Trim(), out int num))
                {
                    await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
                    return;
                }

                num = Math.Clamp(num, 1, total);
                target = num - 1;
            }
            else if (map.TryGetValue(choice, out int idx))
            {
                target = Math.Clamp(idx, 0, total - 1);
            }

            if (target < 0 || target == currentPage) return;

            currentPage = target;
            DisplayPage();
            UpdateProgressUI();
            await SaveReadingProgress();
        }

        // -----------------------------
        // TOC builders
        // -----------------------------
        private void BuildTxtToc()
        {
            tocItems.Clear();
            if (lines == null || lines.Length == 0) return;

            var rxMd = new Regex(@"^\s*#{1,6}\s+(?<t>.+?)\s*$", RegexOptions.Compiled);
            var rxEn = new Regex(@"^\s*(chapter|ch)\s+(\d+|[ivxlcdm]+)\b[:.\- ]*\s*(?<t>.*)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            int lastPage = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                string? title = null;

                var m1 = rxMd.Match(line);
                if (m1.Success) title = m1.Groups["t"].Value.Trim();

                if (title == null)
                {
                    var m2 = rxEn.Match(line);
                    if (m2.Success)
                    {
                        var tail = m2.Groups["t"].Value.Trim();
                        title = string.IsNullOrWhiteSpace(tail)
                            ? $"Chapter {m2.Groups[2].Value}"
                            : $"Chapter {m2.Groups[2].Value}: {tail}";
                    }
                }

                if (title == null) continue;

                title = title.Trim();
                if (title.Length > 60) title = title.Substring(0, 60).Trim() + "…";

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

            var raw = TryParseEpubTocFromZip(epubPath);

            if (raw.Count > 0 && htmlPageKeys.Count == htmlPages.Count)
            {
                foreach (var (title, href) in raw)
                {
                    int idx = MapHrefToPageIndex(href);
                    if (idx < 0) continue;

                    string t = HtmlEntityDecodeLite(title).Trim();
                    if (t.Length > 60) t = t.Substring(0, 60).Trim() + "…";

                    if (tocItems.Any(x => x.PageIndex == idx && x.Title.Equals(t, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    tocItems.Add(new TocItem { Title = t, PageIndex = idx });
                }
            }

            if (tocItems.Count == 0)
                BuildHtmlTocFromHtmlTitles();
        }

        private int MapHrefToPageIndex(string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return -1;

            string key = NormalizeKey(href);
            string fileOnly = NormalizeKey(Path.GetFileName(key));

            if (!string.IsNullOrWhiteSpace(fileOnly))
            {
                for (int i = 0; i < htmlPageKeys.Count; i++)
                {
                    var kFile = NormalizeKey(Path.GetFileName(htmlPageKeys[i] ?? ""));
                    if (fileOnly.Equals(kFile, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

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

            try { s = Uri.UnescapeDataString(s); } catch { }

            s = s.Replace('\\', '/');

            int hash = s.IndexOf('#');
            if (hash >= 0) s = s.Substring(0, hash);

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

                // toc.ncx (EPUB2)
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

                // nav.xhtml/nav.html (EPUB3) - best effort
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

                    var linkRx = new Regex(@"<a[^>]*href\s*=\s*['""](?<h>[^'""]+)['""][^>]*>(?<t>.*?)</a>",
                        RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    foreach (Match m in linkRx.Matches(navHtml))
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

        // -----------------------------
        // Persistence
        // -----------------------------
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

        // -----------------------------
        // Page change animations
        // -----------------------------
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

        // -----------------------------
        // Back / buttons
        // -----------------------------
        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await SaveReadingProgress();
                await SaveCurrentReadingSettings();
            }
            catch { }
            // ✅ Shell modal back
            try
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            catch { }

            // fallback
            try
            {
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else
                    await Navigation.PopAsync();
            }
            catch { }
        }

        private async void OnButtonPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
            {
                try { await v.ScaleTo(0.96, 80, Easing.CubicOut); } catch { }
            }
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();
            try
            {
                await SaveReadingProgress();
                await SaveCurrentReadingSettings();
            }
            catch { }
        }

        private async void OnButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
            {
                try { await v.ScaleTo(1.0, 110, Easing.CubicOut); } catch { }
            }
        }

        // -----------------------------
        // Menu popup
        // -----------------------------
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

        // -----------------------------
        // Font controls
        // -----------------------------
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
            b.BorderColor = dark ? Color.FromArgb("#2A2A32") : Color.FromArgb("#E5E1F2");

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

        // -----------------------------
        // Theme
        // -----------------------------
        private static string NormalizeTheme(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return "Light";
            stored = stored.Trim();

            if (stored.StartsWith("#")) return "Light";
            if (stored.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return "Dark";
            return "Light";
        }

        private void ApplyTheme(string mode)
        {
            themeMode = mode == "Dark" ? "Dark" : "Light";

            // let AppThemeBinding work
            Application.Current!.UserAppTheme =
                themeMode == "Dark" ? AppTheme.Dark : AppTheme.Light;

            UpdateFontButtonStyles();

            if (this.mode == ReaderMode.HtmlPaged && htmlPages.Count > 0)
                DisplayHtmlPage();
        }

        private async void OnThemeClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string mode)
            {
                ApplyTheme(mode);
                await SaveCurrentReadingSettings();
            }
        }
    }
}