using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WeatherWidget.Helpers;
using WeatherWidget.Models;
using WeatherWidget.Services;

namespace WeatherWidget.Views
{
    [SupportedOSPlatform("windows")]
    public partial class WeatherFlyout : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ACCENT_POLICY
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private enum WINDOWCOMPOSITIONATTRIB
        {
            WCA_ACCENT_POLICY = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public WINDOWCOMPOSITIONATTRIB Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        // Windows 11 DWM attributes
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // Backdrop types
        private const int DWMSBT_MAINWINDOW = 2; // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic

        // Corner preferences
        private const int DWMWCP_ROUND = 2;

        private readonly WeatherData _data = null!;
        private readonly LocationData _loc = null!;
        private bool _isDarkMode;
        private bool _allowDeactivate;
        private readonly Rect _anchorRect;
        private bool _settingsVisible;
        private double _initialBottom;

        public WeatherFlyout(WeatherData data, LocationData loc, Rect anchorRect)
        {
            InitializeComponent();
            this.ShowInTaskbar = false;

            if (data == null || loc == null)
            {
                this.Close();
                return;
            }

            _anchorRect = anchorRect;
            _data = data;
            _loc = loc;

            DetectTaskbarTheme();
            PopulateWeatherData(loc);
        }

        private void PopulateWeatherData(LocationData loc)
        {
            // Location name inside card
            CityText.Text = loc.City;

            // Current Conditions
            MainTemp.Text = Math.Round(_data.Temperature) + "°";
            ConditionFull.Text = _data.Condition;
            FeelsLikeText.Text = $"Feels like {Math.Round(_data.FeelsLike)}°";
            HighTempText.Text = $"{Math.Round(_data.HighTemp)}°";
            LowTempText.Text = $"{Math.Round(_data.LowTemp)}°";

            try
            {
                LargeIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/PNG/{_data.IconCode}.png"));
            }
            catch { }

            // Weather Metrics
            PrecipitationText.Text = _data.PrecipitationChance + "%";
            HumidityText.Text = _data.CurrentHumidity + "%";
            WindText.Text = Math.Round(_data.CurrentWindSpeed) + " mph";

            // UV Index
            UVIndexText.Text = _data.UVIndex.ToString("F1");

            // Sunrise/Sunset
            try
            {
                if (!string.IsNullOrEmpty(_data.Sunrise))
                {
                    var sunriseTime = DateTime.Parse(_data.Sunrise);
                    SunriseInlineText.Text = sunriseTime.ToString("h:mm tt");
                }
                else
                {
                    SunriseInlineText.Text = "--";
                }
            }
            catch
            {
                SunriseInlineText.Text = "--";
            }

            try
            {
                if (!string.IsNullOrEmpty(_data.Sunset))
                {
                    var sunsetTime = DateTime.Parse(_data.Sunset);
                    SunsetInlineText.Text = sunsetTime.ToString("h:mm tt");
                }
                else
                {
                    SunsetInlineText.Text = "--";
                }
            }
            catch
            {
                SunsetInlineText.Text = "--";
            }

            // Pressure in inHg (convert from hPa)
            if (_data.Pressure > 0)
            {
                PressureText.Text = (_data.Pressure * 0.02953).ToString("F2") + " inHg";
            }
            else
            {
                PressureText.Text = "--";
            }

            // Cloud Cover
            CloudCoverText.Text = _data.CloudCover + "%";

            // 5-Day Forecast
            PopulateDailyForecast();
        }

        private void PopulateDailyForecast()
        {
            DailyForecastGrid.Children.Clear();

            foreach (var day in _data.Daily)
            {
                var transformGroup = new TransformGroup();
                var scale = new ScaleTransform(1, 1);
                var translate = new TranslateTransform(0, 12);
                transformGroup.Children.Add(scale);
                transformGroup.Children.Add(translate);

                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 14, 12, 14),
                    Margin = new Thickness(0, 0, 8, 0),
                    Opacity = 0,
                    RenderTransform = transformGroup,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
                card.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
                card.MouseEnter += ForecastCard_MouseEnter;
                card.MouseLeave += ForecastCard_MouseLeave;

                var stackPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Day name
                var dayLabel = new TextBlock
                {
                    Text = day.TimeLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                dayLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
                stackPanel.Children.Add(dayLabel);

                // Weather icon
                try
                {
                    var icon = new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,{day.IconPath}")),
                        Width = 44,
                        Height = 44,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    icon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    stackPanel.Children.Add(icon);
                }
                catch { }

                // Temperature range
                var tempLabel = new TextBlock
                {
                    Text = day.TempLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 12),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                };
                tempLabel.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
                stackPanel.Children.Add(tempLabel);

                // Divider line
                var divider = new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                divider.SetResourceReference(Border.BackgroundProperty, "DividerBrush");
                stackPanel.Children.Add(divider);

                // Precipitation chance
                var precipStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                };

                try
                {
                    var precipIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/PNG/ui_water.png")),
                        Width = 12,
                        Height = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    precipIcon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    precipStack.Children.Add(precipIcon);
                }
                catch { }

                var precipText = new TextBlock
                {
                    Text = day.Humidity,
                    FontSize = 11,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                };
                precipText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                precipStack.Children.Add(precipText);

                stackPanel.Children.Add(precipStack);

                // Wind speed
                var windStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                try
                {
                    var windIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/PNG/ui_wind.png")),
                        Width = 12,
                        Height = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    windIcon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    windStack.Children.Add(windIcon);
                }
                catch { }

                var windText = new TextBlock
                {
                    Text = day.Wind,
                    FontSize = 11,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                };
                windText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                windStack.Children.Add(windText);

                stackPanel.Children.Add(windStack);

                card.Child = stackPanel;
                DailyForecastGrid.Children.Add(card);
            }

            // Remove right margin from last card
            if (DailyForecastGrid.Children.Count > 0)
            {
                if (DailyForecastGrid.Children[^1] is Border lastCard)
                {
                    lastCard.Margin = new Thickness(0);
                }
            }
        }

        private void DetectTaskbarTheme()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object? value = key?.GetValue("SystemUsesLightTheme");
                _isDarkMode = value == null || value.ToString() == "0";

                if (_isDarkMode)
                {
                    this.Background = Brushes.Transparent;
                    UpdateThemeResources(
                        Color.FromRgb(237, 237, 237),
                        Color.FromRgb(38, 38, 38),
                        Color.FromRgb(55, 55, 55),
                        Color.FromRgb(52, 52, 52),
                        Color.FromRgb(26, 26, 26),
                        Color.FromArgb(140, 255, 255, 255),
                        true);
                }
                else
                {
                    this.Background = Brushes.Transparent;
                    UpdateThemeResources(
                        Color.FromRgb(32, 32, 32),
                        Color.FromRgb(255, 255, 255),
                        Color.FromRgb(230, 230, 230),
                        Color.FromRgb(220, 220, 220),
                        Color.FromRgb(250, 250, 250),
                        Color.FromArgb(120, 0, 0, 0),
                        false);
                }
            }
            catch
            {
                _isDarkMode = true;
                this.Background = Brushes.Transparent;
                UpdateThemeResources(
                    Color.FromRgb(237, 237, 237),
                    Color.FromRgb(38, 38, 38),
                    Color.FromRgb(55, 55, 55),
                    Color.FromRgb(52, 52, 52),
                    Color.FromRgb(26, 26, 26),
                    Color.FromArgb(140, 255, 255, 255),
                    true);
            }
        }

        private void ForecastCard_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border card)
            {
                AnimateForecastHover(card, -2, 1.02, 150);
            }
        }

        private void ForecastCard_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border card)
            {
                AnimateForecastHover(card, 0, 1.0, 160);
            }
        }

        private static void AnimateForecastHover(Border card, double targetY, double targetScale, int durationMs)
        {
            if (card.RenderTransform is not TransformGroup group)
            {
                return;
            }

            var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault();

            if (scale == null || translate == null)
            {
                return;
            }

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = ease });
        }

        private void UpdateThemeResources(
            Color textColor,
            Color cardBaseColor,
            Color dividerColor,
            Color cardBorderColor,
            Color flyoutBaseColor,
            Color glowColor,
            bool isDarkMode)
        {
            this.Resources["PrimaryTextBrush"] = new SolidColorBrush(textColor);
            this.Resources["SecondaryTextBrush"] = new SolidColorBrush(textColor);
            this.Resources["CardBackgroundBrush"] = CreateAcrylicBrush(cardBaseColor, isDarkMode ? (byte)175 : (byte)200, isDarkMode ? (byte)200 : (byte)220);
            this.Resources["CardBorderBrush"] = new SolidColorBrush(cardBorderColor);
            this.Resources["DividerBrush"] = new SolidColorBrush(dividerColor);
            
            // Use enhanced native Acrylic brush for flyout background
            try
            {
                this.Resources["FlyoutBackgroundBrush"] = NativeAcrylicHelper.CreateEnhancedAcrylicBrush(isDarkMode);
            }
            catch
            {
                // Fallback to native Acrylic
                try
                {
                    this.Resources["FlyoutBackgroundBrush"] = NativeAcrylicHelper.CreateNativeAcrylicBrush(isDarkMode);
                }
                catch
                {
                    // Final fallback to regular gradient
                    this.Resources["FlyoutBackgroundBrush"] = CreateAcrylicBrush(flyoutBaseColor, isDarkMode ? (byte)190 : (byte)200, isDarkMode ? (byte)220 : (byte)225);
                }
            }
            
            this.Resources["GlowColorBrush"] = new SolidColorBrush(glowColor);
        }

        private static LinearGradientBrush CreateAcrylicBrush(Color baseColor, byte topAlpha, byte bottomAlpha)
        {
            var top = Color.FromArgb(topAlpha, baseColor.R, baseColor.G, baseColor.B);
            var bottom = Color.FromArgb(bottomAlpha, baseColor.R, baseColor.G, baseColor.B);
            var brush = new LinearGradientBrush(top, bottom, 90);
            brush.Freeze();
            return brush;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            try
            {
                // Set dark mode preference
                int darkMode = _isDarkMode ? 1 : 0;
                _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Set rounded corners (Windows 11 style)
                int cornerPreference = DWMWCP_ROUND;
                _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

                // Try to enable Mica backdrop (Windows 11 22H2+)
                int backdropType = DWMSBT_MAINWINDOW; // Mica
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

                // If Mica is not available, try Acrylic
                if (result != 0)
                {
                    backdropType = DWMSBT_TRANSIENTWINDOW; // Acrylic
                    result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                }

                // If both fail, extend frame for blur effect
                if (result != 0)
                {
                    MARGINS margins = new()
                    {
                        cxLeftWidth = -1,
                        cxRightWidth = -1,
                        cyTopHeight = -1,
                        cyBottomHeight = -1
                    };
                    _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
                }

                // Apply acrylic blur behind as a reliable fallback for dynamic wallpaper blur.
                EnableFakeAcrylic(hwnd);
            }
            catch
            {
                // Fallback: just use semi-transparent background
            }

            PositionWindow();
            _initialBottom = this.Top + this.ActualHeight;

            var slideAnimation = new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(450))
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideAnimation);

            AnimateEntryCards();

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                _allowDeactivate = true;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_initialBottom > 0 && e.HeightChanged)
            {
                this.Top = _initialBottom - e.NewSize.Height;
            }
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;

            // Windows 11 Quick Settings has ~16px gap from taskbar
            double targetLeft = _anchorRect.Left + (_anchorRect.Width - this.ActualWidth) / 2;
            double targetTop = _anchorRect.Top - this.ActualHeight - 16;

            if (targetLeft < workArea.Left + 10)
                targetLeft = workArea.Left + 10;

            if (targetLeft + this.ActualWidth > workArea.Right - 10)
                targetLeft = workArea.Right - this.ActualWidth - 10;

            if (targetTop < workArea.Top + 10)
                targetTop = workArea.Top + 10;

            this.Left = targetLeft;
            this.Top = targetTop;
        }

        private void OpenWeatherApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("bingweather:")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsVisible = !_settingsVisible;

            if (_settingsVisible)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, a) =>
                {
                    WeatherView.Visibility = Visibility.Collapsed;
                    CreateSettingsContent();
                    SettingsView.Visibility = Visibility.Visible;

                    SettingsView.Opacity = 0;
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                    SettingsView.BeginAnimation(OpacityProperty, fadeIn);
                };
                WeatherView.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, a) =>
                {
                    SettingsView.Visibility = Visibility.Collapsed;
                    WeatherView.Visibility = Visibility.Visible;

                    WeatherView.Opacity = 0;
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                    WeatherView.BeginAnimation(OpacityProperty, fadeIn);
                };
                SettingsView.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void CreateSettingsContent()
        {
            var settings = Properties.Settings.Default;
            SettingsContent.Children.Clear();

            var stackPanel = new StackPanel();

            // Header with back button
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 24)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var backButton = new Button
            {
                Style = (Style)Resources["IconButtonStyle"],
                ToolTip = "Back",
                Content = new TextBlock
                {
                    Text = "\uE72B",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                    Opacity = 0.8
                }
            };
            backButton.Click += (s, e) => OpenSettings_Click(s, e);
            Grid.SetColumn(backButton, 0);
            headerGrid.Children.Add(backButton);

            var headerText = new TextBlock
            {
                Text = "Settings",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(headerText, 1);
            headerGrid.Children.Add(headerText);

            stackPanel.Children.Add(headerGrid);

            // Location section
            var locationSection = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 24)
            };

            var locationHeader = new TextBlock
            {
                Text = "Location",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 12)
            };
            locationSection.Children.Add(locationHeader);

            var useManualCheck = new CheckBox
            {
                Content = "Use manual location",
                IsChecked = settings.UseManualLocation,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Style = (Style)Application.Current.FindResource("Win11CheckBox")
            };

            locationSection.Children.Add(useManualCheck);

            var locationGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            locationGrid.ColumnDefinitions.Add(new ColumnDefinition());
            locationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            locationGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var latStack = new StackPanel();
            latStack.Children.Add(new TextBlock
            {
                Text = "Latitude",
                FontSize = 12,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var latBox = new TextBox
            {
                Text = settings.ManualLatitude.ToString(CultureInfo.InvariantCulture),
                IsEnabled = settings.UseManualLocation,
                Style = (Style)Application.Current.FindResource("Win11TextBox")
            };

            latStack.Children.Add(latBox);
            Grid.SetColumn(latStack, 0);
            locationGrid.Children.Add(latStack);

            var lonStack = new StackPanel();
            lonStack.Children.Add(new TextBlock
            {
                Text = "Longitude",
                FontSize = 12,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var lonBox = new TextBox
            {
                Text = settings.ManualLongitude.ToString(CultureInfo.InvariantCulture),
                IsEnabled = settings.UseManualLocation,
                Style = (Style)Application.Current.FindResource("Win11TextBox")
            };

            lonStack.Children.Add(lonBox);
            Grid.SetColumn(lonStack, 2);
            locationGrid.Children.Add(lonStack);

            useManualCheck.Checked += (s, e) => { latBox.IsEnabled = true; lonBox.IsEnabled = true; };
            useManualCheck.Unchecked += (s, e) => { latBox.IsEnabled = false; lonBox.IsEnabled = false; };

            locationSection.Children.Add(locationGrid);

            var helpText = new TextBlock
            {
                Text = "When disabled, location is detected automatically from your IP address.",
                FontSize = 11,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            locationSection.Children.Add(helpText);

            stackPanel.Children.Add(locationSection);

            // Startup section
            var startupSection = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 24)
            };

            var startupHeader = new TextBlock
            {
                Text = "Startup",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 12)
            };
            startupSection.Children.Add(startupHeader);

            var startupCheck = new CheckBox
            {
                Content = "Start with Windows",
                IsChecked = settings.StartWithWindows,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 6),
                Style = (Style)Application.Current.FindResource("Win11CheckBox")
            };

            startupSection.Children.Add(startupCheck);

            var startupHelp = new TextBlock
            {
                Text = "The widget will appear automatically in your taskbar on startup.",
                FontSize = 11,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 0, 0, 0)
            };
            startupSection.Children.Add(startupHelp);

            stackPanel.Children.Add(startupSection);

            // Buttons
            var buttonGrid = new Grid();
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition());
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var saveButton = new Button
            {
                Content = "Save",
                Style = (Style)Application.Current.FindResource("Win11AccentButton")
            };

            saveButton.Click += (s, e) =>
            {
                try
                {
                    if (useManualCheck.IsChecked == true)
                    {
                        if (double.TryParse(latBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                            double.TryParse(lonBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                        {
                            settings.UseManualLocation = true;
                            settings.ManualLatitude = lat;
                            settings.ManualLongitude = lon;
                        }
                    }
                    else
                    {
                        settings.UseManualLocation = false;
                    }

                    settings.StartWithWindows = startupCheck.IsChecked == true;
                    settings.Save();

                    _settingsVisible = false;

                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                    fadeOut.Completed += (sender, args) =>
                    {
                        SettingsView.Visibility = Visibility.Collapsed;
                        WeatherView.Visibility = Visibility.Visible;
                        WeatherView.Opacity = 0;
                        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                        fadeIn.Completed += (s2, a2) => this.Close();
                        WeatherView.BeginAnimation(OpacityProperty, fadeIn);
                    };
                    SettingsView.BeginAnimation(OpacityProperty, fadeOut);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            Grid.SetColumn(saveButton, 1);
            buttonGrid.Children.Add(saveButton);

            stackPanel.Children.Add(buttonGrid);

            SettingsContent.Children.Add(stackPanel);
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_allowDeactivate)
            {
                this.Close();
            }
        }

        private void AnimateEntryCards()
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                AnimateElement(CurrentConditionsCard, 40);
                AnimateElement(MetricsCard, 140);

                int index = 0;
                foreach (var card in DailyForecastGrid.Children.OfType<Border>())
                {
                    AnimateElement(card, 240 + (index * 40));
                    index++;
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void AnimateElement(UIElement element, int delayMs)
        {
            if (element == null)
            {
                return;
            }

            TranslateTransform transform = EnsureTranslateTransform(element);
            double fromY = transform.Y == 0 ? 12 : transform.Y;

            element.Opacity = 0;

            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            var moveAnim = new DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(300))
            {
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(OpacityProperty, opacityAnim);
            transform.BeginAnimation(TranslateTransform.YProperty, moveAnim);
        }

        private static TranslateTransform EnsureTranslateTransform(UIElement element)
        {
            if (element.RenderTransform is TranslateTransform translate)
            {
                return translate;
            }

            if (element.RenderTransform is TransformGroup group)
            {
                var existing = group.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (existing != null)
                {
                    return existing;
                }

                var added = new TranslateTransform(0, 12);
                group.Children.Add(added);
                return added;
            }

            var created = new TranslateTransform(0, 12);
            element.RenderTransform = created;
            return created;
        }

        private void EnableFakeAcrylic(IntPtr hwnd)
        {
            var tint = _isDarkMode
                ? Color.FromArgb(180, 24, 24, 24)
                : Color.FromArgb(160, 245, 245, 245);

            int gradientColor = (tint.A << 24) | (tint.B << 16) | (tint.G << 8) | tint.R;
            var accent = new ACCENT_POLICY
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 2,
                GradientColor = gradientColor,
                AnimationId = 0
            };

            int size = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);
                var data = new WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                    SizeOfData = size,
                    Data = accentPtr
                };
                _ = SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
    }
}
