using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;

namespace WidgUI
{
    public enum CalendarStyleVariant
    {
        Standard = 0,
        Glass = 1,
        TimeGrid = 2
    }

    public class CalendarWidgetWindow : Window
    {
        private const double StandardWidth = 400;
        private const double StandardHeight = 150;
        private const double GlassWidth = 430;
        private const double GlassHeight = 158;
        private const double TimeGridWidth = 300;
        private const double TimeGridHeight = 310;

        private bool _isLocked;
        private bool _embeddedInDesktop = true;
        private string _widgetId;
        private CalendarStyleVariant _styleVariant = CalendarStyleVariant.Standard;
        private WidgetThemeMode _themeMode = WidgetThemeMode.Light;
        private bool _adaptToBackground;
        private double _opacity = WidgetAppearanceHelper.DefaultOpacity;
        private WidgetAppearanceColors _appearanceColors;
        private MediaColor _solidBaseColor = MediaColor.FromRgb(240, 245, 255);

        private Border _cardBorder;
        private Grid _twoColumnRoot;
        private StackPanel _leftPanel;
        private StackPanel _rightPanel;
        private StackPanel _timeGridPanel;
        private TextBlock _dayNameText;
        private TextBlock _dayNumberText;
        private TextBlock _eventsText;
        private TextBlock _monthNameText;
        private TextBlock _timeText;
        private Grid _calendarGrid;
        private DispatcherTimer _timer;

        public CalendarWidgetWindow() : this(null)
        {
        }

        public CalendarWidgetWindow(CalendarWidgetLayoutData layoutData)
        {
            _widgetId = layoutData != null && !string.IsNullOrEmpty(layoutData.Id)
                ? layoutData.Id
                : Guid.NewGuid().ToString();

            InitializeWindow();
            BuildUI();
            SetupContextMenu();
            SetupTimer();
            this.Loaded += CalendarWidgetWindow_Loaded;

            if (layoutData != null)
            {
                ApplyLayoutData(layoutData);
            }
            else
            {
                ApplyAppearance();
            }
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Calendario";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            ApplyWindowSize();
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 60;
            this.Top = 180;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    WidgetSnapHelper.BeginSnapDrag(this, e);
                }
            };
        }

        private void CalendarWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }
        }

        private void ApplyWindowSize()
        {
            if (_styleVariant == CalendarStyleVariant.Glass)
            {
                this.Width = GlassWidth;
                this.Height = GlassHeight;
            }
            else if (_styleVariant == CalendarStyleVariant.TimeGrid)
            {
                this.Width = TimeGridWidth;
                this.Height = TimeGridHeight;
            }
            else
            {
                this.Width = StandardWidth;
                this.Height = StandardHeight;
            }
        }

        private void BuildUI()
        {
            _cardBorder = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16)
            };

            // --- Árbol para Standard / Glass (dos columnas) ---
            _twoColumnRoot = new Grid();
            _twoColumnRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            _twoColumnRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _leftPanel = new StackPanel
            {
                Margin = new Thickness(4, 0, 8, 0)
            };

            _dayNameText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };

            _dayNumberText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 58,
                FontWeight = FontWeights.Light,
                Margin = new Thickness(0, -6, 0, 0)
            };

            _eventsText = new TextBlock
            {
                Text = "Sin eventos hoy",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetColumn(_leftPanel, 0);
            _twoColumnRoot.Children.Add(_leftPanel);

            _rightPanel = new StackPanel
            {
                Margin = new Thickness(8, 0, 4, 0)
            };

            _monthNameText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };

            _calendarGrid = new Grid();
            for (int i = 0; i < 7; i++)
            {
                _calendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            for (int i = 0; i < 7; i++)
            {
                _calendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            Grid.SetColumn(_rightPanel, 1);
            _twoColumnRoot.Children.Add(_rightPanel);

            // --- Árbol para TimeGrid (vertical: hora, mes, grilla completa) ---
            _timeGridPanel = new StackPanel();

            _timeText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 46,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };

            this.Content = _cardBorder;
            ApplyStyleVariant(_styleVariant);
        }

        // Quita un elemento de cualquier panel que lo contenga actualmente,
        // para poder reasignarlo al árbol visual correcto al cambiar de variante.
        private static void DetachFromParent(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            Panel panel = element.Parent as Panel;
            if (panel != null)
            {
                panel.Children.Remove(element);
            }
        }

        private void RebuildContentTree()
        {
            DetachFromParent(_dayNameText);
            DetachFromParent(_dayNumberText);
            DetachFromParent(_eventsText);
            DetachFromParent(_monthNameText);
            DetachFromParent(_timeText);
            DetachFromParent(_calendarGrid);

            if (_styleVariant == CalendarStyleVariant.TimeGrid)
            {
                _timeGridPanel.Children.Add(_timeText);
                _timeGridPanel.Children.Add(_monthNameText);
                _timeGridPanel.Children.Add(_calendarGrid);
                _cardBorder.Child = _timeGridPanel;
            }
            else
            {
                _leftPanel.Children.Add(_dayNameText);
                _leftPanel.Children.Add(_dayNumberText);
                _leftPanel.Children.Add(_eventsText);

                _rightPanel.Children.Add(_monthNameText);
                _rightPanel.Children.Add(_calendarGrid);

                _cardBorder.Child = _twoColumnRoot;
            }
        }

        private void ApplyStyleVariant(CalendarStyleVariant variant)
        {
            _styleVariant = variant;
            ApplyWindowSize();

            if (_cardBorder == null)
            {
                return;
            }

            if (variant == CalendarStyleVariant.Glass)
            {
                _cardBorder.CornerRadius = new CornerRadius(28);
                _cardBorder.BorderThickness = new Thickness(0);
                _cardBorder.Padding = new Thickness(20, 18, 18, 18);

                _leftPanel.VerticalAlignment = VerticalAlignment.Top;
                _rightPanel.VerticalAlignment = VerticalAlignment.Top;

                _dayNameText.FontSize = 10.5;
                _dayNameText.FontWeight = FontWeights.Bold;
                _dayNumberText.FontSize = 64;
                _dayNumberText.FontWeight = FontWeights.Light;
                _dayNumberText.Margin = new Thickness(0, -4, 0, 0);
                _eventsText.FontSize = 11.5;
                _eventsText.FontWeight = FontWeights.Normal;
                _eventsText.Opacity = 0.72;
                _monthNameText.FontSize = 10.5;
                _monthNameText.FontWeight = FontWeights.Bold;
                _monthNameText.Margin = new Thickness(0, 0, 0, 8);
                _monthNameText.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else if (variant == CalendarStyleVariant.TimeGrid)
            {
                _cardBorder.CornerRadius = new CornerRadius(24);
                _cardBorder.BorderThickness = new Thickness(0);
                _cardBorder.Padding = new Thickness(22, 20, 22, 18);

                _timeText.FontSize = 46;
                _timeText.FontWeight = FontWeights.Bold;
                _timeText.Margin = new Thickness(0, 0, 0, 10);

                _monthNameText.FontSize = 12;
                _monthNameText.FontWeight = FontWeights.Bold;
                _monthNameText.Margin = new Thickness(0, 0, 0, 6);
                _monthNameText.HorizontalAlignment = HorizontalAlignment.Right;
            }
            else
            {
                _cardBorder.CornerRadius = new CornerRadius(26);
                _cardBorder.BorderThickness = new Thickness(1);
                _cardBorder.Padding = new Thickness(16);

                _leftPanel.VerticalAlignment = VerticalAlignment.Center;
                _rightPanel.VerticalAlignment = VerticalAlignment.Center;

                _dayNameText.FontSize = 11;
                _dayNameText.FontWeight = FontWeights.SemiBold;
                _dayNumberText.FontSize = 58;
                _dayNumberText.FontWeight = FontWeights.Light;
                _dayNumberText.Margin = new Thickness(0, -6, 0, 0);
                _eventsText.FontSize = 12;
                _eventsText.FontWeight = FontWeights.Normal;
                _eventsText.Opacity = 1;
                _monthNameText.FontSize = 11;
                _monthNameText.FontWeight = FontWeights.SemiBold;
                _monthNameText.Margin = new Thickness(0, 0, 0, 6);
                _monthNameText.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            RebuildContentTree();
        }

        private void SetupTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _timer.Tick += (s, e) => UpdateCalendarDisplay();
            _timer.Start();
        }

        private void UpdateCalendarDisplay()
        {
            DateTime now = DateTime.Now;
            CultureInfo culture = new CultureInfo("es-ES");

            if (_styleVariant == CalendarStyleVariant.TimeGrid)
            {
                _timeText.Text = now.ToString("HH:mm", culture);
                _monthNameText.Text = culture.DateTimeFormat.GetMonthName(now.Month).ToUpper(culture);
                ApplyTextColors();
                BuildMonthGridTimeGrid(now);
                return;
            }

            _dayNameText.Text = culture.DateTimeFormat.GetDayName(now.DayOfWeek).ToUpper(culture);
            _dayNumberText.Text = now.Day.ToString(culture);
            _monthNameText.Text = culture.DateTimeFormat.GetMonthName(now.Month).ToUpper(culture);

            ApplyTextColors();
            BuildMonthGrid(now);
        }

        private void BuildMonthGrid(DateTime now)
        {
            _calendarGrid.Children.Clear();

            string[] dayLetters = { "D", "L", "M", "X", "J", "V", "S" };
            Brush primary = new SolidColorBrush(_appearanceColors.Foreground);
            Brush secondary = new SolidColorBrush(_appearanceColors.SecondaryForeground);
            bool lightForeground = IsLightForeground();

            for (int col = 0; col < 7; col++)
            {
                _calendarGrid.Children.Add(CreateDayCell(dayLetters[col], true, false, primary, secondary, lightForeground));
                Grid.SetRow(_calendarGrid.Children[_calendarGrid.Children.Count - 1], 0);
                Grid.SetColumn(_calendarGrid.Children[_calendarGrid.Children.Count - 1], col);
            }

            DateTime firstOfMonth = new DateTime(now.Year, now.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int colIndex = (int)firstOfMonth.DayOfWeek;
            int row = 1;

            for (int day = 1; day <= daysInMonth; day++)
            {
                bool isToday = day == now.Day;
                UIElement cell = CreateDayCell(day.ToString(), false, isToday, primary, secondary, lightForeground);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, colIndex);
                _calendarGrid.Children.Add(cell);

                colIndex++;
                if (colIndex >= 7)
                {
                    colIndex = 0;
                    row++;
                }
            }
        }

        // Grilla estilo iOS: semana empieza en lunes, sin conteo de eventos,
        // días pasados en gris y día actual resaltado con un cuadrado.
        private void BuildMonthGridTimeGrid(DateTime now)
        {
            _calendarGrid.Children.Clear();

            string[] dayLetters = { "L", "M", "X", "J", "V", "S", "D" };

            for (int col = 0; col < 7; col++)
            {
                UIElement headerCell = CreateTimeGridDayCell(dayLetters[col], true, false, false);
                Grid.SetRow(headerCell, 0);
                Grid.SetColumn(headerCell, col);
                _calendarGrid.Children.Add(headerCell);
            }

            DateTime firstOfMonth = new DateTime(now.Year, now.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
            int colIndex = ((int)firstOfMonth.DayOfWeek + 6) % 7; // lunes = 0
            int row = 1;

            for (int day = 1; day <= daysInMonth; day++)
            {
                bool isToday = day == now.Day;
                bool isPast = day < now.Day;
                UIElement cell = CreateTimeGridDayCell(day.ToString(), false, isToday, isPast);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, colIndex);
                _calendarGrid.Children.Add(cell);

                colIndex++;
                if (colIndex >= 7)
                {
                    colIndex = 0;
                    row++;
                }
            }
        }

        private UIElement CreateDayCell(string text, bool isHeader, bool isToday, Brush primary, Brush secondary, bool lightForeground)
        {
            double cellWidth = _styleVariant == CalendarStyleVariant.Glass ? 30 : 28;
            double cellHeight = isHeader
                ? (_styleVariant == CalendarStyleVariant.Glass ? 16 : 18)
                : (_styleVariant == CalendarStyleVariant.Glass ? 26 : 24);
            double todaySize = _styleVariant == CalendarStyleVariant.Glass ? 24 : 22;

            Grid host = new Grid
            {
                Width = cellWidth,
                Height = cellHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isToday)
            {
                host.Children.Add(new Ellipse
                {
                    Width = todaySize,
                    Height = todaySize,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Brush dayForeground = isToday
                ? new SolidColorBrush(_solidBaseColor)
                : (isHeader ? secondary : primary);

            if (_styleVariant == CalendarStyleVariant.Glass && !isToday && !isHeader)
            {
                dayForeground = new SolidColorBrush(MediaColor.FromArgb(205, 255, 255, 255));
            }

            TextBlock label = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = isHeader
                    ? (_styleVariant == CalendarStyleVariant.Glass ? 9.5 : 10)
                    : (_styleVariant == CalendarStyleVariant.Glass ? 10.5 : 11),
                FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = dayForeground
            };

            if (isToday)
            {
                label.FontWeight = FontWeights.SemiBold;
            }

            host.Children.Add(label);
            return host;
        }

        private UIElement CreateTimeGridDayCell(string text, bool isHeader, bool isToday, bool isPast)
        {
            const double cellWidth = 34;
            double cellHeight = isHeader ? 20 : 30;
            const double todaySize = 26;

            Grid host = new Grid
            {
                Width = cellWidth,
                Height = cellHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isToday)
            {
                host.Children.Add(new Border
                {
                    Width = todaySize,
                    Height = todaySize,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(_appearanceColors.Foreground),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Brush dayForeground;
            if (isToday)
            {
                dayForeground = new SolidColorBrush(_solidBaseColor);
            }
            else if (isHeader)
            {
                dayForeground = new SolidColorBrush(_appearanceColors.Foreground);
            }
            else if (isPast)
            {
                dayForeground = new SolidColorBrush(_appearanceColors.SecondaryForeground);
            }
            else
            {
                dayForeground = new SolidColorBrush(_appearanceColors.Foreground);
            }

            TextBlock label = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = isHeader ? 12 : 13,
                FontWeight = (isHeader || isToday) ? FontWeights.SemiBold : (isPast ? FontWeights.Normal : FontWeights.Medium),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = dayForeground
            };

            host.Children.Add(label);
            return host;
        }

        private bool IsLightForeground()
        {
            if (_styleVariant == CalendarStyleVariant.Glass && !_adaptToBackground)
            {
                return true;
            }

            return _appearanceColors.Foreground.R > 180;
        }

        private void ApplyTextColors()
        {
            if (_styleVariant == CalendarStyleVariant.Glass)
            {
                _dayNameText.Foreground = new SolidColorBrush(MediaColor.FromArgb(220, 255, 255, 255));
                _dayNumberText.Foreground = Brushes.White;
                _eventsText.Foreground = new SolidColorBrush(MediaColor.FromArgb(180, 255, 255, 255));
                _monthNameText.Foreground = new SolidColorBrush(MediaColor.FromArgb(220, 255, 255, 255));
                return;
            }

            if (_styleVariant == CalendarStyleVariant.TimeGrid)
            {
                _timeText.Foreground = new SolidColorBrush(_appearanceColors.Foreground);
                _monthNameText.Foreground = new SolidColorBrush(_appearanceColors.SecondaryForeground);
                return;
            }

            Brush primary = new SolidColorBrush(_appearanceColors.Foreground);
            Brush secondary = new SolidColorBrush(_appearanceColors.SecondaryForeground);

            _dayNameText.Foreground = secondary;
            _dayNumberText.Foreground = primary;
            _eventsText.Foreground = secondary;
            _monthNameText.Foreground = secondary;
        }

        private void RefreshAppearanceColors()
        {
            if (_styleVariant == CalendarStyleVariant.Glass && !_adaptToBackground)
            {
                ApplyGlassPresetColors();
                return;
            }

            _appearanceColors = WidgetAppearanceHelper.ComputeColors(
                _themeMode,
                _adaptToBackground,
                _opacity,
                WidgetRegistry.GetActiveWallpaperPath(),
                this.Left,
                this.Top,
                this.Width,
                this.Height);

            if (_styleVariant == CalendarStyleVariant.Glass)
            {
                _appearanceColors.Foreground = MediaColor.FromRgb(255, 255, 255);
                _appearanceColors.SecondaryForeground = MediaColor.FromArgb(185, 255, 255, 255);
                _appearanceColors.Border = MediaColor.FromArgb(0, 0, 0, 0);
                _solidBaseColor = MediaColor.FromRgb(
                    _appearanceColors.Background.R,
                    _appearanceColors.Background.G,
                    _appearanceColors.Background.B);
                return;
            }

            UpdateSolidBaseFromTheme();
        }

        private void ApplyGlassPresetColors()
        {
            byte alpha = WidgetAppearanceHelper.ToAlpha(_opacity);
            MediaColor baseColor = _themeMode == WidgetThemeMode.Dark
                ? MediaColor.FromRgb(42, 36, 44)
                : MediaColor.FromRgb(168, 138, 152);

            _solidBaseColor = baseColor;
            _appearanceColors = new WidgetAppearanceColors
            {
                Background = MediaColor.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B),
                Border = MediaColor.FromArgb(0, baseColor.R, baseColor.G, baseColor.B),
                Foreground = MediaColor.FromRgb(255, 255, 255),
                SecondaryForeground = MediaColor.FromArgb(185, 255, 255, 255),
                Separator = MediaColor.FromArgb(90, 255, 255, 255),
                AccentSurface = MediaColor.FromArgb(70, 255, 255, 255)
            };
        }

        private void UpdateSolidBaseFromTheme()
        {
            if (_adaptToBackground && !string.IsNullOrEmpty(WidgetRegistry.GetActiveWallpaperPath()))
            {
                _solidBaseColor = MediaColor.FromRgb(
                    _appearanceColors.Background.R,
                    _appearanceColors.Background.G,
                    _appearanceColors.Background.B);
            }
            else if (_themeMode == WidgetThemeMode.Dark)
            {
                _solidBaseColor = MediaColor.FromRgb(28, 28, 34);
            }
            else
            {
                _solidBaseColor = MediaColor.FromRgb(240, 245, 255);
            }
        }

        private void ApplyAppearance()
        {
            RefreshAppearanceColors();

            if (_cardBorder != null)
            {
                _cardBorder.Background = new SolidColorBrush(_appearanceColors.Background);
                _cardBorder.BorderBrush = new SolidColorBrush(_appearanceColors.Border);
            }

            UpdateCalendarDisplay();
        }

        private void SetupContextMenu()
        {
            ContextMenu cm = new ContextMenu();

            MenuItem styleMenu = new MenuItem { Header = "Estilo" };
            MenuItem standardStyle = new MenuItem
            {
                Header = "Estándar",
                IsCheckable = true,
                IsChecked = _styleVariant == CalendarStyleVariant.Standard
            };
            standardStyle.Click += (s, e) =>
            {
                ApplyStyleVariant(CalendarStyleVariant.Standard);
                ApplyAppearance();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            MenuItem glassStyle = new MenuItem
            {
                Header = "Cristal",
                IsCheckable = true,
                IsChecked = _styleVariant == CalendarStyleVariant.Glass
            };
            glassStyle.Click += (s, e) =>
            {
                ApplyStyleVariant(CalendarStyleVariant.Glass);
                ApplyAppearance();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            MenuItem timeGridStyle = new MenuItem
            {
                Header = "Reloj y grilla",
                IsCheckable = true,
                IsChecked = _styleVariant == CalendarStyleVariant.TimeGrid
            };
            timeGridStyle.Click += (s, e) =>
            {
                ApplyStyleVariant(CalendarStyleVariant.TimeGrid);
                ApplyAppearance();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            styleMenu.Items.Add(standardStyle);
            styleMenu.Items.Add(glassStyle);
            styleMenu.Items.Add(timeGridStyle);
            cm.Items.Add(styleMenu);
            cm.Items.Add(new Separator());

            MenuItem appearanceMenu = new MenuItem { Header = "Apariencia" };

            MenuItem lightItem = new MenuItem
            {
                Header = "Modo claro",
                IsCheckable = true,
                IsChecked = _themeMode == WidgetThemeMode.Light && !_adaptToBackground
            };
            lightItem.Click += (s, e) =>
            {
                _themeMode = WidgetThemeMode.Light;
                _adaptToBackground = false;
                ApplyAppearance();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            MenuItem darkItem = new MenuItem
            {
                Header = "Modo oscuro",
                IsCheckable = true,
                IsChecked = _themeMode == WidgetThemeMode.Dark && !_adaptToBackground
            };
            darkItem.Click += (s, e) =>
            {
                _themeMode = WidgetThemeMode.Dark;
                _adaptToBackground = false;
                ApplyAppearance();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            MenuItem adaptItem = new MenuItem
            {
                Header = "Adaptar al fondo",
                IsCheckable = true,
                IsChecked = _adaptToBackground
            };
            adaptItem.Click += (s, e) =>
            {
                _adaptToBackground = adaptItem.IsChecked;
                ApplyAppearance();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            MenuItem opacityMenu = new MenuItem { Header = "Opacidad" };
            foreach (double preset in WidgetAppearanceHelper.OpacityPresets)
            {
                MenuItem opacityItem = new MenuItem
                {
                    Header = preset + "%",
                    IsCheckable = true,
                    IsChecked = Math.Abs(_opacity - preset) < 0.5
                };
                double targetOpacity = preset;
                opacityItem.Click += (s, e) =>
                {
                    _opacity = targetOpacity;
                    ApplyAppearance();
                    SetupContextMenu();
                    NotifyLayoutChanged();
                };
                opacityMenu.Items.Add(opacityItem);
            }

            appearanceMenu.Items.Add(lightItem);
            appearanceMenu.Items.Add(darkItem);
            appearanceMenu.Items.Add(adaptItem);
            appearanceMenu.Items.Add(opacityMenu);
            cm.Items.Add(appearanceMenu);
            cm.Items.Add(new Separator());

            MenuItem itemLock = new MenuItem { Header = "Bloquear Posición", IsCheckable = true, IsChecked = _isLocked };
            itemLock.Click += (s, e) =>
            {
                _isLocked = itemLock.IsChecked;
                NotifyLayoutChanged();
            };

            MenuItem itemExit = new MenuItem { Header = "Cerrar Widget" };
            itemExit.Click += (s, e) => this.Close();

            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }

        private void NotifyLayoutChanged()
        {
            WidgetRegistry.AutoSaveLayout();
        }

        public CalendarWidgetLayoutData ToLayoutData()
        {
            return new CalendarWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                ThemeMode = (int)_themeMode,
                AdaptToBackground = _adaptToBackground,
                Opacity = _opacity,
                StyleVariant = (int)_styleVariant
            };
        }

        public void ApplyLayoutData(CalendarWidgetLayoutData data)
        {
            if (data == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(data.Id))
            {
                _widgetId = data.Id;
            }

            _isLocked = data.IsLocked;
            this.Left = data.Left;
            this.Top = data.Top;

            if (Enum.IsDefined(typeof(WidgetThemeMode), data.ThemeMode))
            {
                _themeMode = (WidgetThemeMode)data.ThemeMode;
            }

            _adaptToBackground = data.AdaptToBackground;
            if (data.Opacity > 0)
            {
                _opacity = data.Opacity;
            }

            if (Enum.IsDefined(typeof(CalendarStyleVariant), data.StyleVariant))
            {
                ApplyStyleVariant((CalendarStyleVariant)data.StyleVariant);
            }

            ApplyAppearance();
            SetupContextMenu();
        }
    }
}