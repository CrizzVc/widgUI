using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
                // Add some default items so it's not empty and looks cool initially
                AddDefaultItems();
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
            // Glassmorphic background
            _cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 240, 245, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(14, 8, 14, 8),
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 6,
                    Opacity = 0.25,
                    BlurRadius = 18
                }
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

            // Capsule shape corner radius based on icon size
            _cardBorder.CornerRadius = new CornerRadius((_iconSize + 28) / 2);

            if (_items.Count == 0)
            {
                // Placeholder when empty
                TextBlock placeholder = new TextBlock
                {
                    Text = "Arrastra archivos aquí",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 50, 50, 80)),
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
                        Background = new SolidColorBrush(Color.FromArgb(50, 100, 100, 150)),
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
            };
            menu.Items.Add(removeItem);

            return menu;
        }

        private void SetupContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem addShortcut = new MenuItem { Header = "Añadir acceso directo..." };
            addShortcut.Click += (s, e) => AddShortcutDialog();
            menu.Items.Add(addShortcut);

            menu.Items.Add(new Separator());

            MenuItem lockPos = new MenuItem { Header = "Bloquear posición", IsCheckable = true, IsChecked = _isLocked };
            lockPos.Click += (s, e) =>
            {
                _isLocked = lockPos.IsChecked;
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
                    SetupContextMenu(); // refresh checkmarks
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
            }
        }

        #region Drag and Drop Events
        private void Dock_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                _cardBorder.Background = new SolidColorBrush(Color.FromArgb(200, 220, 235, 255));
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
            _cardBorder.Background = new SolidColorBrush(Color.FromArgb(150, 240, 245, 255));
        }

        private void Dock_Drop(object sender, DragEventArgs e)
        {
            _cardBorder.Background = new SolidColorBrush(Color.FromArgb(150, 240, 245, 255));

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    AddItem(file);
                }
                RenderDock();
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

            _items.Clear();
            if (data.Shortcuts != null)
            {
                foreach (string path in data.Shortcuts)
                {
                    AddItem(path);
                }
            }

            RenderDock();
        }
        #endregion
    }
}
