using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Text;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using WeatherWidget.Models;
using WeatherWidget.Services;
using WinRT.Interop;
using Windows.Foundation;
using Windows.Graphics;

namespace WeatherWidget.Views
{
    public sealed partial class WeatherFlyout : Window
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        private readonly WeatherData _data;
        private readonly LocationData _loc;
        private readonly Rect _anchorRect;
        private readonly bool _openSettingsOnLoad;

        private AppWindow? _appWindow;
        private OverlappedPresenter? _presenter;
        private bool _allowDeactivate;

        public WeatherFlyout(WeatherData data, LocationData loc, Rect anchorRect, bool openSettings)
        {
            InitializeComponent();

            _data = data;
            _loc = loc;
            _anchorRect = anchorRect;
            _openSettingsOnLoad = openSettings;

            RootGrid.SizeChanged += RootGrid_SizeChanged;
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWindow();
            TrySetSystemBackdrop();
            PopulateWeatherData(_loc);
            LoadSettings();

            if (_openSettingsOnLoad)
            {
                ShowSettingsView();
            }
            else
            {
                ShowWeatherView();
            }

            UpdateWindowSize();
            PositionWindow();

            DispatcherQueue.TryEnqueue(() => _allowDeactivate = true);
        }

        private void InitializeWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                _presenter = presenter;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
            }

            if (_appWindow != null)
            {
                _appWindow.Title = "Weather";
                _appWindow.IsShownInSwitchers = false;
            }

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt32();
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;
            _ = SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        private void TrySetSystemBackdrop()
        {
            try
            {
                SystemBackdrop = new MicaBackdrop();
            }
            catch
            {
                try
                {
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                }
                catch
                {
                    // No supported system backdrop.
                }
            }
        }

        private void PopulateWeatherData(LocationData loc)
        {
            CityText.Text = loc.City;

            MainTemp.Text = Math.Round(_data.Temperature) + "°";
            ConditionFull.Text = _data.Condition;
            FeelsLikeText.Text = $"Feels like {Math.Round(_data.FeelsLike)}°";
            HighTempText.Text = $"{Math.Round(_data.HighTemp)}°";
            LowTempText.Text = $"{Math.Round(_data.LowTemp)}°";

            try
            {
                LargeIcon.Source = new BitmapImage(new Uri($"ms-appx:///Assets/PNG/{_data.IconCode}.png"));
            }
            catch { }

            PrecipitationText.Text = _data.PrecipitationChance + "%";
            HumidityText.Text = _data.CurrentHumidity + "%";
            WindText.Text = Math.Round(_data.CurrentWindSpeed) + " mph";
            UVIndexText.Text = _data.UVIndex.ToString("F1");

            SunriseInlineText.Text = FormatTime(_data.Sunrise);
            SunsetInlineText.Text = FormatTime(_data.Sunset);

            if (_data.Pressure > 0)
            {
                PressureText.Text = (_data.Pressure * 0.02953).ToString("F2") + " inHg";
            }
            else
            {
                PressureText.Text = "--";
            }

            CloudCoverText.Text = _data.CloudCover + "%";

            PopulateDailyForecast();
        }

        private static string FormatTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "--";
            }

            if (DateTime.TryParse(value, out DateTime parsed))
            {
                return parsed.ToString("h:mm tt");
            }

            return "--";
        }

        private void PopulateDailyForecast()
        {
            DailyForecastGrid.Children.Clear();

            int index = 0;
            foreach (var day in _data.Daily)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 14, 12, 14),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                card.Background = (Brush)RootGrid.Resources["CardBackgroundBrush"];
                card.BorderBrush = (Brush)RootGrid.Resources["CardBorderBrush"];

                var stackPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var dayLabel = new TextBlock
                {
                    Text = day.TimeLabel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                dayLabel.Foreground = (Brush)RootGrid.Resources["PrimaryTextBrush"];
                stackPanel.Children.Add(dayLabel);

                try
                {
                    var icon = new Image
                    {
                        Source = new BitmapImage(new Uri(day.IconPath)),
                        Width = 44,
                        Height = 44,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    stackPanel.Children.Add(icon);
                }
                catch { }

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
                tempLabel.Foreground = (Brush)RootGrid.Resources["PrimaryTextBrush"];
                stackPanel.Children.Add(tempLabel);

                var divider = new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 0, 0, 10),
                    Background = (Brush)RootGrid.Resources["DividerBrush"]
                };
                stackPanel.Children.Add(divider);

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
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/PNG/ui_precipitation.png")),
                        Width = 12,
                        Height = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
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
                precipText.Foreground = (Brush)RootGrid.Resources["SecondaryTextBrush"];
                precipStack.Children.Add(precipText);
                stackPanel.Children.Add(precipStack);

                var windStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                try
                {
                    var windIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/PNG/ui_wind.png")),
                        Width = 12,
                        Height = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 6, 0)
                    };
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
                windText.Foreground = (Brush)RootGrid.Resources["SecondaryTextBrush"];
                windStack.Children.Add(windText);
                stackPanel.Children.Add(windStack);

                card.Child = stackPanel;
                Grid.SetColumn(card, index);
                DailyForecastGrid.Children.Add(card);
                index++;
            }
        }

        private void OpenWeatherApp_Click(object sender, RoutedEventArgs e)
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("bingweather:"));
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsView();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            ShowWeatherView();
        }

        private void ShowSettingsView()
        {
            WeatherView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
        }

        private void ShowWeatherView()
        {
            SettingsView.Visibility = Visibility.Collapsed;
            WeatherView.Visibility = Visibility.Visible;
        }

        private void LoadSettings()
        {
            UseManualLocationCheck.IsChecked = SettingsService.UseManualLocation;
            LatitudeBox.Text = SettingsService.ManualLatitude.ToString(CultureInfo.InvariantCulture);
            LongitudeBox.Text = SettingsService.ManualLongitude.ToString(CultureInfo.InvariantCulture);
            StartWithWindowsCheck.IsChecked = SettingsService.StartWithWindows;

            bool enabled = UseManualLocationCheck.IsChecked == true;
            LatitudeBox.IsEnabled = enabled;
            LongitudeBox.IsEnabled = enabled;
        }

        private void UseManualLocationCheck_Changed(object sender, RoutedEventArgs e)
        {
            bool enabled = UseManualLocationCheck.IsChecked == true;
            LatitudeBox.IsEnabled = enabled;
            LongitudeBox.IsEnabled = enabled;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.UseManualLocation = UseManualLocationCheck.IsChecked == true;

            if (double.TryParse(LatitudeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat))
            {
                SettingsService.ManualLatitude = lat;
            }

            if (double.TryParse(LongitudeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
            {
                SettingsService.ManualLongitude = lon;
            }

            SettingsService.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
            Close();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (_allowDeactivate && e.WindowActivationState == WindowActivationState.Deactivated)
            {
                Close();
            }
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateWindowSize();
            PositionWindow();
        }

        private void UpdateWindowSize()
        {
            if (_appWindow == null)
            {
                return;
            }

            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            int width = (int)Math.Round(RootGrid.ActualWidth * scale);
            int height = (int)Math.Round(RootGrid.ActualHeight * scale);
            _appWindow.Resize(new SizeInt32(width, height));
        }

        private void PositionWindow()
        {
            if (_appWindow == null)
            {
                return;
            }

            double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            double width = RootGrid.ActualWidth;
            double height = RootGrid.ActualHeight;

            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            RectInt32 workArea = displayArea.WorkArea;

            double workLeft = workArea.X / scale;
            double workTop = workArea.Y / scale;
            double workRight = (workArea.X + workArea.Width) / scale;
            double workBottom = (workArea.Y + workArea.Height) / scale;

            double targetLeft = _anchorRect.Left + (_anchorRect.Width - width) / 2;
            double targetTop = _anchorRect.Top - height - 16;

            if (targetLeft < workLeft + 10)
            {
                targetLeft = workLeft + 10;
            }

            if (targetLeft + width > workRight - 10)
            {
                targetLeft = workRight - width - 10;
            }

            if (targetTop < workTop + 10)
            {
                targetTop = workTop + 10;
            }

            _appWindow.Move(new PointInt32((int)Math.Round(targetLeft * scale), (int)Math.Round(targetTop * scale)));
        }
    }
}
