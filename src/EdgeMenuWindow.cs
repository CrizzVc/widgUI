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
                HorizontalAlignment = HorizontalAlignment.Stretch,
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

            // 2. Unified Menu Border (Stretches with Window width)
            _menuBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Height = 200,
                Background = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), // Slate-900 transparent glass
                BorderBrush = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), // Celeste glow
                BorderThickness = new Thickness(0, 1.5, 1.5, 1.5), // No border on left side
                CornerRadius = new CornerRadius(0, 20, 20, 0),
                Margin = new Thickness(0, 0, 2, 0), // Room for right border stroke
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

            _menuGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
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
            _btnAdd = CreateIconButton("\uE710", "Diseño", Color.FromRgb(52, 211, 153)); // Green
            _btnAdd.MouseLeftButtonDown += (s, e) => ShowSubPanel("style");
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

            // 2.b. Sub-panels Container (Width 198 fits perfectly with 10px margins inside 218px border)
            _subPanelContainer = new Grid
            {
                Width = 198,
                HorizontalAlignment = HorizontalAlignment.Left,
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

            _subPanelContentGrid.Children.Add(_stylePanel);
            _subPanelContentGrid.Children.Add(_settingsPanel);
            _subPanelContentGrid.Children.Add(_wallpaperPanel);

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
            _thumbnailsContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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

            // Horizontal scroll viewer for thumbnails
            _thumbnailsScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Height = 55,
                Margin = new Thickness(0, 2, 0, 2)
            };
            // Enable mouse wheel horizontal scrolling
            _thumbnailsScrollViewer.PreviewMouseWheel += (s, e) =>
            {
                _thumbnailsScrollViewer.ScrollToHorizontalOffset(_thumbnailsScrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            };

            _thumbnailsStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
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
                Width = 80,
                Height = 45,
                Margin = new Thickness(4, 2, 4, 2),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1.5),
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
                bmp.DecodePixelWidth = 80;
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
                // Change wallpaper dynamically on hover
                HoverWallpaper(filePath);
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
                
                // Highlight only this active one
                foreach (object child in _thumbnailsStackPanel.Children)
                {
                    Border b = child as Border;
                    if (b != null)
                    {
                        if (b.ToolTip != null && b.ToolTip.ToString() == Path.GetFileName(_activeWallpaperPath))
                        {
                            b.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green for selected
                        }
                        else
                        {
                            b.BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                        }
                    }
                }
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            };

            return border;
        }

        private void HoverWallpaper(string filePath)
        {
            _pendingWallpaperPath = filePath;
            if (_wallpaperDebounceTimer == null)
            {
                _wallpaperDebounceTimer = new DispatcherTimer();
                _wallpaperDebounceTimer.Interval = TimeSpan.FromMilliseconds(150); // 150ms debounce
                _wallpaperDebounceTimer.Tick += (s, e) =>
                {
                    _wallpaperDebounceTimer.Stop();
                    if (!string.IsNullOrEmpty(_pendingWallpaperPath))
                    {
                        SetWallpaper(_pendingWallpaperPath);
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

                if (panelName == "style")
                {
                    _subPanelTitle.Text = "ESTILO DEL RELOJ";
                    _stylePanel.Visibility = Visibility.Visible;
                    UpdateStylePanelSelections();
                }
                else if (panelName == "settings")
                {
                    _subPanelTitle.Text = "AJUSTES WIDGET";
                    _settingsPanel.Visibility = Visibility.Visible;
                    UpdateSettingsPanelSelections();
                }
                else if (panelName == "wallpaper")
                {
                    _subPanelTitle.Text = "FONDO DE ESCRITORIO";
                    _wallpaperPanel.Visibility = Visibility.Visible;
                    LoadWallpaperPanelContent();
                }

                // 3. Make subpanel container visible but transparent
                _subPanelContainer.Visibility = Visibility.Visible;
                _subPanelContainer.Opacity = 0;

                // 4. Animate window width to 220
                AnimateWindowWidth(220);

                // 5. Fade in the subpanel content
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

            // 1. Fade out subpanel container
            DoubleAnimation fadeOutSub = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOutSub.Completed += (s, e) =>
            {
                _subPanelContainer.Visibility = Visibility.Collapsed;

                // 2. Make main menu visible but transparent
                _mainMenuGrid.Visibility = Visibility.Visible;
                _mainMenuGrid.Opacity = 0;

                // 3. Animate window width back to 62
                AnimateWindowWidth(62);

                // 4. Fade in main menu
                DoubleAnimation fadeInMain = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                {
                    BeginTime = TimeSpan.FromMilliseconds(100)
                };
                _mainMenuGrid.BeginAnimation(Grid.OpacityProperty, fadeInMain);
            };
            _subPanelContainer.BeginAnimation(Grid.OpacityProperty, fadeOutSub);
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
            _menuBorder.Visibility = Visibility.Visible;
            
            // Show App Name first
            _mainMenuGrid.Visibility = Visibility.Visible;
            _mainMenuGrid.Opacity = 1;
            
            _appNameText.Visibility = Visibility.Visible;
            _appNameText.Opacity = 0;
            _appNameText.BeginAnimation(TextBlock.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));

            // Hide buttons first
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
            _isHovered = false;
            _hoverTimer.Stop();

            // Reset subpanels state
            _activeSubPanel = null;
            _subPanelContainer.Visibility = Visibility.Collapsed;
            _mainMenuGrid.Visibility = Visibility.Collapsed;

            // Animate width back to collapsed 3px
            AnimateWindowWidth(3);

            // Re-show collapsed indicator, hide border
            _collapsedIndicator.Visibility = Visibility.Visible;
            _menuBorder.Visibility = Visibility.Collapsed;
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
