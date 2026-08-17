using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WidgUI
{
    [DataContract]
    public class LayoutProfile
    {
        [DataMember(Name = "version")]
        public int Version { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "savedAt")]
        public string SavedAt { get; set; }

        [DataMember(Name = "clock")]
        public ClockLayoutData Clock { get; set; }

        [DataMember(Name = "wallpaper")]
        public WallpaperLayoutData Wallpaper { get; set; }

        [DataMember(Name = "folderWidgets")]
        public List<FolderWidgetLayoutData> FolderWidgets { get; set; }

        [DataMember(Name = "imageWidgets")]
        public List<ImageWidgetLayoutData> ImageWidgets { get; set; }

        [DataMember(Name = "musicWidgets")]
        public List<MusicWidgetLayoutData> MusicWidgets { get; set; }

        [DataMember(Name = "dockWidgets")]
        public List<DockWidgetLayoutData> DockWidgets { get; set; }

        [DataMember(Name = "customClockWidgets")]
        public List<CustomClockWidgetLayoutData> CustomClockWidgets { get; set; }

        [DataMember(Name = "appWidgets")]
        public List<AppWidgetLayoutData> AppWidgets { get; set; }

        [DataMember(Name = "expandedFolderWidgets")]
        public List<ExpandedFolderWidgetLayoutData> ExpandedFolderWidgets { get; set; }

        [DataMember(Name = "calendarWidgets")]
        public List<CalendarWidgetLayoutData> CalendarWidgets { get; set; }

        public LayoutProfile()
        {
            Version = 1;
            FolderWidgets = new List<FolderWidgetLayoutData>();
            ImageWidgets = new List<ImageWidgetLayoutData>();
            MusicWidgets = new List<MusicWidgetLayoutData>();
            DockWidgets = new List<DockWidgetLayoutData>();
            CustomClockWidgets = new List<CustomClockWidgetLayoutData>();
            AppWidgets = new List<AppWidgetLayoutData>();
            ExpandedFolderWidgets = new List<ExpandedFolderWidgetLayoutData>();
            CalendarWidgets = new List<CalendarWidgetLayoutData>();
        }
    }

    [DataContract]
    public class ClockLayoutData
    {
        [DataMember(Name = "visible")]
        public bool Visible { get; set; }

        [DataMember(Name = "styleVariant")]
        public int StyleVariant { get; set; }

        [DataMember(Name = "is24HourFormat")]
        public bool Is24HourFormat { get; set; }

        [DataMember(Name = "showAmPm")]
        public bool ShowAmPm { get; set; }

        [DataMember(Name = "showDate")]
        public bool ShowDate { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "width")]
        public double Width { get; set; }

        [DataMember(Name = "height")]
        public double Height { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }
    }

    [DataContract]
    public class WallpaperLayoutData
    {
        [DataMember(Name = "folderPath")]
        public string FolderPath { get; set; }

        [DataMember(Name = "activeWallpaperPath")]
        public string ActiveWallpaperPath { get; set; }
    }

    [DataContract]
    public class FolderWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "shortcuts")]
        public List<string> Shortcuts { get; set; }

        [DataMember(Name = "themeMode")]
        public int ThemeMode { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "opacity")]
        public double Opacity { get; set; }

        [DataMember(Name = "cornerRadius")]
        public double CornerRadius { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        [DataMember(Name = "removeWhiteBackground")]
        public bool RemoveWhiteBackground { get; set; }

        public FolderWidgetLayoutData()
        {
            Shortcuts = new List<string>();
            Opacity = WidgetAppearanceHelper.DefaultOpacity;
            CornerRadius = 30;
            RemoveWhiteBackground = true;
        }
    }

    [DataContract]
    public class ImageWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "imagePath")]
        public string ImagePath { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "width")]
        public double Width { get; set; }

        [DataMember(Name = "height")]
        public double Height { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        public ImageWidgetLayoutData()
        {
            ZIndex = -1;
        }
    }

    [DataContract]
    public class MusicWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "styleVariant")]
        public int StyleVariant { get; set; }

        [DataMember(Name = "width")]
        public double Width { get; set; }

        [DataMember(Name = "height")]
        public double Height { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }
    }

    [DataContract]
    public class DockWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "iconSize")]
        public double IconSize { get; set; }

        [DataMember(Name = "shortcuts")]
        public List<string> Shortcuts { get; set; }

        [DataMember(Name = "themeMode")]
        public int ThemeMode { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "opacity")]
        public double Opacity { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        public DockWidgetLayoutData()
        {
            Shortcuts = new List<string>();
            IconSize = 48.0;
            Opacity = WidgetAppearanceHelper.DefaultOpacity;
        }
    }

    [DataContract]
    public class CustomClockWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "fontFamily")]
        public string FontFamily { get; set; }

        [DataMember(Name = "fontSize")]
        public double FontSize { get; set; }

        [DataMember(Name = "fontStyle")]
        public string FontStyle { get; set; }

        [DataMember(Name = "fontWeight")]
        public string FontWeight { get; set; }

        [DataMember(Name = "isVertical")]
        public bool IsVertical { get; set; }

        [DataMember(Name = "showAmPm")]
        public bool ShowAmPm { get; set; }

        [DataMember(Name = "width")]
        public double Width { get; set; }

        [DataMember(Name = "height")]
        public double Height { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        public CustomClockWidgetLayoutData()
        {
            FontFamily = "Segoe UI";
            FontSize = 48.0;
            FontStyle = "Normal";
            FontWeight = "Normal";
            IsVertical = false;
            ShowAmPm = true;
            Width = 220;
            Height = 80;
        }
    }

    [DataContract]
    public class AppWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "themeMode")]
        public int ThemeMode { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "opacity")]
        public double Opacity { get; set; }

        [DataMember(Name = "showWhiteBackground")]
        public bool? ShowWhiteBackground { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        public AppWidgetLayoutData()
        {
            Opacity = WidgetAppearanceHelper.DefaultOpacity;
        }
    }

    [DataContract]
    public class ExpandedFolderWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "shortcuts")]
        public List<string> Shortcuts { get; set; }

        [DataMember(Name = "themeMode")]
        public int ThemeMode { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "opacity")]
        public double Opacity { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        [DataMember(Name = "removeWhiteBackground")]
        public bool RemoveWhiteBackground { get; set; }

        public ExpandedFolderWidgetLayoutData()
        {
            Shortcuts = new List<string>();
            Opacity = WidgetAppearanceHelper.DefaultOpacity;
            RemoveWhiteBackground = true;
        }
    }

    [DataContract]
    public class CalendarWidgetLayoutData
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "isLocked")]
        public bool IsLocked { get; set; }

        [DataMember(Name = "left")]
        public double Left { get; set; }

        [DataMember(Name = "top")]
        public double Top { get; set; }

        [DataMember(Name = "themeMode")]
        public int ThemeMode { get; set; }

        [DataMember(Name = "adaptToBackground")]
        public bool AdaptToBackground { get; set; }

        [DataMember(Name = "opacity")]
        public double Opacity { get; set; }

        [DataMember(Name = "styleVariant")]
        public int StyleVariant { get; set; }

        [DataMember(Name = "zIndex")]
        public int ZIndex { get; set; }

        public CalendarWidgetLayoutData()
        {
            Opacity = WidgetAppearanceHelper.DefaultOpacity;
        }
    }
}
