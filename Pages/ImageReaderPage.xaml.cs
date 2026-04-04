using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using E_Book.Services;
using Microsoft.Maui.Controls;

namespace E_Book.Pages
{
    [QueryProperty(nameof(FilePath), "filePath")]
    public partial class ImageReaderPage : ContentPage
    {
        private const double DefaultScale = 0.68;
        private const double MaxScale = 4.0;
        private const uint ZoomAnimMs = 160;
        private const uint MenuAnimMs = 180;
        private const double MenuRestY = 0;
        private const double MenuHiddenY = 20;
        private const uint ChromeAnimMs = 140;

        private double _currentScale = DefaultScale;
        private double _startScale = DefaultScale;

        private double _xOffset = 0;
        private double _yOffset = 0;
        private double _panStartX = 0;
        private double _panStartY = 0;

        private bool _menuAnimating;
        private bool _chromeVisible = true;
        private bool _chromeAnimating;

        private string _filePath = string.Empty;
        public string FilePath
        {
            get => _filePath;
            set => _filePath = Uri.UnescapeDataString(value ?? string.Empty);
        }

        private string themeMode = "Dark";

        private Color PageBgColor = Color.FromArgb("#101014");
        private Color TextColorReader = Color.FromArgb("#EDEAF6");
        private Color SubtleTextColor = Color.FromArgb("#BDB8C8");
        private Color PrimaryAccent = Color.FromArgb("#EDEAF6");
        private Color SurfaceColor = Color.FromArgb("#17171C");
        private Color BorderColor = Color.FromArgb("#2A2A32");

        public ImageReaderPage()
        {
            InitializeComponent();
            ApplyTheme("Dark");
            ResetTransform(updateImage: false);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadImageAsync();
        }

        private async Task LoadImageAsync()
        {
            try
            {
                SetLoading(true);
                ErrorState.IsVisible = false;

                if (string.IsNullOrWhiteSpace(FilePath))
                {
                    ShowError("No image file was provided.");
                    return;
                }

                if (!File.Exists(FilePath))
                {
                    ShowError("The selected image file does not exist.");
                    return;
                }

                TitleLabel.Text = Path.GetFileName(FilePath);

                ReaderImage.Source = null;
                await Task.Delay(40);

                ReaderImage.Source = ImageSource.FromFile(FilePath);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Task.Delay(60);
                    ResetTransform(updateImage: true);
                });

                ReadingMetaStore.UpdateLastOpened(FilePath);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void SetLoading(bool isLoading)
        {
            LoadingOverlay.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorState.IsVisible = true;
            ReaderImage.Source = null;
            ResetTransform(updateImage: true);
        }

        private void ResetTransform(bool updateImage)
        {
            _currentScale = DefaultScale;
            _startScale = DefaultScale;
            _xOffset = 0;
            _yOffset = 0;
            _panStartX = 0;
            _panStartY = 0;

            if (updateImage)
            {
                ReaderImage.Scale = _currentScale;
                ReaderImage.TranslationX = 0;
                ReaderImage.TranslationY = 0;
            }

            UpdateZoomButtonStyles();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                if (Navigation.ModalStack.Count > 0)
                    await Navigation.PopModalAsync();
                else
                    await Navigation.PopAsync();
            }
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
                UpdateZoomButtonStyles();
                UpdateThemeButtonStyles();

                Overlay.IsVisible = true;
                Overlay.Opacity = 0;

                MenuPopup.IsVisible = true;
                MenuPopup.Opacity = 0;
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

        private async void OnViewportTapped(object sender, TappedEventArgs e)
        {
            if (MenuPopup.IsVisible)
            {
                await HideMenuAsync();
                return;
            }

            await ToggleChromeAsync();
        }

        private async Task ToggleChromeAsync()
        {
            if (_chromeAnimating)
                return;

            _chromeAnimating = true;

            try
            {
                _chromeVisible = !_chromeVisible;

                if (!_chromeVisible)
                {
                    await Task.WhenAll(
                        TopBar.FadeTo(0, ChromeAnimMs, Easing.CubicIn),
                        TopBar.TranslateTo(0, -10, ChromeAnimMs, Easing.CubicIn)
                    );

                    TopBar.IsVisible = false;
                }
                else
                {
                    TopBar.IsVisible = true;
                    TopBar.Opacity = 0;
                    TopBar.TranslationY = -10;

                    await Task.WhenAll(
                        TopBar.FadeTo(1, ChromeAnimMs, Easing.CubicOut),
                        TopBar.TranslateTo(0, 0, ChromeAnimMs, Easing.CubicOut)
                    );
                }
            }
            finally
            {
                _chromeAnimating = false;
            }
        }

        private async void OnImageDoubleTapped(object sender, TappedEventArgs e)
        {
            double target = _currentScale < 1.35 ? 1.8 : DefaultScale;
            await AnimateScaleToAsync(target);
        }

        private void OnImagePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            if (_currentScale <= DefaultScale + 0.01 || ReaderImage.Source == null)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _panStartX = _xOffset;
                    _panStartY = _yOffset;
                    break;

                case GestureStatus.Running:
                    _xOffset = _panStartX + e.TotalX;
                    _yOffset = _panStartY + e.TotalY;
                    ClampOffsets();
                    ApplyTransform();
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    ClampOffsets();
                    ApplyTransform();
                    break;
            }
        }

        private void OnImagePinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            if (ReaderImage.Source == null)
                return;

            switch (e.Status)
            {
                case GestureStatus.Started:
                    _startScale = _currentScale;
                    break;

                case GestureStatus.Running:
                    double newScale = _startScale * e.Scale;
                    newScale = Math.Clamp(newScale, DefaultScale, MaxScale);

                    _currentScale = newScale;
                    ClampOffsets();
                    ApplyTransform(liveUpdate: true);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    ClampOffsets();
                    ApplyTransform(liveUpdate: false);
                    break;
            }
        }

        private async void OnZoomPresetClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is null)
                return;

            if (!double.TryParse(btn.CommandParameter.ToString(), out double scale))
                return;

            if (Math.Abs(scale - 1.0) < 0.01)
                scale = DefaultScale;

            await AnimateScaleToAsync(scale);
        }

        private async Task AnimateScaleToAsync(double targetScale)
        {
            _currentScale = Math.Clamp(targetScale, DefaultScale, MaxScale);

            if (_currentScale <= DefaultScale + 0.01)
            {
                _xOffset = 0;
                _yOffset = 0;
            }

            ClampOffsets();

            try
            {
                await Task.WhenAll(
                    ReaderImage.ScaleTo(_currentScale, ZoomAnimMs, Easing.CubicOut),
                    ReaderImage.TranslateTo(_xOffset, _yOffset, ZoomAnimMs, Easing.CubicOut)
                );
            }
            catch
            {
                ReaderImage.Scale = _currentScale;
                ReaderImage.TranslationX = _xOffset;
                ReaderImage.TranslationY = _yOffset;
            }

            UpdateZoomButtonStyles();
        }

        private void ApplyTransform(bool liveUpdate = false)
        {
            ReaderImage.Scale = _currentScale;
            ReaderImage.TranslationX = _xOffset;
            ReaderImage.TranslationY = _yOffset;

            if (!liveUpdate)
                UpdateZoomButtonStyles();
        }

        private void ClampOffsets()
        {
            if (ReadingArea.Width <= 0 || ReadingArea.Height <= 0 ||
                ReaderImage.Width <= 0 || ReaderImage.Height <= 0)
                return;

            double scaledWidth = ReaderImage.Width * _currentScale;
            double scaledHeight = ReaderImage.Height * _currentScale;

            double maxX = Math.Max(0, (scaledWidth - ReadingArea.Width * 0.88) / 2);
            double maxY = Math.Max(0, (scaledHeight - ReadingArea.Height * 0.88) / 2);

            _xOffset = Math.Clamp(_xOffset, -maxX, maxX);
            _yOffset = Math.Clamp(_yOffset, -maxY, maxY);

            if (_currentScale <= DefaultScale + 0.01)
            {
                _xOffset = 0;
                _yOffset = 0;
            }
        }

        private void UpdateZoomButtonStyles()
        {
            StyleZoomButton(Zoom1Btn, Math.Abs(_currentScale - DefaultScale) < 0.08);
            StyleZoomButton(Zoom15Btn, Math.Abs(_currentScale - 1.5) < 0.12);
            StyleZoomButton(Zoom2Btn, Math.Abs(_currentScale - 2.0) < 0.12);
            StyleZoomButton(Zoom3Btn, Math.Abs(_currentScale - 3.0) < 0.12);
        }

        private void StyleZoomButton(Button button, bool selected)
        {
            if (selected)
            {
                button.BackgroundColor = themeMode == "Dark"
                    ? Color.FromArgb("#2F2F39")
                    : Color.FromArgb("#FFFFFF");

                button.TextColor = themeMode == "Dark"
                    ? Colors.White
                    : Color.FromArgb("#111111");
            }
            else
            {
                button.BackgroundColor = themeMode == "Dark"
                    ? Color.FromArgb("#20202A")
                    : Color.FromArgb("#F1F1F3");

                button.TextColor = themeMode == "Dark"
                    ? Color.FromArgb("#E2E2E8")
                    : Color.FromArgb("#111111");
            }

            button.BorderWidth = 0;
            button.Shadow = null;
        }

        private void ApplyTheme(string modeValue)
        {
            themeMode = modeValue switch
            {
                "White" => "White",
                "Beige" => "Beige",
                "Green" => "Green",
                "Blue" => "Blue",
                "Dark" => "Dark",
                _ => "Dark"
            };

            switch (themeMode)
            {
                case "White":
                    PageBgColor = Color.FromArgb("#F7F6FB");
                    TextColorReader = Color.FromArgb("#3E3A4A");
                    SubtleTextColor = Color.FromArgb("#6A6577");
                    PrimaryAccent = Color.FromArgb("#24145A");
                    SurfaceColor = Color.FromArgb("#FFFFFF");
                    BorderColor = Color.FromArgb("#E5E1F2");
                    break;

                case "Beige":
                    PageBgColor = Color.FromArgb("#D8D2BE");
                    TextColorReader = Color.FromArgb("#3F372B");
                    SubtleTextColor = Color.FromArgb("#6E6659");
                    PrimaryAccent = Color.FromArgb("#3F372B");
                    SurfaceColor = Color.FromArgb("#F5F1E6");
                    BorderColor = Color.FromArgb("#C8BEA7");
                    break;

                case "Green":
                    PageBgColor = Color.FromArgb("#C9D5B9");
                    TextColorReader = Color.FromArgb("#33402C");
                    SubtleTextColor = Color.FromArgb("#61705B");
                    PrimaryAccent = Color.FromArgb("#33402C");
                    SurfaceColor = Color.FromArgb("#EEF4E6");
                    BorderColor = Color.FromArgb("#B6C5A2");
                    break;

                case "Blue":
                    PageBgColor = Color.FromArgb("#C4D2E2");
                    TextColorReader = Color.FromArgb("#31404F");
                    SubtleTextColor = Color.FromArgb("#617284");
                    PrimaryAccent = Color.FromArgb("#31404F");
                    SurfaceColor = Color.FromArgb("#EEF4FA");
                    BorderColor = Color.FromArgb("#AFC2D8");
                    break;

                default:
                    PageBgColor = Color.FromArgb("#101014");
                    TextColorReader = Color.FromArgb("#EDEAF6");
                    SubtleTextColor = Color.FromArgb("#BDB8C8");
                    PrimaryAccent = Color.FromArgb("#EDEAF6");
                    SurfaceColor = Color.FromArgb("#17171C");
                    BorderColor = Color.FromArgb("#2A2A32");
                    break;
            }

            ApplyThemeToVisuals();
        }

        private void ApplyThemeToVisuals()
        {
            BackgroundColor = PageBgColor;
            ReadingArea.BackgroundColor = PageBgColor;

            TitleLabel.TextColor = PrimaryAccent;
            BackButton.TextColor = PrimaryAccent;
            MenuButton.TextColor = PrimaryAccent;

            TopBar.BackgroundColor = themeMode == "Dark"
                ? Color.FromArgb("#66000000")
                : Color.FromArgb("#55FFFFFF");

            LoadingOverlay.BackgroundColor = themeMode == "Dark"
                ? Color.FromArgb("#AA101014")
                : Color.FromArgb("#AAF7F6FB");

            LoadingText.TextColor = PrimaryAccent;
            LoadingSubText.TextColor = SubtleTextColor;

            MenuPopup.BackgroundColor = SurfaceColor;
            HandleBar.BackgroundColor = BorderColor;
            ErrorText.TextColor = SubtleTextColor;

            UpdatePopupTextColors();
            UpdateThemeButtonStyles();
            UpdateZoomButtonStyles();
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
        private async void OnThemeClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not string modeValue)
                return;

            ApplyTheme(modeValue);
            UpdateThemeButtonStyles();

            await HideMenuAsync();
        }
    }
}