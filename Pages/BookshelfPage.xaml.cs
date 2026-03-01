using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using E_Book.Services;
using E_Book.Models;

namespace E_Book.Pages
{
    public partial class BookshelfPage : ContentPage
    {
        public ObservableCollection<BookItem> Books { get; set; } = new();

        public BookshelfPage()
        {
            InitializeComponent();

            EnsureLibraryExists();

            BindingContext = this;
            LoadSavedFiles();
        }

        private void EnsureLibraryExists()
        {
            // ✅ 统一走 LibraryService
            LibraryService.EnsureLibraryExists();

            // ✅ Usage Guidelines 放到同一个 Library 目录
            string guidePath = Path.Combine(LibraryService.LibraryPath, "Usage Guidelines.txt");

            if (!File.Exists(guidePath))
            {
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

        // ✅ 按你的要求：完全用 LibraryService.LoadBooks()
        private void LoadSavedFiles()
        {
            var list = LibraryService.LoadBooks();

            Books.Clear();
            foreach (var b in list)
                Books.Add(b);
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

            // ✅ 按你的要求：用 LibraryService.LibraryPath
            string targetPath = Path.Combine(LibraryService.LibraryPath, result.FileName);

            if (File.Exists(targetPath))
            {
                await DisplayAlert("Notice", "This file has already been imported!", "OK");
                return;
            }

            await SaveFileToLibrary(result, targetPath);

            // ✅ 重新加载（确保顺序/格式一致）
            LoadSavedFiles();
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

        // ✅ Open ReadingPage via Shell route
        private async void OnFileClicked(object sender, EventArgs e)
        {
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

        private async void OnDeleteFileClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is BookItem book)
            {
                bool confirm = await DisplayAlert("Delete", $"Delete \"{book.FileName}\"?", "Yes", "No");
                if (!confirm) return;

                try
                {
                    if (File.Exists(book.FullPath))
                        File.Delete(book.FullPath);

                    // ✅ 删除后重新加载（避免残留/排序问题）
                    LoadSavedFiles();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete file: {ex.Message}", "OK");
                }
            }
        }

        private async void OnSettingClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//settings");
        }
    }
}