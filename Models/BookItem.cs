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
                OnPropertyChanged(nameof(DisplayFileName));
            }
        }

        public string DisplayFileName =>
            Path.GetFileNameWithoutExtension(FileName ?? "") ?? "";

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
                OnPropertyChanged(nameof(SelectionMark));
                OnPropertyChanged(nameof(SelectionBackgroundColor));
                OnPropertyChanged(nameof(SelectionTextColor));
                OnPropertyChanged(nameof(SelectionBorderColor));
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
                OnPropertyChanged(nameof(ShowProgressBar));
                OnPropertyChanged(nameof(ProgressPercentText));
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsAlmostFinished));
                OnPropertyChanged(nameof(ShowReadingBadge));
                OnPropertyChanged(nameof(ReadingStatusText));
                OnPropertyChanged(nameof(ReadButtonText));
                OnPropertyChanged(nameof(HasLastReadPage));
                OnPropertyChanged(nameof(LastReadPageText));
                OnPropertyChanged(nameof(HasReadingTime));
                OnPropertyChanged(nameof(ReadingTimeText));
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
                OnPropertyChanged(nameof(IsUnread));
            }
        }

        private int _lastReadPage;
        public int LastReadPage
        {
            get => _lastReadPage;
            set
            {
                int normalized = Math.Max(0, value);
                if (_lastReadPage == normalized) return;

                _lastReadPage = normalized;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastReadPage));
                OnPropertyChanged(nameof(LastReadPageText));
            }
        }

        private int _totalPages;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                int normalized = Math.Max(0, value);
                if (_totalPages == normalized) return;

                _totalPages = normalized;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLastReadPage));
                OnPropertyChanged(nameof(LastReadPageText));
            }
        }

        private long _totalReadingSeconds;
        public long TotalReadingSeconds
        {
            get => _totalReadingSeconds;
            set
            {
                long normalized = Math.Max(0, value);
                if (_totalReadingSeconds == normalized) return;

                _totalReadingSeconds = normalized;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasReadingTime));
                OnPropertyChanged(nameof(ReadingTimeText));
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

        private bool _isFreshlyImported;
        public bool IsFreshlyImported
        {
            get => _isFreshlyImported;
            set
            {
                if (_isFreshlyImported == value) return;
                _isFreshlyImported = value;
                OnPropertyChanged();
            }
        }

        public bool HasProgress => ReadingProgress > 0.001;

        public bool ShowProgressBar => ReadingProgress > 0.001 && ReadingProgress < 0.995;

        public bool HasLastOpened => LastOpenedTicks > 0;

        public bool IsUnread => LastOpenedTicks <= 0;

        public bool IsCompleted => ReadingProgress >= 0.995;

        public bool IsAlmostFinished => ReadingProgress >= 0.90 && ReadingProgress < 0.995;

        public bool ShowReadingBadge => !string.IsNullOrWhiteSpace(ReadingStatusText);

        public bool HasLastReadPage => !string.IsNullOrWhiteSpace(LastReadPageText);

        public bool HasReadingTime => !string.IsNullOrWhiteSpace(ReadingTimeText);
        public bool IsPdf => string.Equals(Path.GetExtension(FullPath), ".pdf", StringComparison.OrdinalIgnoreCase);

        public bool IsImage => Path.GetExtension(FullPath).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp";

        public bool IsTextReadable => !IsPdf && !IsImage;
        public string ProgressPercentText =>
    $"{Math.Clamp((int)Math.Round(ReadingProgress * 100), 0, 100)}%";

        public string ReadingStatusText
        {
            get
            {
                if (IsCompleted) return "Completed";
                if (IsAlmostFinished) return "Almost finished";
                if (ReadingProgress > 0.001) return "Continue reading";
                return string.Empty;
            }
        }

        public string ReadButtonText
        {
            get
            {
                if (IsCompleted) return "Read again";
                if (ReadingProgress > 0) return "Resume";
                return "Read";
            }
        }

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

        public string LastReadPageText
        {
            get
            {
                if (LastReadPage > 0)
                {
                    if (TotalPages > 0)
                        return $"Last read: page {LastReadPage} / {TotalPages}";

                    return $"Last read: page {LastReadPage}";
                }

                if (IsCompleted)
                {
                    if (TotalPages > 0)
                        return $"Finished: page {TotalPages} / {TotalPages}";

                    return "Finished reading";
                }

                return string.Empty;
            }
        }

        public string ReadingTimeText
        {
            get
            {
                if (TotalReadingSeconds <= 0)
                {
                    if (IsCompleted)
                        return "Reading completed";
                    return string.Empty;
                }

                var ts = TimeSpan.FromSeconds(TotalReadingSeconds);

                if (ts.TotalHours >= 1)
                    return $"Reading time: {(int)ts.TotalHours}h {ts.Minutes}m";

                if (ts.TotalMinutes >= 1)
                    return $"Reading time: {Math.Max(1, (int)ts.TotalMinutes)}m";

                return "Reading time: <1m";
            }
        }

        public string SelectionMark => IsSelected ? "✓" : "";

        public Color SelectionBackgroundColor =>
            IsSelected ? Color.FromArgb("#8C79FF") : Colors.White;

        public Color SelectionTextColor =>
            IsSelected ? Colors.White : Colors.Transparent;

        public Color SelectionBorderColor =>
            IsSelected ? Color.FromArgb("#8C79FF") : Color.FromArgb("#C9C4DA");

        public void RefreshVisualMeta()
        {
            UpdateCover();

            OnPropertyChanged(nameof(DisplayFileName));
            OnPropertyChanged(nameof(HasProgress));
            OnPropertyChanged(nameof(ShowProgressBar));
            OnPropertyChanged(nameof(ProgressPercentText));
            OnPropertyChanged(nameof(HasLastOpened));
            OnPropertyChanged(nameof(LastOpenedText));
            OnPropertyChanged(nameof(IsUnread));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsAlmostFinished));
            OnPropertyChanged(nameof(ShowReadingBadge));
            OnPropertyChanged(nameof(ReadingStatusText));
            OnPropertyChanged(nameof(ReadButtonText));
            OnPropertyChanged(nameof(HasLastReadPage));
            OnPropertyChanged(nameof(LastReadPageText));
            OnPropertyChanged(nameof(HasReadingTime));
            OnPropertyChanged(nameof(ReadingTimeText));
            OnPropertyChanged(nameof(SelectionMark));
            OnPropertyChanged(nameof(SelectionBackgroundColor));
            OnPropertyChanged(nameof(SelectionTextColor));
            OnPropertyChanged(nameof(SelectionBorderColor));
            OnPropertyChanged(nameof(IsPdf));
            OnPropertyChanged(nameof(IsImage));
            OnPropertyChanged(nameof(IsTextReadable));
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