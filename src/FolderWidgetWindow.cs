using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;
using System.Drawing; // For Icon
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace WidgUI
{
    public class FolderWidgetWindow : Window
    {
        private bool _isLocked = false;
        private bool _embeddedInDesktop = true;
        private bool _isExpanded = false;
        private bool _isAnimating = false;
        
        private Border _cardBorder;
        private Grid _mainGrid;
        private UniformGrid _iconsGrid;
        private Border _closeButton;
        
        // Overlay window for blur/dim background
        private Window _overlayWindow;
        
        // Store original position to restore on collapse
        private double _originalLeft;
        private double _originalTop;
        
        // Collapsed size constants
        private const double COLLAPSED_WIDTH = 140;
        private const double COLLAPSED_HEIGHT = 140;
        
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

            this.Width = COLLAPSED_WIDTH;
            this.Height = COLLAPSED_HEIGHT;
            
            // Allow drag and drop
            this.AllowDrop = true;
            this.DragEnter += FolderWidgetWindow_DragEnter;
            this.Drop += FolderWidgetWindow_Drop;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - this.Width - 50;
            this.Top = 320;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && !_isExpanded && e.ButtonState == MouseButtonState.Pressed)
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
                
                if (_isExpanded)
                {
                    RenderShortcuts();
                }
                else
                {
                    RenderShortcuts();
                }
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

            _mainGrid = new Grid();

            _iconsGrid = new UniformGrid
            {
                Columns = 2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _mainGrid.Children.Add(_iconsGrid);

            // Close button for expanded state
            _closeButton = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -8, -8, 0),
                Visibility = Visibility.Collapsed,
                Opacity = 0,
                Cursor = Cursors.Hand
            };
            TextBlock closeText = new TextBlock
            {
                Text = "✕",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _closeButton.Child = closeText;
            _closeButton.MouseLeftButtonDown += (s, e) => 
            {
                e.Handled = true;
                AnimateExpansion(false);
            };
            _closeButton.MouseEnter += (s, e) =>
            {
                _closeButton.Background = new SolidColorBrush(Color.FromArgb(220, 220, 50, 50));
            };
            _closeButton.MouseLeave += (s, e) =>
            {
                _closeButton.Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));
            };
            _mainGrid.Children.Add(_closeButton);

            _cardBorder.Child = _mainGrid;
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

        private void ShowOverlay()
        {
            if (_overlayWindow != null) return;
            
            _overlayWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Left = 0,
                Top = 0,
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                ResizeMode = ResizeMode.NoResize
            };
            
            // Use a grid with a dark frosted background
            Grid overlayGrid = new Grid();
            
            // Dark frosted layer
            Border frostedBg = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0, 10, 10, 20))
            };
            overlayGrid.Children.Add(frostedBg);
            
            _overlayWindow.Content = overlayGrid;
            
            // Click overlay to close
            _overlayWindow.MouseLeftButtonDown += (s, e) =>
            {
                AnimateExpansion(false);
            };
            
            _overlayWindow.Show();
            
            // Animate background color alpha from transparent to dark
            ColorAnimation colorAnim = new ColorAnimation
            {
                From = Color.FromArgb(0, 10, 10, 20),
                To = Color.FromArgb(160, 10, 10, 20),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            frostedBg.Background.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        }
        
        private void HideOverlay()
        {
            if (_overlayWindow == null) return;
            
            Window overlay = _overlayWindow;
            _overlayWindow = null;
            
            Grid overlayGrid = overlay.Content as Grid;
            if (overlayGrid != null && overlayGrid.Children.Count > 0)
            {
                Border frostedBg = overlayGrid.Children[0] as Border;
                if (frostedBg != null)
                {
                    ColorAnimation colorAnim = new ColorAnimation
                    {
                        To = Color.FromArgb(0, 10, 10, 20),
                        Duration = TimeSpan.FromMilliseconds(250),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };
                    colorAnim.Completed += (s, e) =>
                    {
                        overlay.Close();
                    };
                    frostedBg.Background.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
                }
                else
                {
                    overlay.Close();
                }
            }
            else
            {
                overlay.Close();
            }
        }

        private void AnimateExpansion(bool expand)
        {
            if (_isAnimating) return;
            _isAnimating = true;
            _isExpanded = expand;
            
            TimeSpan duration = TimeSpan.FromMilliseconds(350);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
            
            if (expand)
            {
                // Clear any existing animations so we can read real values
                this.BeginAnimation(Window.LeftProperty, null);
                this.BeginAnimation(Window.TopProperty, null);
                this.BeginAnimation(Window.WidthProperty, null);
                this.BeginAnimation(Window.HeightProperty, null);
                
                // Save original position AFTER clearing animations
                _originalLeft = this.Left;
                _originalTop = this.Top;
                
                // Calculate expanded size
                int itemCount = _shortcuts.Count;
                int cols = Math.Min(4, Math.Max(2, (int)Math.Ceiling(Math.Sqrt(itemCount))));
                int rows = (int)Math.Ceiling(itemCount / (double)cols);
                
                double expandedItemSize = 60;
                double expandedItemMargin = 6;
                double totalItemCell = expandedItemSize + (expandedItemMargin * 2);
                
                double contentWidth = cols * totalItemCell;
                double contentHeight = rows * totalItemCell;
                
                double padTotal = 24 + 16;
                double targetWidth = Math.Max(COLLAPSED_WIDTH * 2, contentWidth + padTotal);
                double targetHeight = Math.Max(COLLAPSED_HEIGHT * 2, contentHeight + padTotal + 10);
                
                // Keep it square-ish
                double maxDim = Math.Max(targetWidth, targetHeight);
                targetWidth = maxDim;
                targetHeight = maxDim;
                
                // Cap to reasonable size
                targetWidth = Math.Min(targetWidth, 500);
                targetHeight = Math.Min(targetHeight, 500);
                
                // Center on screen
                double screenW = SystemParameters.PrimaryScreenWidth;
                double screenH = SystemParameters.PrimaryScreenHeight;
                double targetLeft = (screenW - targetWidth) / 2.0;
                double targetTop = (screenH - targetHeight) / 2.0;
                
                // Update columns for expanded view
                _iconsGrid.Columns = cols;
                RenderShortcuts();
                
                // First show overlay, then bring folder to front
                this.Topmost = true;
                ShowOverlay();
                // Bring the folder ABOVE the overlay
                this.Activate();
                this.Focus();
                
                // Animate size FROM current to target
                DoubleAnimation animW = new DoubleAnimation
                {
                    From = COLLAPSED_WIDTH, To = targetWidth, Duration = duration, EasingFunction = easing
                };
                DoubleAnimation animH = new DoubleAnimation
                {
                    From = COLLAPSED_HEIGHT, To = targetHeight, Duration = duration, EasingFunction = easing
                };
                
                // Animate position FROM original to center
                DoubleAnimation animLeft = new DoubleAnimation
                {
                    From = _originalLeft, To = targetLeft, Duration = duration, EasingFunction = easing
                };
                DoubleAnimation animTop = new DoubleAnimation
                {
                    From = _originalTop, To = targetTop, Duration = duration, EasingFunction = easing
                };
                
                // Animate shadow to be more dramatic
                DropShadowEffect shadow = _cardBorder.Effect as DropShadowEffect;
                if (shadow != null)
                {
                    DoubleAnimation shadowBlur = new DoubleAnimation
                    {
                        To = 40, Duration = duration, EasingFunction = easing
                    };
                    DoubleAnimation shadowOpacity = new DoubleAnimation
                    {
                        To = 0.4, Duration = duration, EasingFunction = easing
                    };
                    DoubleAnimation shadowDepth = new DoubleAnimation
                    {
                        To = 12, Duration = duration, EasingFunction = easing
                    };
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlur);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepth);
                }
                
                // Show close button
                _closeButton.Visibility = Visibility.Visible;
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0, To = 1,
                    Duration = TimeSpan.FromMilliseconds(200),
                    BeginTime = TimeSpan.FromMilliseconds(200)
                };
                _closeButton.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                
                animW.Completed += (s, e) =>
                {
                    _isAnimating = false;
                };
                
                this.BeginAnimation(Window.WidthProperty, animW);
                this.BeginAnimation(Window.HeightProperty, animH);
                this.BeginAnimation(Window.LeftProperty, animLeft);
                this.BeginAnimation(Window.TopProperty, animTop);
            }
            else
            {
                // Collapse back
                _iconsGrid.Columns = 2;
                RenderShortcuts();
                
                // Animate back to original size and position
                DoubleAnimation animW = new DoubleAnimation
                {
                    To = COLLAPSED_WIDTH, Duration = duration, EasingFunction = easing
                };
                DoubleAnimation animH = new DoubleAnimation
                {
                    To = COLLAPSED_HEIGHT, Duration = duration, EasingFunction = easing
                };
                DoubleAnimation animLeft = new DoubleAnimation
                {
                    To = _originalLeft, Duration = duration, EasingFunction = easing
                };
                DoubleAnimation animTop = new DoubleAnimation
                {
                    To = _originalTop, Duration = duration, EasingFunction = easing
                };
                
                // Restore shadow
                DropShadowEffect shadow = _cardBorder.Effect as DropShadowEffect;
                if (shadow != null)
                {
                    DoubleAnimation shadowBlur = new DoubleAnimation
                    {
                        To = 15, Duration = duration, EasingFunction = easing
                    };
                    DoubleAnimation shadowOpacity = new DoubleAnimation
                    {
                        To = 0.2, Duration = duration, EasingFunction = easing
                    };
                    DoubleAnimation shadowDepth = new DoubleAnimation
                    {
                        To = 5, Duration = duration, EasingFunction = easing
                    };
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlur);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepth);
                }
                
                // Hide close button
                DoubleAnimation fadeOut = new DoubleAnimation
                {
                    To = 0, Duration = TimeSpan.FromMilliseconds(150)
                };
                fadeOut.Completed += (s, e) => _closeButton.Visibility = Visibility.Collapsed;
                _closeButton.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                
                animW.Completed += (s, e) =>
                {
                    // Clear ALL animations so properties go back to local values
                    this.BeginAnimation(Window.WidthProperty, null);
                    this.BeginAnimation(Window.HeightProperty, null);
                    this.BeginAnimation(Window.LeftProperty, null);
                    this.BeginAnimation(Window.TopProperty, null);
                    
                    // Explicitly set to original values
                    this.Width = COLLAPSED_WIDTH;
                    this.Height = COLLAPSED_HEIGHT;
                    this.Left = _originalLeft;
                    this.Top = _originalTop;
                    
                    _isAnimating = false;
                    this.Topmost = false;
                    
                    // Re-embed in desktop
                    if (_embeddedInDesktop)
                    {
                        DesktopManager.EmbedInDesktop(this);
                    }
                };
                
                // Hide overlay (animate simultaneously)
                HideOverlay();
                
                this.BeginAnimation(Window.WidthProperty, animW);
                this.BeginAnimation(Window.HeightProperty, animH);
                this.BeginAnimation(Window.LeftProperty, animLeft);
                this.BeginAnimation(Window.TopProperty, animTop);
            }
        }

        private void RenderShortcuts()
        {
            _iconsGrid.Children.Clear();
            
            int count = _shortcuts.Count;
            
            // Determine sizes based on expanded state
            double itemSize = _isExpanded ? 60 : 46;
            double iconSize = _isExpanded ? 32 : 24;
            double itemMargin = _isExpanded ? 6 : 4;
            double cornerRadius = _isExpanded ? 18 : 14;
            double fontSize = _isExpanded ? 22 : 18;
            
            if (_isExpanded)
            {
                for (int i = 0; i < count; i++)
                {
                    _iconsGrid.Children.Add(CreateAppShortcut(
                        _shortcuts[i].IconSource, _shortcuts[i].Tooltip, _shortcuts[i].Path,
                        itemSize, iconSize, itemMargin, cornerRadius, fontSize));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    if (i < 3)
                    {
                        _iconsGrid.Children.Add(CreateAppShortcut(
                            _shortcuts[i].IconSource, _shortcuts[i].Tooltip, _shortcuts[i].Path,
                            itemSize, iconSize, itemMargin, cornerRadius, fontSize));
                    }
                    else if (i == 3)
                    {
                        if (count == 4)
                        {
                            _iconsGrid.Children.Add(CreateAppShortcut(
                                _shortcuts[i].IconSource, _shortcuts[i].Tooltip, _shortcuts[i].Path,
                                itemSize, iconSize, itemMargin, cornerRadius, fontSize));
                        }
                        else
                        {
                            int extra = count - 3;
                            _iconsGrid.Children.Add(CreateMoreIndicator(extra, _shortcuts[i].IconSource,
                                itemSize, iconSize, itemMargin, cornerRadius));
                            break; 
                        }
                    }
                }
            }
        }

        private UIElement CreateMoreIndicator(int extraCount, ImageSource lastIconSource,
            double itemSize, double iconSize, double itemMargin, double cornerRadius)
        {
            Border appBorder = new Border
            {
                Width = itemSize,
                Height = itemSize,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(cornerRadius),
                Margin = new Thickness(itemMargin),
                Cursor = Cursors.Hand,
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
                    Width = iconSize,
                    Height = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.4
                };
                grid.Children.Add(img);
            }

            Border overlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                CornerRadius = new CornerRadius(cornerRadius)
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
            
            appBorder.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                AnimateExpansion(true);
            };
            
            return appBorder;
        }

        private UIElement CreateAppShortcut(ImageSource imageSource, string tooltip, string path,
            double itemSize, double iconSize, double itemMargin, double cornerRadius, double fontSize)
        {
            Border appBorder = new Border
            {
                Width = itemSize,
                Height = itemSize,
                Background = Brushes.White,
                CornerRadius = new CornerRadius(cornerRadius),
                Margin = new Thickness(itemMargin),
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
                    Width = iconSize,
                    Height = iconSize,
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
                    FontSize = fontSize,
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
