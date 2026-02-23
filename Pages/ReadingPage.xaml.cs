using System.Text;
using Microsoft.Maui.Controls;

namespace E_Book.Pages
{
    // 通过 Shell 路由：reading?path=...&title=...
    [QueryProperty(nameof(PathParam), "path")]
    [QueryProperty(nameof(TitleParam), "title")]
    public partial class ReadingPage : ContentPage
    {
        private string? _pathParam;
        public string? PathParam
        {
            get => _pathParam;
            set
            {
                _pathParam = Uri.UnescapeDataString(value ?? "");
                _ = LoadFromPathAsync(_pathParam);
            }
        }

        private string? _titleParam;
        public string? TitleParam
        {
            get => _titleParam;
            set
            {
                _titleParam = Uri.UnescapeDataString(value ?? "");
                if (!string.IsNullOrWhiteSpace(_titleParam))
                    TitleLabel.Text = _titleParam;
            }
        }

        private string _fullText = "Loading...";
        private readonly List<string> _pages = new();
        private int _pageIndex = 0;

        private bool _menuOpen = false;
        private bool _immersive = false;

        private readonly double[] _fontPresets = { 18, 22, 26 }; // Small / Medium / Large
        private int _fontPresetIndex = 1;

        public ReadingPage()
        {
            InitializeComponent();

            ApplyFontPreset(_fontPresetIndex);
            ApplyTheme("Light");

            PaginateAndRender(resetToFirstPage: true);
        }

        // ===== Load content =====
        private async Task LoadFromPathAsync(string? path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    await ShowErrorAsync("No file path provided.");
                    return;
                }

                if (!File.Exists(path))
                {
                    await ShowErrorAsync("File not found.");
                    return;
                }

                // 目前你的 Usage Guidelines 是 TXT，所以直接读文本即可
                // 未来如需 WebView 渲染 EPUB/PDF/HTML/DOCX/RTF，可在这里扩展
                _fullText = await File.ReadAllTextAsync(path, Encoding.UTF8);

                fileContentLabel.IsVisible = true;
                ContentWebView.IsVisible = false;

                PaginateAndRender(resetToFirstPage: true);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync(ex.Message);
            }
        }

        private async Task ShowErrorAsync(string message)
        {
            _fullText = $"Error: {message}";
            fileContentLabel.Text = _fullText;
            await DisplayAlert("Error", message, "OK");
        }

        // ===== Navigation =====
        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // ===== UI =====
        private async void OnMenuClicked(object sender, EventArgs e)
        {
            if (_menuOpen) await HideMenuAsync();
            else await ShowMenuAsync();
        }

        private async void OnDismissTapped(object sender, EventArgs e)
        {
            if (_menuOpen) await HideMenuAsync();
        }

        private async void OnReadingAreaTapped(object sender, EventArgs e)
        {
            _immersive = !_immersive;

            if (_immersive)
            {
                await TopBar.FadeTo(0, 120);
                await BottomBar.FadeTo(0, 120);
                await TocButtonContainer.FadeTo(0, 120);

                TopBar.IsVisible = false;
                BottomBar.IsVisible = false;
                TocButtonContainer.IsVisible = false;
            }
            else
            {
                TopBar.IsVisible = true;
                BottomBar.IsVisible = true;
                TocButtonContainer.IsVisible = true;

                await TopBar.FadeTo(1, 120);
                await BottomBar.FadeTo(1, 120);
                await TocButtonContainer.FadeTo(1, 120);
            }
        }

        // ===== Font =====
        private void OnFontPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.CommandParameter?.ToString(), out int idx))
                ApplyFontPreset(idx);
        }

        private void OnFontSliderChanged(object sender, ValueChangedEventArgs e)
        {
            int idx = (int)Math.Round(e.NewValue);
            idx = Math.Clamp(idx, 0, 2);

            if (idx != _fontPresetIndex)
                ApplyFontPreset(idx);
        }

        private void ApplyFontPreset(int idx)
        {
            _fontPresetIndex = idx;
            fileContentLabel.FontSize = _fontPresets[idx];

            FontSlider.Value = idx;
            FontSizeLabel.Text = idx switch
            {
                0 => "Small",
                1 => "Medium",
                2 => "Large",
                _ => "Medium"
            };

            PaginateAndRender(resetToFirstPage: false);
        }

        // ===== Theme =====
        private void OnThemeClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string mode)
                ApplyTheme(mode);
        }

        private void ApplyTheme(string mode)
        {
            Application.Current!.UserAppTheme =
                mode.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? AppTheme.Dark
                    : AppTheme.Light;
        }

        // ===== Menu Animation =====
        private async Task ShowMenuAsync()
        {
            _menuOpen = true;
            Overlay.IsVisible = true;
            MenuPopup.IsVisible = true;

            await Task.WhenAll(
                Overlay.FadeTo(1, 120),
                MenuPopup.FadeTo(1, 120),
                MenuPopup.ScaleTo(1, 120, Easing.CubicOut)
            );
        }

        private async Task HideMenuAsync()
        {
            _menuOpen = false;

            await Task.WhenAll(
                Overlay.FadeTo(0, 120),
                MenuPopup.FadeTo(0, 120),
                MenuPopup.ScaleTo(0.98, 120, Easing.CubicIn)
            );

            Overlay.IsVisible = false;
            MenuPopup.IsVisible = false;
        }

        // ===== Pagination =====
        private void PaginateAndRender(bool resetToFirstPage)
        {
            int charsPerPage = _fontPresetIndex switch
            {
                0 => 1400,
                1 => 1100,
                2 => 850,
                _ => 1100
            };

            _pages.Clear();

            var text = _fullText ?? "";
            if (text.Length == 0) text = "No content.";

            for (int i = 0; i < text.Length; i += charsPerPage)
            {
                int len = Math.Min(charsPerPage, text.Length - i);
                _pages.Add(text.Substring(i, len));
            }

            if (_pages.Count == 0) _pages.Add("No content.");

            if (resetToFirstPage) _pageIndex = 0;
            _pageIndex = Math.Clamp(_pageIndex, 0, _pages.Count - 1);

            RenderPage();
        }

        private void RenderPage()
        {
            fileContentLabel.Text = _pages[_pageIndex];

            int total = _pages.Count;
            int current = _pageIndex + 1;

            ProgressText.Text = $"{current} of {total}";
            ReadingProgressBar.Progress = total <= 1 ? 1 : (double)_pageIndex / (total - 1);
        }

        private async void OnTocClicked(object sender, EventArgs e)
        {
            await DisplayAlert("TOC", "Table of contents is not implemented yet.", "OK");
        }

        // ===== Button feedback =====
        private void OnButtonPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement v) v.Opacity = 0.6;
        }

        private void OnButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement v) v.Opacity = 1.0;
        }
    }
}