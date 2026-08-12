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

        public LayoutProfile()
        {
            Version = 1;
            FolderWidgets = new List<FolderWidgetLayoutData>();
            ImageWidgets = new List<ImageWidgetLayoutData>();
            MusicWidgets = new List<MusicWidgetLayoutData>();
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

        public FolderWidgetLayoutData()
        {
            Shortcuts = new List<string>();
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
    }
}
