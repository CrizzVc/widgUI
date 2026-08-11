using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace WidgUI
{
    public class EdgeMenuWindow : Window
    {
        #region Win32 API Imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;
        #endregion

        private MainWindow _mainWindow;
        
        private Border _collapsedIndicator;
        private Grid _layoutGrid;
        
        // Expanded main menu
        private Grid _expandedContainer;
        private TextBlock _appNameText;
        private Grid _buttonsGrid;
        
        // Action buttons
        private Border _btnAdd;
        private Border _btnSettings;
        private Border _btnWallpaper;

        // Subpanel border
        private Border _subPanelBorder;
        private Border _divider; // Dummy to prevent reference errors

        // Subpanels container
        private Grid _subPanelContainer;
        private Grid _stylePanel;
        private Grid _settingsPanel;
        private Grid _wallpaperPanel;

        // Settings items
        private Border _toggle24h;
        private Border _toggleDate;
        private Border _toggleLock;

        // Style items
        private Border _styleBtnMin;
        private Border _styleBtnGlass;
        private Border _styleBtnNeu;
        private Border _styleBtnCompact;

        // Active state
        private string _activeSubPanel = null; // "style", "settings", "wallpaper", or null
        private bool _isHovered = false;
        private DispatcherTimer _hoverTimer;

        public EdgeMenuWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            
            InitializeWindow();
            BuildUI();
            
            this.Loaded += EdgeMenuWindow_Loaded;
            this.MouseEnter += EdgeMenuWindow_MouseEnter;
            this.MouseLeave += EdgeMenuWindow_MouseLeave;

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(750);
            _hoverTimer.Tick += HoverTimer_Tick;
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI Menu";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false; // Do not display on top of apps
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowActivated = false;

            this.Width = 3; // Collapsed starting width is just a line!
            this.Height = 300;
            this.Left = 0;
            
            // Center vertically on primary screen
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Top = (screenHeight - this.Height) / 2 - 50;
        }

        private void EdgeMenuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            // Configure as toolwindow and no-activate
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            
            // Embed in desktop so it is only shown on the wallpaper, behind other apps
            DesktopManager.EmbedInDesktop(this);
        }

        private void BuildUI()
        {
            // Root Grid
            Grid rootGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // 1. Collapsed Indicator (glowing thin 3px line)
            _collapsedIndicator = new Border
            {
                Width = 3,
                Height = 120,
                Background = new SolidColorBrush(Color.FromArgb(180, 56, 189, 248)), // Glowing sky blue line
                CornerRadius = new CornerRadius(1.5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };
            rootGrid.Children.Add(_collapsedIndicator);

            // 2. Expanded Grid (Col 0: Tab shape & buttons, Col 1: Sub-panels)
            _layoutGrid = new Grid
            {
                Width = 262,
                Height = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });  // Tab (60px + 2px margin)
            _layoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Subpanel

            // 2.a. Curved Tab Path (Column 0)
            System.Windows.Shapes.Path menuPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,10 C 0,40 60,40 60,70 L 60,210 C 60,240 0,240 0,270 Z"),
                Fill = new SolidColorBrush(Color.FromArgb(235, 20, 20, 30)), // Elegant dark glass background
                Stroke = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)),
                StrokeThickness = 1.5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 4,
                    Opacity = 0.4,
                    BlurRadius = 12
                }
            };
            Grid.SetColumn(menuPath, 0);
            _layoutGrid.Children.Add(menuPath);

            // 2.b. Expanded Container (holds rotated text or vertical button stack)
            _expandedContainer = new Grid
            {
                Width = 60,
                Height = 140,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_expandedContainer, 0);

            // 2.c. App Name (rotated -90 degrees vertically)
            _appNameText = new TextBlock
            {
                Text = "widgUI",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Arial"),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                LayoutTransform = new RotateTransform(-90)
            };
            _expandedContainer.Children.Add(_appNameText);

            // 2.d. Vertical Buttons Grid
            _buttonsGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                Visibility = Visibility.Collapsed
            };
            _buttonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _buttonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _buttonsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create circular buttons stacked vertically
            _btnAdd = CreateIconButton("\uE710", "Diseño", Color.FromRgb(52, 211, 153)); // Green
            _btnAdd.MouseLeftButtonDown += (s, e) => ShowSubPanel("style");
            Grid.SetRow(_btnAdd, 0);
            _buttonsGrid.Children.Add(_btnAdd);

            _btnSettings = CreateIconButton("\uE713", "Ajustes", Color.FromRgb(129, 140, 248)); // Blue
            _btnSettings.MouseLeftButtonDown += (s, e) => ShowSubPanel("settings");
            Grid.SetRow(_btnSettings, 1);
            _buttonsGrid.Children.Add(_btnSettings);

            _btnWallpaper = CreateIconButton("\uE723", "Fondo", Color.FromRgb(244, 114, 182)); // Pink
            _btnWallpaper.MouseLeftButtonDown += (s, e) => ShowSubPanel("wallpaper");
            Grid.SetRow(_btnWallpaper, 2);
            _buttonsGrid.Children.Add(_btnWallpaper);

            _expandedContainer.Children.Add(_buttonsGrid);
            _layoutGrid.Children.Add(_expandedContainer);

            // 3. Sub-panel Border
            _subPanelBorder = new Border
            {
                Width = 200,
                Height = 140,
                Background = new SolidColorBrush(Color.FromArgb(235, 20, 20, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)),
                BorderThickness = new Thickness(0, 1.5, 1.5, 1.5),
                CornerRadius = new CornerRadius(0, 16, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 4,
                    Opacity = 0.4,
                    BlurRadius = 12
                }
            };
            Grid.SetColumn(_subPanelBorder, 1);

            _subPanelContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(10, 8, 10, 8)
            };

            BuildStylePanel();
            BuildSettingsPanel();
            BuildWallpaperPanel();

            _subPanelContainer.Children.Add(_stylePanel);
            _subPanelContainer.Children.Add(_settingsPanel);
            _subPanelContainer.Children.Add(_wallpaperPanel);
            _subPanelBorder.Child = _subPanelContainer;
            _layoutGrid.Children.Add(_subPanelBorder);

            // Dummy divider to prevent compiler errors
            _divider = new Border { Visibility = Visibility.Collapsed };

            rootGrid.Children.Add(_layoutGrid);
            this.Content = rootGrid;
        }

        private Border CreateIconButton(string glyph, string tooltip, Color hoverColor)
        {
            Border border = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 6, 0, 6), // Vertical spacing
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };

            TextBlock textBlock = new TextBlock
            {
                Text = glyph,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = textBlock;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(80, hoverColor.R, hoverColor.G, hoverColor.B));
                border.BorderBrush = new SolidColorBrush(hoverColor);
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            };

            return border;
        }

        #region Style Panel
        private void BuildStylePanel()
        {
            _stylePanel = new Grid { Visibility = Visibility.Collapsed };
            _stylePanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _stylePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock title = new TextBlock
            {
                Text = "ESTILO DEL RELOJ",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(4, 0, 0, 4)
            };
            Grid.SetRow(title, 0);
            _stylePanel.Children.Add(title);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _styleBtnMin = CreateStyleButton("Minimalista", WidgetStyleVariant.MinimalistVertical);
            Grid.SetRow(_styleBtnMin, 0);
            Grid.SetColumn(_styleBtnMin, 0);
            grid.Children.Add(_styleBtnMin);

            _styleBtnGlass = CreateStyleButton("Cristal", WidgetStyleVariant.GlassmorphismCard);
            Grid.SetRow(_styleBtnGlass, 0);
            Grid.SetColumn(_styleBtnGlass, 1);
            grid.Children.Add(_styleBtnGlass);

            _styleBtnNeu = CreateStyleButton("Neumórfico", WidgetStyleVariant.NeumorphismDark);
            Grid.SetRow(_styleBtnNeu, 1);
            Grid.SetColumn(_styleBtnNeu, 0);
            grid.Children.Add(_styleBtnNeu);

            _styleBtnCompact = CreateStyleButton("Compacto", WidgetStyleVariant.HorizontalCompact);
            Grid.SetRow(_styleBtnCompact, 1);
            Grid.SetColumn(_styleBtnCompact, 1);
            grid.Children.Add(_styleBtnCompact);

            Grid.SetRow(grid, 1);
            _stylePanel.Children.Add(grid);
        }

        private Border CreateStyleButton(string text, WidgetStyleVariant variant)
        {
            Border border = new Border
            {
                Height = 18,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            TextBlock tb = new TextBlock
            {
                Text = text,
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = tb;

            border.MouseEnter += (s, e) =>
            {
                if (_mainWindow.CurrentVariant != variant)
                    border.Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
            };
            border.MouseLeave += (s, e) =>
            {
                if (_mainWindow.CurrentVariant != variant)
                    border.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                _mainWindow.CurrentVariant = variant;
                UpdateStylePanelSelections();
            };

            return border;
        }

        private void UpdateStylePanelSelections()
        {
            UpdateStyleButtonVisual(_styleBtnMin, WidgetStyleVariant.MinimalistVertical);
            UpdateStyleButtonVisual(_styleBtnGlass, WidgetStyleVariant.GlassmorphismCard);
            UpdateStyleButtonVisual(_styleBtnNeu, WidgetStyleVariant.NeumorphismDark);
            UpdateStyleButtonVisual(_styleBtnCompact, WidgetStyleVariant.HorizontalCompact);
        }

        private void UpdateStyleButtonVisual(Border btn, WidgetStyleVariant variant)
        {
            if (btn == null) return;
            bool isActive = _mainWindow.CurrentVariant == variant;
            if (isActive)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // sky blue
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            }
        }
        #endregion

        #region Settings Panel
        private void BuildSettingsPanel()
        {
            _settingsPanel = new Grid { Visibility = Visibility.Collapsed };
            _settingsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _settingsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock title = new TextBlock
            {
                Text = "AJUSTES WIDGET",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(4, 0, 0, 4)
            };
            Grid.SetRow(title, 0);
            _settingsPanel.Children.Add(title);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 24H Format Toggle
            _toggle24h = CreateSettingToggleButton("Formato 24H", () => _mainWindow.Is24HourFormat, (v) => _mainWindow.Is24HourFormat = v);
            Grid.SetRow(_toggle24h, 0);
            Grid.SetColumn(_toggle24h, 0);
            grid.Children.Add(_toggle24h);

            // Date Toggle
            _toggleDate = CreateSettingToggleButton("Mostrar Fecha", () => _mainWindow.ShowDate, (v) => _mainWindow.ShowDate = v);
            Grid.SetRow(_toggleDate, 0);
            Grid.SetColumn(_toggleDate, 1);
            grid.Children.Add(_toggleDate);

            // Lock Toggle
            _toggleLock = CreateSettingToggleButton("Bloquear", () => _mainWindow.IsLocked, (v) => _mainWindow.IsLocked = v);
            Grid.SetRow(_toggleLock, 1);
            Grid.SetColumn(_toggleLock, 0);
            grid.Children.Add(_toggleLock);

            // Close App Button (Red)
            Border btnExit = new Border
            {
                Height = 18,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)), // red-500
                BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            TextBlock tbExit = new TextBlock
            {
                Text = "Cerrar",
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnExit.Child = tbExit;
            btnExit.MouseEnter += (s, e) => btnExit.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            btnExit.MouseLeave += (s, e) => btnExit.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            btnExit.MouseLeftButtonDown += (s, e) => Application.Current.Shutdown();

            Grid.SetRow(btnExit, 1);
            Grid.SetColumn(btnExit, 1);
            grid.Children.Add(btnExit);

            Grid.SetRow(grid, 1);
            _settingsPanel.Children.Add(grid);
        }

        private Border CreateSettingToggleButton(string text, Func<bool> getter, Action<bool> setter)
        {
            Border border = new Border
            {
                Height = 18,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            TextBlock tb = new TextBlock
            {
                Text = text,
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = tb;

            border.MouseEnter += (s, e) =>
            {
                if (!getter())
                    border.Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
            };
            border.MouseLeave += (s, e) =>
            {
                UpdateToggleButtonVisual(border, getter());
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                setter(!getter());
                UpdateToggleButtonVisual(border, getter());
            };

            return border;
        }

        private void UpdateSettingsPanelSelections()
        {
            UpdateToggleButtonVisual(_toggle24h, _mainWindow.Is24HourFormat);
            UpdateToggleButtonVisual(_toggleDate, _mainWindow.ShowDate);
            UpdateToggleButtonVisual(_toggleLock, _mainWindow.IsLocked);
        }

        private void UpdateToggleButtonVisual(Border btn, bool isActive)
        {
            if (btn == null) return;
            if (isActive)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // green-500
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                btn.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            }
        }
        #endregion

        #region Wallpaper Panel
        private void BuildWallpaperPanel()
        {
            _wallpaperPanel = new Grid { Visibility = Visibility.Collapsed };
            _wallpaperPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _wallpaperPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBlock title = new TextBlock
            {
                Text = "FONDO DE ESCRITORIO",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                Margin = new Thickness(4, 0, 0, 4)
            };
            Grid.SetRow(title, 0);
            _wallpaperPanel.Children.Add(title);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // File Dialog Selector Button
            Border btnBrowse = new Border
            {
                Height = 18,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            TextBlock tbBrowse = new TextBlock
            {
                Text = "Examinar Imagen...",
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnBrowse.Child = tbBrowse;
            btnBrowse.MouseEnter += (s, e) => btnBrowse.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            btnBrowse.MouseLeave += (s, e) => btnBrowse.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            btnBrowse.MouseLeftButtonDown += (s, e) => OpenWallpaperDialog();

            Grid.SetRow(btnBrowse, 0);
            Grid.SetColumnSpan(btnBrowse, 2);
            grid.Children.Add(btnBrowse);

            // Sunset Preset
            Border btnSunset = CreatePresetWallpaperButton("Atardecer", Color.FromRgb(244, 63, 94)); // Sunset red/pink
            btnSunset.MouseLeftButtonDown += (s, e) => SetPresetWallpaper("AtardecerAura", System.Drawing.Color.FromArgb(26, 21, 44), System.Drawing.Color.FromArgb(244, 114, 182));
            Grid.SetRow(btnSunset, 1);
            Grid.SetColumn(btnSunset, 0);
            grid.Children.Add(btnSunset);

            // Midnight Preset
            Border btnMidnight = CreatePresetWallpaperButton("Medianoche", Color.FromRgb(14, 165, 233)); // Midnight blue
            btnMidnight.MouseLeftButtonDown += (s, e) => SetPresetWallpaper("AzulMedianoche", System.Drawing.Color.FromArgb(15, 23, 42), System.Drawing.Color.FromArgb(56, 189, 248));
            Grid.SetRow(btnMidnight, 1);
            Grid.SetColumn(btnMidnight, 1);
            grid.Children.Add(btnMidnight);

            Grid.SetRow(grid, 1);
            _wallpaperPanel.Children.Add(grid);
        }

        private Border CreatePresetWallpaperButton(string text, Color baseColor)
        {
            Border border = new Border
            {
                Height = 18,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            TextBlock tb = new TextBlock
            {
                Text = text,
                FontSize = 8.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = tb;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(90, baseColor.R, baseColor.G, baseColor.B));
                border.BorderBrush = new SolidColorBrush(baseColor);
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            };

            return border;
        }

        private void OpenWallpaperDialog()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, dlg.FileName, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al establecer fondo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SetPresetWallpaper(string name, System.Drawing.Color color1, System.Drawing.Color color2)
        {
            try
            {
                int width = (int)SystemParameters.PrimaryScreenWidth;
                int height = (int)SystemParameters.PrimaryScreenHeight;

                using (Bitmap bmp = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                            new System.Drawing.Rectangle(0, 0, width, height),
                            color1,
                            color2,
                            45f)) // Beautiful 45 deg gradient angle
                        {
                            g.FillRectangle(brush, 0, 0, width, height);
                        }
                    }

                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "widgUI");
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string filePath = Path.Combine(dir, name + ".png");
                    bmp.Save(filePath, ImageFormat.Png);

                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al autogenerar fondo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Subpanels Navigation and Width Animation
        private void ShowSubPanel(string panelName)
        {
            if (_activeSubPanel == panelName)
            {
                // Collapse subpanel, back to main menu (width 62)
                AnimateWindowWidth(62);
                _subPanelBorder.Visibility = Visibility.Collapsed;
                HideAllSubPanels();
                _activeSubPanel = null;
                return;
            }

            _activeSubPanel = panelName;
            HideAllSubPanels();

            if (panelName == "style")
            {
                _stylePanel.Visibility = Visibility.Visible;
                UpdateStylePanelSelections();
                _stylePanel.Opacity = 0;
                _stylePanel.BeginAnimation(Grid.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            }
            else if (panelName == "settings")
            {
                _settingsPanel.Visibility = Visibility.Visible;
                UpdateSettingsPanelSelections();
                _settingsPanel.Opacity = 0;
                _settingsPanel.BeginAnimation(Grid.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            }
            else if (panelName == "wallpaper")
            {
                _wallpaperPanel.Visibility = Visibility.Visible;
                _wallpaperPanel.Opacity = 0;
                _wallpaperPanel.BeginAnimation(Grid.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
            }

            _subPanelBorder.Visibility = Visibility.Visible;
            AnimateWindowWidth(262);
        }

        private void HideAllSubPanels()
        {
            _stylePanel.Visibility = Visibility.Collapsed;
            _settingsPanel.Visibility = Visibility.Collapsed;
            _wallpaperPanel.Visibility = Visibility.Collapsed;
        }

        private void AnimateWindowWidth(double targetWidth)
        {
            DoubleAnimation anim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(300),
                DecelerationRatio = 0.85
            };
            this.BeginAnimation(Window.WidthProperty, anim);
        }
        #endregion

        #region Mouse Hover Animation Flow
        private void EdgeMenuWindow_MouseEnter(object sender, MouseEventArgs e)
        {
            _isHovered = true;
            _hoverTimer.Stop();

            // Expands immediately to main width (62)
            AnimateWindowWidth(62);

            _collapsedIndicator.Visibility = Visibility.Collapsed;
            _layoutGrid.Visibility = Visibility.Visible;
            
            // Show App Name first
            _appNameText.Visibility = Visibility.Visible;
            _appNameText.Opacity = 0;
            _appNameText.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));

            // Hide buttons first
            _buttonsGrid.Visibility = Visibility.Collapsed;
            _buttonsGrid.Opacity = 0;

            // Wait 750ms before switching to buttons
            _hoverTimer.Start();
        }

        private void EdgeMenuWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovered = false;
            _hoverTimer.Stop();

            // Reset subpanels state
            _activeSubPanel = null;
            _subPanelBorder.Visibility = Visibility.Collapsed;
            HideAllSubPanels();

            // Animate width back to collapsed 3px
            AnimateWindowWidth(3);

            // Re-show collapsed indicator, hide layout grid
            _collapsedIndicator.Visibility = Visibility.Visible;
            _layoutGrid.Visibility = Visibility.Collapsed;
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!_isHovered) return;

            // Transition: Fade out app name, then fade in buttons
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
            fadeOut.Completed += (s, ev) =>
            {
                _appNameText.Visibility = Visibility.Collapsed;
                _buttonsGrid.Visibility = Visibility.Visible;

                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                _buttonsGrid.BeginAnimation(Grid.OpacityProperty, fadeIn);
            };
            _appNameText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
        }
        #endregion
    }
}
