using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using EZ_Read.Data;

namespace EZ_Read.Pages
{
    public partial class ReadingPage : ContentPage
    {
        //Database and reading related
        private string[] lines = Array.Empty<string>();
        private int currentPage = 0;
        private int linesPerPage = 20;
        public string FilePath { get; set; }
        private readonly Database dbHelper = new();
        //

        //Aa button related
        private int[] fontSizes = new[] { 18, 22, 26 };
        private int fontIndex = 0;
        private int currentFontSize = 18;
        private string currentBackgroundColor = "#FFF8E8";
        //

        public ReadingPage(string filePath)
        {
            InitializeComponent();
            FilePath = filePath;
        }

        //Reading txt files + Load saved reading settings + Load reading progress
        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            // Load reading settings (font size & background)
            var readingSettings = await dbHelper.GetReadingSettingsAsync();

            fontIndex = Array.IndexOf(fontSizes, readingSettings.FontSize);
            if (fontIndex < 0) fontIndex = 0;
            currentFontSize = fontSizes[fontIndex];
            fileContentLabel.FontSize = currentFontSize;
            FontSizeLabel.Text = currentFontSize.ToString();
            UpdateLinesPerPage(); // ✨ 动态分页

            currentBackgroundColor = readingSettings.BackgroundColor;
            this.BackgroundColor = Color.FromArgb(currentBackgroundColor);

            // Load file content
            if (!string.IsNullOrEmpty(FilePath))
            {
                await LoadFile(FilePath);

                // Load reading progress
                string fileName = Path.GetFileName(FilePath);
                currentPage = await dbHelper.GetReadingProgressAsync(fileName);
                DisplayPage();
            }
        }
        //

        //Reading txt files
        private async Task LoadFile(string filePath)
        {
            try
            {
                string fileContent = await File.ReadAllTextAsync(filePath);
                lines = fileContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            }
            catch (Exception ex)
            {
                fileContentLabel.Text = "Unable to load file: " + ex.Message;
            }
        }
        //

        //Show content
        private void DisplayPage()
        {
            if (lines == null || lines.Length == 0) return;

            int start = currentPage * linesPerPage;
            int end = Math.Min(start + linesPerPage, lines.Length);

            fileContentLabel.Text = string.Join(Environment.NewLine, lines[start..end]);
        }
        //

        //Page turning interaction + Save progress
        private async void OnLeftTapped(object sender, EventArgs e)
        {
            if (currentPage > 0)
            {
                currentPage--;
                DisplayPage();
                await SaveReadingProgress();
            }
        }

        private async void OnRightTapped(object sender, EventArgs e)
        {
            if ((currentPage + 1) * linesPerPage < lines.Length)
            {
                currentPage++;
                DisplayPage();
                await SaveReadingProgress();
            }
        }
        //

        //Multi-function menu call out
        private void OnCenterTapped(object sender, EventArgs e)
        {
            bool show = !BackButton.IsVisible;

            BackButton.IsVisible = show;
            AaButton.IsVisible = show;
            AaMenu.IsVisible = false;
        }
        //

        //Force hiding other function buttons (avoid cluttered content)
        private void HideAllFunctionButtons()
        {
            BackButton.IsVisible = false;
            AaButton.IsVisible = false;
            AaMenu.IsVisible = false;
        }
        //

        //Back to Homepage
        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            HideAllFunctionButtons();
            await Navigation.PopAsync();
        }
        //

        //Aa table (Modify font size, background color)
        private void OnAaButtonClicked(object sender, EventArgs e)
        {
            AaMenu.IsVisible = !AaMenu.IsVisible;
        }

        private async void OnFontIncreaseClicked(object sender, EventArgs e)
        {
            if (fontIndex < fontSizes.Length - 1)
            {
                fontIndex++;
                currentFontSize = fontSizes[fontIndex];
                fileContentLabel.FontSize = currentFontSize;
                FontSizeLabel.Text = currentFontSize.ToString();

                UpdateLinesPerPage();  //Dynamic paging update
                DisplayPage();         //Refresh current content

                await SaveCurrentReadingSettings();
            }
        }

        private async void OnFontDecreaseClicked(object sender, EventArgs e)
        {
            if (fontIndex > 0)
            {
                fontIndex--;
                currentFontSize = fontSizes[fontIndex];
                fileContentLabel.FontSize = currentFontSize;
                FontSizeLabel.Text = currentFontSize.ToString();

                UpdateLinesPerPage();  //Dynamic paging update
                DisplayPage();         //Refresh current content

                await SaveCurrentReadingSettings();
            }
        }

        private async void OnColorButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string hexColor)
            {
                currentBackgroundColor = hexColor;
                this.BackgroundColor = Color.FromArgb(hexColor);
                await SaveCurrentReadingSettings();
            }
        }
        //

        //Save reading preferences to SQLite (ReadingSettings table)
        private async Task SaveCurrentReadingSettings()
        {
            await dbHelper.SaveReadingSettingsAsync(currentFontSize, currentBackgroundColor);
        }
        //

        //Update how many lines are shown per page based on font size
        private void UpdateLinesPerPage()
        {
            if (currentFontSize == 18)
                linesPerPage = 20;
            else if (currentFontSize == 22)
                linesPerPage = 16;
            else if (currentFontSize == 26)
                linesPerPage = 12;
        }
        //

        //Save reading progress for the file
        private async Task SaveReadingProgress()
        {
            string fileName = Path.GetFileName(FilePath);
            await dbHelper.SaveReadingProgressAsync(fileName, currentPage);
        }
        //
    }
}
