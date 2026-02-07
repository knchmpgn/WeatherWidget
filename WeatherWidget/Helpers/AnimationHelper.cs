using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WeatherWidget.Helpers
{
    public static class AnimationHelper
    {
        public static void ShowFlyout(Window window)
        {
            window.Opacity = 0;
            var transform = new TranslateTransform(0, 20);
            window.RenderTransform = transform;

            var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
            var move = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            window.BeginAnimation(Window.OpacityProperty, fade);
            transform.BeginAnimation(TranslateTransform.YProperty, move);
        }
    }
}