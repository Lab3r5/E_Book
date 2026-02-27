using System.Collections.ObjectModel;
using E_Book.Services;

namespace E_Book.Pages
{
    public partial class SearchPage : ContentPage
    {
        public ObservableCollection<SearchResultItem> Results { get; } = new();

        public SearchPage()
        {
            InitializeComponent();
            ResultList.ItemsSource = Results;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RenderHistory();
        }

        private void RenderHistory()
        {
            var list = SearchHistoryStore.Get(UserSession.UserId);

            HistoryContainer.Children.Clear();
            HistorySection.IsVisible = list.Count > 0;

            foreach (var keyword in list)
            {
                var chip = new HorizontalStackLayout { Spacing = 6 };

                var textBtn = new Button { Text = keyword, Padding = new Thickness(10, 6) };
                textBtn.Clicked += (_, __) =>
                {
                    SearchBarBox.Text = keyword;
                    DoSearch(keyword);
                };

                var delBtn = new Button { Text = "✕", Padding = new Thickness(10, 6) };
                delBtn.Clicked += async (_, __) =>
                {
                    bool ok = await DisplayAlert("Delete", $"Delete history \"{keyword}\"?", "Yes", "No");
                    if (!ok) return;

                    SearchHistoryStore.Delete(UserSession.UserId, keyword);
                    RenderHistory();
                };

                chip.Children.Add(textBtn);
                chip.Children.Add(delBtn);

                HistoryContainer.Children.Add(chip);
            }
        }

        private void OnPopularClicked(object sender, EventArgs e)
        {
            if (sender is Button b)
            {
                SearchBarBox.Text = b.Text;
                DoSearch(b.Text);
            }
        }

        private void OnSearchPressed(object sender, EventArgs e)
        {
            DoSearch(SearchBarBox.Text ?? "");
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            // 规则：输入不记录历史
        }

        private void DoSearch(string keyword)
        {
            keyword = (keyword ?? "").Trim();
            Results.Clear();

            if (keyword.Length == 0)
            {
                StatusLabel.Text = "Type a keyword to search.";
                return;
            }

            // 演示用：小于3个字符当作无结果
            if (keyword.Length < 3)
            {
                StatusLabel.Text = "No search results. Try a different keyword.";
                return;
            }

            StatusLabel.Text = "Search Results";

            Results.Add(new SearchResultItem { Title = keyword, Author = "Unknown Author" });
            Results.Add(new SearchResultItem { Title = $"{keyword} (Illustrated)", Author = "Unknown Author" });
        }

        private async void OnReadClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is SearchResultItem item)
            {
                SearchHistoryStore.AddOnRead(UserSession.UserId, SearchBarBox.Text ?? item.Title);
                RenderHistory();

                await DisplayAlert("Read", $"Open: {item.Title}", "OK");
            }
        }
    }

    public class SearchResultItem
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
    }
}