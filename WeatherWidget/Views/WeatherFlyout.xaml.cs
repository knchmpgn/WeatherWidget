using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
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

        private readonly WeatherData _data = null!;
        private readonly LocationData _loc = null!;
        private bool _isDarkMode;
        private bool _allowDeactivate = false;
        private readonly Rect _anchorRect;
        private bool _showingDaily = true;
        private bool _settingsVisible = false;
        private double _initialBottom;
        private bool _isDay = true;

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

            // Determine if it's day or night from sunrise/sunset
            DetermineTimeOfDay();

            DetectTaskbarTheme();
            PopulateWeatherData(loc);
        }

        private void DetermineTimeOfDay()
        {
            try
            {
                var now = DateTime.Now;
                var sunrise = DateTime.Parse(_data.Sunrise);
                var sunset = DateTime.Parse(_data.Sunset);

                _isDay = now >= sunrise && now < sunset;
            }
            catch
            {
                // Default to day if parsing fails
                _isDay = true;
            }
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

            DailyForecastList.ItemsSource = _data.Daily;
            HourlyForecastList.ItemsSource = _data.Hourly;
        }

        private void ToggleForecast_Click(object sender, RoutedEventArgs e)
        {
            _showingDaily = !_showingDaily;

            if (_showingDaily)
            {
                DailyForecastList.Visibility = Visibility.Visible;
                HourlyForecastList.Visibility = Visibility.Collapsed;
            }
            else
            {
                DailyForecastList.Visibility = Visibility.Collapsed;
                HourlyForecastList.Visibility = Visibility.Visible;
            }
        }

        private void DetectTaskbarTheme()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object? value = key?.GetValue("SystemUsesLightTheme");
                    _isDarkMode = value == null || value.ToString() == "0";
                }

                if (_isDarkMode)
                {
                    this.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
                    UpdateThemeResources(Color.FromRgb(237, 237, 237), Color.FromRgb(42, 42, 42));
                }
                else
                {
                    this.Background = new SolidColorBrush(Color.FromRgb(243, 243, 243));
                    UpdateThemeResources(Color.FromRgb(32, 32, 32), Color.FromRgb(250, 250, 250));
                }
            }
            catch
            {
                _isDarkMode = true;
                this.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
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
                const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
                const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
                const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

                int dark = _isDarkMode ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int roundCorners = 2;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorners, sizeof(int));

                int micaBackdrop = 2;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref micaBackdrop, sizeof(int));

                if (result != 0)
                {
                    MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                    DwmExtendFrameIntoClientArea(hwnd, ref margins);

                    var bg = this.Background as SolidColorBrush;
                    if (bg != null)
                    {
                        var color = bg.Color;
                        this.Background = new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B));
                    }
                }
            }
            catch
            {
                var bg = this.Background as SolidColorBrush;
                if (bg != null)
                {
                    var color = bg.Color;
                    this.Background = new SolidColorBrush(Color.FromArgb(245, color.R, color.G, color.B));
                }
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
                // Keep bottom position fixed, adjust top to grow upward
                this.Top = _initialBottom - e.NewSize.Height;
            }
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;

            double targetLeft = _anchorRect.Left + (_anchorRect.Width - this.ActualWidth) / 2;
            double targetTop = _anchorRect.Top - this.ActualHeight - 12;

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
                // Fade out weather view
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, a) =>
                {
                    WeatherView.Visibility = Visibility.Collapsed;
                    CreateSettingsContent();
                    SettingsView.Visibility = Visibility.Visible;

                    // Fade in settings view
                    SettingsView.Opacity = 0;
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                    SettingsView.BeginAnimation(OpacityProperty, fadeIn);
                };
                WeatherView.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                // Fade out settings view
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
                fadeOut.Completed += (s, a) =>
                {
                    SettingsView.Visibility = Visibility.Collapsed;
                    WeatherView.Visibility = Visibility.Visible;

                    // Fade in weather view
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

            var stackPanel = new System.Windows.Controls.StackPanel();

            // Header with back button
            var headerGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, -6, 0, 20) };
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());

            var backButton = new System.Windows.Controls.Button
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
            System.Windows.Controls.Grid.SetColumn(backButton, 0);
            headerGrid.Children.Add(backButton);

            var headerText = new System.Windows.Controls.TextBlock
            {
                Text = "Settings",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            System.Windows.Controls.Grid.SetColumn(headerText, 1);
            headerGrid.Children.Add(headerText);

            stackPanel.Children.Add(headerGrid);

            // Location section
            var locationSection = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 20) };

            var locationHeader = new System.Windows.Controls.TextBlock
            {
                Text = "Location",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 12)
            };
            locationSection.Children.Add(locationHeader);

            var useManualCheck = new System.Windows.Controls.CheckBox
            {
                Content = "Use manual location",
                IsChecked = settings.UseManualLocation,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"]
            };
            locationSection.Children.Add(useManualCheck);

            var locationGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 8) };
            locationGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            locationGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(16) });
            locationGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());

            var latStack = new System.Windows.Controls.StackPanel();
            latStack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Latitude",
                FontSize = 12,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 6)
            });
            var latBox = new System.Windows.Controls.TextBox
            {
                Text = settings.ManualLatitude.ToString(),
                Padding = new Thickness(10, 8, 10, 8),
                IsEnabled = settings.UseManualLocation,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            latStack.Children.Add(latBox);
            System.Windows.Controls.Grid.SetColumn(latStack, 0);
            locationGrid.Children.Add(latStack);

            var lonStack = new System.Windows.Controls.StackPanel();
            lonStack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Longitude",
                FontSize = 12,
                Foreground = (SolidColorBrush)Resources["SecondaryTextBrush"],
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 6)
            });
            var lonBox = new System.Windows.Controls.TextBox
            {
                Text = settings.ManualLongitude.ToString(),
                Padding = new Thickness(10, 8, 10, 8),
                IsEnabled = settings.UseManualLocation,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            lonStack.Children.Add(lonBox);
            System.Windows.Controls.Grid.SetColumn(lonStack, 2);
            locationGrid.Children.Add(lonStack);

            useManualCheck.Checked += (s, e) => { latBox.IsEnabled = true; lonBox.IsEnabled = true; };
            useManualCheck.Unchecked += (s, e) => { latBox.IsEnabled = false; lonBox.IsEnabled = false; };

            locationSection.Children.Add(locationGrid);

            var helpText = new System.Windows.Controls.TextBlock
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
            var startupSection = new System.Windows.Controls.StackPanel { Margin = new Thickness(0, 0, 0, 24) };

            var startupHeader = new System.Windows.Controls.TextBlock
            {
                Text = "Startup",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 12)
            };
            startupSection.Children.Add(startupHeader);

            var startupCheck = new System.Windows.Controls.CheckBox
            {
                Content = "Start with Windows",
                IsChecked = settings.StartWithWindows,
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 6)
            };
            startupSection.Children.Add(startupCheck);

            var startupHelp = new System.Windows.Controls.TextBlock
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
            var buttonGrid = new System.Windows.Controls.Grid();
            buttonGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
            buttonGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

            var saveButton = new System.Windows.Controls.Button
            {
                Content = "Save",
                Padding = new Thickness(24, 10, 24, 10),
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };

            // Create rounded corner template
            var template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button));
            var factory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            factory.SetValue(System.Windows.Controls.Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Button.BackgroundProperty));
            factory.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(4));
            factory.SetValue(System.Windows.Controls.Border.PaddingProperty, new TemplateBindingExtension(System.Windows.Controls.Button.PaddingProperty));
            var contentFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentFactory);
            template.VisualTree = factory;
            saveButton.Template = template;

            saveButton.Click += (s, e) =>
            {
                if (useManualCheck.IsChecked == true)
                {
                    if (double.TryParse(latBox.Text, out double lat) && double.TryParse(lonBox.Text, out double lon))
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

                // Fade back to weather view
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
            };
            System.Windows.Controls.Grid.SetColumn(saveButton, 1);
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