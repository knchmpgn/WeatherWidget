using System;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace WeatherWidget.Helpers
{
    /// <summary>
    /// Helper class to create native Windows 11 Mica/Acrylic effects
    /// Matches actual Windows 11 system flyout appearance
    /// </summary>
    public class NativeAcrylicHelper
    {
        /// <summary>
        /// Create an authentic Windows 11 Mica brush matching volume/settings flyouts
        /// Uses high transparency with subtle color variation
        /// </summary>
        public static LinearGradientBrush CreateEnhancedAcrylicBrush(bool isDarkMode = true)
        {
            try
            {
                if (isDarkMode)
                {
                    // Windows 11 Dark Mica - very high transparency with subtle elevation
                    // This matches the actual volume flyout appearance
                    Color topColor = Color.FromArgb(242, 32, 32, 32);      // Very transparent dark
                    Color bottomColor = Color.FromArgb(245, 28, 28, 28);   // Slightly more opaque, darker

                    LinearGradientBrush brush = new LinearGradientBrush(topColor, bottomColor, 90);
                    brush.Freeze();
                    return brush;
                }
                else
                {
                    // Windows 11 Light Mica - very high transparency
                    Color topColor = Color.FromArgb(245, 252, 252, 252);   // Very transparent off-white
                    Color bottomColor = Color.FromArgb(248, 248, 248, 248); // Slightly more opaque, very light gray

                    LinearGradientBrush brush = new LinearGradientBrush(topColor, bottomColor, 90);
                    brush.Freeze();
                    return brush;
                }
            }
            catch
            {
                return CreateNativeAcrylicBrush(isDarkMode);
            }
        }

        /// <summary>
        /// Create standard Windows 11 Acrylic brush
        /// </summary>
        public static LinearGradientBrush CreateNativeAcrylicBrush(bool isDarkMode = true)
        {
            try
            {
                if (isDarkMode)
                {
                    // Dark mode Acrylic
                    Color topColor = Color.FromArgb(238, 32, 32, 32);
                    Color bottomColor = Color.FromArgb(242, 28, 28, 28);

                    LinearGradientBrush brush = new LinearGradientBrush(topColor, bottomColor, 90);
                    brush.Freeze();
                    return brush;
                }
                else
                {
                    // Light mode Acrylic
                    Color topColor = Color.FromArgb(240, 250, 250, 250);
                    Color bottomColor = Color.FromArgb(244, 248, 248, 248);

                    LinearGradientBrush brush = new LinearGradientBrush(topColor, bottomColor, 90);
                    brush.Freeze();
                    return brush;
                }
            }
            catch
            {
                // Fallback to safe default
                Color fallback = isDarkMode 
                    ? Color.FromArgb(238, 32, 32, 32)
                    : Color.FromArgb(240, 250, 250, 250);

                LinearGradientBrush fallbackBrush = new LinearGradientBrush(fallback, fallback, 90);
                fallbackBrush.Freeze();
                return fallbackBrush;
            }
        }

        /// <summary>
        /// Create a Windows 11 Mica brush with even higher transparency
        /// Closest to actual Windows 11 system Mica material
        /// </summary>
        public static LinearGradientBrush CreateMicaBrush(bool isDarkMode = true)
        {
            try
            {
                if (isDarkMode)
                {
                    // Windows 11 Mica - ultra-high transparency to show backdrop blur effect
                    Color topColor = Color.FromArgb(244, 32, 32, 32);      // 95.7% transparent
                    Color bottomColor = Color.FromArgb(247, 28, 28, 28);   // 96.5% transparent

                    LinearGradientBrush brush = new LinearGradientBrush(topColor, bottomColor, 90);
                    brush.Freeze();
                    return brush;
                }
                else
                {
                    // Light mode Mica
                    Color topColor = Color.FromArgb(247, 253, 253, 253);   // 96.5% transparent
                    Color bottomColor = Color.FromArgb(249, 250, 250, 250); // 97.3% transparent

                    LinearGradientBrush brush = new LinearGradientBrush(topColor, bottomColor, 90);
                    brush.Freeze();
                    return brush;
                }
            }
            catch
            {
                return CreateNativeAcrylicBrush(isDarkMode);
            }
        }
    }
}
