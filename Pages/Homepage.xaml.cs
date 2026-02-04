using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class Homepage : ContentPage
    {
        public ObservableCollection<string> TxtFiles { get; set; } = new ObservableCollection<string>();
        private readonly string TxtFolderPath;

        public Homepage()
        {
            InitializeComponent();
            TxtFolderPath = Path.Combine(FileSystem.AppDataDirectory, "TxtFiles");
            EnsureTxtFolderExists();
            BindingContext = this;
            LoadSavedFiles();
        }

        // Make sure the Txt folder exists, if not create it
        private void EnsureTxtFolderExists()
        {
            if (!Directory.Exists(TxtFolderPath))
            {
                Directory.CreateDirectory(TxtFolderPath);
            }

            // Create a default usage guide (first time run only)
            string guidePath = Path.Combine(TxtFolderPath, "Usage Guidelines.txt");
            if (!File.Exists(guidePath))
            {
                string guideContent = """
                Welcome to E_Book 📘

                E_Book is a lightweight TXT reading application built with .NET MAUI.
                It helps you organize, read, and customize your text-based documents
                with a clean and distraction-free experience.

                ────────────────────────────
                📂 Bookshelf & File Management:
                ────────────────────────────
                ➕ Tap the plus button in the top-left to import TXT files from your device   
                📖 Tap the book icon next to a file to start reading  
                🗑️ Tap the trash icon to remove unwanted files from the bookshelf   

                Imported files are stored locally and managed automatically by the app.
                
                ────────────────────────────
                🛠️ Reading Features:
                ────────────────────────────
                • Tap the center of the screen to show or hide reading tools  
                • Tap the ❮ back button to return to the bookshelf  
                • Tap the Aa button to customize your reading experience:
                   - Adjust font size (A- / A+)
                   - Switch background color (multiple themes available)

                Your changes are applied instantly for comfortable reading.
                ────────────────────────────
                🔐 Settings (from ⚙️ page):
                ────────────────────────────
                From the Settings page, you can:
                • Enable password protection when launching the app  
                • Automatically lock the app after exiting (password required)  
                • Keep the screen on while reading to avoid interruptions  

                These options help protect your privacy and improve usability.
                
                ────────────────────────────
                📌 Reading Tips:
                ────────────────────────────
                ✓ Reading progress is saved automatically for each file  
                ✓ You will continue reading from where you last stopped  
                ✓ Adjust font size and background color to reduce eye strain  
                ✓ All data is stored locally on your device
                ────────────────────────────
                Thank you for using E_Book!
                Enjoy a simple and focused reading experience.
                """;
                File.WriteAllText(guidePath, guideContent);
            }
        }
        //

        // Read the saved TXT file and update the list
        private void LoadSavedFiles()
        {
            var files = Directory.GetFiles(TxtFolderPath, "*.txt");
            var fileNames = files.Select(Path.GetFileName).ToList();

            // Only update the UI when the file list has changed
            if (!TxtFiles.SequenceEqual(fileNames))
            {
                TxtFiles.Clear();
                foreach (var file in fileNames)
                {
                    TxtFiles.Add(file);
                }
            }
        }
        //

        // Add TXT file
        private async void OnAddFileClicked(object sender, EventArgs e)
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "text/plain" } },
                    { DevicePlatform.iOS, new[] { "public.plain-text" } }
                }),
                PickerTitle = "Select TXT file"
            });

            if (result != null)
            {
                string newFilePath = Path.Combine(TxtFolderPath, result.FileName);

                // Prevent duplicate file imports
                if (File.Exists(newFilePath))
                {
                    await DisplayAlert("Notice", "This file has already been imported!", "OK");
                    return;
                }

                await SaveTxtFile(result);
                LoadSavedFiles();  // Reload the file list
            }
        }
        //

        // Save TXT file
        private async Task SaveTxtFile(FileResult file)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var newFileStream = File.Create(Path.Combine(TxtFolderPath, file.FileName));
                await stream.CopyToAsync(newFileStream);
                TxtFiles.Add(Path.GetFileName(newFileStream.Name));  // Update UI immediately
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to save file: {ex.Message}", "OK");
            }
        }
        //

        // Go to ReadingPage
        private async void OnFileClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string fileName)
            {
                string filePath = Path.Combine(TxtFolderPath, fileName);

                // Ensure the file exists before opening it
                if (!File.Exists(filePath))
                {
                    await DisplayAlert("Error", "File not found!", "OK");
                    return;
                }

                await Navigation.PushAsync(new ReadingPage(filePath));
            }
        }
        //

        // Delete TXT file
        private async void OnDeleteFileClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string fileName)
            {
                string filePath = Path.Combine(TxtFolderPath, fileName);

                // Confirm before deleting
                bool confirm = await DisplayAlert("Delete", $"Are you sure you want to delete \"{fileName}\"?", "Yes", "No");
                if (!confirm) return;

                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        TxtFiles.Remove(fileName); // Update UI
                    }
                    else
                    {
                        await DisplayAlert("Error", "File not found!", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to delete file: {ex.Message}", "OK");
                }
            }
        }
        //

        // Bottom navigation bar
        private async void OnSettingClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingPage());
        }
        //
    }
}
