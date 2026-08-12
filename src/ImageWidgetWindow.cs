using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace WidgUI
{
    public class ImageWidgetWindow : Window
    {
        private const double MinSize = 80;
        private const double MaxSize = 800;
        private const double CardPadding = 8;
        private const double DefaultMaxDimension = 280;
        private const double ImageCornerRadius = 14;
        private const int IdleHideDelayMs = 2500;
        private const int ChromeFadeMs = 350;

        private bool _isLocked;
        private bool _embeddedInDesktop = true;
        private bool _isResizing;
        private bool _isChromeVisible = true;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartHeight;

        private Border _cardBorder;
        private Border _chromeLayer;
        private Border _imageBorder;
        private System.Windows.Controls.Image _imageControl;
        private Border _resizeHandle;
        private DispatcherTimer _idleTimer;
        private string _imagePath;
        private string _widgetId;

        public ImageWidgetWindow(string imagePath)
            : this(CreateLayoutFromPath(imagePath))
        {
        }

        public ImageWidgetWindow(ImageWidgetLayoutData layoutData)
        {
            if (layoutData == null || string.IsNullOrEmpty(layoutData.ImagePath) || !File.Exists(layoutData.ImagePath))
            {
                throw new ArgumentException("Ruta de imagen invalida.", "layoutData");
            }

            _widgetId = string.IsNullOrEmpty(layoutData.Id) ? Guid.NewGuid().ToString() : layoutData.Id;
            _imagePath = layoutData.ImagePath;
            InitializeWindow();
            BuildUI();
            LoadImage(_imagePath);
            SetupContextMenu();
            this.Loaded += ImageWidgetWindow_Loaded;
            ApplyLayoutData(layoutData);
        }

        private static ImageWidgetLayoutData CreateLayoutFromPath(string imagePath)
        {
            return new ImageWidgetLayoutData { ImagePath = imagePath };
        }

        public static void CreateFromFilePicker(Action<ImageWidgetWindow> onCreated)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Imagenes|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Todos los archivos|*.*",
                Title = "Seleccionar imagen",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            foreach (string file in dialog.FileNames)
            {
                if (!IsSupportedImage(file))
                {
                    continue;
                }

                ImageWidgetWindow widget = new ImageWidgetWindow(file);
                if (onCreated != null)
                {
                    onCreated(widget);
                }
                widget.Show();
            }
        }

        private static bool IsSupportedImage(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                return false;
            }

            switch (ext.ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    return true;
                default:
                    return false;
            }
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Imagen";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - 330;
            this.Top = 420;

            this.MouseLeftButtonDown += ImageWidgetWindow_MouseLeftButtonDown;
            this.MouseLeftButtonUp += (s, e) => StartIdleTimer();
            this.MouseEnter += (s, e) => ShowChrome();
            this.MouseLeave += (s, e) => StartIdleTimer();

            _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(IdleHideDelayMs) };
            _idleTimer.Tick += (s, e) =>
            {
                _idleTimer.Stop();
                HideChrome();
            };
        }

        private void ImageWidgetWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked || _isResizing)
            {
                return;
            }

            if (e.OriginalSource == _resizeHandle || IsDescendantOf(e.OriginalSource as DependencyObject, _resizeHandle))
            {
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                ShowChrome();
                this.DragMove();
                StartIdleTimer();
            }
        }

        private static bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent)
                {
                    return true;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return false;
        }

        private void ImageWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }

            StartIdleTimer();
        }

        private void BuildUI()
        {
            _cardBorder = new Border
            {
                Background = Brushes.Transparent,
                ClipToBounds = true
            };

            Grid mainGrid = new Grid();

            _chromeLayer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 20, 20, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(ImageCornerRadius + 2),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 4,
                    Opacity = 0.25,
                    BlurRadius = 12
                }
            };

            _imageBorder = new Border
            {
                CornerRadius = new CornerRadius(ImageCornerRadius),
                ClipToBounds = true,
                Margin = new Thickness(CardPadding),
                Background = Brushes.Transparent
            };

            _imageControl = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            RenderOptions.SetBitmapScalingMode(_imageControl, BitmapScalingMode.HighQuality);
            _imageBorder.Child = _imageControl;

            _resizeHandle = new Border
            {
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                CornerRadius = new CornerRadius(6, 0, ImageCornerRadius, 0),
                Cursor = Cursors.SizeNWSE,
                ToolTip = "Arrastra para cambiar tamano"
            };

            TextBlock resizeGlyph = new TextBlock
            {
                Text = "\uE7E8",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _resizeHandle.Child = resizeGlyph;

            _resizeHandle.MouseEnter += (s, e) =>
            {
                _resizeHandle.Background = new SolidColorBrush(Color.FromArgb(200, 56, 189, 248));
            };
            _resizeHandle.MouseLeave += (s, e) =>
            {
                if (!_isResizing)
                {
                    _resizeHandle.Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
                }
            };
            _resizeHandle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
            _resizeHandle.MouseMove += ResizeHandle_MouseMove;
            _resizeHandle.MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;

            mainGrid.Children.Add(_chromeLayer);
            mainGrid.Children.Add(_imageBorder);
            mainGrid.Children.Add(_resizeHandle);
            _cardBorder.Child = mainGrid;
            this.Content = _cardBorder;
        }

        private void ShowChrome()
        {
            _idleTimer.Stop();

            if (_isChromeVisible)
            {
                return;
            }

            _isChromeVisible = true;
            AnimateChrome(1, CardPadding);
            if (!_isLocked)
            {
                _resizeHandle.Visibility = Visibility.Visible;
            }
        }

        private void HideChrome()
        {
            if (!_isChromeVisible || _isResizing || this.IsMouseOver)
            {
                return;
            }

            _isChromeVisible = false;
            AnimateChrome(0, 0);
            _resizeHandle.Visibility = Visibility.Collapsed;
        }

        private void AnimateChrome(double targetOpacity, double targetMargin)
        {
            DoubleAnimation opacityAnim = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(ChromeFadeMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _chromeLayer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

            ThicknessAnimation marginAnim = new ThicknessAnimation(
                new Thickness(targetMargin),
                TimeSpan.FromMilliseconds(ChromeFadeMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _imageBorder.BeginAnimation(Border.MarginProperty, marginAnim);

            DoubleAnimation handleAnim = new DoubleAnimation(
                (!_isLocked && targetOpacity > 0) ? 1 : 0,
                TimeSpan.FromMilliseconds(ChromeFadeMs))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _resizeHandle.BeginAnimation(UIElement.OpacityProperty, handleAnim);
        }

        private void StartIdleTimer()
        {
            _idleTimer.Stop();
            if (!_isChromeVisible || _isResizing)
            {
                return;
            }

            _idleTimer.Start();
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked)
            {
                return;
            }

            _isResizing = true;
            ShowChrome();
            _resizeStartPoint = e.GetPosition(this);
            _resizeStartWidth = this.Width;
            _resizeStartHeight = this.Height;
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

            double newWidth = ClampSize(_resizeStartWidth + deltaX);
            double newHeight = ClampSize(_resizeStartHeight + deltaY);

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                double aspect = _resizeStartWidth / _resizeStartHeight;
                if (Math.Abs(deltaX) >= Math.Abs(deltaY))
                {
                    newHeight = newWidth / aspect;
                }
                else
                {
                    newWidth = newHeight * aspect;
                }

                newWidth = ClampSize(newWidth);
                newHeight = ClampSize(newHeight);
            }

            this.Width = newWidth;
            this.Height = newHeight;
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing)
            {
                return;
            }

            _isResizing = false;
            _resizeHandle.ReleaseMouseCapture();
            _resizeHandle.Background = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            StartIdleTimer();
        }

        private static double ClampSize(double value)
        {
            return Math.Max(MinSize, Math.Min(MaxSize, value));
        }

        private void LoadImage(string imagePath)
        {
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(imagePath));
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _imageControl.Source = bitmap;
            ApplyInitialSize(bitmap.PixelWidth, bitmap.PixelHeight);
            this.Title = "widgUI - " + Path.GetFileName(imagePath);
        }

        private void ApplyInitialSize(int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                this.Width = 200;
                this.Height = 200;
                return;
            }

            double contentMax = DefaultMaxDimension - (CardPadding * 2) - 2;
            double scale = Math.Min(contentMax / pixelWidth, contentMax / pixelHeight);
            if (scale > 1)
            {
                scale = 1;
            }

            double contentWidth = pixelWidth * scale;
            double contentHeight = pixelHeight * scale;
            this.Width = Math.Max(MinSize, contentWidth + (CardPadding * 2) + 2);
            this.Height = Math.Max(MinSize, contentHeight + (CardPadding * 2) + 2);
        }

        private void ChangeImage()
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Imagenes|*.jpg;*.jpeg;*.png;*.gif;*.bmp|Todos los archivos|*.*",
                Title = "Seleccionar imagen"
            };

            if (dialog.ShowDialog() != true || !IsSupportedImage(dialog.FileName))
            {
                return;
            }

            _imagePath = dialog.FileName;
            LoadImage(_imagePath);
        }

        private void SetupContextMenu()
        {
            ContextMenu cm = new ContextMenu();

            MenuItem itemChange = new MenuItem { Header = "Cambiar imagen..." };
            itemChange.Click += (s, e) => ChangeImage();

            MenuItem itemLock = new MenuItem { Header = "Bloquear posicion" };
            itemLock.IsCheckable = true;
            itemLock.IsChecked = _isLocked;
            itemLock.Click += (s, e) =>
            {
                _isLocked = itemLock.IsChecked;
                if (_isLocked)
                {
                    _resizeHandle.Visibility = Visibility.Collapsed;
                }
                else if (_isChromeVisible)
                {
                    _resizeHandle.Visibility = Visibility.Visible;
                    _resizeHandle.Opacity = 1;
                }
            };

            MenuItem itemSize = new MenuItem { Header = "Tamano" };
            itemSize.Items.Add(CreateSizeMenuItem("Pequeno (150px)", 150));
            itemSize.Items.Add(CreateSizeMenuItem("Mediano (250px)", 250));
            itemSize.Items.Add(CreateSizeMenuItem("Grande (400px)", 400));

            MenuItem itemExit = new MenuItem { Header = "Cerrar widget" };
            itemExit.Click += (s, e) => this.Close();

            cm.Items.Add(itemChange);
            cm.Items.Add(itemLock);
            cm.Items.Add(itemSize);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
            _cardBorder.ContextMenuOpening += (s, e) => ShowChrome();
            cm.Closed += (s, e) => StartIdleTimer();
        }

        private MenuItem CreateSizeMenuItem(string label, double targetSize)
        {
            MenuItem item = new MenuItem { Header = label };
            item.Click += (s, e) => ApplyPresetSize(targetSize);
            return item;
        }

        private void ApplyPresetSize(double targetSize)
        {
            if (_imageControl.Source == null)
            {
                this.Width = targetSize;
                this.Height = targetSize;
                return;
            }

            BitmapSource source = (BitmapSource)_imageControl.Source;
            double aspect = (double)source.PixelWidth / source.PixelHeight;
            double contentSize = targetSize - (CardPadding * 2) - 2;

            double contentWidth;
            double contentHeight;
            if (aspect >= 1)
            {
                contentWidth = contentSize;
                contentHeight = contentSize / aspect;
            }
            else
            {
                contentHeight = contentSize;
                contentWidth = contentSize * aspect;
            }

            this.Width = ClampSize(contentWidth + (CardPadding * 2) + 2);
            this.Height = ClampSize(contentHeight + (CardPadding * 2) + 2);
        }

        public ImageWidgetLayoutData ToLayoutData()
        {
            return new ImageWidgetLayoutData
            {
                Id = _widgetId,
                ImagePath = _imagePath,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                Width = this.Width,
                Height = this.Height
            };
        }

        public void ApplyLayoutData(ImageWidgetLayoutData data)
        {
            if (data == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(data.Id))
            {
                _widgetId = data.Id;
            }

            if (data.Width >= MinSize && data.Height >= MinSize)
            {
                this.Left = data.Left;
                this.Top = data.Top;
                this.Width = data.Width;
                this.Height = data.Height;
            }

            _isLocked = data.IsLocked;
            _resizeHandle.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
