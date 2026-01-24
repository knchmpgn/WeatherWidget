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

            // Populate 5-day forecast
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

                // Day label
                stackPanel.Children.Add(new TextBlock
                {
                    Text = day.TimeLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                // Weather icon - Now using the IconPath which includes day/night variants
                try
                {
                    var icon = new Image
                    {
                        Source = new BitmapImage(new Uri($"pack://application:,,,{day.IconPath}")),
                        Width = 40,
                        Height = 40,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    icon.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                    stackPanel.Children.Add(icon);
                }
                catch { }

                // Temperature
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

                // Details stack
                var detailsStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Precipitation
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

                // Wind
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

                // Set dark mode
                int dark = _isDarkMode ? 1 : 0;
                _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                // Set rounded corners
                int roundCorners = 2; // DWMWCP_ROUND
                _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref roundCorners, sizeof(int));

                // Try to set Mica backdrop (value 2 = DWMSBT_MAINWINDOW for Mica)
                int micaBackdrop = 2;
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref micaBackdrop, sizeof(int));

                // If Mica fails or needs DWM extension, extend frame
                if (result != 0)
                {
                    MARGINS margins = new() { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                    _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);
                }

                // Make window background semi-transparent for better backdrop visibility
                if (this.Background is SolidColorBrush bg)
                {
                    var color = bg.Color;
                    // Reduce opacity for better Mica/Acrylic effect visibility
                    this.Background = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B));
                }
            }
            catch
            {
                // Fallback to semi-transparent background
                if (this.Background is SolidColorBrush bg)
                {
                    var color = bg.Color;
                    this.Background = new SolidColorBrush(Color.FromArgb(240, color.R, color.G, color.B));
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

            var useManualCheck = new CheckBox
            {
                Content = "Use manual location",
                IsChecked = settings.UseManualLocation,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Style = CreateRoundedCheckBoxStyle()
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
                Padding = new Thickness(12, 10, 12, 10),
                IsEnabled = settings.UseManualLocation,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Background = new SolidColorBrush(_isDarkMode ? Color.FromRgb(42, 42, 42) : Color.FromRgb(255, 255, 255)),
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Style = CreateRoundedTextBoxStyle()
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
                Padding = new Thickness(12, 10, 12, 10),
                IsEnabled = settings.UseManualLocation,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Background = new SolidColorBrush(_isDarkMode ? Color.FromRgb(42, 42, 42) : Color.FromRgb(255, 255, 255)),
                Foreground = (SolidColorBrush)Resources["PrimaryTextBrush"],
                Style = CreateRoundedTextBoxStyle()
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
                Style = CreateRoundedCheckBoxStyle()
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
                Padding = new Thickness(24, 10, 24, 10),
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                Style = CreateRoundedButtonStyle()
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

        private static Style CreateRoundedTextBoxStyle()
        {
            var style = new Style(typeof(TextBox));

            var template = new ControlTemplate(typeof(TextBox));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TextBox.PaddingProperty));

            var scrollFactory = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollFactory.SetValue(FrameworkElement.NameProperty, "PART_ContentHost");
            scrollFactory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);
            scrollFactory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden);

            borderFactory.AppendChild(scrollFactory);
            template.VisualTree = borderFactory;

            style.Setters.Add(new Setter(TextBox.TemplateProperty, template));
            return style;
        }

        private static Style CreateRoundedCheckBoxStyle()
        {
            var style = new Style(typeof(CheckBox));

            var template = new ControlTemplate(typeof(CheckBox));
            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            // Create column definitions manually
            var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            gridFactory.AppendChild(col0);

            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            gridFactory.AppendChild(col1);

            // Checkbox border with rounded corners
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 18.0);
            borderFactory.SetValue(Border.HeightProperty, 18.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(100, 100, 100)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Grid.ColumnProperty, 0);
            borderFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.SetValue(FrameworkElement.NameProperty, "CheckBoxBorder");

            // Checkmark
            var checkMarkFactory = new FrameworkElementFactory(typeof(TextBlock));
            checkMarkFactory.SetValue(TextBlock.TextProperty, "✓");
            checkMarkFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White));
            checkMarkFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
            checkMarkFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            checkMarkFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkMarkFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            checkMarkFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            checkMarkFactory.SetValue(FrameworkElement.NameProperty, "CheckMark");

            borderFactory.AppendChild(checkMarkFactory);
            gridFactory.AppendChild(borderFactory);

            // Content presenter
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(Grid.ColumnProperty, 1);
            contentFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            contentFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            gridFactory.AppendChild(contentFactory);
            template.VisualTree = gridFactory;

            // Trigger for checked state
            var checkedTrigger = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckMark"));
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 120, 212)), "CheckBoxBorder"));
            checkedTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0, 120, 212)), "CheckBoxBorder"));
            template.Triggers.Add(checkedTrigger);

            // Trigger for mouse over
            var hoverTrigger = new Trigger { Property = CheckBox.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(150, 150, 150)), "CheckBoxBorder"));
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(CheckBox.TemplateProperty, template));
            return style;
        }

        private static Style CreateRoundedButtonStyle()
        {
            var style = new Style(typeof(Button));

            var template = new ControlTemplate(typeof(Button));
            var buttonFactory = new FrameworkElementFactory(typeof(Border));
            buttonFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            buttonFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            buttonFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            buttonFactory.SetValue(FrameworkElement.NameProperty, "ButtonBorder");

            var contentBtnFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentBtnFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentBtnFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            buttonFactory.AppendChild(contentBtnFactory);
            template.VisualTree = buttonFactory;

            // Hover trigger
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 100, 180)), "ButtonBorder"));
            template.Triggers.Add(hoverTrigger);

            // Pressed trigger
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 80, 150)), "ButtonBorder"));
            template.Triggers.Add(pressedTrigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
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