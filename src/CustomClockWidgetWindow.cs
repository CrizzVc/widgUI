using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfCursors = System.Windows.Input.Cursors;

namespace WidgUI
{
    public class CustomClockWidgetWindow : Window
    {
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;
        private bool _adaptToBackground = false;
        private string _widgetId;

        private string _fontFamily = "Segoe UI";
        private double _fontSize = 48.0;
        private string _fontStyle = "Normal";
        private string _fontWeight = "Normal";
        private bool _isVertical = false;
        private bool _showAmPm = true;

        private Border _cardBorder;
        private StackPanel _timePanel;
        private TextBlock _hoursText;
        private TextBlock _minutesText;
        private TextBlock _secondsSeparatorText;
        private TextBlock _amPmText;
        private DispatcherTimer _timer;

        private Grid _rootGrid;
        private Viewbox _contentViewbox;
        private Grid _designHost;
        private Border _resizeHandle;
        private bool _isResizing;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _aspectRatio = 220.0 / 80.0;
        private double _designWidth = 220;
        private double _designHeight = 80;
        private const double MinClockWidth = 80;
        private const double MinClockHeight = 40;
        private const double MaxClockWidth = 640;
        private const double MaxClockHeight = 480;

        public CustomClockWidgetWindow() : this(null)
        {
        }

        public CustomClockWidgetWindow(CustomClockWidgetLayoutData layoutData)
        {
            _widgetId = layoutData != null && !string.IsNullOrEmpty(layoutData.Id)
                ? layoutData.Id
                : Guid.NewGuid().ToString();

            InitializeWindow();
            BuildUI();

            if (layoutData != null)
            {
                ApplyLayoutData(layoutData);
            }
            else
            {
                UpdateDesignDimensions();
                ApplyWindowSize(_designWidth, _designHeight);
            }

            SetupTimer();
            SetupContextMenu();
            this.Loaded += CustomClockWidgetWindow_Loaded;
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Reloj Extra";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.Width = 220;
            this.Height = 80;
            this.Left = 100;
            this.Top = 100;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && !_isResizing && !IsInteractiveTarget(e.OriginalSource as DependencyObject)
                    && e.ButtonState == MouseButtonState.Pressed)
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
                if (border != null && border.Cursor == WpfCursors.Hand)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void CustomClockWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }
        }

        private void BuildUI()
        {
            _cardBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };

            _timePanel = new StackPanel
            {
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center
            };

            _hoursText = new TextBlock { Text = "00", TextAlignment = TextAlignment.Center, Foreground = Brushes.White };
            _secondsSeparatorText = new TextBlock { Text = ":", TextAlignment = TextAlignment.Center, Foreground = Brushes.White };
            _minutesText = new TextBlock { Text = "00", TextAlignment = TextAlignment.Center, Foreground = Brushes.White };
            _amPmText = new TextBlock { Text = "AM", TextAlignment = TextAlignment.Center, Foreground = Brushes.White, Margin = new Thickness(6, 0, 0, 0) };

            _cardBorder.Child = _timePanel;

            _designHost = new Grid
            {
                Width = _designWidth,
                Height = _designHeight
            };
            _designHost.Children.Add(_cardBorder);

            _rootGrid = new Grid();
            _contentViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both
            };
            _contentViewbox.Child = _designHost;
            _rootGrid.Children.Add(_contentViewbox);

            _resizeHandle = CreateResizeHandle();
            _rootGrid.Children.Add(_resizeHandle);

            this.Content = _rootGrid;
            UpdateLayoutMode();
        }

        private Border CreateResizeHandle()
        {
            Border handle = new Border
            {
                Width = 16,
                Height = 16,
                HorizontalAlignment = WpfHorizontalAlignment.Right,
                VerticalAlignment = WpfVerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                CornerRadius = new CornerRadius(4, 0, 0, 0),
                Cursor = WpfCursors.Hand,
                ToolTip = "Arrastra para cambiar tamano"
            };

            handle.Child = new TextBlock
            {
                Text = "\uE7E8",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 8,
                Foreground = Brushes.White,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center
            };

            handle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
            handle.MouseMove += ResizeHandle_MouseMove;
            handle.MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
            return handle;
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked) return;

            _isResizing = true;
            _resizeStartPoint = e.GetPosition(this);
            _resizeStartWidth = this.Width;
            _resizeStartHeight = this.Height;
            _aspectRatio = _resizeStartWidth / Math.Max(_resizeStartHeight, 1);
            _resizeHandle.CaptureMouse();
            e.Handled = true;
        }

        private void ResizeHandle_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isResizing) return;

            Point current = e.GetPosition(this);
            double deltaX = current.X - _resizeStartPoint.X;
            double deltaY = current.Y - _resizeStartPoint.Y;
            double delta = Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX : deltaY;

            double newWidth = ClampClockSize(_resizeStartWidth + delta, true);
            double newHeight = ClampClockSize(newWidth / _aspectRatio, false);

            this.Width = newWidth;
            this.Height = newHeight;
            WidgetRegistry.AutoSaveLayout();
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing) return;
            _isResizing = false;
            _resizeHandle.ReleaseMouseCapture();
        }

        private static double ClampClockSize(double value, bool isWidth)
        {
            double min = isWidth ? MinClockWidth : MinClockHeight;
            double max = isWidth ? MaxClockWidth : MaxClockHeight;
            return Math.Max(min, Math.Min(max, value));
        }

        private void SetDesignSize(double width, double height)
        {
            _designWidth = width;
            _designHeight = height;
            if (_designHost != null)
            {
                _designHost.Width = _designWidth;
                _designHost.Height = _designHeight;
            }
        }

        private void ApplyWindowSize(double width, double height)
        {
            this.Width = ClampClockSize(width, true);
            this.Height = ClampClockSize(height, false);
            if (this.Height > 0)
            {
                _aspectRatio = this.Width / this.Height;
            }
        }

        private void UpdateDesignDimensions()
        {
            double width = _isVertical ? _fontSize * 2.4 : _fontSize * 5.2;
            double height = _isVertical ? _fontSize * 3.8 : _fontSize * 1.6;
            if (_showAmPm)
            {
                width += _isVertical ? 0 : _fontSize * 0.9;
                height += _isVertical ? _fontSize * 0.8 : 0;
            }

            SetDesignSize(Math.Max(80, width), Math.Max(40, height));
        }

        private void SetupTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateTime();
            _timer.Start();
            UpdateTime();
        }

        private void UpdateTime()
        {
            DateTime now = DateTime.Now;
            int hour = now.Hour;
            string ampm = hour >= 12 ? "PM" : "AM";
            if (hour == 0) hour = 12;
            else if (hour > 12) hour -= 12;

            _hoursText.Text = hour.ToString("00");
            _minutesText.Text = now.Minute.ToString("00");
            _amPmText.Text = ampm;

            if (!_isVertical)
            {
                _secondsSeparatorText.Visibility = (now.Second % 2 == 0) ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void UpdateLayoutMode()
        {
            _timePanel.Children.Clear();
            ApplyFontToText(_hoursText);
            ApplyFontToText(_minutesText);
            ApplyFontToText(_secondsSeparatorText);
            ApplyFontToText(_amPmText);

            if (_isVertical)
            {
                _timePanel.Orientation = WpfOrientation.Vertical;
                _timePanel.Children.Add(_hoursText);
                _timePanel.Children.Add(_minutesText);
                if (_showAmPm)
                {
                    _amPmText.Margin = new Thickness(0, 4, 0, 0);
                    _timePanel.Children.Add(_amPmText);
                }
            }
            else
            {
                _timePanel.Orientation = WpfOrientation.Horizontal;
                _secondsSeparatorText.Text = ":";
                _secondsSeparatorText.Margin = new Thickness(2, 0, 2, 0);
                _timePanel.Children.Add(_hoursText);
                _timePanel.Children.Add(_secondsSeparatorText);
                _timePanel.Children.Add(_minutesText);
                if (_showAmPm)
                {
                    _amPmText.Margin = new Thickness(8, 0, 0, 0);
                    _timePanel.Children.Add(_amPmText);
                }
            }

            UpdateDesignDimensions();
            UpdateTime();
            ApplyAdaptiveColors();
        }

        private void ApplyFontToText(TextBlock textBlock)
        {
            try
            {
                textBlock.FontFamily = new FontFamily(_fontFamily);
                textBlock.FontSize = _fontSize;
                textBlock.FontStyle = _fontStyle.Equals("Italic", StringComparison.OrdinalIgnoreCase) ? FontStyles.Italic : FontStyles.Normal;
                textBlock.FontWeight = _fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase) ? FontWeights.Bold : FontWeights.Normal;
            }
            catch
            {
                textBlock.FontFamily = new FontFamily("Segoe UI");
            }
        }

        private void ApplyAdaptiveColors()
        {
            Brush primary = Brushes.White;
            Brush secondary = new SolidColorBrush(Color.FromArgb(210, 230, 230, 240));

            if (_adaptToBackground)
            {
                WidgetAppearanceColors colors = WidgetAppearanceHelper.ComputeColors(
                    WidgetThemeMode.Light,
                    true,
                    100,
                    WidgetRegistry.GetActiveWallpaperPath(),
                    this.Left,
                    this.Top,
                    Math.Max(this.ActualWidth > 0 ? this.ActualWidth : this.Width, MinClockWidth),
                    Math.Max(this.ActualHeight > 0 ? this.ActualHeight : this.Height, MinClockHeight));

                primary = new SolidColorBrush(colors.Foreground);
                secondary = new SolidColorBrush(colors.SecondaryForeground);
            }

            _hoursText.Foreground = primary;
            _minutesText.Foreground = primary;
            _secondsSeparatorText.Foreground = primary;
            _amPmText.Foreground = secondary;
        }

        private void SetupContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem changeFont = new MenuItem { Header = "Cambiar Fuente..." };
            changeFont.Click += (s, e) => ShowFontPicker();
            menu.Items.Add(changeFont);

            MenuItem orientationMenu = new MenuItem { Header = "Orientación" };
            MenuItem horiz = new MenuItem { Header = "Horizontal", IsCheckable = true, IsChecked = !_isVertical };
            horiz.Click += (s, e) => { _isVertical = false; UpdateLayoutMode(); SetupContextMenu(); NotifyLayoutChanged(); };
            MenuItem vert = new MenuItem { Header = "Vertical", IsCheckable = true, IsChecked = _isVertical };
            vert.Click += (s, e) => { _isVertical = true; UpdateLayoutMode(); SetupContextMenu(); NotifyLayoutChanged(); };
            orientationMenu.Items.Add(horiz);
            orientationMenu.Items.Add(vert);
            menu.Items.Add(orientationMenu);

            MenuItem ampmToggle = new MenuItem { Header = "Mostrar AM/PM", IsCheckable = true, IsChecked = _showAmPm };
            ampmToggle.Click += (s, e) =>
            {
                _showAmPm = ampmToggle.IsChecked;
                UpdateLayoutMode();
                NotifyLayoutChanged();
            };
            menu.Items.Add(ampmToggle);

            MenuItem adaptItem = new MenuItem { Header = "Adaptar color al fondo", IsCheckable = true, IsChecked = _adaptToBackground };
            adaptItem.Click += (s, e) =>
            {
                _adaptToBackground = adaptItem.IsChecked;
                ApplyAdaptiveColors();
                NotifyLayoutChanged();
            };
            menu.Items.Add(adaptItem);

            menu.Items.Add(new Separator());

            MenuItem lockPos = new MenuItem { Header = "Bloquear posición", IsCheckable = true, IsChecked = _isLocked };
            lockPos.Click += (s, e) =>
            {
                _isLocked = lockPos.IsChecked;
                if (_resizeHandle != null)
                {
                    _resizeHandle.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
                }
                NotifyLayoutChanged();
            };
            menu.Items.Add(lockPos);

            menu.Items.Add(new Separator());

            MenuItem closeItem = new MenuItem { Header = "Cerrar widget" };
            closeItem.Click += (s, e) => this.Close();
            menu.Items.Add(closeItem);

            _cardBorder.ContextMenu = menu;
        }

        private void ShowFontPicker()
        {
            try
            {
                using (FontDialog dialog = new FontDialog())
                {
                    float sizePoints = (float)(_fontSize * 72.0 / 96.0);
                    System.Drawing.FontStyle style = System.Drawing.FontStyle.Regular;
                    if (_fontStyle.Equals("Italic", StringComparison.OrdinalIgnoreCase)) style |= System.Drawing.FontStyle.Italic;
                    if (_fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase)) style |= System.Drawing.FontStyle.Bold;

                    try
                    {
                        dialog.Font = new System.Drawing.Font(_fontFamily, sizePoints, style);
                    }
                    catch
                    {
                        dialog.Font = new System.Drawing.Font("Segoe UI", 36, System.Drawing.FontStyle.Regular);
                    }

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _fontFamily = dialog.Font.FontFamily.Name;
                        _fontSize = dialog.Font.Size * 96.0 / 72.0;
                        _fontStyle = dialog.Font.Italic ? "Italic" : "Normal";
                        _fontWeight = dialog.Font.Bold ? "Bold" : "Normal";
                        UpdateLayoutMode();
                        NotifyLayoutChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir selector de fuentes:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NotifyLayoutChanged()
        {
            WidgetRegistry.AutoSaveLayout();
        }

        public CustomClockWidgetLayoutData ToLayoutData()
        {
            return new CustomClockWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                FontFamily = _fontFamily,
                FontSize = _fontSize,
                FontStyle = _fontStyle,
                FontWeight = _fontWeight,
                IsVertical = _isVertical,
                ShowAmPm = _showAmPm,
                Width = this.Width,
                Height = this.Height,
                AdaptToBackground = _adaptToBackground
            };
        }

        public void ApplyLayoutData(CustomClockWidgetLayoutData data)
        {
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.Id)) _widgetId = data.Id;

            _isLocked = data.IsLocked;
            this.Left = data.Left;
            this.Top = data.Top;
            _fontFamily = data.FontFamily;
            _fontSize = data.FontSize;
            _fontStyle = data.FontStyle;
            _fontWeight = data.FontWeight;
            _isVertical = data.IsVertical;
            _showAmPm = data.ShowAmPm;
            _adaptToBackground = data.AdaptToBackground;

            UpdateLayoutMode();

            if (data.Width >= MinClockWidth && data.Height >= MinClockHeight)
            {
                ApplyWindowSize(data.Width, data.Height);
            }

            if (_resizeHandle != null)
            {
                _resizeHandle.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
            }

            SetupContextMenu();
        }
    }
}
