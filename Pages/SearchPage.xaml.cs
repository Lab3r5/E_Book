using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        private bool _isLoaded;
        private bool _hasPlayedEntrance;

        public SearchPage()
        {
            InitializeComponent();
            ResultList.ItemsSource = Results;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            LoadBooksToCache();
            RenderHistory();
            RestoreStateFromCurrentInput();

            if (!_hasPlayedEntrance)
            {
                _hasPlayedEntrance = true;
                await RunEntranceAsync();
            }

            FocusSearchLater();

            if (string.IsNullOrWhiteSpace(SearchEntry?.Text))
            {
                await PlayEmptyAnimation();
            }
        }

        private void LoadBooksToCache()
        {
            _allBooks.Clear();

            var books = LibraryService.LoadBooks();
            foreach (var book in books)
            {
                ReadingMetaStore.ApplyToBook(book);
                _allBooks.Add(book);
            }

            _isLoaded = true;
        }

        private async void FocusSearchLater()
        {
            await Task.Delay(180);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                SearchEntry?.Focus();
            });
        }

        private void RestoreStateFromCurrentInput()
        {
            var keyword = (SearchEntry.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                Results.Clear();
                ShowInitialState();
                UpdateClearButtonVisibility();
                return;
            }

            DoSearch(keyword, saveHistory: false);
        }

        private void OnSearchPressed(object sender, EventArgs e)
        {
            DoSearch(SearchEntry.Text ?? string.Empty, saveHistory: true);
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateClearButtonVisibility();

            var keyword = (e.NewTextValue ?? string.Empty).Trim();

            if (!_isLoaded)
                return;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                Results.Clear();
                ShowInitialState();
                _ = PlayEmptyAnimation();
                return;
            }

            DoSearch(keyword, saveHistory: true);
        }

        private void UpdateClearButtonVisibility()
        {
            if (ClearTextLabel == null || SearchEntry == null)
                return;

            ClearTextLabel.IsVisible = !string.IsNullOrWhiteSpace(SearchEntry.Text);
        }

        private void DoSearch(string keyword, bool saveHistory)
        {
            keyword = (keyword ?? string.Empty).Trim();
            Results.Clear();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                ShowInitialState();
                return;
            }

            if (saveHistory)
            {
                SearchHistoryStore.Add(UserSession.UserId, keyword);
                RenderHistory();
            }

            var result = LibraryService.SearchFromCache(_allBooks, keyword);

            foreach (var book in result)
            {
                ReadingMetaStore.ApplyToBook(book);
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
            EmptyTitleLabel.Text = "Search your books";
            EmptySubLabel.Text = "Find books you added to your library.";

            ResetEmptyVisualState();
        }

        private void ShowNoResultState(string keyword)
        {
            ResultTitleLabel.IsVisible = false;
            ResultList.IsVisible = false;

            EmptyStateLayout.IsVisible = true;
            EmptyTitleLabel.Text = "No search results";
            EmptySubLabel.Text = $"No books matched \"{keyword}\". Try another title or file format.";

            ResetEmptyVisualState();
            _ = PlayEmptyAnimation();
        }

        private void ShowResultState()
        {
            ResultTitleLabel.IsVisible = true;
            ResultList.IsVisible = true;
            EmptyStateLayout.IsVisible = false;

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

        private async Task PlayEmptyAnimation()
        {
            if (EmptyImage == null || !EmptyStateLayout.IsVisible)
                return;

            ResetEmptyVisualState();

            await Task.Delay(90);

            if (!EmptyStateLayout.IsVisible)
                return;

            await Task.WhenAll(
                EmptyImage.FadeTo(1, 250, Easing.CubicOut),
                EmptyImage.ScaleTo(1, 300, Easing.SpringOut)
            );

            await Task.WhenAll(
                EmptyTitleLabel.FadeTo(1, 190, Easing.CubicOut),
                EmptySubLabel.FadeTo(1, 210, Easing.CubicOut)
            );
        }

        private void RenderHistory()
        {
            var list = SearchHistoryStore.Get(UserSession.UserId);

            HistoryContainer.Children.Clear();
            HistorySection.IsVisible = list.Count > 0;

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

            var keywordFrame = new Frame
            {
                Padding = new Thickness(12, 6),
                CornerRadius = 14,
                HasShadow = false,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#2B2B2F")
                    : Color.FromArgb("#F4F1FF"),
                Content = new Label
                {
                    Text = keyword,
                    FontSize = 12.5,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Colors.White
                        : Color.FromArgb("#4A426F")
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, __) =>
            {
                SearchEntry.Text = keyword;
                DoSearch(keyword, saveHistory: true);
            };
            keywordFrame.GestureRecognizers.Add(tap);

            var deleteLabel = new Label
            {
                Text = "✕",
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#C8C1E3")
                    : Color.FromArgb("#8D86B2")
            };

            var deleteTap = new TapGestureRecognizer();
            deleteTap.Tapped += async (_, __) =>
            {
                bool ok = await DisplayAlert("Delete", $"Delete history \"{keyword}\"?", "Yes", "No");
                if (!ok) return;

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
            SearchEntry.Text = string.Empty;
            Results.Clear();
            ShowInitialState();
            SearchEntry.Focus();
            UpdateClearButtonVisibility();
            _ = PlayEmptyAnimation();
        }

        private void OnCancelTapped(object sender, TappedEventArgs e)
        {
            SearchEntry.Text = string.Empty;
            Results.Clear();
            ShowInitialState();
            SearchEntry.Unfocus();
            UpdateClearButtonVisibility();
            _ = PlayEmptyAnimation();
        }

        private async void OnClearHistoryTapped(object sender, TappedEventArgs e)
        {
            bool ok = await DisplayAlert("Clear history", "Delete all recent searches?", "Yes", "No");
            if (!ok) return;

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

                LoadBooksToCache();

                var keyword = (SearchEntry.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    Results.Clear();
                    ShowInitialState();
                    await PlayEmptyAnimation();
                }
                else
                {
                    DoSearch(keyword, saveHistory: false);
                }

                return;
            }

            var keywordToSave = (SearchEntry.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(keywordToSave))
            {
                SearchHistoryStore.AddOnRead(UserSession.UserId, keywordToSave);
                RenderHistory();
            }

            ReadingMetaStore.UpdateLastOpened(book.FullPath);

            var route = $"reading?filePath={Uri.EscapeDataString(book.FullPath)}";
            await Shell.Current.GoToAsync(route);
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