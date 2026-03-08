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
        private int currentPage = 0;

        private readonly List<string> txtParagraphs = new();
        private readonly List<List<string>> txtPages = new();
        private readonly List<int> txtParagraphStartPageIndices = new();

        private readonly List<string> htmlPages = new();

        private readonly List<string> _rawHtmlChapters = new();
        private readonly List<string> _rawHtmlChapterKeys = new();
        private readonly List<int> _chapterStartPageIndices = new();

        private readonly Dictionary<int, string> _renderedPageCache = new();

        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set => _filePath = Uri.UnescapeDataString(value ?? string.Empty);
        }

        private readonly Database dbHelper = new();

        private readonly int[] fontSizes = new[] { 18, 22, 26 };
        private int fontIndex = 1;
        private int currentFontSize = 22;

        private readonly double[] lineSpacings = new[] { 1.4, 1.65, 1.9 };
        private int lineSpacingIndex = 1;
        private double currentLineSpacing = 1.65;

        private string themeMode = "White";

        private Color PageBgColor = Color.FromArgb("#F7F6FB");
        private Color TextColorReader = Color.FromArgb("#3E3A4A");
        private Color SubtleTextColor = Color.FromArgb("#6A6577");
        private Color PrimaryAccent = Color.FromArgb("#24145A");
        private Color SurfaceColor = Color.FromArgb("#FFFFFF");
        private Color SoftSurfaceColor = Color.FromArgb("#F1EEFF");
        private Color BorderColor = Color.FromArgb("#E5E1F2");

        private bool isAnimating = false;
        private bool chromeVisible = true;
        private bool _enterAnimationPlayed = false;
        private bool _readerInitialized = false;

        private const uint PageAnimMs = 120;
        private const double SlideDistance = 28;

        private const uint MenuAnimMs = 180;
        private const double MenuRestY = 0;
        private const double MenuHiddenY = 20;

        private const uint ChromeAnimMs = 140;

        private const int InitialTxtPreloadParagraphCount = 300;

        private enum ReaderMode { TxtPaged, HtmlPaged, PdfExternal, Unknown }
        private ReaderMode mode = ReaderMode.Unknown;

        private string _loadedFilePath = string.Empty;
        private double _lastReaderWidth = -1;
        private double _lastReaderHeight = -1;

        private CancellationTokenSource? _repaginateCts;
        private CancellationTokenSource? _progressSaveCts;

        private bool _txtPaginationIsPartial = false;
        private int _txtPreloadedParagraphCount = 0;
        private bool _isFullTxtPaginationRunning = false;

        private int _pendingRestorePage = 0;
        private double _pendingRestoreProgress = 0;
        private int _lastSavedPage = -1;

        private sealed class TocItem
        {
            public string Title { get; set; } = "";
            public int PageIndex { get; set; }
        }

        private sealed class TxtPaginationResult
        {
            public List<List<string>> Pages { get; } = new();
            public List<int> ParagraphStartPageIndices { get; } = new();
            public bool IsPartial { get; set; }
            public int PreloadedParagraphCount { get; set; }
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
        }

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
            _progressSaveCts?.Cancel();

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
                Math.Abs(_lastReaderWidth - width) > 12 ||
                Math.Abs(_lastReaderHeight - height) > 12;

            if (!sizeChanged)
                return;

            _lastReaderWidth = width;
            _lastReaderHeight = height;

            if (!_readerInitialized)
                return;

            RequestRepaginate();
        }

        private async Task InitializeReaderAsync()
        {
            TitleLabel.Text = Path.GetFileNameWithoutExtension(FilePath);

            try
            {
                var readingSettings = await dbHelper.GetReadingSettingsAsync();

                int idx = Array.IndexOf(fontSizes, readingSettings.FontSize);
                fontIndex = idx >= 0 ? idx : 1;
                currentFontSize = fontSizes[fontIndex];

                themeMode = NormalizeTheme(readingSettings.BackgroundColor);
                ApplyTheme(themeMode);

                ShowLoadingPage();

                try
                {
                    var lineSpacingProperty = readingSettings.GetType().GetProperty("LineSpacing");
                    if (lineSpacingProperty != null)
                    {
                        var val = lineSpacingProperty.GetValue(readingSettings);
                        if (val is double d)
                        {
                            currentLineSpacing = d;
                            int lsIdx = Array.IndexOf(lineSpacings, d);
                            lineSpacingIndex = lsIdx >= 0 ? lsIdx : 1;
                        }
                    }
                }
                catch
                {
                    currentLineSpacing = lineSpacings[lineSpacingIndex];
                }

                await LoadByTypeAsync(FilePath);

                var progress = await dbHelper.GetReadingProgressRecordAsync(GetReadingKey());
                _pendingRestorePage = progress.LastPage;

                _pendingRestoreProgress =
                    progress.TotalPages > 0
                        ? progress.LastPage / (double)progress.TotalPages
                        : 0;

                if (GetTotalPages() > 0 && _pendingRestoreProgress > 0)
                {
                    currentPage = Math.Clamp(
                        (int)Math.Round(_pendingRestoreProgress * Math.Max(0, GetTotalPages() - 1)),
                        0,
                        Math.Max(0, GetTotalPages() - 1));
                }
                else
                {
                    currentPage = progress.LastPage;
                }

                ClampCurrentPage();
                await Task.Yield();
                DisplayPage();
                UpdateProgressUI();
                TrimRenderedPageCache();

                // 为了打开时尽量回到准确页码（如 556 页），这里直接等待完整分页
                if (mode == ReaderMode.TxtPaged && _txtPaginationIsPartial)
                {
                    await StartBackgroundFullTxtPaginationAsync();
                }

                _readerInitialized = true;

                if (!_enterAnimationPlayed)
                {
                    _enterAnimationPlayed = true;
                    await PlayEnterAnimationAsync();
                }
            }
            catch (Exception ex)
            {
                _readerInitialized = false;
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void ShowLoadingPage()
        {
            ShowWebView();
            ContentWebView.BackgroundColor = PageBgColor;
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

        private string GetReadingKey()
        {
            return Path.GetFileName(FilePath ?? string.Empty).Trim().ToLowerInvariant();
        }

        private double GetReaderAreaWidth()
        {
            double areaWidth = ReadingArea.Width;
            if (areaWidth <= 0)
                areaWidth = Width > 0 ? Width : 430;
            return areaWidth;
        }

        private double GetReaderAreaHeight()
        {
            double areaHeight = ReadingArea.Height;
            if (areaHeight <= 0)
                areaHeight = Height > 0 ? Height : 760;
            return areaHeight;
        }

        private void RequestRepaginate()
        {
            _repaginateCts?.Cancel();
            _repaginateCts = new CancellationTokenSource();
            var token = _repaginateCts.Token;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(220, token);
                    if (token.IsCancellationRequested) return;

                    ClearRenderedPageCache();

                    if (mode == ReaderMode.TxtPaged)
                    {
                        var result = BuildTxtPaginationResult(GetReaderAreaWidth(), GetReaderAreaHeight(), null);
                        ApplyTxtPaginationResult(result);
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

        private void TrimRenderedPageCache()
        {
            const int maxDistance = 3;

            var keep = new HashSet<int>();

            for (int i = currentPage - maxDistance; i <= currentPage + maxDistance; i++)
            {
                if (i >= 0)
                    keep.Add(i);
            }

            var remove = _renderedPageCache.Keys
                .Where(k => !keep.Contains(k))
                .ToList();

            foreach (var key in remove)
                _renderedPageCache.Remove(key);
        }

        private void ScheduleSaveReadingProgress()
        {
            _progressSaveCts?.Cancel();
            _progressSaveCts = new CancellationTokenSource();
            var token = _progressSaveCts.Token;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(800, token);
                    if (token.IsCancellationRequested) return;
                    await SaveReadingProgress();
                }
                catch (TaskCanceledException) { }
                catch { }
            });
        }

        private async void OnReadingAreaTapped(object sender, TappedEventArgs e)
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
            }
            finally
            {
                isAnimating = false;
            }
        }

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

        private async Task LoadByTypeAsync(string filePath)
        {
            _readerInitialized = false;

            txtParagraphs.Clear();
            txtPages.Clear();
            txtParagraphStartPageIndices.Clear();

            htmlPages.Clear();
            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();
            _chapterStartPageIndices.Clear();

            tocItems.Clear();
            currentPage = 0;
            ClearRenderedPageCache();

            _txtPaginationIsPartial = false;
            _txtPreloadedParagraphCount = 0;
            _isFullTxtPaginationRunning = false;

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";

            try
            {
                switch (ext)
                {
                    case ".txt":
                        mode = ReaderMode.TxtPaged;
                        await LoadTxtAsync(filePath);

                        double txtWidth = GetReaderAreaWidth();
                        double txtHeight = GetReaderAreaHeight();

                        var preloadResult = await Task.Run(() =>
                            BuildTxtPaginationResult(txtWidth, txtHeight, InitialTxtPreloadParagraphCount));

                        ApplyTxtPaginationResult(preloadResult);
                        ShowWebView();
                        break;

                    case ".html":
                    case ".htm":
                        mode = ReaderMode.HtmlPaged;
                        await LoadHtmlFileAsync(filePath);
                        RebuildHtmlPagination();
                        ShowWebView();
                        break;

                    case ".epub":
                        mode = ReaderMode.HtmlPaged;
                        await LoadEpubAsync(filePath);
                        RebuildHtmlPagination();
                        ShowWebView();
                        break;

                    case ".docx":
                        mode = ReaderMode.HtmlPaged;
                        await LoadDocxAsync(filePath);
                        RebuildHtmlPagination();
                        ShowWebView();
                        break;

                    case ".rtf":
                        mode = ReaderMode.HtmlPaged;
                        await LoadRtfAsync(filePath);
                        RebuildHtmlPagination();
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

        private async Task LoadTxtAsync(string filePath)
        {
            txtParagraphs.Clear();

            using var reader = new StreamReader(filePath);
            var buffer = new StringBuilder();
            bool previousLineLooksEnglish = false;

            while (!reader.EndOfStream)
            {
                string line = await reader.ReadLineAsync() ?? "";
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (buffer.Length > 0)
                    {
                        txtParagraphs.Add(buffer.ToString().Trim());
                        buffer.Clear();
                    }

                    txtParagraphs.Add(string.Empty);
                    previousLineLooksEnglish = false;
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^[_\-─—=·•\.]{5,}$"))
                {
                    if (buffer.Length > 0)
                    {
                        txtParagraphs.Add(buffer.ToString().Trim());
                        buffer.Clear();
                    }

                    txtParagraphs.Add(trimmed);
                    previousLineLooksEnglish = false;
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^\s*#{1,6}\s+"))
                {
                    if (buffer.Length > 0)
                    {
                        txtParagraphs.Add(buffer.ToString().Trim());
                        buffer.Clear();
                    }

                    txtParagraphs.Add(trimmed);
                    previousLineLooksEnglish = false;
                    continue;
                }

                bool currentLineLooksEnglish = LooksLikeEnglishText(trimmed);

                if (buffer.Length > 0 && previousLineLooksEnglish && currentLineLooksEnglish)
                    buffer.Append(' ');

                buffer.Append(line.Trim());
                previousLineLooksEnglish = currentLineLooksEnglish;
            }

            if (buffer.Length > 0)
                txtParagraphs.Add(buffer.ToString().Trim());
        }

        private bool LooksLikeEnglishText(string text)
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

        private TxtPaginationResult BuildTxtPaginationResult(double areaWidth, double areaHeight, int? paragraphLimit)
        {
            var result = new TxtPaginationResult();

            if (txtParagraphs.Count == 0)
                return result;

            double usableWidth = Math.Max(180, areaWidth - 44);
            double usableHeight = Math.Max(180, areaHeight - 104);

            bool mostlyChinese = ContainsMostlyChinese(string.Join("", txtParagraphs.Take(80)));

            double lineHeightPx = GetReaderCssFontSize() * currentLineSpacing;
            double paragraphSpacingPx = 16;
            double blankParagraphPx = 20;

            double currentHeight = 0;
            var currentPageParagraphs = new List<string>();
            int currentPageStartParagraphIndex = 0;

            int actualLimit = paragraphLimit.HasValue
                ? Math.Min(txtParagraphs.Count, paragraphLimit.Value)
                : txtParagraphs.Count;

            result.PreloadedParagraphCount = actualLimit;
            result.IsPartial = actualLimit < txtParagraphs.Count;

            for (int i = 0; i < actualLimit; i++)
            {
                string paragraph = txtParagraphs[i] ?? "";

                double estimatedHeight = EstimateParagraphHeightPx(
                    paragraph,
                    usableWidth,
                    lineHeightPx,
                    paragraphSpacingPx,
                    blankParagraphPx,
                    mostlyChinese);

                if (estimatedHeight > usableHeight)
                {
                    if (currentPageParagraphs.Count > 0)
                    {
                        result.Pages.Add(new List<string>(currentPageParagraphs));
                        currentPageParagraphs.Clear();
                        currentHeight = 0;
                    }

                    var splitParagraphs = SplitLongParagraphForPaging(
                        paragraph,
                        usableWidth,
                        usableHeight,
                        lineHeightPx,
                        paragraphSpacingPx,
                        blankParagraphPx,
                        mostlyChinese);

                    result.ParagraphStartPageIndices.Add(result.Pages.Count);

                    foreach (var part in splitParagraphs)
                    {
                        result.Pages.Add(new List<string> { part });
                    }

                    currentPageStartParagraphIndex = i + 1;
                    continue;
                }

                if (currentPageParagraphs.Count > 0 && currentHeight + estimatedHeight > usableHeight)
                {
                    result.Pages.Add(new List<string>(currentPageParagraphs));
                    currentPageParagraphs.Clear();
                    currentHeight = 0;
                    currentPageStartParagraphIndex = i;
                }

                if (currentPageParagraphs.Count == 0)
                {
                    while (result.ParagraphStartPageIndices.Count <= currentPageStartParagraphIndex)
                        result.ParagraphStartPageIndices.Add(result.Pages.Count);
                }

                currentPageParagraphs.Add(paragraph);
                currentHeight += estimatedHeight;
            }

            if (currentPageParagraphs.Count > 0)
                result.Pages.Add(new List<string>(currentPageParagraphs));

            while (result.ParagraphStartPageIndices.Count < actualLimit)
                result.ParagraphStartPageIndices.Add(Math.Max(0, result.Pages.Count - 1));

            return result;
        }

        private void ApplyTxtPaginationResult(TxtPaginationResult result)
        {
            txtPages.Clear();
            txtPages.AddRange(result.Pages);

            txtParagraphStartPageIndices.Clear();
            txtParagraphStartPageIndices.AddRange(result.ParagraphStartPageIndices);

            _txtPaginationIsPartial = result.IsPartial;
            _txtPreloadedParagraphCount = result.PreloadedParagraphCount;

            ClampCurrentPage();
        }

        private async Task StartBackgroundFullTxtPaginationAsync()
        {
            if (mode != ReaderMode.TxtPaged || !_txtPaginationIsPartial || _isFullTxtPaginationRunning)
                return;

            _isFullTxtPaginationRunning = true;

            try
            {
                double width = GetReaderAreaWidth();
                double height = GetReaderAreaHeight();

                var fullResult = await Task.Run(() => BuildTxtPaginationResult(width, height, null));

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ApplyTxtPaginationResult(fullResult);

                    if (_pendingRestoreProgress > 0)
                    {
                        currentPage = Math.Clamp(
                            (int)Math.Round(_pendingRestoreProgress * Math.Max(0, txtPages.Count - 1)),
                            0,
                            Math.Max(0, txtPages.Count - 1));
                    }
                    else
                    {
                        currentPage = Math.Min(_pendingRestorePage, Math.Max(0, txtPages.Count - 1));
                    }

                    _pendingRestorePage = currentPage;
                    _pendingRestoreProgress = 0;

                    ClearRenderedPageCache();
                    DisplayPage();
                    UpdateProgressUI();
                    WarmupNearbyPages();
                    TrimRenderedPageCache();
                });
            }
            catch
            {
            }
            finally
            {
                _isFullTxtPaginationRunning = false;
            }
        }

        private List<string> SplitLongParagraphForPaging(
            string paragraph,
            double usableWidth,
            double usableHeight,
            double lineHeightPx,
            double paragraphSpacingPx,
            double blankParagraphPx,
            bool mostlyChinese)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(paragraph))
            {
                result.Add(paragraph);
                return result;
            }

            string trimmed = paragraph.Trim();

            if (Regex.IsMatch(trimmed, @"^[_\-─—=·•\.]{5,}$"))
            {
                result.Add(trimmed);
                return result;
            }

            var mdHeading = Regex.Match(trimmed, @"^\s*#{1,6}\s+(.*)$");
            if (mdHeading.Success)
            {
                result.Add(trimmed);
                return result;
            }

            bool isEnglish = LooksLikeEnglishText(paragraph);

            double avgCharWidth = mostlyChinese
                ? GetReaderCssFontSize() * 0.95
                : GetReaderCssFontSize() * 0.36;

            double firstLineIndentWidth = isEnglish ? 0 : GetReaderCssFontSize() * 2.0;
            int charsFirstLine = Math.Max(6, (int)((usableWidth - firstLineIndentWidth) / avgCharWidth));
            int charsOtherLines = Math.Max(8, (int)(usableWidth / avgCharWidth));

            int maxLinesPerPage = Math.Max(3, (int)Math.Floor((usableHeight - paragraphSpacingPx) / lineHeightPx));
            int maxCharsFirstPage = charsFirstLine + Math.Max(0, maxLinesPerPage - 1) * charsOtherLines;
            int maxCharsNormalPage = Math.Max(charsOtherLines * maxLinesPerPage, charsOtherLines * 3);

            if (paragraph.Length <= maxCharsFirstPage)
            {
                result.Add(paragraph);
                return result;
            }

            int start = 0;
            bool firstChunk = true;

            while (start < paragraph.Length)
            {
                int maxChars = firstChunk ? maxCharsFirstPage : maxCharsNormalPage;
                int remaining = paragraph.Length - start;
                int take = Math.Min(maxChars, remaining);

                if (isEnglish && start + take < paragraph.Length)
                {
                    int lastSpace = paragraph.LastIndexOf(' ', start + take - 1, take);
                    if (lastSpace > start + Math.Min(20, take / 3))
                        take = lastSpace - start + 1;
                }

                string chunk = paragraph.Substring(start, take).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                    result.Add(chunk);

                start += take;

                while (start < paragraph.Length && paragraph[start] == ' ')
                    start++;

                firstChunk = false;
            }

            return result.Count > 0 ? result : new List<string> { paragraph };
        }

        private double EstimateParagraphHeightPx(
            string paragraph,
            double usableWidth,
            double lineHeightPx,
            double paragraphSpacingPx,
            double blankParagraphPx,
            bool mostlyChinese)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                return blankParagraphPx;

            string trimmed = paragraph.Trim();

            if (Regex.IsMatch(trimmed, @"^[_\-─—=·•\.]{5,}$"))
                return lineHeightPx + 18;

            var mdHeading = Regex.Match(trimmed, @"^\s*#{1,6}\s+(.*)$");
            if (mdHeading.Success)
            {
                string headingText = mdHeading.Groups[1].Value.Trim();
                int headingCharsPerLine = Math.Max(6, (int)(usableWidth / (GetReaderCssFontSize() * 1.0)));
                int headingLinesNeeded = Math.Max(1, (int)Math.Ceiling(headingText.Length / (double)headingCharsPerLine));
                return headingLinesNeeded * (lineHeightPx * 1.15) + 22;
            }

            bool isEnglish = LooksLikeEnglishText(paragraph);

            double avgCharWidth = mostlyChinese
                ? GetReaderCssFontSize() * 0.95
                : GetReaderCssFontSize() * 0.43;

            double firstLineIndentWidth = isEnglish ? 0 : GetReaderCssFontSize() * 2.0;
            int charsFirstLine = Math.Max(6, (int)((usableWidth - firstLineIndentWidth) / avgCharWidth));
            int charsOtherLines = Math.Max(8, (int)(usableWidth / avgCharWidth));

            int textLength = paragraph.Length;

            int paragraphLinesNeeded;
            if (textLength <= charsFirstLine)
            {
                paragraphLinesNeeded = 1;
            }
            else
            {
                int remaining = textLength - charsFirstLine;
                paragraphLinesNeeded = 1 + (int)Math.Ceiling(remaining / (double)charsOtherLines);
            }

            double extraSafety = isEnglish ? -4 : 8;
            return paragraphLinesNeeded * lineHeightPx + paragraphSpacingPx + extraSafety;
        }

        private string BuildTxtPageHtmlFromParagraphs(List<string> paragraphs)
        {
            var sb = new StringBuilder();

            foreach (var raw in paragraphs)
            {
                string original = raw ?? "";
                string trimmed = original.Trim();

                if (string.IsNullOrWhiteSpace(original))
                {
                    sb.Append("<div class='sp'></div>");
                    continue;
                }

                if (Regex.IsMatch(trimmed, @"^[_\-─—=·•\.]{5,}$"))
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

                bool isEnglish = LooksLikeEnglishText(original);
                string encoded = WebUtility.HtmlEncode(original);

                if (original.StartsWith("　　"))
                    encoded = "&emsp;&emsp;" + WebUtility.HtmlEncode(original.Substring(2));
                else if (original.StartsWith("　"))
                    encoded = "&emsp;" + WebUtility.HtmlEncode(original.Substring(1));

                sb.Append(isEnglish
                    ? $"<p class='txt txt-en'>{encoded}</p>"
                    : $"<p class='txt txt-cn'>{encoded}</p>");
            }

            return sb.ToString();
        }

        private int GetReaderCssFontSize()
        {
            return currentFontSize switch
            {
                18 => 16,
                22 => 18,
                _ => 20
            };
        }

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

            double estimatedLineHeight = currentFontSize * currentLineSpacing * 1.18;
            int estimatedLinesPerPage = Math.Max(5, (int)((areaHeight - 150) / estimatedLineHeight));

            int currentLines = 0;
            var currentPageBlocks = new List<string>();

            foreach (var block in cleanedBlocks)
            {
                string plainText = StripHtmlTags(block);
                plainText = HtmlEntityDecodeLite(plainText);

                bool mostlyChinese = ContainsMostlyChinese(plainText);

                int estimatedLinesForBlock = mostlyChinese
                    ? Math.Max(1, (int)Math.Ceiling(plainText.Length / 16.0))
                    : Math.Max(1, (int)Math.Ceiling(plainText.Length / 28.0));

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

        private static bool ContainsMostlyChinese(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int total = 0;
            int chinese = 0;

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                    continue;

                total++;

                if (c >= 0x4E00 && c <= 0x9FFF)
                    chinese++;
            }

            if (total == 0) return false;
            return chinese / (double)total >= 0.35;
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

        private int GetTotalPages()
        {
            return mode switch
            {
                ReaderMode.TxtPaged => txtPages.Count == 0 ? 0 : txtPages.Count,
                ReaderMode.HtmlPaged => htmlPages.Count == 0 ? 0 : htmlPages.Count,
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
            TrimRenderedPageCache();
        }

        private void DisplayPage()
        {
            ClampCurrentPage();

            if (_renderedPageCache.TryGetValue(currentPage, out var cachedHtml))
            {
                ShowWebView();
                ContentWebView.Source = new HtmlWebViewSource { Html = cachedHtml };
                TrimRenderedPageCache();
                return;
            }

            string pageHtml;

            if (mode == ReaderMode.TxtPaged)
            {
                if (txtPages.Count == 0)
                    pageHtml = "<p></p>";
                else
                    pageHtml = BuildTxtPageHtmlFromParagraphs(txtPages[Math.Clamp(currentPage, 0, txtPages.Count - 1)]);
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
            TrimRenderedPageCache();
        }

        private string BuildHtmlPageHtml()
        {
            if (htmlPages.Count == 0)
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

                string pageHtml;
                if (mode == ReaderMode.TxtPaged)
                {
                    pageHtml = BuildTxtPageHtmlFromParagraphs(txtPages[index]);
                }
                else
                {
                    pageHtml = BuildHtmlPageHtml();
                }

                _renderedPageCache[index] = WrapHtml(pageHtml);
            }

            currentPage = original;
            TrimRenderedPageCache();
        }

        private string WrapHtml(string bodyHtml)
        {
            string bg = ToCssColor(PageBgColor);
            string fg = ToCssColor(TextColorReader);
            string subtle = ToCssColor(SubtleTextColor);
            string border = ToCssColor(BorderColor);
            string accent = ToCssColor(Color.FromArgb("#7B6CFF"));

            int px = GetReaderCssFontSize();
            string lineHeightCss = currentLineSpacing.ToString(CultureInfo.InvariantCulture);

            return $@"
<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
<style>
    html, body {{
        margin: 0;
        padding: 0;
        background: {bg};
        color: {fg};
        font-size: {px}px;
        line-height: {lineHeightCss};
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
        overflow-wrap: break-word;
        word-break: normal;
        -webkit-font-smoothing: antialiased;
        text-rendering: optimizeLegibility;
    }}

    body {{
        -webkit-text-size-adjust: 100%;
        text-size-adjust: 100%;
    }}

    .wrap {{
        padding: 12px 14px 20px 14px;
        max-width: 900px;
        margin: auto;
        box-sizing: border-box;
        background: {bg};
    }}

    p {{
        margin: 0 0 10px 0;
        color: {fg};
    }}

    p.txt {{
        white-space: normal;
    }}

    p.txt-cn {{
        margin: 0 0 16px 0;
        text-indent: 2em;
        text-align: justify;
        word-break: break-word;
        overflow-wrap: break-word;
        line-break: auto;
    }}

    p.txt-en {{
        margin: 0 0 2px 0;
        text-indent: 0;
        text-align: left;
        word-break: normal;
        overflow-wrap: break-word;
        line-break: auto;
        line-height: 1.18;
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
            double percent = total <= 0 ? 100 : (current * 100.0 / total);

            ProgressText.Text = $"{Math.Max(1, current)} / {Math.Max(1, total)} ({percent:0}%)";
            ReadingProgressBar.Progress = total <= 1 ? 1 : (current / (double)total);
        }

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
                    BuildTxtToc();
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

            options.Add("Go to page...");

            string choice = await DisplayActionSheet(
                $"TOC (Current: p.{currentPage + 1}/{total})",
                "Cancel",
                null,
                options.ToArray());

            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
                return;

            int target = -1;

            if (choice == "Go to page...")
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

        private void BuildTxtToc()
        {
            tocItems.Clear();

            if (txtParagraphs.Count == 0)
                return;

            var rxMd = new Regex(@"^\s*#{1,6}\s+(?<t>.+?)\s*$", RegexOptions.Compiled);
            var rxEn = new Regex(
                @"^\s*(chapter|ch)\s+(\d+|[ivxlcdm]+)\b[:.\- ]*\s*(?<t>.*)$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            int paragraphCountForToc = _txtPaginationIsPartial
                ? Math.Min(txtParagraphs.Count, _txtPreloadedParagraphCount)
                : txtParagraphs.Count;

            for (int i = 0; i < paragraphCountForToc; i++)
            {
                string paragraph = (txtParagraphs[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;

                string? title = null;

                var m1 = rxMd.Match(paragraph);
                if (m1.Success)
                    title = m1.Groups["t"].Value.Trim();

                if (title == null)
                {
                    var m2 = rxEn.Match(paragraph);
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

                if (title.Length > 60)
                    title = title.Substring(0, 60).Trim() + "…";

                int pageIndex = FindParagraphStartPageIndex(i);

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

        private int FindParagraphStartPageIndex(int paragraphIndex)
        {
            if (txtParagraphStartPageIndices.Count == 0)
                return 0;

            int safeIndex = Math.Clamp(paragraphIndex, 0, txtParagraphStartPageIndices.Count - 1);
            return Math.Clamp(txtParagraphStartPageIndices[safeIndex], 0, Math.Max(0, txtPages.Count - 1));
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

        private async Task SaveReadingProgress()
        {
            if (string.IsNullOrEmpty(FilePath))
                return;

            if (currentPage == _lastSavedPage)
                return;

            _lastSavedPage = currentPage;

            await dbHelper.SaveReadingProgressAsync(
                GetReadingKey(),
                currentPage,
                GetTotalPages());
        }

        private async Task SaveCurrentReadingSettings()
        {
            try
            {
                await dbHelper.SaveReadingSettingsAsync(currentFontSize, themeMode, currentLineSpacing);
            }
            catch
            {
                await dbHelper.SaveReadingSettingsAsync(currentFontSize, themeMode);
            }
        }

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
            ScheduleSaveReadingProgress();
        }

        private async Task PrevPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0 || currentPage <= 0)
                return;

            currentPage--;
            await AnimatePageChangeAsync(-1);
            ScheduleSaveReadingProgress();
        }

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            _progressSaveCts?.Cancel();

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

        private async void OnMenuClicked(object sender, EventArgs e)
        {
            if (isAnimating) return;

            if (!MenuPopup.IsVisible)
                await ShowMenuAsync();
            else
                await HideMenuAsync();
        }

        private async void OnDismissTapped(object sender, TappedEventArgs e)
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
                UpdateAllButtonStyles();

                Overlay.IsVisible = true;
                Overlay.Opacity = 0;

                TocButtonContainer.IsVisible = false;

                MenuPopup.IsVisible = true;
                MenuPopup.Opacity = 0;
                MenuPopup.Scale = 1.0;
                MenuPopup.TranslationY = MenuHiddenY;

                await Task.WhenAll(
                    Overlay.FadeTo(1, 120, Easing.CubicOut),
                    MenuPopup.FadeTo(1, MenuAnimMs, Easing.CubicOut),
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

                if (chromeVisible)
                    TocButtonContainer.IsVisible = true;

                return;
            }

            if (isAnimating) return;
            isAnimating = true;

            try
            {
                await Task.WhenAll(
                    Overlay.FadeTo(0, 100, Easing.CubicIn),
                    MenuPopup.FadeTo(0, 100, Easing.CubicIn),
                    MenuPopup.TranslateTo(0, MenuHiddenY, 100, Easing.CubicIn)
                );

                MenuPopup.IsVisible = false;
                Overlay.IsVisible = false;

                if (chromeVisible)
                    TocButtonContainer.IsVisible = true;
            }
            finally
            {
                Overlay.Opacity = 0;
                MenuPopup.Opacity = 0;
                MenuPopup.TranslationY = MenuRestY;
                isAnimating = false;
            }
        }

        private async void OnFontPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out var idx))
            {
                idx = Math.Clamp(idx, 0, fontSizes.Length - 1);
                if (idx == fontIndex) return;

                fontIndex = idx;
                await ApplyTypographyAsync();
            }
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

            UpdateAllButtonStyles();

            ClearRenderedPageCache();
            RequestRepaginate();

            await SaveCurrentReadingSettings();
        }

        private static string NormalizeTheme(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
                return "White";

            stored = stored.Trim();

            return stored switch
            {
                "White" => "White",
                "Beige" => "Beige",
                "Green" => "Green",
                "Blue" => "Blue",
                "Dark" => "Dark",
                "Light" => "White",
                _ => "White"
            };
        }

        private void ApplyTheme(string modeValue)
        {
            themeMode = modeValue switch
            {
                "Beige" => "Beige",
                "Green" => "Green",
                "Blue" => "Blue",
                "Dark" => "Dark",
                _ => "White"
            };

            switch (themeMode)
            {
                case "Beige":
                    PageBgColor = Color.FromArgb("#D8D2BE");
                    TextColorReader = Color.FromArgb("#3F372B");
                    SubtleTextColor = Color.FromArgb("#6E6659");
                    PrimaryAccent = Color.FromArgb("#3F372B");
                    SurfaceColor = Color.FromArgb("#F5F1E6");
                    SoftSurfaceColor = Color.FromArgb("#E7E0CF");
                    BorderColor = Color.FromArgb("#C8BEA7");
                    break;

                case "Green":
                    PageBgColor = Color.FromArgb("#C9D5B9");
                    TextColorReader = Color.FromArgb("#33402C");
                    SubtleTextColor = Color.FromArgb("#61705B");
                    PrimaryAccent = Color.FromArgb("#33402C");
                    SurfaceColor = Color.FromArgb("#EEF4E6");
                    SoftSurfaceColor = Color.FromArgb("#DDE7D0");
                    BorderColor = Color.FromArgb("#B6C5A2");
                    break;

                case "Blue":
                    PageBgColor = Color.FromArgb("#C4D2E2");
                    TextColorReader = Color.FromArgb("#31404F");
                    SubtleTextColor = Color.FromArgb("#617284");
                    PrimaryAccent = Color.FromArgb("#31404F");
                    SurfaceColor = Color.FromArgb("#EEF4FA");
                    SoftSurfaceColor = Color.FromArgb("#D8E3EF");
                    BorderColor = Color.FromArgb("#AFC2D8");
                    break;

                case "Dark":
                    PageBgColor = Color.FromArgb("#101014");
                    TextColorReader = Color.FromArgb("#EDEAF6");
                    SubtleTextColor = Color.FromArgb("#BDB8C8");
                    PrimaryAccent = Color.FromArgb("#EDEAF6");
                    SurfaceColor = Color.FromArgb("#17171C");
                    SoftSurfaceColor = Color.FromArgb("#20202A");
                    BorderColor = Color.FromArgb("#2A2A32");
                    break;

                default:
                    PageBgColor = Color.FromArgb("#F7F6FB");
                    TextColorReader = Color.FromArgb("#3E3A4A");
                    SubtleTextColor = Color.FromArgb("#6A6577");
                    PrimaryAccent = Color.FromArgb("#24145A");
                    SurfaceColor = Color.FromArgb("#FFFFFF");
                    SoftSurfaceColor = Color.FromArgb("#F1EEFF");
                    BorderColor = Color.FromArgb("#E5E1F2");
                    break;
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
            ContentWebView.BackgroundColor = PageBgColor;

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
            HandleBar.BackgroundColor = BorderColor;

            UpdatePopupTextColors();
            UpdateSegmentBackgrounds();
        }

        private void UpdateSegmentBackgrounds()
        {
            Color segmentColor =
                themeMode == "Dark"
                    ? Color.FromArgb("#24242C")
                    : Color.FromArgb("#F1F1F3");

            FontSegmentContainer.BackgroundColor = segmentColor;
            SpacingSegmentContainer.BackgroundColor = segmentColor;
        }

        private void UpdatePopupTextColors()
        {
            foreach (var child in GetDescendants(MenuPopup))
            {
                if (child is Label lbl)
                    lbl.TextColor = TextColorReader;

                if (child is Button btn && btn == ThemeDarkBtn)
                    btn.TextColor = Color.FromArgb("#A5A7AE");
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
            if (sender is Button btn && btn.CommandParameter is string modeValue)
            {
                ApplyTheme(modeValue);
                await SaveCurrentReadingSettings();
            }
        }

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
            StyleThemeChip(ThemeWhiteBtn, themeMode == "White", "#F7F6FB", false);
            StyleThemeChip(ThemeBeigeBtn, themeMode == "Beige", "#D8D2BE", false);
            StyleThemeChip(ThemeGreenBtn, themeMode == "Green", "#C9D5B9", false);
            StyleThemeChip(ThemeBlueBtn, themeMode == "Blue", "#C4D2E2", false);
            StyleThemeChip(ThemeDarkBtn, themeMode == "Dark", "#101014", true);
        }

        private void StyleSegmentButton(Button button, bool selected)
        {
            button.Shadow = null;
            button.BorderWidth = 0;

            if (selected)
            {
                button.BackgroundColor =
                    themeMode == "Dark"
                        ? Color.FromArgb("#2F2F39")
                        : Color.FromArgb("#FFFFFF");

                button.TextColor =
                    themeMode == "Dark"
                        ? Colors.White
                        : Color.FromArgb("#111111");
            }
            else
            {
                button.BackgroundColor = Colors.Transparent;
                button.TextColor =
                    themeMode == "Dark"
                        ? Color.FromArgb("#E2E2E8")
                        : Color.FromArgb("#111111");
            }
        }

        private void StyleThemeChip(Button button, bool selected, string bgHex, bool isDarkChip)
        {
            button.Shadow = null;
            button.BackgroundColor = Color.FromArgb(bgHex);
            button.BorderWidth = selected ? 2 : 0;
            button.BorderColor =
                selected
                    ? (themeMode == "Dark"
                        ? Color.FromArgb("#FFFFFF")
                        : Color.FromArgb("#3A3A3A"))
                    : Colors.Transparent;

            if (isDarkChip)
            {
                button.Text = "☾";
                button.TextColor = Color.FromArgb("#A5A7AE");
            }
            else
            {
                button.Text = "";
                button.TextColor = Colors.Transparent;
            }
        }
    }
}