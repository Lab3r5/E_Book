using System.Windows.Input;
using E_Book.Services;

namespace E_Book.Controls
{
    public partial class AppHeader : ContentView
    {
        public AppHeader()
        {
            InitializeComponent();
            ApplyState();
        }

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(
                nameof(Title),
                typeof(string),
                typeof(AppHeader),
                "Title",
                propertyChanged: (b, o, n) =>
                {
                    if (b is AppHeader h && n is string s)
                        h.TitleLabel.Text = s;
                });

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly BindableProperty ShowBackProperty =
            BindableProperty.Create(
                nameof(ShowBack),
                typeof(bool),
                typeof(AppHeader),
                true,
                propertyChanged: (b, o, n) =>
                {
                    if (b is AppHeader h && n is bool v)
                        h.BackContainer.IsVisible = v;
                });

        public bool ShowBack
        {
            get => (bool)GetValue(ShowBackProperty);
            set => SetValue(ShowBackProperty, value);
        }

        public static readonly BindableProperty BackCommandProperty =
            BindableProperty.Create(nameof(BackCommand), typeof(ICommand), typeof(AppHeader), null);

        public ICommand? BackCommand
        {
            get => (ICommand?)GetValue(BackCommandProperty);
            set => SetValue(BackCommandProperty, value);
        }

        public static readonly BindableProperty RightContentProperty =
            BindableProperty.Create(
                nameof(RightContent),
                typeof(View),
                typeof(AppHeader),
                null,
                propertyChanged: (b, o, n) =>
                {
                    if (b is AppHeader h)
                        h.RightSlot.Content = n as View;
                });

        public View? RightContent
        {
            get => (View?)GetValue(RightContentProperty);
            set => SetValue(RightContentProperty, value);
        }

        private void ApplyState()
        {
            BackContainer.IsVisible = ShowBack;
        }

        private async void OnBackTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v)
                await UIAnimationService.PressAsync(v, 0.94, 0.95, 70, 120);

            if (BackCommand?.CanExecute(null) == true)
                BackCommand.Execute(null);
        }
    }
}