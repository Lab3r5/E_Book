using E_Book.Services;

namespace E_Book
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // ✅ 启动就应用上次模式（Auto/Light/Dark）
            ThemeScheduler.StartTimer();
            ThemeScheduler.ApplyNow();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}