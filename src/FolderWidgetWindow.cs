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
        private const int AppsPerPage = 9;
        private const int ExpandedGridColumns = 3;
        private const int ExpandedGridRows = 3;
        
        private Grid _pagerPanel;
        private TextBlock _pageIndicator;
        private int _currentPage;
        
        private class ShortcutData 
        {
            public string Path { get; set; }
            public string Tooltip { get; set; }
            public ImageSource IconSource { get; set; }
        }
        private System.Collections.Generic.List<ShortcutData> _shortcuts = new System.Collections.Generic.List<ShortcutData>();
        private string _widgetId;

        public FolderWidgetWindow()
            : this(null)
        {
        }

        public FolderWidgetWindow(FolderWidgetLayoutData layoutData)
        {
            _widgetId = Guid.NewGuid().ToString();
            InitializeWindow();
            BuildUI();
            SetupContextMenu();
            this.Loaded += FolderWidgetWindow_Loaded;

            if (layoutData != null)
            {
                ApplyLayoutData(layoutData);
            }
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
                if (_dropOverlay != null)
                {
                    _dropOverlay.Visibility = Visibility.Visible;
                    if (_iconsGrid != null) _iconsGrid.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void FolderWidgetWindow_DragLeave(object sender, DragEventArgs e)
        {
            if (_dropOverlay != null)
            {
                _dropOverlay.Visibility = Visibility.Collapsed;
                if (_iconsGrid != null) _iconsGrid.Visibility = Visibility.Visible;
            }
        }

        private void FolderWidgetWindow_Drop(object sender, DragEventArgs e)
        {
            if (_dropOverlay != null)
            {
                _dropOverlay.Visibility = Visibility.Collapsed;
                if (_iconsGrid != null) _iconsGrid.Visibility = Visibility.Visible;
            }

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
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _iconsGrid = new UniformGrid
            {
                Columns = 2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(_iconsGrid, 0);
            _mainGrid.Children.Add(_iconsGrid);

            _pagerPanel = CreatePagerPanel();
            Grid.SetRow(_pagerPanel, 1);
            _mainGrid.Children.Add(_pagerPanel);

            _dropOverlay = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(30),
                Visibility = Visibility.Collapsed
            };
            TextBlock dropText = new TextBlock
            {
                Text = "Suelta aquí",
                Foreground = Brushes.White,
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

        private Grid CreatePagerPanel()
        {
            Grid pager = new Grid
            {
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed
            };
            pager.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            pager.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pager.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border prevButton = CreatePagerButton("\uE76B", () => GoToPage(_currentPage - 1));
            _pageIndicator = new TextBlock
            {
                Text = "1 / 1",
                Foreground = new SolidColorBrush(Color.FromRgb(70, 80, 95)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border nextButton = CreatePagerButton("\uE76C", () => GoToPage(_currentPage + 1));

            Grid.SetColumn(prevButton, 0);
            Grid.SetColumn(_pageIndicator, 1);
            Grid.SetColumn(nextButton, 2);
            pager.Children.Add(prevButton);
            pager.Children.Add(_pageIndicator);
            pager.Children.Add(nextButton);
            return pager;
        }

        private static Border CreatePagerButton(string glyph, Action action)
        {
            Border button = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 4, 0)
            };
            button.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 70, 85)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            button.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (action != null)
                {
                    action();
                }
            };
            return button;
        }

        private int GetTotalPages()
        {
            if (_shortcuts.Count == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(_shortcuts.Count / (double)AppsPerPage);
        }

        private void GoToPage(int page)
        {
            int totalPages = GetTotalPages();
            _currentPage = Math.Max(0, Math.Min(totalPages - 1, page));
            RenderShortcuts();
            UpdatePager();
        }

        private void UpdatePager()
        {
            if (_pagerPanel == null || _pageIndicator == null)
            {
                return;
            }

            int totalPages = GetTotalPages();
            bool showPager = _isExpanded && totalPages > 1;
            _pagerPanel.Visibility = showPager ? Visibility.Visible : Visibility.Collapsed;

            if (showPager)
            {
                _pageIndicator.Text = string.Format("{0} / {1}", _currentPage + 1, totalPages);
            }
        }

        private void AddShortcutData(string filePath)
        {
            try
            {
                string tooltip = System.IO.Path.GetFileNameWithoutExtension(filePath);
                
                // Extract high quality icon
                ImageSource imageSource = IconHelper.GetHighQualityIcon(filePath);

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
                Topmost = false,
                ShowInTaskbar = false,
                ShowActivated = false,
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
                _currentPage = 0;

                // Clear any existing animations so we can read real values
                this.BeginAnimation(Window.LeftProperty, null);
                this.BeginAnimation(Window.TopProperty, null);
                this.BeginAnimation(Window.WidthProperty, null);
                this.BeginAnimation(Window.HeightProperty, null);
                
                // Save original position AFTER clearing animations
                _originalLeft = this.Left;
                _originalTop = this.Top;
                
                // Fixed 3x3 grid with up to 9 apps per page
                int cols = ExpandedGridColumns;
                int rows = ExpandedGridRows;

                double expandedItemSize = 60;
                double expandedItemMargin = 6;
                double totalItemCell = expandedItemSize + (expandedItemMargin * 2);

                double contentWidth = cols * totalItemCell;
                double contentHeight = rows * totalItemCell;

                double padTotal = 24 + 16;
                double pagerHeight = GetTotalPages() > 1 ? 28 : 0;
                double targetWidth = Math.Max(COLLAPSED_WIDTH * 2, contentWidth + padTotal);
                double targetHeight = Math.Max(COLLAPSED_HEIGHT * 2, contentHeight + padTotal + pagerHeight + 10);
                
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
                _iconsGrid.Rows = rows;
                
                // Render shortcuts but start them invisible for staggered entrance
                RenderShortcuts();
                UpdatePager();
                PrepareIconsForStaggeredEntrance();
                
                // Show overlay behind folder
                this.Topmost = true;
                ShowOverlay();
                
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
                DoubleAnimation animL = new DoubleAnimation
                {
                    From = _originalLeft, To = targetLeft,
                    Duration = moveDuration, EasingFunction = moveEasing
                };
                DoubleAnimation animT = new DoubleAnimation
                {
                    From = _originalTop, To = targetTop,
                    Duration = moveDuration, EasingFunction = moveEasing
                };
                
                // Animate border radius from large (collapsed) to slightly smaller (expanded)
                AnimateCornerRadius(_cardBorder, new CornerRadius(30), new CornerRadius(24), moveDuration, moveEasing);
                
                // Animate shadow to be more dramatic
                DropShadowEffect shadow = _cardBorder.Effect as DropShadowEffect;
                if (shadow != null)
                {
                    DoubleAnimation shadowBlur = new DoubleAnimation { To = 50, Duration = moveDuration, EasingFunction = moveEasing };
                    DoubleAnimation shadowOpacity = new DoubleAnimation { To = 0.35, Duration = moveDuration, EasingFunction = moveEasing };
                    DoubleAnimation shadowDepth = new DoubleAnimation { To = 15, Duration = moveDuration, EasingFunction = moveEasing };
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlur);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepth);
                }
                
                animW.Completed += (s, e) => { _isAnimating = false; };
                
                this.BeginAnimation(Window.WidthProperty, animW);
                this.BeginAnimation(Window.HeightProperty, animH);
                this.BeginAnimation(Window.LeftProperty, animL);
                this.BeginAnimation(Window.TopProperty, animT);
                
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
                
                double targetX = _originalLeft;
                double targetY = _originalTop;
                
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
                    DoubleAnimation shadowBlur = new DoubleAnimation { To = 15, Duration = collapseDuration, EasingFunction = collapseEasing };
                    DoubleAnimation shadowOpacity = new DoubleAnimation { To = 0.2, Duration = collapseDuration, EasingFunction = collapseEasing };
                    DoubleAnimation shadowDepth = new DoubleAnimation { To = 5, Duration = collapseDuration, EasingFunction = collapseEasing };
                    shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlur);
                    shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
                    shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, shadowDepth);
                }
                
                animW.Completed += (s, e) =>
                {
                    this.BeginAnimation(Window.WidthProperty, null);
                    this.BeginAnimation(Window.HeightProperty, null);
                    this.BeginAnimation(Window.LeftProperty, null);
                    this.BeginAnimation(Window.TopProperty, null);
                    
                    this.Width = COLLAPSED_WIDTH;
                    this.Height = COLLAPSED_HEIGHT;
                    this.Left = _originalLeft;
                    this.Top = _originalTop;
                    
                    // Switch back to collapsed layout
                    _iconsGrid.Columns = 2;
                    _iconsGrid.Rows = 0;
                    _currentPage = 0;
                    _isExpanded = false;
                    RenderShortcuts();
                    UpdatePager();
                    
                    _isAnimating = false;
                    this.Topmost = false;
                };
                
                HideOverlay();
                
                this.BeginAnimation(Window.WidthProperty, animW);
                this.BeginAnimation(Window.HeightProperty, animH);
                this.BeginAnimation(Window.LeftProperty, animX);
                this.BeginAnimation(Window.TopProperty, animY);
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
            double iconSize = itemSize;
            double itemMargin = _isExpanded ? 6 : 4;
            double cornerRadius = _isExpanded ? 18 : 14;
            double fontSize = _isExpanded ? 22 : 18;
            
            if (_isExpanded)
            {
                _iconsGrid.Columns = ExpandedGridColumns;
                _iconsGrid.Rows = ExpandedGridRows;

                int startIndex = _currentPage * AppsPerPage;
                int endIndex = Math.Min(count, startIndex + AppsPerPage);

                for (int i = startIndex; i < endIndex; i++)
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
                    Opacity = 0.4,
                    Clip = new RectangleGeometry(new Rect(0, 0, iconSize, iconSize), cornerRadius, cornerRadius)
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
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
            
            appBorder.MouseEnter += (s, e) =>
            {
                overlay.Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0));
            };
            appBorder.MouseLeave += (s, e) =>
            {
                overlay.Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
            };
            
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

            Grid containerGrid = new Grid();

            if (imageSource != null)
            {
                System.Windows.Controls.Image img = new System.Windows.Controls.Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform,
                    Width = iconSize,
                    Height = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Clip = new RectangleGeometry(new Rect(0, 0, iconSize, iconSize), cornerRadius, cornerRadius)
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                containerGrid.Children.Add(img);
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
                containerGrid.Children.Add(tb);
            }

            // Darken overlay for hover
            Border darkenOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                CornerRadius = new CornerRadius(cornerRadius),
                IsHitTestVisible = false
            };
            containerGrid.Children.Add(darkenOverlay);

            appBorder.Child = containerGrid;

            appBorder.MouseEnter += (s, e) =>
            {
                darkenOverlay.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
            };
            appBorder.MouseLeave += (s, e) =>
            {
                darkenOverlay.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
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

        public FolderWidgetLayoutData ToLayoutData()
        {
            FolderWidgetLayoutData data = new FolderWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top
            };

            foreach (ShortcutData shortcut in _shortcuts)
            {
                if (!string.IsNullOrEmpty(shortcut.Path))
                {
                    data.Shortcuts.Add(shortcut.Path);
                }
            }

            return data;
        }

        public void ApplyLayoutData(FolderWidgetLayoutData data)
        {
            if (data == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(data.Id))
            {
                _widgetId = data.Id;
            }

            _isLocked = data.IsLocked;
            this.Left = data.Left;
            this.Top = data.Top;

            _shortcuts.Clear();
            if (data.Shortcuts != null)
            {
                foreach (string path in data.Shortcuts)
                {
                    if (File.Exists(path))
                    {
                        AddShortcutData(path);
                    }
                }
            }

            RenderShortcuts();
        }
    }
}
