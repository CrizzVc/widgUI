using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace WidgUI
{
    public class AppWidgetWindow : Window, ILayeredDesktopWidget
    {
        private const double ItemSize = 46;
        private const double IconSize = 46;
        private const double CornerRadius = 14;
        private const double DragThreshold = 5;

        private bool _isLocked;
        private bool _showWhiteBackground = true;
        private bool _embeddedInDesktop = true;
        private bool _dragStarted;
        private string _widgetId;
        private string _appPath;
        private Point _mouseDownPoint;
        private int _layerIndex;

        public int LayerIndex
        {
            get { return _layerIndex; }
            set { _layerIndex = value; }
        }

        private Border _iconTile;
        private Grid _iconGrid;

        public AppWidgetWindow() : this(null)
        {
        }

        public AppWidgetWindow(AppWidgetLayoutData layoutData)
        {
            _widgetId = layoutData != null && !string.IsNullOrEmpty(layoutData.Id)
                ? layoutData.Id
                : Guid.NewGuid().ToString();

            InitializeWindow();
            BuildUI();
            SetupContextMenu();

            if (layoutData != null)
            {
                ApplyLayoutData(layoutData);
            }
            else
            {
                ApplyTileBackground();
                _layerIndex = WidgetRegistry.AllocateLayerIndex();
            }

            this.Loaded += AppWidgetWindow_Loaded;
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - App";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.Width = ItemSize;
            this.Height = ItemSize;
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 180;
            this.Top = 420;

            this.AllowDrop = true;
            this.DragEnter += AppWidgetWindow_DragEnter;
            this.DragLeave += AppWidgetWindow_DragLeave;
            this.Drop += AppWidgetWindow_Drop;
        }

        private void AppWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }
        }

        private void BuildUI()
        {
            _iconTile = new Border
            {
                Width = ItemSize,
                Height = ItemSize,
                CornerRadius = new CornerRadius(CornerRadius),
                Cursor = Cursors.Hand
            };

            _iconGrid = new Grid();
            _iconTile.Child = _iconGrid;

            _iconTile.MouseLeftButtonDown += IconTile_MouseLeftButtonDown;
            _iconTile.MouseMove += IconTile_MouseMove;
            _iconTile.MouseLeftButtonUp += IconTile_MouseLeftButtonUp;

            this.Content = _iconTile;
            RenderIcon();
        }

        private void IconTile_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDownPoint = e.GetPosition(this);
            _dragStarted = false;
            _iconTile.CaptureMouse();
            WidgetLayerHelper.BeginHoldPreview(this);
            e.Handled = true;
        }

        private void IconTile_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isLocked || e.LeftButton != MouseButtonState.Pressed || _dragStarted)
            {
                return;
            }

            Point current = e.GetPosition(this);
            Vector delta = current - _mouseDownPoint;
            if (delta.Length >= DragThreshold)
            {
                _dragStarted = true;
                _iconTile.ReleaseMouseCapture();
                WidgetSnapHelper.BeginSnapDrag(this, e);
            }
        }

        private void IconTile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_iconTile.IsMouseCaptured)
            {
                _iconTile.ReleaseMouseCapture();
            }

            if (!_dragStarted)
            {
                WidgetLayerHelper.EndHoldPreview(this);

                if (string.IsNullOrEmpty(_appPath))
                {
                    PickApp();
                }
                else
                {
                    LaunchApp();
                }
            }

            _dragStarted = false;
            e.Handled = true;
        }

        private void LaunchApp()
        {
            if (string.IsNullOrEmpty(_appPath))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _appPath,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        private void ApplyTileBackground()
        {
            if (_iconTile == null)
            {
                return;
            }

            if (_showWhiteBackground)
            {
                _iconTile.Background = Brushes.White;
                _iconTile.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 2,
                    Opacity = 0.1,
                    BlurRadius = 5
                };
            }
            else
            {
                _iconTile.Background = Brushes.Transparent;
                _iconTile.Effect = null;
            }
        }

        private void RenderIcon()
        {
            _iconGrid.Children.Clear();
            _iconTile.MouseEnter -= IconTile_MouseEnter;
            _iconTile.MouseLeave -= IconTile_MouseLeave;

            if (string.IsNullOrEmpty(_appPath))
            {
                _iconGrid.Children.Add(new TextBlock
                {
                    Text = "+",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                _iconTile.ToolTip = "Arrastra una app o clic derecho para elegir";
                return;
            }

            string tooltip = System.IO.Path.GetFileNameWithoutExtension(_appPath);
            _iconTile.ToolTip = tooltip;

            ImageSource icon = IconHelper.GetHighQualityIcon(_appPath);
            if (icon != null)
            {
                Image img = new Image
                {
                    Source = icon,
                    Stretch = Stretch.Uniform,
                    Width = IconSize,
                    Height = IconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Clip = new RectangleGeometry(new Rect(0, 0, IconSize, IconSize), CornerRadius, CornerRadius)
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                _iconGrid.Children.Add(img);
            }
            else
            {
                _iconGrid.Children.Add(new TextBlock
                {
                    Text = "?",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Border darkenOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                CornerRadius = new CornerRadius(CornerRadius),
                IsHitTestVisible = false
            };
            _iconGrid.Children.Add(darkenOverlay);

            _iconTile.MouseEnter += IconTile_MouseEnter;
            _iconTile.MouseLeave += IconTile_MouseLeave;
        }

        private void IconTile_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_iconGrid.Children.Count == 0)
            {
                return;
            }

            Border overlay = _iconGrid.Children[_iconGrid.Children.Count - 1] as Border;
            if (overlay != null)
            {
                overlay.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            }
        }

        private void IconTile_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_iconGrid.Children.Count == 0)
            {
                return;
            }

            Border overlay = _iconGrid.Children[_iconGrid.Children.Count - 1] as Border;
            if (overlay != null)
            {
                overlay.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            }
        }

        private void SetAppPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _appPath = path;
            RenderIcon();
            NotifyLayoutChanged();
        }

        private void PickApp()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Archivos ejecutables|*.exe;*.lnk;*.bat;*.cmd|Todos los archivos|*.*",
                Title = "Seleccionar aplicación"
            };

            if (dialog.ShowDialog() == true)
            {
                SetAppPath(dialog.FileName);
            }
        }

        private void AppWidgetWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (_showWhiteBackground)
                {
                    _iconTile.Background = new SolidColorBrush(Color.FromRgb(235, 240, 250));
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void AppWidgetWindow_DragLeave(object sender, DragEventArgs e)
        {
            ApplyTileBackground();
        }

        private void AppWidgetWindow_Drop(object sender, DragEventArgs e)
        {
            ApplyTileBackground();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    SetAppPath(files[0]);
                }
            }
        }

        private void SetupContextMenu()
        {
            ContextMenu menu = new ContextMenu();

            MenuItem pickApp = new MenuItem { Header = "Elegir aplicación..." };
            pickApp.Click += (s, e) => PickApp();
            menu.Items.Add(pickApp);

            if (!string.IsNullOrEmpty(_appPath))
            {
                MenuItem openApp = new MenuItem { Header = "Abrir" };
                openApp.Click += (s, e) => LaunchApp();
                menu.Items.Add(openApp);

                MenuItem clearApp = new MenuItem { Header = "Quitar aplicación" };
                clearApp.Click += (s, e) =>
                {
                    _appPath = null;
                    RenderIcon();
                    NotifyLayoutChanged();
                };
                menu.Items.Add(clearApp);
            }

            menu.Items.Add(new Separator());

            MenuItem whiteBg = new MenuItem
            {
                Header = "Fondo blanco",
                IsCheckable = true,
                IsChecked = _showWhiteBackground
            };
            whiteBg.Click += (s, e) =>
            {
                _showWhiteBackground = whiteBg.IsChecked;
                ApplyTileBackground();
                SetupContextMenu();
                NotifyLayoutChanged();
            };
            menu.Items.Add(whiteBg);

            MenuItem lockPos = new MenuItem { Header = "Bloquear posición", IsCheckable = true, IsChecked = _isLocked };
            lockPos.Click += (s, e) =>
            {
                _isLocked = lockPos.IsChecked;
                NotifyLayoutChanged();
            };
            menu.Items.Add(lockPos);

            WidgetLayerHelper.AppendLayerMenuItems(menu, this);

            MenuItem closeItem = new MenuItem { Header = "Cerrar widget" };
            closeItem.Click += (s, e) => this.Close();
            menu.Items.Add(closeItem);

            _iconTile.ContextMenu = menu;
        }

        private void NotifyLayoutChanged()
        {
            WidgetRegistry.AutoSaveLayout();
        }

        public AppWidgetLayoutData ToLayoutData()
        {
            return new AppWidgetLayoutData
            {
                Id = _widgetId,
                Path = _appPath,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                ShowWhiteBackground = _showWhiteBackground,
                ZIndex = _layerIndex
            };
        }

        public void ApplyLayoutData(AppWidgetLayoutData data)
        {
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.Id)) _widgetId = data.Id;

            _isLocked = data.IsLocked;
            _showWhiteBackground = data.ShowWhiteBackground ?? true;
            this.Left = data.Left;
            this.Top = data.Top;
            _layerIndex = data.ZIndex;

            if (!string.IsNullOrEmpty(data.Path) && (File.Exists(data.Path) || data.Path == "explorer.exe"))
            {
                _appPath = data.Path;
            }

            ApplyTileBackground();
            RenderIcon();
            SetupContextMenu();
        }
    }
}
