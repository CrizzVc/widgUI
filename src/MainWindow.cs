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
        HorizontalCompact,
        OutlineHorizontal,
        StackedMono
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
        private Grid _hoursHost;
        private bool _hoursUsesOutline;
        private DispatcherTimer _timer;

        private bool _is24HourFormat = true;
        private bool _showAmPm = true;
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
        private double _aspectRatio = 170.0 / 230.0;
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

        public bool ShowAmPm
        {
            get { return _showAmPm; }
            set
            {
                if (_showAmPm != value)
                {
                    _showAmPm = value;
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
                Background = Brushes.Transparent,
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

            _clockStack.Children.Add(_hoursHost = new Grid());
            _hoursHost.Children.Add(_hoursText);
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
            _aspectRatio = _resizeStartWidth / _resizeStartHeight;
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
            double delta = Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX : deltaY;

            double newWidth = ClampClockSize(_resizeStartWidth + delta, true);
            double newHeight = ClampClockSize(newWidth / _aspectRatio, false);

            if (Math.Abs(newHeight - (newWidth / _aspectRatio)) > 0.5)
            {
                newHeight = ClampClockSize(_resizeStartHeight + delta, false);
                newWidth = ClampClockSize(newHeight * _aspectRatio, true);
            }

            this.Width = newWidth;
            this.Height = newHeight;
            UpdateDesignSize(newWidth, newHeight);
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
            if (this.Height > 0)
            {
                _aspectRatio = this.Width / this.Height;
            }
            UpdateDesignSize(this.Width, this.Height);
        }

        public void ApplyStyleVariant(WidgetStyleVariant variant)
        {
            _currentVariant = variant;
            _hoursUsesOutline = false;
            ClearHoursOutline();

            FontFamily defaultFont = new FontFamily("Segoe UI, Arial");
            _hoursText.FontFamily = defaultFont;
            _minutesText.FontFamily = defaultFont;
            _hoursText.FontWeight = FontWeights.Bold;
            _minutesText.FontWeight = FontWeights.Bold;
            _hoursText.Effect = null;
            _minutesText.Effect = null;
            _cardBorder.Padding = new Thickness(10);
            ApplyTransparentCardShell();

            switch (variant)
            {
                case WidgetStyleVariant.MinimalistVertical:
                    SetClockSize(170, 230);

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

                    _clockStack.Orientation = Orientation.Vertical;
                    _separatorText.Visibility = Visibility.Collapsed;

                    _hoursText.FontSize = 70;
                    _hoursText.Margin = new Thickness(0, 0, 0, -8);
                    _hoursText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));

                    _minutesText.FontSize = 70;
                    _minutesText.Margin = new Thickness(0, -8, 0, 4);
                    _minutesText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
                    break;

                case WidgetStyleVariant.NeumorphismDark:
                    SetClockSize(190, 240);

                    _clockStack.Orientation = Orientation.Vertical;
                    _separatorText.Visibility = Visibility.Collapsed;

                    _hoursText.FontSize = 70;
                    _hoursText.Margin = new Thickness(0, 0, 0, -8);
                    _hoursText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBA6F7"));

                    _minutesText.FontSize = 70;
                    _minutesText.Margin = new Thickness(0, -8, 0, 4);
                    _minutesText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5E0DC"));
                    break;

                case WidgetStyleVariant.HorizontalCompact:
                    SetClockSize(260, 110);

                    _clockStack.Orientation = Orientation.Horizontal;
                    _separatorText.Visibility = Visibility.Visible;
                    _separatorText.Margin = new Thickness(2, -8, 2, 0);
                    _separatorText.Foreground = Brushes.White;
                    _separatorText.Effect = null;

                    _hoursText.FontSize = 56;
                    _hoursText.Margin = new Thickness(0);
                    _hoursText.Foreground = Brushes.White;

                    _minutesText.FontSize = 56;
                    _minutesText.Margin = new Thickness(0);
                    _minutesText.Foreground = Brushes.White;
                    break;

                case WidgetStyleVariant.OutlineHorizontal:
                    SetClockSize(300, 120);
                    _cardBorder.Padding = new Thickness(14, 6, 14, 6);

                    _clockStack.Orientation = Orientation.Horizontal;
                    _separatorText.Visibility = Visibility.Collapsed;

                    FontFamily condensedFont = new FontFamily("Arial Narrow, Segoe UI");
                    _hoursText.FontFamily = condensedFont;
                    _minutesText.FontFamily = condensedFont;
                    _hoursText.FontSize = 92;
                    _minutesText.FontSize = 92;
                    _hoursText.FontWeight = FontWeights.Black;
                    _minutesText.FontWeight = FontWeights.Black;
                    _hoursText.Margin = new Thickness(0, 0, -8, 0);
                    _minutesText.Margin = new Thickness(-8, 0, 0, 0);
                    _minutesText.Foreground = Brushes.White;
                    _hoursUsesOutline = true;
                    break;

                case WidgetStyleVariant.StackedMono:
                    SetClockSize(170, 150);
                    _cardBorder.Padding = new Thickness(12, 4, 12, 4);

                    _clockStack.Orientation = Orientation.Vertical;
                    _separatorText.Visibility = Visibility.Collapsed;

                    FontFamily stackedFont = new FontFamily("Segoe UI, Arial");
                    _hoursText.FontFamily = stackedFont;
                    _minutesText.FontFamily = stackedFont;
                    _hoursText.FontSize = 88;
                    _minutesText.FontSize = 88;
                    _hoursText.FontWeight = FontWeights.Black;
                    _minutesText.FontWeight = FontWeights.Black;
                    _hoursText.Margin = new Thickness(0, 0, 0, -30);
                    _minutesText.Margin = new Thickness(0, -30, 0, 0);
                    _hoursText.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 218));
                    _minutesText.Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                    break;
            }

            UpdateTimeDisplay();
        }

        private void ApplyTransparentCardShell()
        {
            _cardBorder.Background = Brushes.Transparent;
            _cardBorder.BorderBrush = Brushes.Transparent;
            _cardBorder.BorderThickness = new Thickness(0);
            _cardBorder.CornerRadius = new CornerRadius(0);
            _cardBorder.Effect = null;
        }

        private void ClearHoursOutline()
        {
            if (_hoursHost == null)
            {
                return;
            }

            for (int i = _hoursHost.Children.Count - 1; i >= 0; i--)
            {
                if (_hoursHost.Children[i] != _hoursText)
                {
                    _hoursHost.Children.RemoveAt(i);
                }
            }
        }

        private void SyncHoursOutline()
        {
            if (_hoursHost == null || _hoursText == null || !_hoursUsesOutline)
            {
                return;
            }

            ClearHoursOutline();
            _hoursText.Foreground = Brushes.Transparent;
            Panel.SetZIndex(_hoursText, 10);

            double stroke = 1.6;
            int[][] offsets =
            {
                new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 },
                new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { 1, 1 }
            };

            foreach (int[] offset in offsets)
            {
                TextBlock layer = CreateOutlineLayer(_hoursText);
                layer.Margin = new Thickness(offset[0] * stroke, offset[1] * stroke, 0, 0);
                _hoursHost.Children.Insert(0, layer);
            }
        }

        private static TextBlock CreateOutlineLayer(TextBlock source)
        {
            return new TextBlock
            {
                Text = source.Text,
                FontFamily = source.FontFamily,
                FontSize = source.FontSize,
                FontWeight = source.FontWeight,
                Foreground = Brushes.White,
                HorizontalAlignment = source.HorizontalAlignment,
                TextAlignment = source.TextAlignment,
                IsHitTestVisible = false
            };
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
                if (_showAmPm)
                {
                    _amPmText.Text = now.ToString("tt", CultureInfo.InvariantCulture).ToUpper();
                    _amPmText.Visibility = Visibility.Visible;
                }
                else
                {
                    _amPmText.Visibility = Visibility.Collapsed;
                }
            }

            _minutesText.Text = now.ToString("mm");

            if (_hoursUsesOutline)
            {
                SyncHoursOutline();
            }

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

            MenuItem v5 = new MenuItem { Header = "Contorno Horizontal", IsCheckable = true, IsChecked = (_currentVariant == WidgetStyleVariant.OutlineHorizontal) };
            v5.Click += (s, e) => { ApplyStyleVariant(WidgetStyleVariant.OutlineHorizontal); UpdateContextMenuChecks(itemVariants); };

            MenuItem v6 = new MenuItem { Header = "Apilado Mono", IsCheckable = true, IsChecked = (_currentVariant == WidgetStyleVariant.StackedMono) };
            v6.Click += (s, e) => { ApplyStyleVariant(WidgetStyleVariant.StackedMono); UpdateContextMenuChecks(itemVariants); };

            itemVariants.Items.Add(v1);
            itemVariants.Items.Add(v2);
            itemVariants.Items.Add(v3);
            itemVariants.Items.Add(v4);
            itemVariants.Items.Add(v5);
            itemVariants.Items.Add(v6);

            MenuItem itemFormat = new MenuItem { Header = "Formato 24 Horas" };
            itemFormat.IsCheckable = true;
            itemFormat.IsChecked = _is24HourFormat;
            itemFormat.Click += (s, e) =>
            {
                Is24HourFormat = itemFormat.IsChecked;
            };

            MenuItem itemAmPm = new MenuItem { Header = "Mostrar AM/PM" };
            itemAmPm.IsCheckable = true;
            itemAmPm.IsChecked = _showAmPm;
            itemAmPm.IsEnabled = !_is24HourFormat;
            itemAmPm.Click += (s, e) =>
            {
                ShowAmPm = itemAmPm.IsChecked;
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

            MenuItem itemExit = new MenuItem { Header = "Ocultar Widget" };
            itemExit.Click += (s, e) =>
            {
                this.Hide();
            };

            cm.Items.Add(itemVariants);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemFormat);
            cm.Items.Add(itemAmPm);
            cm.Items.Add(itemDate);
            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }

        private void UpdateContextMenuChecks(MenuItem variantsParent)
        {
            WidgetStyleVariant[] variants =
            {
                WidgetStyleVariant.MinimalistVertical,
                WidgetStyleVariant.GlassmorphismCard,
                WidgetStyleVariant.NeumorphismDark,
                WidgetStyleVariant.HorizontalCompact,
                WidgetStyleVariant.OutlineHorizontal,
                WidgetStyleVariant.StackedMono
            };

            for (int i = 0; i < variants.Length && i < variantsParent.Items.Count; i++)
            {
                MenuItem item = variantsParent.Items[i] as MenuItem;
                if (item != null)
                {
                    item.IsChecked = _currentVariant == variants[i];
                }
            }
        }

        public ClockLayoutData ToLayoutData()
        {
            return new ClockLayoutData
            {
                Visible = this.IsVisible,
                StyleVariant = (int)_currentVariant,
                Is24HourFormat = _is24HourFormat,
                ShowAmPm = _showAmPm,
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
            ShowAmPm = data.ShowAmPm;
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

