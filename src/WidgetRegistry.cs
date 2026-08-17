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
        private static readonly List<DockWidgetWindow> _dockWidgets = new List<DockWidgetWindow>();
        private static readonly List<CustomClockWidgetWindow> _customClockWidgets = new List<CustomClockWidgetWindow>();
        private static readonly List<AppWidgetWindow> _appWidgets = new List<AppWidgetWindow>();
        private static readonly List<ExpandedFolderWidgetWindow> _expandedFolderWidgets = new List<ExpandedFolderWidgetWindow>();
        private static readonly List<CalendarWidgetWindow> _calendarWidgets = new List<CalendarWidgetWindow>();
        private static Window _temporaryLayerBoostWindow;

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
            ApplyLayerStack();
        }

        public static void ApplyLayerStack()
        {
            List<Window> stack = BuildLayerStack(_temporaryLayerBoostWindow);

            if (_edgeMenu != null)
            {
                stack.Add(_edgeMenu);
            }

            DesktopManager.StackWindows(stack);

            // Bring active expanded folder overlay and expanded folder window to the very top Z-order
            foreach (ILayeredDesktopWidget w in EnumerateLayeredWidgets())
            {
                FolderWidgetWindow folder = w as FolderWidgetWindow;
                if (folder != null && folder.IsExpanded)
                {
                    if (folder.OverlayWindow != null)
                    {
                        DesktopManager.BringWindowToTop(folder.OverlayWindow);
                    }
                    DesktopManager.BringWindowToTop(folder);
                }
            }

            if (_edgeMenu != null)
            {
                DesktopManager.BringWindowToTop(_edgeMenu);
            }
        }

        public static void BeginTemporaryLayerBoost(Window window)
        {
            if (window == null || window == _edgeMenu || !IsStackableWidget(window))
            {
                return;
            }

            // Always boost expanded folder widgets or unboosted top widgets
            FolderWidgetWindow folder = window as FolderWidgetWindow;
            bool isExpandedFolder = folder != null && folder.IsExpanded;

            if (!isExpandedFolder && (_temporaryLayerBoostWindow == window || IsTopStackableWidget(window)))
            {
                return;
            }

            _temporaryLayerBoostWindow = window;
            ApplyLayerStack();
        }

        public static void EndTemporaryLayerBoost(Window window)
        {
            if (_temporaryLayerBoostWindow != window)
            {
                return;
            }

            _temporaryLayerBoostWindow = null;
            ApplyLayerStack();
        }

        private static bool IsStackableWidget(Window window)
        {
            return window is ILayeredDesktopWidget;
        }

        private static bool IsTopStackableWidget(Window window)
        {
            List<Window> sorted = GetStackableWindowsSorted();
            return sorted.Count > 0 && sorted[sorted.Count - 1] == window;
        }

        private static List<Window> BuildLayerStack(Window promotedWindow)
        {
            List<Window> stack = new List<Window>();
            List<Window> sorted = GetStackableWindowsSorted();

            // First add all non-promoted windows and non-active overlays
            foreach (Window window in sorted)
            {
                if (promotedWindow != null && window == promotedWindow)
                {
                    continue;
                }

                AppendWindowWithOverlay(stack, window);
            }

            if (promotedWindow != null)
            {
                AppendWindowWithOverlay(stack, promotedWindow);
            }

            // Ensure any active FolderWidgetWindow overlay is positioned right behind its folder, above all other widgets
            foreach (Window window in sorted)
            {
                FolderWidgetWindow folder = window as FolderWidgetWindow;
                if (folder != null && folder.OverlayWindow != null && folder.IsExpanded)
                {
                    stack.Remove(folder.OverlayWindow);
                    int folderIndex = stack.IndexOf(folder);
                    if (folderIndex >= 0)
                    {
                        stack.Insert(folderIndex, folder.OverlayWindow);
                    }
                    else
                    {
                        stack.Add(folder.OverlayWindow);
                        stack.Add(folder);
                    }
                }
            }

            return stack;
        }

        private static void AppendWindowWithOverlay(List<Window> stack, Window window)
        {
            FolderWidgetWindow folder = window as FolderWidgetWindow;
            if (folder != null && folder.OverlayWindow != null)
            {
                stack.Add(folder.OverlayWindow);
            }

            stack.Add(window);
        }

        public static void RaiseWidgetLayer(Window window)
        {
            SwapWidgetLayer(window, 1);
        }

        public static void LowerWidgetLayer(Window window)
        {
            SwapWidgetLayer(window, -1);
        }

        public static int AllocateLayerIndex()
        {
            int maxIndex = -1;

            foreach (ILayeredDesktopWidget widget in EnumerateLayeredWidgets())
            {
                if (widget.LayerIndex > maxIndex)
                {
                    maxIndex = widget.LayerIndex;
                }
            }

            return maxIndex + 1;
        }

        private static void SwapWidgetLayer(Window window, int direction)
        {
            if (!(window is ILayeredDesktopWidget))
            {
                return;
            }

            List<Window> ordered = GetStackableWindowsSorted();
            int index = ordered.IndexOf(window);
            if (index < 0)
            {
                return;
            }

            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= ordered.Count)
            {
                return;
            }

            Window temp = ordered[index];
            ordered[index] = ordered[targetIndex];
            ordered[targetIndex] = temp;

            for (int i = 0; i < ordered.Count; i++)
            {
                ((ILayeredDesktopWidget)ordered[i]).LayerIndex = i;
            }

            ApplyLayerStack();
            AutoSaveLayout();
        }

        private static IEnumerable<ILayeredDesktopWidget> EnumerateLayeredWidgets()
        {
            if (_clock != null)
            {
                yield return _clock;
            }

            foreach (FolderWidgetWindow widget in _folderWidgets)
            {
                yield return widget;
            }

            foreach (ImageWidgetWindow widget in _imageWidgets)
            {
                yield return widget;
            }

            foreach (MusicWidgetWindow widget in _musicWidgets)
            {
                yield return widget;
            }

            foreach (DockWidgetWindow widget in _dockWidgets)
            {
                yield return widget;
            }

            foreach (CustomClockWidgetWindow widget in _customClockWidgets)
            {
                yield return widget;
            }

            foreach (AppWidgetWindow widget in _appWidgets)
            {
                yield return widget;
            }

            foreach (ExpandedFolderWidgetWindow widget in _expandedFolderWidgets)
            {
                yield return widget;
            }

            foreach (CalendarWidgetWindow widget in _calendarWidgets)
            {
                yield return widget;
            }
        }

        private static List<Window> GetStackableWindowsSorted()
        {
            List<Window> windows = new List<Window>();

            if (_clock != null)
            {
                windows.Add(_clock);
            }

            windows.AddRange(_folderWidgets);
            windows.AddRange(_imageWidgets);
            windows.AddRange(_musicWidgets);
            windows.AddRange(_dockWidgets);
            windows.AddRange(_customClockWidgets);
            windows.AddRange(_appWidgets);
            windows.AddRange(_expandedFolderWidgets);
            windows.AddRange(_calendarWidgets);

            windows.Sort(CompareWidgetLayer);

            return windows;
        }

        private static int CompareWidgetLayer(Window left, Window right)
        {
            ILayeredDesktopWidget leftLayer = left as ILayeredDesktopWidget;
            ILayeredDesktopWidget rightLayer = right as ILayeredDesktopWidget;

            int leftIndex = leftLayer != null ? leftLayer.LayerIndex : 0;
            int rightIndex = rightLayer != null ? rightLayer.LayerIndex : 0;
            int compare = leftIndex.CompareTo(rightIndex);

            if (compare != 0)
            {
                return compare;
            }

            return string.Compare(left.GetHashCode().ToString(), right.GetHashCode().ToString(), StringComparison.Ordinal);
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
                MusicWidgets = _musicWidgets.Select(w => w.ToLayoutData()).ToList(),
                DockWidgets = _dockWidgets.Select(w => w.ToLayoutData()).ToList(),
                CustomClockWidgets = _customClockWidgets.Select(w => w.ToLayoutData()).ToList(),
                AppWidgets = _appWidgets.Select(w => w.ToLayoutData()).ToList(),
                ExpandedFolderWidgets = _expandedFolderWidgets.Select(w => w.ToLayoutData()).ToList(),
                CalendarWidgets = _calendarWidgets.Select(w => w.ToLayoutData()).ToList()
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

            if (profile.DockWidgets != null)
            {
                foreach (DockWidgetLayoutData data in profile.DockWidgets)
                {
                    OpenDockWidget(data);
                }
            }

            if (profile.CustomClockWidgets != null)
            {
                foreach (CustomClockWidgetLayoutData data in profile.CustomClockWidgets)
                {
                    OpenCustomClockWidget(data);
                }
            }

            if (profile.AppWidgets != null)
            {
                foreach (AppWidgetLayoutData data in profile.AppWidgets)
                {
                    OpenAppWidget(data);
                }
            }

            if (profile.ExpandedFolderWidgets != null)
            {
                foreach (ExpandedFolderWidgetLayoutData data in profile.ExpandedFolderWidgets)
                {
                    OpenExpandedFolderWidget(data);
                }
            }

            if (profile.CalendarWidgets != null)
            {
                foreach (CalendarWidgetLayoutData data in profile.CalendarWidgets)
                {
                    OpenCalendarWidget(data);
                }
            }

            EnsureEdgeMenuOnTop();
        }

        public static void LoadLastProfileIfAvailable()
        {
            string lastProfileName = ProfileService.GetLastProfileName();
            LayoutProfile profile = null;

            if (!string.IsNullOrWhiteSpace(lastProfileName))
            {
                profile = ProfileService.LoadProfile(lastProfileName);
            }

            if (profile == null)
            {
                profile = ProfileService.LoadProfile(AutoSaveProfileName);
            }

            if (profile != null)
            {
                ApplyLayout(profile);
            }
        }

        public const string AutoSaveProfileName = "__autosave";

        public static string GetActiveWallpaperPath()
        {
            return _edgeMenu != null ? _edgeMenu.ActiveWallpaperPath : null;
        }

        public static IEnumerable<Rect> GetWidgetBoundsExcept(Window exclude)
        {
            if (_clock != null && _clock != exclude && _clock.IsVisible)
            {
                yield return GetWindowBounds(_clock);
            }

            foreach (FolderWidgetWindow widget in _folderWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (ImageWidgetWindow widget in _imageWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (MusicWidgetWindow widget in _musicWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (DockWidgetWindow widget in _dockWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (CustomClockWidgetWindow widget in _customClockWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (AppWidgetWindow widget in _appWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (ExpandedFolderWidgetWindow widget in _expandedFolderWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }

            foreach (CalendarWidgetWindow widget in _calendarWidgets)
            {
                if (widget != exclude && widget.IsVisible)
                {
                    yield return GetWindowBounds(widget);
                }
            }
        }

        private static Rect GetWindowBounds(Window window)
        {
            double width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            double height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            return new Rect(window.Left, window.Top, width, height);
        }

        public static void AutoSaveLayout()
        {
            try
            {
                string profileName = ProfileService.GetLastProfileName();
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    profileName = AutoSaveProfileName;
                }

                LayoutProfile profile = CaptureCurrentLayout(profileName);
                ProfileService.SaveProfile(profile);
            }
            catch
            {
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

        public static DockWidgetWindow OpenDockWidget(DockWidgetLayoutData data = null)
        {
            DockWidgetWindow widget = data != null
                ? new DockWidgetWindow(data)
                : new DockWidgetWindow();

            RegisterDockWidget(widget);
            widget.Show();
            return widget;
        }

        public static CustomClockWidgetWindow OpenCustomClockWidget(CustomClockWidgetLayoutData data = null)
        {
            CustomClockWidgetWindow widget = data != null
                ? new CustomClockWidgetWindow(data)
                : new CustomClockWidgetWindow();

            RegisterCustomClockWidget(widget);
            widget.Show();
            return widget;
        }

        public static AppWidgetWindow OpenAppWidget(AppWidgetLayoutData data = null)
        {
            AppWidgetWindow widget = data != null
                ? new AppWidgetWindow(data)
                : new AppWidgetWindow();

            RegisterAppWidget(widget);
            widget.Show();
            return widget;
        }

        public static ExpandedFolderWidgetWindow OpenExpandedFolderWidget(ExpandedFolderWidgetLayoutData data = null)
        {
            ExpandedFolderWidgetWindow widget = data != null
                ? new ExpandedFolderWidgetWindow(data)
                : new ExpandedFolderWidgetWindow();

            RegisterExpandedFolderWidget(widget);
            widget.Show();
            return widget;
        }

        public static CalendarWidgetWindow OpenCalendarWidget(CalendarWidgetLayoutData data = null)
        {
            CalendarWidgetWindow widget = data != null
                ? new CalendarWidgetWindow(data)
                : new CalendarWidgetWindow();

            RegisterCalendarWidget(widget);
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

        public static void RegisterDockWidget(DockWidgetWindow widget)
        {
            if (widget == null || _dockWidgets.Contains(widget))
            {
                return;
            }

            _dockWidgets.Add(widget);
            widget.Closed += DockWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        public static void RegisterCustomClockWidget(CustomClockWidgetWindow widget)
        {
            if (widget == null || _customClockWidgets.Contains(widget))
            {
                return;
            }

            _customClockWidgets.Add(widget);
            widget.Closed += CustomClockWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        public static void RegisterAppWidget(AppWidgetWindow widget)
        {
            if (widget == null || _appWidgets.Contains(widget))
            {
                return;
            }

            _appWidgets.Add(widget);
            widget.Closed += AppWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        public static void RegisterExpandedFolderWidget(ExpandedFolderWidgetWindow widget)
        {
            if (widget == null || _expandedFolderWidgets.Contains(widget))
            {
                return;
            }

            _expandedFolderWidgets.Add(widget);
            widget.Closed += ExpandedFolderWidget_Closed;
            widget.Loaded += Widget_Loaded_EnsureEdgeMenuOnTop;
        }

        public static void RegisterCalendarWidget(CalendarWidgetWindow widget)
        {
            if (widget == null || _calendarWidgets.Contains(widget))
            {
                return;
            }

            _calendarWidgets.Add(widget);
            widget.Closed += CalendarWidget_Closed;
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

        private static void DockWidget_Closed(object sender, EventArgs e)
        {
            DockWidgetWindow widget = sender as DockWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= DockWidget_Closed;
            _dockWidgets.Remove(widget);
        }

        private static void CustomClockWidget_Closed(object sender, EventArgs e)
        {
            CustomClockWidgetWindow widget = sender as CustomClockWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= CustomClockWidget_Closed;
            _customClockWidgets.Remove(widget);
        }

        private static void AppWidget_Closed(object sender, EventArgs e)
        {
            AppWidgetWindow widget = sender as AppWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= AppWidget_Closed;
            _appWidgets.Remove(widget);
        }

        private static void ExpandedFolderWidget_Closed(object sender, EventArgs e)
        {
            ExpandedFolderWidgetWindow widget = sender as ExpandedFolderWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= ExpandedFolderWidget_Closed;
            _expandedFolderWidgets.Remove(widget);
        }

        private static void CalendarWidget_Closed(object sender, EventArgs e)
        {
            CalendarWidgetWindow widget = sender as CalendarWidgetWindow;
            if (widget == null)
            {
                return;
            }

            widget.Closed -= CalendarWidget_Closed;
            _calendarWidgets.Remove(widget);
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

            foreach (DockWidgetWindow widget in _dockWidgets.ToList())
            {
                widget.Closed -= DockWidget_Closed;
                widget.Close();
            }
            _dockWidgets.Clear();

            foreach (CustomClockWidgetWindow widget in _customClockWidgets.ToList())
            {
                widget.Closed -= CustomClockWidget_Closed;
                widget.Close();
            }
            _customClockWidgets.Clear();

            foreach (AppWidgetWindow widget in _appWidgets.ToList())
            {
                widget.Closed -= AppWidget_Closed;
                widget.Close();
            }
            _appWidgets.Clear();

            foreach (ExpandedFolderWidgetWindow widget in _expandedFolderWidgets.ToList())
            {
                widget.Closed -= ExpandedFolderWidget_Closed;
                widget.Close();
            }
            _expandedFolderWidgets.Clear();

            foreach (CalendarWidgetWindow widget in _calendarWidgets.ToList())
            {
                widget.Closed -= CalendarWidget_Closed;
                widget.Close();
            }
            _calendarWidgets.Clear();
        }
    }
}
