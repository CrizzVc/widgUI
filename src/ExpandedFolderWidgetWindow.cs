using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WidgUI
{
    public class ExpandedFolderWidgetWindow : Window, ILayeredDesktopWidget
    {
        public const int MaxShortcuts = 8;
        private const int GridColumns = 4;
        private const int GridRows = 2;
        private const double ItemSize = 52;
        private const double ItemMargin = 5;
        private const double IconSize = 52;
        private const double IconCornerRadius = 14;
        private const double CardPadding = 14;
        private const double CardCornerRadius = 22;

        private bool _isLocked;
        private bool _embeddedInDesktop = true;
        private string _widgetId;
        private WidgetThemeMode _themeMode = WidgetThemeMode.Light;
        private bool _adaptToBackground;
        private double _opacity = WidgetAppearanceHelper.DefaultOpacity;
        private bool _removeWhiteBackground = false;
        private WidgetAppearanceColors _appearanceColors;

        private Border _cardBorder;
        private UniformGrid _iconsGrid;
        private Border _dropOverlay;
        private TextBlock _dropText;

        private class ShortcutData
        {
            public string Path { get; set; }
            public string Tooltip { get; set; }
            public ImageSource IconSource { get; set; }
        }

        private readonly List<ShortcutData> _shortcuts = new List<ShortcutData>();
        private int _layerIndex;

        public int LayerIndex
        {
            get { return _layerIndex; }
            set { _layerIndex = value; }
        }

        public ExpandedFolderWidgetWindow() : this(null)
        {
        }

        public ExpandedFolderWidgetWindow(ExpandedFolderWidgetLayoutData layoutData)
        {
            _widgetId = layoutData != null && !string.IsNullOrEmpty(layoutData.Id)
                ? layoutData.Id
                : Guid.NewGuid().ToString();

            InitializeWindow();
            BuildUI();
            SetupContextMenu();
            this.Loaded += ExpandedFolderWidgetWindow_Loaded;

            if (layoutData != null)
            {
                ApplyLayoutData(layoutData);
            }
            else
            {
                ApplyAppearance();
                _layerIndex = WidgetRegistry.AllocateLayerIndex();
            }
        }

        private void InitializeWindow()
        {
            this.Title = "widgUI - Carpeta Ampliada";
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = false;
            this.ShowInTaskbar = false;
            this.ResizeMode = ResizeMode.NoResize;
            this.Width = CalculateWidth();
            this.Height = CalculateHeight();
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 100;

            this.AllowDrop = true;
            this.DragEnter += ExpandedFolderWidgetWindow_DragEnter;
            this.DragLeave += ExpandedFolderWidgetWindow_DragLeave;
            this.Drop += ExpandedFolderWidgetWindow_Drop;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
                {
                    WidgetSnapHelper.BeginSnapDrag(this, e);
                }
            };
        }

        private static double CalculateWidth()
        {
            return CardPadding * 2 + GridColumns * (ItemSize + ItemMargin * 2);
        }

        private static double CalculateHeight()
        {
            return CardPadding * 2 + GridRows * (ItemSize + ItemMargin * 2);
        }

        private void ExpandedFolderWidgetWindow_Loaded(object sender, RoutedEventArgs e)
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
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(CardCornerRadius),
                Padding = new Thickness(CardPadding)
            };

            Grid mainGrid = new Grid();
            _iconsGrid = new UniformGrid
            {
                Columns = GridColumns,
                Rows = GridRows,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainGrid.Children.Add(_iconsGrid);

            _dropOverlay = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(CardCornerRadius),
                Visibility = Visibility.Collapsed
            };
            _dropText = new TextBlock
            {
                Text = "Suelta aquí",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _dropOverlay.Child = _dropText;
            mainGrid.Children.Add(_dropOverlay);

            _cardBorder.Child = mainGrid;
            this.Content = _cardBorder;
            ApplyAppearance();
            RenderShortcuts();
        }

        private void ExpandedFolderWidgetWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && _shortcuts.Count < MaxShortcuts)
            {
                e.Effects = DragDropEffects.Copy;
                _dropOverlay.Visibility = Visibility.Visible;
                _iconsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void ExpandedFolderWidgetWindow_DragLeave(object sender, DragEventArgs e)
        {
            _dropOverlay.Visibility = Visibility.Collapsed;
            _iconsGrid.Visibility = Visibility.Visible;
        }

        private void ExpandedFolderWidgetWindow_Drop(object sender, DragEventArgs e)
        {
            _dropOverlay.Visibility = Visibility.Collapsed;
            _iconsGrid.Visibility = Visibility.Visible;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                if (_shortcuts.Count >= MaxShortcuts)
                {
                    break;
                }

                AddShortcutData(file);
            }

            RenderShortcuts();
            NotifyLayoutChanged();
        }

        private void AddShortcutData(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || _shortcuts.Count >= MaxShortcuts)
            {
                return;
            }

            foreach (ShortcutData existing in _shortcuts)
            {
                if (string.Equals(existing.Path, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            try
            {
                _shortcuts.Add(new ShortcutData
                {
                    Path = filePath,
                    Tooltip = System.IO.Path.GetFileNameWithoutExtension(filePath),
                    IconSource = IconHelper.GetHighQualityIcon(filePath)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error adding shortcut: " + ex.Message);
            }
        }

        private void RenderShortcuts()
        {
            _iconsGrid.Children.Clear();

            for (int i = 0; i < _shortcuts.Count; i++)
            {
                ShortcutData shortcut = _shortcuts[i];
                int index = i;
                UIElement tile = CreateAppShortcut(
                    shortcut.IconSource,
                    shortcut.Tooltip,
                    shortcut.Path,
                    index);

                _iconsGrid.Children.Add(tile);
            }
        }

        private UIElement CreateAppShortcut(ImageSource imageSource, string tooltip, string path, int index)
        {
            Border appBorder = new Border
            {
                Width = ItemSize,
                Height = ItemSize,
                Background = _removeWhiteBackground ? Brushes.Transparent : Brushes.White,
                CornerRadius = new CornerRadius(IconCornerRadius),
                Margin = new Thickness(ItemMargin),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Effect = _removeWhiteBackground ? null : new DropShadowEffect
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
                Image img = new Image
                {
                    Source = imageSource,
                    Stretch = Stretch.Uniform,
                    Width = IconSize,
                    Height = IconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Clip = new RectangleGeometry(new Rect(0, 0, IconSize, IconSize), IconCornerRadius, IconCornerRadius)
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                containerGrid.Children.Add(img);
            }
            else
            {
                containerGrid.Children.Add(new TextBlock
                {
                    Text = "?",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Border darkenOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                CornerRadius = new CornerRadius(IconCornerRadius),
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
            appBorder.MouseLeftButtonUp += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
            };

            ContextMenu itemMenu = new ContextMenu();
            MenuItem openItem = new MenuItem { Header = "Abrir" };
            openItem.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
                catch { }
            };
            MenuItem removeItem = new MenuItem { Header = "Eliminar de la carpeta" };
            removeItem.Click += (s, e) =>
            {
                if (index >= 0 && index < _shortcuts.Count)
                {
                    _shortcuts.RemoveAt(index);
                    RenderShortcuts();
                    NotifyLayoutChanged();
                }
            };
            itemMenu.Items.Add(openItem);
            itemMenu.Items.Add(removeItem);
            appBorder.ContextMenu = itemMenu;

            return appBorder;
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
                Math.Max(this.ActualWidth > 0 ? this.ActualWidth : this.Width, CalculateWidth()),
                Math.Max(this.ActualHeight > 0 ? this.ActualHeight : this.Height, CalculateHeight()));
        }

        private void ApplyAppearance()
        {
            RefreshAppearanceColors();

            if (_cardBorder != null)
            {
                _cardBorder.Background = new SolidColorBrush(_appearanceColors.Background);
                _cardBorder.BorderBrush = new SolidColorBrush(_appearanceColors.Border);
            }

            if (_dropText != null)
            {
                _dropText.Foreground = new SolidColorBrush(_appearanceColors.Foreground);
            }
        }

        private void SetupContextMenu()
        {
            ContextMenu cm = new ContextMenu();
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
                    SetupContextMenu();
                    NotifyLayoutChanged();
                };
                opacityMenu.Items.Add(opacityItem);
            }

            MenuItem removeWhiteBgItem = new MenuItem
            {
                Header = "Quitar fondo blanco en elementos",
                IsCheckable = true,
                IsChecked = _removeWhiteBackground
            };
            removeWhiteBgItem.Click += (s, e) =>
            {
                _removeWhiteBackground = removeWhiteBgItem.IsChecked;
                RenderShortcuts();
                SetupContextMenu();
                NotifyLayoutChanged();
            };

            appearanceMenu.Items.Add(lightItem);
            appearanceMenu.Items.Add(darkItem);
            appearanceMenu.Items.Add(adaptItem);
            appearanceMenu.Items.Add(opacityMenu);
            appearanceMenu.Items.Add(removeWhiteBgItem);
            cm.Items.Add(appearanceMenu);
            cm.Items.Add(new Separator());

            MenuItem itemLock = new MenuItem { Header = "Bloquear Posición", IsCheckable = true, IsChecked = _isLocked };
            itemLock.Click += (s, e) =>
            {
                _isLocked = itemLock.IsChecked;
                NotifyLayoutChanged();
            };

            MenuItem itemExit = new MenuItem { Header = "Cerrar Widget" };
            itemExit.Click += (s, e) => this.Close();

            cm.Items.Add(itemLock);
            WidgetLayerHelper.AppendLayerMenuItems(cm, this);
            cm.Items.Add(itemExit);

            _cardBorder.ContextMenu = cm;
        }

        private void NotifyLayoutChanged()
        {
            WidgetRegistry.AutoSaveLayout();
        }

        public ExpandedFolderWidgetLayoutData ToLayoutData()
        {
            ExpandedFolderWidgetLayoutData data = new ExpandedFolderWidgetLayoutData
            {
                Id = _widgetId,
                IsLocked = _isLocked,
                Left = this.Left,
                Top = this.Top,
                ThemeMode = (int)_themeMode,
                AdaptToBackground = _adaptToBackground,
                Opacity = _opacity,
                RemoveWhiteBackground = _removeWhiteBackground,
                ZIndex = _layerIndex
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

        public void ApplyLayoutData(ExpandedFolderWidgetLayoutData data)
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

            if (Enum.IsDefined(typeof(WidgetThemeMode), data.ThemeMode))
            {
                _themeMode = (WidgetThemeMode)data.ThemeMode;
            }

            _adaptToBackground = data.AdaptToBackground;
            if (data.Opacity > 0)
            {
                _opacity = data.Opacity;
            }

            _removeWhiteBackground = data.RemoveWhiteBackground;
            _layerIndex = data.ZIndex;

            _shortcuts.Clear();
            if (data.Shortcuts != null)
            {
                foreach (string path in data.Shortcuts)
                {
                    if (_shortcuts.Count >= MaxShortcuts)
                    {
                        break;
                    }

                    if (File.Exists(path) || path == "explorer.exe")
                    {
                        AddShortcutData(path);
                    }
                }
            }

            ApplyAppearance();
            RenderShortcuts();
            SetupContextMenu();
        }
    }
}
