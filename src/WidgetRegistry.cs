using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace WidgUI
{
    public static class WidgetRegistry
    {
        private static MainWindow _clock;
        private static EdgeMenuWindow _edgeMenu;
        private static readonly List<FolderWidgetWindow> _folderWidgets = new List<FolderWidgetWindow>();
        private static readonly List<ImageWidgetWindow> _imageWidgets = new List<ImageWidgetWindow>();
        private static readonly List<MusicWidgetWindow> _musicWidgets = new List<MusicWidgetWindow>();

        public static void Initialize(MainWindow clock, EdgeMenuWindow edgeMenu)
        {
            _clock = clock;
            _edgeMenu = edgeMenu;

            if (_clock != null)
            {
                _clock.Loaded += (s, e) => EnsureEdgeMenuOnTop();
            }
        }

        public static void EnsureEdgeMenuOnTop()
        {
            if (_edgeMenu == null)
            {
                return;
            }

            List<Window> stack = new List<Window>();

            if (_clock != null)
            {
                stack.Add(_clock);
            }

            stack.AddRange(_folderWidgets);
            stack.AddRange(_imageWidgets);
            stack.AddRange(_musicWidgets);
            stack.Add(_edgeMenu);

            DesktopManager.StackWindows(stack);
        }

        public static LayoutProfile CaptureCurrentLayout(string profileName)
        {
            LayoutProfile profile = new LayoutProfile
            {
                Name = profileName,
                Clock = _clock != null ? _clock.ToLayoutData() : new ClockLayoutData(),
                Wallpaper = _edgeMenu != null ? _edgeMenu.ToWallpaperLayoutData() : new WallpaperLayoutData(),
                FolderWidgets = _folderWidgets.Select(w => w.ToLayoutData()).ToList(),
                ImageWidgets = _imageWidgets.Select(w => w.ToLayoutData()).ToList(),
                MusicWidgets = _musicWidgets.Select(w => w.ToLayoutData()).ToList()
            };

            return profile;
        }

        public static void ApplyLayout(LayoutProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            CloseSecondaryWidgets();

            if (profile.Clock != null && _clock != null)
            {
                _clock.ApplyLayoutData(profile.Clock);
            }

            if (profile.Wallpaper != null && _edgeMenu != null)
            {
                _edgeMenu.ApplyWallpaperLayoutData(profile.Wallpaper);
            }

            if (profile.FolderWidgets != null)
            {
                foreach (FolderWidgetLayoutData data in profile.FolderWidgets)
                {
                    OpenFolderWidget(data);
                }
            }

            if (profile.ImageWidgets != null)
            {
                foreach (ImageWidgetLayoutData data in profile.ImageWidgets)
                {
                    OpenImageWidget(data);
                }
            }

            if (profile.MusicWidgets != null)
            {
                foreach (MusicWidgetLayoutData data in profile.MusicWidgets)
                {
                    OpenMusicWidget(data);
                }
            }

            EnsureEdgeMenuOnTop();
        }

        public static void LoadLastProfileIfAvailable()
        {
            string lastProfileName = ProfileService.GetLastProfileName();
            if (string.IsNullOrWhiteSpace(lastProfileName))
            {
                return;
            }

            LayoutProfile profile = ProfileService.LoadProfile(lastProfileName);
            if (profile != null)
            {
                ApplyLayout(profile);
            }
        }

        public static FolderWidgetWindow OpenFolderWidget(FolderWidgetLayoutData data = null)
        {
            FolderWidgetWindow widget = data != null
                ? new FolderWidgetWindow(data)
                : new FolderWidgetWindow();

            RegisterFolderWidget(widget);
            widget.Show();
            return widget;
        }

        public static ImageWidgetWindow OpenImageWidget(ImageWidgetLayoutData data)
        {
            if (data == null || string.IsNullOrEmpty(data.ImagePath) || !File.Exists(data.ImagePath))
            {
                return null;
            }

            ImageWidgetWindow widget = new ImageWidgetWindow(data);
            RegisterImageWidget(widget);
            widget.Show();
            return widget;
        }

        public static void OpenImageWidgetsFromPicker()
        {
            ImageWidgetWindow.CreateFromFilePicker(RegisterImageWidget);
        }

        public static MusicWidgetWindow OpenMusicWidget(MusicWidgetLayoutData data = null)
        {
            MusicWidgetWindow widget = data != null
                ? new MusicWidgetWindow(data)
                : new MusicWidgetWindow();

            RegisterMusicWidget(widget);
            widget.Show();
            return widget;
        }

        public static void RegisterFolderWidget(FolderWidgetWindow widget)
        {
            if (widget == null || _folderWidgets.Contains(widget))
            {
                return;
            }

            _folderWidgets.Add(widget);
            widget.Closed += FolderWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        public static void RegisterImageWidget(ImageWidgetWindow widget)
        {
            if (widget == null || _imageWidgets.Contains(widget))
            {
                return;
            }

            _imageWidgets.Add(widget);
            widget.Closed += ImageWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        public static void RegisterMusicWidget(MusicWidgetWindow widget)
        {
            if (widget == null || _musicWidgets.Contains(widget))
            {
                return;
            }

            _musicWidgets.Add(widget);
            widget.Closed += MusicWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        private static void Widget_Loaded_EnsureEdgeMenuOnTop(object sender, RoutedEventArgs e)
        {
            EnsureEdgeMenuOnTop();
        }

        private static void FolderWidget_Closed(object sender, EventArgs e)
        {
            FolderWidgetWindow widget = sender as FolderWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= FolderWidget_Closed;
            _folderWidgets.Remove(widget);
        }

        private static void ImageWidget_Closed(object sender, EventArgs e)
        {
            ImageWidgetWindow widget = sender as ImageWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= ImageWidget_Closed;
            _imageWidgets.Remove(widget);
        }

        private static void MusicWidget_Closed(object sender, EventArgs e)
        {
            MusicWidgetWindow widget = sender as MusicWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= MusicWidget_Closed;
            _musicWidgets.Remove(widget);
        }

        private static void CloseSecondaryWidgets()
        {
            foreach (FolderWidgetWindow widget in _folderWidgets.ToList())
            {
                widget.Closed -= FolderWidget_Closed;
                widget.Close();
            }
            _folderWidgets.Clear();

            foreach (ImageWidgetWindow widget in _imageWidgets.ToList())
            {
                widget.Closed -= ImageWidget_Closed;
                widget.Close();
            }
            _imageWidgets.Clear();

            foreach (MusicWidgetWindow widget in _musicWidgets.ToList())
            {
                widget.Closed -= MusicWidget_Closed;
                widget.Close();
            }
            _musicWidgets.Clear();
        }
    }
}
