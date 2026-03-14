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
        private enum ImportFeedbackState
        {
            Idle,
            Success,
            Failure
        }

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

        private bool _isImporting;
        public bool IsImporting
        {
            get => _isImporting;
            set
            {
                if (_isImporting == value) return;
                _isImporting = value;
                OnPropertyChanged();
                RaiseImportUiProperties();
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

        public string AddBookButtonTitle
        {
            get
            {
                if (IsImporting) return "Importing...";
                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => "Imported",
                    ImportFeedbackState.Failure => "Import failed",
                    _ => "Add Book"
                };
            }
        }

        public string AddBookButtonSubtitle
        {
            get
            {
                if (IsImporting) return "Please wait while we add your file";
                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => "Your book is ready in the library",
                    ImportFeedbackState.Failure => "Please check the file and try again",
                    _ => "Import a new file to your library"
                };
            }
        }

        public string AddBookHelperText
        {
            get
            {
                if (IsImporting) return "Import in progress...";
                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => "Book added successfully.",
                    ImportFeedbackState.Failure => "Import failed. Supports TXT, EPUB, PDF, HTML, DOCX, and RTF.",
                    _ => "Supports TXT, EPUB, PDF, HTML, DOCX, and RTF."
                };
            }
        }

        public double AddBookArrowOpacity => IsImporting ? 0.35 : 1.0;

        public bool ShowAddBookPlus => !IsImporting;

        public string EmptyStatePrimaryButtonText => IsImporting ? "Importing..." : "Add Your First Book";

        public string EmptyStateSecondaryHint
        {
            get
            {
                if (IsImporting)
                    return "Please keep this page open while your book is being imported.";

                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => "Nice — your first book has been added.",
                    ImportFeedbackState.Failure => "Import failed. Try another supported file.",
                    _ => "Tip: imported files stay in your local library for quick access."
                };
            }
        }

        public string InProgressCountText => Books.Count(b => b.ReadingProgress > 0 && b.ReadingProgress < 0.999).ToString();

        public string CompletedCountText => Books.Count(b => b.ReadingProgress >= 0.999).ToString();

        public string TotalReadingTimeText
        {
            get
            {
                long totalSeconds = Books.Sum(b => b.TotalReadingSeconds);
                var ts = TimeSpan.FromSeconds(totalSeconds);

                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m";

                if (ts.TotalMinutes >= 1)
                    return $"{Math.Max(1, (int)ts.TotalMinutes)}m";

                return "0m";
            }
        }

        public ICommand LongPressCommand { get; }

        private List<BookItem> _pendingDeleteItems = new();
        private readonly HashSet<BookItem> _subscribedItems = new();
        private readonly Dictionary<string, WeakReference<Border>> _bookCardMap = new(StringComparer.OrdinalIgnoreCase);

        private bool _isHeaderAnimating;
        private bool _isSelectionCountPulsing;
        private bool _hasPlayedEntrance;
        private bool _isEmptyIconBreathing;
        private bool _hasLoadedOnce;

        private bool _refreshOnNextAppear = true;
        private bool _animateListOnNextAppear = true;

        private ImportFeedbackState _importFeedbackState = ImportFeedbackState.Idle;
        private int _importFeedbackVersion;
        private string? _pendingHighlightBookPath;

        public BookshelfPage()
        {
            InitializeComponent();

            LongPressCommand = new Command<BookItem>(OnItemLongPressed);

            EnsureLibraryExists();

            BindingContext = this;
            ReadingMetaStore.MetaChanged += OnReadingMetaChanged;

            UpdateSelectAllText();
            UpdateConfirmState();
            RaiseSummaryProperties();
            OnPropertyChanged(nameof(SelectedCountText));
            RaiseImportUiProperties();
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

            await TryHighlightPendingBookAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isEmptyIconBreathing = false;
        }

        private void RaiseImportUiProperties()
        {
            OnPropertyChanged(nameof(AddBookButtonTitle));
            OnPropertyChanged(nameof(AddBookButtonSubtitle));
            OnPropertyChanged(nameof(AddBookHelperText));
            OnPropertyChanged(nameof(AddBookArrowOpacity));
            OnPropertyChanged(nameof(ShowAddBookPlus));
            OnPropertyChanged(nameof(EmptyStatePrimaryButtonText));
            OnPropertyChanged(nameof(EmptyStateSecondaryHint));
        }

        private void RaiseSummaryProperties()
        {
            OnPropertyChanged(nameof(InProgressCountText));
            OnPropertyChanged(nameof(CompletedCountText));
            OnPropertyChanged(nameof(TotalReadingTimeText));
        }

        private async Task SetTransientImportFeedbackAsync(ImportFeedbackState state)
        {
            _importFeedbackState = state;
            int version = ++_importFeedbackVersion;
            RaiseImportUiProperties();

            await Task.Delay(1500);

            if (version != _importFeedbackVersion || IsImporting)
                return;

            _importFeedbackState = ImportFeedbackState.Idle;
            RaiseImportUiProperties();
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
                        b.LastReadPage = meta.LastReadPage;
                        b.TotalPages = meta.TotalPages;
                        b.TotalReadingSeconds = meta.TotalReadingSeconds;
                    }
                    else
                    {
                        b.ReadingProgress = 0;
                        b.LastOpenedTicks = 0;
                        b.LastReadPage = 0;
                        b.TotalPages = 0;
                        b.TotalReadingSeconds = 0;
                    }

                    b.RefreshVisualMeta();
                }

                return loaded
                    .OrderByDescending(b => b.LastOpenedTicks > 0)
                    .ThenByDescending(b => b.LastOpenedTicks)
                    .ThenBy(b => b.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SyncBooksCollection(list);

                if (Books.Count == 0 && IsMultiSelectMode)
                    ExitMultiSelectMode();

                UpdateSelectAllText();
                UpdateConfirmState();
                RaiseSummaryProperties();
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
                    existing.LastReadPage = incoming.LastReadPage;
                    existing.TotalPages = incoming.TotalPages;
                    existing.TotalReadingSeconds = incoming.TotalReadingSeconds;
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
            It focuses on a clean reading experience, an intelligent bookshelf system,
            and smooth mobile-style interactions.

            Current Version
            E_Book v1.07

            ────────────────────────────
            📚 Smart Library
            ────────────────────────────
            Your library acts as a reading dashboard where all imported books are organized automatically.

            Features include:

            • Automatically generated book covers
            • File format labels
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

            All imported files are stored inside the app's private Library folder to ensure stable access.

            ────────────────────────────
            ➕ Importing Books
            ────────────────────────────
            Adding books to your library is simple.

            1. Tap the "Add Book" button
            2. Select a supported file
            3. The file will be copied into your library

            During import you will see a loading indicator.

            Notes:

            • Duplicate files will not be imported again
            • Imported books remain available even if the original file is removed
            • Large files may take a few seconds to import

            ────────────────────────────
            📖 Reading Books
            ────────────────────────────
            To start reading a book:

            1. Tap the "Read" button on a book card
            2. The reading page will open instantly

            Reader controls include:

            • Swipe left / right to change pages
            • Tap the center to toggle reading controls
            • Use the back button to return to the library

            The reading interface is designed to minimize distractions.

            ────────────────────────────
            🧠 Reading Progress
            ────────────────────────────
            E_Book automatically tracks your reading activity.

            The system remembers:

            ✓ Last reading position
            ✓ Reading progress percentage
            ✓ Last opened time

            Books you started reading will show:

            • Continue Reading badge
            • Progress bar
            • Reading percentage

            When reopening a book, you continue exactly where you stopped.

            ────────────────────────────
            🗑️ Managing Books
            ────────────────────────────
            The bookshelf supports modern mobile interactions.

            Delete a book by:

            • Swiping left on a book card
            • Tapping Delete

            You can also delete multiple books:

            1. Tap the trash icon to enter selection mode
            2. Select multiple books
            3. Press the ✓ confirm button

            A confirmation dialog will appear before files are permanently removed.

            ────────────────────────────
            🔎 Search
            ────────────────────────────
            The Search page helps you quickly find books.

            Search features include:

            • Search by book title
            • Instant results while typing
            • Recently searched keywords
            • Reading progress shown in results

            Search results update automatically as you type.

            ────────────────────────────
            ✨ Interface Features
            ────────────────────────────
            E_Book includes several interface improvements:

            • Smooth page transitions
            • Import success feedback
            • Animated bookshelf updates
            • Skeleton loading when the library loads
            • Empty library guidance

            These help create a smooth and responsive reading experience.

            ────────────────────────────
            💡 Tips
            ────────────────────────────

            • Recently opened books appear at the top of the library
            • Books with progress display a Continue Reading badge
            • Long-press a book to quickly enter multi-select mode
            • The library automatically refreshes after reading

            ────────────────────────────

            Thank you for using E_Book.

            Enjoy your reading experience!
            """;

            if (!File.Exists(guidePath))
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
            if (IsImporting || IsMultiSelectMode)
                return;

            await AnimateAddBookPressAsync();

            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = BuildPickerTypes(),
                    PickerTitle = "Select a file (TXT/EPUB/PDF/HTML/DOCX/RTF)"
                });

                if (result == null)
                    return;

                var ext = Path.GetExtension(result.FileName)?.ToLowerInvariant() ?? "";
                if (!LibraryService.SupportedExtensions.Contains(ext))
                {
                    await DisplayAlert("Not supported", $"Unsupported file type: {ext}", "OK");
                    _ = SetTransientImportFeedbackAsync(ImportFeedbackState.Failure);
                    return;
                }

                string targetPath = Path.Combine(LibraryService.LibraryPath, result.FileName);

                if (File.Exists(targetPath))
                {
                    await DisplayAlert("Notice", "This file has already been imported!", "OK");
                    _ = SetTransientImportFeedbackAsync(ImportFeedbackState.Failure);
                    return;
                }

                IsImporting = true;
                _importFeedbackState = ImportFeedbackState.Idle;
                RaiseImportUiProperties();

                var importedBook = await SaveFileToLibraryAndCreateBookAsync(result, targetPath);
                if (importedBook == null)
                {
                    _ = SetTransientImportFeedbackAsync(ImportFeedbackState.Failure);
                    return;
                }

                importedBook.IsFreshlyImported = true;
                importedBook.RefreshVisualMeta();

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    InsertImportedBookAtTop(importedBook);

                    if (Books.Count == 1)
                    {
                        _isEmptyIconBreathing = false;

                        if (EmptyStateContainer != null)
                        {
                            EmptyStateContainer.Opacity = 1;
                            EmptyStateContainer.TranslationY = 0;
                        }
                    }

                    EnsureBookListVisibleImmediately();
                    RaiseSummaryProperties();
                });

                await ShowToast("Imported successfully");
                _ = SetTransientImportFeedbackAsync(ImportFeedbackState.Success);

                _refreshOnNextAppear = false;
                _animateListOnNextAppear = false;
            }
            finally
            {
                IsImporting = false;
            }
        }

        private async Task<BookItem?> SaveFileToLibraryAndCreateBookAsync(FileResult file, string targetPath)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var newFileStream = File.Create(targetPath);
                await stream.CopyToAsync(newFileStream);

                return new BookItem
                {
                    FileName = Path.GetFileName(targetPath),
                    FullPath = targetPath,
                    Format = LibraryService.GetFormatTag(targetPath),
                    ReadingProgress = 0,
                    LastOpenedTicks = 0,
                    LastReadPage = 0,
                    TotalPages = 0,
                    TotalReadingSeconds = 0,
                    IsSelected = false
                };
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
                return null;
            }
        }

        private void InsertImportedBookAtTop(BookItem importedBook)
        {
            var existing = Books.FirstOrDefault(x =>
                string.Equals(x.FullPath, importedBook.FullPath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.FileName = importedBook.FileName;
                existing.Format = importedBook.Format;
                existing.ReadingProgress = importedBook.ReadingProgress;
                existing.LastOpenedTicks = importedBook.LastOpenedTicks;
                existing.LastReadPage = importedBook.LastReadPage;
                existing.TotalPages = importedBook.TotalPages;
                existing.TotalReadingSeconds = importedBook.TotalReadingSeconds;
                existing.IsFreshlyImported = true;
                existing.RefreshVisualMeta();

                int existingIndex = Books.IndexOf(existing);
                if (existingIndex > 0)
                    Books.Move(existingIndex, 0);

                RaiseSummaryProperties();
                return;
            }

            Books.Insert(0, importedBook);
            SubscribeItem(importedBook);

            UpdateSelectAllText();
            UpdateConfirmState();
            RaiseSummaryProperties();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        private async Task AnimateAddBookPressAsync()
        {
            if (AddBookTapSurface == null)
                return;

            await AddBookTapSurface.ScaleTo(0.985, 70, Easing.CubicIn);
            await AddBookTapSurface.ScaleTo(1.0, 90, Easing.CubicOut);
        }

        private async void OnBookCardLoaded(object sender, EventArgs e)
        {
            if (sender is not Border border)
                return;

            if (border.BindingContext is not BookItem book)
                return;

            _bookCardMap[book.FullPath] = new WeakReference<Border>(border);

            if (book.IsFreshlyImported)
            {
                border.Opacity = 0;
                border.Scale = 0.97;
                border.TranslationY = 8;

                await Task.WhenAll(
                    border.FadeTo(1, 240, Easing.CubicOut),
                    border.ScaleTo(1.0, 240, Easing.CubicOut),
                    border.TranslateTo(0, 0, 240, Easing.CubicOut)
                );

                book.IsFreshlyImported = false;
            }

            if (!string.IsNullOrWhiteSpace(_pendingHighlightBookPath) &&
                string.Equals(_pendingHighlightBookPath, book.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                await PulseBookCardAsync(border);
                _pendingHighlightBookPath = null;
            }
        }

        private async Task TryHighlightPendingBookAsync()
        {
            if (string.IsNullOrWhiteSpace(_pendingHighlightBookPath))
                return;

            for (int i = 0; i < 8; i++)
            {
                var book = Books.FirstOrDefault(b =>
                    string.Equals(b.FullPath, _pendingHighlightBookPath, StringComparison.OrdinalIgnoreCase));

                if (book != null && TryGetLiveBookCard(book, out var card) && card != null)
                {
                    try
                    {
                        if (BookCollectionView != null)
                            BookCollectionView.ScrollTo(book, position: ScrollToPosition.Start, animate: true);
                    }
                    catch
                    {
                    }

                    await Task.Delay(100);
                    await PulseBookCardAsync(card);
                    _pendingHighlightBookPath = null;
                    return;
                }

                await Task.Delay(90);
            }
        }

        private bool TryGetLiveBookCard(BookItem book, out Border? card)
        {
            card = null;

            if (!_bookCardMap.TryGetValue(book.FullPath, out var weakRef))
                return false;

            if (!weakRef.TryGetTarget(out var candidate))
            {
                _bookCardMap.Remove(book.FullPath);
                return false;
            }

            if (candidate.BindingContext is not BookItem ctxBook ||
                !string.Equals(ctxBook.FullPath, book.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            card = candidate;
            return true;
        }

        private async Task PulseBookCardAsync(Border card)
        {
            try
            {
                await card.ScaleTo(1.018, 110, Easing.CubicOut);
                await card.ScaleTo(1.0, 130, Easing.CubicOut);
            }
            catch
            {
            }
        }

        private async Task AnimateBookRemovalAsync(BookItem book)
        {
            if (!TryGetLiveBookCard(book, out var card) || card == null)
                return;

            try
            {
                await Task.WhenAll(
                    card.FadeTo(0, 180, Easing.CubicIn),
                    card.ScaleTo(0.96, 180, Easing.CubicIn),
                    card.TranslateTo(0, -8, 180, Easing.CubicIn)
                );
            }
            catch
            {
            }
        }

        private async Task AnimateRemoveBooksAsync(List<BookItem> books)
        {
            var tasks = books.Select(AnimateBookRemovalAsync).ToList();
            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }

        private async void OnFileClicked(object sender, EventArgs e)
        {
            if (IsMultiSelectMode || IsImporting) return;

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

                _pendingHighlightBookPath = book.FullPath;
                _refreshOnNextAppear = false;
                _animateListOnNextAppear = false;

                bool restart = book.IsCompleted;
                var route = $"reading?filePath={Uri.EscapeDataString(book.FullPath)}&restart={restart.ToString().ToLowerInvariant()}";
                await Shell.Current.GoToAsync(route);
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (!IsMultiSelectMode || IsImporting) return;

            if (e.Parameter is BookItem book)
            {
                book.IsSelected = !book.IsSelected;
                UpdateConfirmState();
                OnPropertyChanged(nameof(SelectedCountText));
            }
        }

        private void OnItemLongPressed(BookItem? book)
        {
            if (book == null || IsImporting) return;

            if (!IsMultiSelectMode)
                EnterMultiSelectMode();

            if (!book.IsSelected)
                book.IsSelected = true;

            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        private async void OnTrashTapped(object sender, EventArgs e)
        {
            if (IsImporting)
                return;

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
            if (IsImporting || IsMultiSelectMode)
                return;

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
            if (!IsMultiSelectMode || Books.Count == 0 || IsImporting) return;

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
            if (IsImporting) return;
            ExitMultiSelectMode();
        }

        private async void OnConfirmTapped(object sender, EventArgs e)
        {
            if (!IsMultiSelectMode || IsImporting) return;

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
                ConfirmButton.BackgroundColor = Color.FromArgb("#8FD8A2");
                return;
            }

            bool hasSelected = Books.Any(b => b.IsSelected);
            ConfirmButton.Opacity = hasSelected ? 1.0 : 0.42;
            ConfirmButton.InputTransparent = !hasSelected;
            ConfirmButton.Scale = hasSelected ? 1.0 : 0.97;
            ConfirmButton.BackgroundColor = hasSelected
                ? Color.FromArgb("#8FD8A2")
                : Color.FromArgb("#BED7C5");
        }

        private async void OpenDeleteDialog(List<BookItem> items, bool single)
        {
            _pendingDeleteItems = items;

            if (single)
            {
                var name = items[0].DisplayFileName;
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

            await AnimateRemoveBooksAsync(toDelete);

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

            RaiseSummaryProperties();
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
                _bookCardMap.Remove(book.FullPath);

                RaiseSummaryProperties();

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
                    ? (Books.Any(b => b.IsSelected) ? 1.0 : 0.42)
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
                    double targetOpacity = Books.Any(b => b.IsSelected) ? 1.0 : 0.42;
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
            MainThread.BeginInvokeOnMainThread(async () =>
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
                    _bookCardMap.Remove(book.FullPath);

                    UpdateSelectAllText();
                    UpdateConfirmState();
                    RaiseSummaryProperties();
                    OnPropertyChanged(nameof(SelectedCountText));
                    return;
                }

                book.ReadingProgress = e.Progress;
                book.LastOpenedTicks = e.LastOpenedTicks;
                book.LastReadPage = e.LastReadPage;
                book.TotalPages = e.TotalPages;
                book.TotalReadingSeconds = e.TotalReadingSeconds;
                book.RefreshVisualMeta();

                int oldIndex = Books.IndexOf(book);
                if (oldIndex > 0)
                    Books.Move(oldIndex, 0);

                _pendingHighlightBookPath = book.FullPath;

                try
                {
                    if (BookCollectionView != null)
                        BookCollectionView.ScrollTo(book, position: ScrollToPosition.Start, animate: true);
                }
                catch
                {
                }

                RaiseSummaryProperties();
                await Task.Delay(120);
                await TryHighlightPendingBookAsync();
            });
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}