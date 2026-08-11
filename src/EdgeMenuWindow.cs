using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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
        private Border _menuBorder;
        private Grid _menuGrid;
        
        // Expanded main menu
        private Grid _mainMenuGrid;
        private TextBlock _appNameText;
        private Grid _buttonsGrid;
        
        // Action buttons
        private Border _btnAdd;
        private Border _btnSettings;
        private Border _btnWallpaper;

        // Subpanels container
        private Grid _subPanelContainer;
        private Border _btnBack;
        private TextBlock _subPanelTitle;
        private Grid _subPanelContentGrid;
        private Grid _stylePanel;
        private Grid _settingsPanel;
        private Grid _wallpaperPanel;
        private Grid _widgetsPanel;

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
        private double _screenCenterY; // Vertical center for symmetric expansion
        private DispatcherTimer _hoverTimer;
        private DispatcherTimer _leaveTimer;

        // Wallpaper subpanel state
        private string _wallpaperFolderPath = null;
        private string _activeWallpaperPath = null;
        private StackPanel _thumbnailsStackPanel;
        private ScrollViewer _thumbnailsScrollViewer;
        private Grid _chooseFolderContainer;
        private Grid _thumbnailsContainer;
        private TextBlock _folderPathText;
        private DispatcherTimer _wallpaperDebounceTimer;
        private string _pendingWallpaperPath;

        // Arrow key navigation
        private int _selectedThumbnailIndex = -1;
        private string[] _loadedImageFiles = new string[0];

        // Smooth wallpaper transition overlay
        private Window _wallpaperOverlay;
        private System.Windows.Controls.Image _overlayImage;

        public EdgeMenuWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            
            InitializeWindow();
            BuildUI();
            
            this.Loaded += EdgeMenuWindow_Loaded;
            this.MouseEnter += EdgeMenuWindow_MouseEnter;
            this.MouseLeave += EdgeMenuWindow_MouseLeave;
            this.PreviewKeyDown += EdgeMenuWindow_KeyDown;
            this.Focusable = true;

            _hoverTimer = new DispatcherTimer();
            _hoverTimer.Interval = TimeSpan.FromMilliseconds(750);
            _hoverTimer.Tick += HoverTimer_Tick;

            _leaveTimer = new DispatcherTimer();
            _leaveTimer.Interval = TimeSpan.FromMilliseconds(200); // 200ms debounce
            _leaveTimer.Tick += LeaveTimer_Tick;
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

            this.Width = 360; // Max expanded width + shadow room
            this.Height = 560; // Max expanded height + flairs room
            this.Left = 0;
            
            // Store center for symmetric expansion
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            _screenCenterY = screenHeight / 2;
            this.Top = _screenCenterY - (this.Height / 2);
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

        private void EnableKeyboard()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE);
            this.Activate();
            this.Focus();
        }

        private void DisableKeyboard()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
            DesktopManager.EmbedInDesktop(this);
        }

        private void BuildUI()
        {
            // Root Grid
            Grid rootGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent // Let clicks pass through transparent areas
            };

            // 1. Collapsed Indicator (glowing thin 3px line)
            _collapsedIndicator = new Border
            {
                Width = 3,
                Height = 120, // Revert to initial tiny height
                Background = new SolidColorBrush(Color.FromRgb(0x17, 0x17, 0x17)), // #171717
                CornerRadius = new CornerRadius(1.5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center, // Revert to center anchor
                Visibility = Visibility.Visible
            };
            rootGrid.Children.Add(_collapsedIndicator);

            // 2. Unified Menu Border (Stretches with Window)
            _menuBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left, // Anchor left
                VerticalAlignment = VerticalAlignment.Center, // Anchor center
                Width = 3,
                Height = 120,
                ClipToBounds = false, // Allow flairs to stick out
                Background = Brushes.Black, // Pure black edge
                BorderBrush = Brushes.Transparent, // Removed blue border
                BorderThickness = new Thickness(0), // Removed blue border
                CornerRadius = new CornerRadius(0, 20, 20, 0), // Flat on the left, rounded on the right
                Margin = new Thickness(0, 0, 2, 0), // Room for right border stroke
                Visibility = Visibility.Collapsed,
                Opacity = 0,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 4,
                    Opacity = 0.4,
                    BlurRadius = 12
                }
            };

            _menuGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Top Flair for the inverse curve
            System.Windows.Shapes.Path topFlair = new System.Windows.Shapes.Path
            {
                Fill = Brushes.Black,
                Data = Geometry.Parse("M 0,0 L 0,20 L 20,20 A 20,20 0 0,1 0,0 Z"),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, -20, 0, 0)
            };
            _menuGrid.Children.Add(topFlair);

            // Bottom Flair for the inverse curve
            System.Windows.Shapes.Path bottomFlair = new System.Windows.Shapes.Path
            {
                Fill = Brushes.Black,
                Data = Geometry.Parse("M 0,0 L 20,0 A 20,20 0 0,0 0,20 Z"),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, -20)
            };
            _menuGrid.Children.Add(bottomFlair);

            _menuBorder.Child = _menuGrid;

            // 2.a. Expanded Main Menu Grid
            _mainMenuGrid = new Grid
            {
                Width = 60,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch
            };

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
            _mainMenuGrid.Children.Add(_appNameText);

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
            _btnAdd = CreateIconButton("\uE710", "Widgets", Color.FromRgb(52, 211, 153)); // Green
            _btnAdd.MouseLeftButtonDown += (s, e) => ShowSubPanel("widgets");
            Grid.SetRow(_btnAdd, 0);
            _buttonsGrid.Children.Add(_btnAdd);

            _btnSettings = CreateIconButton("\uE713", "Ajustes", Color.FromRgb(129, 140, 248)); // Blue
            _btnSettings.MouseLeftButtonDown += (s, e) => ShowSubPanel("settings");
            Grid.SetRow(_btnSettings, 1);
            _buttonsGrid.Children.Add(_btnSettings);

            // Change background icon to picture frame (\uE722)
            _btnWallpaper = CreateIconButton("\uE722", "Fondo", Color.FromRgb(244, 114, 182)); // Pink
            _btnWallpaper.MouseLeftButtonDown += (s, e) => ShowSubPanel("wallpaper");
            Grid.SetRow(_btnWallpaper, 2);
            _buttonsGrid.Children.Add(_btnWallpaper);

            _mainMenuGrid.Children.Add(_buttonsGrid);
            _menuGrid.Children.Add(_mainMenuGrid);

            // 2.b. Sub-panels Container (stretches to fill available space)
            _subPanelContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(10, 8, 10, 8),
                Visibility = Visibility.Collapsed
            };
            _subPanelContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            _subPanelContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // Header Grid
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnBack = CreateBackButton();
            Grid.SetColumn(_btnBack, 0);
            headerGrid.Children.Add(_btnBack);

            _subPanelTitle = new TextBlock
            {
                Text = "ESTILO DEL RELOJ",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_subPanelTitle, 1);
            headerGrid.Children.Add(_subPanelTitle);

            Grid.SetRow(headerGrid, 0);
            _subPanelContainer.Children.Add(headerGrid);

            // Subpanel contents grid
            _subPanelContentGrid = new Grid();
            Grid.SetRow(_subPanelContentGrid, 1);
            _subPanelContainer.Children.Add(_subPanelContentGrid);

            BuildStylePanel();
            BuildSettingsPanel();
            BuildWallpaperPanel();
            BuildWidgetsPanel();

            _subPanelContentGrid.Children.Add(_stylePanel);
            _subPanelContentGrid.Children.Add(_settingsPanel);
            _subPanelContentGrid.Children.Add(_wallpaperPanel);
            _subPanelContentGrid.Children.Add(_widgetsPanel);

            _menuGrid.Children.Add(_subPanelContainer);

            rootGrid.Children.Add(_menuBorder);
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
                border.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            };

            return border;
        }

        private Border CreateBackButton()
        {
            Border border = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Volver"
            };

            TextBlock textBlock = new TextBlock
            {
                Text = "\uE72B", // Left arrow glyph
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 9,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = textBlock;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)); // sky blue hover
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            };

            border.MouseLeftButtonDown += (s, e) => GoBackToMainMenu();

            return border;
        }

        #region Style Panel
        private void BuildStylePanel()
        {
            _stylePanel = new Grid { Visibility = Visibility.Collapsed };
            _stylePanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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
            _settingsPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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
            
            // 1. Choose Folder Container
            _chooseFolderContainer = new Grid { Visibility = Visibility.Collapsed };
            _chooseFolderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _chooseFolderContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock chooseMsg = new TextBlock
            {
                Text = "Selecciona una carpeta para ver tus fondos de pantalla:",
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                FontSize = 9.5,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(10, 10, 10, 15)
            };
            Grid.SetRow(chooseMsg, 0);
            _chooseFolderContainer.Children.Add(chooseMsg);

            Border btnSelectFolder = new Border
            {
                Height = 22,
                Width = 110,
                CornerRadius = new CornerRadius(11),
                Background = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // sky blue
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock tbSelect = new TextBlock
            {
                Text = "Elegir Carpeta",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnSelectFolder.Child = tbSelect;
            btnSelectFolder.MouseEnter += (s, e) => btnSelectFolder.Background = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            btnSelectFolder.MouseLeave += (s, e) => btnSelectFolder.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            btnSelectFolder.MouseLeftButtonDown += (s, e) => SelectWallpaperFolder();

            Grid.SetRow(btnSelectFolder, 1);
            _chooseFolderContainer.Children.Add(btnSelectFolder);
            _wallpaperPanel.Children.Add(_chooseFolderContainer);

            // 2. Thumbnails Container
            _thumbnailsContainer = new Grid { Visibility = Visibility.Collapsed };
            _thumbnailsContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _thumbnailsContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Folder path & edit button row
            Grid folderRow = new Grid { Margin = new Thickness(2, 0, 2, 6) };
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _folderPathText = new TextBlock
            {
                Text = "Carpeta: ...",
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                FontSize = 8,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_folderPathText, 0);
            folderRow.Children.Add(_folderPathText);

            Border btnChangeFolder = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Cambiar carpeta"
            };
            TextBlock changeIcon = new TextBlock
            {
                Text = "\uE838", // Folder glyph in Segoe MDL2 Assets
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 9,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnChangeFolder.Child = changeIcon;
            btnChangeFolder.MouseEnter += (s, e) =>
            {
                btnChangeFolder.Background = new SolidColorBrush(Color.FromArgb(80, 244, 114, 182)); // pinkish hover
                btnChangeFolder.BorderBrush = new SolidColorBrush(Color.FromRgb(244, 114, 182));
            };
            btnChangeFolder.MouseLeave += (s, e) =>
            {
                btnChangeFolder.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
                btnChangeFolder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            };
            btnChangeFolder.MouseLeftButtonDown += (s, e) => SelectWallpaperFolder();
            Grid.SetColumn(btnChangeFolder, 1);
            folderRow.Children.Add(btnChangeFolder);

            Grid.SetRow(folderRow, 0);
            _thumbnailsContainer.Children.Add(folderRow);

            // Vertical scroll viewer for thumbnails
            _thumbnailsScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 2, 0, 2)
            };

            Style scrollStyle = GetCustomScrollBarStyle();
            if (scrollStyle != null)
            {
                _thumbnailsScrollViewer.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), scrollStyle);
            }

            _thumbnailsStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _thumbnailsScrollViewer.Content = _thumbnailsStackPanel;

            Grid.SetRow(_thumbnailsScrollViewer, 1);
            _thumbnailsContainer.Children.Add(_thumbnailsScrollViewer);

            _wallpaperPanel.Children.Add(_thumbnailsContainer);
        }

        private void SelectWallpaperFolder()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Selecciona la carpeta que contiene tus fondos de pantalla";
                dialog.ShowNewFolderButton = false;
                
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string path = dialog.SelectedPath;
                    if (Directory.Exists(path))
                    {
                        _wallpaperFolderPath = path;
                        SaveWallpaperFolder(path);
                        LoadWallpaperPanelContent();
                    }
                }
            }
        }

        private string GetSavedWallpaperFolder()
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "widgUI");
                string configFile = Path.Combine(configDir, "wallpaper_folder.txt");
                if (File.Exists(configFile))
                {
                    string path = File.ReadAllText(configFile).Trim();
                    if (Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch {}
            return null;
        }

        private void SaveWallpaperFolder(string path)
        {
            try
            {
                string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "widgUI");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                string configFile = Path.Combine(configDir, "wallpaper_folder.txt");
                File.WriteAllText(configFile, path);
            }
            catch {}
        }

        private void LoadWallpaperPanelContent()
        {
            if (string.IsNullOrEmpty(_wallpaperFolderPath))
            {
                _wallpaperFolderPath = GetSavedWallpaperFolder();
            }

            if (string.IsNullOrEmpty(_wallpaperFolderPath) || !Directory.Exists(_wallpaperFolderPath))
            {
                _chooseFolderContainer.Visibility = Visibility.Visible;
                _thumbnailsContainer.Visibility = Visibility.Collapsed;
                return;
            }

            _chooseFolderContainer.Visibility = Visibility.Collapsed;
            _thumbnailsContainer.Visibility = Visibility.Visible;

            _folderPathText.Text = "Carpeta: " + Path.GetFileName(_wallpaperFolderPath);
            _folderPathText.ToolTip = _wallpaperFolderPath;

            // Clear previous thumbnails
            _thumbnailsStackPanel.Children.Clear();

            // Load images from folder
            try
            {
                string[] files = Directory.GetFiles(_wallpaperFolderPath, "*.*")
                    .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (files.Length == 0)
                {
                    _loadedImageFiles = new string[0];
                    _selectedThumbnailIndex = -1;
                    TextBlock noImagesText = new TextBlock
                    {
                        Text = "Sin imágenes en la carpeta.",
                        Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                        FontSize = 9,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 20, 10, 20)
                    };
                    _thumbnailsStackPanel.Children.Add(noImagesText);
                    return;
                }

                _loadedImageFiles = files;
                _selectedThumbnailIndex = -1;

                foreach (string file in files)
                {
                    Border thumbBorder = CreateThumbnail(file);
                    _thumbnailsStackPanel.Children.Add(thumbBorder);
                }
            }
            catch (Exception ex)
            {
                TextBlock errText = new TextBlock
                {
                    Text = "Error: " + ex.Message,
                    Foreground = Brushes.Red,
                    FontSize = 8,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5)
                };
                _thumbnailsStackPanel.Children.Add(errText);
            }
        }

        private Border CreateThumbnail(string filePath)
        {
            Border border = new Border
            {
                Height = 90,
                Margin = new Thickness(2, 3, 2, 3),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                Cursor = Cursors.Hand,
                ToolTip = Path.GetFileName(filePath),
                ClipToBounds = true
            };

            // Image control to display thumbnail
            System.Windows.Controls.Image img = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill
            };

            // Load BitmapImage with DecodePixelWidth for efficiency
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(filePath);
                bmp.DecodePixelWidth = 280;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.DelayCreation;
                bmp.EndInit();
                img.Source = bmp;
            }
            catch
            {
                // If loading fails, show nothing
            }

            Grid grid = new Grid();
            grid.Children.Add(img);
            border.Child = grid;

            border.MouseEnter += (s, e) =>
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // sky blue glow
            };

            border.MouseLeave += (s, e) =>
            {
                if (_activeWallpaperPath != filePath)
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                }
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                _activeWallpaperPath = filePath;
                // Set permanently
                SetWallpaper(filePath);
                
                // Synchronize the selected index to allow arrow navigation from this point
                _selectedThumbnailIndex = _thumbnailsStackPanel.Children.IndexOf(border);
                
                // Refresh highlights manually
                SelectThumbnailByIndex(_selectedThumbnailIndex);
            };

            return border;
        }

        private void HoverWallpaper(string filePath)
        {
            _pendingWallpaperPath = filePath;
            if (_wallpaperDebounceTimer == null)
            {
                _wallpaperDebounceTimer = new DispatcherTimer();
                _wallpaperDebounceTimer.Interval = TimeSpan.FromMilliseconds(200); // 200ms debounce
                _wallpaperDebounceTimer.Tick += (s, e) =>
                {
                    _wallpaperDebounceTimer.Stop();
                    if (!string.IsNullOrEmpty(_pendingWallpaperPath))
                    {
                        SetWallpaperSmooth(_pendingWallpaperPath);
                    }
                };
            }
            _wallpaperDebounceTimer.Stop();
            _wallpaperDebounceTimer.Start();
        }

        private void SetWallpaper(string filePath)
        {
            try
            {
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error setting wallpaper: " + ex.Message);
            }
        }

        private void EnsureOverlayWindow()
        {
            if (_wallpaperOverlay != null) return;

            _wallpaperOverlay = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = false,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                Left = 0,
                Top = 0,
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                Opacity = 0
            };

            _overlayImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _wallpaperOverlay.Content = _overlayImage;

            _wallpaperOverlay.Loaded += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(_wallpaperOverlay).Handle;
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                DesktopManager.EmbedInDesktop(_wallpaperOverlay);
            };

            _wallpaperOverlay.Show();
        }

        private void SetWallpaperSmooth(string filePath)
        {
            try
            {
                EnsureOverlayWindow();

                // Load the new wallpaper image for the overlay
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(filePath);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                _overlayImage.Source = bmp;

                // Fade in the overlay
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))
                {
                    DecelerationRatio = 0.7
                };
                fadeIn.Completed += (s, e) =>
                {
                    // Once fully visible, apply actual wallpaper behind
                    SetWallpaper(filePath);

                    // Then fade out overlay
                    DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(100)
                    };
                    _wallpaperOverlay.BeginAnimation(Window.OpacityProperty, fadeOut);
                };
                _wallpaperOverlay.BeginAnimation(Window.OpacityProperty, fadeIn);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Smooth wallpaper error: " + ex.Message);
                // Fallback to direct set
                SetWallpaper(filePath);
            }
        }

        private void SelectThumbnailByIndex(int index)
        {
            if (_loadedImageFiles == null || _loadedImageFiles.Length == 0) return;
            if (index < 0) index = 0;
            if (index >= _loadedImageFiles.Length) index = _loadedImageFiles.Length - 1;

            _selectedThumbnailIndex = index;

            // Reset all borders
            for (int i = 0; i < _thumbnailsStackPanel.Children.Count; i++)
            {
                Border b = _thumbnailsStackPanel.Children[i] as Border;
                if (b != null)
                {
                    if (i == _selectedThumbnailIndex)
                    {
                        b.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // sky blue highlight
                    }
                    else if (_activeWallpaperPath != null && b.ToolTip != null && b.ToolTip.ToString() == Path.GetFileName(_activeWallpaperPath))
                    {
                        b.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // green for active
                    }
                    else
                    {
                        b.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    }
                }
            }

            // Scroll selected into view securely and center it
            Border selected = _thumbnailsStackPanel.Children[_selectedThumbnailIndex] as Border;
            if (selected != null)
            {
                // Each thumbnail has Height=90, Margin=3 top + 3 bottom = 96
                double itemHeight = 96.0;
                double center = (_selectedThumbnailIndex * itemHeight) + (itemHeight / 2.0);
                double targetOffset = center - (_thumbnailsScrollViewer.ViewportHeight / 2.0);
                
                if (targetOffset < 0) targetOffset = 0;
                
                _thumbnailsScrollViewer.ScrollToVerticalOffset(targetOffset);
            }

            // Preview the wallpaper
            HoverWallpaper(_loadedImageFiles[_selectedThumbnailIndex]);
        }

        private void EdgeMenuWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (_activeSubPanel != "wallpaper" || _loadedImageFiles == null || _loadedImageFiles.Length == 0)
                return;

            if (e.Key == Key.Down || e.Key == Key.Right)
            {
                SelectThumbnailByIndex(_selectedThumbnailIndex + 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Up || e.Key == Key.Left)
            {
                SelectThumbnailByIndex(_selectedThumbnailIndex - 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                // Confirm selection
                if (_selectedThumbnailIndex >= 0 && _selectedThumbnailIndex < _loadedImageFiles.Length)
                {
                    string filePath = _loadedImageFiles[_selectedThumbnailIndex];
                    _activeWallpaperPath = filePath;
                    SetWallpaperSmooth(filePath);
                    SelectThumbnailByIndex(_selectedThumbnailIndex); // Refresh highlights
                }
                e.Handled = true;
            }
        }
        #endregion

        #region Widgets Panel
        private Style GetCustomScrollBarStyle()
        {
            string scrollBarStyleXaml = @"
            <Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" 
                   xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" 
                   TargetType=""ScrollBar"">
                <Setter Property=""Width"" Value=""5""/>
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""ScrollBar"">
                            <Border Background=""Transparent"">
                                <Track x:Name=""PART_Track"" IsDirectionReversed=""true"">
                                    <Track.Thumb>
                                        <Thumb>
                                            <Thumb.Template>
                                                <ControlTemplate TargetType=""Thumb"">
                                                    <Border Background=""#606060"" CornerRadius=""2.5""/>
                                                </ControlTemplate>
                                            </Thumb.Template>
                                        </Thumb>
                                    </Track.Thumb>
                                </Track>
                            </Border>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";
            try
            {
                return (Style)System.Windows.Markup.XamlReader.Parse(scrollBarStyleXaml);
            }
            catch { return null; }
        }

        private void BuildWidgetsPanel()
        {
            _widgetsPanel = new Grid { Visibility = Visibility.Collapsed };

            ScrollViewer scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 2, 0, 2)
            };

            Style scrollStyle = GetCustomScrollBarStyle();
            if (scrollStyle != null)
            {
                scrollViewer.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), scrollStyle);
            }

            System.Windows.Controls.Primitives.UniformGrid grid = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 2,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Add Clock Widget
            Border clockWidget = CreateWidgetItem("Reloj", "\uE823", true, () => 
            {
                if (_mainWindow.IsVisible) _mainWindow.Hide(); else _mainWindow.Show();
                return _mainWindow.IsVisible;
            });
            grid.Children.Add(clockWidget);

            // Mock widgets
            grid.Children.Add(CreateWidgetItem("Carpetas", "\uE838", false, null));
            grid.Children.Add(CreateWidgetItem("Clima", "\uE706", false, null));
            grid.Children.Add(CreateWidgetItem("Sistema", "\uE90F", false, null));
            grid.Children.Add(CreateWidgetItem("Música", "\uE8D6", false, null));
            grid.Children.Add(CreateWidgetItem("Notas", "\uE70B", false, null));
            grid.Children.Add(CreateWidgetItem("Fotos", "\uE8B9", false, null));
            grid.Children.Add(CreateWidgetItem("Juegos", "\uE7FC", false, null));
            grid.Children.Add(CreateWidgetItem("Calendario", "\uE787", false, null));

            scrollViewer.Content = grid;
            _widgetsPanel.Children.Add(scrollViewer);
        }

        private Border CreateWidgetItem(string name, string iconGlyph, bool isActiveInitially, Func<bool> toggleAction)
        {
            bool isActive = isActiveInitially;

            Border border = new Border
            {
                Height = 85,
                Margin = new Thickness(4),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(isActive ? Color.FromRgb(56, 189, 248) : Color.FromArgb(60, 255, 255, 255)),
                BorderThickness = new Thickness(2),
                Cursor = Cursors.Hand
            };

            Grid grid = new Grid();

            StackPanel contentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock icon = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 24,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            TextBlock text = new TextBlock
            {
                Text = name,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            contentStack.Children.Add(icon);
            contentStack.Children.Add(text);
            grid.Children.Add(contentStack);

            Border editBtn = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4),
                Visibility = isActive && toggleAction != null ? Visibility.Visible : Visibility.Collapsed,
                Cursor = Cursors.Hand
            };
            TextBlock editIcon = new TextBlock
            {
                Text = "\uE70F", // Edit pencil glyph
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            editBtn.Child = editIcon;

            editBtn.MouseEnter += (s, e) => editBtn.Background = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            editBtn.MouseLeave += (s, e) => editBtn.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            editBtn.MouseLeftButtonDown += (s, e) => 
            {
                e.Handled = true; 
                ShowSubPanel("style");
            };

            grid.Children.Add(editBtn);
            border.Child = grid;

            border.MouseEnter += (s, e) =>
            {
                if (!isActive) border.BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            };
            border.MouseLeave += (s, e) =>
            {
                if (!isActive) border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (toggleAction != null)
                {
                    isActive = toggleAction();
                    border.BorderBrush = new SolidColorBrush(isActive ? Color.FromRgb(56, 189, 248) : Color.FromArgb(60, 255, 255, 255));
                    editBtn.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            return border;
        }
        #endregion

        #region Subpanels Navigation and Width Animation
        private void ShowSubPanel(string panelName)
        {
            if (_activeSubPanel == panelName)
            {
                GoBackToMainMenu();
                return;
            }

            _activeSubPanel = panelName;

            // 1. Hide main menu grid with animation
            DoubleAnimation fadeOutMain = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOutMain.Completed += (s, e) =>
            {
                _mainMenuGrid.Visibility = Visibility.Collapsed;

                // 2. Setup the subpanel to be shown
                _stylePanel.Visibility = Visibility.Collapsed;
                _settingsPanel.Visibility = Visibility.Collapsed;
                _wallpaperPanel.Visibility = Visibility.Collapsed;
                if (_widgetsPanel != null) _widgetsPanel.Visibility = Visibility.Collapsed;

                if (panelName == "style")
                {
                    _subPanelTitle.Text = "ESTILO DEL RELOJ";
                    _stylePanel.Visibility = Visibility.Visible;
                    UpdateStylePanelSelections();
                    AnimateMenuSize(220, 200);
                }
                else if (panelName == "settings")
                {
                    _subPanelTitle.Text = "AJUSTES WIDGET";
                    _settingsPanel.Visibility = Visibility.Visible;
                    UpdateSettingsPanelSelections();
                    AnimateMenuSize(220, 200);
                }
                else if (panelName == "wallpaper")
                {
                    _subPanelTitle.Text = "FONDO DE ESCRITORIO";
                    _wallpaperPanel.Visibility = Visibility.Visible;
                    LoadWallpaperPanelContent();
                    AnimateMenuSize(320, 480);
                    
                    // Ensure the window and scroll viewer have focus to receive keyboard events
                    this.Focus();
                    if (_thumbnailsScrollViewer != null)
                    {
                        _thumbnailsScrollViewer.Focus();
                    }
                }
                else if (panelName == "widgets")
                {
                    _subPanelTitle.Text = "WIDGETS";
                    _widgetsPanel.Visibility = Visibility.Visible;
                    AnimateMenuSize(260, 360);
                }

                // 3. Make subpanel container visible but transparent
                _subPanelContainer.Visibility = Visibility.Visible;
                _subPanelContainer.Opacity = 0;
                
                EnableKeyboard();

                // 4. Fade in the subpanel content
                DoubleAnimation fadeInSub = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                {
                    BeginTime = TimeSpan.FromMilliseconds(100) // slight delay for smooth width expansion
                };
                _subPanelContainer.BeginAnimation(Grid.OpacityProperty, fadeInSub);
            };
            _mainMenuGrid.BeginAnimation(Grid.OpacityProperty, fadeOutMain);
        }

        private void GoBackToMainMenu()
        {
            _activeSubPanel = null;
            DisableKeyboard();

            // 1. Fade out subpanel container
            DoubleAnimation fadeOutSub = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOutSub.Completed += (s, e) =>
            {
                _subPanelContainer.Visibility = Visibility.Collapsed;

                // 2. Make main menu visible but transparent
                _mainMenuGrid.Visibility = Visibility.Visible;
                _mainMenuGrid.Opacity = 0;

                // 3. Animate back to main menu size
                AnimateMenuSize(62, 200);

                // 4. Fade in main menu
                DoubleAnimation fadeInMain = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                {
                    BeginTime = TimeSpan.FromMilliseconds(100)
                };
                _mainMenuGrid.BeginAnimation(Grid.OpacityProperty, fadeInMain);
            };
            _subPanelContainer.BeginAnimation(Grid.OpacityProperty, fadeOutSub);
        }

        private void AnimateMenuSize(double targetWidth, double targetHeight)
        {
            TimeSpan dur = TimeSpan.FromMilliseconds(300);
            double decel = 0.85;

            DoubleAnimation widthAnim = new DoubleAnimation
            {
                To = targetWidth,
                Duration = dur,
                DecelerationRatio = decel
            };
            _menuBorder.BeginAnimation(Border.WidthProperty, widthAnim);

            DoubleAnimation heightAnim = new DoubleAnimation
            {
                To = targetHeight,
                Duration = dur,
                DecelerationRatio = decel
            };
            _menuBorder.BeginAnimation(Border.HeightProperty, heightAnim);
        }
        #endregion

        #region Mouse Hover Animation Flow
        private void EdgeMenuWindow_MouseEnter(object sender, MouseEventArgs e)
        {
            _leaveTimer.Stop(); // Cancel any pending close
            if (_isHovered) return;

            _isHovered = true;
            _hoverTimer.Stop();

            // Expands immediately to main size (62x200)
            AnimateMenuSize(62, 200);

            _collapsedIndicator.Visibility = Visibility.Collapsed;
            _menuBorder.Visibility = Visibility.Visible;
            _menuBorder.Opacity = 1;
            _menuBorder.BeginAnimation(Border.OpacityProperty, null);
            
            // Show App Name first
            _mainMenuGrid.Visibility = Visibility.Visible;
            _mainMenuGrid.Opacity = 1;
            
            _appNameText.Visibility = Visibility.Visible;
            _appNameText.Opacity = 0;
            _appNameText.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));

            // Hide buttons first
            _buttonsGrid.BeginAnimation(Grid.OpacityProperty, null); // Clear any pending fade in
            _buttonsGrid.Visibility = Visibility.Collapsed;
            _buttonsGrid.Opacity = 0;

            // Hide subpanels if they were left open (safeguard)
            _subPanelContainer.Visibility = Visibility.Collapsed;
            _activeSubPanel = null;

            // Wait 750ms before switching to buttons
            _hoverTimer.Start();
        }

        private void EdgeMenuWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            // Debounce to prevent accidental closing during WPF window bounds animations
            _leaveTimer.Start();
        }

        private void LeaveTimer_Tick(object sender, EventArgs e)
        {
            _leaveTimer.Stop();
            if (this.IsMouseOver) return; // False alarm, mouse is still over the window

            _isHovered = false;
            _hoverTimer.Stop();

            DisableKeyboard();

            // Reset subpanels state
            _activeSubPanel = null;
            _subPanelContainer.Visibility = Visibility.Collapsed;
            _mainMenuGrid.Visibility = Visibility.Collapsed;

            // Fade out the menu border smoothly, then collapse
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, ev) =>
            {
                _menuBorder.Visibility = Visibility.Collapsed;
                _collapsedIndicator.Visibility = Visibility.Visible;
                _collapsedIndicator.Opacity = 1;
                _collapsedIndicator.BeginAnimation(Border.OpacityProperty, null);
            };
            _menuBorder.BeginAnimation(Border.OpacityProperty, fadeOut);

            // Animate back to collapsed size
            AnimateMenuSize(3, 120);
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            if (!_isHovered) return;

            // Transition: Fade out app name, then fade in buttons
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
            _appNameText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);

            _buttonsGrid.Visibility = Visibility.Visible;
            DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                BeginTime = TimeSpan.FromMilliseconds(180)
            };
            _buttonsGrid.BeginAnimation(Grid.OpacityProperty, fadeIn);
        }
        #endregion
    }
}
