using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Forms; // for FontDialog
using MessageBox = System.Windows.MessageBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace WidgUI
{
    public class CustomClockWidgetWindow : Window
    {
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;
        private string _widgetId;
        
        // Font settings
        private string _fontFamily = "Segoe UI";
        private double _fontSize = 48.0;
        private string _fontStyle = "Normal";
        private string _fontWeight = "Normal";

        // Layout settings
        private bool _isVertical = false;
        private bool _showAmPm = true;

        private Border _cardBorder;
        private StackPanel _timePanel;
        private TextBlock _hoursText;
        private TextBlock _minutesText;
        private TextBlock _secondsSeparatorText;
        private TextBlock _amPmText;
        private DispatcherTimer _timer;

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
            this.SizeToContent = SizeToContent.WidthAndHeight;

            // Positioning defaults
            this.Left = 100;
            this.Top = 100;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
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
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _hoursText = new TextBlock
            {
                Text = "00",
                TextAlignment = TextAlignment.Center
            };

            _secondsSeparatorText = new TextBlock
            {
                Text = ":",
                TextAlignment = TextAlignment.Center
            };

            _minutesText = new TextBlock
            {
                Text = "00",
                TextAlignment = TextAlignment.Center
            };

            _amPmText = new TextBlock
            {
                Text = "AM",
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            _cardBorder.Child = _timePanel;
            this.Content = _cardBorder;

            UpdateLayoutMode();
        }

        private void SetupTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => UpdateTime();
            _timer.Start();
            UpdateTime();
        }

        private void UpdateTime()
        {
            DateTime now = DateTime.Now;
            
            // Hours
            int hour = now.Hour;
            string ampm = "AM";
            
            if (hour >= 12)
            {
                ampm = "PM";
            }
            
            // Convert to 12 hour format
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
            
            // Apply font configuration to all text items
            ApplyFontToText(_hoursText);
            ApplyFontToText(_minutesText);
            ApplyFontToText(_secondsSeparatorText);
            ApplyFontToText(_amPmText);

            if (_isVertical)
            {
                _timePanel.Orientation = System.Windows.Controls.Orientation.Vertical;

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
                _timePanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
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

            UpdateTime();
        }

        private void ApplyFontToText(TextBlock textBlock)
        {
            try
            {
                textBlock.FontFamily = new FontFamily(_fontFamily);
                textBlock.FontSize = _fontSize;
                
                textBlock.FontStyle = _fontStyle.Equals("Italic", StringComparison.OrdinalIgnoreCase) 
                    ? FontStyles.Italic 
                    : FontStyles.Normal;

                textBlock.FontWeight = _fontWeight.Equals("Bold", StringComparison.OrdinalIgnoreCase) 
                    ? FontWeights.Bold 
                    : FontWeights.Normal;
            }
            catch
            {
                // Fallback
                textBlock.FontFamily = new FontFamily("Segoe UI");
            }
        }

        private void SetupContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem changeFont = new MenuItem { Header = "Cambiar Fuente..." };
            changeFont.Click += (s, e) => ShowFontPicker();
            menu.Items.Add(changeFont);

            MenuItem orientationMenu = new MenuItem { Header = "Orientación" };
            MenuItem horiz = new MenuItem { Header = "Horizontal", IsCheckable = true, IsChecked = !_isVertical };
            horiz.Click += (s, e) => { _isVertical = false; UpdateLayoutMode(); SetupContextMenu(); };
            MenuItem vert = new MenuItem { Header = "Vertical", IsCheckable = true, IsChecked = _isVertical };
            vert.Click += (s, e) => { _isVertical = true; UpdateLayoutMode(); SetupContextMenu(); };
            orientationMenu.Items.Add(horiz);
            orientationMenu.Items.Add(vert);
            menu.Items.Add(orientationMenu);

            MenuItem ampmToggle = new MenuItem { Header = "Mostrar AM/PM", IsCheckable = true, IsChecked = _showAmPm };
            ampmToggle.Click += (s, e) =>
            {
                _showAmPm = ampmToggle.IsChecked;
                UpdateLayoutMode();
            };
            menu.Items.Add(ampmToggle);

            menu.Items.Add(new Separator());

            MenuItem lockPos = new MenuItem { Header = "Bloquear posición", IsCheckable = true, IsChecked = _isLocked };
            lockPos.Click += (s, e) =>
            {
                _isLocked = lockPos.IsChecked;
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
                    // Convert WPF font family & size to WinForms equivalents
                    float sizePoints = (float)(_fontSize * 72.0 / 96.0); // Convert WPF pixels to points
                    
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
                        // Convert points back to WPF pixels
                        _fontSize = dialog.Font.Size * 96.0 / 72.0;
                        _fontStyle = dialog.Font.Italic ? "Italic" : "Normal";
                        _fontWeight = dialog.Font.Bold ? "Bold" : "Normal";

                        UpdateLayoutMode();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir selector de fuentes:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Layout Serialization
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
                ShowAmPm = _showAmPm
            };
        }

        public void ApplyLayoutData(CustomClockWidgetLayoutData data)
        {
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.Id))
            {
                _widgetId = data.Id;
            }

            _isLocked = data.IsLocked;
            this.Left = data.Left;
            this.Top = data.Top;
            _fontFamily = data.FontFamily;
            _fontSize = data.FontSize;
            _fontStyle = data.FontStyle;
            _fontWeight = data.FontWeight;
            _isVertical = data.IsVertical;
            _showAmPm = data.ShowAmPm;

            UpdateLayoutMode();
            SetupContextMenu();
        }
        #endregion
    }
}
