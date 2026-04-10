using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
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
using E_Book.Services;
using E_Book.Models;

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

        private bool _isReaderLoading;
        private bool _deferFirstDisplayUntilFullPagination;
        private bool _hasDisplayedInitialPage;

        private const uint PageAnimMs = 120;
        private const double SlideDistance = 28;

        private const uint MenuAnimMs = 180;
        private const double MenuRestY = 0;
        private const double MenuHiddenY = 20;

        private const uint ChromeAnimMs = 140;

        private const int InitialTxtPreloadParagraphCount = 1200;

        private bool _documentParseRunning;
        private int _documentParseVersion = 0;

        private const double TxtWidthPadding = 64;
        private const double TxtHeightPaddingChinese = 190;
        private const double TxtHeightPaddingEnglish = 132;

        private const double HtmlWidthPadding = 58;
        private const double HtmlHeightPadding = 210;

        private readonly List<string> _rawHtmlChapterTitles = new();

        private CancellationTokenSource? _documentLoadCts;

        private enum ReaderMode { TxtPaged, DocumentPaged, Unknown }
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

        private DateTime _sessionStartUtc;
        private bool _sessionOpened;
        private bool _shouldRestoreImmersiveChrome = true;

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
                HideReaderContentForLoading();
                SetReaderLoading(true, "No file selected", "Please return and choose a book.");
                UpdateProgressUI();
                return;
            }

            if (_loadedFilePath == FilePath)
                return;

            _loadedFilePath = FilePath;
            _hasDisplayedInitialPage = false;

            await InitializeReaderAsync();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            _documentLoadCts?.Cancel();
            _repaginateCts?.Cancel();
            _progressSaveCts?.Cancel();

            try
            {
                await SaveReadingProgress();
                SaveReadingDuration();
                await SaveCurrentReadingSettings();
                SaveContinueReadingReminderState();
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
            _documentLoadCts?.Cancel();
            _documentLoadCts = new CancellationTokenSource();

            try
            {
                _readerInitialized = false;
                _deferFirstDisplayUntilFullPagination = false;
                _hasDisplayedInitialPage = false;

                var readingSettings = await dbHelper.GetReadingSettingsAsync();

                int idx = Array.IndexOf(fontSizes, readingSettings.FontSize);
                fontIndex = idx >= 0 ? idx : 1;
                currentFontSize = fontSizes[fontIndex];

                themeMode = NormalizeTheme(readingSettings.BackgroundColor);
                ApplyTheme(themeMode);

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

                ShowLoadingPage();

                await LoadByTypeAsync(FilePath, _documentLoadCts.Token);

                var progress = await dbHelper.GetReadingProgressRecordAsync(GetReadingKey());
                _pendingRestorePage = progress.LastPage;

                if (mode == ReaderMode.TxtPaged)
                {
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
                }
                else
                {
                    _pendingRestoreProgress = 0;
                    currentPage = Math.Clamp(progress.LastPage, 0, Math.Max(0, GetTotalPages() - 1));
                }

                ClampCurrentPage();

                bool hasSavedProgress = progress.LastPage > 0;
                bool waitForFullTxtPagination =
                    mode == ReaderMode.TxtPaged &&
                    _txtPaginationIsPartial &&
                    hasSavedProgress;

                _deferFirstDisplayUntilFullPagination = waitForFullTxtPagination;

                if (waitForFullTxtPagination)
                {
                    SetReaderLoading(true, "Restoring your page...", "Finishing pagination for accurate position");
                    await StartBackgroundFullTxtPaginationAsync();
                }
                else
                {
                    await Task.Yield();
                    DisplayPage();
                    UpdateProgressUI();
                    WarmupNearbyPages();
                    TrimRenderedPageCache();
                    ShowReaderContent();
                    _hasDisplayedInitialPage = true;
                    await SaveReadingProgress();
                }

                ReadingMetaStore.UpdateLastOpened(FilePath);
                _sessionStartUtc = DateTime.UtcNow;
                _sessionOpened = true;

                _readerInitialized = true;

                if (!_enterAnimationPlayed)
                {
                    _enterAnimationPlayed = true;
                    await PlayEnterAnimationAsync();
                }

                if (_shouldRestoreImmersiveChrome)
                    EnterImmersiveModeImmediately();
            }
            catch (Exception ex)
            {
                _readerInitialized = false;
                SetReaderLoading(true, "Unable to open book", ex.Message);
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void SetReaderLoading(bool isLoading, string? title = null, string? subtitle = null)
        {
            _isReaderLoading = isLoading;

            if (ReaderLoadingOverlay == null)
                return;

            ReaderLoadingOverlay.IsVisible = isLoading;
            ReaderLoadingOverlay.Opacity = isLoading ? 1 : 0;

            if (ReaderLoadingText != null && !string.IsNullOrWhiteSpace(title))
                ReaderLoadingText.Text = title;

            if (ReaderLoadingSubText != null && !string.IsNullOrWhiteSpace(subtitle))
                ReaderLoadingSubText.Text = subtitle;
        }

        private void ShowReaderContent()
        {
            if (ContentWebView != null)
                ContentWebView.IsVisible = true;

            if (fileContentLabel != null)
                fileContentLabel.IsVisible = false;

            SetReaderLoading(false);
        }

        private void HideReaderContentForLoading()
        {
            if (ContentWebView != null)
                ContentWebView.IsVisible = false;

            if (fileContentLabel != null)
                fileContentLabel.IsVisible = false;
        }

        private void ShowLoadingPage()
        {
            HideReaderContentForLoading();
            SetReaderLoading(true, "Loading book...", "Preparing your reading page");
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
            return FilePath?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private void ClearAllReaderState()
        {
            txtParagraphs.Clear();
            txtPages.Clear();
            txtParagraphStartPageIndices.Clear();

            htmlPages.Clear();
            _rawHtmlChapters.Clear();
            _rawHtmlChapterKeys.Clear();
            _rawHtmlChapterTitles.Clear();
            _chapterStartPageIndices.Clear();

            tocItems.Clear();
            currentPage = 0;

            _txtPaginationIsPartial = false;
            _txtPreloadedParagraphCount = 0;
            _isFullTxtPaginationRunning = false;

            ClearRenderedPageCache();
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
            if (_documentParseRunning)
                return;

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
                        int oldPage = currentPage;

                        var result = await Task.Run(() =>
                            BuildTxtPaginationResult(GetReaderAreaWidth(), GetReaderAreaHeight(), null));

                        if (token.IsCancellationRequested) return;

                        ApplyTxtPaginationResult(result);
                        BuildTxtToc();

                        currentPage = Math.Min(oldPage, Math.Max(0, txtPages.Count - 1));
                    }
                    else if (mode == ReaderMode.DocumentPaged)
                    {
                        int oldPage = currentPage;

                        await Task.Run(() => RebuildHtmlPagination());

                        if (token.IsCancellationRequested) return;

                        tocItems.Clear();

                        if (Path.GetExtension(FilePath)?.Equals(".epub", StringComparison.OrdinalIgnoreCase) == true)
                            BuildEpubTocPreferNcxOrNav(FilePath);

                        if (tocItems.Count == 0)
                            BuildDocumentTocFromPages();

                        currentPage = Math.Min(oldPage, Math.Max(0, htmlPages.Count - 1));
                    }

                    ClampCurrentPage();

                    if (!_isReaderLoading || _hasDisplayedInitialPage)
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

        private void EnterImmersiveModeImmediately()
        {
            chromeVisible = false;

            if (TopBar != null)
            {
                TopBar.IsVisible = false;
                TopBar.Opacity = 0;
                TopBar.TranslationY = -10;
            }

            if (BottomBar != null)
            {
                BottomBar.IsVisible = false;
                BottomBar.Opacity = 0;
                BottomBar.TranslationY = 10;
            }

            if (TocButtonContainer != null)
            {
                TocButtonContainer.IsVisible = false;
                TocButtonContainer.Opacity = 0;
                TocButtonContainer.TranslationY = 10;
            }
        }

        private void SaveReadingDuration()
        {
            if (!_sessionOpened || string.IsNullOrWhiteSpace(FilePath))
                return;

            try
            {
                long seconds = (long)Math.Floor((DateTime.UtcNow - _sessionStartUtc).TotalSeconds);
                if (seconds > 0)
                    ReadingMetaStore.AddReadingDuration(FilePath, seconds);
            }
            catch
            {
            }
            finally
            {
                _sessionOpened = false;
                _sessionStartUtc = DateTime.UtcNow;
            }
        }

        private void SaveContinueReadingReminderState()
        {
            try
            {
                bool continueEnabled = Preferences.Get(NotificationPrefs.ContinueReadingEnabled, true);
                if (!continueEnabled)
                    return;

                if (string.IsNullOrWhiteSpace(FilePath))
                    return;

                int totalPages = GetTotalPages();
                if (totalPages <= 0)
                    return;

                string bookTitle = Path.GetFileNameWithoutExtension(FilePath);
                int pageNumber = Math.Max(1, currentPage + 1);

                Preferences.Set(NotificationPrefs.PendingBookTitle, bookTitle);
                Preferences.Set(NotificationPrefs.PendingBookPage, pageNumber);
                Preferences.Set(NotificationPrefs.PendingBookTime, DateTime.Now.ToString("O"));
            }
            catch
            {
            }
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

        private async Task LoadByTypeAsync(string filePath, CancellationToken token)
        {
            _readerInitialized = false;

            ClearAllReaderState();

            string ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            int version = ++_documentParseVersion;

            try
            {
                switch (ext)
                {
                    case ".txt":
                        {
                            mode = ReaderMode.TxtPaged;

                            ParsedReadingContent parsed =
                                await DocumentContentService.ParseAsync(filePath, token);

                            token.ThrowIfCancellationRequested();

                            if (version != _documentParseVersion)
                                return;

                            txtParagraphs.Clear();
                            txtParagraphs.AddRange(parsed.TxtParagraphs);

                            double width = GetReaderAreaWidth();
                            double height = GetReaderAreaHeight();

                            var pagination = await Task.Run(() =>
                                BuildTxtPaginationResult(
                                    width,
                                    height,
                                    InitialTxtPreloadParagraphCount), token);

                            token.ThrowIfCancellationRequested();

                            ApplyTxtPaginationResult(pagination);
                            BuildTxtToc();

                            break;
                        }

                    case ".epub":
                    case ".html":
                    case ".htm":
                    case ".docx":
                    case ".rtf":
                        {
                            mode = ReaderMode.DocumentPaged;
                            _documentParseRunning = true;

                            ParsedReadingContent parsed =
                                await DocumentContentService.ParseAsync(filePath, token);

                            token.ThrowIfCancellationRequested();

                            if (version != _documentParseVersion)
                                return;

                            _rawHtmlChapters.Clear();
                            _rawHtmlChapterKeys.Clear();
                            _rawHtmlChapterTitles.Clear();

                            _rawHtmlChapters.AddRange(parsed.RawHtmlChapters);
                            _rawHtmlChapterKeys.AddRange(parsed.RawHtmlChapterKeys);
                            _rawHtmlChapterTitles.AddRange(parsed.RawHtmlChapterTitles);

                            await Task.Run(() => RebuildHtmlPagination(), token);

                            token.ThrowIfCancellationRequested();

                            if (ext == ".epub")
                                BuildEpubTocPreferNcxOrNav(filePath);
                            else
                                BuildDocumentTocFromPages();

                            _documentParseRunning = false;
                            break;
                        }

                    default:
                        throw new NotSupportedException("Unsupported file type");
                }

                _readerInitialized = true;
            }
            catch (OperationCanceledException)
            {
                _documentParseRunning = false;
                throw;
            }
            catch (Exception ex)
            {
                _documentParseRunning = false;
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private void ShowWebView()
        {
            ContentWebView.IsVisible = true;
            fileContentLabel.IsVisible = false;
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

            bool mostlyChinese = ContainsMostlyChinese(string.Join("", txtParagraphs.Take(80)));

            double usableWidth = Math.Max(180, areaWidth - TxtWidthPadding);

            double usableHeight = mostlyChinese
                ? Math.Max(180, areaHeight - TxtHeightPaddingChinese)
                : Math.Max(180, areaHeight - TxtHeightPaddingEnglish);

            double lineHeightPx = GetReaderCssFontSize() * currentLineSpacing * 1.04;

            double paragraphSpacingPx = mostlyChinese ? 18 : 8;
            double blankParagraphPx = mostlyChinese ? 24 : 12;

            double currentHeight = 0;
            var currentPageParagraphs = new List<string>();

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

                bool isEnglishParagraph = LooksLikeEnglishText(paragraph);

                if (estimatedHeight > usableHeight)
                {
                    if (currentPageParagraphs.Count > 0)
                    {
                        result.Pages.Add(new List<string>(currentPageParagraphs));
                        currentPageParagraphs.Clear();
                        currentHeight = 0;
                    }

                    int startPageIndex = result.Pages.Count;
                    result.ParagraphStartPageIndices.Add(startPageIndex);

                    var splitParagraphs = SplitLongParagraphForPaging(
                        paragraph,
                        usableWidth,
                        usableHeight,
                        lineHeightPx,
                        paragraphSpacingPx,
                        blankParagraphPx,
                        mostlyChinese);

                    foreach (var part in splitParagraphs)
                        result.Pages.Add(new List<string> { part });

                    continue;
                }

                if (currentPageParagraphs.Count > 0)
                {
                    double nextHeight = currentHeight + estimatedHeight;

                    bool allowSoftFit =
                        !mostlyChinese &&
                        isEnglishParagraph &&
                        nextHeight <= usableHeight + lineHeightPx * 1.2;

                    if (nextHeight > usableHeight && !allowSoftFit)
                    {
                        result.Pages.Add(new List<string>(currentPageParagraphs));
                        currentPageParagraphs.Clear();
                        currentHeight = 0;
                    }
                }

                result.ParagraphStartPageIndices.Add(result.Pages.Count);

                currentPageParagraphs.Add(paragraph);
                currentHeight += estimatedHeight;
            }

            if (currentPageParagraphs.Count > 0)
                result.Pages.Add(new List<string>(currentPageParagraphs));

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

                    ShowReaderContent();
                    _hasDisplayedInitialPage = true;
                    _deferFirstDisplayUntilFullPagination = false;
                    _ = SaveReadingProgress();
                });
            }
            catch
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_hasDisplayedInitialPage)
                    {
                        DisplayPage();
                        UpdateProgressUI();
                        WarmupNearbyPages();
                        TrimRenderedPageCache();
                        ShowReaderContent();
                        _hasDisplayedInitialPage = true;
                    }
                });
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

            double fontPx = GetReaderCssFontSize();
            double avgCharWidth = isEnglish ? fontPx * 0.38 : fontPx * 1.0;
            double firstLineIndentWidth = isEnglish ? 0 : fontPx * 2.0;

            int charsFirstLine = Math.Max(4, (int)((usableWidth - firstLineIndentWidth) / avgCharWidth));
            int charsOtherLines = Math.Max(6, (int)(usableWidth / avgCharWidth));

            int maxLinesPerPage = Math.Max(3, (int)Math.Floor((usableHeight - paragraphSpacingPx - 10) / lineHeightPx));
            int maxCharsFirstPage = charsFirstLine + Math.Max(0, maxLinesPerPage - 1) * charsOtherLines;
            int maxCharsNormalPage = Math.Max(charsOtherLines * maxLinesPerPage, charsOtherLines * 3);

            if (paragraph.Length <= maxCharsFirstPage)
            {
                result.Add(paragraph);
                return result;
            }

            var units = isEnglish
                ? SplitEnglishUnits(paragraph)
                : SplitChineseUnits(paragraph);

            var current = new StringBuilder();
            bool firstChunk = true;

            foreach (var unit in units)
            {
                int limit = firstChunk ? maxCharsFirstPage : maxCharsNormalPage;

                if (current.Length > 0 && current.Length + unit.Length > limit)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    firstChunk = false;
                }

                current.Append(unit);
            }

            if (current.Length > 0)
                result.Add(current.ToString().Trim());

            return result.Count > 0 ? result : new List<string> { paragraph };
        }

        private List<string> SplitChineseUnits(string text)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            var parts = Regex.Split(text, @"(?<=[。！？；：])|(?<=[，、])");

            foreach (var p in parts)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    result.Add(p);
            }

            if (result.Count == 0)
                result.Add(text);

            return result;
        }

        private List<string> SplitEnglishUnits(string text)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");

            foreach (var sentence in sentences)
            {
                if (string.IsNullOrWhiteSpace(sentence))
                    continue;

                var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                    continue;

                var sb = new StringBuilder();
                foreach (var word in words)
                {
                    if (sb.Length > 0)
                        sb.Append(' ');
                    sb.Append(word);

                    if (word.EndsWith(",") || word.EndsWith(".") || word.EndsWith("!") || word.EndsWith("?") || word.EndsWith(";") || word.EndsWith(":"))
                        sb.Append(' ');
                }

                result.Add(sb.ToString());
            }

            if (result.Count == 0)
                result.Add(text);

            return result;
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

            if (Regex.IsMatch(trimmed, @"^第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇]"))
                return lineHeightPx * 2.0 + 26;

            if (Regex.IsMatch(trimmed, @"^[_\-─—=·•\.]{5,}$"))
                return lineHeightPx + 12;

            var mdHeading = Regex.Match(trimmed, @"^\s*#{1,6}\s+(.*)$");
            if (mdHeading.Success)
            {
                string headingText = mdHeading.Groups[1].Value.Trim();
                int headingCharsPerLine = Math.Max(6, (int)(usableWidth / (GetReaderCssFontSize() * 0.92)));
                int headingLinesNeeded = Math.Max(1, (int)Math.Ceiling(headingText.Length / (double)headingCharsPerLine));
                return headingLinesNeeded * (lineHeightPx * 1.12) + 18;
            }

            bool isEnglish = LooksLikeEnglishText(paragraph);

            double fontPx = GetReaderCssFontSize();

            double avgCharWidth = isEnglish
                ? fontPx * 0.38
                : fontPx * 1.0;

            double firstLineIndentWidth = isEnglish ? 0 : fontPx * 2.0;

            int charsFirstLine = Math.Max(4, (int)((usableWidth - firstLineIndentWidth) / avgCharWidth));
            int charsOtherLines = Math.Max(6, (int)(usableWidth / avgCharWidth));

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

            double safetyExtra = isEnglish ? 0 : 12;

            return paragraphLinesNeeded * lineHeightPx +
                   paragraphSpacingPx +
                   safetyExtra;
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

                if (Regex.IsMatch(trimmed, @"^第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇]"))
                {
                    sb.Append($"<p class='chapter-title'>{WebUtility.HtmlEncode(trimmed)}</p>");
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

        private List<string> PaginateHtmlContent(string html)
        {
            var pages = new List<string>();

            if (string.IsNullOrWhiteSpace(html))
            {
                pages.Add("<p>(Empty)</p>");
                return pages;
            }

            var blocks = SplitHtmlIntoBlocks(html);

            if (blocks.Count == 0)
            {
                pages.Add(html);
                return pages;
            }

            double areaWidth = GetReaderAreaWidth();
            double areaHeight = GetReaderAreaHeight();

            double usableWidth = Math.Max(180, areaWidth - HtmlWidthPadding);
            double usableHeight = Math.Max(180, areaHeight - HtmlHeightPadding);

            double lineHeightPx = GetReaderCssFontSize() * currentLineSpacing * 1.18;
            int maxLinesPerPage = Math.Max(4, (int)Math.Floor(usableHeight / lineHeightPx));

            int currentLines = 0;
            var currentPageBlocks = new List<string>();

            foreach (var rawBlock in blocks)
            {
                string block = rawBlock?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(block))
                    continue;

                int estimatedLines = EstimateHtmlBlockLines(block, usableWidth);

                bool isAtomicBlock =
                    Regex.IsMatch(block, @"^<h[1-6]\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(block, @"^<img\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(block, @"^<hr\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(block, @"^<table\b", RegexOptions.IgnoreCase);

                if ((isAtomicBlock || estimatedLines <= maxLinesPerPage) &&
                    currentLines + estimatedLines > maxLinesPerPage &&
                    currentPageBlocks.Count > 0)
                {
                    pages.Add(string.Join(Environment.NewLine, currentPageBlocks));
                    currentPageBlocks.Clear();
                    currentLines = 0;
                }

                if (!isAtomicBlock && estimatedLines > maxLinesPerPage)
                {
                    var splitParts = SplitHtmlParagraphBlockForPaging(block, usableWidth, maxLinesPerPage);

                    foreach (var part in splitParts)
                    {
                        int partLines = EstimateHtmlBlockLines(part, usableWidth);

                        if (currentLines + partLines > maxLinesPerPage && currentPageBlocks.Count > 0)
                        {
                            pages.Add(string.Join(Environment.NewLine, currentPageBlocks));
                            currentPageBlocks.Clear();
                            currentLines = 0;
                        }

                        currentPageBlocks.Add(part);
                        currentLines += partLines;
                    }

                    continue;
                }

                currentPageBlocks.Add(block);
                currentLines += estimatedLines;
            }

            if (currentPageBlocks.Count > 0)
                pages.Add(string.Join(Environment.NewLine, currentPageBlocks));

            return pages.Count > 0 ? pages : new List<string> { html };
        }

        private List<string> SplitHtmlIntoBlocks(string html)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(html))
                return result;

            var matches = Regex.Matches(
                html,
                @"(<h[1-6][^>]*>.*?</h[1-6]>|<p[^>]*>.*?</p>|<div[^>]*>.*?</div>|<blockquote[^>]*>.*?</blockquote>|<ul[^>]*>.*?</ul>|<ol[^>]*>.*?</ol>|<table[^>]*>.*?</table>|<img[^>]*?/?>|<hr[^>]*?/?>)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var value = match.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    result.Add(value);
            }

            if (result.Count == 0)
            {
                string trimmed = html.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    result.Add(trimmed);
            }

            return result;
        }

        private int EstimateHtmlBlockLines(string htmlBlock, double usableWidth)
        {
            if (string.IsNullOrWhiteSpace(htmlBlock))
                return 1;

            string plainText = StripHtmlTags(htmlBlock);
            plainText = HtmlEntityDecodeLite(plainText);

            bool mostlyChinese = ContainsMostlyChinese(plainText);

            double fontPx = GetReaderCssFontSize();
            double avgCharWidth = mostlyChinese ? fontPx * 0.95 : fontPx * 0.48;
            int charsPerLine = Math.Max(8, (int)(usableWidth / avgCharWidth));

            int lines = Math.Max(1, (int)Math.Ceiling(Math.Max(1, plainText.Length) / (double)charsPerLine));

            if (Regex.IsMatch(htmlBlock, @"^<h[1-6]\b", RegexOptions.IgnoreCase))
                lines += 1;
            else if (Regex.IsMatch(htmlBlock, @"^<blockquote\b", RegexOptions.IgnoreCase))
                lines += 1;
            else if (Regex.IsMatch(htmlBlock, @"^<(ul|ol)\b", RegexOptions.IgnoreCase))
                lines += 2;
            else if (Regex.IsMatch(htmlBlock, @"^<img\b", RegexOptions.IgnoreCase))
                lines += 8;
            else if (Regex.IsMatch(htmlBlock, @"^<table\b", RegexOptions.IgnoreCase))
                lines += 10;
            else if (Regex.IsMatch(htmlBlock, @"^<hr\b", RegexOptions.IgnoreCase))
                lines = 1;

            return Math.Max(1, lines);
        }

        private List<string> SplitHtmlParagraphBlockForPaging(string htmlBlock, double usableWidth, int maxLinesPerPage)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(htmlBlock))
            {
                result.Add(htmlBlock);
                return result;
            }

            string tagName = GetHtmlOuterTagName(htmlBlock);

            bool splittable =
                tagName.Equals("p", StringComparison.OrdinalIgnoreCase) ||
                tagName.Equals("div", StringComparison.OrdinalIgnoreCase) ||
                tagName.Equals("blockquote", StringComparison.OrdinalIgnoreCase);

            if (!splittable)
            {
                result.Add(htmlBlock);
                return result;
            }

            string innerHtml = ExtractInnerHtml(htmlBlock);
            string plainText = HtmlEntityDecodeLite(StripHtmlTags(innerHtml));

            if (string.IsNullOrWhiteSpace(plainText))
            {
                result.Add(htmlBlock);
                return result;
            }

            bool mostlyChinese = ContainsMostlyChinese(plainText);

            var segments = mostlyChinese
                ? SplitChineseTextForPaging(plainText)
                : SplitEnglishTextForPaging(plainText);

            double fontPx = GetReaderCssFontSize();
            double avgCharWidth = mostlyChinese ? fontPx * 0.95 : fontPx * 0.46;
            int charsPerLine = Math.Max(8, (int)(usableWidth / avgCharWidth));
            int maxCharsPerPage = Math.Max(charsPerLine * maxLinesPerPage, charsPerLine * 3);

            var current = new StringBuilder();

            foreach (var seg in segments)
            {
                if (string.IsNullOrWhiteSpace(seg))
                    continue;

                string candidate = current.Length == 0
                    ? seg.Trim()
                    : current.ToString() + seg;

                if (candidate.Length > maxCharsPerPage && current.Length > 0)
                {
                    string wrapped = WrapTextBackIntoHtmlBlock(tagName, current.ToString().Trim(), htmlBlock);
                    result.Add(wrapped);
                    current.Clear();
                    current.Append(seg.Trim());
                }
                else
                {
                    current.Append(seg);
                }
            }

            if (current.Length > 0)
            {
                string wrapped = WrapTextBackIntoHtmlBlock(tagName, current.ToString().Trim(), htmlBlock);
                result.Add(wrapped);
            }

            return result.Count > 0 ? result : new List<string> { htmlBlock };
        }

        private List<string> SplitChineseTextForPaging(string text)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            var parts = Regex.Split(text, @"(?<=[。！？；：])|(?<=[，、])");

            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                    result.Add(part);
            }

            if (result.Count == 0)
                result.Add(text);

            return result;
        }

        private List<string> SplitEnglishTextForPaging(string text)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            var sentenceParts = Regex.Split(text, @"(?<=[\.!\?])\s+");

            foreach (var sentence in sentenceParts)
            {
                if (string.IsNullOrWhiteSpace(sentence))
                    continue;

                if (sentence.Length <= 160)
                {
                    result.Add(sentence.Trim() + " ");
                }
                else
                {
                    var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var sb = new StringBuilder();

                    foreach (var word in words)
                    {
                        string candidate = sb.Length == 0 ? word : sb + " " + word;

                        if (candidate.Length > 140 && sb.Length > 0)
                        {
                            result.Add(sb.ToString().Trim() + " ");
                            sb.Clear();
                            sb.Append(word);
                        }
                        else
                        {
                            if (sb.Length > 0)
                                sb.Append(' ');
                            sb.Append(word);
                        }
                    }

                    if (sb.Length > 0)
                        result.Add(sb.ToString().Trim() + " ");
                }
            }

            if (result.Count == 0)
                result.Add(text);

            return result;
        }

        private string GetHtmlOuterTagName(string htmlBlock)
        {
            var match = Regex.Match(htmlBlock, @"^<\s*(\w+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : "p";
        }

        private string ExtractInnerHtml(string htmlBlock)
        {
            var match = Regex.Match(
                htmlBlock,
                @"^<\s*(\w+)[^>]*>(.*)</\s*\1\s*>$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (match.Success)
                return match.Groups[2].Value;

            return StripOuterTagFallback(htmlBlock);
        }

        private string StripOuterTagFallback(string htmlBlock)
        {
            string s = Regex.Replace(htmlBlock, @"^<[^>]+>", "", RegexOptions.Singleline);
            s = Regex.Replace(s, @"</[^>]+>$", "", RegexOptions.Singleline);
            return s;
        }

        private string WrapTextBackIntoHtmlBlock(string tagName, string plainText, string originalBlock)
        {
            string encoded = WebUtility.HtmlEncode(plainText);

            if (tagName.Equals("blockquote", StringComparison.OrdinalIgnoreCase))
                return $"<blockquote>{encoded}</blockquote>";

            if (tagName.Equals("div", StringComparison.OrdinalIgnoreCase))
                return $"<div><p>{encoded}</p></div>";

            return $"<p>{encoded}</p>";
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
            if (mode != ReaderMode.DocumentPaged)
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
                ReaderMode.DocumentPaged => htmlPages.Count == 0 ? 0 : htmlPages.Count,
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

            bool isLastPage = mode switch
            {
                ReaderMode.TxtPaged => txtPages.Count > 0 && currentPage == txtPages.Count - 1,
                ReaderMode.DocumentPaged => htmlPages.Count > 0 && currentPage == htmlPages.Count - 1,
                _ => false
            };

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
            else if (mode == ReaderMode.DocumentPaged)
            {
                pageHtml = BuildHtmlPageHtml();
            }
            else
            {
                return;
            }

            string wrapped = WrapHtml(pageHtml, isLastPage);
            _renderedPageCache[currentPage] = wrapped;

            ShowWebView();
            ContentWebView.Source = new HtmlWebViewSource { Html = wrapped };
            _hasDisplayedInitialPage = true;
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
            if (mode == ReaderMode.DocumentPaged)
                return;

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

                bool isLastPage = mode switch
                {
                    ReaderMode.TxtPaged => txtPages.Count > 0 && index == txtPages.Count - 1,
                    ReaderMode.DocumentPaged => htmlPages.Count > 0 && index == htmlPages.Count - 1,
                    _ => false
                };

                _renderedPageCache[index] = WrapHtml(pageHtml, isLastPage);
            }

            currentPage = original;
            TrimRenderedPageCache();
        }

        private string WrapHtml(string bodyHtml, bool isLastPage = false)
        {
            string bg = ToCssColor(PageBgColor);
            string fg = ToCssColor(TextColorReader);
            string subtle = ToCssColor(SubtleTextColor);
            string border = ToCssColor(BorderColor);
            string accent = ToCssColor(Color.FromArgb("#7B6CFF"));

            int px = GetReaderCssFontSize();
            string lineHeightCss = currentLineSpacing.ToString(CultureInfo.InvariantCulture);
            string wrapClass = isLastPage ? "wrap last-page" : "wrap";

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
        min-height: 100%;
    }}

    body {{
        -webkit-text-size-adjust: 100%;
        text-size-adjust: 100%;
        padding-bottom: env(safe-area-inset-bottom);
        box-sizing: border-box;
    }}

    .wrap {{
        padding: 24px 22px 56px 22px;
        max-width: 900px;
        margin: auto;
        box-sizing: border-box;
        background: {bg};
        min-height: 100vh;
        display: flex;
        flex-direction: column;
        justify-content: flex-start;
    }}

    .wrap.last-page {{
        justify-content: center;
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
        margin: 0 0 0 0;
        text-indent: 0;
        text-align: left;
        word-break: normal;
        overflow-wrap: break-word;
        line-break: auto;
        line-height: 1.00;
    }}

    p.chapter-title {{
        font-weight: 600;
        text-align: center;
        font-size: 1.2em;
        margin: 18px 0 12px 0;
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
        margin: 12px auto;
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
<div class='{wrapClass}'>
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
                else if (mode == ReaderMode.DocumentPaged)
                {
                    if (Path.GetExtension(FilePath)?.Equals(".epub", StringComparison.OrdinalIgnoreCase) == true)
                        BuildEpubTocPreferNcxOrNav(FilePath);

                    if (tocItems.Count == 0)
                        BuildDocumentTocFromPages();
                }
            }

            if (tocItems.Count == 0)
            {
                await DisplayAlert("TOC", "No TOC items found.", "OK");
                return;
            }

            const int MaxItems = 60;
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

            var chapterRegex =
                new Regex(
                    @"^(第.{1,9}[章回节卷篇部]|chapter\s+\d+)",
                    RegexOptions.IgnoreCase);

            for (int i = 0; i < txtParagraphs.Count; i++)
            {
                string paragraph = txtParagraphs[i].Trim();

                if (!chapterRegex.IsMatch(paragraph))
                    continue;

                int pageIndex = FindParagraphStartPageIndex(i);

                if (pageIndex < 0)
                    continue;

                if (tocItems.Any(x => x.PageIndex == pageIndex))
                    continue;

                tocItems.Add(new TocItem
                {
                    Title = paragraph,
                    PageIndex = pageIndex
                });
            }

            if (tocItems.Count == 0)
            {
                tocItems.Add(new TocItem
                {
                    Title = "Start",
                    PageIndex = 0
                });
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

        private void BuildDocumentTocFromPages()
        {
            tocItems.Clear();

            if (_rawHtmlChapters.Count == 0)
                return;

            for (int i = 0; i < _rawHtmlChapters.Count; i++)
            {
                string html = _rawHtmlChapters[i] ?? "";
                string title = ExtractDocumentHeading(html);

                if (string.IsNullOrWhiteSpace(title))
                    continue;

                if (!IsLikelyRealHeading(title))
                    continue;

                if (IsFrontMatterTitle(title) && tocItems.Count == 0)
                    continue;

                if (title.Length > 60)
                    title = title.Substring(0, 60).Trim() + "…";

                int pageIndex = i < _chapterStartPageIndices.Count
                    ? _chapterStartPageIndices[i]
                    : 0;

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
                tocItems.Add(new TocItem
                {
                    Title = "Start",
                    PageIndex = 0
                });
            }
        }

        private void BuildEpubTocPreferNcxOrNav(string epubPath)
        {
            tocItems.Clear();

            var tocFromFile = TryParseEpubTocFromZip(epubPath);

            if (tocFromFile.Count > 0 &&
                _rawHtmlChapterKeys.Count == _rawHtmlChapters.Count)
            {
                foreach (var (title, href) in tocFromFile)
                {
                    int chapterIndex =
                        MapHrefToRawChapterIndex(href);

                    if (chapterIndex < 0)
                        continue;

                    int pageIndex =
                        chapterIndex < _chapterStartPageIndices.Count
                            ? _chapterStartPageIndices[chapterIndex]
                            : 0;

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    if (tocItems.Any(x => x.PageIndex == pageIndex))
                        continue;

                    tocItems.Add(new TocItem
                    {
                        Title = HtmlEntityDecodeLite(title),
                        PageIndex = pageIndex
                    });
                }
            }

            if (tocItems.Count == 0 &&
                _rawHtmlChapterTitles.Count == _rawHtmlChapters.Count)
            {
                for (int i = 0; i < _rawHtmlChapterTitles.Count; i++)
                {
                    string title = _rawHtmlChapterTitles[i];

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    int pageIndex =
                        i < _chapterStartPageIndices.Count
                            ? _chapterStartPageIndices[i]
                            : 0;

                    if (tocItems.Any(x => x.PageIndex == pageIndex))
                        continue;

                    tocItems.Add(new TocItem
                    {
                        Title = title,
                        PageIndex = pageIndex
                    });
                }
            }

            if (tocItems.Count == 0)
                BuildDocumentTocFromPages();

            if (tocItems.Count == 0)
            {
                tocItems.Add(new TocItem
                {
                    Title = "Start",
                    PageIndex = 0
                });
            }
        }

        private int MapHrefToRawChapterIndex(string href)
        {
            if (string.IsNullOrWhiteSpace(href))
                return -1;

            string key = NormalizeKey(href);
            string fileOnly = NormalizeKey(Path.GetFileName(key));

            for (int i = 0; i < _rawHtmlChapterKeys.Count; i++)
            {
                string rawKey = NormalizeKey(_rawHtmlChapterKeys[i] ?? "");
                if (string.IsNullOrWhiteSpace(rawKey))
                    continue;

                if (rawKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return i;

                if (rawKey.EndsWith(key, StringComparison.OrdinalIgnoreCase))
                    return i;

                string rawFileOnly = NormalizeKey(Path.GetFileName(rawKey));
                if (!string.IsNullOrWhiteSpace(fileOnly) &&
                    rawFileOnly.Equals(fileOnly, StringComparison.OrdinalIgnoreCase))
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
                        {
                            href = CombineRelativeEpubPath(ncxEntry.FullName, href);
                            result.Add((title, href));
                        }
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
                        {
                            href = CombineRelativeEpubPath(navEntry.FullName, href);
                            result.Add((title, href));
                        }
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

        private static string ExtractDocumentHeading(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            string[] patterns =
            {
        @"<h1[^>]*>\s*(?<t>.*?)\s*</h1>",
        @"<h2[^>]*>\s*(?<t>.*?)\s*</h2>",
        @"<h3[^>]*>\s*(?<t>.*?)\s*</h3>",
        @"<p[^>]*>\s*(?<t>第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇](?:[\s　:：、\.\-—_]*[^<]{0,30})?)\s*</p>",
        @"^\s*(?<t>第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇](?:[\s　:：、\.\-—_]*[^<]{0,30})?)",
        @"<p[^>]*>\s*(?<t>(chapter|part|section)\s+(\d+|[ivxlcdm]+)(?:[:：\.\- ]*[^<]{0,30})?)\s*</p>"
    };

            foreach (var pat in patterns)
            {
                var m = Regex.Match(html, pat, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (!m.Success)
                    continue;

                string t = StripHtmlTags(m.Groups["t"].Value);
                t = HtmlEntityDecodeLite(t).Trim();

                if (IsLikelyRealHeading(t))
                    return t;
            }

            return string.Empty;
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

            int totalPages = GetTotalPages();
            if (totalPages <= 0)
                return;

            if (currentPage == _lastSavedPage)
                return;

            _lastSavedPage = currentPage;

            await dbHelper.SaveReadingProgressAsync(
                GetReadingKey(),
                currentPage,
                totalPages);

            ReadingMetaStore.UpdateProgress(
                FilePath,
                currentPage + 1,
                totalPages);
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

            if (currentPage >= total - 1)
                await SaveReadingProgress();
            else
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
            _documentLoadCts?.Cancel();
            _repaginateCts?.Cancel();
            _progressSaveCts?.Cancel();

            try
            {
                _ = SaveReadingProgress();
                SaveReadingDuration();
                _ = SaveCurrentReadingSettings();
                SaveContinueReadingReminderState();
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

            if (mode == ReaderMode.DocumentPaged || mode == ReaderMode.TxtPaged)
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

        private static string CombineRelativeEpubPath(string basePath, string href)
        {
            if (string.IsNullOrWhiteSpace(href))
                return "";

            href = href.Replace('\\', '/').Trim();

            int hash = href.IndexOf('#');
            if (hash >= 0)
                href = href.Substring(0, hash);

            if (string.IsNullOrWhiteSpace(basePath))
                return NormalizeKey(href);

            basePath = basePath.Replace('\\', '/');
            string baseDir = "";

            int lastSlash = basePath.LastIndexOf('/');
            if (lastSlash >= 0)
                baseDir = basePath.Substring(0, lastSlash + 1);

            var combined = baseDir + href;
            var parts = new List<string>();

            foreach (var part in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".")
                    continue;

                if (part == "..")
                {
                    if (parts.Count > 0)
                        parts.RemoveAt(parts.Count - 1);
                    continue;
                }

                parts.Add(part);
            }

            return NormalizeKey(string.Join("/", parts));
        }

        private static bool IsLikelyRealHeading(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (text.Length > 40)
                return false;

            if (Regex.IsMatch(text, @"^第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇]"))
                return true;

            if (Regex.IsMatch(text, @"^(chapter|part|section)\s+(\d+|[ivxlcdm]+)\b", RegexOptions.IgnoreCase))
                return true;

            if (text.Length <= 24 && !Regex.IsMatch(text, @"[。！？；，,]"))
                return true;

            return false;
        }

        private static bool IsFrontMatterTitle(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (text.Contains("作者") || text.Contains("简介"))
                return true;

            if (text.Length > 18 && !Regex.IsMatch(text, @"^第[0-9一二三四五六七八九十百千两〇零]+[章节回卷部篇]"))
                return true;

            return false;
        }
    }
}