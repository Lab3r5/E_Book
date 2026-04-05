using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using E_Book.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace E_Book.Pages
{
    [QueryProperty(nameof(FilePath), "filePath")]
    public partial class ImageReaderPage : ContentPage
    {
        private const double DefaultScale = 1.0;
        private const double DoubleTapScale = 2.0;
        private const double MaxScale = 4.0;

        private readonly List<string> _imagePaths = new();

        private string _filePath = string.Empty;
        private int _currentIndex = -1;

        private double _currentScale = DefaultScale;
        private double _startScale = DefaultScale;

        private double _xOffset;
        private double _yOffset;
        private double _panStartX;
        private double _panStartY;

        private double _pinchStartXOffset;
        private double _pinchStartYOffset;

        private bool _isPanning;
        private bool _isPinching;
        private bool _isLoadingImage;
        private bool _isNavigating;
        private bool _topBarVisible = true;

        private bool _isThemeSheetOpen;
        private string _readerTheme = "Beige";

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = Uri.UnescapeDataString(value ?? string.Empty);
                _ = InitializeAsync();
            }
        }

        public ImageReaderPage()
        {
            InitializeComponent();
            ApplyThemeColors();
        }

        private async Task InitializeAsync()
        {
            try
            {
                SetLoading(true);
                ErrorState.IsVisible = false;

                if (string.IsNullOrWhiteSpace(_filePath))
                {
                    ShowError("No image file was provided.");
                    return;
                }

                BuildGallery();
                ResolveCurrentIndex();

                if (_currentIndex < 0 || _currentIndex >= _imagePaths.Count)
                {
                    ShowError("The selected image could not be found.");
                    return;
                }

                await LoadCurrentImageAsync(animated: false);
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

        private void BuildGallery()
        {
            _imagePaths.Clear();

            var books = LibraryService.LoadBooks();

            foreach (var book in books)
            {
                if (FileTypeHelper.IsImage(book.FullPath) && File.Exists(book.FullPath))
                    _imagePaths.Add(book.FullPath);
            }

            _imagePaths.Sort((a, b) =>
                string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(_filePath) &&
                File.Exists(_filePath) &&
                !_imagePaths.Contains(_filePath, StringComparer.OrdinalIgnoreCase))
            {
                _imagePaths.Insert(0, _filePath);
            }
        }

        private void ResolveCurrentIndex()
        {
            _currentIndex = _imagePaths.FindIndex(x =>
                string.Equals(x, _filePath, StringComparison.OrdinalIgnoreCase));

            if (_currentIndex < 0 && _imagePaths.Count > 0)
                _currentIndex = 0;
        }

        private async Task LoadCurrentImageAsync(bool animated)
        {
            if (_currentIndex < 0 || _currentIndex >= _imagePaths.Count)
            {
                ShowError("Unable to locate the image.");
                return;
            }

            string path = _imagePaths[_currentIndex];

            if (!File.Exists(path))
            {
                ShowError("The selected image file no longer exists.");
                return;
            }

            try
            {
                _isLoadingImage = true;
                SetLoading(true);
                ErrorState.IsVisible = false;

                _filePath = path;
                TitleLabel.Text = Path.GetFileName(path);

                ResetTransform();

                if (animated)
                {
                    ReaderImage.Opacity = 0;
                    ReaderImage.TranslationX = 18;
                }
                else
                {
                    ReaderImage.Opacity = 1;
                    ReaderImage.TranslationX = 0;
                }

                ReaderImage.Source = null;
                await Task.Delay(30);

                ReaderImage.Source = ImageSource.FromFile(path);
                await Task.Delay(50);

                if (animated)
                {
                    await Task.WhenAll(
                        ReaderImage.FadeTo(1, 150, Easing.CubicOut),
                        ReaderImage.TranslateTo(0, 0, 150, Easing.CubicOut)
                    );
                }

                ReadingMetaStore.UpdateLastOpened(path);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                _isLoadingImage = false;
                SetLoading(false);
            }
        }

        private void ResetTransform()
        {
            _currentScale = DefaultScale;
            _startScale = DefaultScale;

            _xOffset = 0;
            _yOffset = 0;
            _panStartX = 0;
            _panStartY = 0;

            _pinchStartXOffset = 0;
            _pinchStartYOffset = 0;

            _isPanning = false;
            _isPinching = false;

            ReaderImage.Scale = 1;
            ReaderImage.TranslationX = 0;
            ReaderImage.TranslationY = 0;
            ReaderImage.AnchorX = 0.5;
            ReaderImage.AnchorY = 0.5;
        }

        private void ApplyTransform()
        {
            ReaderImage.Scale = _currentScale;
            ReaderImage.TranslationX = _xOffset;
            ReaderImage.TranslationY = _yOffset;
        }

        private void ClampOffsets()
        {
            if (Viewport.Width <= 0 || Viewport.Height <= 0 || ReaderImage.Width <= 0 || ReaderImage.Height <= 0)
                return;

            if (_currentScale <= DefaultScale + 0.01)
            {
                _xOffset = 0;
                _yOffset = 0;
                return;
            }

            double scaledWidth = ReaderImage.Width * _currentScale;
            double scaledHeight = ReaderImage.Height * _currentScale;

            double maxX = Math.Max(0, (scaledWidth - Viewport.Width) / 2);
            double maxY = Math.Max(0, (scaledHeight - Viewport.Height) / 2);

            _xOffset = Math.Clamp(_xOffset, -maxX, maxX);
            _yOffset = Math.Clamp(_yOffset, -maxY, maxY);
        }

        private async Task AnimateBackToBoundsAsync()
        {
            ClampOffsets();

            await Task.WhenAll(
                ReaderImage.ScaleTo(_currentScale, 90, Easing.CubicOut),
                ReaderImage.TranslateTo(_xOffset, _yOffset, 90, Easing.CubicOut)
            );
        }

        private void SetLoading(bool isLoading)
        {
            LoadingOverlay.IsVisible = isLoading;
        }

        private void ShowError(string message)
        {
            ErrorState.IsVisible = true;
            ErrorMessageLabel.Text = message;
            SetLoading(false);
        }

        private async Task ShowPreviousImageAsync()
        {
            if (_isNavigating || _isLoadingImage || _isThemeSheetOpen || _currentScale > 1.01 || _imagePaths.Count <= 1)
                return;

            if (_currentIndex <= 0)
                return;

            _isNavigating = true;

            try
            {
                _currentIndex--;
                await LoadCurrentImageAsync(animated: true);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private async Task ShowNextImageAsync()
        {
            if (_isNavigating || _isLoadingImage || _isThemeSheetOpen || _currentScale > 1.01 || _imagePaths.Count <= 1)
                return;

            if (_currentIndex >= _imagePaths.Count - 1)
                return;

            _isNavigating = true;

            try
            {
                _currentIndex++;
                await LoadCurrentImageAsync(animated: true);
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            if (_isThemeSheetOpen)
            {
                await HideThemeSheetAsync();
                return;
            }

            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                try
                {
                    await Navigation.PopAsync();
                }
                catch
                {
                    if (Navigation.ModalStack.Count > 0)
                        await Navigation.PopModalAsync();
                }
            }
        }

        private async void OnViewportTapped(object sender, TappedEventArgs e)
        {
            if (_isThemeSheetOpen)
                return;

            _topBarVisible = !_topBarVisible;

            if (_topBarVisible)
            {
                TopBar.IsVisible = true;
                await TopBar.FadeTo(1, 150, Easing.CubicOut);
            }
            else
            {
                await TopBar.FadeTo(0, 150, Easing.CubicIn);
                TopBar.IsVisible = false;
            }
        }

        private async void OnImageDoubleTapped(object sender, TappedEventArgs e)
        {
            if (_isLoadingImage || _isThemeSheetOpen || _isPinching)
                return;

            if (_currentScale < 1.3)
            {
                _currentScale = DoubleTapScale;
            }
            else
            {
                _currentScale = DefaultScale;
                _xOffset = 0;
                _yOffset = 0;
            }

            ClampOffsets();

            await Task.WhenAll(
                ReaderImage.ScaleTo(_currentScale, 160, Easing.CubicOut),
                ReaderImage.TranslateTo(_xOffset, _yOffset, 160, Easing.CubicOut)
            );
        }

        private void OnImagePinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            if (_isLoadingImage || _isThemeSheetOpen)
                return;

            switch (e.Status)
            {
                case GestureStatus.Started:
                    _isPinching = true;
                    _startScale = _currentScale;
                    _pinchStartXOffset = _xOffset;
                    _pinchStartYOffset = _yOffset;
                    break;

                case GestureStatus.Running:
                    if (Viewport.Width <= 0 || Viewport.Height <= 0)
                        return;

                    double targetScale = Math.Clamp(_startScale * e.Scale, DefaultScale, MaxScale);
                    double scaleRatio = targetScale / _startScale;

                    double originX = (e.ScaleOrigin.X - 0.5) * Viewport.Width;
                    double originY = (e.ScaleOrigin.Y - 0.5) * Viewport.Height;

                    _xOffset = (_pinchStartXOffset * scaleRatio) + originX * (scaleRatio - 1);
                    _yOffset = (_pinchStartYOffset * scaleRatio) + originY * (scaleRatio - 1);

                    _currentScale = targetScale;

                    ClampOffsets();
                    ApplyTransform();
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _isPinching = false;

                    if (_currentScale <= DefaultScale + 0.01)
                    {
                        _currentScale = DefaultScale;
                        _xOffset = 0;
                        _yOffset = 0;
                    }

                    ClampOffsets();
                    ApplyTransform();
                    break;
            }
        }

        private async void OnImagePanUpdated(object sender, PanUpdatedEventArgs e)
        {
            if (_isLoadingImage || _isThemeSheetOpen || _isPinching)
                return;

            if (_currentScale <= DefaultScale + 0.01)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _isPanning = true;
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
                    _isPanning = false;
                    await AnimateBackToBoundsAsync();
                    break;
            }
        }

        private async void OnPreviousImageSwiped(object sender, SwipedEventArgs e)
        {
            await ShowPreviousImageAsync();
        }

        private async void OnNextImageSwiped(object sender, SwipedEventArgs e)
        {
            await ShowNextImageAsync();
        }

        private async void OnButtonPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement view)
                await view.ScaleTo(0.92, 70, Easing.CubicOut);
        }

        private async void OnButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement view)
                await view.ScaleTo(1, 90, Easing.CubicOut);
        }

        private async void OnMoreClicked(object sender, EventArgs e)
        {
            if (_isThemeSheetOpen)
            {
                await HideThemeSheetAsync();
                return;
            }

            await ShowThemeSheetAsync();
        }

        private async void OnThemeOverlayTapped(object sender, TappedEventArgs e)
        {
            if (!_isThemeSheetOpen)
                return;

            await HideThemeSheetAsync();
        }

        private async Task ShowThemeSheetAsync()
        {
            if (_isThemeSheetOpen)
                return;

            _isThemeSheetOpen = true;

            UpdateThemeSelectionUi();

            ThemeOverlay.IsVisible = true;
            ThemeOverlay.InputTransparent = false;
            ThemeOverlay.Opacity = 0;
            ThemeSheet.TranslationY = 240;

            await Task.WhenAll(
                ThemeOverlay.FadeTo(1, 180, Easing.CubicOut),
                ThemeSheet.TranslateTo(0, 0, 220, Easing.CubicOut)
            );
        }

        private async Task HideThemeSheetAsync()
        {
            if (!_isThemeSheetOpen)
                return;

            await Task.WhenAll(
                ThemeOverlay.FadeTo(0, 160, Easing.CubicIn),
                ThemeSheet.TranslateTo(0, 240, 180, Easing.CubicIn)
            );

            ThemeOverlay.IsVisible = false;
            ThemeOverlay.InputTransparent = true;
            _isThemeSheetOpen = false;
        }

        private async void OnThemeLightTapped(object sender, TappedEventArgs e)
        {
            await ApplyReaderThemeAsync("Light");
        }

        private async void OnThemeBeigeTapped(object sender, TappedEventArgs e)
        {
            await ApplyReaderThemeAsync("Beige");
        }

        private async void OnThemeGreenTapped(object sender, TappedEventArgs e)
        {
            await ApplyReaderThemeAsync("Green");
        }

        private async void OnThemeBlueTapped(object sender, TappedEventArgs e)
        {
            await ApplyReaderThemeAsync("Blue");
        }

        private async void OnThemeDarkTapped(object sender, TappedEventArgs e)
        {
            await ApplyReaderThemeAsync("Dark");
        }

        private async Task ApplyReaderThemeAsync(string theme)
        {
            _readerTheme = theme;
            ApplyThemeColors();
            UpdateThemeSelectionUi();
            await HideThemeSheetAsync();
        }

        private void ApplyThemeColors()
        {
            Color pageBg;
            Color barBg;
            Color textColor;
            Color secondaryText;
            Color sheetBg;
            Color handleColor;

            switch (_readerTheme)
            {
                case "Light":
                    pageBg = Color.FromArgb("#F3F0FA");
                    barBg = Color.FromArgb("#F3F0FA");
                    textColor = Color.FromArgb("#2E241A");
                    secondaryText = Color.FromArgb("#5B5146");
                    sheetBg = Colors.White;
                    handleColor = Color.FromArgb("#DDD6EA");
                    break;

                case "Green":
                    pageBg = Color.FromArgb("#C8D4B3");
                    barBg = Color.FromArgb("#C8D4B3");
                    textColor = Color.FromArgb("#223018");
                    secondaryText = Color.FromArgb("#425235");
                    sheetBg = Color.FromArgb("#F7F9F3");
                    handleColor = Color.FromArgb("#C7D2B7");
                    break;

                case "Blue":
                    pageBg = Color.FromArgb("#C8D5E8");
                    barBg = Color.FromArgb("#C8D5E8");
                    textColor = Color.FromArgb("#1E2B3C");
                    secondaryText = Color.FromArgb("#475B73");
                    sheetBg = Color.FromArgb("#F5F8FC");
                    handleColor = Color.FromArgb("#D5DFEE");
                    break;

                case "Dark":
                    pageBg = Color.FromArgb("#101014");
                    barBg = Color.FromArgb("#101014");
                    textColor = Colors.White;
                    secondaryText = Color.FromArgb("#D4D4E2");
                    sheetBg = Color.FromArgb("#1A1A22");
                    handleColor = Color.FromArgb("#3A3A46");
                    break;

                default:
                    pageBg = Color.FromArgb("#E8E1CF");
                    barBg = Color.FromArgb("#E8E1CF");
                    textColor = Color.FromArgb("#2E241A");
                    secondaryText = Color.FromArgb("#5B5146");
                    sheetBg = Colors.White;
                    handleColor = Color.FromArgb("#DDD6EA");
                    break;
            }

            BackgroundColor = pageBg;
            RootGrid.BackgroundColor = pageBg;
            ImageHost.BackgroundColor = pageBg;
            TopBar.BackgroundColor = barBg;

            TitleLabel.TextColor = textColor;
            BackButton.TextColor = textColor;
            MoreButton.TextColor = textColor;

            ErrorTitleLabel.TextColor = textColor;
            ErrorMessageLabel.TextColor = secondaryText;

            ThemeSheet.BackgroundColor = sheetBg;
            ThemeSheetTitle.TextColor = textColor;
            ThemeLabel.TextColor = textColor;
            ThemeHandle.BackgroundColor = handleColor;

            ThemeDarkIcon.TextColor = _readerTheme == "Dark"
                ? Colors.White
                : Color.FromArgb("#FFFFFF");
        }

        private void UpdateThemeSelectionUi()
        {
            ResetThemeBorder(ThemeLightBorder);
            ResetThemeBorder(ThemeBeigeBorder);
            ResetThemeBorder(ThemeGreenBorder);
            ResetThemeBorder(ThemeBlueBorder);
            ResetThemeBorder(ThemeDarkBorder);

            switch (_readerTheme)
            {
                case "Light":
                    HighlightThemeBorder(ThemeLightBorder, "#3A332B");
                    break;
                case "Green":
                    HighlightThemeBorder(ThemeGreenBorder, "#4D5E39");
                    break;
                case "Blue":
                    HighlightThemeBorder(ThemeBlueBorder, "#506784");
                    break;
                case "Dark":
                    HighlightThemeBorder(ThemeDarkBorder, "#AFA8FF");
                    break;
                default:
                    HighlightThemeBorder(ThemeBeigeBorder, "#7B6E52");
                    break;
            }
        }

        private static void ResetThemeBorder(Border border)
        {
            border.StrokeThickness = 0;
            border.Stroke = Colors.Transparent;
        }

        private static void HighlightThemeBorder(Border border, string strokeColor)
        {
            border.StrokeThickness = 3;
            border.Stroke = Color.FromArgb(strokeColor);
        }
    }
}