using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using E_Book.Services;
using E_Book.Models;

namespace E_Book.Pages
{
    public partial class BookshelfPage : ContentPage, INotifyPropertyChanged
    {
        public ObservableCollection<BookItem> Books { get; set; } = new();

        private bool _isMultiSelectMode;
        public bool IsMultiSelectMode
        {
            get => _isMultiSelectMode;
            set
            {
                if (_isMultiSelectMode == value) return;
                _isMultiSelectMode = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoadingBooks;
        public bool IsLoadingBooks
        {
            get => _isLoadingBooks;
            set
            {
                if (_isLoadingBooks == value) return;
                _isLoadingBooks = value;
                OnPropertyChanged();
            }
        }

        public string SelectedCountText
        {
            get
            {
                int count = Books.Count(b => b.IsSelected);
                return count == 0 ? "Select books" : $"{count} selected";
            }
        }

        public ICommand LongPressCommand { get; }

        private List<BookItem> _pendingDeleteItems = new();
        private readonly HashSet<BookItem> _subscribedItems = new();

        private bool _isHeaderAnimating;
        private bool _isSelectionCountPulsing;
        private bool _hasPlayedEntrance;
        private bool _isEmptyIconBreathing;
        private bool _hasLoadedOnce;

        private bool _refreshOnNextAppear = true;
        private bool _animateListOnNextAppear = true;

        public BookshelfPage()
        {
            InitializeComponent();

            LongPressCommand = new Command<BookItem>(OnItemLongPressed);

            EnsureLibraryExists();

            BindingContext = this;
            ReadingMetaStore.MetaChanged += OnReadingMetaChanged;

            UpdateSelectAllText();
            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            await Task.Yield();

            bool firstLoad = !_hasLoadedOnce;
            bool shouldRefresh = firstLoad || _refreshOnNextAppear;
            bool shouldAnimateList = firstLoad || _animateListOnNextAppear;

            if (firstLoad)
            {
                IsLoadingBooks = true;
                _ = StartSkeletonShimmer();
            }

            if (!_hasPlayedEntrance)
            {
                _hasPlayedEntrance = true;
                await PlayEntranceAnimationAsync();
            }
            else
            {
                if (RootHost != null)
                {
                    RootHost.Opacity = 1;
                    RootHost.TranslationY = 0;
                }

                if (HeaderSection != null)
                {
                    HeaderSection.Opacity = 1;
                    HeaderSection.TranslationY = 0;
                }

                if (MainCard != null)
                {
                    MainCard.Opacity = 1;
                    MainCard.TranslationY = 0;
                }
            }

            if (shouldRefresh)
            {
                await RefreshBooksAsync(showLoadingPlaceholder: firstLoad);
                _refreshOnNextAppear = false;
            }

            _hasLoadedOnce = true;

            if (shouldAnimateList)
            {
                if (Books.Count == 0)
                {
                    await AnimateEmptyState();
                    _ = StartEmptyIconBreathing();
                }
                else
                {
                    _isEmptyIconBreathing = false;

                    if (Books.Count < 30)
                        await AnimateBookListAppearance();
                    else
                        EnsureBookListVisibleImmediately();
                }

                _animateListOnNextAppear = false;
            }
            else
            {
                _isEmptyIconBreathing = false;
                EnsureBookListVisibleImmediately();

                if (EmptyStateContainer != null)
                {
                    EmptyStateContainer.Opacity = 1;
                    EmptyStateContainer.TranslationY = 0;
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isEmptyIconBreathing = false;
        }

        private void EnsureBookListVisibleImmediately()
        {
            if (BookCollectionView != null)
            {
                BookCollectionView.Opacity = 1;
                BookCollectionView.TranslationY = 0;
                BookCollectionView.IsVisible = !IsLoadingBooks;
            }
        }

        private async Task PlayEntranceAnimationAsync()
        {
            if (RootHost != null)
                RootHost.Opacity = 1;

            if (HeaderSection != null)
            {
                HeaderSection.Opacity = 0;
                HeaderSection.TranslationY = -16;
            }

            if (MainCard != null)
            {
                MainCard.Opacity = 0;
                MainCard.TranslationY = 26;
            }

            var tasks = new List<Task>();

            if (HeaderSection != null)
            {
                tasks.Add(HeaderSection.FadeTo(1, 320, Easing.CubicOut));
                tasks.Add(HeaderSection.TranslateTo(0, 0, 320, Easing.CubicOut));
            }

            if (MainCard != null)
            {
                tasks.Add(MainCard.FadeTo(1, 420, Easing.CubicOut));
                tasks.Add(MainCard.TranslateTo(0, 0, 420, Easing.CubicOut));
            }

            await Task.WhenAll(tasks);
        }

        private async Task RefreshBooksAsync(bool showLoadingPlaceholder = false)
        {
            if (showLoadingPlaceholder)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    IsLoadingBooks = true;
                    _isEmptyIconBreathing = false;

                    if (BookCollectionView != null)
                    {
                        BookCollectionView.Opacity = 0;
                        BookCollectionView.TranslationY = 10;
                    }
                });
            }

            var list = await Task.Run(() =>
            {
                var loaded = LibraryService.LoadBooks();
                var metaMap = ReadingMetaStore.LoadSnapshotMap();

                foreach (var b in loaded)
                {
                    b.IsSelected = false;

                    var key = b.FullPath.Trim().ToLowerInvariant();
                    if (metaMap.TryGetValue(key, out var meta))
                    {
                        b.ReadingProgress = meta.Progress;
                        b.LastOpenedTicks = meta.LastOpenedTicks;
                    }
                    else
                    {
                        b.ReadingProgress = 0;
                        b.LastOpenedTicks = 0;
                    }
                    b.RefreshVisualMeta();
                }

                return loaded
                    .OrderByDescending(b => b.LastOpenedTicks > 0)
                    .ThenByDescending(b => b.LastOpenedTicks)
                    .ThenBy(b => b.FileName)
                    .ToList();
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SyncBooksCollection(list);

                if (Books.Count == 0 && IsMultiSelectMode)
                    ExitMultiSelectMode();

                UpdateSelectAllText();
                UpdateConfirmState();
                OnPropertyChanged(nameof(SelectedCountText));

                IsLoadingBooks = false;
            });
        }

        private void SyncBooksCollection(List<BookItem> latest)
        {
            var existingByPath = Books.ToDictionary(b => b.FullPath, StringComparer.OrdinalIgnoreCase);

            for (int targetIndex = 0; targetIndex < latest.Count; targetIndex++)
            {
                var incoming = latest[targetIndex];

                if (existingByPath.TryGetValue(incoming.FullPath, out var existing))
                {
                    existing.FileName = incoming.FileName;
                    existing.Format = incoming.Format;
                    existing.ReadingProgress = incoming.ReadingProgress;
                    existing.LastOpenedTicks = incoming.LastOpenedTicks;
                    existing.IsSelected = false;
                    existing.RefreshVisualMeta();

                    int currentIndex = Books.IndexOf(existing);
                    if (currentIndex >= 0 && currentIndex != targetIndex)
                        Books.Move(currentIndex, targetIndex);
                }
                else
                {
                    incoming.IsSelected = false;

                    if (targetIndex >= Books.Count)
                        Books.Add(incoming);
                    else
                        Books.Insert(targetIndex, incoming);

                    SubscribeItem(incoming);
                    existingByPath[incoming.FullPath] = incoming;
                }
            }

            var latestPaths = latest
                .Select(b => b.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (int i = Books.Count - 1; i >= 0; i--)
            {
                var book = Books[i];
                if (!latestPaths.Contains(book.FullPath))
                {
                    UnsubscribeItem(book);
                    Books.RemoveAt(i);
                }
            }
        }

        private async Task StartSkeletonShimmer()
        {
            if (SkeletonShimmer == null)
                return;

            SkeletonShimmer.TranslationX = -420;

            while (IsLoadingBooks)
            {
                await SkeletonShimmer.TranslateTo(420, 0, 900, Easing.Linear);
                SkeletonShimmer.TranslationX = -420;
            }

            SkeletonShimmer.TranslationX = -420;
        }

        private async Task AnimateBookListAppearance()
        {
            if (BookCollectionView == null)
                return;

            BookCollectionView.Opacity = 0;
            BookCollectionView.TranslationY = 10;

            await Task.WhenAll(
                BookCollectionView.FadeTo(1, 220, Easing.CubicOut),
                BookCollectionView.TranslateTo(0, 0, 240, Easing.CubicOut)
            );
        }

        private void EnsureLibraryExists()
        {
            LibraryService.EnsureLibraryExists();

            string guidePath = Path.Combine(LibraryService.LibraryPath, "Usage Guidelines.txt");

            string guideContent = """
            Welcome to E_Book 📚

            E_Book is a modern cross-platform e-book reader built with .NET MAUI.
            It provides a clean reading experience, a smart bookshelf system,
            and intuitive mobile interactions designed for everyday reading.

            Current Version
            E_Book v1.07

            ────────────────────────────
            📚 Smart Bookshelf
            ────────────────────────────
            Your library is designed to behave like a modern reading dashboard.

            Features include:

            • Automatically generated book covers
            • File format labels for each book
            • Reading progress indicators
            • Continue Reading badges
            • Last opened time tracking
            • Recently opened books appear first

            Supported file formats:

            TXT
            EPUB
            PDF
            HTML / HTM
            DOCX
            RTF

            All imported files are stored inside the app's local Library folder
            to ensure stable access and fast loading.

            ────────────────────────────
            ➕ Importing Books
            ────────────────────────────
            Adding books to your library is simple.

            1. Tap the Add Book button
            2. Choose a supported file from your device
            3. The file will be copied into your E_Book library

            Notes:

            • Duplicate files will not be imported again
            • Imported books remain available even if the original file is removed

            ────────────────────────────
            📖 Reading Experience
            ────────────────────────────
            E_Book focuses on a distraction-free reading environment.

            Reading controls:

            • Tap Read to open a book
            • Swipe left or right to change pages
            • Tap the center of the screen to show or hide controls
            • Use the back button to return to the bookshelf

            Customization options include:

            • Font size adjustment
            • Light / Dark theme
            • Reading layout preferences

            ────────────────────────────
            🧠 Reading Progress
            ────────────────────────────
            Your reading activity is automatically saved.

            E_Book will remember:

            ✓ Your last reading position
            ✓ Reading progress percentage
            ✓ Last opened time
            ✓ Continue reading status

            When reopening a book, you will continue from where you stopped.

            ────────────────────────────
            🗑️ Bookshelf Management
            ────────────────────────────
            The bookshelf supports modern mobile interactions.

            You can manage books using:

            • Swipe left to reveal Delete
            • Tap the trash icon to enter multi-select mode
            • Long-press a book to quickly start selection
            • Use Select All / Unselect All
            • Confirm deletion with the ✓ button

            A confirmation dialog will appear before files are deleted.

            ────────────────────────────
            🔎 Search
            ────────────────────────────
            The Search page helps you quickly find books in your library.

            Features include:

            • Keyword search by file name
            • Search history
            • Quick access to matching books
            • Reading progress shown in results

            ────────────────────────────
            💡 Tips
            ────────────────────────────

            • Recently opened books appear at the top of the library
            • Books with reading progress show a Continue Reading badge
            • Large books may take slightly longer to load the first time
            • Deleting a book permanently removes it from the library

            ────────────────────────────

            Thank you for using E_Book.

            Enjoy your reading experience.
            """;

            File.WriteAllText(guidePath, guideContent);
        }

        private void SubscribeItem(BookItem item)
        {
            if (item == null) return;
            if (_subscribedItems.Contains(item)) return;

            item.PropertyChanged += OnBookItemPropertyChanged;
            _subscribedItems.Add(item);
        }

        private void UnsubscribeItem(BookItem item)
        {
            if (item == null) return;
            if (!_subscribedItems.Contains(item)) return;

            item.PropertyChanged -= OnBookItemPropertyChanged;
            _subscribedItems.Remove(item);
        }

        private void OnBookItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BookItem.IsSelected))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSelectAllText();
                    UpdateConfirmState();
                    OnPropertyChanged(nameof(SelectedCountText));

                    if (IsMultiSelectMode)
                        _ = PulseSelectionCountAsync();
                });
            }
        }

        private static FilePickerFileType BuildPickerTypes()
        {
            return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                {
                    DevicePlatform.Android,
                    new[]
                    {
                        "text/plain",
                        "application/epub+zip",
                        "application/pdf",
                        "text/html",
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        "application/rtf",
                        "*/*"
                    }
                },
                {
                    DevicePlatform.iOS,
                    new[]
                    {
                        "public.plain-text",
                        "org.idpf.epub-container",
                        "com.adobe.pdf",
                        "public.html",
                        "org.openxmlformats.wordprocessingml.document",
                        "public.rtf"
                    }
                }
            });
        }

        private async void OnAddFileClicked(object sender, EventArgs e)
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = BuildPickerTypes(),
                PickerTitle = "Select a file (TXT/EPUB/PDF/HTML/DOCX/RTF)"
            });

            if (result == null) return;

            var ext = Path.GetExtension(result.FileName)?.ToLowerInvariant() ?? "";
            if (!LibraryService.SupportedExtensions.Contains(ext))
            {
                await DisplayAlert("Not supported", $"Unsupported file type: {ext}", "OK");
                return;
            }

            string targetPath = Path.Combine(LibraryService.LibraryPath, result.FileName);

            if (File.Exists(targetPath))
            {
                await DisplayAlert("Notice", "This file has already been imported!", "OK");
                return;
            }

            await SaveFileToLibrary(result, targetPath);

            await RefreshBooksAsync();

            if (Books.Count < 30)
                await AnimateBookListAppearance();
            else
                EnsureBookListVisibleImmediately();

            await ShowToast("Imported successfully");

            _refreshOnNextAppear = false;
            _animateListOnNextAppear = false;
        }

        private async Task SaveFileToLibrary(FileResult file, string targetPath)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var newFileStream = File.Create(targetPath);
                await stream.CopyToAsync(newFileStream);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
            }
        }

        private async void OnFileClicked(object sender, EventArgs e)
        {
            if (IsMultiSelectMode) return;

            if (sender is Button button && button.CommandParameter is BookItem book)
            {
                if (!File.Exists(book.FullPath))
                {
                    await DisplayAlert("Error", "File not found!", "OK");
                    _refreshOnNextAppear = true;
                    _animateListOnNextAppear = false;
                    await RefreshBooksAsync();
                    return;
                }

                ReadingMetaStore.UpdateLastOpened(book.FullPath);
                book.LastOpenedTicks = DateTime.UtcNow.Ticks;
                book.RefreshVisualMeta();

                int currentIndex = Books.IndexOf(book);
                if (currentIndex > 0)
                    Books.Move(currentIndex, 0);
                
                _refreshOnNextAppear = false;
                _animateListOnNextAppear = false;

                var route = $"reading?filePath={Uri.EscapeDataString(book.FullPath)}";
                await Shell.Current.GoToAsync(route);
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (!IsMultiSelectMode) return;

            if (e.Parameter is BookItem book)
            {
                book.IsSelected = !book.IsSelected;
                UpdateConfirmState();
                OnPropertyChanged(nameof(SelectedCountText));
            }
        }

        private void OnItemLongPressed(BookItem? book)
        {
            if (book == null) return;

            if (!IsMultiSelectMode)
                EnterMultiSelectMode();

            if (!book.IsSelected)
                book.IsSelected = true;

            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        private async void OnTrashTapped(object sender, EventArgs e)
        {
            await AnimatePress(TrashButton);

            if (Books.Count == 0)
            {
                await ShowToast("No books to delete");
                return;
            }

            if (Books.Count == 1)
            {
                OpenDeleteDialog(new List<BookItem> { Books[0] }, single: true);
                return;
            }

            EnterMultiSelectMode();
        }

        private void OnSwipeDelete(object sender, EventArgs e)
        {
            if (sender is SwipeItem swipe && swipe.CommandParameter is BookItem book)
            {
                OpenDeleteDialog(new List<BookItem> { book }, single: true);
            }
        }

        private void EnterMultiSelectMode()
        {
            IsMultiSelectMode = true;

            foreach (var b in Books)
                b.IsSelected = false;

            UpdateSelectAllText();
            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));

            _ = PlayMultiSelectEnterAnimationAsync();
        }

        private void ExitMultiSelectMode()
        {
            IsMultiSelectMode = false;

            foreach (var b in Books)
                b.IsSelected = false;

            UpdateSelectAllText();
            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));

            ResetHeaderAnimationState();
        }

        private void OnSelectAllClicked(object? sender, EventArgs e)
        {
            if (!IsMultiSelectMode || Books.Count == 0) return;

            bool allSelected = Books.All(b => b.IsSelected);
            foreach (var b in Books)
                b.IsSelected = !allSelected;

            UpdateSelectAllText();
            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        private void UpdateSelectAllText()
        {
            if (SelectAllButton == null) return;

            if (!IsMultiSelectMode)
            {
                SelectAllButton.Text = "Select All";
                return;
            }

            bool allSelected = Books.Count > 0 && Books.All(b => b.IsSelected);
            SelectAllButton.Text = allSelected ? "Unselect All" : "Select All";
        }

        private void OnCancelMultiSelectClicked(object? sender, EventArgs e)
        {
            ExitMultiSelectMode();
        }

        private async void OnConfirmTapped(object sender, EventArgs e)
        {
            if (!IsMultiSelectMode) return;

            var selected = Books.Where(b => b.IsSelected).ToList();
            if (selected.Count == 0) return;

            await AnimatePress(ConfirmButton);
            OpenDeleteDialog(selected, single: false);
        }

        private void UpdateConfirmState()
        {
            if (ConfirmButton == null) return;

            if (!IsMultiSelectMode)
            {
                ConfirmButton.Opacity = 1;
                ConfirmButton.InputTransparent = false;
                ConfirmButton.Scale = 1;
                return;
            }

            bool hasSelected = Books.Any(b => b.IsSelected);
            ConfirmButton.Opacity = hasSelected ? 1.0 : 0.40;
            ConfirmButton.InputTransparent = !hasSelected;
        }

        private async void OpenDeleteDialog(List<BookItem> items, bool single)
        {
            _pendingDeleteItems = items;

            if (single)
            {
                var name = items[0].FileName;
                DeleteTitle.Text = "Delete this book?";
                DeleteMessage.Text = $"Delete \"{name}\"?\nThis action cannot be undone.";
            }
            else
            {
                DeleteTitle.Text = "Delete selected books?";
                DeleteMessage.Text = $"You are about to delete {items.Count} file(s).\nThis action cannot be undone.";
            }

            await ShowDeleteDialog();
        }

        private async Task ShowDeleteDialog()
        {
            DeleteDialog.Opacity = 0;
            DeleteDialog.Scale = 0.85;
            DeleteOverlay.IsVisible = true;

            await Task.WhenAll(
                DeleteDialog.FadeTo(1, 220, Easing.CubicOut),
                DeleteDialog.ScaleTo(1, 220, Easing.SpringOut)
            );
        }

        private async Task HideDeleteDialog()
        {
            await Task.WhenAll(
                DeleteDialog.FadeTo(0, 160, Easing.CubicIn),
                DeleteDialog.ScaleTo(0.85, 160, Easing.CubicIn)
            );

            DeleteOverlay.IsVisible = false;
        }

        private async void OnCancelDeleteDialog(object sender, EventArgs e)
        {
            await HideDeleteDialog();
        }

        private async void OnConfirmDeleteDialog(object sender, EventArgs e)
        {
            var toDelete = _pendingDeleteItems?.ToList() ?? new List<BookItem>();
            _pendingDeleteItems = new List<BookItem>();

            foreach (var book in toDelete)
                await DeleteBookFileAsync(book, reloadAfter: false);

            await HideDeleteDialog();

            if (IsMultiSelectMode)
                ExitMultiSelectMode();

            await RefreshBooksAsync();

            if (Books.Count < 30)
                await AnimateBookListAppearance();
            else
                EnsureBookListVisibleImmediately();

            await ShowToast("Deleted successfully");

            _refreshOnNextAppear = false;
            _animateListOnNextAppear = false;
        }

        private async Task DeleteBookFileAsync(BookItem book, bool reloadAfter)
        {
            try
            {
                var inList = Books.FirstOrDefault(x =>
                    string.Equals(x.FullPath, book.FullPath, StringComparison.OrdinalIgnoreCase));

                if (inList != null)
                {
                    UnsubscribeItem(inList);
                    Books.Remove(inList);
                }

                if (File.Exists(book.FullPath))
                    File.Delete(book.FullPath);

                ReadingMetaStore.Remove(book.FullPath);

                if (reloadAfter)
                    await RefreshBooksAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete file: {ex.Message}", "OK");
            }
        }

        private void ResetHeaderAnimationState()
        {
            if (CancelChip != null)
            {
                CancelChip.Opacity = 1;
                CancelChip.TranslationX = 0;
                CancelChip.Scale = 1;
            }

            if (MultiSelectTitleChip != null)
            {
                MultiSelectTitleChip.Opacity = 1;
                MultiSelectTitleChip.Scale = 1;
                MultiSelectTitleChip.TranslationY = 0;
            }

            if (ConfirmButton != null)
            {
                ConfirmButton.Opacity = IsMultiSelectMode
                    ? (Books.Any(b => b.IsSelected) ? 1.0 : 0.40)
                    : 1.0;

                ConfirmButton.TranslationX = 0;
                ConfirmButton.Scale = 1;
            }

            if (SelectAllChip != null)
            {
                SelectAllChip.Opacity = 1;
                SelectAllChip.TranslationY = 0;
                SelectAllChip.Scale = 1;
            }
        }

        private async Task PlayMultiSelectEnterAnimationAsync()
        {
            if (_isHeaderAnimating) return;
            if (!IsMultiSelectMode) return;

            _isHeaderAnimating = true;

            try
            {
                await Task.Delay(30);

                if (CancelChip != null)
                {
                    CancelChip.Opacity = 0;
                    CancelChip.TranslationX = -16;
                }

                if (MultiSelectTitleChip != null)
                {
                    MultiSelectTitleChip.Opacity = 0;
                    MultiSelectTitleChip.Scale = 0.94;
                    MultiSelectTitleChip.TranslationY = 6;
                }

                if (ConfirmButton != null)
                {
                    ConfirmButton.Opacity = 0;
                    ConfirmButton.TranslationX = 16;
                    ConfirmButton.Scale = 0.94;
                }

                if (SelectAllChip != null)
                {
                    SelectAllChip.Opacity = 0;
                    SelectAllChip.TranslationY = -8;
                }

                var tasks = new List<Task>();

                if (CancelChip != null)
                {
                    tasks.Add(CancelChip.FadeTo(1, 160, Easing.CubicOut));
                    tasks.Add(CancelChip.TranslateTo(0, 0, 180, Easing.CubicOut));
                }

                if (MultiSelectTitleChip != null)
                {
                    tasks.Add(MultiSelectTitleChip.FadeTo(1, 180, Easing.CubicOut));
                    tasks.Add(MultiSelectTitleChip.ScaleTo(1, 180, Easing.CubicOut));
                    tasks.Add(MultiSelectTitleChip.TranslateTo(0, 0, 180, Easing.CubicOut));
                }

                if (ConfirmButton != null)
                {
                    double targetOpacity = Books.Any(b => b.IsSelected) ? 1.0 : 0.40;
                    tasks.Add(ConfirmButton.FadeTo(targetOpacity, 180, Easing.CubicOut));
                    tasks.Add(ConfirmButton.TranslateTo(0, 0, 180, Easing.CubicOut));
                    tasks.Add(ConfirmButton.ScaleTo(1, 180, Easing.CubicOut));
                }

                if (SelectAllChip != null)
                {
                    tasks.Add(SelectAllChip.FadeTo(1, 180, Easing.CubicOut));
                    tasks.Add(SelectAllChip.TranslateTo(0, 0, 180, Easing.CubicOut));
                }

                await Task.WhenAll(tasks);
            }
            catch
            {
            }
            finally
            {
                _isHeaderAnimating = false;
            }
        }

        private async Task PulseSelectionCountAsync()
        {
            if (_isSelectionCountPulsing) return;
            if (MultiSelectTitleChip == null) return;
            if (!IsMultiSelectMode) return;

            _isSelectionCountPulsing = true;

            try
            {
                await MultiSelectTitleChip.ScaleTo(1.05, 90, Easing.CubicOut);
                await MultiSelectTitleChip.ScaleTo(1.0, 110, Easing.CubicOut);
            }
            catch
            {
            }
            finally
            {
                _isSelectionCountPulsing = false;
            }
        }

        private async Task AnimatePress(VisualElement view)
        {
            if (view == null) return;
            await view.ScaleTo(0.88, 80, Easing.CubicIn);
            await view.ScaleTo(1.00, 90, Easing.CubicOut);
        }

        private async Task ShowToast(string message)
        {
            if (ToastFrame == null || ToastLabel == null) return;

            ToastLabel.Text = message;
            ToastFrame.IsVisible = true;
            ToastFrame.Opacity = 0;
            ToastFrame.TranslationY = 10;

            await Task.WhenAll(
                ToastFrame.FadeTo(1, 160, Easing.CubicOut),
                ToastFrame.TranslateTo(0, 0, 160, Easing.CubicOut)
            );

            await Task.Delay(1100);

            await Task.WhenAll(
                ToastFrame.FadeTo(0, 220, Easing.CubicIn),
                ToastFrame.TranslateTo(0, 10, 220, Easing.CubicIn)
            );

            ToastFrame.IsVisible = false;
        }

        private async Task AnimateEmptyState()
        {
            if (EmptyStateContainer == null)
                return;

            EmptyStateContainer.Opacity = 0;
            EmptyStateContainer.TranslationY = 20;

            await Task.WhenAll(
                EmptyStateContainer.FadeTo(1, 400, Easing.CubicOut),
                EmptyStateContainer.TranslateTo(0, 0, 420, Easing.CubicOut)
            );
        }

        private async Task StartEmptyIconBreathing()
        {
            if (EmptyIcon == null || _isEmptyIconBreathing)
                return;

            _isEmptyIconBreathing = true;

            try
            {
                while (_isEmptyIconBreathing && Books.Count == 0)
                {
                    await EmptyIcon.ScaleTo(1.08, 900, Easing.CubicInOut);
                    if (!_isEmptyIconBreathing || Books.Count != 0) break;
                    await EmptyIcon.ScaleTo(1.0, 900, Easing.CubicInOut);
                }
            }
            catch
            {
            }
            finally
            {
                _isEmptyIconBreathing = false;
                if (EmptyIcon != null)
                    EmptyIcon.Scale = 1.0;
            }
        }
        private void OnReadingMetaChanged(object? sender, ReadingMetaStore.ReadingMetaChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (string.IsNullOrWhiteSpace(e.FullPath))
                    return;

                var book = Books.FirstOrDefault(x =>
                    string.Equals(x.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase));

                if (book == null)
                    return;

                if (e.Removed)
                {
                    UnsubscribeItem(book);
                    Books.Remove(book);

                    UpdateSelectAllText();
                    UpdateConfirmState();
                    OnPropertyChanged(nameof(SelectedCountText));
                    return;
                }

                book.ReadingProgress = e.Progress;
                book.LastOpenedTicks = e.LastOpenedTicks;
                book.RefreshVisualMeta();

                int oldIndex = Books.IndexOf(book);
                if (oldIndex > 0)
                    Books.Move(oldIndex, 0);
            });
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}