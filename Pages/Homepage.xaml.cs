using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace EZ_Read.Pages
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
                Welcome to EZ-Read 📚

                Your lightweight Android reading app for TXT files.

                ────────────────────────────
                📂 Basic Usage:
                ────────────────────────────
                ➕ Tap the plus button in the top-left to import TXT files  
                📖 Tap the book icon next to a file to start reading  
                🗑️ Tap the trash icon to delete unwanted files  

                ────────────────────────────
                🛠️ Reading Tools:
                ────────────────────────────
                • Tap the center of the screen to show tools  
                • Tap the ❮ button to return to the bookshelf  
                • Tap the Aa button to customize:
                   - Font Size (A- / A+)
                   - Background Color (5 themes available)

                ────────────────────────────
                🔐 Settings (from ⚙️ page):
                ────────────────────────────
                • Enable password protection on startup  
                • Lock screen after exit (requires password enabled)  
                • Keep screen on during reading  

                ────────────────────────────
                📌 Tips:
                ────────────────────────────
                ✓ Your reading settings will be saved automatically  
                ✓ Reading progress is tracked per file — you’ll resume where you left off  
                ✓ Use a comfortable background color and font size for better readability  

                ────────────────────────────
                Thank you for using EZ-Read!
                Developed by Team Shawarma  
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
