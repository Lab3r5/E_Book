using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using System.Net;
using System.Globalization;
using E_Book.Data;

using VersOne.Epub;
using Mammoth;
using RtfPipe;

namespace E_Book.Pages
{
    [QueryProperty(nameof(FilePath), "filePath")]
    public partial class ReadingPage : ContentPage
    {
        // -----------------------------
        // Paging state
        // -----------------------------
        private int currentPage = 0;

        // TXT
        private string[] lines = Array.Empty<string>();
        private readonly List<string> wrappedTxtLines = new();
        private int linesPerPage = 12;

        // HTML paged result
        private readonly List<string> htmlPages = new();

        // Raw chapter-based source
        private readonly List<string> _rawHtmlChapters = new();
        private readonly List<string> _rawHtmlChapterKeys = new();
        private readonly List<int> _chapterStartPageIndices = new();

        // Render cache
        private readonly Dictionary<int, string> _renderedPageCache = new();

        // QueryProperty target
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

        // Line spacing presets
        private readonly double[] lineSpacings = new[] { 1.4, 1.65, 1.9 };
        private int lineSpacingIndex = 1;
        private double currentLineSpacing = 1.65;

        // Reader local theme only
        private string themeMode = "Light";

        // Reader colors
        private Color PageBgColor = Color.FromArgb("#F7F6FB");
        private Color TextColorReader = Color.FromArgb("#3E3A4A");
        private Color SubtleTextColor = Color.FromArgb("#6A6577");
        private Color PrimaryAccent = Color.FromArgb("#24145A");
        private Color SurfaceColor = Color.FromArgb("#FFFFFF");
        private Color SoftSurfaceColor = Color.FromArgb("#F1EEFF");
        private Color BorderColor = Color.FromArgb("#E5E1F2");

        // Animation flags
        private bool isAnimating = false;
        private bool chromeVisible = true;
        private bool _enterAnimationPlayed = false;

        // Animation timings
        private const uint PageAnimMs = 120;
        private const double SlideDistance = 28;

        private const uint MenuAnimMs = 140;
        private const double MenuRestY = -68;
        private const double MenuHiddenY = -30;

        private const uint ChromeAnimMs = 140;

        private enum ReaderMode { TxtPaged, HtmlPaged, PdfExternal, Unknown }
        private ReaderMode mode = ReaderMode.Unknown;

        // Prevent duplicate loading
        private string _loadedFilePath = string.Empty;
        private double _lastReaderWidth = -1;
        private double _lastReaderHeight = -1;

        // Debounce repagination
        private CancellationTokenSource? _repaginateCts;

        // TOC
        private sealed class TocItem
        {
            public string Title { get; set; } = "";
            public int PageIndex { get; set; }
        }
        private readonly List<TocItem> tocItems = new();

        public ReadingPage()
        {
            InitializeComponent();
            SetupGestures();
            ApplyTheme(themeMode);
        }

        private void SetupGestures()
        {
            // GestureLayer in XAML handles tap + swipe.
        }

        // -----------------------------
        // Lifecycle
        // -----------------------------
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (string.IsNullOrWhiteSpace(FilePath))
            {
                ShowWebView();
                ContentWebView.Source = new HtmlWebViewSource
                {
                    Html = WrapHtml("<p>No file selected.</p>")
                };
                UpdateProgressUI();
                return;
            }

            if (_loadedFilePath == FilePath)
                return;

            _loadedFilePath = FilePath;
            await InitializeReaderAsync();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            _repaginateCts?.Cancel();

            try
            {
                await SaveReadingProgress();
                await SaveCurrentReadingSettings();
            }
            catch { }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width <= 0 || height <= 0)
                return;

            bool sizeChanged =
                Math.Abs(_lastReaderWidth - width) > 1 ||
                Math.Abs(_lastReaderHeight - height) > 1;

            if (!sizeChanged)
                return;

            _lastReaderWidth = width;
            _lastReaderHeight = height;

            RequestRepaginate();
        }

        private async Task InitializeReaderAsync()
        {
            ShowLoadingPage();

            TitleLabel.Text = Path.GetFileNameWithoutExtension(FilePath);

            try
            {
                var readingSettings = await dbHelper.GetReadingSettingsAsync();

                int idx = Array.IndexOf(fontSizes, readingSettings.FontSize);
                fontIndex = idx >= 0 ? idx : 1;
                currentFontSize = fontSizes[fontIndex];
                FontSlider.Value = fontIndex;
                UpdateFontSizeLabel();

                themeMode = NormalizeTheme(readingSettings.BackgroundColor);
                ApplyTheme(themeMode);

                currentLineSpacing = lineSpacings[lineSpacingIndex];
                UpdateLineSpacingLabel();

                await LoadByTypeAsync(FilePath);

                currentPage = await dbHelper.GetReadingProgressAsync(GetReadingKey());

                ClampCurrentPage();
                DisplayPage();
                UpdateProgressUI();
                WarmupNearbyPages();

                if (!_enterAnimationPlayed)
                {
                    _enterAnimationPlayed = true;
                    await PlayEnterAnimationAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // -----------------------------
        // Loading / transitions
        // -----------------------------
        private void ShowLoadingPage()
        {
            ShowWebView();
            ContentWebView.Source = new HtmlWebViewSource
            {
                Html = WrapHtml("<p style='opacity:.65'>Loading book...</p>")
            };
        }

        private async Task PlayEnterAnimationAsync()
        {
            if (RootGrid == null) return;

            RootGrid.Opacity = 0;
            RootGrid.TranslationY = 14;

            await Task.WhenAll(
                RootGrid.FadeTo(1, 180, Easing.CubicOut),
                RootGrid.TranslateTo(0, 0, 180, Easing.CubicOut)
            );
        }

        private async Task PlayExitAnimationAsync()
        {
            if (RootGrid == null) return;

            await Task.WhenAll(
                RootGrid.FadeTo(0, 120, Easing.CubicIn),
                RootGrid.ScaleTo(0.985, 120, Easing.CubicIn)
            );
        }

        // -----------------------------
        // Reading key
        // -----------------------------
        private string GetReadingKey()
        {
            return (FilePath ?? string.Empty).Trim().ToLowerInvariant();
        }

        // -----------------------------
        // Repagination debounce
        // -----------------------------
        private void RequestRepaginate()
        {
            _repaginateCts?.Cancel();
            _repaginateCts = new CancellationTokenSource();
            var token = _repaginateCts.Token;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(120, token);
                    if (token.IsCancellationRequested) return;

                    ClearRenderedPageCache();

                    if (mode == ReaderMode.TxtPaged)
                    {
                        UpdateLinesPerPage();
                        RebuildWrappedTxtLines();
                        BuildTxtToc();
                    }
                    else if (mode == ReaderMode.HtmlPaged)
                    {
                        RebuildHtmlPagination();

                        if (Path.GetExtension(FilePath)?.Equals(".epub", StringComparison.OrdinalIgnoreCase) == true)
                            BuildEpubTocPreferNcxOrNav(FilePath);
                        else
                            BuildHtmlTocFromRawHtmlTitles();
                    }

                    ClampCurrentPage();
                    RefreshCurrentPage();
                }
                catch (TaskCanceledException) { }
            });
        }

        private void ClearRenderedPageCache()
        {
            _renderedPageCache.Clear();
        }

        // -----------------------------
        // Immersive toggle
        // -----------------------------
        private async void OnReadingAreaTapped(object sender, EventArgs e)
        {
            if (Overlay.IsVisible || MenuPopup.IsVisible)
            {
                if (!isAnimating)
                    await HideMenuAsync();
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
                        TopBar.TranslateTo(0, -8, ChromeAnimMs, Easing.CubicIn),

                        BottomBar.FadeTo(0, ChromeAnimMs, Easing.CubicIn),
                        BottomBar.TranslateTo(0, 8, ChromeAnimMs, Easing.CubicIn),

                        TocButtonContainer.FadeTo(0, ChromeAnimMs, Easing.CubicIn),
                        TocButtonContainer.TranslateTo(0, 8, ChromeAnimMs, Easing.CubicIn)
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

                    TopBar.Opacity = 0; TopBar.TranslationY = -8;
                    BottomBar.Opacity = 0; BottomBar.TranslationY = 8;
                    TocButtonContainer.Opacity = 0; TocButtonContainer.TranslationY = 8;

                    await Task.WhenAll(
                        TopBar.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        TopBar.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut),

                        BottomBar.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        BottomBar.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut),

                        TocButtonContainer.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        TocButtonContainer.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut)
                    );
                }

                RequestRepaginate();
            }
            finally
            {
                isAnimating = false;
            }
        }

        // -----------------------------
        // Swipe handlers
        // -----------------------------
        private async void OnSwipeLeft(object sender, SwipedEventArgs e)
        {
            if (isAnimating || Overlay.IsVisible || MenuPopup.IsVisible)
                return;

            await NextPageAsync();
        }

        private async void OnSwipeRight(object sender, SwipedEventArgs e)
        {
            if (isAnimating || Overlay.IsVisible || MenuPopup.IsVisible)
                return;

            await PrevPageAsync();
        }

        // -----------------------------
        // Router
        // -----------------------------
        private async Task LoadByTypeAsync(string filePath)
        {
            lines = Array.Empty<string>();
            wrappedTxtLines.Clear();

            htmlPages.Clear();
            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();
            _chapterStartPageIndices.Clear();

            tocItems.Clear();
            currentPage = 0;
            ClearRenderedPageCache();

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";

            try
            {
                switch (ext)
                {
                    case ".txt":
                        mode = ReaderMode.TxtPaged;
                        await LoadTxtAsync(filePath);
                        UpdateLinesPerPage();
                        RebuildWrappedTxtLines();
                        BuildTxtToc();
                        ShowWebView();
                        break;

                    case ".html":
                    case ".htm":
                        mode = ReaderMode.HtmlPaged;
                        await LoadHtmlFileAsync(filePath);
                        RebuildHtmlPagination();
                        BuildHtmlTocFromRawHtmlTitles();
                        ShowWebView();
                        break;

                    case ".epub":
                        mode = ReaderMode.HtmlPaged;
                        await LoadEpubAsync(filePath);
                        RebuildHtmlPagination();
                        BuildEpubTocPreferNcxOrNav(filePath);
                        ShowWebView();
                        break;

                    case ".docx":
                        mode = ReaderMode.HtmlPaged;
                        await LoadDocxAsync(filePath);
                        RebuildHtmlPagination();
                        BuildHtmlTocFromRawHtmlTitles();
                        ShowWebView();
                        break;

                    case ".rtf":
                        mode = ReaderMode.HtmlPaged;
                        await LoadRtfAsync(filePath);
                        RebuildHtmlPagination();
                        BuildHtmlTocFromRawHtmlTitles();
                        ShowWebView();
                        break;

                    case ".pdf":
                        mode = ReaderMode.PdfExternal;
                        await OpenPdfExternalAsync(filePath);
                        ShowWebView();
                        ContentWebView.Source = new HtmlWebViewSource
                        {
                            Html = WrapHtml("<p>PDF opened in an external viewer.</p><p>Use the Back button to return.</p>")
                        };
                        break;

                    default:
                        mode = ReaderMode.Unknown;
                        ShowWebView();
                        ContentWebView.Source = new HtmlWebViewSource
                        {
                            Html = WrapHtml($"<p>Unsupported format: {WebUtility.HtmlEncode(ext)}</p>")
                        };
                        break;
                }
            }
            catch (Exception ex)
            {
                mode = ReaderMode.Unknown;
                ShowWebView();
                ContentWebView.Source = new HtmlWebViewSource
                {
                    Html = WrapHtml($"<p>Unable to load file: {WebUtility.HtmlEncode(ex.Message)}</p>")
                };
            }
        }

        private void ShowWebView()
        {
            ContentWebView.IsVisible = true;
            fileContentLabel.IsVisible = false;
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
            double areaHeight = ReadingArea.Height;

            if (areaHeight <= 0)
            {
                linesPerPage = 12;
                return;
            }

            double topBottomSafety = 64;
            double availableHeight = Math.Max(120, areaHeight - topBottomSafety);

            double estimatedLineHeight = currentFontSize * currentLineSpacing * 1.20;

            int calculated = (int)Math.Floor(availableHeight / Math.Max(20, estimatedLineHeight));
            linesPerPage = Math.Max(4, calculated - 1);
        }

        private void RebuildWrappedTxtLines()
        {
            wrappedTxtLines.Clear();

            if (lines == null || lines.Length == 0)
                return;

            double areaWidth = ReadingArea.Width;
            if (areaWidth <= 0)
                areaWidth = Width > 0 ? Width : 400;

            double usableWidth = Math.Max(120, areaWidth - 44);
            double avgCharWidth = currentFontSize * 0.55;
            int charsPerVisualLine = Math.Max(10, (int)(usableWidth / avgCharWidth));

            foreach (var raw in lines)
            {
                string line = raw ?? "";

                if (string.IsNullOrWhiteSpace(line))
                {
                    wrappedTxtLines.Add(string.Empty);
                    continue;
                }

                if (Regex.IsMatch(line.Trim(), @"^[_\-─—=]{5,}$"))
                {
                    wrappedTxtLines.Add(line.Trim());
                    continue;
                }

                int start = 0;
                while (start < line.Length)
                {
                    int remaining = line.Length - start;
                    int take = Math.Min(charsPerVisualLine, remaining);

                    if (start + take < line.Length)
                    {
                        int lastSpace = line.LastIndexOf(' ', start + take - 1, take);
                        if (lastSpace > start)
                            take = lastSpace - start + 1;
                    }

                    string segment = line.Substring(start, take).TrimEnd();
                    wrappedTxtLines.Add(segment);

                    start += take;

                    while (start < line.Length && line[start] == ' ')
                        start++;
                }
            }
        }

        private string ConvertTxtPageToHtml(IEnumerable<string> pageLines)
        {
            var sb = new StringBuilder();

            foreach (var raw in pageLines)
            {
                string original = raw ?? "";
                string trimmed = original.Trim();

                if (Regex.IsMatch(trimmed, @"^[_\-─—=]{5,}$"))
                {
                    sb.Append("<hr />");
                    continue;
                }

                var mdHeading = Regex.Match(original, @"^\s*#{1,6}\s+(.*)$");
                if (mdHeading.Success)
                {
                    string heading = WebUtility.HtmlEncode(mdHeading.Groups[1].Value.Trim());
                    sb.Append($"<h2>{heading}</h2>");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(original))
                {
                    sb.Append("<div class='sp'></div>");
                    continue;
                }

                string encoded = WebUtility.HtmlEncode(original).Replace(" ", "&nbsp;");
                sb.Append($"<p>{encoded}</p>");
            }

            return sb.ToString();
        }

        // -----------------------------
        // HTML / EPUB / DOCX / RTF raw loading
        // -----------------------------
        private async Task LoadHtmlFileAsync(string filePath)
        {
            var html = await File.ReadAllTextAsync(filePath);

            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();

            _rawHtmlChapters.Add(string.IsNullOrWhiteSpace(html) ? "<p>(Empty HTML)</p>" : html);
            _rawHtmlChapterKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
        }

        private async Task LoadEpubAsync(string filePath)
        {
            var book = await EpubReader.ReadBookAsync(filePath);
            var readingOrder = book.ReadingOrder?.ToList() ?? new List<EpubLocalTextContentFile>();

            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();

            foreach (var item in readingOrder)
            {
                var content = item?.Content;
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                _rawHtmlChapters.Add(content);

                string key =
                    GetStringProperty(item!, "Href") ??
                    GetStringProperty(item!, "FileName") ??
                    GetStringProperty(item!, "FilePath") ??
                    GetStringProperty(item!, "Path") ??
                    "";

                _rawHtmlChapterKeys.Add(NormalizeKey(key));
            }

            if (_rawHtmlChapters.Count == 0)
            {
                _rawHtmlChapters.Add("<p>(No readable chapters found)</p>");
                _rawHtmlChapterKeys.Add("");
            }
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

        private Task LoadDocxAsync(string filePath)
        {
            var converter = new DocumentConverter();
            var result = converter.ConvertToHtml(filePath);
            var html = result?.Value;

            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();

            _rawHtmlChapters.Add(string.IsNullOrWhiteSpace(html) ? "<p>(Empty DOCX)</p>" : html!);
            _rawHtmlChapterKeys.Add(NormalizeKey(Path.GetFileName(filePath)));

            return Task.CompletedTask;
        }

        private async Task LoadRtfAsync(string filePath)
        {
            var rtfText = await File.ReadAllTextAsync(filePath);
            var html = Rtf.ToHtml(rtfText);

            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();

            _rawHtmlChapters.Add(string.IsNullOrWhiteSpace(html) ? "<p>(Empty RTF)</p>" : html);
            _rawHtmlChapterKeys.Add(NormalizeKey(Path.GetFileName(filePath)));
        }

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
        // HTML pagination
        // -----------------------------
        private List<string> PaginateHtmlContent(string html)
        {
            var pages = new List<string>();

            if (string.IsNullOrWhiteSpace(html))
            {
                pages.Add("<p>(Empty)</p>");
                return pages;
            }

            var blocks = Regex.Split(
                html,
                @"(?=<h1|<h2|<h3|<h4|<h5|<h6|<p|<div|<blockquote|<ul|<ol|<table|<hr|<img)",
                RegexOptions.IgnoreCase);

            var cleanedBlocks = blocks
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => b.Trim())
                .ToList();

            if (cleanedBlocks.Count == 0)
            {
                pages.Add(html);
                return pages;
            }

            double areaHeight = ReadingArea.Height;
            if (areaHeight <= 0)
                areaHeight = 700;

            double estimatedLineHeight = currentFontSize * currentLineSpacing * 1.10;
            int estimatedLinesPerPage = Math.Max(8, (int)((areaHeight - 40) / estimatedLineHeight));

            int currentLines = 0;
            var currentPageBlocks = new List<string>();

            foreach (var block in cleanedBlocks)
            {
                string plainText = StripHtmlTags(block);
                plainText = HtmlEntityDecodeLite(plainText);

                int estimatedLinesForBlock = Math.Max(1, (int)Math.Ceiling(plainText.Length / 28.0));

                if (Regex.IsMatch(block, @"^<h[1-6]", RegexOptions.IgnoreCase)) estimatedLinesForBlock += 2;
                if (Regex.IsMatch(block, @"^<table", RegexOptions.IgnoreCase)) estimatedLinesForBlock += 6;
                if (Regex.IsMatch(block, @"^<(ul|ol|blockquote)", RegexOptions.IgnoreCase)) estimatedLinesForBlock += 2;
                if (Regex.IsMatch(block, @"^<img", RegexOptions.IgnoreCase)) estimatedLinesForBlock += 8;
                if (Regex.IsMatch(block, @"^<hr", RegexOptions.IgnoreCase)) estimatedLinesForBlock += 1;

                if (currentLines + estimatedLinesForBlock > estimatedLinesPerPage && currentPageBlocks.Count > 0)
                {
                    pages.Add(string.Join(Environment.NewLine, currentPageBlocks));
                    currentPageBlocks.Clear();
                    currentLines = 0;
                }

                currentPageBlocks.Add(block);
                currentLines += estimatedLinesForBlock;
            }

            if (currentPageBlocks.Count > 0)
                pages.Add(string.Join(Environment.NewLine, currentPageBlocks));

            return pages.Count > 0 ? pages : new List<string> { html };
        }

        private void RebuildHtmlPagination()
        {
            if (mode != ReaderMode.HtmlPaged)
                return;

            htmlPages.Clear();
            _chapterStartPageIndices.Clear();

            if (_rawHtmlChapters.Count == 0)
                return;

            for (int i = 0; i < _rawHtmlChapters.Count; i++)
            {
                string raw = _rawHtmlChapters[i];
                _chapterStartPageIndices.Add(htmlPages.Count);

                var splitPages = PaginateHtmlContent(raw);
                foreach (var page in splitPages)
                    htmlPages.Add(page);
            }

            ClampCurrentPage();
        }

        // -----------------------------
        // Paging / rendering
        // -----------------------------
        private int GetTotalPages()
        {
            return mode switch
            {
                ReaderMode.TxtPaged => (wrappedTxtLines == null || wrappedTxtLines.Count == 0) ? 0
                    : (int)Math.Ceiling(wrappedTxtLines.Count / (double)linesPerPage),

                ReaderMode.HtmlPaged => (htmlPages == null || htmlPages.Count == 0) ? 0 : htmlPages.Count,

                _ => 0
            };
        }

        private void ClampCurrentPage()
        {
            int total = GetTotalPages();
            if (total <= 0)
            {
                currentPage = 0;
                return;
            }

            currentPage = Math.Clamp(currentPage, 0, total - 1);
        }

        private void RefreshCurrentPage()
        {
            DisplayPage();
            UpdateProgressUI();
            WarmupNearbyPages();
        }

        private void DisplayPage()
        {
            ClampCurrentPage();

            if (_renderedPageCache.TryGetValue(currentPage, out var cachedHtml))
            {
                ShowWebView();
                ContentWebView.Source = new HtmlWebViewSource { Html = cachedHtml };
                return;
            }

            string pageHtml;

            if (mode == ReaderMode.TxtPaged)
            {
                pageHtml = BuildTxtPageHtml();
            }
            else if (mode == ReaderMode.HtmlPaged)
            {
                pageHtml = BuildHtmlPageHtml();
            }
            else
            {
                return;
            }

            string wrapped = WrapHtml(pageHtml);
            _renderedPageCache[currentPage] = wrapped;

            ShowWebView();
            ContentWebView.Source = new HtmlWebViewSource { Html = wrapped };
        }

        private string BuildTxtPageHtml()
        {
            if (wrappedTxtLines == null || wrappedTxtLines.Count == 0)
                return "<p></p>";

            int start = Math.Max(0, currentPage * linesPerPage);
            int end = Math.Min(start + linesPerPage, wrappedTxtLines.Count);

            if (start >= wrappedTxtLines.Count)
            {
                currentPage = Math.Max(0, GetTotalPages() - 1);
                start = currentPage * linesPerPage;
                end = Math.Min(start + linesPerPage, wrappedTxtLines.Count);
            }

            var pageLines = wrappedTxtLines.Skip(start).Take(Math.Max(0, end - start));
            return ConvertTxtPageToHtml(pageLines);
        }

        private string BuildHtmlPageHtml()
        {
            if (htmlPages == null || htmlPages.Count == 0)
                return "<p>(Empty)</p>";

            return htmlPages[Math.Clamp(currentPage, 0, htmlPages.Count - 1)];
        }

        private void WarmupNearbyPages()
        {
            int total = GetTotalPages();
            int original = currentPage;

            foreach (int index in new[] { currentPage - 1, currentPage + 1 })
            {
                if (index < 0 || index >= total) continue;
                if (_renderedPageCache.ContainsKey(index)) continue;

                currentPage = index;

                string pageHtml = mode == ReaderMode.TxtPaged
                    ? BuildTxtPageHtml()
                    : BuildHtmlPageHtml();

                _renderedPageCache[index] = WrapHtml(pageHtml);
            }

            currentPage = original;
        }

        private string WrapHtml(string bodyHtml)
        {
            string bg = ToCssColor(PageBgColor);
            string fg = ToCssColor(TextColorReader);
            string subtle = ToCssColor(SubtleTextColor);
            string border = ToCssColor(BorderColor);
            string accent = ToCssColor(Color.FromArgb("#7B6CFF"));

            int px = currentFontSize switch
            {
                18 => 16,
                22 => 18,
                _ => 20
            };

            string lineHeightCss = currentLineSpacing.ToString(CultureInfo.InvariantCulture);

            return $@"
<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<style>
    html, body {{
        margin: 0;
        padding: 0;
        background: {bg};
        color: {fg};
        font-size: {px}px;
        line-height: {lineHeightCss};
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
        word-wrap: break-word;
        overflow-wrap: anywhere;
        word-break: break-word;
    }}

    body {{
        -webkit-text-size-adjust: 100%;
        text-size-adjust: 100%;
    }}

    .wrap {{
        padding: 6px 2px 24px 2px;
    }}

    p {{
        margin: 0 0 12px 0;
        color: {fg};
    }}

    h1, h2, h3, h4, h5, h6 {{
        margin: 0 0 14px 0;
        line-height: 1.35;
        color: {fg};
    }}

    hr {{
        border: none;
        border-top: 1px solid {subtle};
        margin: 18px 0;
    }}

    .sp {{
        height: 14px;
    }}

    img {{
        max-width: 100%;
        height: auto;
        display: block;
    }}

    a {{
        color: {accent};
        text-decoration: none;
    }}

    blockquote {{
        margin: 14px 0;
        padding: 10px 14px;
        border-left: 3px solid {border};
        color: {subtle};
    }}

    ul, ol {{
        margin: 0 0 12px 0;
        padding-left: 22px;
    }}

    li {{
        margin: 0 0 8px 0;
    }}

    table {{
        width: 100%;
        border-collapse: collapse;
        margin-bottom: 12px;
    }}

    th, td {{
        border: 1px solid {border};
        padding: 8px;
        vertical-align: top;
    }}
</style>
</head>
<body>
<div class='wrap'>
{bodyHtml}
</div>
</body>
</html>";
        }

        private static string ToCssColor(Color color)
        {
            int r = (int)(color.Red * 255);
            int g = (int)(color.Green * 255);
            int b = (int)(color.Blue * 255);
            return $"rgb({r},{g},{b})";
        }

        private void UpdateProgressUI()
        {
            int total = GetTotalPages();
            int current = total <= 0 ? 1 : currentPage + 1;

            ProgressText.Text = $"{Math.Max(1, current)} of {Math.Max(1, total)}";
            ReadingProgressBar.Progress = total <= 1 ? 1 : (current / (double)total);
        }

        // -----------------------------
        // TOC
        // -----------------------------
        private async void OnTocClicked(object sender, EventArgs e)
        {
            int total = GetTotalPages();
            if (total <= 1)
            {
                await DisplayAlert("TOC", "No additional pages or chapters available.", "OK");
                return;
            }

            if (tocItems.Count == 0)
            {
                if (mode == ReaderMode.TxtPaged)
                {
                    BuildTxtToc();
                }
                else if (mode == ReaderMode.HtmlPaged)
                {
                    if (Path.GetExtension(FilePath)?.Equals(".epub", StringComparison.OrdinalIgnoreCase) == true)
                        BuildEpubTocPreferNcxOrNav(FilePath);
                    else
                        BuildHtmlTocFromRawHtmlTitles();
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

            string choice = await DisplayActionSheet(
                $"TOC (Current: p.{currentPage + 1}/{total})",
                "Cancel",
                null,
                options.ToArray());

            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
                return;

            int target = -1;

            if (choice == "Go to page…")
            {
                string? input = await DisplayPromptAsync(
                    title: "Go to",
                    message: $"Enter page/chapter number (1 - {total})",
                    accept: "Go",
                    cancel: "Cancel"
                );

                if (string.IsNullOrWhiteSpace(input))
                    return;

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

            if (target < 0 || target == currentPage)
                return;

            currentPage = target;
            RefreshCurrentPage();
            await SaveReadingProgress();
        }

        // -----------------------------
        // TOC builders
        // -----------------------------
        private void BuildTxtToc()
        {
            tocItems.Clear();

            if (lines == null || lines.Length == 0)
                return;

            var rxMd = new Regex(@"^\s*#{1,6}\s+(?<t>.+?)\s*$", RegexOptions.Compiled);
            var rxEn = new Regex(
                @"^\s*(chapter|ch)\s+(\d+|[ivxlcdm]+)\b[:.\- ]*\s*(?<t>.*)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string? title = null;

                var m1 = rxMd.Match(line);
                if (m1.Success)
                    title = m1.Groups["t"].Value.Trim();

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

                if (title == null)
                    continue;

                title = title.Trim();
                if (title.Length > 60)
                    title = title.Substring(0, 60).Trim() + "…";

                int visualIndex = EstimateWrappedVisualStartIndexForRawLine(i);
                int pageIndex = Math.Max(0, visualIndex / Math.Max(1, linesPerPage));

                if (tocItems.Any(x => x.PageIndex == pageIndex))
                    continue;

                tocItems.Add(new TocItem
                {
                    Title = title,
                    PageIndex = pageIndex
                });
            }

            if (tocItems.Count == 0)
            {
                int total = GetTotalPages();
                for (int p = 0; p < total; p++)
                {
                    tocItems.Add(new TocItem
                    {
                        Title = $"Page {p + 1}",
                        PageIndex = p
                    });
                }
            }
        }

        private int EstimateWrappedVisualStartIndexForRawLine(int rawLineIndex)
        {
            if (rawLineIndex <= 0 || lines == null || lines.Length == 0)
                return 0;

            double areaWidth = ReadingArea.Width;
            if (areaWidth <= 0)
                areaWidth = Width > 0 ? Width : 400;

            double usableWidth = Math.Max(120, areaWidth - 44);
            double avgCharWidth = currentFontSize * 0.55;
            int charsPerVisualLine = Math.Max(10, (int)(usableWidth / avgCharWidth));

            int visualCount = 0;

            for (int i = 0; i < rawLineIndex; i++)
            {
                string line = lines[i] ?? "";

                if (string.IsNullOrWhiteSpace(line))
                {
                    visualCount += 1;
                    continue;
                }

                if (Regex.IsMatch(line.Trim(), @"^[_\-─—=]{5,}$"))
                {
                    visualCount += 1;
                    continue;
                }

                int estimated = Math.Max(1, (int)Math.Ceiling(line.Length / (double)charsPerVisualLine));
                visualCount += estimated;
            }

            return visualCount;
        }

        private void BuildHtmlTocFromRawHtmlTitles()
        {
            tocItems.Clear();

            if (_rawHtmlChapters.Count == 0)
                return;

            for (int i = 0; i < _rawHtmlChapters.Count; i++)
            {
                string html = _rawHtmlChapters[i] ?? "";
                string title = ExtractHtmlTitle(html);

                if (string.IsNullOrWhiteSpace(title))
                    title = $"Chapter {i + 1}";

                int pageIndex = i < _chapterStartPageIndices.Count
                    ? _chapterStartPageIndices[i]
                    : Math.Min(i, Math.Max(0, htmlPages.Count - 1));

                tocItems.Add(new TocItem
                {
                    Title = title,
                    PageIndex = pageIndex
                });
            }
        }

        private void BuildEpubTocPreferNcxOrNav(string epubPath)
        {
            tocItems.Clear();

            var raw = TryParseEpubTocFromZip(epubPath);

            if (raw.Count > 0 && _rawHtmlChapterKeys.Count == _rawHtmlChapters.Count)
            {
                foreach (var (title, href) in raw)
                {
                    int chapterIdx = MapHrefToRawChapterIndex(href);
                    if (chapterIdx < 0)
                        continue;

                    int pageIndex = chapterIdx < _chapterStartPageIndices.Count
                        ? _chapterStartPageIndices[chapterIdx]
                        : 0;

                    string t = HtmlEntityDecodeLite(title).Trim();
                    if (t.Length > 60)
                        t = t.Substring(0, 60).Trim() + "…";

                    if (tocItems.Any(x => x.PageIndex == pageIndex && x.Title.Equals(t, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    tocItems.Add(new TocItem
                    {
                        Title = t,
                        PageIndex = pageIndex
                    });
                }
            }

            if (tocItems.Count == 0)
                BuildHtmlTocFromRawHtmlTitles();
        }

        private int MapHrefToRawChapterIndex(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
                return -1;

            string key = NormalizeKey(href);
            string fileOnly = NormalizeKey(Path.GetFileName(key));

            if (!string.IsNullOrWhiteSpace(fileOnly))
            {
                for (int i = 0; i < _rawHtmlChapterKeys.Count; i++)
                {
                    var kFile = NormalizeKey(Path.GetFileName(_rawHtmlChapterKeys[i] ?? ""));
                    if (fileOnly.Equals(kFile, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            for (int i = 0; i < _rawHtmlChapterKeys.Count; i++)
            {
                var k = NormalizeKey(_rawHtmlChapterKeys[i] ?? "");
                if (string.IsNullOrWhiteSpace(k))
                    continue;

                if (key.EndsWith(k, StringComparison.OrdinalIgnoreCase) || k.EndsWith(key, StringComparison.OrdinalIgnoreCase))
                    return i;

                if (!string.IsNullOrWhiteSpace(fileOnly) &&
                    k.IndexOf(fileOnly, StringComparison.OrdinalIgnoreCase) >= 0)
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

                var ncxEntry = zip.Entries.FirstOrDefault(e =>
                    e.FullName.EndsWith(".ncx", StringComparison.OrdinalIgnoreCase));

                if (ncxEntry != null)
                {
                    using var s = ncxEntry.Open();
                    var doc = XDocument.Load(s);

                    var navPoints = doc.Descendants()
                        .Where(x => x.Name.LocalName == "navPoint")
                        .ToList();

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

                    if (result.Count > 0)
                        return result;
                }

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

                    var linkRx = new Regex(
                        @"<a[^>]*href\s*=\s*['""](?<h>[^'""]+)['""][^>]*>(?<t>.*?)</a>",
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
            if (string.IsNullOrWhiteSpace(html))
                return "";

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
                if (!m.Success)
                    continue;

                string t = StripHtmlTags(m.Groups["t"].Value);
                t = HtmlEntityDecodeLite(t).Trim();

                if (t.Length > 0)
                {
                    if (t.Length > 60)
                        t = t.Substring(0, 60).Trim() + "…";
                    return t;
                }
            }

            return "";
        }

        private static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            return Regex.Replace(input, "<.*?>", string.Empty, RegexOptions.Singleline).Trim();
        }

        private static string HtmlEntityDecodeLite(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
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
            if (string.IsNullOrEmpty(FilePath))
                return;

            await dbHelper.SaveReadingProgressAsync(GetReadingKey(), currentPage);
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
                var target = (VisualElement)ContentWebView;
                double outX = direction > 0 ? -SlideDistance : SlideDistance;
                double inX = direction > 0 ? SlideDistance : -SlideDistance;

                await target.TranslateTo(outX, 0, PageAnimMs, Easing.CubicOut);
                target.Opacity = 0.96;

                RefreshCurrentPage();

                target.TranslationX = inX;
                await target.TranslateTo(0, 0, PageAnimMs, Easing.CubicOut);
                target.Opacity = 1;
            }
            finally
            {
                ContentWebView.TranslationX = 0;
                ContentWebView.Opacity = 1;
                isAnimating = false;
            }
        }

        private async Task NextPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0 || currentPage >= total - 1)
                return;

            currentPage++;
            await AnimatePageChangeAsync(+1);
            await SaveReadingProgress();
        }

        private async Task PrevPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0 || currentPage <= 0)
                return;

            currentPage--;
            await AnimatePageChangeAsync(-1);
            await SaveReadingProgress();
        }

        // -----------------------------
        // Back / button press effect
        // -----------------------------
        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await SaveReadingProgress();
                await SaveCurrentReadingSettings();
            }
            catch { }

            try
            {
                await PlayExitAnimationAsync();
            }
            catch { }

            try
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            catch { }

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
                try { await v.ScaleTo(0.97, 70, Easing.CubicOut); } catch { }
            }
        }

        private async void OnButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
            {
                try { await v.ScaleTo(1.0, 90, Easing.CubicOut); } catch { }
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
                UpdateLineSpacingLabel();
                UpdateAllButtonStyles();

                Overlay.IsVisible = true;
                Overlay.Opacity = 0;

                MenuPopup.IsVisible = true;
                MenuPopup.Opacity = 0;
                MenuPopup.Scale = 0.985;
                MenuPopup.TranslationY = MenuHiddenY;

                await Task.WhenAll(
                    Overlay.FadeTo(1, 120, Easing.CubicOut),
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
                    Overlay.FadeTo(0, 100, Easing.CubicIn),
                    MenuPopup.FadeTo(0, 100, Easing.CubicIn),
                    MenuPopup.ScaleTo(0.985, 100, Easing.CubicIn),
                    MenuPopup.TranslateTo(0, MenuHiddenY, 100, Easing.CubicIn)
                );

                MenuPopup.IsVisible = false;
                Overlay.IsVisible = false;
            }
            finally
            {
                Overlay.Opacity = 0;
                MenuPopup.Opacity = 0;
                MenuPopup.Scale = 0.985;
                MenuPopup.TranslationY = MenuRestY;
                isAnimating = false;
            }
        }

        // -----------------------------
        // Font / line spacing controls
        // -----------------------------
        private async void OnFontPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out var idx))
            {
                idx = Math.Clamp(idx, 0, fontSizes.Length - 1);
                if (idx == fontIndex) return;

                fontIndex = idx;
                FontSlider.Value = fontIndex;
                await ApplyTypographyAsync();
            }
        }

        private async void OnFontSliderChanged(object sender, ValueChangedEventArgs e)
        {
            int idx = (int)Math.Round(e.NewValue);
            idx = Math.Clamp(idx, 0, fontSizes.Length - 1);
            if (idx == fontIndex) return;

            fontIndex = idx;
            await ApplyTypographyAsync();
        }

        private async void OnLineSpacingClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out var idx))
            {
                idx = Math.Clamp(idx, 0, lineSpacings.Length - 1);
                if (idx == lineSpacingIndex) return;

                lineSpacingIndex = idx;
                currentLineSpacing = lineSpacings[lineSpacingIndex];
                await ApplyTypographyAsync();
            }
        }

        private async Task ApplyTypographyAsync()
        {
            currentFontSize = fontSizes[fontIndex];
            currentLineSpacing = lineSpacings[lineSpacingIndex];

            fileContentLabel.FontSize = currentFontSize;
            fileContentLabel.LineHeight = currentLineSpacing;

            UpdateFontSizeLabel();
            UpdateLineSpacingLabel();
            UpdateAllButtonStyles();

            ClearRenderedPageCache();
            RequestRepaginate();

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

        private void UpdateLineSpacingLabel()
        {
            LineSpacingLabel.Text = lineSpacingIndex switch
            {
                0 => "Compact",
                1 => "Normal",
                _ => "Relaxed"
            };
        }

        // -----------------------------
        // Theme
        // -----------------------------
        private static string NormalizeTheme(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return "Light";
            stored = stored.Trim();

            if (stored.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                return "Dark";

            return "Light";
        }

        private void ApplyTheme(string mode)
        {
            themeMode = mode == "Dark" ? "Dark" : "Light";

            if (themeMode == "Dark")
            {
                PageBgColor = Color.FromArgb("#0F0F12");
                TextColorReader = Color.FromArgb("#EDEAF6");
                SubtleTextColor = Color.FromArgb("#BDB8C8");
                PrimaryAccent = Color.FromArgb("#EDEAF6");
                SurfaceColor = Color.FromArgb("#17171C");
                SoftSurfaceColor = Color.FromArgb("#20202A");
                BorderColor = Color.FromArgb("#2A2A32");
            }
            else
            {
                PageBgColor = Color.FromArgb("#F7F6FB");
                TextColorReader = Color.FromArgb("#3E3A4A");
                SubtleTextColor = Color.FromArgb("#6A6577");
                PrimaryAccent = Color.FromArgb("#24145A");
                SurfaceColor = Color.FromArgb("#FFFFFF");
                SoftSurfaceColor = Color.FromArgb("#F1EEFF");
                BorderColor = Color.FromArgb("#E5E1F2");
            }

            ApplyThemeToVisuals();
            UpdateAllButtonStyles();
            ClearRenderedPageCache();

            if (this.mode == ReaderMode.HtmlPaged || this.mode == ReaderMode.TxtPaged)
                DisplayPage();
        }

        private void ApplyThemeToVisuals()
        {
            BackgroundColor = PageBgColor;
            ReadingArea.BackgroundColor = PageBgColor;
            BottomBar.BackgroundColor = PageBgColor;

            TitleLabel.TextColor = PrimaryAccent;
            BackButton.TextColor = PrimaryAccent;
            MenuButton.TextColor = PrimaryAccent;
            TocButton.TextColor = PrimaryAccent;

            ProgressText.TextColor = SubtleTextColor;
            ReadingProgressBar.ProgressColor = Color.FromArgb("#7B6CFF");
            ReadingProgressBar.BackgroundColor = BorderColor;

            TocButtonContainer.BackgroundColor = SoftSurfaceColor;

            MenuPopup.BackgroundColor = SurfaceColor;
            MenuPopup.Stroke = BorderColor;

            fileContentLabel.TextColor = TextColorReader;

            UpdatePopupTextColors();
        }

        private void UpdatePopupTextColors()
        {
            foreach (var child in GetDescendants(MenuPopup))
            {
                if (child is Label lbl)
                {
                    lbl.TextColor =
                        lbl == FontSizeLabel || lbl == LineSpacingLabel
                        ? SubtleTextColor
                        : TextColorReader;
                }
            }
        }

        private IEnumerable<Element> GetDescendants(Element parent)
        {
            foreach (var child in parent.LogicalChildren)
            {
                yield return child;

                foreach (var grandChild in GetDescendants(child))
                    yield return grandChild;
            }
        }

        private async void OnThemeClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string mode)
            {
                ApplyTheme(mode);
                await SaveCurrentReadingSettings();
            }
        }

        // -----------------------------
        // Button styling
        // -----------------------------
        private void UpdateAllButtonStyles()
        {
            UpdateFontButtonStyles();
            UpdateLineSpacingButtonStyles();
            UpdateThemeButtonStyles();
        }

        private void UpdateFontButtonStyles()
        {
            StyleSegmentButton(SmallBtn, fontIndex == 0);
            StyleSegmentButton(MediumBtn, fontIndex == 1);
            StyleSegmentButton(LargeBtn, fontIndex == 2);
        }

        private void UpdateLineSpacingButtonStyles()
        {
            StyleSegmentButton(CompactBtn, lineSpacingIndex == 0);
            StyleSegmentButton(NormalBtn, lineSpacingIndex == 1);
            StyleSegmentButton(RelaxedBtn, lineSpacingIndex == 2);
        }

        private void UpdateThemeButtonStyles()
        {
            StyleSegmentButton(LightBtn, themeMode == "Light");
            StyleSegmentButton(DarkBtn, themeMode == "Dark");
        }

        private void StyleSegmentButton(Button button, bool selected)
        {
            button.BorderWidth = 1;
            button.BorderColor = BorderColor;

            if (selected)
            {
                button.BackgroundColor = PrimaryAccent;
                button.TextColor = SurfaceColor;
            }
            else
            {
                button.BackgroundColor = SurfaceColor;
                button.TextColor = PrimaryAccent;
            }
        }
    }
}