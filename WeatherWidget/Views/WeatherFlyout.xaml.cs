using Microsoft.Win32;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WeatherWidget.Models;

namespace WeatherWidget.Views
{
    public partial class WeatherFlyout : Window
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        // Windows 11 DWM attributes
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // Backdrop types
        private const int DWMSBT_AUTO = 0;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2; // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed Mica

        // Corner preferences
        private const int DWMWCP_DEFAULT = 0;
        private const int DWMWCP_DONOTROUND = 1;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWCP_ROUNDSMALL = 3;

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
            CityText.Text = loc.City;
            MainTemp.Text = Math.Round(_data.Temperature) + "°";
            ConditionFull.Text = _data.Condition;
            FeelsLikeText.Text = $"Feels like {Math.Round(_data.FeelsLike)}°";

            HighLowTempText.Text = $"{Math.Round(_data.HighTemp)}° / {Math.Round(_data.LowTemp)}°";
            PrecipitationText.Text = _data.PrecipitationChance + "%";
            HumidityText.Text = _data.CurrentHumidity + "%";
            WindText.Text = Math.Round(_data.CurrentWindSpeed) + " mph";

            try
            {
                LargeIcon.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/PNG/{_data.IconCode}.png"));
            }
            catch { }

            PopulateDailyForecast();
        }

        private void PopulateDailyForecast()
        {
            DailyForecastGrid.Children.Clear();

            foreach (var day in _data.Daily)
            {
                var card = new Border
                {
                    Background = (SolidColorBrush)Resources["CardBackgroundBrush"],
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var stackPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                stackPanel.Children.Add(new TextBlock
                {
                    Text = day.TimeLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                try
                {
                    var icon = new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,{day.IconPath}")),
                        Width = 40,
                        Height = 40,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 17)
                    };
                    icon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    stackPanel.Children.Add(icon);
                }
                catch { }

                stackPanel.Children.Add(new TextBlock
                {
                    Text = day.TempLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 12),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });

                var detailsStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var precipStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                };

                try
                {
                    var precipIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/PNG/ui-rain.png")),
                        Width = 12,
                        Height = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    precipIcon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    precipStack.Children.Add(precipIcon);
                }
                catch { }

                precipStack.Children.Add(new TextBlock
                {
                    Text = day.Humidity,
                    FontSize = 11,
                    Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });

                detailsStack.Children.Add(precipStack);

                var windStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                try
                {
                    var windIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("pack://application:,,,/Assets/PNG/ui-wind.png")),
                        Width = 12,
                        Height = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
                    windIcon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    windStack.Children.Add(windIcon);
                }
                catch { }

                windStack.Children.Add(new TextBlock
                {
                    Text = day.Wind,
                    FontSize = 11,
                    Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center
                });

                detailsStack.Children.Add(windStack);

                stackPanel.Children.Add(detailsStack);
                card.Child = stackPanel;

                DailyForecastGrid.Children.Add(card);
            }

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
                    this.Background = new SolidColorBrush(Color.FromArgb(235, 32, 32, 32));
                    UpdateThemeResources(Color.FromRgb(237, 237, 237), Color.FromRgb(42, 42, 42));
                }
                else
                {
                    this.Background = new SolidColorBrush(Color.FromArgb(235, 243, 243, 243));
                    UpdateThemeResources(Color.FromRgb(32, 32, 32), Color.FromRgb(250, 250, 250));
                }
            }
            catch
            {
                _isDarkMode = true;
                this.Background = new SolidColorBrush(Color.FromArgb(235, 32, 32, 32));
                UpdateThemeResources(Color.FromRgb(237, 237, 237), Color.FromRgb(42, 42, 42));
            }
        }

        private void UpdateThemeResources(Color textColor, Color cardColor)
        {
            this.Resources["PrimaryTextBrush"] = new SolidColorBrush(textColor);
            this.Resources["SecondaryTextBrush"] = new SolidColorBrush(textColor);
            this.Resources["CardBackgroundBrush"] = new SolidColorBrush(cardColor);
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
                Content = "←",
                FontSize = 20,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Cursor = Cursors.Hand,
                Padding = new Thickness(8, 4, 12, 4),
                VerticalAlignment = VerticalAlignment.Center
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

            // Use Win11 checkbox style
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

            // Use Win11 textbox style
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

            // Use Win11 textbox style
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

            // Use Win11 checkbox style
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

            // Use Win11 accent button style
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
    }
}