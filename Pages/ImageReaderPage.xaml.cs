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
        private const double DoubleTapScale = 2.2;
        private const double MaxScale = 4.5;
        private const double OverPanResistance = 0.28;
        private const double SwipeNavigateThreshold = 72;

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

        private DateTime _lastDoubleTapAt = DateTime.MinValue;
        private bool _isTopBarAnimating;

        private double _freePanTotalX;
        private double _freePanTotalY;
        private bool _hasTriggeredSwipeNavigation;

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

            TopBar.Opacity = 1;
            TopBar.TranslationY = 0;
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
                    ImageTransformHost.Opacity = 0;
                    ImageTransformHost.TranslationX = 16;
                }
                else
                {
                    ImageTransformHost.Opacity = 1;
                    ImageTransformHost.TranslationX = 0;
                }

                ReaderImage.Source = null;
                await Task.Yield();

                ReaderImage.Source = ImageSource.FromFile(path);

                if (animated)
                {
                    await Task.WhenAll(
                        ImageTransformHost.FadeTo(1, 160, Easing.CubicOut),
                        ImageTransformHost.TranslateTo(0, 0, 160, Easing.CubicOut)
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

            _freePanTotalX = 0;
            _freePanTotalY = 0;
            _hasTriggeredSwipeNavigation = false;

            ImageTransformHost.Scale = 1;
            ImageTransformHost.TranslationX = 0;
            ImageTransformHost.TranslationY = 0;
            ImageTransformHost.AnchorX = 0.5;
            ImageTransformHost.AnchorY = 0.5;
        }

        private void ApplyTransform()
        {
            ImageTransformHost.Scale = _currentScale;
            ImageTransformHost.TranslationX = _xOffset;
            ImageTransformHost.TranslationY = _yOffset;
        }

        private void ClampOffsets(bool withResistance = false)
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

            if (!withResistance)
            {
                _xOffset = Math.Clamp(_xOffset, -maxX, maxX);
                _yOffset = Math.Clamp(_yOffset, -maxY, maxY);
                return;
            }

            if (_xOffset < -maxX)
                _xOffset = -maxX + (_xOffset + maxX) * OverPanResistance;
            else if (_xOffset > maxX)
                _xOffset = maxX + (_xOffset - maxX) * OverPanResistance;

            if (_yOffset < -maxY)
                _yOffset = -maxY + (_yOffset + maxY) * OverPanResistance;
            else if (_yOffset > maxY)
                _yOffset = maxY + (_yOffset - maxY) * OverPanResistance;
        }

        private async Task AnimateBackToBoundsAsync()
        {
            ClampOffsets();

            await Task.WhenAll(
                ImageTransformHost.ScaleTo(_currentScale, 110, Easing.CubicOut),
                ImageTransformHost.TranslateTo(_xOffset, _yOffset, 110, Easing.CubicOut)
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
            if (_isNavigating || _isLoadingImage || _isThemeSheetOpen || _isPinching || _imagePaths.Count <= 1)
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
            if (_isNavigating || _isLoadingImage || _isThemeSheetOpen || _isPinching || _imagePaths.Count <= 1)
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
            if (_isThemeSheetOpen || _isLoadingImage || _isPinching || _isPanning || _isTopBarAnimating)
                return;

            if ((DateTime.UtcNow - _lastDoubleTapAt).TotalMilliseconds < 280)
                return;

            await ToggleTopBarAsync();
        }

        private async Task ToggleTopBarAsync()
        {
            if (_isTopBarAnimating)
                return;

            _isTopBarAnimating = true;

            try
            {
                _topBarVisible = !_topBarVisible;

                if (_topBarVisible)
                {
                    TopBar.IsVisible = true;
                    TopBar.Opacity = 0;
                    TopBar.TranslationY = -10;

                    await Task.WhenAll(
                        TopBar.FadeTo(1, 160, Easing.CubicOut),
                        TopBar.TranslateTo(0, 0, 160, Easing.CubicOut)
                    );
                }
                else
                {
                    await Task.WhenAll(
                        TopBar.FadeTo(0, 140, Easing.CubicIn),
                        TopBar.TranslateTo(0, -10, 140, Easing.CubicIn)
                    );

                    TopBar.IsVisible = false;
                    TopBar.TranslationY = 0;
                }
            }
            finally
            {
                _isTopBarAnimating = false;
            }
        }

        private async void OnImageDoubleTapped(object sender, TappedEventArgs e)
        {
            if (_isLoadingImage || _isThemeSheetOpen || _isPinching || _isPanning)
                return;

            _lastDoubleTapAt = DateTime.UtcNow;

            if (_currentScale < 1.25)
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
                ImageTransformHost.ScaleTo(_currentScale, 170, Easing.CubicOut),
                ImageTransformHost.TranslateTo(_xOffset, _yOffset, 170, Easing.CubicOut)
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
                    if (Viewport.Width <= 0 || Viewport.Height <= 0 || _startScale <= 0)
                        return;

                    double newScale = Math.Clamp(_startScale * e.Scale, DefaultScale, MaxScale);
                    double scaleRatio = newScale / _startScale;

                    double originX = (e.ScaleOrigin.X - 0.5) * Viewport.Width;
                    double originY = (e.ScaleOrigin.Y - 0.5) * Viewport.Height;

                    _currentScale = newScale;
                    _xOffset = _pinchStartXOffset - (originX * (scaleRatio - 1) * _startScale);
                    _yOffset = _pinchStartYOffset - (originY * (scaleRatio - 1) * _startScale);

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

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _isPanning = true;
                    _panStartX = _xOffset;
                    _panStartY = _yOffset;
                    _freePanTotalX = 0;
                    _freePanTotalY = 0;
                    _hasTriggeredSwipeNavigation = false;
                    break;

                case GestureStatus.Running:
                    if (_currentScale > DefaultScale + 0.01)
                    {
                        _xOffset = _panStartX + e.TotalX;
                        _yOffset = _panStartY + e.TotalY;

                        ClampOffsets(withResistance: true);
                        ApplyTransform();
                    }
                    else
                    {
                        _freePanTotalX = e.TotalX;
                        _freePanTotalY = e.TotalY;
                    }
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    _isPanning = false;

                    if (_currentScale > DefaultScale + 0.01)
                    {
                        await AnimateBackToBoundsAsync();
                    }
                    else
                    {
                        if (!_hasTriggeredSwipeNavigation &&
                            Math.Abs(_freePanTotalX) > SwipeNavigateThreshold &&
                            Math.Abs(_freePanTotalX) > Math.Abs(_freePanTotalY))
                        {
                            _hasTriggeredSwipeNavigation = true;

                            if (_freePanTotalX < 0)
                                await ShowNextImageAsync();
                            else
                                await ShowPreviousImageAsync();
                        }
                    }

                    _freePanTotalX = 0;
                    _freePanTotalY = 0;
                    break;
            }
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
            Color overlayColor;

            switch (_readerTheme)
            {
                case "Light":
                    pageBg = Color.FromArgb("#F3F0FA");
                    barBg = Color.FromArgb("#F3F0FA");
                    textColor = Color.FromArgb("#2E241A");
                    secondaryText = Color.FromArgb("#5B5146");
                    sheetBg = Colors.White;
                    handleColor = Color.FromArgb("#DDD6EA");
                    overlayColor = Color.FromArgb("#66000000");
                    break;

                case "Green":
                    pageBg = Color.FromArgb("#C8D4B3");
                    barBg = Color.FromArgb("#C8D4B3");
                    textColor = Color.FromArgb("#223018");
                    secondaryText = Color.FromArgb("#425235");
                    sheetBg = Color.FromArgb("#F4F7EE");
                    handleColor = Color.FromArgb("#C7D2B7");
                    overlayColor = Color.FromArgb("#66000000");
                    break;

                case "Blue":
                    pageBg = Color.FromArgb("#C8D5E8");
                    barBg = Color.FromArgb("#C8D5E8");
                    textColor = Color.FromArgb("#1E2B3C");
                    secondaryText = Color.FromArgb("#475B73");
                    sheetBg = Color.FromArgb("#F4F8FD");
                    handleColor = Color.FromArgb("#D5DFEE");
                    overlayColor = Color.FromArgb("#66000000");
                    break;

                case "Dark":
                    pageBg = Color.FromArgb("#101014");
                    barBg = Color.FromArgb("#101014");
                    textColor = Colors.White;
                    secondaryText = Color.FromArgb("#C9CBD6");
                    sheetBg = Color.FromArgb("#1A1A22");
                    handleColor = Color.FromArgb("#3A3A46");
                    overlayColor = Color.FromArgb("#99000000");
                    break;

                default:
                    pageBg = Color.FromArgb("#E8E1CF");
                    barBg = Color.FromArgb("#E8E1CF");
                    textColor = Color.FromArgb("#2E241A");
                    secondaryText = Color.FromArgb("#5B5146");
                    sheetBg = Color.FromArgb("#F7F1E3");
                    handleColor = Color.FromArgb("#CDBE9C");
                    overlayColor = Color.FromArgb("#66000000");
                    break;
            }

            BackgroundColor = pageBg;
            RootGrid.BackgroundColor = pageBg;
            ImageHost.BackgroundColor = pageBg;
            Viewport.BackgroundColor = pageBg;
            TopBar.BackgroundColor = barBg;

            TitleLabel.TextColor = textColor;
            BackButton.TextColor = textColor;
            MoreButton.TextColor = textColor;

            ErrorTitleLabel.TextColor = textColor;
            ErrorMessageLabel.TextColor = secondaryText;

            ThemeOverlay.BackgroundColor = overlayColor;

            ThemeSheet.Background = new SolidColorBrush(sheetBg);
            ThemeHandle.Background = new SolidColorBrush(handleColor);

            ThemeSheetTitle.TextColor = textColor;
            ThemeLabel.TextColor = textColor;
            ThemeDarkIcon.TextColor = Colors.White;
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