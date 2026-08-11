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
        private TextBlock _timeText;
        private TextBlock _amPmText;
        private TextBlock _dateText;
        private TextBlock _greetingText;
        private Border _cardBorder;
        private DispatcherTimer _timer;

        private bool _is24HourFormat = false;
        private bool _showSeconds = true;
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
            this.Title = "widgUI - Clock Widget";
            this.Width = 340;
            this.Height = 160;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;

            // Default position top right
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - this.Width - 40;
            this.Top = 50;

            // Enable dragging
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

        private void BuildUI()
        {
            // Outer container border with Fluent Glassmorphism effect
            _cardBorder = new Border
            {
                CornerRadius = new CornerRadius(20),
                Margin = new Thickness(10),
                Padding = new Thickness(20, 16, 20, 16),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString("#E6181926"), 0.0), // Deep Dark Glass
                        new GradientStop((Color)ColorConverter.ConvertFromString("#CC24273A"), 1.0)
                    }
                },
                BorderBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop((Color)ColorConverter.ConvertFromString("#80FFFFFF"), 0.0),
                        new GradientStop((Color)ColorConverter.ConvertFromString("#20808080"), 1.0)
                    }
                },
                BorderThickness = new Thickness(1.2),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 8,
                    Opacity = 0.45,
                    BlurRadius = 20
                }
            };

            // Main vertical layout
            StackPanel mainLayout = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Top Greeting Header
            _greetingText = new TextBlock
            {
                Text = "BUENAS TARDES",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CAD3F5")),
                Opacity = 0.7,
                Margin = new Thickness(2, 0, 0, 4)
            };

            // Clock Row Layout (Time + AM/PM)
            StackPanel timeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _timeText = new TextBlock
            {
                Text = "00:00:00",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, Arial"),
                FontSize = 42,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Effect = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#8B5CF6"), // Subtle purple glow
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };

            _amPmText = new TextBlock
            {
                Text = "PM",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5A97F")), // Peach accent
                Margin = new Thickness(8, 6, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            timeRow.Children.Add(_timeText);
            timeRow.Children.Add(_amPmText);

            // Date Footer Line
            _dateText = new TextBlock
            {
                Text = "Cargando fecha...",
                FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8C0E0")),
                Margin = new Thickness(2, 4, 0, 0)
            };

            mainLayout.Children.Add(_greetingText);
            mainLayout.Children.Add(timeRow);
            mainLayout.Children.Add(_dateText);

            _cardBorder.Child = mainLayout;
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

            // Format time
            string format = _is24HourFormat
                ? (_showSeconds ? "HH:mm:ss" : "HH:mm")
                : (_showSeconds ? "hh:mm:ss" : "hh:mm");

            _timeText.Text = now.ToString(format);
            _amPmText.Text = _is24HourFormat ? "" : now.ToString("tt", CultureInfo.InvariantCulture).ToUpper();

            // Format Date in Spanish
            CultureInfo ci = new CultureInfo("es-ES");
            string dateStr = now.ToString("dddd, d 'de' MMMM", ci);
            // Capitalize first letter of day
            if (dateStr.Length > 0)
            {
                dateStr = char.ToUpper(dateStr[0]) + dateStr.Substring(1);
            }
            _dateText.Text = dateStr;

            // Update Greeting based on hour
            int hour = now.Hour;
            if (hour >= 6 && hour < 12)
                _greetingText.Text = "¡BUENOS DÍAS!";
            else if (hour >= 12 && hour < 20)
                _greetingText.Text = "¡BUENAS TARDES!";
            else
                _greetingText.Text = "¡BUENAS NOCHES!";
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

            MenuItem itemSeconds = new MenuItem { Header = "Mostrar Segundos" };
            itemSeconds.IsCheckable = true;
            itemSeconds.IsChecked = _showSeconds;
            itemSeconds.Click += (s, e) =>
            {
                _showSeconds = itemSeconds.IsChecked;
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
            cm.Items.Add(itemSeconds);
            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }
    }
}
