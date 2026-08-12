using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace WidgUI
{
    public enum MusicWidgetVariant
    {
        ControlCenter,
        Immersive,
        Compact,
        IosPanel,
        Material,
        Transparent,
        SpotifyTile
    }

    public class MusicWidgetWindow : Window
    {
        private enum TransportIconKind
        {
            Previous,
            Play,
            Pause,
            Next
        }

        private bool _isLocked;
        private bool _embeddedInDesktop = true;
        private bool _isSeeking;
        private bool _isResizing;
        private MediaState _currentState = MediaState.Empty();
        private MusicWidgetVariant _currentVariant = MusicWidgetVariant.ControlCenter;

        private Grid _rootGrid;
        private Viewbox _contentViewbox;
        private Grid _designHost;
        private Border _resizeHandle;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _minWidth = 280;
        private double _minHeight = 120;
        private double _maxWidth = 640;
        private double _maxHeight = 320;
        private double _designWidth = 380;
        private double _designHeight = 162;

        private Border _cardBorder;
        private System.Windows.Controls.Image _albumArtImage;
        private System.Windows.Controls.Image _backgroundArtImage;
        private System.Windows.Controls.Image _avatarImage;
        private TextBlock _placeholderIcon;
        private TextBlock _titleText;
        private TextBlock _artistText;
        private StackPanel _equalizerPanel;
        private Grid _progressTrack;
        private Border _progressFill;
        private Ellipse _progressScrubber;
        private TextBlock _elapsedText;
        private TextBlock _remainingText;
        private Border _playPauseButton;
        private Border _prevButton;
        private Border _nextButton;
        private TextBlock _outputIcon;
        private Grid _materialProgressFillHost;
        private Path _materialWavePath;

        private SystemMediaHelper _mediaHelper;
        private DispatcherTimer _progressTimer;
        private DispatcherTimer _equalizerTimer;
        private readonly Random _random = new Random();
        private string _widgetId;

        public MusicWidgetWindow()
            : this(null)
        {
        }

        public MusicWidgetWindow(MusicWidgetLayoutData layoutData)
        {
            _widgetId = Guid.NewGuid().ToString();
            InitializeWindow();
            ApplyVariant(MusicWidgetVariant.ControlCenter);
            SetupContextMenu();
            SetupTimers();
            this.Loaded += MusicWidgetWindow_Loaded;
            this.Closed += MusicWidgetWindow_Closed;

            if (layoutData != null)
            {
                ApplyLayoutData(layoutData);
            }
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Musica";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - 430;
            this.Top = 520;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (_isLocked || _isResizing || IsInteractiveTarget(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };
        }

        private static bool IsInteractiveTarget(DependencyObject source)
        {
            while (source != null)
            {
                Border border = source as Border;
                if (border != null && border.Cursor == Cursors.Hand)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void MusicWidgetWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_embeddedInDesktop)
            {
                DesktopManager.EmbedInDesktop(this);
            }

            _mediaHelper = new SystemMediaHelper(this.Dispatcher);
            _mediaHelper.StateChanged += MediaHelper_StateChanged;
            _mediaHelper.Initialize();
        }

        private void MusicWidgetWindow_Closed(object sender, EventArgs e)
        {
            if (_mediaHelper != null)
            {
                _mediaHelper.StateChanged -= MediaHelper_StateChanged;
                _mediaHelper.Dispose();
                _mediaHelper = null;
            }

            if (_progressTimer != null)
            {
                _progressTimer.Stop();
            }

            if (_equalizerTimer != null)
            {
                _equalizerTimer.Stop();
            }
        }

        private void SetupTimers()
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _progressTimer.Tick += (s, e) =>
            {
                if (_currentState.HasSession && _currentState.IsPlaying && !_isSeeking)
                {
                    if (_currentState.Duration > TimeSpan.Zero)
                    {
                        _currentState.Position = _currentState.Position.Add(TimeSpan.FromMilliseconds(500));
                        if (_currentState.Position > _currentState.Duration)
                        {
                            _currentState.Position = _currentState.Duration;
                        }
                    }

                    ApplyTimeline(_currentState, true);
                }
            };
            _progressTimer.Start();

            _equalizerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _equalizerTimer.Tick += (s, e) => AnimateEqualizer();
            _equalizerTimer.Start();
        }

        public void ApplyVariant(MusicWidgetVariant variant)
        {
            _currentVariant = variant;
            SetVariantSizeLimits(variant);

            switch (variant)
            {
                case MusicWidgetVariant.Immersive:
                    SetWidgetSize(300, 300);
                    BuildImmersiveUI();
                    break;
                case MusicWidgetVariant.Compact:
                    SetWidgetSize(420, 118);
                    BuildCompactUI();
                    break;
                case MusicWidgetVariant.IosPanel:
                    SetWidgetSize(400, 145);
                    BuildIosPanelUI();
                    break;
                case MusicWidgetVariant.Material:
                    SetWidgetSize(480, 168);
                    BuildMaterialUI();
                    break;
                case MusicWidgetVariant.Transparent:
                    SetWidgetSize(280, 84);
                    BuildTransparentUI();
                    break;
                case MusicWidgetVariant.SpotifyTile:
                    SetWidgetSize(200, 200);
                    BuildSpotifyTileUI();
                    break;
                default:
                    SetWidgetSize(380, 162);
                    BuildControlCenterUI();
                    break;
            }

            SetupContextMenu();
            MediaHelper_StateChanged(_currentState);
        }

        private void SetVariantSizeLimits(MusicWidgetVariant variant)
        {
            switch (variant)
            {
                case MusicWidgetVariant.Immersive:
                    _minWidth = 180;
                    _minHeight = 180;
                    _maxWidth = 480;
                    _maxHeight = 480;
                    break;
                case MusicWidgetVariant.Compact:
                    _minWidth = 300;
                    _minHeight = 90;
                    _maxWidth = 760;
                    _maxHeight = 220;
                    break;
                case MusicWidgetVariant.IosPanel:
                    _minWidth = 320;
                    _minHeight = 110;
                    _maxWidth = 680;
                    _maxHeight = 240;
                    break;
                case MusicWidgetVariant.Material:
                    _minWidth = 360;
                    _minHeight = 130;
                    _maxWidth = 720;
                    _maxHeight = 260;
                    break;
                case MusicWidgetVariant.Transparent:
                    _minWidth = 180;
                    _minHeight = 64;
                    _maxWidth = 600;
                    _maxHeight = 150;
                    break;
                case MusicWidgetVariant.SpotifyTile:
                    _minWidth = 160;
                    _minHeight = 160;
                    _maxWidth = 320;
                    _maxHeight = 320;
                    break;
                default:
                    _minWidth = 280;
                    _minHeight = 120;
                    _maxWidth = 640;
                    _maxHeight = 320;
                    break;
            }
        }

        private void SetWidgetSize(double width, double height)
        {
            _designWidth = width;
            _designHeight = height;
            this.Width = ClampWidgetSize(width, true);
            this.Height = ClampWidgetSize(height, false);
            UpdateDesignSize(this.Width, this.Height);
        }

        private void FinishResizableLayout(UIElement content)
        {
            _designHost = new Grid
            {
                Width = _designWidth,
                Height = _designHeight
            };
            _designHost.Children.Add(content);

            _rootGrid = new Grid();
            _contentViewbox = new Viewbox
            {
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both
            };
            _contentViewbox.Child = _designHost;
            _rootGrid.Children.Add(_contentViewbox);

            _resizeHandle = CreateResizeHandle();
            _rootGrid.Children.Add(_resizeHandle);

            this.Content = _rootGrid;
        }

        private void UpdateDesignSize(double width, double height)
        {
            if (_designHost != null)
            {
                _designHost.Width = width;
                _designHost.Height = height;
            }
        }

        private Border CreateResizeHandle()
        {
            Border handle = new Border
            {
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                CornerRadius = new CornerRadius(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Arrastra para cambiar tamano",
                Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible
            };

            handle.Child = new TextBlock
            {
                Text = "\uE7E8",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 9,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            handle.MouseLeftButtonDown += ResizeHandle_MouseLeftButtonDown;
            handle.MouseMove += ResizeHandle_MouseMove;
            handle.MouseLeftButtonUp += ResizeHandle_MouseLeftButtonUp;
            return handle;
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked)
            {
                return;
            }

            _isResizing = true;
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

            this.Width = ClampWidgetSize(_resizeStartWidth + deltaX, true);
            this.Height = ClampWidgetSize(_resizeStartHeight + deltaY, false);
            UpdateDesignSize(this.Width, this.Height);
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing)
            {
                return;
            }

            _isResizing = false;
            _resizeHandle.ReleaseMouseCapture();
        }

        private double ClampWidgetSize(double value, bool isWidth)
        {
            double min = isWidth ? _minWidth : _minHeight;
            double max = isWidth ? _maxWidth : _maxHeight;
            return Math.Max(min, Math.Min(max, value));
        }

        private Border CreateCardShell(double cornerRadius, Thickness padding, Thickness borderThickness)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(cornerRadius),
                Background = new SolidColorBrush(Color.FromArgb(210, 34, 34, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = borderThickness,
                Padding = padding,
                ClipToBounds = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 6,
                    Opacity = 0.35,
                    BlurRadius = 18
                }
            };
        }

        private Border AddBlurredBackgroundLayers(Grid shell, double cornerRadius, byte frostAlpha, byte frostR, byte frostG, byte frostB)
        {
            ApplyRoundedClip(shell, cornerRadius);

            Border placeholderBg = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(48, 44, 52),
                    Color.FromRgb(24, 22, 28),
                    90)
            };

            _backgroundArtImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed,
                Effect = new BlurEffect
                {
                    Radius = 28,
                    RenderingBias = RenderingBias.Quality
                }
            };
            RenderOptions.SetBitmapScalingMode(_backgroundArtImage, BitmapScalingMode.HighQuality);

            Border frostOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(frostAlpha, frostR, frostG, frostB))
            };

            shell.Children.Add(placeholderBg);
            shell.Children.Add(_backgroundArtImage);
            shell.Children.Add(frostOverlay);
            return placeholderBg;
        }

        private void BuildControlCenterUI()
        {
            _cardBorder = CreateCardShell(22, new Thickness(0), new Thickness(0));
            _cardBorder.Background = Brushes.Transparent;

            Grid shell = new Grid { ClipToBounds = true };
            ApplyRoundedClip(shell, 22);

            Border placeholderBg = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(48, 44, 52),
                    Color.FromRgb(24, 22, 28),
                    90)
            };

            _backgroundArtImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed,
                Effect = new BlurEffect
                {
                    Radius = 28,
                    RenderingBias = RenderingBias.Quality
                }
            };
            RenderOptions.SetBitmapScalingMode(_backgroundArtImage, BitmapScalingMode.HighQuality);

            Border frostOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(155, 22, 22, 26))
            };

            Grid content = new Grid { Margin = new Thickness(14, 12, 14, 12) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid topRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border albumArt = CreateAlbumArtBorder(54, 10);
            StackPanel textPanel = CreateTitleArtistPanel(15, 12);
            textPanel.Margin = new Thickness(12, 0, 8, 0);
            _equalizerPanel = CreateEqualizer();

            Grid.SetColumn(albumArt, 0);
            Grid.SetColumn(textPanel, 1);
            Grid.SetColumn(_equalizerPanel, 2);
            topRow.Children.Add(albumArt);
            topRow.Children.Add(textPanel);
            topRow.Children.Add(_equalizerPanel);

            Grid timelineRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            timelineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            timelineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timelineRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _elapsedText = CreateTimeLabel("0:00", HorizontalAlignment.Left, 11, new Thickness(0));
            _remainingText = CreateTimeLabel("-0:00", HorizontalAlignment.Right, 11, new Thickness(0));

            CreateProgressBar(4, 2);
            _progressTrack.Margin = new Thickness(10, 0, 10, 0);
            _progressTrack.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(_elapsedText, 0);
            Grid.SetColumn(_progressTrack, 1);
            Grid.SetColumn(_remainingText, 2);
            timelineRow.Children.Add(_elapsedText);
            timelineRow.Children.Add(_progressTrack);
            timelineRow.Children.Add(_remainingText);

            Grid controlsRow = new Grid();
            StackPanel controls = CreateTransportControls(28, 38, 20, 34, 38, false, HorizontalAlignment.Center, true);
            controlsRow.Children.Add(controls);

            _outputIcon = new TextBlock
            {
                Text = "\uE7F5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 175)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            controlsRow.Children.Add(_outputIcon);

            Grid.SetRow(topRow, 0);
            Grid.SetRow(timelineRow, 1);
            Grid.SetRow(controlsRow, 2);
            content.Children.Add(topRow);
            content.Children.Add(timelineRow);
            content.Children.Add(controlsRow);

            shell.Children.Add(placeholderBg);
            shell.Children.Add(_backgroundArtImage);
            shell.Children.Add(frostOverlay);
            shell.Children.Add(content);

            _avatarImage = null;

            _cardBorder.Child = shell;
            FinishResizableLayout(_cardBorder);
        }

        private void BuildImmersiveUI()
        {
            _cardBorder = new Border
            {
                CornerRadius = new CornerRadius(40),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1.5),
                ClipToBounds = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 0,
                    Opacity = 0.55,
                    BlurRadius = 30
                }
            };

            Grid root = new Grid();

            Border placeholderBg = new Border
            {
                Name = "ImmersivePlaceholder",
                Background = new LinearGradientBrush(
                    Color.FromRgb(62, 58, 55),
                    Color.FromRgb(28, 26, 24),
                    90)
            };
            root.Children.Add(placeholderBg);

            _backgroundArtImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(_backgroundArtImage, BitmapScalingMode.HighQuality);
            root.Children.Add(_backgroundArtImage);

            Border topFade = new Border
            {
                VerticalAlignment = VerticalAlignment.Top,
                Height = 80,
                Background = new LinearGradientBrush(
                    Color.FromArgb(120, 0, 0, 0),
                    Color.FromArgb(0, 0, 0, 0),
                    90)
            };
            root.Children.Add(topFade);

            Border bottomFade = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 150,
                Background = new LinearGradientBrush(
                    Color.FromArgb(0, 0, 0, 0),
                    Color.FromArgb(200, 0, 0, 0),
                    90)
            };
            root.Children.Add(bottomFade);

            Grid overlay = new Grid { Margin = new Thickness(14, 14, 14, 12) };
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overlay.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid identityRow = new Grid
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 200
            };
            identityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            identityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid avatarWrap = new Grid
            {
                Width = 34,
                Height = 34,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Border avatarBorder = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            Grid avatarGrid = new Grid();
            _avatarImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(_avatarImage, BitmapScalingMode.HighQuality);
            _placeholderIcon = new TextBlock
            {
                Text = "\uE8D6",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarGrid.Children.Add(_avatarImage);
            avatarGrid.Children.Add(_placeholderIcon);
            ApplyRoundedClip(avatarGrid, 17);
            avatarBorder.Child = avatarGrid;
            avatarWrap.Children.Add(avatarBorder);

            Border cameraBadge = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(230, 28, 28, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -3, -3)
            };
            cameraBadge.Child = new TextBlock
            {
                Text = "\uE722",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 8,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarWrap.Children.Add(cameraBadge);

            StackPanel identityText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 0, BlurRadius = 6, Opacity = 0.5 }
            };
            _artistText = new TextBlock
            {
                Text = "@artista",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 220, 220, 225)),
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 0, BlurRadius = 6, Opacity = 0.5 }
            };
            identityText.Children.Add(_titleText);
            identityText.Children.Add(_artistText);

            Grid.SetColumn(avatarWrap, 0);
            Grid.SetColumn(identityText, 1);
            identityRow.Children.Add(avatarWrap);
            identityRow.Children.Add(identityText);

            StackPanel actionButtons = new StackPanel { Orientation = Orientation.Horizontal };
            actionButtons.Children.Add(CreateIconCircle("\uE72D", 34));
            actionButtons.Children.Add(CreateIconCircle("\uEB52", 34, new Thickness(8, 0, 0, 0)));

            Grid.SetColumn(identityRow, 0);
            Grid.SetColumn(actionButtons, 1);
            topRow.Children.Add(identityRow);
            topRow.Children.Add(actionButtons);

            Grid timeRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            _elapsedText = CreateTimeLabel("0:00", HorizontalAlignment.Left, 11, new Thickness(0));
            _remainingText = CreateTimeLabel("-0:00", HorizontalAlignment.Right, 11, new Thickness(0));
            timeRow.Children.Add(_elapsedText);
            timeRow.Children.Add(_remainingText);

            Grid progressRow = new Grid();
            CreateImmersiveProgressBar();
            progressRow.Children.Add(_progressTrack);

            StackPanel controls = CreateTransportControls(11, 13, 11, 40, 44, true);
            controls.Margin = new Thickness(0, 10, 0, 0);

            Grid.SetRow(topRow, 0);
            Grid.SetRow(timeRow, 2);
            Grid.SetRow(progressRow, 3);
            Grid.SetRow(controls, 4);
            overlay.Children.Add(topRow);
            overlay.Children.Add(timeRow);
            overlay.Children.Add(progressRow);
            overlay.Children.Add(controls);

            root.Children.Add(overlay);
            ApplyRoundedClip(root, 40);
            _cardBorder.Child = root;
            FinishResizableLayout(_cardBorder);
        }

        private static void ApplyRoundedClip(FrameworkElement element, double radius)
        {
            RectangleGeometry clip = new RectangleGeometry { RadiusX = radius, RadiusY = radius };
            element.Clip = clip;

            element.SizeChanged += (s, e) =>
            {
                clip.Rect = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            };

            if (element.ActualWidth > 0 && element.ActualHeight > 0)
            {
                clip.Rect = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            }
        }

        private void CreateImmersiveProgressBar()
        {
            _progressScrubber = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(-3, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };

            _progressTrack = new Grid
            {
                Height = 14,
                Cursor = Cursors.Hand
            };

            Border trackLine = new Border
            {
                Height = 2,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))
            };

            _progressFill = new Border
            {
                Width = 0,
                Height = 3,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(1.5),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            _progressTrack.Children.Add(trackLine);
            _progressTrack.Children.Add(_progressFill);
            _progressTrack.Children.Add(_progressScrubber);

            _progressTrack.MouseLeftButtonDown += ProgressTrack_MouseLeftButtonDown;
            _progressTrack.MouseLeftButtonUp += ProgressTrack_MouseLeftButtonUp;
            _progressTrack.MouseMove += ProgressTrack_MouseMove;
        }

        private void BuildCompactUI()
        {
            _cardBorder = CreateCardShell(16, new Thickness(0), new Thickness(0));
            _cardBorder.Background = Brushes.Transparent;

            Grid shell = new Grid { ClipToBounds = true };
            ApplyRoundedClip(shell, 16);

            Border placeholderBg = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(48, 44, 52),
                    Color.FromRgb(24, 22, 28),
                    90)
            };

            _backgroundArtImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed,
                Effect = new BlurEffect
                {
                    Radius = 28,
                    RenderingBias = RenderingBias.Quality
                }
            };
            RenderOptions.SetBitmapScalingMode(_backgroundArtImage, BitmapScalingMode.HighQuality);

            Border frostOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(155, 22, 22, 26))
            };

            Grid content = new Grid { Margin = new Thickness(10) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border albumArt = CreateAlbumArtBorder(88, 12);
            Grid.SetColumn(albumArt, 0);
            content.Children.Add(albumArt);

            Grid right = new Grid { Margin = new Thickness(12, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid titleRow = new Grid();
            StackPanel textPanel = CreateTitleArtistPanel(13, 10);
            textPanel.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(textPanel);

            _outputIcon = new TextBlock
            {
                Text = "\uE7F5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 195)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            titleRow.Children.Add(_outputIcon);

            StackPanel controls = CreateTransportControls(12, 14, 12, 0, 0, false, HorizontalAlignment.Left);
            controls.Margin = new Thickness(0, 2, 0, 2);

            Grid progressBlock = new Grid();
            CreateProgressBar(5, 0);
            _progressTrack.Margin = new Thickness(0, 0, 0, 2);
            progressBlock.Children.Add(_progressTrack);

            Grid timeRow = new Grid();
            _elapsedText = CreateTimeLabel("-:--", HorizontalAlignment.Left, 9, new Thickness(0));
            _remainingText = CreateTimeLabel("-:--", HorizontalAlignment.Right, 9, new Thickness(0));
            timeRow.Children.Add(_elapsedText);
            timeRow.Children.Add(_remainingText);

            Grid.SetRow(titleRow, 0);
            Grid.SetRow(controls, 1);
            Grid.SetRow(progressBlock, 2);
            Grid.SetRow(timeRow, 3);
            right.Children.Add(titleRow);
            right.Children.Add(controls);
            right.Children.Add(progressBlock);
            right.Children.Add(timeRow);

            Grid.SetColumn(right, 1);
            content.Children.Add(right);

            shell.Children.Add(placeholderBg);
            shell.Children.Add(_backgroundArtImage);
            shell.Children.Add(frostOverlay);
            shell.Children.Add(content);

            _equalizerPanel = null;
            _avatarImage = null;

            _cardBorder.Child = shell;
            FinishResizableLayout(_cardBorder);
        }

        private void BuildIosPanelUI()
        {
            _materialProgressFillHost = null;
            _materialWavePath = null;

            _cardBorder = CreateCardShell(24, new Thickness(0), new Thickness(0));
            _cardBorder.Background = Brushes.Transparent;

            Grid shell = new Grid();
            AddBlurredBackgroundLayers(shell, 24, 155, 22, 22, 26);

            Grid content = new Grid { Margin = new Thickness(12) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border albumArt = CreateAlbumArtBorder(108, 14);
            Grid.SetColumn(albumArt, 0);
            content.Children.Add(albumArt);

            Grid right = new Grid { Margin = new Thickness(14, 2, 0, 2) };
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _artistText = new TextBlock
            {
                Text = "Artista",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(175, 175, 180)),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Collapsed
            };
            StackPanel titleStack = new StackPanel();
            titleStack.Children.Add(_titleText);
            titleStack.Children.Add(_artistText);
            header.Children.Add(titleStack);

            _outputIcon = new TextBlock
            {
                Text = "\uE7F5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(195, 195, 200)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            header.Children.Add(_outputIcon);

            StackPanel controls = CreateTransportControls(22, 30, 18, 36, 40, false);
            controls.VerticalAlignment = VerticalAlignment.Center;

            Grid progressBlock = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            CreateProgressBar(3, 1.5);
            progressBlock.Children.Add(_progressTrack);

            Grid timeRow = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            _elapsedText = CreateTimeLabel("-:--", HorizontalAlignment.Left, 10, new Thickness(0));
            _remainingText = CreateTimeLabel("-:--", HorizontalAlignment.Right, 10, new Thickness(0));
            timeRow.Children.Add(_elapsedText);
            timeRow.Children.Add(_remainingText);

            Grid.SetRow(header, 0);
            Grid.SetRow(controls, 1);
            Grid.SetRow(progressBlock, 2);
            Grid.SetRow(timeRow, 3);
            right.Children.Add(header);
            right.Children.Add(controls);
            right.Children.Add(progressBlock);
            right.Children.Add(timeRow);

            Grid.SetColumn(right, 1);
            content.Children.Add(right);

            shell.Children.Add(content);

            _equalizerPanel = null;
            _avatarImage = null;

            _cardBorder.Child = shell;
            FinishResizableLayout(_cardBorder);
        }

        private void BuildMaterialUI()
        {
            _cardBorder = CreateCardShell(28, new Thickness(0), new Thickness(0));
            _cardBorder.Background = Brushes.Transparent;

            Grid shell = new Grid();
            AddBlurredBackgroundLayers(shell, 28, 170, 26, 40, 58);

            Grid content = new Grid { Margin = new Thickness(16, 14, 16, 12) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            TextBlock appIcon = new TextBlock
            {
                Text = "\uE1D6",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border devicePill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(120, 120, 180, 210)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            StackPanel pillContent = new StackPanel { Orientation = Orientation.Horizontal };
            pillContent.Children.Add(new TextBlock
            {
                Text = "\uE7F6",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 245, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            pillContent.Children.Add(new TextBlock
            {
                Text = "Salida de audio",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 245, 255)),
                VerticalAlignment = VerticalAlignment.Center
            });
            devicePill.Child = pillContent;
            header.Children.Add(appIcon);
            header.Children.Add(devicePill);

            Grid middle = new Grid();
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            middle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel trackInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _artistText = new TextBlock
            {
                Text = "Artista",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(184, 200, 216)),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            trackInfo.Children.Add(_titleText);
            trackInfo.Children.Add(_artistText);

            _playPauseButton = CreateMaterialPlayButton(58);
            Grid.SetColumn(trackInfo, 0);
            Grid.SetColumn(_playPauseButton, 1);
            middle.Children.Add(trackInfo);
            middle.Children.Add(_playPauseButton);

            Grid footer = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _prevButton = CreateMaterialIconButton("\uE710", 28, null);
            CreateMaterialProgressBar();
            _progressTrack.Margin = new Thickness(10, 0, 10, 0);
            _progressTrack.VerticalAlignment = VerticalAlignment.Center;

            StackPanel footerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _nextButton = CreateMaterialIconButton("\uE893", 28, () => _mediaHelper.SkipNext());
            _nextButton.Margin = new Thickness(0, 0, 10, 0);
            _outputIcon = new TextBlock
            {
                Text = "\uE7F5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            TextBlock repeatIcon = new TextBlock
            {
                Text = "\uE8EE",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            footerActions.Children.Add(_nextButton);
            footerActions.Children.Add(_outputIcon);
            footerActions.Children.Add(repeatIcon);

            Grid.SetColumn(_prevButton, 0);
            Grid.SetColumn(_progressTrack, 1);
            Grid.SetColumn(footerActions, 2);
            footer.Children.Add(_prevButton);
            footer.Children.Add(_progressTrack);
            footer.Children.Add(footerActions);

            Grid.SetRow(header, 0);
            Grid.SetRow(middle, 1);
            Grid.SetRow(footer, 2);
            content.Children.Add(header);
            content.Children.Add(middle);
            content.Children.Add(footer);

            shell.Children.Add(content);

            _equalizerPanel = null;
            _albumArtImage = null;
            _avatarImage = null;
            _elapsedText = null;
            _remainingText = null;

            _cardBorder.Child = shell;
            FinishResizableLayout(_cardBorder);
        }

        private void BuildTransparentUI()
        {
            _cardBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ClipToBounds = false
            };

            Grid content = new Grid { Margin = new Thickness(10) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border albumArt = CreateAlbumArtBorder(64, 12);
            Grid.SetColumn(albumArt, 0);
            content.Children.Add(albumArt);

            StackPanel textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 0,
                    Opacity = 0.65,
                    BlurRadius = 4
                }
            };

            _artistText = new TextBlock
            {
                Text = "♫ Sin artista",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 0,
                    Opacity = 0.65,
                    BlurRadius = 4
                }
            };

            textPanel.Children.Add(_titleText);
            textPanel.Children.Add(_artistText);

            Grid.SetColumn(textPanel, 1);
            content.Children.Add(textPanel);

            _equalizerPanel = null;
            _avatarImage = null;
            _backgroundArtImage = null;
            _elapsedText = null;
            _remainingText = null;
            _progressTrack = null;
            _progressFill = null;
            _progressScrubber = null;
            _playPauseButton = null;
            _prevButton = null;
            _nextButton = null;
            _outputIcon = null;
            _materialProgressFillHost = null;
            _materialWavePath = null;

            _cardBorder.Child = content;
            FinishResizableLayout(_cardBorder);
        }

        private void BuildSpotifyTileUI()
        {
            _materialProgressFillHost = null;
            _materialWavePath = null;

            const double tileRadius = 32;
            Color spotifyRed = Color.FromRgb(209, 51, 62);

            _cardBorder = CreateCardShell(tileRadius, new Thickness(14), new Thickness(0));
            _cardBorder.Background = new SolidColorBrush(spotifyRed);

            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border albumArt = CreateAlbumArtBorder(74, 10);
            albumArt.HorizontalAlignment = HorizontalAlignment.Left;
            albumArt.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(albumArt, 0);
            Grid.SetColumn(albumArt, 0);
            content.Children.Add(albumArt);

            Border spotifyBadge = CreateSpotifyBadge(30);
            spotifyBadge.HorizontalAlignment = HorizontalAlignment.Right;
            spotifyBadge.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(spotifyBadge, 0);
            Grid.SetColumn(spotifyBadge, 1);
            content.Children.Add(spotifyBadge);

            StackPanel textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _artistText = new TextBlock
            {
                Text = "Artista",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Collapsed
            };
            textPanel.Children.Add(_titleText);
            textPanel.Children.Add(_artistText);
            Grid.SetRow(textPanel, 1);
            Grid.SetColumn(textPanel, 0);
            content.Children.Add(textPanel);

            _playPauseButton = CreateSpotifyPlayButton(54, spotifyRed);
            _playPauseButton.HorizontalAlignment = HorizontalAlignment.Right;
            _playPauseButton.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetRow(_playPauseButton, 1);
            Grid.SetColumn(_playPauseButton, 1);
            content.Children.Add(_playPauseButton);

            _equalizerPanel = null;
            _avatarImage = null;
            _backgroundArtImage = null;
            _elapsedText = null;
            _remainingText = null;
            _progressTrack = null;
            _progressFill = null;
            _progressScrubber = null;
            _prevButton = null;
            _nextButton = null;
            _outputIcon = null;

            _cardBorder.Child = content;
            FinishResizableLayout(_cardBorder);
        }

        private static Border CreateSpotifyBadge(double size)
        {
            Border badge = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };

            Canvas logoCanvas = new Canvas
            {
                Width = size,
                Height = size
            };

            SolidColorBrush logoBrush = new SolidColorBrush(Color.FromRgb(29, 185, 84));
            Path arc1 = new Path
            {
                Data = Geometry.Parse("M 8.5,17.5 A 9,9 0 0 1 21.5,17.5"),
                Stroke = logoBrush,
                StrokeThickness = 2.2,
                StrokeStartLineCap = PenLineCap.Round
            };
            Path arc2 = new Path
            {
                Data = Geometry.Parse("M 10,14 A 6.5,6.5 0 0 1 20,14"),
                Stroke = logoBrush,
                StrokeThickness = 2.2,
                StrokeStartLineCap = PenLineCap.Round
            };
            Path arc3 = new Path
            {
                Data = Geometry.Parse("M 11.5,10.5 A 4,4 0 0 1 18.5,10.5"),
                Stroke = logoBrush,
                StrokeThickness = 2.2,
                StrokeStartLineCap = PenLineCap.Round
            };

            logoCanvas.Children.Add(arc1);
            logoCanvas.Children.Add(arc2);
            logoCanvas.Children.Add(arc3);
            badge.Child = logoCanvas;
            return badge;
        }

        private Border CreateSpotifyPlayButton(double size, Color accentColor)
        {
            Border button = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = Brushes.White,
                Cursor = Cursors.Hand
            };

            Path path = new Path
            {
                Data = CreateTransportIconGeometry(TransportIconKind.Play),
                Fill = new SolidColorBrush(accentColor),
                Stretch = Stretch.Uniform
            };

            button.Tag = path;
            button.Child = new Viewbox
            {
                Width = size * 0.38,
                Height = size * 0.38,
                Margin = new Thickness(size * 0.04, 0, 0, 0),
                Child = path
            };

            button.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (_mediaHelper != null)
                {
                    _mediaHelper.TogglePlayPause();
                }
            };

            return button;
        }

        private Border CreateMaterialPlayButton(double size)
        {
            Border button = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(140, 210, 235)),
                Cursor = Cursors.Hand
            };

            Path path = new Path
            {
                Data = CreateTransportIconGeometry(TransportIconKind.Play),
                Fill = new SolidColorBrush(Color.FromRgb(38, 44, 52)),
                Stretch = Stretch.Uniform
            };

            button.Tag = path;
            button.Child = new Viewbox
            {
                Width = size * 0.38,
                Height = size * 0.38,
                Child = path
            };

            button.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (_mediaHelper != null)
                {
                    _mediaHelper.TogglePlayPause();
                }
            };

            return button;
        }

        private Border CreateMaterialIconButton(string glyph, double size, Action action)
        {
            Border button = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = action == null
                    ? new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
                    : Brushes.Transparent,
                Cursor = action != null ? Cursors.Hand : Cursors.Arrow
            };

            button.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (action != null)
            {
                button.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    action();
                };
            }

            return button;
        }

        private void CreateMaterialProgressBar()
        {
            _progressScrubber = null;
            _progressFill = null;

            _progressTrack = new Grid
            {
                Height = 20,
                Cursor = Cursors.Hand
            };

            Border trackLine = new Border
            {
                Height = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255))
            };

            _materialProgressFillHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                ClipToBounds = true,
                Height = 20,
                Width = 0
            };

            _materialWavePath = new Path
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                VerticalAlignment = VerticalAlignment.Center
            };

            _materialProgressFillHost.Children.Add(_materialWavePath);
            _progressTrack.Children.Add(trackLine);
            _progressTrack.Children.Add(_materialProgressFillHost);

            _progressTrack.MouseLeftButtonDown += ProgressTrack_MouseLeftButtonDown;
            _progressTrack.MouseLeftButtonUp += ProgressTrack_MouseLeftButtonUp;
            _progressTrack.MouseMove += ProgressTrack_MouseMove;
        }

        private static Geometry CreateWavePathGeometry(double width, double height)
        {
            if (width <= 1)
            {
                return Geometry.Empty;
            }

            double midY = height / 2.0;
            double amplitude = 3.0;
            double wavelength = 14.0;
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "M 0,{0:0.##}", midY);

            for (double x = 1; x <= width; x += 2)
            {
                double y = midY + amplitude * Math.Sin(x / wavelength * Math.PI * 2);
                builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, " L {0:0.##},{1:0.##}", x, y);
            }

            return Geometry.Parse(builder.ToString());
        }

        private Border CreateAlbumArtBorder(double size, double radius)
        {
            Border border = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(radius),
                Background = Brushes.Transparent,
                ClipToBounds = true
            };

            Grid artGrid = new Grid();
            Border placeholderBg = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(96, 132, 220),
                    Color.FromRgb(168, 96, 210),
                    45)
            };
            _albumArtImage = new System.Windows.Controls.Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(_albumArtImage, BitmapScalingMode.HighQuality);

            _placeholderIcon = new TextBlock
            {
                Text = "\uE8D6",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = size * 0.34,
                Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            artGrid.Children.Add(placeholderBg);
            artGrid.Children.Add(_albumArtImage);
            artGrid.Children.Add(_placeholderIcon);
            ApplyRoundedClip(artGrid, radius);
            border.Child = artGrid;
            return border;
        }

        private StackPanel CreateTitleArtistPanel(double titleSize, double artistSize)
        {
            StackPanel textPanel = new StackPanel();
            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = titleSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _artistText = new TextBlock
            {
                Text = "Reproduce musica en tu PC",
                FontSize = artistSize,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 175)),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(_titleText);
            textPanel.Children.Add(_artistText);
            return textPanel;
        }

        private void CreateProgressBar(double height, double radius)
        {
            _progressScrubber = null;
            _progressTrack = new Grid
            {
                Height = height,
                Cursor = Cursors.Hand
            };

            Border trackBg = new Border
            {
                CornerRadius = new CornerRadius(radius),
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
            };

            _progressFill = new Border
            {
                Width = 0,
                Height = height,
                CornerRadius = new CornerRadius(radius),
                Background = new SolidColorBrush(Color.FromRgb(230, 230, 235)),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            _progressTrack.Children.Add(trackBg);
            _progressTrack.Children.Add(_progressFill);
            _progressTrack.MouseLeftButtonDown += ProgressTrack_MouseLeftButtonDown;
            _progressTrack.MouseLeftButtonUp += ProgressTrack_MouseLeftButtonUp;
            _progressTrack.MouseMove += ProgressTrack_MouseMove;
        }

        private StackPanel CreateTransportControls(
            double sideFont,
            double playFont,
            double circleSize,
            double sideButtonSize,
            double playButtonSize,
            bool darkCircles)
        {
            return CreateTransportControls(sideFont, playFont, circleSize, sideButtonSize, playButtonSize, darkCircles, HorizontalAlignment.Center, false);
        }

        private StackPanel CreateTransportControls(
            double sideFont,
            double playFont,
            double circleSize,
            double sideButtonSize,
            double playButtonSize,
            bool darkCircles,
            HorizontalAlignment alignment)
        {
            return CreateTransportControls(sideFont, playFont, circleSize, sideButtonSize, playButtonSize, darkCircles, alignment, false);
        }

        private StackPanel CreateTransportControls(
            double sideFont,
            double playFont,
            double circleSize,
            double sideButtonSize,
            double playButtonSize,
            bool darkCircles,
            HorizontalAlignment alignment,
            bool boldPathIcons)
        {
            StackPanel controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (boldPathIcons)
            {
                _prevButton = CreateControlButton(TransportIconKind.Previous, null, sideFont, () => _mediaHelper.SkipPrevious(), sideButtonSize, darkCircles, true);
                _playPauseButton = CreatePlayPauseButton(playFont, playButtonSize, darkCircles, true);
                _nextButton = CreateControlButton(TransportIconKind.Next, null, sideFont, () => _mediaHelper.SkipNext(), sideButtonSize, darkCircles, true);
            }
            else
            {
                _prevButton = CreateControlButton(null, "\uE892", sideFont, () => _mediaHelper.SkipPrevious(), sideButtonSize, darkCircles, false);
                _playPauseButton = CreatePlayPauseButton(playFont, playButtonSize, darkCircles, false);
                _nextButton = CreateControlButton(null, "\uE893", sideFont, () => _mediaHelper.SkipNext(), sideButtonSize, darkCircles, false);
            }

            controls.Children.Add(_prevButton);
            controls.Children.Add(_playPauseButton);
            controls.Children.Add(_nextButton);
            return controls;
        }

        private static Geometry CreateTransportIconGeometry(TransportIconKind kind)
        {
            switch (kind)
            {
                case TransportIconKind.Play:
                    return Geometry.Parse("M 13,9 L 13,31 L 31,20 Z");
                case TransportIconKind.Pause:
                    return Geometry.Parse("M 11,9 L 18,9 L 18,31 L 11,31 Z M 22,9 L 29,9 L 29,31 L 22,31 Z");
                case TransportIconKind.Previous:
                    return Geometry.Parse("M 6,9 L 6,31 L 11,31 L 11,9 Z M 14,20 L 24,9 L 24,31 Z M 26,20 L 34,9 L 34,31 Z");
                default:
                    return Geometry.Parse("M 34,9 L 34,31 L 29,31 L 29,9 Z M 26,20 L 16,9 L 16,31 Z M 14,20 L 6,9 L 6,31 Z");
            }
        }

        private static Viewbox CreateTransportIconViewbox(TransportIconKind kind, double buttonSize, double scale)
        {
            Path path = new Path
            {
                Data = CreateTransportIconGeometry(kind),
                Fill = Brushes.White,
                Stretch = Stretch.Uniform
            };

            return new Viewbox
            {
                Width = buttonSize * scale,
                Height = buttonSize * scale,
                Child = path
            };
        }

        private Border CreateIconCircle(string glyph, double size, Thickness margin)
        {
            Border circle = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = new SolidColorBrush(Color.FromArgb(145, 22, 22, 24)),
                Margin = margin
            };
            circle.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return circle;
        }

        private Border CreateIconCircle(string glyph, double size)
        {
            return CreateIconCircle(glyph, size, new Thickness(0));
        }

        private StackPanel CreateEqualizer()
        {
            StackPanel panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };

            for (int i = 0; i < 5; i++)
            {
                Border bar = new Border
                {
                    Width = 3,
                    Height = 8 + (i % 3) * 4,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(Color.FromRgb(150, 150, 155)),
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                panel.Children.Add(bar);
            }

            return panel;
        }

        private void AnimateEqualizer()
        {
            if (_equalizerPanel == null)
            {
                return;
            }

            bool active = _currentState.HasSession && _currentState.IsPlaying;

            foreach (UIElement child in _equalizerPanel.Children)
            {
                Border bar = child as Border;
                if (bar == null)
                {
                    continue;
                }

                double height = active ? 6 + _random.Next(4, 18) : 6;
                bar.Height = height;
                bar.Background = new SolidColorBrush(active
                    ? Color.FromRgb(210, 210, 215)
                    : Color.FromRgb(120, 120, 125));
            }
        }

        private TextBlock CreateTimeLabel(string text, HorizontalAlignment alignment, double fontSize, Thickness margin)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = Brushes.White,
                HorizontalAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin
            };
        }

        private Border CreateControlButton(TransportIconKind? pathKind, string glyph, double fontSize, Action action, double size, bool darkCircle, bool boldPathIcons)
        {
            Border button = new Border
            {
                Width = size > 0 ? size : 34,
                Height = size > 0 ? size : 34,
                CornerRadius = new CornerRadius((size > 0 ? size : 34) / 2),
                Background = darkCircle
                    ? new SolidColorBrush(Color.FromArgb(145, 22, 22, 24))
                    : Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 6, 0)
            };

            if (boldPathIcons && pathKind.HasValue)
            {
                button.Child = CreateTransportIconViewbox(pathKind.Value, size > 0 ? size : 34, 0.46);
            }
            else
            {
                TextBlock icon = new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = fontSize,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                button.Child = icon;
            }

            if (!darkCircle)
            {
                button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                button.MouseLeave += (s, e) => button.Background = Brushes.Transparent;
            }

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

        private Border CreatePlayPauseButton(double fontSize, double size, bool darkCircle, bool boldPathIcons)
        {
            Border button = new Border
            {
                Width = size > 0 ? size : 38,
                Height = size > 0 ? size : 38,
                CornerRadius = new CornerRadius((size > 0 ? size : 38) / 2),
                Background = darkCircle
                    ? new SolidColorBrush(Color.FromArgb(145, 22, 22, 24))
                    : Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 6, 0)
            };

            if (boldPathIcons)
            {
                Path path = new Path
                {
                    Data = CreateTransportIconGeometry(TransportIconKind.Play),
                    Fill = Brushes.White,
                    Stretch = Stretch.Uniform
                };

                button.Tag = path;
                button.Child = new Viewbox
                {
                    Width = (size > 0 ? size : 38) * 0.52,
                    Height = (size > 0 ? size : 38) * 0.52,
                    Child = path
                };
            }
            else
            {
                TextBlock icon = new TextBlock
                {
                    Text = "\uE768",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = fontSize,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                button.Child = icon;
            }

            if (!darkCircle)
            {
                button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                button.MouseLeave += (s, e) => button.Background = Brushes.Transparent;
            }

            button.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (_mediaHelper != null)
                {
                    _mediaHelper.TogglePlayPause();
                }
            };

            return button;
        }

        private void ProgressTrack_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_mediaHelper == null || !_currentState.CanSeek)
            {
                return;
            }

            _isSeeking = true;
            _progressTrack.CaptureMouse();
            SeekFromMouse(e.GetPosition(_progressTrack).X);
        }

        private void ProgressTrack_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSeeking)
            {
                return;
            }

            SeekFromMouse(e.GetPosition(_progressTrack).X);
        }

        private void ProgressTrack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSeeking)
            {
                return;
            }

            _isSeeking = false;
            _progressTrack.ReleaseMouseCapture();
            SeekFromMouse(e.GetPosition(_progressTrack).X);
        }

        private void SeekFromMouse(double x)
        {
            if (_progressTrack.ActualWidth <= 0)
            {
                return;
            }

            double percent = x / _progressTrack.ActualWidth;
            UpdateProgressVisual(percent);

            if (_mediaHelper != null)
            {
                _mediaHelper.SeekToPercent(percent);
            }
        }

        private void MediaHelper_StateChanged(MediaState state)
        {
            if (state == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(state.Title))
            {
                _currentState.Title = state.Title;
                if (_titleText != null)
                {
                    if (_currentVariant == MusicWidgetVariant.Immersive)
                    {
                        string artistLabel = string.IsNullOrWhiteSpace(state.Artist) ? state.Title : state.Artist;
                        _titleText.Text = artistLabel;
                    }
                    else
                    {
                        _titleText.Text = state.Title;
                    }
                }
            }

            if (!string.IsNullOrEmpty(state.Artist))
            {
                _currentState.Artist = state.Artist;
                if (_artistText != null)
                {
                    if (_currentVariant == MusicWidgetVariant.Immersive)
                    {
                        _artistText.Text = "@" + BuildHandle(state.Artist);
                    }
                    else if (_currentVariant == MusicWidgetVariant.Transparent)
                    {
                        _artistText.Text = "♫ " + state.Artist;
                    }
                    else
                    {
                        _artistText.Text = state.Artist;
                    }
                }
            }
            else if (_currentVariant == MusicWidgetVariant.Immersive && _artistText != null && !string.IsNullOrEmpty(state.Title))
            {
                _artistText.Text = "@" + BuildHandle(state.Title);
            }

            UpdateAlbumArt(state);

            _currentState.HasSession = state.HasSession;
            _currentState.IsPlaying = state.IsPlaying;
            _currentState.CanPlayPause = state.CanPlayPause;
            _currentState.CanSkipNext = state.CanSkipNext;
            _currentState.CanSkipPrevious = state.CanSkipPrevious;
            _currentState.CanSeek = state.CanSeek;

            if (state.Duration > TimeSpan.Zero || state.Position > TimeSpan.Zero)
            {
                _currentState.Position = state.Position;
                _currentState.Duration = state.Duration;
                ApplyTimeline(_currentState, true);
            }
            else if ((_currentVariant == MusicWidgetVariant.Compact || _currentVariant == MusicWidgetVariant.IosPanel) && !state.HasSession)
            {
                if (_elapsedText != null) _elapsedText.Text = "-:--";
                if (_remainingText != null) _remainingText.Text = "-:--";
            }

            if (_currentVariant == MusicWidgetVariant.IosPanel || _currentVariant == MusicWidgetVariant.Transparent
                || _currentVariant == MusicWidgetVariant.SpotifyTile)
            {
                if (_artistText != null)
                {
                    _artistText.Visibility = state.HasSession ? Visibility.Visible : Visibility.Collapsed;
                }

                if (!state.HasSession && _titleText != null)
                {
                    _titleText.Text = "Sin reproduccion";
                }
            }

            UpdateControls(state);
        }

        private void UpdateAlbumArt(MediaState state)
        {
            if (state.AlbumArt != null)
            {
                _currentState.AlbumArt = state.AlbumArt;

                if (_albumArtImage != null)
                {
                    _albumArtImage.Source = state.AlbumArt;
                    _albumArtImage.Visibility = Visibility.Visible;
                }

                if (_backgroundArtImage != null)
                {
                    _backgroundArtImage.Source = state.AlbumArt;
                    _backgroundArtImage.Visibility = Visibility.Visible;
                    Border placeholder = FindPlaceholderBackground();
                    if (placeholder != null)
                    {
                        placeholder.Visibility = Visibility.Collapsed;
                    }
                }

                if (_avatarImage != null)
                {
                    _avatarImage.Source = state.AlbumArt;
                    _avatarImage.Visibility = Visibility.Visible;
                }

                if (_placeholderIcon != null)
                {
                    _placeholderIcon.Visibility = Visibility.Collapsed;
                }
            }
            else if (!state.HasSession)
            {
                _currentState.AlbumArt = null;

                if (_albumArtImage != null)
                {
                    _albumArtImage.Source = null;
                    _albumArtImage.Visibility = Visibility.Collapsed;
                }

                if (_backgroundArtImage != null)
                {
                    _backgroundArtImage.Source = null;
                    _backgroundArtImage.Visibility = Visibility.Collapsed;
                    Border placeholder = FindPlaceholderBackground();
                    if (placeholder != null)
                    {
                        placeholder.Visibility = Visibility.Visible;
                    }
                }

                if (_avatarImage != null)
                {
                    _avatarImage.Source = null;
                    _avatarImage.Visibility = Visibility.Collapsed;
                }

                if (_placeholderIcon != null)
                {
                    _placeholderIcon.Visibility = Visibility.Visible;
                }
            }
        }

        private Border FindPlaceholderBackground()
        {
            if (_cardBorder == null || _cardBorder.Child == null)
            {
                return null;
            }

            Grid root = _cardBorder.Child as Grid;
            if (root == null || root.Children.Count == 0)
            {
                return null;
            }

            return root.Children[0] as Border;
        }

        private static string BuildHandle(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist))
            {
                return "artista";
            }

            string handle = artist.ToLowerInvariant().Replace(" ", string.Empty);
            if (handle.Length > 14)
            {
                handle = handle.Substring(0, 14);
            }

            return handle;
        }

        private void ApplyTimeline(MediaState state, bool updateRemainingFromState)
        {
            if (updateRemainingFromState && _elapsedText != null && _remainingText != null)
            {
                if (state.HasSession && state.Duration > TimeSpan.Zero)
                {
                    _elapsedText.Text = FormatTime(state.Position);
                    _remainingText.Text = "-" + FormatTime(GetRemaining(state));
                }
                else if (_currentVariant == MusicWidgetVariant.Compact || _currentVariant == MusicWidgetVariant.IosPanel)
                {
                    _elapsedText.Text = "-:--";
                    _remainingText.Text = "-:--";
                }
                else
                {
                    _elapsedText.Text = "0:00";
                    _remainingText.Text = "-0:00";
                }
            }

            double percent = 0;
            if (state.Duration.TotalMilliseconds > 0)
            {
                percent = state.Position.TotalMilliseconds / state.Duration.TotalMilliseconds;
            }

            UpdateProgressVisual(percent);
        }

        private static TimeSpan GetRemaining(MediaState state)
        {
            TimeSpan remaining = state.Duration - state.Position;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        private void UpdateProgressVisual(double percent)
        {
            if (_progressTrack == null)
            {
                return;
            }

            double width = _progressTrack.ActualWidth;
            if (width <= 0)
            {
                width = this.Width - 28;
            }

            double fillWidth = Math.Max(0, Math.Min(width, width * percent));

            if (_currentVariant == MusicWidgetVariant.Material && _materialProgressFillHost != null && _materialWavePath != null)
            {
                _materialProgressFillHost.Width = fillWidth;
                _materialWavePath.Data = CreateWavePathGeometry(fillWidth, 20);
                return;
            }

            if (_progressFill == null)
            {
                return;
            }

            _progressFill.Width = fillWidth;

            if (_progressScrubber != null)
            {
                _progressScrubber.Margin = new Thickness(Math.Max(-3, fillWidth - 3.5), 0, 0, 0);
                _progressScrubber.Visibility = fillWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateControls(MediaState state)
        {
            if (_playPauseButton == null)
            {
                return;
            }

            TextBlock icon = _playPauseButton.Child as TextBlock;
            if (icon != null)
            {
                icon.Text = state.IsPlaying ? "\uE769" : "\uE768";
            }
            else
            {
                Path pathIcon = _playPauseButton.Tag as Path;
                if (pathIcon != null)
                {
                    pathIcon.Data = CreateTransportIconGeometry(state.IsPlaying ? TransportIconKind.Pause : TransportIconKind.Play);
                }
            }

            _playPauseButton.Opacity = state.CanPlayPause ? 1 : 0.35;

            if (_prevButton != null && _currentVariant != MusicWidgetVariant.Material)
            {
                _prevButton.Opacity = state.CanSkipPrevious ? 1 : 0.35;
            }

            if (_nextButton != null)
            {
                _nextButton.Opacity = state.CanSkipNext ? 1 : 0.35;
            }

            if (_progressTrack != null)
            {
                _progressTrack.Cursor = state.CanSeek ? Cursors.Hand : Cursors.Arrow;
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return string.Format("{0}:{1:D2}:{2:D2}", (int)time.TotalHours, time.Minutes, time.Seconds);
            }

            return string.Format("{0}:{1:D2}", time.Minutes, time.Seconds);
        }

        private void SetupContextMenu()
        {
            if (_cardBorder == null)
            {
                return;
            }

            ContextMenu cm = new ContextMenu();

            MenuItem itemVariants = new MenuItem { Header = "Estilo" };
            itemVariants.Items.Add(CreateVariantMenuItem("Centro de control", MusicWidgetVariant.ControlCenter));
            itemVariants.Items.Add(CreateVariantMenuItem("Inmersivo", MusicWidgetVariant.Immersive));
            itemVariants.Items.Add(CreateVariantMenuItem("Compacto", MusicWidgetVariant.Compact));
            itemVariants.Items.Add(CreateVariantMenuItem("Panel iOS", MusicWidgetVariant.IosPanel));
            itemVariants.Items.Add(CreateVariantMenuItem("Material", MusicWidgetVariant.Material));
            itemVariants.Items.Add(CreateVariantMenuItem("Sin fondo", MusicWidgetVariant.Transparent));
            itemVariants.Items.Add(CreateVariantMenuItem("Tarjeta Spotify", MusicWidgetVariant.SpotifyTile));

            MenuItem itemLock = new MenuItem { Header = "Bloquear posicion" };
            itemLock.IsCheckable = true;
            itemLock.IsChecked = _isLocked;
            itemLock.Click += (s, e) =>
            {
                _isLocked = itemLock.IsChecked;
                if (_resizeHandle != null)
                {
                    _resizeHandle.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
                }
            };

            MenuItem itemExit = new MenuItem { Header = "Cerrar widget" };
            itemExit.Click += (s, e) => this.Close();

            cm.Items.Add(itemVariants);
            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }

        private MenuItem CreateVariantMenuItem(string label, MusicWidgetVariant variant)
        {
            MenuItem item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = _currentVariant == variant
            };
            item.Click += (s, e) => ApplyVariant(variant);
            return item;
        }

        public MusicWidgetLayoutData ToLayoutData()
        {
            return new MusicWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                StyleVariant = (int)_currentVariant,
                Width = this.Width,
                Height = this.Height
            };
        }

        public void ApplyLayoutData(MusicWidgetLayoutData data)
        {
            if (data == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(data.Id))
            {
                _widgetId = data.Id;
            }

            if (Enum.IsDefined(typeof(MusicWidgetVariant), data.StyleVariant))
            {
                ApplyVariant((MusicWidgetVariant)data.StyleVariant);
            }

            this.Left = data.Left;
            this.Top = data.Top;
            _isLocked = data.IsLocked;

            if (data.Width >= _minWidth && data.Height >= _minHeight)
            {
                this.Width = ClampWidgetSize(data.Width, true);
                this.Height = ClampWidgetSize(data.Height, false);
                UpdateDesignSize(this.Width, this.Height);
            }

            if (_resizeHandle != null)
            {
                _resizeHandle.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}