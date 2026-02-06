using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class Homepage : ContentPage
    {
        public ObservableCollection<BookItem> Books { get; set; } = new();

        private readonly string LibraryPath;

        private static readonly string[] SupportedExtensions =
        {
            ".txt", ".epub", ".pdf", ".html", ".htm", ".docx", ".rtf"
        };

        public Homepage()
        {
            InitializeComponent();

            LibraryPath = Path.Combine(FileSystem.AppDataDirectory, "Library");
            EnsureLibraryExists();

            BindingContext = this;
            LoadSavedFiles();
        }

        private void EnsureLibraryExists()
        {
            if (!Directory.Exists(LibraryPath))
                Directory.CreateDirectory(LibraryPath);

            // Create a default usage guide (first time run only)
            string guidePath = Path.Combine(LibraryPath, "Usage Guidelines.txt");
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

        private void LoadSavedFiles()
        {
            if (!Directory.Exists(LibraryPath))
                Directory.CreateDirectory(LibraryPath);

            var files = Directory.GetFiles(LibraryPath)
                                 .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                 .OrderBy(f => Path.GetFileName(f))
                                 .ToList();

            var list = files.Select(f => new BookItem
            {
                FileName = Path.GetFileName(f),
                FullPath = f,
                Format = GetFormatTag(f)
            }).ToList();

            // Only update if changed (by filename order)
            bool same = Books.Count == list.Count &&
                        !Books.Where((t, i) => t.FileName != list[i].FileName).Any();

            if (!same)
            {
                Books.Clear();
                foreach (var b in list) Books.Add(b);
            }
        }

        private static string GetFormatTag(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".txt" => "TXT",
                ".epub" => "EPUB",
                ".pdf" => "PDF",
                ".html" or ".htm" => "HTML",
                ".docx" => "DOCX",
                ".rtf" => "RTF",
                _ => "FILE"
            };
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
                        "*/*" // fallback; we validate extension after picking
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
            if (!SupportedExtensions.Contains(ext))
            {
                await DisplayAlert("Not supported", $"Unsupported file type: {ext}", "OK");
                return;
            }

            string targetPath = Path.Combine(LibraryPath, result.FileName);

            // Prevent duplicates
            if (File.Exists(targetPath))
            {
                await DisplayAlert("Notice", "This file has already been imported!", "OK");
                return;
            }

            await SaveFileToLibrary(result, targetPath);

            // Reload to keep order consistent
            LoadSavedFiles();
        }

        private async Task SaveFileToLibrary(FileResult file, string targetPath)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var newFileStream = File.Create(targetPath);
                await stream.CopyToAsync(newFileStream);

                Books.Add(new BookItem
                {
                    FileName = Path.GetFileName(targetPath),
                    FullPath = targetPath,
                    Format = GetFormatTag(targetPath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
            }
        }

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

                await Navigation.PushAsync(new ReadingPage(book.FullPath));
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

                    Books.Remove(book);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete file: {ex.Message}", "OK");
                }
            }
        }

        private async void OnSettingClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingPage());
        }
    }

    public class BookItem
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Format { get; set; } = ""; // TXT/EPUB/PDF/HTML/DOCX/RTF
    }
}