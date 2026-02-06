using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
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

        // HTML-based mode (EPUB chapters, HTML file, DOCX->HTML, RTF->HTML)
        private List<string> htmlPages = new();

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

        private const uint PageAnimMs = 180;
        private const double SlideDistance = 60;

        private const uint MenuAnimMs = 170;
        private const double MenuRestY = -68;
        private const double MenuHiddenY = -30;

        private enum ReaderMode { TxtPaged, HtmlPaged, PdfExternal, Unknown }
        private ReaderMode mode = ReaderMode.Unknown;

        public ReadingPage(string filePath)
        {
            InitializeComponent();
            FilePath = filePath;

            // Swipe paging
            var swipeLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
            swipeLeft.Swiped += async (_, __) => await NextPageAsync();

            var swipeRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
            swipeRight.Swiped += async (_, __) => await PrevPageAsync();

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
                await LoadByTypeAsync(FilePath);

                string fileName = Path.GetFileName(FilePath);
                currentPage = await dbHelper.GetReadingProgressAsync(fileName);

                ClampCurrentPage();
                DisplayPage();
                UpdateProgressUI();
            }
        }

        // ===================== Multi-format router =====================

        private async Task LoadByTypeAsync(string filePath)
        {
            lines = Array.Empty<string>();
            htmlPages.Clear();
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
                        break;

                    case ".html":
                    case ".htm":
                        mode = ReaderMode.HtmlPaged;
                        await LoadHtmlFileAsync(filePath);
                        ShowWebView();
                        break;

                    case ".epub":
                        mode = ReaderMode.HtmlPaged;
                        await LoadEpubAsync(filePath);
                        ShowWebView();
                        break;

                    case ".docx":
                        mode = ReaderMode.HtmlPaged;
                        await LoadDocxAsync(filePath);
                        ShowWebView();
                        break;

                    case ".rtf":
                        mode = ReaderMode.HtmlPaged;
                        await LoadRtfAsync(filePath);
                        ShowWebView();
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
            lines = fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
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
        }

        // ===================== EPUB =====================

        private async Task LoadEpubAsync(string filePath)
        {
            // Each chapter HTML = one page
            var book = await EpubReader.ReadBookAsync(filePath);

            var chapters = book.ReadingOrder
                               .Select(c => c.Content)
                               .Where(x => !string.IsNullOrWhiteSpace(x))
                               .ToList();

            if (chapters.Count == 0)
                chapters.Add("<p>(No readable chapters found)</p>");

            htmlPages = chapters;
        }

        // ===================== DOCX =====================

        private Task LoadDocxAsync(string filePath)
        {
            var converter = new DocumentConverter();
            var result = converter.ConvertToHtml(filePath);

            var html = result?.Value;
            htmlPages = new List<string> { string.IsNullOrWhiteSpace(html) ? "<p>(Empty DOCX)</p>" : html! };

            return Task.CompletedTask;
        }

        // ===================== RTF =====================

        private async Task LoadRtfAsync(string filePath)
        {
            var rtfText = await File.ReadAllTextAsync(filePath);
            var html = Rtf.ToHtml(rtfText);
            htmlPages = new List<string> { string.IsNullOrWhiteSpace(html) ? "<p>(Empty RTF)</p>" : html };
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
            catch
            {
                // keep silent (do not crash)
            }
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

            if (mode == ReaderMode.TxtPaged)
            {
                DisplayTxtPage();
                return;
            }

            if (mode == ReaderMode.HtmlPaged)
            {
                DisplayHtmlPage();
                return;
            }
        }

        private void DisplayTxtPage()
        {
            if (lines == null || lines.Length == 0)
            {
                fileContentLabel.Text = "";
                return;
            }

            int start = currentPage * linesPerPage;
            start = Math.Max(0, start);

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
            ClampCurrentPage();
            DisplayPage();
            UpdateProgressUI();
            UpdateFontSizeLabel();

            UpdateFontButtonStyles();

            // Re-render HTML with new CSS
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