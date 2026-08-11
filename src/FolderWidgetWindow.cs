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
        private Border _dropOverlay;
        
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
            this.DragLeave += FolderWidgetWindow_DragLeave;
            this.Drop += FolderWidgetWindow_Drop;

            this.Deactivated += (s, e) =>
            {
                if (_isExpanded)
                {
                    AnimateExpansion(false);
                }
            };

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
            {
                e.Effects = DragDropEffects.Copy;
                if (_dropOverlay != null) _dropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FolderWidgetWindow_DragLeave(object sender, DragEventArgs e)
        {
            if (_dropOverlay != null) _dropOverlay.Visibility = Visibility.Collapsed;
        }

        private void FolderWidgetWindow_Drop(object sender, DragEventArgs e)
        {
            if (_dropOverlay != null) _dropOverlay.Visibility = Visibility.Collapsed;

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

            _dropOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(140, 240, 245, 255)),
                CornerRadius = new CornerRadius(30),
                Visibility = Visibility.Collapsed
            };
            TextBlock dropText = new TextBlock
            {
                Text = "Suelta aquí",
                Foreground = Brushes.DarkSlateGray,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _dropOverlay.Child = dropText;
            _mainGrid.Children.Add(_dropOverlay);

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
            
            // Use longer, smoother duration
            TimeSpan moveDuration = TimeSpan.FromMilliseconds(450);
            // Smooth deceleration for the main motion
            var moveEasing = new QuinticEase { EasingMode = EasingMode.EaseInOut };
            // Slight overshoot for a lively feel on expand
            var expandEasing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 };
            
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
                
                // Render shortcuts but start them invisible for staggered entrance
                RenderShortcuts();
                PrepareIconsForStaggeredEntrance();
                
                // Make window cover all screens to avoid clipping during smooth WPF render transform animation
                double virtLeft = SystemParameters.VirtualScreenLeft;
                double virtTop = SystemParameters.VirtualScreenTop;
                this.Left = virtLeft;
                this.Top = virtTop;
                this.Width = SystemParameters.VirtualScreenWidth;
                this.Height = SystemParameters.VirtualScreenHeight;
                
                // Setup _cardBorder for hardware-accelerated transform animation
                _cardBorder.HorizontalAlignment = HorizontalAlignment.Left;
                _cardBorder.VerticalAlignment = VerticalAlignment.Top;
                _cardBorder.Width = COLLAPSED_WIDTH;
                _cardBorder.Height = COLLAPSED_HEIGHT;
                
                double startX = _originalLeft - virtLeft;
                double startY = _originalTop - virtTop;
                double endX = targetLeft - virtLeft;
                double endY = targetTop - virtTop;
                
                TranslateTransform trans = new TranslateTransform(startX, startY);
                _cardBorder.RenderTransform = trans;
                
                // First show overlay, then bring folder to front
                this.Topmost = true;
                ShowOverlay();
                // Bring the folder ABOVE the overlay
                this.Activate();
                this.Focus();
                
                DoubleAnimation animW = new DoubleAnimation
                {
                    From = COLLAPSED_WIDTH, To = targetWidth,
                    Duration = moveDuration, EasingFunction = moveEasing
                };
                DoubleAnimation animH = new DoubleAnimation
                {
                    From = COLLAPSED_HEIGHT, To = targetHeight,
                    Duration = moveDuration, EasingFunction = moveEasing
                };
                
                DoubleAnimation animX = new DoubleAnimation
                {
                    From = startX, To = endX,
                    Duration = moveDuration, EasingFunction = moveEasing
                };
                DoubleAnimation animY = new DoubleAnimation
                {
                    From = startY, To = endY,
                    Duration = moveDuration, EasingFunction = moveEasing
                };
                
                // Animate border radius from large (collapsed) to slightly smaller (expanded)
                AnimateCornerRadius(_cardBorder, new CornerRadius(30), new CornerRadius(24), moveDuration, moveEasing);
                
                // Animate shadow to be more dramatic
                DropShadowEffect shadow = _cardBorder.Effect as DropShadowEffect;
                if (shadow != null)
                {
                    DoubleAnimation shadowBlur = new DoubleAnimation
                    {
                        To = 50, Duration = moveDuration, EasingFunction = moveEasing
                    };
                    DoubleAnimation shadowOpacity = new DoubleAnimation
                    {
                        To = 0.35, Duration = moveDuration, EasingFunction = moveEasing
                    };
                    DoubleAnimation shadowDepth = new DoubleAnimation
                    {
                        To = 15, Duration = moveDuration, EasingFunction = moveEasing
                    };
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlur);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepth);
                }
                
                animW.Completed += (s, e) =>
                {
                    _isAnimating = false;
                };
                
                _cardBorder.BeginAnimation(FrameworkElement.WidthProperty, animW);
                _cardBorder.BeginAnimation(FrameworkElement.HeightProperty, animH);
                trans.BeginAnimation(TranslateTransform.XProperty, animX);
                trans.BeginAnimation(TranslateTransform.YProperty, animY);
                
                // Stagger icons in after a small delay (let container start growing first)
                AnimateIconsStaggeredEntrance(true, TimeSpan.FromMilliseconds(120));
            }
            else
            {
                // First, animate icons OUT quickly before collapsing
                AnimateIconsStaggeredEntrance(false, TimeSpan.Zero);
                
                // Use slightly shorter duration for collapse (feels snappier)
                TimeSpan collapseDuration = TimeSpan.FromMilliseconds(380);
                var collapseEasing = new QuinticEase { EasingMode = EasingMode.EaseInOut };
                
                // Delay the window collapse slightly so icons start fading first
                TimeSpan collapseDelay = TimeSpan.FromMilliseconds(60);
                
                double virtLeft = SystemParameters.VirtualScreenLeft;
                double virtTop = SystemParameters.VirtualScreenTop;
                double targetX = _originalLeft - virtLeft;
                double targetY = _originalTop - virtTop;
                
                DoubleAnimation animW = new DoubleAnimation
                {
                    To = COLLAPSED_WIDTH, Duration = collapseDuration,
                    EasingFunction = collapseEasing, BeginTime = collapseDelay
                };
                DoubleAnimation animH = new DoubleAnimation
                {
                    To = COLLAPSED_HEIGHT, Duration = collapseDuration,
                    EasingFunction = collapseEasing, BeginTime = collapseDelay
                };
                DoubleAnimation animX = new DoubleAnimation
                {
                    To = targetX, Duration = collapseDuration,
                    EasingFunction = collapseEasing, BeginTime = collapseDelay
                };
                DoubleAnimation animY = new DoubleAnimation
                {
                    To = targetY, Duration = collapseDuration,
                    EasingFunction = collapseEasing, BeginTime = collapseDelay
                };
                
                // Animate corner radius back
                AnimateCornerRadius(_cardBorder, new CornerRadius(24), new CornerRadius(30), collapseDuration, collapseEasing);
                
                // Restore shadow
                DropShadowEffect shadow = _cardBorder.Effect as DropShadowEffect;
                if (shadow != null)
                {
                    DoubleAnimation shadowBlur = new DoubleAnimation
                    {
                        To = 15, Duration = collapseDuration, EasingFunction = collapseEasing
                    };
                    DoubleAnimation shadowOpacity = new DoubleAnimation
                    {
                        To = 0.2, Duration = collapseDuration, EasingFunction = collapseEasing
                    };
                    DoubleAnimation shadowDepth = new DoubleAnimation
                    {
                        To = 5, Duration = collapseDuration, EasingFunction = collapseEasing
                    };
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlur);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepth);
                }
                
                animW.Completed += (s, e) =>
                {
                    // Clean up animations on _cardBorder
                    _cardBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                    _cardBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
                    TranslateTransform t = _cardBorder.RenderTransform as TranslateTransform;
                    if (t != null)
                    {
                        t.BeginAnimation(TranslateTransform.XProperty, null);
                        t.BeginAnimation(TranslateTransform.YProperty, null);
                    }
                    _cardBorder.RenderTransform = null;
                    
                    // Reset card border layout
                    _cardBorder.Width = double.NaN;
                    _cardBorder.Height = double.NaN;
                    _cardBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
                    _cardBorder.VerticalAlignment = VerticalAlignment.Stretch;
                    
                    // Explicitly set window to original values
                    this.Width = COLLAPSED_WIDTH;
                    this.Height = COLLAPSED_HEIGHT;
                    this.Left = _originalLeft;
                    this.Top = _originalTop;
                    
                    // Switch back to collapsed layout
                    _iconsGrid.Columns = 2;
                    _isExpanded = false;
                    RenderShortcuts();
                    
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
                
                TranslateTransform trans = _cardBorder.RenderTransform as TranslateTransform;
                if (trans != null)
                {
                    _cardBorder.BeginAnimation(FrameworkElement.WidthProperty, animW);
                    _cardBorder.BeginAnimation(FrameworkElement.HeightProperty, animH);
                    trans.BeginAnimation(TranslateTransform.XProperty, animX);
                    trans.BeginAnimation(TranslateTransform.YProperty, animY);
                }
            }
        }
        
        /// <summary>
        /// Prepares all icons in _iconsGrid for a staggered entrance by setting them
        /// to invisible (scale 0, opacity 0) with a ScaleTransform centered at 0.5,0.5.
        /// </summary>
        private void PrepareIconsForStaggeredEntrance()
        {
            foreach (UIElement child in _iconsGrid.Children)
            {
                child.Opacity = 0;
                child.RenderTransformOrigin = new Point(0.5, 0.5);
                child.RenderTransform = new ScaleTransform(0.5, 0.5);
            }
        }
        
        /// <summary>
        /// Animates each icon in _iconsGrid with a staggered scale+fade effect.
        /// If entering: scales from 0.5 → 1 and fades in.
        /// If exiting: scales from 1 → 0.7 and fades out.
        /// </summary>
        private void AnimateIconsStaggeredEntrance(bool enter, TimeSpan initialDelay)
        {
            int count = _iconsGrid.Children.Count;
            double staggerMs = Math.Min(50, 200.0 / Math.Max(count, 1)); // Cap stagger for many items
            
            for (int i = 0; i < count; i++)
            {
                UIElement child = _iconsGrid.Children[i];
                TimeSpan itemDelay = initialDelay + TimeSpan.FromMilliseconds(i * staggerMs);
                
                if (enter)
                {
                    // Ensure transform is set
                    child.RenderTransformOrigin = new Point(0.5, 0.5);
                    if (!(child.RenderTransform is ScaleTransform))
                        child.RenderTransform = new ScaleTransform(0.5, 0.5);
                    
                    ScaleTransform st = child.RenderTransform as ScaleTransform;
                    
                    // Scale: 0.5 → 1 with BackEase for a subtle pop
                    var scaleEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
                    TimeSpan scaleDuration = TimeSpan.FromMilliseconds(320);
                    
                    DoubleAnimation scaleX = new DoubleAnimation
                    {
                        From = 0.5, To = 1, Duration = scaleDuration,
                        BeginTime = itemDelay, EasingFunction = scaleEase
                    };
                    DoubleAnimation scaleY = new DoubleAnimation
                    {
                        From = 0.5, To = 1, Duration = scaleDuration,
                        BeginTime = itemDelay, EasingFunction = scaleEase
                    };
                    
                    // Opacity: 0 → 1
                    DoubleAnimation fadeIn = new DoubleAnimation
                    {
                        From = 0, To = 1,
                        Duration = TimeSpan.FromMilliseconds(220),
                        BeginTime = itemDelay,
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                    child.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
                else
                {
                    // Exit: scale down and fade out quickly
                    child.RenderTransformOrigin = new Point(0.5, 0.5);
                    if (!(child.RenderTransform is ScaleTransform))
                        child.RenderTransform = new ScaleTransform(1, 1);
                    
                    ScaleTransform st = child.RenderTransform as ScaleTransform;
                    TimeSpan exitDuration = TimeSpan.FromMilliseconds(180);
                    var exitEasing = new QuadraticEase { EasingMode = EasingMode.EaseIn };
                    
                    // Reverse stagger: last items disappear first
                    TimeSpan reverseDelay = initialDelay + TimeSpan.FromMilliseconds((count - 1 - i) * (staggerMs * 0.5));
                    
                    DoubleAnimation scaleX = new DoubleAnimation
                    {
                        To = 0.7, Duration = exitDuration,
                        BeginTime = reverseDelay, EasingFunction = exitEasing
                    };
                    DoubleAnimation scaleY = new DoubleAnimation
                    {
                        To = 0.7, Duration = exitDuration,
                        BeginTime = reverseDelay, EasingFunction = exitEasing
                    };
                    DoubleAnimation fadeOut = new DoubleAnimation
                    {
                        To = 0, Duration = exitDuration,
                        BeginTime = reverseDelay, EasingFunction = exitEasing
                    };
                    
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
                    child.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
            }
        }
        
        /// <summary>
        /// Smoothly animates CornerRadius on a Border using a DispatcherTimer-based interpolation,
        /// since WPF doesn't natively support CornerRadius animations.
        /// </summary>
        private void AnimateCornerRadius(Border border, CornerRadius from, CornerRadius to, TimeSpan duration, IEasingFunction easing)
        {
            DateTime startTime = DateTime.Now;
            double totalMs = duration.TotalMilliseconds;
            
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60fps
            };
            
            timer.Tick += (s, e) =>
            {
                double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                double progress = Math.Min(elapsed / totalMs, 1.0);
                
                // Apply easing manually
                double easedProgress = progress;
                if (easing != null)
                {
                    easedProgress = easing.Ease(progress);
                }
                
                double topLeft = from.TopLeft + (to.TopLeft - from.TopLeft) * easedProgress;
                double topRight = from.TopRight + (to.TopRight - from.TopRight) * easedProgress;
                double bottomRight = from.BottomRight + (to.BottomRight - from.BottomRight) * easedProgress;
                double bottomLeft = from.BottomLeft + (to.BottomLeft - from.BottomLeft) * easedProgress;
                
                border.CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
                
                if (progress >= 1.0)
                {
                    timer.Stop();
                    border.CornerRadius = to;
                }
            };
            
            timer.Start();
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
