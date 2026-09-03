using Microsoft.Win32;
using System;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WeatherWidget.Models;
using WeatherWidget.Services;
using Wpf.Ui.Appearance;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;
using UiButton = Wpf.Ui.Controls.Button;
using UiNumberBox = Wpf.Ui.Controls.NumberBox;
using MessageBox = System.Windows.MessageBox;

namespace WeatherWidget.Views
{
    [SupportedOSPlatform("windows")]
    public partial class WeatherFlyout : FluentWindow
    {
        // DWM Mica backdrop interop
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_MAINWINDOW = 2; // Mica

        private readonly WeatherData _data;
        private readonly LocationData _loc;
        private bool _isDarkMode;
        private bool _allowDeactivate;
        private readonly Rect _anchorRect;
        private bool _settingsVisible;
        private double _initialBottom;
        private bool _anchoredAbove = true;
        private readonly bool _startInSettings;
        private bool _isClosing;
        public bool SettingsWereSaved { get; set; }
        private readonly GeocodingService _geocodingService = new();
        private CancellationTokenSource? _geocodeCts;

        // Reusable brush instances to avoid reallocation on every theme change
        private readonly SolidColorBrush _primaryTextBrush = new();
        private readonly SolidColorBrush _cardBorderBrush = new();
        private readonly SolidColorBrush _dividerBrush = new();
        private readonly SolidColorBrush _flyoutBackgroundBrush = new();
        private readonly SolidColorBrush _inputBackgroundBrush = new();
        private readonly SolidColorBrush _inputBorderBrush = new();
        private readonly SolidColorBrush _inputHoverBrush = new();
        private readonly SolidColorBrush _inputFocusedBrush = new();
        private readonly SolidColorBrush _glowColorBrush = new();
        private readonly LinearGradientBrush _cardBackgroundBrush = new()
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(), new GradientStop() }
        };

        public WeatherFlyout(WeatherData data, LocationData loc, Rect anchorRect, bool startInSettings = false)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(loc);

            TaskbarThemeService.ApplyTaskbarTheme();
            InitializeComponent();
            this.ShowInTaskbar = false;

            _anchorRect = anchorRect;
            _data = data;
            _loc = loc;
            _startInSettings = startInSettings;

            SynchronizeWithTaskbarTheme();
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            PopulateWeatherData(loc);

            if (_startInSettings)
            {
                ShowSettingsDirect();
            }
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
                card.Cursor = Cursors.Hand;
                card.MouseLeftButtonUp += (s, e) => OpenDayInWeatherApp(day.Date);

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
                        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/PNG/ui_precipitation.png")),
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

        private void OpenDayInWeatherApp(DateTime date)
        {
            try
            {
                // ms-weather protocol supported by the Windows Weather app
                var lat = _loc.Latitude.ToString(CultureInfo.InvariantCulture);
                var lon = _loc.Longitude.ToString(CultureInfo.InvariantCulture);
                // Use dt= with triple slash form; this is what the Windows Weather app deep link expects.
                var dateParam = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string uri = $"ms-weather:///forecast?dt={dateParam}&lat={lat}&long={lon}";

                if (IsProtocolRegistered("ms-weather"))
                {
                    // Launch the URI directly so ShellExecute resolves the custom protocol.
                    Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                }
                else if (IsProtocolRegistered("bingweather"))
                {
                    // Legacy protocol; may ignore the date query but still opens the app.
                    Process.Start(new ProcessStartInfo("bingweather:") { UseShellExecute = true });
                }
                else
                {
                    var result = MessageBox.Show(
                        "The Weather app isn't installed. Open Microsoft Store to install it?",
                        "Weather",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo("ms-windows-store://pdp/?PFN=Microsoft.BingWeather_8wekyb3d8bbwe")
                        {
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch
            {
                // Fallback silently if protocol not available
            }
        }

        private static bool IsProtocolRegistered(string scheme)
        {
            try
            {
                using var key = Registry.ClassesRoot.OpenSubKey(scheme);
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        private void SynchronizeWithTaskbarTheme()
        {
            var applicationTheme = TaskbarThemeService.ApplyTaskbarTheme();

            try
            {
                ApplicationThemeManager.Apply(this);
            }
            catch
            {
                // The custom flyout palette still updates even if WPF-UI skips a window-level refresh.
            }

            _isDarkMode = applicationTheme == ApplicationTheme.Dark;

            this.Background = Brushes.Transparent;
            if (_isDarkMode)
            {
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

        private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General
                && e.Category != UserPreferenceCategory.Color
                && e.Category != UserPreferenceCategory.VisualStyle)
            {
                return;
            }

            if (_isClosing || !IsLoaded)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(SynchronizeWithTaskbarTheme), System.Windows.Threading.DispatcherPriority.Normal);
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
            _primaryTextBrush.Color = textColor;
            this.Resources["PrimaryTextBrush"] = _primaryTextBrush;
            this.Resources["SecondaryTextBrush"] = _primaryTextBrush;

            byte topAlpha = isDarkMode ? (byte)100 : (byte)120;
            byte bottomAlpha = isDarkMode ? (byte)130 : (byte)150;
            _cardBackgroundBrush.GradientStops[0].Color = Color.FromArgb(topAlpha, cardBaseColor.R, cardBaseColor.G, cardBaseColor.B);
            _cardBackgroundBrush.GradientStops[1].Color = Color.FromArgb(bottomAlpha, cardBaseColor.R, cardBaseColor.G, cardBaseColor.B);
            this.Resources["CardBackgroundBrush"] = _cardBackgroundBrush;

            _cardBorderBrush.Color = cardBorderColor;
            this.Resources["CardBorderBrush"] = _cardBorderBrush;

            _dividerBrush.Color = dividerColor;
            this.Resources["DividerBrush"] = _dividerBrush;

            byte flyoutAlpha = isDarkMode ? (byte)26 : (byte)38;
            _flyoutBackgroundBrush.Color = Color.FromArgb(flyoutAlpha, flyoutBaseColor.R, flyoutBaseColor.G, flyoutBaseColor.B);
            this.Resources["FlyoutBackgroundBrush"] = _flyoutBackgroundBrush;

            var inputBg = OffsetColor(cardBaseColor, isDarkMode ? 10 : 20);
            var inputBorder = OffsetColor(cardBorderColor, isDarkMode ? 35 : 20);
            var inputHover = OffsetColor(cardBorderColor, isDarkMode ? 60 : 45);
            var inputFocused = OffsetColor(glowColor, isDarkMode ? -10 : -20);

            _inputBackgroundBrush.Color = inputBg;
            this.Resources["InputBackgroundBrush"] = _inputBackgroundBrush;

            _inputBorderBrush.Color = inputBorder;
            this.Resources["InputBorderBrush"] = _inputBorderBrush;

            _inputHoverBrush.Color = inputHover;
            this.Resources["InputHoverBrush"] = _inputHoverBrush;

            _inputFocusedBrush.Color = inputFocused;
            this.Resources["InputFocusedBrush"] = _inputFocusedBrush;

            _glowColorBrush.Color = glowColor;
            this.Resources["GlowColorBrush"] = _glowColorBrush;
        }

        private static Color OffsetColor(Color color, int delta)
        {
            byte r = (byte)Math.Clamp(color.R + delta, 0, 255);
            byte g = (byte)Math.Clamp(color.G + delta, 0, 255);
            byte b = (byte)Math.Clamp(color.B + delta, 0, 255);
            return Color.FromArgb(color.A, r, g, b);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Apply Mica backdrop via DWM
            ApplyMicaBackdrop();

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

        private void ApplyMicaBackdrop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                // Set dark/light mode
                int darkMode = _isDarkMode ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // Apply Mica backdrop
                int backdropType = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            catch
            {
                // Silently fail if DWM APIs are not available
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.HeightChanged) return;

            if (_anchoredAbove && _initialBottom > 0)
            {
                // Anchored above the taskbar/widget: keep the bottom edge fixed so the
                // flyout still "sits" against the anchor as its height shrinks/grows.
                this.Top = _initialBottom - e.NewSize.Height;
            }
            // else: anchored below the anchor — top edge is already fixed (this.Top
            // untouched), which is the correct pin point in that orientation.
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;

            double targetLeft = _anchorRect.Left + (_anchorRect.Width - this.ActualWidth) / 2;
            double targetTop;

            // Choose direction: prefer opening above the anchor, flip below if insufficient room
            double spaceAbove = _anchorRect.Top - workArea.Top;
            double spaceBelow = workArea.Bottom - _anchorRect.Bottom;

            if (spaceAbove >= this.ActualHeight + 16)
            {
                targetTop = _anchorRect.Top - this.ActualHeight - 16;
                _anchoredAbove = true;
            }
            else if (spaceBelow >= this.ActualHeight + 16)
            {
                targetTop = _anchorRect.Bottom + 16;
                _anchoredAbove = false;
            }
            else
            {
                targetTop = _anchorRect.Top - this.ActualHeight - 16;
                _anchoredAbove = true;
            }

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

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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

        private void ShowSettingsDirect()
        {
            _settingsVisible = true;

            WeatherView.Visibility = Visibility.Collapsed;
            WeatherView.Opacity = 0;

            CreateSettingsContent();

            SettingsView.Visibility = Visibility.Visible;
            SettingsView.Opacity = 1;
        }

        private void CreateSettingsContent()
        {
            var settings = AppSettingsStore.Load();
            SettingsContent.Children.Clear();

            var stackPanel = new StackPanel();

            // Header with back button
            var headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 24),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());

            var backButton = new UiButton
            {
                ToolTip = "Back",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Appearance = ControlAppearance.Secondary,
                Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowLeft24 },
                Content = string.Empty,
                Width = 36,
                Height = 36,
                Padding = new Thickness(6),
                Cursor = Cursors.Hand
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
                Margin = new Thickness(8, -2, 0, 0)
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
                Style = (Style)Resources["Win11CheckBox"]
            };

            locationSection.Children.Add(useManualCheck);

            // ── City / zip search ────────────────────────────────────────────────────
            // Selected suggestion — updated whenever the user picks from the dropdown.
            LocationSuggestion? selectedSuggestion = null;

            // Pre-populate from saved settings so the box shows the current city.
            string savedCity = settings.ManualCityName;
            double savedLat = settings.ManualLatitude;
            double savedLon = settings.ManualLongitude;

            var searchBox = new TextBox
            {
                Text = savedCity,
                Style = (Style)Resources["Win11TextBox"],
                IsEnabled = settings.UseManualLocation
            };

            // Status / hint text shown below the search box.
            var searchHint = new TextBlock
            {
                FontSize = 11,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.65,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Text = string.IsNullOrEmpty(savedCity)
                    ? "Type a city name or zip code to search."
                    : $"Saved: {savedCity}"
            };

            // Popup that holds the suggestion list.
            var suggestionList = new ListBox
            {
                MaxHeight = 180,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0, 4, 0, 4),
                Focusable = false,
                ItemContainerStyle = (Style)Resources["Win11SuggestionItem"]
            };
            suggestionList.SetResourceReference(ListBox.ForegroundProperty, "PrimaryTextBrush");

            var suggestionBorder = new Border
            {
                Child = suggestionList,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0)
            };
            suggestionBorder.SetResourceReference(Border.BackgroundProperty, "InputBackgroundBrush");
            suggestionBorder.SetResourceReference(Border.BorderBrushProperty, "InputBorderBrush");

            var suggestionPopup = new Popup
            {
                Child = suggestionBorder,
                StaysOpen = true,
                PlacementTarget = searchBox,
                Placement = PlacementMode.Bottom,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };

            // Bind popup width to the search box width.
            searchBox.SizeChanged += (s, e) =>
            {
                suggestionPopup.Width = searchBox.ActualWidth;
            };

            // Debounce token for geocoding.
            CancellationTokenSource? cts = null;

            searchBox.TextChanged += async (s, e) =>
            {
                string query = searchBox.Text;

                // If the user cleared the box, reset the selection.
                if (string.IsNullOrWhiteSpace(query))
                {
                    selectedSuggestion = null;
                    suggestionPopup.IsOpen = false;
                    searchHint.Text = "Type a city name or zip code to search.";
                    return;
                }

                // Cancel any in-flight request.
                cts?.Cancel();
                cts = new CancellationTokenSource();
                var token = cts.Token;

                try
                {
                    // Small debounce so we don't fire on every keystroke.
                    await Task.Delay(300, token);
                    if (token.IsCancellationRequested) return;

                    searchHint.Text = "Searching…";
                    var suggestions = await _geocodingService.SearchAsync(query, token);
                    if (token.IsCancellationRequested) return;

                    suggestionList.Items.Clear();

                    if (suggestions.Count == 0)
                    {
                        searchHint.Text = "No results found. Try a different city or zip code.";
                        suggestionPopup.IsOpen = false;
                        return;
                    }

                    searchHint.Text = "Select a location from the list below.";
                    foreach (var suggestion in suggestions)
                        suggestionList.Items.Add(suggestion);

                    _allowDeactivate = false;
                    suggestionPopup.IsOpen = true;
                }
                catch (OperationCanceledException) { }
                catch
                {
                    searchHint.Text = "Search failed. Check your connection and try again.";
                    suggestionPopup.IsOpen = false;
                }
            };

            suggestionList.MouseUp += (s, e) =>
            {
                if (suggestionList.SelectedItem is LocationSuggestion picked)
                {
                    selectedSuggestion = picked;
                    searchBox.Text = picked.DisplayName;
                    searchHint.Text = $"Selected: {picked.DisplayName}";
                    suggestionPopup.IsOpen = false;
                    _allowDeactivate = true;
                }
            };

            // Close popup if the user clicks away inside the flyout.
            searchBox.LostFocus += (s, e) =>
            {
                // Short delay so a click on the popup list is processed first.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!suggestionBorder.IsMouseOver)
                    {
                        suggestionPopup.IsOpen = false;
                        _allowDeactivate = true;
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            };

            useManualCheck.Checked += (s, e) => { searchBox.IsEnabled = true; };
            useManualCheck.Unchecked += (s, e) => { searchBox.IsEnabled = false; suggestionPopup.IsOpen = false; _allowDeactivate = true; };

            locationSection.Children.Add(searchBox);
            locationSection.Children.Add(suggestionPopup);
            locationSection.Children.Add(searchHint);

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
                IsChecked = StartupService.IsEnabled(),
                Margin = new Thickness(0, 0, 0, 6),
                Style = (Style)Resources["Win11CheckBox"]
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
            buttonGrid.Margin = new Thickness(0, 12, 0, 0);

            var saveButton = new UiButton
            {
                Content = "Save",
                Appearance = ControlAppearance.Primary,
                Icon = new SymbolIcon { Symbol = SymbolRegular.Save24 },
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };

            saveButton.Click += (s, e) =>
            {
                try
                {
                    if (useManualCheck.IsChecked == true)
                    {
                        // Prefer an explicitly-selected suggestion; fall back to the
                        // previously-saved values when the user left the text unchanged.
                        double latValue;
                        double lonValue;
                        string cityName;

                        if (selectedSuggestion != null)
                        {
                            latValue = selectedSuggestion.Latitude;
                            lonValue = selectedSuggestion.Longitude;
                            cityName = selectedSuggestion.DisplayName;
                        }
                        else if (!string.IsNullOrWhiteSpace(savedCity)
                                 && searchBox.Text.Equals(savedCity, StringComparison.OrdinalIgnoreCase))
                        {
                            latValue = savedLat;
                            lonValue = savedLon;
                            cityName = savedCity;
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "Please select a location from the dropdown list before saving.");
                        }

                        settings.UseManualLocation = true;
                        settings.ManualLatitude = latValue;
                        settings.ManualLongitude = lonValue;
                        settings.ManualCityName = cityName;
                    }
                    else
                    {
                        settings.UseManualLocation = false;
                        settings.ManualCityName = "";
                    }

                    settings.StartWithWindows = startupCheck.IsChecked == true;
                    StartupService.SetEnabled(settings.StartWithWindows);
                    AppSettingsStore.Save(settings);

                    SettingsWereSaved = true;
                    _isClosing = true;
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
            if (_allowDeactivate && !_isClosing)
            {
                _isClosing = true;
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

        protected override void OnClosed(EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnClosed(e);
        }

    }
}
