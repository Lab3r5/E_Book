using Microsoft.Maui.Controls;

namespace E_Book.Services
{
    public static class UIAnimationService
    {
        public static async Task PressAsync(
            VisualElement? view,
            double downScale = 0.975,
            double downOpacity = 0.96,
            uint downDuration = 70,
            uint upDuration = 125)
        {
            if (view == null) return;

            await Task.WhenAll(
                view.ScaleTo(downScale, downDuration, Easing.CubicOut),
                view.FadeTo(downOpacity, downDuration, Easing.CubicOut)
            );

            await Task.WhenAll(
                view.ScaleTo(1.0, upDuration, Easing.CubicOut),
                view.FadeTo(1.0, upDuration, Easing.CubicOut)
            );
        }

        public static async Task FadeSlideInAsync(
            VisualElement? view,
            double fromY = 8,
            uint duration = 190,
            uint delay = 0)
        {
            if (view == null) return;

            view.Opacity = 0;
            view.TranslationY = fromY;

            if (delay > 0)
                await Task.Delay((int)delay);

            await Task.WhenAll(
                view.FadeTo(1, duration, Easing.CubicOut),
                view.TranslateTo(0, 0, duration, Easing.CubicOut)
            );
        }

        public static async Task FadeScaleCardInAsync(
            VisualElement? view,
            double fromY = 14,
            double fromScale = 0.995,
            uint duration = 240,
            uint delay = 0)
        {
            if (view == null) return;

            view.Opacity = 0;
            view.TranslationY = fromY;
            view.Scale = fromScale;

            if (delay > 0)
                await Task.Delay((int)delay);

            await Task.WhenAll(
                view.FadeTo(1, duration, Easing.CubicOut),
                view.TranslateTo(0, 0, duration, Easing.CubicOut),
                view.ScaleTo(1, duration, Easing.CubicOut)
            );
        }

        public static async Task FadeListInAsync(
            VisualElement? view,
            double fromY = 18,
            uint duration = 220,
            uint delay = 0)
        {
            if (view == null) return;

            view.Opacity = 0;
            view.TranslationY = fromY;

            if (delay > 0)
                await Task.Delay((int)delay);

            await Task.WhenAll(
                view.FadeTo(1, duration, Easing.CubicOut),
                view.TranslateTo(0, 0, duration, Easing.CubicOut)
            );
        }

        public static async Task HighlightAsync(VisualElement? view, double peakOpacity = 0.30)
        {
            if (view == null) return;

            view.Opacity = 0;
            view.Scale = 0.96;

            await Task.WhenAll(
                view.FadeTo(peakOpacity, 80, Easing.CubicOut),
                view.ScaleTo(1.0, 110, Easing.CubicOut)
            );

            await Task.WhenAll(
                view.FadeTo(0, 180, Easing.CubicOut),
                view.ScaleTo(1.01, 180, Easing.CubicOut)
            );
        }

        public static async Task PopAsync(VisualElement? view)
        {
            if (view == null) return;

            await view.ScaleTo(1.04, 90, Easing.CubicOut);
            await view.ScaleTo(1.0, 140, Easing.CubicOut);
        }
    }
}