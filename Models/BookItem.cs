using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace E_Book.Models
{
    public class BookItem : INotifyPropertyChanged
    {
        private string _fileName = "";
        public string FileName
        {
            get => _fileName;
            set
            {
                if (_fileName == value) return;
                _fileName = value;
                UpdateCover();
                OnPropertyChanged();
            }
        }

        private string _fullPath = "";
        public string FullPath
        {
            get => _fullPath;
            set
            {
                if (_fullPath == value) return;
                _fullPath = value;
                OnPropertyChanged();
            }
        }

        private string _format = "";
        public string Format
        {
            get => _format;
            set
            {
                if (_format == value) return;
                _format = value;
                OnPropertyChanged();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private double _readingProgress;
        public double ReadingProgress
        {
            get => _readingProgress;
            set
            {
                double normalized = Math.Max(0, Math.Min(1, value));
                if (Math.Abs(_readingProgress - normalized) < 0.0001) return;
                _readingProgress = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasProgress));
                OnPropertyChanged(nameof(ProgressPercentText));
            }
        }

        private long _lastOpenedTicks;
        public long LastOpenedTicks
        {
            get => _lastOpenedTicks;
            set
            {
                if (_lastOpenedTicks == value) return;
                _lastOpenedTicks = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastOpened));
                OnPropertyChanged(nameof(LastOpenedText));
            }
        }

        private string _coverText = "BK";
        public string CoverText
        {
            get => _coverText;
            set
            {
                if (_coverText == value) return;
                _coverText = value;
                OnPropertyChanged();
            }
        }

        private Color _coverColor = Color.FromArgb("#8E7CF3");
        public Color CoverColor
        {
            get => _coverColor;
            set
            {
                if (_coverColor == value) return;
                _coverColor = value;
                OnPropertyChanged();
            }
        }

        public bool HasProgress => ReadingProgress > 0;
        public bool HasLastOpened => LastOpenedTicks > 0;

        public string ProgressPercentText => $"{Math.Round(ReadingProgress * 100)}%";

        public string LastOpenedText
        {
            get
            {
                if (LastOpenedTicks <= 0)
                    return "Not opened";

                var dt = new DateTime(LastOpenedTicks, DateTimeKind.Utc).ToLocalTime();
                var today = DateTime.Now.Date;

                if (dt.Date == today)
                    return "Opened today";

                if (dt.Date == today.AddDays(-1))
                    return "Opened yesterday";

                return $"Opened {dt:dd MMM}";
            }
        }

        public void RefreshVisualMeta()
        {
            UpdateCover();
            OnPropertyChanged(nameof(HasProgress));
            OnPropertyChanged(nameof(ProgressPercentText));
            OnPropertyChanged(nameof(HasLastOpened));
            OnPropertyChanged(nameof(LastOpenedText));
        }

        private void UpdateCover()
        {
            string title = Path.GetFileNameWithoutExtension(FileName ?? "")?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(title))
            {
                CoverText = "BK";
                CoverColor = Color.FromArgb("#8E7CF3");
                return;
            }

            title = title.Replace("_", " ").Replace("-", " ").Trim();

            string displayText;
            var parts = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (ContainsCjk(title))
            {
                displayText = title.Length >= 2 ? title.Substring(0, 2) : title;
            }
            else if (parts.Length >= 2)
            {
                displayText = $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
            }
            else
            {
                displayText = title.Length >= 2
                    ? title.Substring(0, 2).ToUpperInvariant()
                    : title.ToUpperInvariant();
            }

            CoverText = displayText;

            string[] palette =
            {
                "#8E7CF3",
                "#6C63FF",
                "#7C92F7",
                "#8E96D9",
                "#B07CF3",
                "#6F86D6",
                "#7D6AE8",
                "#8C7AE6"
            };

            int hash = GetStableHash(title);
            int index = Math.Abs(hash) % palette.Length;
            CoverColor = Color.FromArgb(palette[index]);
        }

        private static bool ContainsCjk(string text)
        {
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF)
                    return true;
            }
            return false;
        }

        private static int GetStableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        public override string ToString() => $"{FileName} ({Format})";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}