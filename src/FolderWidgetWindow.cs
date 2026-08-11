using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Drawing; // For Icon
using Point = System.Windows.Point; // to avoid ambiguity
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace WidgUI
{
    public class FolderWidgetWindow : Window
    {
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;
        private Border _cardBorder;
        private UniformGrid _iconsGrid;
        
        private class ShortcutData 
        {
            public string Path { get; set; }
            public string Tooltip { get; set; }
            public ImageSource IconSource { get; set; }
        }
        private System.Collections.Generic.List<ShortcutData> _shortcuts = new System.Collections.Generic.List<ShortcutData>();

        public FolderWidgetWindow()
        {
            InitializeWindow();
            BuildUI();
            SetupContextMenu();
            this.Loaded += FolderWidgetWindow_Loaded;
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Carpeta";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;

            this.Width = 140;
            this.Height = 140;
            
            // Allow drag and drop
            this.AllowDrop = true;
            this.DragEnter += FolderWidgetWindow_DragEnter;
            this.Drop += FolderWidgetWindow_Drop;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - this.Width - 50;
            this.Top = 320;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private void FolderWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }
        }

        private void FolderWidgetWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
        }

        private void FolderWidgetWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    AddShortcutData(file);
                }
                RenderShortcuts();
            }
        }

        private void BuildUI()
        {
            _cardBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(140, 240, 245, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(30),
                Padding = new Thickness(12),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 5,
                    Opacity = 0.2,
                    BlurRadius = 15
                }
            };

            _iconsGrid = new UniformGrid
            {
                Columns = 2,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            _cardBorder.Child = _iconsGrid;
            this.Content = _cardBorder;
        }

        private void AddShortcutData(string filePath)
        {
            try
            {
                string tooltip = System.IO.Path.GetFileNameWithoutExtension(filePath);
                
                // Extract icon
                Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                ImageSource imageSource = null;
                if (icon != null)
                {
                    imageSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }

                _shortcuts.Add(new ShortcutData { Path = filePath, Tooltip = tooltip, IconSource = imageSource });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error adding shortcut: " + ex.Message);
            }
        }

        private void RenderShortcuts()
        {
            _iconsGrid.Children.Clear();
            
            int count = _shortcuts.Count;
            for (int i = 0; i < count; i++)
            {
                if (i < 3)
                {
                    _iconsGrid.Children.Add(CreateAppShortcut(_shortcuts[i].IconSource, _shortcuts[i].Tooltip, _shortcuts[i].Path));
                }
                else if (i == 3)
                {
                    if (count == 4)
                    {
                        _iconsGrid.Children.Add(CreateAppShortcut(_shortcuts[i].IconSource, _shortcuts[i].Tooltip, _shortcuts[i].Path));
                    }
                    else
                    {
                        int extra = count - 3;
                        _iconsGrid.Children.Add(CreateMoreIndicator(extra, _shortcuts[i].IconSource));
                        break; 
                    }
                }
            }
        }

        private UIElement CreateMoreIndicator(int extraCount, ImageSource lastIconSource)
        {
            Border appBorder = new Border
            {
                Width = 46,
                Height = 46,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(4),
                Cursor = Cursors.Arrow,
                ToolTip = string.Format("+{0} elementos adicionales", extraCount),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 2,
                    Opacity = 0.1,
                    BlurRadius = 5
                }
            };

            Grid grid = new Grid();

            if (lastIconSource != null)
            {
                System.Windows.Controls.Image img = new System.Windows.Controls.Image
                {
                    Source = lastIconSource,
                    Stretch = Stretch.Uniform,
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.4
                };
                grid.Children.Add(img);
            }

            Border overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                CornerRadius = new CornerRadius(14)
            };
            grid.Children.Add(overlay);

            TextBlock tb = new TextBlock
            {
                Text = string.Format("+{0}", extraCount),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(tb);

            appBorder.Child = grid;
            return appBorder;
        }

        private UIElement CreateAppShortcut(ImageSource imageSource, string tooltip, string path)
        {
            Border appBorder = new Border
            {
                Width = 46,
                Height = 46,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 2,
                    Opacity = 0.1,
                    BlurRadius = 5
                }
            };

            if (imageSource != null)
            {
                System.Windows.Controls.Image img = new System.Windows.Controls.Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform,
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                appBorder.Child = img;
            }
            else
            {
                TextBlock tb = new TextBlock
                {
                    Text = "?",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                appBorder.Child = tb;
            }

            appBorder.MouseEnter += (s, e) =>
            {
                appBorder.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            };
            appBorder.MouseLeave += (s, e) =>
            {
                appBorder.Background = Brushes.White;
            };
            appBorder.MouseLeftButtonDown += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            return appBorder;
        }

        private void SetupContextMenu()
        {
            ContextMenu cm = new ContextMenu();
            
            MenuItem itemLock = new MenuItem { Header = "Bloquear Posición" };
            itemLock.IsCheckable = true;
            itemLock.IsChecked = _isLocked;
            itemLock.Click += (s, e) =>
            {
                _isLocked = itemLock.IsChecked;
            };

            MenuItem itemExit = new MenuItem { Header = "Cerrar Widget" };
            itemExit.Click += (s, e) =>
            {
                this.Close();
            };

            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }
    }
}
