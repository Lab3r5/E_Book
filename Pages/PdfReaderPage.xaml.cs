using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using E_Book.Data;
using E_Book.Services;
using Microsoft.Maui.Controls;
using Syncfusion.Maui.PdfViewer;

namespace E_Book.Pages
{
    [QueryProperty(nameof(FilePath), "filePath")]
    public partial class PdfReaderPage : ContentPage
    {
        private readonly Database dbHelper = new();

        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set => _filePath = Uri.UnescapeDataString(value ?? string.Empty);
        }

        private FileStream? _pdfStream;

        private string themeMode = "White";

        private Color PageBgColor = Color.FromArgb("#F7F6FB");
        private Color TextColorReader = Color.FromArgb("#3E3A4A");
        private Color SubtleTextColor = Color.FromArgb("#6A6577");
        private Color PrimaryAccent = Color.FromArgb("#24145A");
        private Color SurfaceColor = Color.FromArgb("#FFFFFF");
        private Color SoftSurfaceColor = Color.FromArgb("#F1EEFF");
        private Color BorderColor = Color.FromArgb("#E5E1F2");

        private bool _loaded;
        private bool _documentReady;
        private bool _restoringPage;
        private bool _suppressPropertySave;
        private bool _sessionOpened;
        private bool _menuAnimating;

        private DateTime _sessionStartUtc;
        private int _lastSavedDisplayPage = -1;
        private string _loadedFilePath = string.Empty;

        private const uint MenuAnimMs = 180;
        private const double MenuRestY = 0;
        private const double MenuHiddenY = 20;

        public PdfReaderPage()
        {
            InitializeComponent();
            ApplyTheme(themeMode);
            PdfViewer.PropertyChanged += OnPdfViewerPropertyChanged;
            UpdateThemeButtonStyles();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (string.IsNullOrWhiteSpace(FilePath))
            {
                SetReaderLoading(true, "No file selected", "Please return and choose a PDF.");
                return;
            }

            if (_loaded && string.Equals(_loadedFilePath, FilePath, StringComparison.OrdinalIgnoreCase))
                return;

            _loadedFilePath = FilePath;
            _loaded = true;

            await InitializePdfAsync();
        }

        protected override async void OnDisappearing()
        {
            base.OnDisappearing();

            try
            {
                await SaveReadingProgressAsync();
                SaveReadingDuration();
                await SavePdfThemeAsync();
            }
            catch
            {
            }

            try
            {
                PdfViewer?.UnloadDocument();
            }
            catch
            {
            }

            try
            {
                _pdfStream?.Dispose();
                _pdfStream = null;
            }
            catch
            {
            }
        }

        private async Task InitializePdfAsync()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    SetReaderLoading(true, "File not found", "The selected PDF no longer exists.");
                    return;
                }

                TitleLabel.Text = Path.GetFileNameWithoutExtension(FilePath);

                var settings = await dbHelper.GetReadingSettingsAsync();
                themeMode = NormalizeTheme(settings.BackgroundColor);
                ApplyTheme(themeMode);

                SetReaderLoading(true, "Loading PDF...", "Preparing your reading page");

                _pdfStream?.Dispose();
                _pdfStream = File.Open(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                PdfViewer.DocumentSource = _pdfStream;

                await WaitUntilDocumentReadyAsync();

                _documentReady = true;

                var progress = await dbHelper.GetReadingProgressRecordAsync(GetReadingKey());

                int savedDisplayPage = progress.LastPage > 0 ? progress.LastPage : 1;
                int totalPages = Math.Max(1, GetTotalPages());
                savedDisplayPage = Math.Clamp(savedDisplayPage, 1, totalPages);

                _restoringPage = true;
                _suppressPropertySave = true;

                try
                {
                    PdfViewer.GoToPage(savedDisplayPage);
                }
                catch
                {
                }
                finally
                {
                    _restoringPage = false;
                    _suppressPropertySave = false;
                }

                UpdateProgressUI();
                SetReaderLoading(false);

                ReadingMetaStore.UpdateLastOpened(FilePath);
                ReadingMetaStore.UpdateProgress(FilePath, GetCurrentDisplayPage(), GetTotalPages());

                _sessionStartUtc = DateTime.UtcNow;
                _sessionOpened = true;
            }
            catch (Exception ex)
            {
                SetReaderLoading(true, "Unable to open PDF", ex.Message);
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async Task WaitUntilDocumentReadyAsync()
        {
            const int maxAttempts = 80;

            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(100);

                try
                {
                    if (PdfViewer.PageCount > 0)
                        return;
                }
                catch
                {
                }
            }
        }

        private void OnPdfViewerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_documentReady)
                return;

            if (e.PropertyName == nameof(SfPdfViewer.PageNumber) ||
                e.PropertyName == nameof(SfPdfViewer.PageCount))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    UpdateProgressUI();

                    if (_restoringPage || _suppressPropertySave)
                        return;

                    ReadingMetaStore.UpdateProgress(FilePath, GetCurrentDisplayPage(), GetTotalPages());
                    await SaveReadingProgressAsync();
                });
            }
        }

        private int GetCurrentDisplayPage()
        {
            try
            {
                return Math.Max(1, PdfViewer.PageNumber);
            }
            catch
            {
                return 1;
            }
        }

        private int GetTotalPages()
        {
            try
            {
                return Math.Max(1, PdfViewer.PageCount);
            }
            catch
            {
                return 1;
            }
        }

        private string GetReadingKey()
        {
            return Path.GetFileName(FilePath ?? string.Empty).Trim().ToLowerInvariant();
        }

        private async Task SaveReadingProgressAsync()
        {
            if (string.IsNullOrWhiteSpace(FilePath) || !_documentReady)
                return;

            int currentDisplayPage = GetCurrentDisplayPage();
            int totalPages = GetTotalPages();

            if (currentDisplayPage == _lastSavedDisplayPage)
                return;

            _lastSavedDisplayPage = currentDisplayPage;

            await dbHelper.SaveReadingProgressAsync(
                GetReadingKey(),
                currentDisplayPage,
                totalPages);

            ReadingMetaStore.UpdateProgress(
                FilePath,
                currentDisplayPage,
                totalPages);
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

        private async Task SavePdfThemeAsync()
        {
            try
            {
                var current = await dbHelper.GetReadingSettingsAsync();
                await dbHelper.SaveReadingSettingsAsync(current.FontSize, themeMode, current.LineSpacing);
            }
            catch
            {
                try
                {
                    var current = await dbHelper.GetReadingSettingsAsync();
                    await dbHelper.SaveReadingSettingsAsync(current.FontSize, themeMode);
                }
                catch
                {
                }
            }
        }

        private void UpdateProgressUI()
        {
            int total = GetTotalPages();
            int current = GetCurrentDisplayPage();

            ProgressText.Text = $"Page {current} / {total}";
            ReadingProgressBar.Progress = total <= 1 ? 1 : current / (double)total;
        }

        private void SetReaderLoading(bool isLoading, string? title = null, string? subtitle = null)
        {
            ReaderLoadingOverlay.IsVisible = isLoading;
            ReaderLoadingOverlay.Opacity = isLoading ? 1 : 0;

            if (!string.IsNullOrWhiteSpace(title))
                ReaderLoadingText.Text = title;

            if (!string.IsNullOrWhiteSpace(subtitle))
                ReaderLoadingSubText.Text = subtitle;
        }

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await SaveReadingProgressAsync();
                SaveReadingDuration();
                await SavePdfThemeAsync();
            }
            catch
            {
            }

            try
            {
                await Shell.Current.GoToAsync("..");
                return;
            }
            catch
            {
            }

            try
            {
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else
                    await Navigation.PopAsync();
            }
            catch
            {
            }
        }

        private async void OnTocClicked(object sender, EventArgs e)
        {
            if (!_documentReady)
                return;

            int total = GetTotalPages();
            int current = GetCurrentDisplayPage();

            string? input = await DisplayPromptAsync(
                title: "Go to page",
                message: $"Enter page number (1 - {total})",
                accept: "Go",
                cancel: "Cancel",
                initialValue: current.ToString(),
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(input))
                return;

            if (!int.TryParse(input.Trim(), out int target))
            {
                await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
                return;
            }

            target = Math.Clamp(target, 1, total);

            _restoringPage = true;
            try
            {
                PdfViewer.GoToPage(target);
            }
            catch
            {
            }
            finally
            {
                _restoringPage = false;
            }

            UpdateProgressUI();
            await SaveReadingProgressAsync();
        }

        private async void OnMenuClicked(object sender, EventArgs e)
        {
            if (_menuAnimating)
                return;

            if (!MenuPopup.IsVisible)
                await ShowMenuAsync();
            else
                await HideMenuAsync();
        }

        private async void OnDismissTapped(object sender, TappedEventArgs e)
        {
            if (_menuAnimating)
                return;

            await HideMenuAsync();
        }

        private async Task ShowMenuAsync()
        {
            if (_menuAnimating)
                return;

            _menuAnimating = true;

            try
            {
                UpdateThemeButtonStyles();

                Overlay.IsVisible = true;
                Overlay.Opacity = 0;

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
                _menuAnimating = false;
            }
        }

        private async Task HideMenuAsync()
        {
            if (_menuAnimating)
                return;

            if (!MenuPopup.IsVisible)
            {
                Overlay.IsVisible = false;
                Overlay.Opacity = 0;
                return;
            }

            _menuAnimating = true;

            try
            {
                await Task.WhenAll(
                    Overlay.FadeTo(0, 100, Easing.CubicIn),
                    MenuPopup.FadeTo(0, 100, Easing.CubicIn),
                    MenuPopup.TranslateTo(0, MenuHiddenY, 100, Easing.CubicIn)
                );

                MenuPopup.IsVisible = false;
                Overlay.IsVisible = false;
            }
            finally
            {
                Overlay.Opacity = 0;
                MenuPopup.Opacity = 0;
                MenuPopup.TranslationY = MenuRestY;
                _menuAnimating = false;
            }
        }

        private async void OnThemeClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not string modeValue)
                return;

            ApplyTheme(modeValue);
            UpdateThemeButtonStyles();
            await SavePdfThemeAsync();
            await HideMenuAsync();
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
        }

        private void ApplyThemeToVisuals()
        {
            BackgroundColor = PageBgColor;
            ReadingArea.BackgroundColor = PageBgColor;
            ReaderSurface.BackgroundColor = PageBgColor;
            BottomBar.BackgroundColor = PageBgColor;

            TitleLabel.TextColor = PrimaryAccent;
            BackButton.TextColor = PrimaryAccent;
            MenuButton.TextColor = PrimaryAccent;
            TocButton.TextColor = PrimaryAccent;

            ProgressText.TextColor = SubtleTextColor;
            ReadingProgressBar.ProgressColor = Color.FromArgb("#7B6CFF");
            ReadingProgressBar.BackgroundColor = BorderColor;

            ReaderLoadingOverlay.BackgroundColor = themeMode == "Dark"
                ? Color.FromArgb("#CC101014")
                : Color.FromArgb("#CCF7F6FB");

            ReaderLoadingText.TextColor = PrimaryAccent;
            ReaderLoadingSubText.TextColor = SubtleTextColor;

            MenuPopup.BackgroundColor = SurfaceColor;
            HandleBar.BackgroundColor = BorderColor;
            UpdatePopupTextColors();
            UpdateThemeButtonStyles();
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

        private void UpdateThemeButtonStyles()
        {
            StyleThemeChip(ThemeWhiteBtn, themeMode == "White", "#F7F6FB", false);
            StyleThemeChip(ThemeBeigeBtn, themeMode == "Beige", "#D8D2BE", false);
            StyleThemeChip(ThemeGreenBtn, themeMode == "Green", "#C9D5B9", false);
            StyleThemeChip(ThemeBlueBtn, themeMode == "Blue", "#C4D2E2", false);
            StyleThemeChip(ThemeDarkBtn, themeMode == "Dark", "#101014", true);
        }

        private void StyleThemeChip(Button button, bool selected, string bgHex, bool isDarkChip)
        {
            button.Shadow = null;
            button.BackgroundColor = Color.FromArgb(bgHex);
            button.BorderWidth = selected ? 2 : 0;
            button.BorderColor = selected
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