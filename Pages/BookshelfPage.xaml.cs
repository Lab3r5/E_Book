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

        public ICommand LongPressCommand { get; }

        private List<BookItem> _pendingDeleteItems = new();
        private readonly List<BookItem> _subscribedItems = new();

        public BookshelfPage()
        {
            InitializeComponent();

            LongPressCommand = new Command<BookItem>(OnItemLongPressed);

            EnsureLibraryExists();

            BindingContext = this;
            LoadSavedFiles();

            UpdateSelectAllText();
            UpdateConfirmState();
        }

        private void EnsureLibraryExists()
        {
            LibraryService.EnsureLibraryExists();

            string guidePath = Path.Combine(LibraryService.LibraryPath, "Usage Guidelines.txt");

            if (!File.Exists(guidePath))
            {
                // ✅ 你原来的 Usage Guidelines 内容（保留）
                string guideContent = """
                Welcome to E_Book 📘

                E_Book is a lightweight and multi-format reading application built with .NET MAUI.
                It helps you organize, read, and customize your documents with a clean,
                distraction-free reading experience across different file types.

                ────────────────────────────
                📂 Bookshelf & File Management:
                ────────────────────────────
                ➕ Tap the plus button to import files into your bookshelf  
                📖 Tap the book icon next to a file to start reading  
                🗑️ Tap the trash icon to remove unwanted files from the bookshelf  
                
                Supported formats:
                • TXT  • EPUB  • PDF
                • HTML/HTM  • DOCX  • RTF

                Imported files are copied into the app’s local Library folder and managed
                automatically by the application to ensure stable access and permissions.

                ────────────────────────────
                🛠️ Reading Features:
                ────────────────────────────
                • Swipe left or right to navigate pages or chapters
                • Tap the center of the screen to show or hide reading tools  
                • Tap the ❮ back button to return to the bookshelf  
                • Tap the Aa button to customize your reading experience:
                   - Adjust font size (Small / Medium / Large)
                   - Switch between Light and Dark reading themes

                All changes are applied instantly to improve reading comfort.

                ────────────────────────────
                🔐 Settings (from ⚙️ page):
                ────────────────────────────
                From the Settings page, you can:
                • Enable password protection when launching the app  
                • Automatically lock the app after exiting (password required)  
                • Keep the screen on while reading to avoid interruptions
                
                These options help protect your privacy and enhance usability.

                ────────────────────────────
                📌 Reading Tips:
                ────────────────────────────
                ✓ Reading progress is saved automatically for each document  
                ✓ You will continue reading from where you last stopped  
                ✓ Adjust font size and theme to reduce eye strain  
                ✓ All data is stored locally on your device for privacy and performance
                
                ────────────────────────────
                Thank you for using E_Book!
                Enjoy a simple, flexible, and focused reading experience.
                """;

                File.WriteAllText(guidePath, guideContent);
            }
        }

        private void LoadSavedFiles()
        {
            UnsubscribeAll();

            var list = LibraryService.LoadBooks();

            Books.Clear();
            foreach (var b in list)
            {
                b.IsSelected = false;
                Books.Add(b);
                SubscribeItem(b);
            }

            if (Books.Count == 0 && IsMultiSelectMode)
                ExitMultiSelectMode();

            UpdateSelectAllText();
            UpdateConfirmState();
        }

        private void SubscribeItem(BookItem item)
        {
            if (item == null) return;
            item.PropertyChanged += OnBookItemPropertyChanged;
            _subscribedItems.Add(item);
        }

        private void UnsubscribeAll()
        {
            foreach (var it in _subscribedItems)
                it.PropertyChanged -= OnBookItemPropertyChanged;
            _subscribedItems.Clear();
        }

        private void OnBookItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BookItem.IsSelected))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateSelectAllText();
                    UpdateConfirmState();
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
            LoadSavedFiles();
            await ShowToast("Imported successfully");
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

        // ✅ Read（正常模式可用）
        private async void OnFileClicked(object sender, EventArgs e)
        {
            if (IsMultiSelectMode) return;

            if (sender is Button button && button.CommandParameter is BookItem book)
            {
                if (!File.Exists(book.FullPath))
                {
                    await DisplayAlert("Error", "File not found!", "OK");
                    LoadSavedFiles();
                    return;
                }

                var route = $"reading?filePath={Uri.EscapeDataString(book.FullPath)}";
                await Shell.Current.GoToAsync(route);
            }
        }

        // ✅ 点击左侧内容区域：多选模式下切换选中
        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (!IsMultiSelectMode) return;

            if (e.Parameter is BookItem book)
            {
                book.IsSelected = !book.IsSelected;
                UpdateConfirmState();
            }
        }

        // ✅ 长按：进入多选并选中当前项
        private void OnItemLongPressed(BookItem? book)
        {
            if (book == null) return;

            if (!IsMultiSelectMode)
                EnterMultiSelectMode();

            book.IsSelected = true;
            UpdateConfirmState();
        }

        // ✅ 顶部垃圾桶
        private async void OnTrashTapped(object sender, EventArgs e)
        {
            await AnimatePress(TrashButton);

            if (Books.Count == 0)
            {
                await ShowToast("No books to delete");
                return;
            }

            // 单本：直接弹窗
            if (Books.Count == 1)
            {
                OpenDeleteDialog(new List<BookItem> { Books[0] }, single: true);
                return;
            }

            // 多本：进入多选模式
            EnterMultiSelectMode();
        }

        // ✅ Swipe Delete（单本）
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
        }

        private void ExitMultiSelectMode()
        {
            IsMultiSelectMode = false;

            foreach (var b in Books)
                b.IsSelected = false;

            UpdateSelectAllText();
            UpdateConfirmState();
        }

        private void OnSelectAllClicked(object sender, EventArgs e)
        {
            if (!IsMultiSelectMode || Books.Count == 0) return;

            bool allSelected = Books.All(b => b.IsSelected);
            foreach (var b in Books)
                b.IsSelected = !allSelected;

            UpdateSelectAllText();
            UpdateConfirmState();
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

        private void OnCancelMultiSelectClicked(object sender, EventArgs e)
        {
            ExitMultiSelectMode();
        }

        // ✅ Confirm（多选删除）
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
                return;
            }

            bool hasSelected = Books.Any(b => b.IsSelected);
            ConfirmButton.Opacity = hasSelected ? 1.0 : 0.45;
            ConfirmButton.InputTransparent = !hasSelected;
        }

        // ===== Delete Dialog =====

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

            LoadSavedFiles();
            await ShowToast("Deleted successfully");
        }

        private async Task DeleteBookFileAsync(BookItem book, bool reloadAfter)
        {
            try
            {
                var inList = Books.FirstOrDefault(x => x.FullPath == book.FullPath);
                if (inList != null)
                    Books.Remove(inList);

                if (File.Exists(book.FullPath))
                    File.Delete(book.FullPath);

                if (reloadAfter)
                    LoadSavedFiles();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to delete file: {ex.Message}", "OK");
            }
        }

        // ===== Animations & Toast =====

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

        public new event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}