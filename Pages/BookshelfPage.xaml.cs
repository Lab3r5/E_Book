using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using E_Book.Models;
using E_Book.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace E_Book.Pages
{
    public partial class BookshelfPage : ContentPage, INotifyPropertyChanged
    {
        #region Text Constants

        private const string TextCancel = "Cancel";
        private const string TextSelectAll = "Select All";
        private const string TextUnselectAll = "Unselect All";
        private const string TextSelectBooks = "Select books";
        private const string TextSelectedSuffix = "selected";

        private const string TextImportedSuccessfully = "Imported successfully";
        private const string TextDeletedSuccessfully = "Deleted successfully";
        private const string TextNoBooksToDelete = "No books to delete";

        private const string TextImporting = "Importing...";
        private const string TextImported = "Imported";
        private const string TextImportFailed = "Import failed";
        private const string TextAddBook = "Add Book";

        private const string TextImportWaitMessage = "Please wait while we add your file";
        private const string TextImportReadyMessage = "Your book is ready in the library";
        private const string TextImportFailMessage = "Please check the file and try again";

        private const string TextImportInProgress = "Import in progress...";
        private const string TextImportSuccessHelper = "Book added successfully.";
        private const string TextImportFailHelper = "Import failed. Supports TXT, EPUB, PDF, HTML, DOCX, RTF, and image files.";
        private const string TextImportSupportHelper = "Supports TXT, EPUB, PDF, HTML, DOCX, RTF, and image files.";

        private const string TextEmptyImportWait = "Please keep this page open while your book is being imported.";
        private const string TextEmptyImportSuccess = "Nice — your first book has been added.";
        private const string TextEmptyImportFailed = "Import failed. Try another supported file.";
        private const string TextEmptyImportDefault = "Use the Add Book card below to import your first file.";

        private const string TextUnsupportedTypeTitle = "Not supported";
        private const string TextNoticeTitle = "Notice";
        private const string TextErrorTitle = "Error";
        private const string TextOk = "OK";

        #endregion

        #region Enum

        private enum ImportFeedbackState
        {
            Idle,
            Success,
            Failure
        }

        #endregion

        #region Collections / Commands

        public ObservableCollection<BookItem> Books { get; } = new();

        public ICommand LongPressCommand { get; }

        #endregion

        #region State Fields

        private bool _isMultiSelectMode;
        private bool _isLoadingBooks;
        private bool _isImporting;

        private bool _isHeaderAnimating;
        private bool _isSelectionCountPulsing;
        private bool _hasPlayedEntrance;
        private bool _isEmptyIconBreathing;
        private bool _hasLoadedOnce;
        private bool _aggregateMetricsDirty = true;
        private int _selectedCountCached;
        private int _inProgressCountCached;
        private int _completedCountCached;
        private long _totalReadingSecondsCached;
        private bool _showReadingSummaryCached;

        private bool _refreshOnNextAppear = true;
        private bool _animateListOnNextAppear = true;

        private ImportFeedbackState _importFeedbackState = ImportFeedbackState.Idle;
        private int _importFeedbackVersion;
        private string? _pendingHighlightBookPath;

        private List<BookItem> _pendingDeleteItems = new();
        private readonly HashSet<BookItem> _subscribedItems = new();
        private readonly Dictionary<string, WeakReference<Border>> _bookCardMap =
            new(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Bindable Properties

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
                EnsureAggregateMetrics();
                return _selectedCountCached == 0 ? TextSelectBooks : $"{_selectedCountCached} {TextSelectedSuffix}";
            }
        }

        public string AddBookButtonTitle
        {
            get
            {
                if (IsImporting) return TextImporting;

                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => TextImported,
                    ImportFeedbackState.Failure => TextImportFailed,
                    _ => TextAddBook
                };
            }
        }

        public string AddBookButtonSubtitle
        {
            get
            {
                if (IsImporting) return TextImportWaitMessage;

                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => TextImportReadyMessage,
                    ImportFeedbackState.Failure => TextImportFailMessage,
                    _ => "Import a new file to your library"
                };
            }
        }

        public string AddBookHelperText
        {
            get
            {
                if (IsImporting) return TextImportInProgress;

                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => TextImportSuccessHelper,
                    ImportFeedbackState.Failure => TextImportFailHelper,
                    _ => TextImportSupportHelper
                };
            }
        }

        public double AddBookArrowOpacity => IsImporting ? 0.35 : 1.0;

        public bool ShowAddBookPlus => !IsImporting;

        public string EmptyStatePrimaryButtonText => IsImporting ? TextImporting : "Add Your First Book";

        public string EmptyStateSecondaryHint
        {
            get
            {
                if (IsImporting) return TextEmptyImportWait;

                return _importFeedbackState switch
                {
                    ImportFeedbackState.Success => TextEmptyImportSuccess,
                    ImportFeedbackState.Failure => TextEmptyImportFailed,
                    _ => TextEmptyImportDefault
                };
            }
        }

        public string InProgressCountText => GetInProgressCountText();

        public string CompletedCountText => GetCompletedCountText();

        public string TotalReadingTimeText
        {
            get
            {
                EnsureAggregateMetrics();
                var ts = TimeSpan.FromSeconds(_totalReadingSecondsCached);

                if (ts.TotalHours >= 1)
                    return $"{(int)ts.TotalHours}h {ts.Minutes}m";

                if (ts.TotalMinutes >= 1)
                    return $"{Math.Max(1, (int)ts.TotalMinutes)}m";

                return "0m";
            }
        }

        public bool ShowBottomAddBookArea => !IsMultiSelectMode;

        public bool ShowReadingSummary
        {
            get
            {
                EnsureAggregateMetrics();
                return _showReadingSummaryCached;
            }
        }

        #endregion

        #region Constructor / Lifecycle

        public BookshelfPage()
        {
            InitializeComponent();

            BindingContext = this;

            LongPressCommand = new Command<BookItem>(OnItemLongPressed);

            EnsureLibraryExists();
            ReadingMetaStore.MetaChanged += OnReadingMetaChanged;
            UserSession.SessionChanged += OnUserSessionChanged;

            UpdateSelectAllText();
            UpdateConfirmState();
            RaiseSummaryProperties();
            RaiseCommonUiProperties();
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

            await HandlePageEntranceAsync();

            if (shouldRefresh)
            {
                await RefreshBooksAsync(showLoadingPlaceholder: firstLoad);
                _refreshOnNextAppear = false;
            }

            _hasLoadedOnce = true;

            if (shouldAnimateList)
            {
                await HandleListAppearanceAsync();
                _animateListOnNextAppear = false;
            }
            else
            {
                EnsureBookListVisibleImmediately();

                if (EmptyStateContainer != null)
                {
                    EmptyStateContainer.Opacity = 1;
                    EmptyStateContainer.TranslationY = 0;
                }
            }

            await TryHighlightPendingBookAsync();
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            if (Handler == null)
            {
                ReadingMetaStore.MetaChanged -= OnReadingMetaChanged;
                UserSession.SessionChanged -= OnUserSessionChanged;

                foreach (var item in _subscribedItems.ToList())
                    UnsubscribeItem(item);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isEmptyIconBreathing = false;
        }

        #endregion

        #region Page Entrance

        private async Task HandlePageEntranceAsync()
        {
            if (!_hasPlayedEntrance)
            {
                _hasPlayedEntrance = true;
                await PlayEntranceAnimationAsync();
                return;
            }

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

        private void OnUserSessionChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    ReadingMetaStore.ResetCache();
                    EnsureLibraryExists();

                    _refreshOnNextAppear = true;
                    _animateListOnNextAppear = false;

                    if (Window != null)
                        await RefreshBooksAsync(showLoadingPlaceholder: true);
                }
                catch
                {
                }
            });
        }
        
        private async Task HandleListAppearanceAsync()
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
        }

        private async Task PlayEntranceAnimationAsync()
        {
            if (RootHost != null)
                RootHost.Opacity = 1;

            await UIAnimationService.FadeSlideInAsync(HeaderSection, 10, 200);
            await UIAnimationService.FadeScaleCardInAsync(MainCard, 18, 0.995, 250, 20);
        }

        private void EnsureBookListVisibleImmediately()
        {
            if (BookCollectionView == null) return;

            BookCollectionView.Opacity = 1;
            BookCollectionView.TranslationY = 0;
            BookCollectionView.IsVisible = !IsLoadingBooks;
        }

        #endregion

        #region UI Property Refresh

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

        private void EnsureAggregateMetrics()
        {
            if (!_aggregateMetricsDirty)
                return;

            int selectedCount = 0;
            int inProgressCount = 0;
            int completedCount = 0;
            long totalReadingSeconds = 0;

            foreach (var book in Books)
            {
                if (book.IsSelected)
                    selectedCount++;

                if (book.ReadingProgress >= 0.999)
                    completedCount++;
                else if (book.ReadingProgress > 0)
                    inProgressCount++;

                totalReadingSeconds += Math.Max(0, book.TotalReadingSeconds);
            }

            _selectedCountCached = selectedCount;
            _inProgressCountCached = inProgressCount;
            _completedCountCached = completedCount;
            _totalReadingSecondsCached = totalReadingSeconds;
            _showReadingSummaryCached = Books.Count > 0;
            _aggregateMetricsDirty = false;
        }

        private void MarkAggregateMetricsDirty()
        {
            _aggregateMetricsDirty = true;
        }

        private string GetInProgressCountText()
        {
            EnsureAggregateMetrics();
            return _inProgressCountCached.ToString();
        }

        private string GetCompletedCountText()
        {
            EnsureAggregateMetrics();
            return _completedCountCached.ToString();
        }

        private void RaiseSummaryProperties()
        {
            MarkAggregateMetricsDirty();
            OnPropertyChanged(nameof(InProgressCountText));
            OnPropertyChanged(nameof(CompletedCountText));
            OnPropertyChanged(nameof(TotalReadingTimeText));
        }

        private void RaiseCommonUiProperties()
        {
            MarkAggregateMetricsDirty();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(ShowBottomAddBookArea));
            OnPropertyChanged(nameof(ShowReadingSummary));
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

        #endregion

        #region Library / Guide

        private void EnsureLibraryExists()
        {
            LibraryService.EnsureLibraryExists();

            string guidePath = Path.Combine(LibraryService.LibraryPath, "Usage Guidelines.txt");

            string guideContent = """
==================================================
                📘 E_Book User Guide
==================================================

Welcome to E_Book.

E_Book is a modern cross-platform reading application
built with .NET MAUI, designed to deliver a clean,
intuitive, and immersive reading experience.

This guide introduces the main features available in
version 1.10 and explains how to use the application
effectively.


--------------------------------------------------
Chapter 1 · 👤 Account System
--------------------------------------------------

E_Book supports a complete user account system.

You can:

• 🆕 Create a new account (Sign Up)
• 🔐 Log in using email and password
• 👥 Use Guest mode for quick access

During registration, you will:

• Enter your display name
• Provide an email address
• Set a password
• Select a security question
• Provide an answer for recovery

✅ Credentials are stored securely on your device.


--------------------------------------------------
Chapter 2 · 🔑 Forgot Password
--------------------------------------------------

If you forget your password:

① Tap "Forgot Password"
② Enter your registered email
③ Answer your security question
④ Create a new password

⚠️ Password reset requires correct answer verification.


--------------------------------------------------
Chapter 3 · 📚 My Library
--------------------------------------------------

After login, you will enter:

📚 My Library

Your personal digital bookshelf.

Features:

• View imported books
• Continue reading from last position
• Display reading progress
• Show last opened time
• Track reading duration

💡 If library is empty:
Tap "Add Book" to import files.


--------------------------------------------------
Chapter 4 · 📄 Supported File Formats
--------------------------------------------------

E_Book supports multiple formats:

TXT
EPUB
PDF
HTML
DOCX
RTF
JPG

Reader behavior:

📖 Text-based → Reader Mode
📑 PDF → PDF Viewer
🖼 Images → Image Viewer


--------------------------------------------------
Chapter 5 · ➕ Importing Books
--------------------------------------------------

To import a file:

① Tap "Add Book"
② Select supported format
③ Wait for processing

Notes:

• Duplicate files are skipped
• Unsupported files are blocked
• Import progress indicator is shown

After import, books appear in My Library.


--------------------------------------------------
Chapter 6 · 📖 Opening a Book
--------------------------------------------------

To start reading:

① Tap a book card
② Reader opens automatically

Reading session remembers:

• Last opened page
• Reading progress
• Reading duration
• Last opened timestamp

Resume reading continues from last position.


--------------------------------------------------
Chapter 7 · 📝 Text Reader
--------------------------------------------------

TXT / EPUB / DOCX / RTF open in Reader Mode.

Supported gestures:

⬅ Swipe left → next page
➡ Swipe right → previous page
👆 Tap → open reading menu

Reader automatically paginates content
based on screen size and typography.


--------------------------------------------------
Chapter 8 · 📑 PDF Reader
--------------------------------------------------

PDF files open in PDF Reader.

Features:

• True page count
• Page navigation
• Resume last page
• Reading progress tracking

Progress is saved automatically.


--------------------------------------------------
Chapter 9 · 🖼 Image Viewer
--------------------------------------------------

Image files open in Image Viewer.

Gestures:

👆 Double tap → quick zoom In/Out
➡ Swipe → next image

Images in same directory are grouped
into a gallery experience.


--------------------------------------------------
Chapter 10 · 🎨 Reader Appearance
--------------------------------------------------

Customize reading style:

• Font size
• Line spacing
• Theme color

Themes:

☀ Light
📜 Beige
🌿 Green
💙 Blue
🌙 Dark

Settings are saved per book.


--------------------------------------------------
Chapter 11 · 📊 Reading Progress
--------------------------------------------------

E_Book tracks reading activity:

• Current page
• Total pages
• Progress percentage
• Reading duration
• Last opened time

Books with progress display:

📖 Continue reading


--------------------------------------------------
Chapter 12 · 📈 Progress Indicator
--------------------------------------------------

Progress indicators include:

• Page position
• Completion percentage
• Reading status

Status examples:

🆕 New
📖 Continue reading
✅ Completed


--------------------------------------------------
Chapter 13 · 📑 Table of Contents
--------------------------------------------------

Supported formats provide navigation:

• EPUB table of contents
• Detected document headings
• Chapter-based navigation

Selecting a chapter jumps directly
to the relevant reading position.


--------------------------------------------------
Chapter 14 · 🔍 Search
--------------------------------------------------

Use Search tab to locate books quickly.

Search supports:

• File name
• Keywords
• Partial matching

Recent searches:

• Stored automatically
• Up to 6 records
• Individual removal supported
• Clear history available


--------------------------------------------------
Chapter 15 · 🔔 Notifications
--------------------------------------------------

E_Book supports reading reminders.

Manage in:

Settings → Notifications

Options include:

• Reading reminders
• Continue reading alerts
• Library notifications


--------------------------------------------------
Chapter 16 · ⚙ Settings
--------------------------------------------------

Customize application behavior:

• Appearance
• Notifications
• Account settings
• Help center
• Privacy policy

Changes apply immediately.


--------------------------------------------------
Chapter 17 · 🗂 Library Management
--------------------------------------------------

Manage books:

① Enter selection mode
② Select books
③ Confirm deletion

Deleted books are permanently removed.


--------------------------------------------------
Chapter 18 · ⚡ Performance
--------------------------------------------------

Reader engine supports:

• Dynamic pagination
• Content caching
• Smooth page transitions
• Stable reading performance

Large documents may require
additional loading time.


--------------------------------------------------
Chapter 19 · 💡 Tips
--------------------------------------------------

For best experience:

• Organize library regularly
• Adjust font size if needed
• Use search for large collections
• Enable reminders for consistency
• Keep application updated

Enjoy a focused reading experience.


--------------------------------------------------
Final Message
--------------------------------------------------

E_Book is designed to provide
a modern digital reading experience.

Build your personal library and
read anytime, anywhere.

✨ Happy Reading ✨

--------------------------------------------------
                    E_Book
--------------------------------------------------
""";

            if (!File.Exists(guidePath))
                File.WriteAllText(guidePath, guideContent);
        }

        #endregion

        #region Refresh / Load Books

        private async Task RefreshBooksAsync(bool showLoadingPlaceholder = false)
        {
            EnsureLibraryExists();
            ReadingMetaStore.ResetCache();

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

            var latestBooks = await Task.Run(() =>
            {
                var loaded = LibraryService.LoadBooks();
                var metaMap = ReadingMetaStore.LoadSnapshotMap();

                foreach (var book in loaded)
                {
                    book.IsSelected = false;

                    string key = book.FullPath.Trim().ToLowerInvariant();
                    if (metaMap.TryGetValue(key, out var meta))
                    {
                        book.ReadingProgress = meta.Progress;
                        book.LastOpenedTicks = meta.LastOpenedTicks;
                        book.LastReadPage = meta.LastReadPage;
                        book.TotalPages = meta.TotalPages;
                        book.TotalReadingSeconds = meta.TotalReadingSeconds;
                    }
                    else
                    {
                        book.ReadingProgress = 0;
                        book.LastOpenedTicks = 0;
                        book.LastReadPage = 0;
                        book.TotalPages = 0;
                        book.TotalReadingSeconds = 0;
                    }

                    book.RefreshVisualMeta();
                }

                return loaded
                    .OrderByDescending(b => b.LastOpenedTicks > 0)
                    .ThenByDescending(b => b.LastOpenedTicks)
                    .ThenBy(b => b.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SyncBooksCollection(latestBooks);

                if (Books.Count == 0 && IsMultiSelectMode)
                    ExitMultiSelectMode();

                UpdateSelectAllText();
                UpdateConfirmState();
                RaiseSummaryProperties();
                RaiseCommonUiProperties();
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
                    UpdateBookItem(existing, incoming, resetSelection: true);

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

        private static void UpdateBookItem(BookItem target, BookItem source, bool resetSelection)
        {
            target.FileName = source.FileName;
            target.Format = source.Format;
            target.ReadingProgress = source.ReadingProgress;
            target.LastOpenedTicks = source.LastOpenedTicks;
            target.LastReadPage = source.LastReadPage;
            target.TotalPages = source.TotalPages;
            target.TotalReadingSeconds = source.TotalReadingSeconds;

            if (resetSelection)
                target.IsSelected = false;

            target.RefreshVisualMeta();
        }

        #endregion

        #region Item Subscription

        private void SubscribeItem(BookItem item)
        {
            if (item == null || _subscribedItems.Contains(item))
                return;

            item.PropertyChanged += OnBookItemPropertyChanged;
            _subscribedItems.Add(item);
        }

        private void UnsubscribeItem(BookItem item)
        {
            if (item == null || !_subscribedItems.Contains(item))
                return;

            item.PropertyChanged -= OnBookItemPropertyChanged;
            _subscribedItems.Remove(item);
        }

        private void OnBookItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BookItem.IsSelected))
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                MarkAggregateMetricsDirty();
                UpdateSelectAllText();
                UpdateConfirmState();
                OnPropertyChanged(nameof(SelectedCountText));

                if (IsMultiSelectMode)
                    _ = PulseSelectionCountAsync();
            });
        }

        #endregion

        #region File Import

        private async Task NotifyImportSuccessAsync(string fileName)
        {
            try
            {
                bool notificationsEnabled = Preferences.Get(NotificationPrefs.NotificationsEnabled, true);
                bool importEnabled = Preferences.Get(NotificationPrefs.ImportAlertEnabled, true);

                if (!notificationsEnabled || !importEnabled)
                    return;

                var notificationService = Application.Current?.Handler?.MauiContext?.Services.GetService<INotificationService>();
                if (notificationService == null)
                    return;

                await notificationService.ShowNowAsync(
                    "Book imported successfully",
                    $"\"{fileName}\" has been added to your library.");
            }
            catch
            {
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
                "image/jpeg",
                "image/png",
                "image/webp",
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
                "public.rtf",
                "public.jpeg",
                "public.png",
                "org.webmproject.webp"
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
                    PickerTitle = "Select a file (TXT/EPUB/PDF/HTML/DOCX/RTF/Image)"
                });

                if (result == null)
                    return;

                string ext = Path.GetExtension(result.FileName)?.ToLowerInvariant() ?? "";

                if (!LibraryService.SupportedExtensions.Contains(ext))
                {
                    await DisplayAlert(TextUnsupportedTypeTitle, $"Unsupported file type: {ext}", TextOk);
                    _ = SetTransientImportFeedbackAsync(ImportFeedbackState.Failure);
                    return;
                }

                string targetPath = Path.Combine(LibraryService.LibraryPath, result.FileName);

                if (File.Exists(targetPath))
                {
                    await DisplayAlert(TextNoticeTitle, "This file has already been imported!", TextOk);
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

                await ShowToast(TextImportedSuccessfully);
                await NotifyImportSuccessAsync(importedBook.FileName);
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
                using var sourceStream = await file.OpenReadAsync();
                using var targetStream = File.Create(targetPath);
                await sourceStream.CopyToAsync(targetStream);

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
                await DisplayAlert(TextErrorTitle, $"Failed to save file: {ex.Message}", TextOk);
                return null;
            }
        }

        private void InsertImportedBookAtTop(BookItem importedBook)
        {
            var existing = Books.FirstOrDefault(x =>
                string.Equals(x.FullPath, importedBook.FullPath, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                UpdateBookItem(existing, importedBook, resetSelection: false);
                existing.IsFreshlyImported = true;

                int index = Books.IndexOf(existing);
                if (index > 0)
                    Books.Move(index, 0);

                RaiseSummaryProperties();
                RaiseCommonUiProperties();
                return;
            }

            Books.Insert(0, importedBook);
            SubscribeItem(importedBook);

            UpdateSelectAllText();
            UpdateConfirmState();
            RaiseSummaryProperties();
            RaiseCommonUiProperties();
        }

        private async Task AnimateAddBookPressAsync()
        {
            if (AddBookTapSurface == null)
                return;

            await UIAnimationService.PressAsync(AddBookTapSurface, 0.985, 0.98, 70, 110);
        }

        #endregion

        #region Card Mapping / Highlight

        private async void OnBookCardLoaded(object sender, EventArgs e)
        {
            if (sender is not Border border) return;
            if (border.BindingContext is not BookItem book) return;

            _bookCardMap[book.FullPath] = new WeakReference<Border>(border);

            if (book.IsFreshlyImported)
            {
                border.Opacity = 0;
                border.Scale = 0.985;
                border.TranslationY = 10;

                await Task.WhenAll(
                    border.FadeTo(1, 220, Easing.CubicOut),
                    border.ScaleTo(1.0, 220, Easing.CubicOut),
                    border.TranslateTo(0, 0, 220, Easing.CubicOut)
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
                        BookCollectionView?.ScrollTo(book, position: ScrollToPosition.Start, animate: true);
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
                await UIAnimationService.PopAsync(card);
            }
            catch
            {
            }
        }

        #endregion

        #region Book Click / Tap / Long Press

        private async void OnFileClicked(object sender, EventArgs e)
        {
            if (IsMultiSelectMode || IsImporting)
                return;

            if (sender is not Button button || button.CommandParameter is not BookItem book)
                return;

            if (!File.Exists(book.FullPath))
            {
                await DisplayAlert(TextErrorTitle, "File not found!", TextOk);
                _refreshOnNextAppear = true;
                _animateListOnNextAppear = false;
                await RefreshBooksAsync();
                return;
            }

            _pendingHighlightBookPath = book.FullPath;
            _refreshOnNextAppear = false;
            _animateListOnNextAppear = false;

            ReadingMetaStore.UpdateLastOpened(book.FullPath);

            if (FileTypeHelper.IsImage(book.FullPath))
            {
                string route = $"{AppShell.RouteImageReader}?filePath={Uri.EscapeDataString(book.FullPath)}";
                await Shell.Current.GoToAsync(route);
                return;
            }

            if (book.IsPdf)
            {
                string pdfRoute =
                    $"{AppShell.RoutePdfReader}?filePath={Uri.EscapeDataString(book.FullPath)}";

                await Shell.Current.GoToAsync(pdfRoute);
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

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (!IsMultiSelectMode || IsImporting)
                return;

            if (e.Parameter is not BookItem book)
                return;

            ToggleBookSelection(book);
        }

        private void OnItemLongPressed(BookItem? book)
        {
            if (book == null || IsImporting)
                return;

            if (!IsMultiSelectMode)
                EnterMultiSelectMode();

            if (!book.IsSelected)
                book.IsSelected = true;

            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        private void OnSelectionBoxTapped(object sender, TappedEventArgs e)
        {
            if (!IsMultiSelectMode || IsImporting)
                return;

            if (e.Parameter is not BookItem book)
                return;

            ToggleBookSelection(book);
        }

        private void OnBookCardTapped(object sender, TappedEventArgs e)
        {
            if (IsImporting || !IsMultiSelectMode)
                return;

            if (e.Parameter is not BookItem book)
                return;

            ToggleBookSelection(book);
        }

        private void ToggleBookSelection(BookItem book)
        {
            book.IsSelected = !book.IsSelected;
            UpdateConfirmState();
            UpdateSelectAllText();
            OnPropertyChanged(nameof(SelectedCountText));
        }

        #endregion

        #region Multi Select

        private void EnterMultiSelectMode()
        {
            IsMultiSelectMode = true;

            foreach (var book in Books)
                book.IsSelected = false;

            UpdateSelectAllText();
            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(ShowBottomAddBookArea));

            _ = PlayMultiSelectEnterAnimationAsync();
        }

        private void ExitMultiSelectMode()
        {
            IsMultiSelectMode = false;

            foreach (var book in Books)
                book.IsSelected = false;

            UpdateSelectAllText();
            UpdateConfirmState();
            OnPropertyChanged(nameof(SelectedCountText));
            OnPropertyChanged(nameof(ShowBottomAddBookArea));

            ResetHeaderAnimationState();
        }

        private void OnSelectAllClicked(object? sender, EventArgs e)
        {
            if (!IsMultiSelectMode || Books.Count == 0 || IsImporting)
                return;

            bool allSelected = Books.All(b => b.IsSelected);

            foreach (var book in Books)
                book.IsSelected = !allSelected;

            UpdateSelectAllText();
            UpdateConfirmState();
            RaiseCommonUiProperties();
        }

        private void OnCancelMultiSelectClicked(object? sender, EventArgs e)
        {
            if (IsImporting)
                return;

            ExitMultiSelectMode();
        }

        private async void OnConfirmTapped(object sender, EventArgs e)
        {
            if (!IsMultiSelectMode || IsImporting)
                return;

            var selected = Books.Where(b => b.IsSelected).ToList();
            if (selected.Count == 0)
                return;

            await AnimatePress(ConfirmButton);
            OpenDeleteDialog(selected, single: false);
        }

        private void UpdateSelectAllText()
        {
            if (SelectAllButton == null)
                return;

            if (!IsMultiSelectMode)
            {
                SelectAllButton.Text = TextSelectAll;
                return;
            }

            bool allSelected = Books.Count > 0 && Books.All(b => b.IsSelected);
            SelectAllButton.Text = allSelected ? TextUnselectAll : TextSelectAll;
        }

        private void UpdateConfirmState()
        {
            if (ConfirmButton == null)
                return;

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

        #endregion

        #region Delete

        private async void OnTrashTapped(object sender, EventArgs e)
        {
            if (IsImporting)
                return;

            await AnimatePress(TrashButton);

            if (Books.Count == 0)
            {
                await ShowToast(TextNoBooksToDelete);
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
                OpenDeleteDialog(new List<BookItem> { book }, single: true);
        }

        private void OpenDeleteDialog(List<BookItem> items, bool single)
        {
            _pendingDeleteItems = items;

            if (single)
            {
                string name = items[0].DisplayFileName;
                DeleteTitle.Text = "Delete this book?";
                DeleteMessage.Text = $"Delete \"{name}\"? This action cannot be undone.";
            }
            else
            {
                DeleteTitle.Text = "Delete selected books?";
                DeleteMessage.Text = $"You are about to delete {items.Count} file(s). This action cannot be undone.";
            }

            _ = ShowDeleteDialog();
        }

        private async Task ShowDeleteDialog()
        {
            if (DeleteDialog == null || DeleteOverlay == null)
                return;

            DeleteOverlay.IsVisible = true;
            DeleteOverlay.Opacity = 0;

            DeleteDialog.Opacity = 0;
            DeleteDialog.Scale = 0.96;
            DeleteDialog.TranslationX = 0;
            DeleteDialog.TranslationY = 24;

            await Task.WhenAll(
                DeleteOverlay.FadeTo(1, 160, Easing.CubicOut),
                DeleteDialog.FadeTo(1, 190, Easing.CubicOut),
                DeleteDialog.ScaleTo(1, 220, Easing.SpringOut),
                DeleteDialog.TranslateTo(0, 0, 220, Easing.CubicOut)
            );
        }

        private async Task HideDeleteDialog()
        {
            if (DeleteDialog == null || DeleteOverlay == null)
                return;

            await Task.WhenAll(
                DeleteOverlay.FadeTo(0, 140, Easing.CubicIn),
                DeleteDialog.FadeTo(0, 140, Easing.CubicIn),
                DeleteDialog.ScaleTo(0.96, 140, Easing.CubicIn),
                DeleteDialog.TranslateTo(0, 20, 140, Easing.CubicIn)
            );

            DeleteOverlay.IsVisible = false;
            DeleteOverlay.Opacity = 1;
            DeleteDialog.Opacity = 0;
            DeleteDialog.Scale = 1;
            DeleteDialog.TranslationX = 0;
            DeleteDialog.TranslationY = 0;
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

            RaiseSummaryProperties();
            RaiseCommonUiProperties();

            if (Books.Count < 30)
                await AnimateBookListAppearance();
            else
                EnsureBookListVisibleImmediately();

            await ShowToast(TextDeletedSuccessfully);

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
                RaiseCommonUiProperties();

                if (reloadAfter)
                    await RefreshBooksAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert(TextErrorTitle, $"Failed to delete file: {ex.Message}", TextOk);
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

        #endregion

        #region Header Animation

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
            if (_isHeaderAnimating || !IsMultiSelectMode)
                return;

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
            if (_isSelectionCountPulsing || MultiSelectTitleChip == null || !IsMultiSelectMode)
                return;

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

        #endregion

        #region Loading / Empty State / Toast / Press

        private async Task StartSkeletonShimmer()
        {
            if (SkeletonShimmer == null)
                return;

            SkeletonShimmer.TranslationX = -320;

            int loopCount = 0;
            while (IsLoadingBooks && loopCount < 4)
            {
                await SkeletonShimmer.TranslateTo(320, 0, 850, Easing.Linear);
                SkeletonShimmer.TranslationX = -320;
                loopCount++;
            }

            SkeletonShimmer.TranslationX = -320;
        }

        private async Task AnimateBookListAppearance()
        {
            if (BookCollectionView == null)
                return;

            await UIAnimationService.FadeListInAsync(BookCollectionView, 18, 220, 10);
        }

        private async Task AnimateEmptyState()
        {
            if (EmptyStateContainer == null)
                return;

            await UIAnimationService.FadeListInAsync(EmptyStateContainer, 20, 240, 10);
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

                    if (!_isEmptyIconBreathing || Books.Count != 0)
                        break;

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

        private async Task AnimatePress(VisualElement view)
        {
            if (view == null)
                return;

            await UIAnimationService.PressAsync(view, 0.94, 0.96, 70, 110);
        }

        private async Task ShowToast(string message)
        {
            if (ToastFrame == null || ToastLabel == null)
                return;

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

        #endregion

        #region Reading Meta Sync

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
                    _bookCardMap.Remove(book.FullPath);

                    UpdateSelectAllText();
                    UpdateConfirmState();
                    RaiseSummaryProperties();
                    RaiseCommonUiProperties();
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

                RaiseSummaryProperties();
                RaiseCommonUiProperties();
            });
        }

        #endregion

        #region Button Press Animations

        private async void OnDeleteButtonPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
                await v.ScaleTo(0.96, 70);
        }

        private async void OnDeleteButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement v)
                await v.ScaleTo(1, 120, Easing.SpringOut);
        }

        private async void OnDialogButtonPressed(object sender, EventArgs e)
        {
            if (sender is VisualElement view)
                await view.ScaleTo(0.96, 70);
        }

        private async void OnDialogButtonReleased(object sender, EventArgs e)
        {
            if (sender is VisualElement view)
                await view.ScaleTo(1, 120, Easing.SpringOut);
        }

        #endregion

        #region INotifyPropertyChanged

        public new event PropertyChangedEventHandler? PropertyChanged;

        private new void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}

