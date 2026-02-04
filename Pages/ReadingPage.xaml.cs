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

        // 主题色（你项目里常用的深蓝）
        private static readonly Color DeepBlue = Color.FromArgb("#24145A");
        private static readonly Color OffWhite = Color.FromArgb("#FFFFFF");
        private static readonly Color SoftGray = Color.FromArgb("#ECEAF6");

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

        // ===== robust paging =====

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

        // ===== file =====

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

        // ===== progress =====

        private void UpdateProgressUI()
        {
            int total = GetTotalPages();
            int current = total <= 0 ? 1 : currentPage + 1;

            ProgressText.Text = $"{Math.Max(1, current)} of {Math.Max(1, total)}";
            ReadingProgressBar.Progress = total <= 0 ? 0 : current / (double)total;
        }

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

        // ===== swipe paging =====

        private async Task NextPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0) return;

            if (currentPage < total - 1)
            {
                currentPage++;
                DisplayPage();
                UpdateProgressUI();
                await SaveReadingProgress();
            }
        }

        private async Task PrevPageAsync()
        {
            int total = GetTotalPages();
            if (total <= 0) return;

            if (currentPage > 0)
            {
                currentPage--;
                DisplayPage();
                UpdateProgressUI();
                await SaveReadingProgress();
            }
        }

        // ===== buttons =====

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private void OnMenuClicked(object sender, EventArgs e)
        {
            bool open = !MenuPopup.IsVisible;
            MenuPopup.IsVisible = open;

            // ✅ 半透明黑遮罩（不再蓝色），还能看到文字
            Overlay.IsVisible = open;

            if (open)
            {
                FontSlider.Value = fontIndex;
                UpdateFontSizeLabel();
                UpdateFontButtonStyles();
                UpdateThemeButtonStyles();
            }
        }

        private void OnDismissTapped(object sender, EventArgs e)
        {
            MenuPopup.IsVisible = false;
            Overlay.IsVisible = false;
        }

        // ===== font =====

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

        // ✅ Font Size 单选高亮逻辑（按你描述的规则做成一致版）
        private void UpdateFontButtonStyles()
        {
            bool dark = themeMode == "Dark";

            // Light: selected deepblue, others white
            // Dark : selected white, others deepblue
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
                // Light mode
                b.BackgroundColor = selected ? DeepBlue : OffWhite;
                b.TextColor = selected ? OffWhite : DeepBlue;
            }
            else
            {
                // Dark mode
                b.BackgroundColor = selected ? OffWhite : DeepBlue;
                b.TextColor = selected ? DeepBlue : OffWhite;
            }
        }

        // ===== theme =====

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

            // ✅ 去掉“蓝色背景块”：阅读区和页面背景跟主题一致
            var pageBg = dark ? Color.FromArgb("#0B0B0F") : Color.FromArgb("#F7F6FB");
            this.BackgroundColor = pageBg;
            ReadingArea.BackgroundColor = pageBg;
            fileContentLabel.BackgroundColor = Colors.Transparent;

            // Top bar contrast
            BackButton.TextColor = dark ? OffWhite : Color.FromArgb("#6C6883");
            MenuButton.TextColor = dark ? OffWhite : Color.FromArgb("#6C6883");
            TitleLabel.TextColor = dark ? OffWhite : Color.FromArgb("#2B2B33");

            // Content text
            fileContentLabel.TextColor = dark ? Color.FromArgb("#EAE8F5") : Color.FromArgb("#4B475A");

            // Progress
            ProgressText.TextColor = dark ? Color.FromArgb("#C9C7D6") : Color.FromArgb("#8C8A9A");

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

            // 更新按钮状态
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

        // ✅ Theme 两按钮：颜色相反 + 未选中变“浅一点”
        private void UpdateThemeButtonStyles()
        {
            bool dark = themeMode == "Dark";

            if (!dark)
            {
                // Light theme selected
                LightBtn.BackgroundColor = DeepBlue;
                LightBtn.TextColor = OffWhite;

                // Dark button becomes lighter (unselected)
                DarkBtn.BackgroundColor = SoftGray;
                DarkBtn.TextColor = DeepBlue;

                LightBtn.BorderWidth = 0;
                DarkBtn.BorderWidth = 1;
                DarkBtn.BorderColor = Color.FromArgb("#D6D2EA");
            }
            else
            {
                // Dark theme selected
                DarkBtn.BackgroundColor = OffWhite;
                DarkBtn.TextColor = DeepBlue;

                // Light button becomes darker-ish (unselected)
                LightBtn.BackgroundColor = Color.FromArgb("#3B3560");
                LightBtn.TextColor = OffWhite;

                DarkBtn.BorderWidth = 0;
                LightBtn.BorderWidth = 1;
                LightBtn.BorderColor = Color.FromArgb("#4B446E");
            }
        }
    }
}
