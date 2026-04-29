using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E_Book.Models;
using E_Book.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace E_Book.Pages
{
    public partial class SearchPage : ContentPage
    {
        public ObservableCollection<BookItem> Results { get; } = new();

        private readonly List<BookItem> _allBooks = new();
        private string _loadedStorageKey = string.Empty;

        private bool _isLoaded;
        private bool _hasPlayedEntrance;
        private bool _skipAutoFocusOnce;
        private bool _isEmptyAnimationRunning;

        private CancellationTokenSource? _searchDebounceCts;

        public SearchPage()
        {
            InitializeComponent();
            ResultList.ItemsSource = Results;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await EnsureBooksLoadedAsync();
            RenderHistory();
            RestoreStateFromCurrentInput();

            if (!_hasPlayedEntrance)
            {
                _hasPlayedEntrance = true;
                await RunEntranceAsync();
            }

            bool shouldAutoFocus = !_skipAutoFocusOnce;

            if (_skipAutoFocusOnce)
                _skipAutoFocusOnce = false;

            if (shouldAutoFocus)
                FocusSearchLater();

            if (string.IsNullOrWhiteSpace(SearchEntry?.Text))
            {
                await PlayEmptyAnimationIfNeeded();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _searchDebounceCts?.Cancel();

            try
            {
                SearchEntry?.Unfocus();
            }
            catch
            {
            }

#if ANDROID
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MainActivity.Instance?.HideSoftKeyboard();
            });
#endif
        }

        private async Task EnsureBooksLoadedAsync(bool forceRefresh = false)
        {
            string storageKey = UserSession.StorageKey;
            bool needsReload =
                forceRefresh ||
                !_isLoaded ||
                !string.Equals(_loadedStorageKey, storageKey, StringComparison.Ordinal);

            if (!needsReload)
                return;

            var books = await Task.Run(() =>
            {
                var loaded = LibraryService.LoadBooks(forceRefresh);
                var metaMap = ReadingMetaStore.LoadSnapshotMap();

                foreach (var book in loaded)
                {
                    string key = (book.FullPath ?? string.Empty).Trim().ToLowerInvariant();
                    if (metaMap.TryGetValue(key, out var meta))
                    {
                        book.ReadingProgress = meta.Progress;
                        book.LastOpenedTicks = meta.LastOpenedTicks;
                        book.LastReadPage = meta.LastReadPage;
                        book.TotalPages = meta.TotalPages;
                        book.TotalReadingSeconds = meta.TotalReadingSeconds;
                    }

                    book.RefreshVisualMeta();
                }

                return loaded;
            });

            _allBooks.Clear();
            _allBooks.AddRange(books);
            _loadedStorageKey = storageKey;
            _isLoaded = true;
        }
        private async void FocusSearchLater()
        {
            await Task.Delay(220);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    SearchEntry?.Unfocus();
                    await Task.Delay(25);
                    SearchEntry?.Focus();
                }
                catch
                {
                }
            });
        }

        private void RestoreStateFromCurrentInput()
        {
            string keyword = (SearchEntry.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                Results.Clear();
                ShowInitialState();
                UpdateClearButtonVisibility();
                return;
            }

            HistorySection.IsVisible = false;
            DoSearch(keyword);
        }

        private void OnSearchPressed(object sender, EventArgs e)
        {
            string keyword = (SearchEntry.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                Results.Clear();
                ShowInitialState();
                _ = PlayEmptyAnimationIfNeeded();
                return;
            }

            HistorySection.IsVisible = false;
            DoSearch(keyword);
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateClearButtonVisibility();

            string keyword = (e.NewTextValue ?? string.Empty).Trim();

            if (!_isLoaded)
                return;

            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                Results.Clear();
                ShowInitialState();
                RenderHistory();
                await PlayEmptyAnimationIfNeeded();
                return;
            }

            try
            {
                await Task.Delay(220, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            HistorySection.IsVisible = false;
            DoSearch(keyword);
        }

        private void UpdateClearButtonVisibility()
        {
            if (ClearTextLabel == null || SearchEntry == null)
                return;

            ClearTextLabel.IsVisible = !string.IsNullOrWhiteSpace(SearchEntry.Text);
        }

        private void DoSearch(string keyword)
        {
            keyword = (keyword ?? string.Empty).Trim();
            Results.Clear();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                ShowInitialState();
                return;
            }

            var rawResults = LibraryService.SearchFromCache(_allBooks, keyword);

            var orderedResults = rawResults
                .Select(book =>
                {
                    ReadingMetaStore.ApplyToBook(book);

                    string fileName = (book.DisplayFileName ?? Path.GetFileNameWithoutExtension(book.FileName) ?? string.Empty).Trim();
                    string compareName = fileName.ToLowerInvariant();
                    string compareKeyword = keyword.ToLowerInvariant();

                    int prefixScore = compareName.StartsWith(compareKeyword) ? 0 : 1;
                    int containsScore = compareName.Contains(compareKeyword) ? 0 : 1;
                    int progressScore = book.HasProgress ? 0 : 1;
                    long recentScore = book.LastOpenedTicks > 0 ? -book.LastOpenedTicks : long.MaxValue;

                    return new
                    {
                        Book = book,
                        prefixScore,
                        containsScore,
                        progressScore,
                        recentScore
                    };
                })
                .OrderBy(x => x.prefixScore)
                .ThenBy(x => x.containsScore)
                .ThenBy(x => x.progressScore)
                .ThenBy(x => x.recentScore)
                .Select(x => x.Book)
                .ToList();

            foreach (var book in orderedResults)
            {
                Results.Add(book);
            }

            if (Results.Count == 0)
                ShowNoResultState(keyword);
            else
                ShowResultState();
        }

        private void ShowInitialState()
        {
            ResultTitleLabel.IsVisible = false;
            ResultList.IsVisible = false;

            EmptyStateLayout.IsVisible = true;
            HistorySection.IsVisible = SearchHistoryStore.Get(UserSession.UserId).Count > 0;

            EmptyTitleLabel.Text = "Search your books";
            EmptySubLabel.Text = "Find books you added to your library.";

            ResetEmptyVisualState();
        }

        private void ShowNoResultState(string keyword)
        {
            ResultTitleLabel.IsVisible = false;
            ResultList.IsVisible = false;

            EmptyStateLayout.IsVisible = true;
            HistorySection.IsVisible = false;

            EmptyTitleLabel.Text = "No search results";
            EmptySubLabel.Text = $"No books matched \"{keyword}\". Try another title or file format.";

            ResetEmptyVisualState();
            _ = PlayEmptyAnimationIfNeeded();
        }

        private void ShowResultState()
        {
            ResultTitleLabel.IsVisible = true;
            ResultList.IsVisible = true;
            EmptyStateLayout.IsVisible = false;
            HistorySection.IsVisible = false;

            _ = PlayResultListAnimation();
        }

        private void ResetEmptyVisualState()
        {
            if (EmptyImage != null)
            {
                EmptyImage.Opacity = 0;
                EmptyImage.Scale = 0.85;
            }

            if (EmptyTitleLabel != null)
                EmptyTitleLabel.Opacity = 0;

            if (EmptySubLabel != null)
                EmptySubLabel.Opacity = 0;
        }

        private async Task PlayEmptyAnimationIfNeeded()
        {
            if (_isEmptyAnimationRunning)
                return;

            if (EmptyImage == null || !EmptyStateLayout.IsVisible)
                return;

            _isEmptyAnimationRunning = true;

            try
            {
                ResetEmptyVisualState();

                await Task.Delay(70);

                if (!EmptyStateLayout.IsVisible)
                    return;

                await Task.WhenAll(
                    EmptyImage.FadeTo(1, 230, Easing.CubicOut),
                    EmptyImage.ScaleTo(1, 280, Easing.SpringOut)
                );

                await Task.WhenAll(
                    EmptyTitleLabel.FadeTo(1, 180, Easing.CubicOut),
                    EmptySubLabel.FadeTo(1, 200, Easing.CubicOut)
                );
            }
            finally
            {
                _isEmptyAnimationRunning = false;
            }
        }

        private void RenderHistory()
        {
            var list = SearchHistoryStore.Get(UserSession.UserId);

            HistoryContainer.Children.Clear();
            HistorySection.IsVisible = list.Count > 0 && string.IsNullOrWhiteSpace(SearchEntry.Text);

            foreach (var keyword in list)
            {
                var chip = BuildHistoryChip(keyword);
                HistoryContainer.Children.Add(chip);
            }
        }

        private View BuildHistoryChip(string keyword)
        {
            var holder = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 6,
                Margin = new Thickness(0, 0, 8, 8)
            };

            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

            var keywordFrame = new Border
            {
                Padding = new Thickness(12, 6),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(14) },
                BackgroundColor = isDark
                    ? Color.FromArgb("#2B2B2F")
                    : Color.FromArgb("#F4F1FF"),
                Content = new Label
                {
                    Text = keyword,
                    FontSize = 12.5,
                    TextColor = isDark
                        ? Colors.White
                        : Color.FromArgb("#4A426F")
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, __) =>
            {
                SearchEntry.Text = keyword;
                HistorySection.IsVisible = false;
                DoSearch(keyword);
            };
            keywordFrame.GestureRecognizers.Add(tap);

            var deleteLabel = new Label
            {
                Text = "✕",
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                TextColor = isDark
                    ? Color.FromArgb("#C8C1E3")
                    : Color.FromArgb("#8D86B2")
            };

            var deleteTap = new TapGestureRecognizer();
            deleteTap.Tapped += async (_, __) =>
            {
                bool ok = await DisplayAlert("Delete", $"Delete history \"{keyword}\"?", "Yes", "No");
                if (!ok)
                    return;

                SearchHistoryStore.Delete(UserSession.UserId, keyword);
                RenderHistory();
            };
            deleteLabel.GestureRecognizers.Add(deleteTap);

            holder.Add(keywordFrame);
            holder.Add(deleteLabel, 1);

            return holder;
        }

        private void OnClearTapped(object sender, TappedEventArgs e)
        {
            _searchDebounceCts?.Cancel();

            SearchEntry.Text = string.Empty;
            Results.Clear();
            ShowInitialState();
            SearchEntry.Focus();
            UpdateClearButtonVisibility();
            RenderHistory();
            _ = PlayEmptyAnimationIfNeeded();
        }

        private void OnCancelTapped(object sender, TappedEventArgs e)
        {
            _searchDebounceCts?.Cancel();

            SearchEntry.Text = string.Empty;
            Results.Clear();
            ShowInitialState();
            SearchEntry.Unfocus();

#if ANDROID
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MainActivity.Instance?.HideSoftKeyboard();
            });
#endif

            UpdateClearButtonVisibility();
            RenderHistory();
            _ = PlayEmptyAnimationIfNeeded();
        }

        private async void OnClearHistoryTapped(object sender, TappedEventArgs e)
        {
            bool ok = await DisplayAlert("Clear history", "Delete all recent searches?", "Yes", "No");
            if (!ok)
                return;

            SearchHistoryStore.Clear(UserSession.UserId);
            RenderHistory();
        }

        private async void OnReadClicked(object sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not BookItem book)
                return;

            await OpenBookAsync(book);
        }

        private async void OnResultCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not BookItem book)
                return;

            await OpenBookAsync(book);
        }

        private async Task OpenBookAsync(BookItem book)
        {
            if (!File.Exists(book.FullPath))
            {
                await DisplayAlert("Error", "File not found!", "OK");

                await EnsureBooksLoadedAsync(forceRefresh: true);

                string keyword = (SearchEntry.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    Results.Clear();
                    ShowInitialState();
                    await PlayEmptyAnimationIfNeeded();
                }
                else
                {
                    DoSearch(keyword);
                }

                return;
            }

            string keywordToSave = (SearchEntry.Text ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(keywordToSave))
            {
                SearchHistoryStore.AddOnRead(UserSession.UserId, keywordToSave);
            }

            ReadingMetaStore.UpdateLastOpened(book.FullPath);

            _skipAutoFocusOnce = true;

#if ANDROID
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchEntry?.Unfocus();
                MainActivity.Instance?.HideSoftKeyboard();
            });
#endif

            if (FileTypeHelper.IsImage(book.FullPath))
            {
                string imageRoute = $"{AppShell.RouteImageReader}?filePath={Uri.EscapeDataString(book.FullPath)}";
                await Shell.Current.GoToAsync(imageRoute);
                return;
            }

            string routeName = FileTypeHelper.GetRouteByPath(book.FullPath);

            if (routeName == AppShell.RouteReading)
            {
                bool restart = book.IsCompleted;
                string route =
                    $"{routeName}?filePath={Uri.EscapeDataString(book.FullPath)}&restart={restart.ToString().ToLowerInvariant()}";

                await Shell.Current.GoToAsync(route);
            }
            else
            {
                string route = $"{routeName}?filePath={Uri.EscapeDataString(book.FullPath)}";
                await Shell.Current.GoToAsync(route);
            }
        }

        private async Task PlayResultListAnimation()
        {
            if (ResultList == null || !ResultList.IsVisible)
                return;

            await UIAnimationService.FadeListInAsync(ResultList, 18, 220, 30);
        }

        private async Task RunEntranceAsync()
        {
            if (HeaderBlock != null)
            {
                HeaderBlock.Opacity = 0;
                HeaderBlock.TranslationY = 8;
            }

            if (SearchBarCard != null)
            {
                SearchBarCard.Opacity = 0;
                SearchBarCard.TranslationY = 14;
                SearchBarCard.Scale = 0.995;
            }

            if (MainCard != null)
            {
                MainCard.Opacity = 0;
                MainCard.TranslationY = 14;
                MainCard.Scale = 0.995;
            }

            await UIAnimationService.FadeSlideInAsync(HeaderBlock, 8, 180);
            await UIAnimationService.FadeScaleCardInAsync(SearchBarCard, 14, 0.995, 230, 10);
            await UIAnimationService.FadeScaleCardInAsync(MainCard, 14, 0.995, 240, 10);
        }
    }
}
