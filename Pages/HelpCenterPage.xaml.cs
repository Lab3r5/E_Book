using System.Windows.Input;
using E_Book.Services;

namespace E_Book.Pages
{
    public partial class HelpCenterPage : ContentPage
    {
        public ICommand BackCommand { get; }

        public HelpCenterPage()
        {
            InitializeComponent();

            BackCommand = new Command(async () =>
            {
                try
                {
                    await Navigation.PopAsync();
                    return;
                }
                catch
                {
                }

                try
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }
                catch
                {
                }

                await Navigation.PopModalAsync();
            });

            BindingContext = this;
        }

        private async Task CollapseAllExcept(Label? keepAnswer = null, Label? keepArrow = null)
        {
            var items = new List<(Label Answer, Label Arrow)>
            {
                (Answer1, Arrow1),
                (Answer2, Arrow2),
                (Answer3, Arrow3),
                (Answer4, Arrow4)
            };

            foreach (var item in items)
            {
                if (item.Answer == keepAnswer && item.Arrow == keepArrow)
                    continue;

                if (item.Answer.IsVisible)
                {
                    item.Answer.Opacity = 0;
                    item.Answer.IsVisible = false;
                    item.Arrow.Text = "⌄";
                }
            }

            await Task.CompletedTask;
        }

        private async Task ToggleAnswer(Label answerLabel, Label arrowLabel, VisualElement? card = null)
        {
            if (card != null)
                await UIAnimationService.PressAsync(card, 0.985, 0.99, 60, 90);

            if (answerLabel.IsVisible)
            {
                await answerLabel.FadeTo(0, 100, Easing.CubicOut);
                answerLabel.IsVisible = false;
                arrowLabel.Text = "⌄";
            }
            else
            {
                await CollapseAllExcept(answerLabel, arrowLabel);

                answerLabel.IsVisible = true;
                answerLabel.Opacity = 0;
                await answerLabel.FadeTo(1, 140, Easing.CubicOut);
                arrowLabel.Text = "⌃";
            }
        }

        private async void OnFaq1Tapped(object sender, TappedEventArgs e)
        {
            await ToggleAnswer(Answer1, Arrow1, FaqItem1);
        }

        private async void OnFaq2Tapped(object sender, TappedEventArgs e)
        {
            await ToggleAnswer(Answer2, Arrow2, FaqItem2);
        }

        private async void OnFaq3Tapped(object sender, TappedEventArgs e)
        {
            await ToggleAnswer(Answer3, Arrow3, FaqItem3);
        }

        private async void OnFaq4Tapped(object sender, TappedEventArgs e)
        {
            await ToggleAnswer(Answer4, Arrow4, FaqItem4);
        }
    }
}