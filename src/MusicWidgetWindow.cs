using System;
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
    public class MusicWidgetWindow : Window
    {
        private const double WidgetWidth = 380;
        private const double WidgetHeight = 162;

        private bool _isLocked;
        private bool _embeddedInDesktop = true;
        private bool _isSeeking;
        private MediaState _currentState = MediaState.Empty();

        private Border _cardBorder;
        private Border _albumArtBorder;
        private System.Windows.Controls.Image _albumArtImage;
        private TextBlock _placeholderIcon;
        private TextBlock _titleText;
        private TextBlock _artistText;
        private StackPanel _equalizerPanel;
        private Border _progressTrack;
        private Border _progressFill;
        private TextBlock _elapsedText;
        private TextBlock _remainingText;
        private Border _playPauseButton;
        private Border _prevButton;
        private Border _nextButton;

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
            BuildUI();
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
            this.Width = WidgetWidth;
            this.Height = WidgetHeight;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = screenWidth - this.Width - 50;
            this.Top = 520;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (_isLocked || IsInteractiveTarget(e.OriginalSource as DependencyObject))
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

        private void BuildUI()
        {
            _cardBorder = new Border
            {
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(Color.FromArgb(210, 34, 34, 38)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 12, 14, 12),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 6,
                    Opacity = 0.35,
                    BlurRadius = 18
                }
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid topRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _albumArtBorder = new Border
            {
                Width = 54,
                Height = 54,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 12, 0)
            };

            Grid artGrid = new Grid();
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
                FontSize = 24,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            artGrid.Children.Add(_albumArtImage);
            artGrid.Children.Add(_placeholderIcon);
            _albumArtBorder.Child = artGrid;

            StackPanel textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _titleText = new TextBlock
            {
                Text = "Sin reproduccion",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _artistText = new TextBlock
            {
                Text = "Reproduce musica en tu PC",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 175)),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(_titleText);
            textPanel.Children.Add(_artistText);

            _equalizerPanel = CreateEqualizer();

            Grid.SetColumn(_albumArtBorder, 0);
            Grid.SetColumn(textPanel, 1);
            Grid.SetColumn(_equalizerPanel, 2);
            topRow.Children.Add(_albumArtBorder);
            topRow.Children.Add(textPanel);
            topRow.Children.Add(_equalizerPanel);

            Grid progressRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            _progressTrack = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Cursor = Cursors.Hand
            };
            _progressFill = new Border
            {
                Width = 0,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _progressTrack.Child = _progressFill;
            _progressTrack.MouseLeftButtonDown += ProgressTrack_MouseLeftButtonDown;
            _progressTrack.MouseLeftButtonUp += ProgressTrack_MouseLeftButtonUp;
            _progressTrack.MouseMove += ProgressTrack_MouseMove;
            progressRow.Children.Add(_progressTrack);

            Grid timeRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            _elapsedText = CreateTimeLabel("0:00", HorizontalAlignment.Left);
            _remainingText = CreateTimeLabel("-0:00", HorizontalAlignment.Right);
            timeRow.Children.Add(_elapsedText);
            timeRow.Children.Add(_remainingText);

            Grid controlsRow = new Grid();
            StackPanel controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _prevButton = CreateControlButton("\uE892", 28, () => _mediaHelper.SkipPrevious());
            _playPauseButton = CreatePlayPauseButton();
            _nextButton = CreateControlButton("\uE893", 28, () => _mediaHelper.SkipNext());

            controls.Children.Add(_prevButton);
            controls.Children.Add(_playPauseButton);
            controls.Children.Add(_nextButton);
            controlsRow.Children.Add(controls);

            TextBlock outputIcon = new TextBlock
            {
                Text = "\uE7F5",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 175)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            controlsRow.Children.Add(outputIcon);

            Grid.SetRow(topRow, 0);
            Grid.SetRow(progressRow, 1);
            Grid.SetRow(timeRow, 2);
            Grid.SetRow(controlsRow, 3);

            root.Children.Add(topRow);
            root.Children.Add(progressRow);
            root.Children.Add(timeRow);
            root.Children.Add(controlsRow);

            _cardBorder.Child = root;
            this.Content = _cardBorder;
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
                bar.Tag = i;
                panel.Children.Add(bar);
            }

            return panel;
        }

        private void AnimateEqualizer()
        {
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

        private TextBlock CreateTimeLabel(string text, HorizontalAlignment alignment)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 175)),
                HorizontalAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 8, 0, 0)
            };
        }

        private Border CreateControlButton(string glyph, double fontSize, Action action)
        {
            Border button = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 6, 0)
            };

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

            button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            button.MouseLeave += (s, e) => button.Background = Brushes.Transparent;
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

        private Border CreatePlayPauseButton()
        {
            Border button = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 6, 0)
            };

            TextBlock icon = new TextBlock
            {
                Name = "PlayPauseIcon",
                Text = "\uE768",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Child = icon;

            button.MouseEnter += (s, e) => button.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            button.MouseLeave += (s, e) => button.Background = Brushes.Transparent;
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
                _titleText.Text = state.Title;
            }

            if (!string.IsNullOrEmpty(state.Artist))
            {
                _currentState.Artist = state.Artist;
                _artistText.Text = state.Artist;
            }

            if (state.AlbumArt != null)
            {
                _currentState.AlbumArt = state.AlbumArt;
                _albumArtImage.Source = state.AlbumArt;
                _albumArtImage.Visibility = Visibility.Visible;
                _placeholderIcon.Visibility = Visibility.Collapsed;
            }
            else if (!state.HasSession)
            {
                _currentState.AlbumArt = null;
                _albumArtImage.Source = null;
                _albumArtImage.Visibility = Visibility.Collapsed;
                _placeholderIcon.Visibility = Visibility.Visible;
            }

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

            UpdateControls(state);
        }

        private void ApplyTimeline(MediaState state, bool updateRemainingFromState)
        {
            if (updateRemainingFromState)
            {
                _elapsedText.Text = FormatTime(state.Position);
                _remainingText.Text = "-" + FormatTime(GetRemaining(state));
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
            double width = _progressTrack.ActualWidth;
            if (width <= 0)
            {
                width = WidgetWidth - 28;
            }

            _progressFill.Width = Math.Max(0, Math.Min(width, width * percent));
        }

        private void UpdateControls(MediaState state)
        {
            TextBlock icon = _playPauseButton.Child as TextBlock;
            if (icon != null)
            {
                icon.Text = state.IsPlaying ? "\uE769" : "\uE768";
            }

            _playPauseButton.Opacity = state.CanPlayPause ? 1 : 0.35;
            _prevButton.Opacity = state.CanSkipPrevious ? 1 : 0.35;
            _nextButton.Opacity = state.CanSkipNext ? 1 : 0.35;
            _progressTrack.Cursor = state.CanSeek ? Cursors.Hand : Cursors.Arrow;
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
            ContextMenu cm = new ContextMenu();

            MenuItem itemLock = new MenuItem { Header = "Bloquear posicion" };
            itemLock.IsCheckable = true;
            itemLock.IsChecked = _isLocked;
            itemLock.Click += (s, e) => { _isLocked = itemLock.IsChecked; };

            MenuItem itemExit = new MenuItem { Header = "Cerrar widget" };
            itemExit.Click += (s, e) => this.Close();

            cm.Items.Add(itemLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }

        public MusicWidgetLayoutData ToLayoutData()
        {
            return new MusicWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top
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

            this.Left = data.Left;
            this.Top = data.Top;
            _isLocked = data.IsLocked;
        }
    }
}
