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

            await Shell.Current.GoToAsync("password");
        }

        // =========================================================
        // ✅ Log Out：功能不变（确认 -> Logout -> //login），只改弹窗样式
        // =========================================================

        private async void OnLogoutTapped(object sender, TappedEventArgs e)
        {
            if (sender is VisualElement v) await PressAnim(v);

            // 你原本的 guest 行为保留
            if (UserSession.IsGuest)
            {
                await DisplayAlert("Notice", "Guest cannot log out.", "OK");
                return;
            }

            // ✅ 用自定义弹窗代替 DisplayAlert（行为仍然是确认/取消）
            await ShowLogoutDialog();
        }

        private async Task ShowLogoutDialog()
        {
            LogoutOverlay.IsVisible = true;

            // 重置动画状态
            LogoutDialog.Opacity = 0;
            LogoutDialog.Scale = 0.85;

            await Task.WhenAll(
                LogoutDialog.FadeTo(1, 220, Easing.CubicOut),
                LogoutDialog.ScaleTo(1, 220, Easing.SpringOut)
            );
        }

        private async Task HideLogoutDialog()
        {
            await Task.WhenAll(
                LogoutDialog.FadeTo(0, 160, Easing.CubicIn),
                LogoutDialog.ScaleTo(0.85, 160, Easing.CubicIn)
            );

            LogoutOverlay.IsVisible = false;
        }

        private async void OnLogoutDialogCancel(object sender, EventArgs e)
        {
            await HideLogoutDialog();
        }

        private async void OnLogoutDialogConfirm(object sender, EventArgs e)
        {
            await HideLogoutDialog();

            // ✅ 这里完全照你原来的功能：Logout + 跳 login
            UserSession.Logout();
            await Shell.Current.GoToAsync("//login");
        }
    }
}