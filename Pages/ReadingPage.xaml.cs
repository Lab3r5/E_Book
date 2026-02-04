using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using E_Book.Data;

namespace E_Book.Pages
{
    public partial class ReadingPage : ContentPage
    {
        private string[] lines = Array.Empty<string>();
        private int currentPage = 0;
        private int linesPerPage = 16;

        public string FilePath { get; set; }
        private readonly Database dbHelper = new();

        private readonly int[] fontSizes = new[] { 18, 22, 26 }; // Small/Medium/Large
        private int fontIndex = 1;
        private int currentFontSize = 22;

        private string themeMode = "Light"; // Light | Dark

        // UI colors
        private static readonly Color DeepBlue = Color.FromArgb("#24145A");
        private static readonly Color OffWhite = Color.FromArgb("#FFFFFF");
        private static readonly Color SoftGray = Color.FromArgb("#ECEAF6");

        // Animation flags / params
        private bool isAnimating = false;

        private const uint PageAnimMs = 180;
        private const double SlideDistance = 60;

        private const uint MenuAnimMs = 170;
        private const double MenuRestY = -68;    // same as XAML TranslationY
        private const double MenuHiddenY = -30;  // slightly lower for enter/exit

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
                await LoadFile(FilePath);

                string fileName = Path.GetFileName(FilePath);
                currentPage = await dbHelper.GetReadingProgressAsync(fileName);

                ClampCurrentPage();
                DisplayPage();
                UpdateProgressUI();
            }
        }

        // ===================== Paging (safe) =====================

        private int GetTotalPages()
        {
            if (lines == null || lines.Length == 0) return 0;
            return (int)Math.Ceiling(lines.Length / (double)linesPerPage);
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
            if (lines == null || lines.Length == 0)
            {
                fileContentLabel.Text = "";
                return;
            }

            ClampCurrentPage();

            int start = currentPage * linesPerPage;
            if (start < 0) start = 0;

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

        private void UpdateProgressUI()
        {
            int total = GetTotalPages();
            int current = total <= 0 ? 1 : currentPage + 1;

            ProgressText.Text = $"{Math.Max(1, current)} of {Math.Max(1, total)}";
            ReadingProgressBar.Progress = total <= 0 ? 0 : current / (double)total;
        }

        // ===================== File =====================

        private async Task LoadFile(string filePath)
        {
            try
            {
                string fileContent = await File.ReadAllTextAsync(filePath);
                lines = fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            }
            catch (Exception ex)
            {
                fileContentLabel.Text = "Unable to load file: " + ex.Message;
                lines = Array.Empty<string>();
            }
        }

        private void UpdateLinesPerPage()
        {
            if (currentFontSize == 18) linesPerPage = 20;
            else if (currentFontSize == 22) linesPerPage = 16;
            else linesPerPage = 12;
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

        // ===================== Page animations =====================

        private async Task AnimatePageChangeAsync(int direction)
        {
            // direction: +1 next (slide left), -1 prev (slide right)
            if (isAnimating) return;
            isAnimating = true;

            try
            {
                double outX = direction > 0 ? -SlideDistance : SlideDistance;

                await Task.WhenAll(
                    fileContentLabel.TranslateTo(outX, 0, PageAnimMs, Easing.CubicIn),
                    fileContentLabel.FadeTo(0, PageAnimMs, Easing.CubicIn)
                );

                DisplayPage();
                UpdateProgressUI();

                double inX = direction > 0 ? SlideDistance : -SlideDistance;
                fileContentLabel.TranslationX = inX;

                await Task.WhenAll(
                    fileContentLabel.TranslateTo(0, 0, PageAnimMs, Easing.CubicOut),
                    fileContentLabel.FadeTo(1, PageAnimMs, Easing.CubicOut)
                );
            }
            finally
            {
                fileContentLabel.TranslationX = 0;
                fileContentLabel.Opacity = 1;
                isAnimating = false;
            }
        }

        private async Task NextPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0) return;
            if (currentPage >= total - 1) return;

            currentPage++;
            await AnimatePageChangeAsync(direction: +1);
            await SaveReadingProgress();
        }

        private async Task PrevPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0) return;
            if (currentPage <= 0) return;

            currentPage--;
            await AnimatePageChangeAsync(direction: -1);
            await SaveReadingProgress();
        }

        // ===================== Button press animation =====================

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

        // ===================== Menu animations =====================

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

        // ===================== Navigation =====================

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
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

            await SaveCurrentReadingSettings();
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

            // Page background
            var pageBg = dark ? Color.FromArgb("#0B0B0F") : Colors.White;

            this.BackgroundColor = pageBg;
            ReadingArea.BackgroundColor = pageBg;

            // Top bar
            BackButton.TextColor = dark ? OffWhite : Color.FromArgb("#6C6883");
            MenuButton.TextColor = dark ? OffWhite : Color.FromArgb("#6C6883");
            TitleLabel.TextColor = dark ? OffWhite : Color.FromArgb("#2B2B33");

            // Content
            fileContentLabel.TextColor = dark ? Color.FromArgb("#EAE8F5") : Color.FromArgb("#4B475A");

            // Progress text
            ProgressText.TextColor = dark ? Color.FromArgb("#C9C7D6") : Color.FromArgb("#8C8A9A");

            // Progress bar (brighter in Dark)
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

            // Popup
            MenuPopup.BackgroundColor = dark ? Color.FromArgb("#2B2B33") : OffWhite;
            MenuPopup.Stroke = dark ? Color.FromArgb("#33FFFFFF") : Color.FromArgb("#22000000");

            var popupText = dark ? OffWhite : Color.FromArgb("#2B2B33");
            var popupSub = dark ? Color.FromArgb("#C9C7D6") : Color.FromArgb("#8C8A9A");

            PopupTitle1.TextColor = popupText;
            PopupTitle2.TextColor = popupText;
            FontSizeLabel.TextColor = popupSub;
            SmallLabel.TextColor = popupSub;
            LargeLabel.TextColor = popupSub;

            // ✅ 关键：Dark 模式把 Slider 调亮一点
            if (dark)
            {
                FontSlider.MinimumTrackColor = Color.FromArgb("#C7B9FF"); // 亮紫/亮色
                FontSlider.MaximumTrackColor = Color.FromArgb("#8F8AA8"); // 比 Gray600 亮很多
                FontSlider.ThumbColor = Colors.White;
            }
            else
            {
                FontSlider.MinimumTrackColor = Color.FromArgb("#5E4DB2");
                FontSlider.MaximumTrackColor = Color.FromArgb("#D8D5EA");
                FontSlider.ThumbColor = Color.FromArgb("#5E4DB2");
            }

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
