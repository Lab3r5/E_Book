using System.Windows.Input;
using E_Book.Services;
using Microsoft.Maui.Storage;

namespace E_Book.Pages
{
    public partial class EditProfilePage : ContentPage
    {
        private string ProfileKey => $"profile_name_{UserSession.UserId}";

        public EditProfilePage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var savedName = Preferences.Get(ProfileKey, UserSession.DisplayName);

            NameEntry.Text = savedName;
            EmailEntry.Text = UserSession.IsGuest ? "" : UserSession.UserId;

            // Guest 不允许编辑
            if (UserSession.IsGuest)
            {
                NameEntry.IsEnabled = false;
                NameEntry.Text = "Guest";

                SaveLabel.IsVisible = false;
                GuestTip.IsVisible = true;

                EmailEntry.IsVisible = false;
                PasswordDots.IsVisible = false;
            }
            else
            {
                SaveLabel.IsVisible = true;
                GuestTip.IsVisible = false;

                EmailEntry.IsVisible = true;
                PasswordDots.IsVisible = true;
            }
        }

        private async Task PressAnim(VisualElement view)
        {
            if (view == null) return;
            await view.ScaleTo(0.96, 80, Easing.CubicOut);
            await view.ScaleTo(1.0, 120, Easing.CubicOut);
        }

        private async Task GoBackAsync()
        {
            // ✅ 优先 Shell 返回（Modal route）
            try { await Shell.Current.GoToAsync(".."); return; } catch { }
            await Navigation.PopModalAsync();
        }

        private async void OnCancelTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);
            await GoBackAsync();
        }

        private async void OnSaveTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            if (UserSession.IsGuest) return;

            var name = NameEntry.Text?.Trim() ?? "";
            if (name.Length == 0)
            {
                await DisplayAlert("Error", "Name cannot be empty.", "OK");
                return;
            }

            Preferences.Set(ProfileKey, name);
            UserSession.UpdateDisplayName(name);

            await DisplayAlert("Saved", "Profile updated.", "OK");
            await GoBackAsync();
        }

        private async void OnChangePasswordTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            if (UserSession.IsGuest) return;

            // ✅ 仍然用 Shell route
            await Shell.Current.GoToAsync("password");
        }

        private async void OnLogoutTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            // 你按自己的 logout 逻辑替换这段
            await DisplayAlert("Log Out", "Coming soon.", "OK");
        }
    }
}