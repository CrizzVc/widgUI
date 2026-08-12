using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace WidgUI
{
    public class DockWidgetWindow : Window
    {
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;
        private double _iconSize = 48.0;
        private string _widgetId;
        private WidgetThemeMode _themeMode = WidgetThemeMode.Light;
        private bool _adaptToBackground = false;
        private double _opacity = WidgetAppearanceHelper.DefaultOpacity;
        private WidgetAppearanceColors _appearanceColors;
        
        private Border _cardBorder;
        private StackPanel _dockPanel;
        
        private class DockItem
        {
            public string Path { get; set; }
            public string Name { get; set; }
            public ImageSource IconSource { get; set; }
        }
        
        private readonly List<DockItem> _items = new List<DockItem>();

        public DockWidgetWindow() : this(null)
        {
        }

        public DockWidgetWindow(DockWidgetLayoutData layoutData)
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
                AddDefaultItems();
                NotifyLayoutChanged();
            }

            SetupContextMenu();
            this.Loaded += DockWidgetWindow_Loaded;
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Dock";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.SizeToContent = SizeToContent.WidthAndHeight;

            // Positioning defaults
            this.Left = (SystemParameters.PrimaryScreenWidth - 400) / 2;
            this.Top = SystemParameters.PrimaryScreenHeight - 120;

            // Drag and drop support
            this.AllowDrop = true;
            this.DragEnter += Dock_DragEnter;
            this.DragOver += Dock_DragOver;
            this.DragLeave += Dock_DragLeave;
            this.Drop += Dock_Drop;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private void DockWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }
        }

        private void BuildUI()
        {
            RefreshAppearanceColors();

            _cardBorder = new Border
            {
                Background = new SolidColorBrush(_appearanceColors.Background),
                BorderBrush = new SolidColorBrush(_appearanceColors.Border),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 8, 14, 8),
                SnapsToDevicePixels = true
            };

            _dockPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _cardBorder.Child = _dockPanel;
            this.Content = _cardBorder;
        }

        private void AddDefaultItems()
        {
            // Add some common defaults if they exist
            string[] commonApps = new string[]
            {
                "explorer.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
            };

            foreach (string app in commonApps)
            {
                if (File.Exists(app) || app == "explorer.exe")
                {
                    AddItem(app);
                }
            }

            RenderDock();
        }

        private void AddItem(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name)) name = path;

                ImageSource icon = IconHelper.GetHighQualityIcon(path, 128);

                _items.Add(new DockItem
                {
                    Path = path,
                    Name = name,
                    IconSource = icon
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error adding dock item: " + ex.Message);
            }
        }

        private void RenderDock()
        {
            _dockPanel.Children.Clear();

            _cardBorder.CornerRadius = new CornerRadius(Math.Max(12, _iconSize / 3));

            if (_items.Count == 0)
            {
                // Placeholder when empty
                TextBlock placeholder = new TextBlock
                {
                    Text = "Arrastra archivos aquí",
                    Foreground = new SolidColorBrush(_appearanceColors.SecondaryForeground),
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 6, 10, 6)
                };
                _dockPanel.Children.Add(placeholder);
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                DockItem item = _items[i];
                int itemIndex = i;

                // Container grid for hover styling
                Grid itemGrid = new Grid
                {
                    Width = _iconSize + 16,
                    Height = _iconSize + 16,
                    Background = Brushes.Transparent,
                    ToolTip = item.Name,
                    Cursor = Cursors.Hand
                };

                // ScaleTransform for mac-like hover zoom effect
                ScaleTransform scaleTransform = new ScaleTransform(1.0, 1.0);
                itemGrid.RenderTransform = scaleTransform;
                itemGrid.RenderTransformOrigin = new Point(0.5, 0.5);

                Image img = new Image
                {
                    Source = item.IconSource,
                    Width = _iconSize,
                    Height = _iconSize,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                itemGrid.Children.Add(img);

                // Hover animations
                itemGrid.MouseEnter += (s, e) =>
                {
                    DoubleAnimation zoomIn = new DoubleAnimation(1.22, new Duration(TimeSpan.FromMilliseconds(160)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, zoomIn);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, zoomIn);
                };

                itemGrid.MouseLeave += (s, e) =>
                {
                    DoubleAnimation zoomOut = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(160)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, zoomOut);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, zoomOut);
                };

                // Launch on click
                itemGrid.MouseLeftButtonUp += (s, e) =>
                {
                    LaunchItem(item.Path);
                };

                // Right click item options
                itemGrid.ContextMenu = CreateItemContextMenu(itemIndex);

                _dockPanel.Children.Add(itemGrid);

                // Add small separator between icons
                if (i < _items.Count - 1)
                {
                    Border separator = new Border
                    {
                        Width = 1,
                        Height = _iconSize * 0.5,
                        Background = new SolidColorBrush(_appearanceColors.Separator),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    };
                    _dockPanel.Children.Add(separator);
                }
            }
        }

        private void LaunchItem(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar el archivo:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ContextMenu CreateItemContextMenu(int index)
        {
            ContextMenu menu = new ContextMenu();

            MenuItem openItem = new MenuItem { Header = "Abrir" };
            openItem.Click += (s, e) => LaunchItem(_items[index].Path);
            menu.Items.Add(openItem);

            MenuItem removeItem = new MenuItem { Header = "Eliminar" };
            removeItem.Click += (s, e) =>
            {
                _items.RemoveAt(index);
                RenderDock();
                NotifyLayoutChanged();
            };
            menu.Items.Add(removeItem);

            return menu;
        }

        private void SetupContextMenu()
        {
            ContextMenu menu = new ContextMenu();

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
                RenderDock();
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
                RenderDock();
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
                RenderDock();
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
                    RenderDock();
                    SetupContextMenu();
                    NotifyLayoutChanged();
                };
                opacityMenu.Items.Add(opacityItem);
            }

            appearanceMenu.Items.Add(lightItem);
            appearanceMenu.Items.Add(darkItem);
            appearanceMenu.Items.Add(adaptItem);
            appearanceMenu.Items.Add(opacityMenu);
            menu.Items.Add(appearanceMenu);

            MenuItem addShortcut = new MenuItem { Header = "Añadir acceso directo..." };
            addShortcut.Click += (s, e) => AddShortcutDialog();
            menu.Items.Add(addShortcut);

            menu.Items.Add(new Separator());

            MenuItem lockPos = new MenuItem { Header = "Bloquear posición", IsCheckable = true, IsChecked = _isLocked };
            lockPos.Click += (s, e) =>
            {
                _isLocked = lockPos.IsChecked;
                NotifyLayoutChanged();
            };
            menu.Items.Add(lockPos);

            MenuItem iconSizeMenu = new MenuItem { Header = "Tamaño de íconos" };
            double[] sizes = { 32, 40, 48, 56, 64, 80 };
            foreach (double sz in sizes)
            {
                MenuItem sizeItem = new MenuItem { Header = sz + "px", IsCheckable = true, IsChecked = (Math.Abs(_iconSize - sz) < 1.0) };
                double targetSz = sz;
                sizeItem.Click += (s, e) =>
                {
                    _iconSize = targetSz;
                    RenderDock();
                    SetupContextMenu();
                    NotifyLayoutChanged();
                };
                iconSizeMenu.Items.Add(sizeItem);
            }
            menu.Items.Add(iconSizeMenu);

            menu.Items.Add(new Separator());

            MenuItem closeItem = new MenuItem { Header = "Cerrar Dock" };
            closeItem.Click += (s, e) => this.Close();
            menu.Items.Add(closeItem);

            _cardBorder.ContextMenu = menu;
        }

        private void AddShortcutDialog()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Archivos Ejecutables|*.exe;*.lnk;*.bat;*.cmd|Todos los archivos|*.*",
                Title = "Seleccionar archivo para añadir al Dock"
            };

            if (dialog.ShowDialog() == true)
            {
                AddItem(dialog.FileName);
                RenderDock();
                NotifyLayoutChanged();
            }
        }

        private void RefreshAppearanceColors()
        {
            _appearanceColors = WidgetAppearanceHelper.ComputeColors(
                _themeMode,
                _adaptToBackground,
                _opacity,
                WidgetRegistry.GetActiveWallpaperPath(),
                this.Left,
                this.Top,
                Math.Max(this.ActualWidth > 0 ? this.ActualWidth : 400, 120),
                Math.Max(this.ActualHeight > 0 ? this.ActualHeight : 80, 60));
        }

        private void ApplyAppearance()
        {
            RefreshAppearanceColors();

            if (_cardBorder != null)
            {
                _cardBorder.Background = new SolidColorBrush(_appearanceColors.Background);
                _cardBorder.BorderBrush = new SolidColorBrush(_appearanceColors.Border);
            }
        }

        private void NotifyLayoutChanged()
        {
            WidgetRegistry.AutoSaveLayout();
        }

        #region Drag and Drop Events
        private void Dock_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                Color highlight = _appearanceColors.AccentSurface;
                _cardBorder.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, highlight.A + 40), highlight.R, highlight.G, highlight.B));
            }
        }

        private void Dock_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void Dock_DragLeave(object sender, DragEventArgs e)
        {
            ApplyAppearance();
        }

        private void Dock_Drop(object sender, DragEventArgs e)
        {
            ApplyAppearance();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    AddItem(file);
                }
                RenderDock();
                NotifyLayoutChanged();
            }
        }
        #endregion

        #region Layout Serialization
        public DockWidgetLayoutData ToLayoutData()
        {
            return new DockWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                IconSize = _iconSize,
                ThemeMode = (int)_themeMode,
                AdaptToBackground = _adaptToBackground,
                Opacity = _opacity,
                Shortcuts = _items.Select(i => i.Path).ToList()
            };
        }

        public void ApplyLayoutData(DockWidgetLayoutData data)
        {
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.Id))
            {
                _widgetId = data.Id;
            }

            _isLocked = data.IsLocked;
            _iconSize = data.IconSize;
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

            _items.Clear();
            if (data.Shortcuts != null)
            {
                foreach (string path in data.Shortcuts)
                {
                    AddItem(path);
                }
            }

            ApplyAppearance();
            RenderDock();
            SetupContextMenu();
        }
        #endregion
    }
}
