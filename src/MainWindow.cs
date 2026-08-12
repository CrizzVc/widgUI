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
    public enum WidgetStyleVariant
    {
        MinimalistVertical,
        GlassmorphismCard,
        NeumorphismDark,
        HorizontalCompact
    }

    public class MainWindow : Window
    {
        private TextBlock _hoursText;
        private TextBlock _minutesText;
        private TextBlock _separatorText;
        private TextBlock _dateText;
        private TextBlock _amPmText;
        private Border _cardBorder;
        private StackPanel _mainStack;
        private StackPanel _clockStack;
        private DispatcherTimer _timer;

        private bool _is24HourFormat = true;
        private bool _showDate = false;
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;

        private WidgetStyleVariant _currentVariant = WidgetStyleVariant.MinimalistVertical;

        private Grid _rootGrid;
        private Viewbox _contentViewbox;
        private Grid _designHost;
        private Border _resizeHandle;
        private bool _isResizing;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private const double MinClockWidth = 120;
        private const double MinClockHeight = 90;
        private const double MaxClockWidth = 520;
        private const double MaxClockHeight = 420;

        public MainWindow()
        {
            InitializeWindow();
            BuildUI();
            ApplyStyleVariant(_currentVariant);
            SetupTimer();
            SetupContextMenu();

            this.Loaded += MainWindow_Loaded;
        }

        public bool Is24HourFormat
        {
            get { return _is24HourFormat; }
            set
            {
                if (_is24HourFormat != value)
                {
                    _is24HourFormat = value;
                    UpdateTimeDisplay();
                    SetupContextMenu();
                }
            }
        }

        public bool ShowDate
        {
            get { return _showDate; }
            set
            {
                if (_showDate != value)
                {
                    _showDate = value;
                    UpdateTimeDisplay();
                    SetupContextMenu();
                }
            }
        }

        public bool IsLocked
        {
            get { return _isLocked; }
            set
            {
                if (_isLocked != value)
                {
                    _isLocked = value;
                    if (_resizeHandle != null)
                    {
                        _resizeHandle.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
                    }
                    SetupContextMenu();
                }
            }
        }

        public WidgetStyleVariant CurrentVariant
        {
            get { return _currentVariant; }
            set
            {
                if (_currentVariant != value)
                {
                    ApplyStyleVariant(value);
                    SetupContextMenu();
                }
            }
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Reloj Desktop";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;

            // Default position top right of primary monitor
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Width = 170;
            this.Height = 230;
            this.Left = screenWidth - this.Width - 50;
            this.Top = 60;

            // Allow dragging anywhere on the clock
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (_isLocked || _isResizing || IsInteractiveTarget(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private static bool IsInteractiveTarget(DependencyObject source)
        {
            while (source != null)
            {
                Border border = source as Border;
                if (border != null && border.Cursor == Cursors.Hand)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
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
            _cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10)
            };

            _mainStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _clockStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            FontFamily boldFont = new FontFamily("Segoe UI, Arial");

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

            _separatorText = new TextBlock
            {
                Text = ":",
                FontFamily = boldFont,
                FontSize = 54,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, -10, 2, 0),
                Visibility = Visibility.Collapsed,
                Effect = CreateTextShadow()
            };

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

            _clockStack.Children.Add(_hoursText);
            _clockStack.Children.Add(_separatorText);
            _clockStack.Children.Add(_minutesText);

            _mainStack.Children.Add(_clockStack);
            _mainStack.Children.Add(_amPmText);
            _mainStack.Children.Add(_dateText);

            _cardBorder.Child = _mainStack;
            WrapContentForResize(_cardBorder, this.Width, this.Height);
        }

        private void WrapContentForResize(UIElement content, double designWidth, double designHeight)
        {
            _designHost = new Grid
            {
                Width = designWidth,
                Height = designHeight
            };
            _designHost.Children.Add(content);

            _rootGrid = new Grid();
            _contentViewbox = new Viewbox
            {
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both
            };
            _contentViewbox.Child = _designHost;
            _rootGrid.Children.Add(_contentViewbox);

            _resizeHandle = CreateResizeHandle();
            _rootGrid.Children.Add(_resizeHandle);

            this.Content = _rootGrid;
        }

        private void UpdateDesignSize(double width, double height)
        {
            if (_designHost != null)
            {
                _designHost.Width = width;
                _designHost.Height = height;
            }
        }

        private Border CreateResizeHandle()
        {
            Border handle = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                CornerRadius = new CornerRadius(4, 0, 0, 0),
                Cursor = Cursors.SizeNWSE,
                ToolTip = "Arrastra para cambiar tamano"
            };

            handle.Child = new TextBlock
            {
                Text = "\uE7E8",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 9,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            handle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
            handle.MouseMove += ResizeHandle_MouseMove;
            handle.MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
            return handle;
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked)
            {
                return;
            }

            _isResizing = true;
            _resizeStartPoint = e.GetPosition(this);
            _resizeStartWidth = this.Width;
            _resizeStartHeight = this.Height;
            _resizeHandle.CaptureMouse();
            e.Handled = true;
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing)
            {
                return;
            }

            Point current = e.GetPosition(this);
            double deltaX = current.X - _resizeStartPoint.X;
            double deltaY = current.Y - _resizeStartPoint.Y;

            this.Width = ClampClockSize(_resizeStartWidth + deltaX, true);
            this.Height = ClampClockSize(_resizeStartHeight + deltaY, false);
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing)
            {
                return;
            }

            _isResizing = false;
            _resizeHandle.ReleaseMouseCapture();
        }

        private static double ClampClockSize(double value, bool isWidth)
        {
            double min = isWidth ? MinClockWidth : MinClockHeight;
            double max = isWidth ? MaxClockWidth : MaxClockHeight;
            return Math.Max(min, Math.Min(max, value));
        }

        private void SetClockSize(double width, double height)
        {
            this.Width = ClampClockSize(width, true);
            this.Height = ClampClockSize(height, false);
            UpdateDesignSize(this.Width, this.Height);
        }

        public void ApplyStyleVariant(WidgetStyleVariant variant)
        {
            _currentVariant = variant;

            switch (variant)
            {
                case WidgetStyleVariant.MinimalistVertical:
                    SetClockSize(170, 230);
                    _cardBorder.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
                    _cardBorder.BorderBrush = Brushes.Transparent;
                    _cardBorder.BorderThickness = new Thickness(0);
                    _cardBorder.CornerRadius = new CornerRadius(0);
                    _cardBorder.Effect = null;

                    _clockStack.Orientation = Orientation.Vertical;
                    _separatorText.Visibility = Visibility.Collapsed;

                    _hoursText.FontSize = 76;
                    _hoursText.Margin = new Thickness(0, -8, 0, -10);
                    _hoursText.Foreground = Brushes.White;
                    _hoursText.Effect = CreateTextShadow();

                    _minutesText.FontSize = 76;
                    _minutesText.Margin = new Thickness(0, -10, 0, 2);
                    _minutesText.Foreground = Brushes.White;
                    _minutesText.Effect = CreateTextShadow();
                    break;

                case WidgetStyleVariant.GlassmorphismCard:
                    SetClockSize(190, 240);
                    _cardBorder.Background = new SolidColorBrush(Color.FromArgb(120, 20, 25, 40));
                    _cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                    _cardBorder.BorderThickness = new Thickness(1.5);
                    _cardBorder.CornerRadius = new CornerRadius(24);
                    _cardBorder.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 270,
                        ShadowDepth = 8,
                        Opacity = 0.4,
                        BlurRadius = 20
                    };

                    _clockStack.Orientation = Orientation.Vertical;
                    _separatorText.Visibility = Visibility.Collapsed;

                    _hoursText.FontSize = 70;
                    _hoursText.Margin = new Thickness(0, 0, 0, -8);
                    _hoursText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
                    _hoursText.Effect = null;

                    _minutesText.FontSize = 70;
                    _minutesText.Margin = new Thickness(0, -8, 0, 4);
                    _minutesText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    _minutesText.Effect = null;
                    break;

                case WidgetStyleVariant.NeumorphismDark:
                    SetClockSize(190, 240);
                    _cardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));
                    _cardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                    _cardBorder.BorderThickness = new Thickness(2);
                    _cardBorder.CornerRadius = new CornerRadius(28);
                    _cardBorder.Effect = new DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#11111B"),
                        Direction = 270,
                        ShadowDepth = 10,
                        Opacity = 0.6,
                        BlurRadius = 18
                    };

                    _clockStack.Orientation = Orientation.Vertical;
                    _separatorText.Visibility = Visibility.Collapsed;

                    _hoursText.FontSize = 70;
                    _hoursText.Margin = new Thickness(0, 0, 0, -8);
                    _hoursText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBA6F7"));
                    _hoursText.Effect = null;

                    _minutesText.FontSize = 70;
                    _minutesText.Margin = new Thickness(0, -8, 0, 4);
                    _minutesText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5E0DC"));
                    _minutesText.Effect = null;
                    break;

                case WidgetStyleVariant.HorizontalCompact:
                    SetClockSize(260, 110);
                    _cardBorder.Background = new SolidColorBrush(Color.FromArgb(180, 15, 23, 42));
                    _cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 56, 189, 248));
                    _cardBorder.BorderThickness = new Thickness(1);
                    _cardBorder.CornerRadius = new CornerRadius(18);
                    _cardBorder.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 270,
                        ShadowDepth = 5,
                        Opacity = 0.5,
                        BlurRadius = 15
                    };

                    _clockStack.Orientation = Orientation.Horizontal;
                    _separatorText.Visibility = Visibility.Visible;
                    _separatorText.Margin = new Thickness(2, -8, 2, 0);

                    _hoursText.FontSize = 56;
                    _hoursText.Margin = new Thickness(0);
                    _hoursText.Foreground = Brushes.White;
                    _hoursText.Effect = null;

                    _minutesText.FontSize = 56;
                    _minutesText.Margin = new Thickness(0);
                    _minutesText.Foreground = Brushes.White;
                    _minutesText.Effect = null;
                    break;
            }

            UpdateTimeDisplay();
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

            // Submenú de Variantes de Diseño
            MenuItem itemVariants = new MenuItem { Header = "Diseño / Variante" };

            MenuItem v1 = new MenuItem { Header = "Vertical Minimalista", IsCheckable = true, IsChecked = (_currentVariant == WidgetStyleVariant.MinimalistVertical) };
            v1.Click += (s, e) => { ApplyStyleVariant(WidgetStyleVariant.MinimalistVertical); UpdateContextMenuChecks(itemVariants); };

            MenuItem v2 = new MenuItem { Header = "Tarjeta Glassmorphism (Cristal)", IsCheckable = true, IsChecked = (_currentVariant == WidgetStyleVariant.GlassmorphismCard) };
            v2.Click += (s, e) => { ApplyStyleVariant(WidgetStyleVariant.GlassmorphismCard); UpdateContextMenuChecks(itemVariants); };

            MenuItem v3 = new MenuItem { Header = "Neumorfismo Oscuro", IsCheckable = true, IsChecked = (_currentVariant == WidgetStyleVariant.NeumorphismDark) };
            v3.Click += (s, e) => { ApplyStyleVariant(WidgetStyleVariant.NeumorphismDark); UpdateContextMenuChecks(itemVariants); };

            MenuItem v4 = new MenuItem { Header = "Horizontal Compacto", IsCheckable = true, IsChecked = (_currentVariant == WidgetStyleVariant.HorizontalCompact) };
            v4.Click += (s, e) => { ApplyStyleVariant(WidgetStyleVariant.HorizontalCompact); UpdateContextMenuChecks(itemVariants); };

            itemVariants.Items.Add(v1);
            itemVariants.Items.Add(v2);
            itemVariants.Items.Add(v3);
            itemVariants.Items.Add(v4);

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
                IsLocked = itemLock.IsChecked;
            };

            MenuItem itemExit = new MenuItem { Header = "Cerrar Widget" };
            itemExit.Click += (s, e) =>
            {
                Application.Current.Shutdown();
            };

            cm.Items.Add(itemVariants);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemFormat);
            cm.Items.Add(itemDate);
            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }

        private void UpdateContextMenuChecks(MenuItem variantsParent)
        {
            if (variantsParent.Items.Count >= 4)
            {
                ((MenuItem)variantsParent.Items[0]).IsChecked = (_currentVariant == WidgetStyleVariant.MinimalistVertical);
                ((MenuItem)variantsParent.Items[1]).IsChecked = (_currentVariant == WidgetStyleVariant.GlassmorphismCard);
                ((MenuItem)variantsParent.Items[2]).IsChecked = (_currentVariant == WidgetStyleVariant.NeumorphismDark);
                ((MenuItem)variantsParent.Items[3]).IsChecked = (_currentVariant == WidgetStyleVariant.HorizontalCompact);
            }
        }

        public ClockLayoutData ToLayoutData()
        {
            return new ClockLayoutData
            {
                Visible = this.IsVisible,
                StyleVariant = (int)_currentVariant,
                Is24HourFormat = _is24HourFormat,
                ShowDate = _showDate,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                Width = this.Width,
                Height = this.Height
            };
        }

        public void ApplyLayoutData(ClockLayoutData data)
        {
            if (data == null)
            {
                return;
            }

            if (Enum.IsDefined(typeof(WidgetStyleVariant), data.StyleVariant))
            {
                CurrentVariant = (WidgetStyleVariant)data.StyleVariant;
            }

            Is24HourFormat = data.Is24HourFormat;
            ShowDate = data.ShowDate;
            IsLocked = data.IsLocked;
            this.Left = data.Left;
            this.Top = data.Top;

            if (data.Width >= MinClockWidth && data.Height >= MinClockHeight)
            {
                this.Width = ClampClockSize(data.Width, true);
                this.Height = ClampClockSize(data.Height, false);
                UpdateDesignSize(this.Width, this.Height);
            }

            if (data.Visible)
            {
                this.Show();
            }
            else
            {
                this.Hide();
            }
        }
    }
}

