using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WidgUI
{
    public class MainWindow : Window
    {
        private TextBlock _hoursText;
        private TextBlock _minutesText;
        private TextBlock _dateText;
        private TextBlock _amPmText;
        private Border _cardBorder;
        private DispatcherTimer _timer;

        private bool _is24HourFormat = true;
        private bool _showDate = false; // Fecha opcional (desactivada por defecto)
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;

        public MainWindow()
        {
            InitializeWindow();
            BuildUI();
            SetupTimer();
            SetupContextMenu();

            this.Loaded += MainWindow_Loaded;
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Reloj Vertical Transparente";
            this.Width = 160;
            this.Height = 220;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;

            // Default position top right of primary monitor
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - this.Width - 50;
            this.Top = 60;

            // Allow dragging anywhere on the clock
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }
        }

        private DropShadowEffect CreateTextShadow()
        {
            return new DropShadowEffect
            {
                Color = (Color)ColorConverter.ConvertFromString("#180B28"),
                Direction = 270,
                ShadowDepth = 4,
                Opacity = 0.9,
                BlurRadius = 12
            };
        }

        private void BuildUI()
        {
            // Invisible background container with #01000000 so mouse drag and right click work 100%
            _cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4)
            };

            StackPanel mainStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            FontFamily boldFont = new FontFamily("Segoe UI, Arial");

            // Hours Text (Top line)
            _hoursText = new TextBlock
            {
                Text = "23",
                FontFamily = boldFont,
                FontSize = 76,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -8, 0, -10),
                Effect = CreateTextShadow()
            };

            // Minutes Text (Bottom line)
            _minutesText = new TextBlock
            {
                Text = "28",
                FontFamily = boldFont,
                FontSize = 76,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -10, 0, 2),
                Effect = CreateTextShadow()
            };

            // AM/PM Indicator (For 12h mode)
            _amPmText = new TextBlock
            {
                Text = "",
                FontFamily = boldFont,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F472B6")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Effect = CreateTextShadow()
            };

            // Optional Date Text
            _dateText = new TextBlock
            {
                Text = "",
                FontFamily = boldFont,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.9,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
                Effect = CreateTextShadow()
            };

            mainStack.Children.Add(_hoursText);
            mainStack.Children.Add(_minutesText);
            mainStack.Children.Add(_amPmText);
            mainStack.Children.Add(_dateText);

            _cardBorder.Child = mainStack;
            this.Content = _cardBorder;
        }

        private void SetupTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += (s, e) => UpdateTimeDisplay();
            _timer.Start();
            UpdateTimeDisplay();
        }

        private void UpdateTimeDisplay()
        {
            DateTime now = DateTime.Now;

            if (_is24HourFormat)
            {
                _hoursText.Text = now.ToString("HH");
                _amPmText.Visibility = Visibility.Collapsed;
            }
            else
            {
                _hoursText.Text = now.ToString("hh");
                _amPmText.Text = now.ToString("tt", CultureInfo.InvariantCulture).ToUpper();
                _amPmText.Visibility = Visibility.Visible;
            }

            _minutesText.Text = now.ToString("mm");

            if (_showDate)
            {
                CultureInfo ci = new CultureInfo("es-ES");
                string dayName = now.ToString("ddd", ci).ToUpper().Replace(".", "");
                string monthName = now.ToString("MMM", ci).ToUpper().Replace(".", "");
                _dateText.Text = string.Format("{0} {1} {2}", dayName, now.Day, monthName);
                _dateText.Visibility = Visibility.Visible;
            }
            else
            {
                _dateText.Visibility = Visibility.Collapsed;
            }
        }

        private void SetupContextMenu()
        {
            ContextMenu cm = new ContextMenu();

            MenuItem itemFormat = new MenuItem { Header = "Formato 24 Horas" };
            itemFormat.IsCheckable = true;
            itemFormat.IsChecked = _is24HourFormat;
            itemFormat.Click += (s, e) =>
            {
                _is24HourFormat = itemFormat.IsChecked;
                UpdateTimeDisplay();
            };

            MenuItem itemDate = new MenuItem { Header = "Mostrar Fecha" };
            itemDate.IsCheckable = true;
            itemDate.IsChecked = _showDate;
            itemDate.Click += (s, e) =>
            {
                _showDate = itemDate.IsChecked;
                UpdateTimeDisplay();
            };

            MenuItem itemLock = new MenuItem { Header = "Bloquear Posición" };
            itemLock.IsCheckable = true;
            itemLock.IsChecked = _isLocked;
            itemLock.Click += (s, e) =>
            {
                _isLocked = itemLock.IsChecked;
            };

            MenuItem itemExit = new MenuItem { Header = "Cerrar Widget" };
            itemExit.Click += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            cm.Items.Add(itemFormat);
            cm.Items.Add(itemDate);
            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }
    }
}
