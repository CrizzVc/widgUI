using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace WidgUI
{
    public enum WidgetThemeMode
    {
        Light = 0,
        Dark = 1
    }

    public struct WidgetAppearanceColors
    {
        public MediaColor Background;
        public MediaColor Border;
        public MediaColor Foreground;
        public MediaColor SecondaryForeground;
        public MediaColor Separator;
        public MediaColor AccentSurface;
    }

    public static class WidgetAppearanceHelper
    {
        public const double DefaultOpacity = 70.0;
        public static readonly double[] OpacityPresets = { 40, 55, 70, 85, 100 };

        private const int WallpaperThumbnailSize = 128;
        private const int MaxWallpaperCacheEntries = 8;

        private static readonly Dictionary<string, CachedWallpaperThumbnail> _wallpaperThumbnailCache =
            new Dictionary<string, CachedWallpaperThumbnail>(StringComparer.OrdinalIgnoreCase);

        private static readonly LinkedList<string> _wallpaperCacheOrder = new LinkedList<string>();

        private struct CachedWallpaperThumbnail
        {
            public long LastWriteUtcTicks;
            public BitmapSource Thumbnail;
            public int OriginalWidth;
            public int OriginalHeight;
        }

        public static WidgetAppearanceColors ComputeColors(
            WidgetThemeMode theme,
            bool adaptToBackground,
            double opacityPercent,
            string wallpaperPath,
            double left,
            double top,
            double width,
            double height)
        {
            byte alpha = ToAlpha(opacityPercent);
            MediaColor baseColor;

            if (adaptToBackground && !string.IsNullOrEmpty(wallpaperPath) && File.Exists(wallpaperPath))
            {
                baseColor = SampleWallpaperColor(wallpaperPath, left, top, width, height);
            }
            else if (theme == WidgetThemeMode.Dark)
            {
                baseColor = MediaColor.FromRgb(28, 28, 34);
            }
            else
            {
                baseColor = MediaColor.FromRgb(240, 245, 255);
            }

            bool useLightForeground = adaptToBackground
                ? GetLuminance(baseColor) < 0.45
                : theme == WidgetThemeMode.Dark;

            MediaColor foreground = useLightForeground
                ? MediaColor.FromRgb(245, 245, 250)
                : MediaColor.FromRgb(45, 55, 75);

            MediaColor secondaryForeground = useLightForeground
                ? MediaColor.FromArgb(190, 230, 230, 240)
                : MediaColor.FromArgb(190, 70, 80, 100);

            bool isDarkContext = useLightForeground;

            MediaColor borderBase = isDarkContext
                ? Darken(baseColor, 0.38)
                : Darken(baseColor, 0.14);
            byte borderAlpha = (byte)Math.Min(255, alpha + (isDarkContext ? 55 : 40));
            MediaColor border = MediaColor.FromArgb(borderAlpha, borderBase.R, borderBase.G, borderBase.B);

            MediaColor separatorBase = isDarkContext
                ? Lighten(baseColor, 0.18)
                : Darken(baseColor, 0.22);
            MediaColor separator = MediaColor.FromArgb(
                (byte)(isDarkContext ? 95 : 85),
                separatorBase.R,
                separatorBase.G,
                separatorBase.B);

            MediaColor accentBase = isDarkContext
                ? Lighten(baseColor, 0.22)
                : Lighten(baseColor, 0.06);
            MediaColor accentSurface = MediaColor.FromArgb(75, accentBase.R, accentBase.G, accentBase.B);

            return new WidgetAppearanceColors
            {
                Background = MediaColor.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B),
                Border = border,
                Foreground = foreground,
                SecondaryForeground = secondaryForeground,
                Separator = separator,
                AccentSurface = accentSurface
            };
        }

        public static byte ToAlpha(double opacityPercent)
        {
            double clamped = Math.Max(10, Math.Min(100, opacityPercent));
            return (byte)Math.Round(255.0 * (clamped / 100.0));
        }

        private static double GetLuminance(MediaColor color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static MediaColor Darken(MediaColor color, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            double factor = 1.0 - amount;
            return MediaColor.FromRgb(
                (byte)Math.Round(color.R * factor),
                (byte)Math.Round(color.G * factor),
                (byte)Math.Round(color.B * factor));
        }

        private static MediaColor Lighten(MediaColor color, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return MediaColor.FromRgb(
                (byte)Math.Round(color.R + (255 - color.R) * amount),
                (byte)Math.Round(color.G + (255 - color.G) * amount),
                (byte)Math.Round(color.B + (255 - color.B) * amount));
        }

        private static MediaColor SampleWallpaperColor(string path, double left, double top, double width, double height)
        {
            try
            {
                CachedWallpaperThumbnail cached = GetOrCreateWallpaperThumbnail(path);
                if (cached.Thumbnail == null)
                {
                    return MediaColor.FromRgb(120, 120, 130);
                }

                return SampleColorFromThumbnail(cached.Thumbnail, left, top, width, height);
            }
            catch
            {
                return MediaColor.FromRgb(120, 120, 130);
            }
        }

        private static CachedWallpaperThumbnail GetOrCreateWallpaperThumbnail(string path)
        {
            long lastWriteTicks = File.GetLastWriteTimeUtc(path).Ticks;
            CachedWallpaperThumbnail cached;
            if (_wallpaperThumbnailCache.TryGetValue(path, out cached) &&
                cached.LastWriteUtcTicks == lastWriteTicks &&
                cached.Thumbnail != null)
            {
                TouchWallpaperCacheEntry(path);
                return cached;
            }

            cached = LoadWallpaperThumbnail(path, lastWriteTicks);
            StoreWallpaperCacheEntry(path, cached);
            return cached;
        }

        private static CachedWallpaperThumbnail LoadWallpaperThumbnail(string path, long lastWriteTicks)
        {
            BitmapFrame frame = BitmapDecoder.Create(
                new Uri(path),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None).Frames[0];

            int originalWidth = frame.PixelWidth;
            int originalHeight = frame.PixelHeight;
            if (originalWidth <= 0 || originalHeight <= 0)
            {
                return new CachedWallpaperThumbnail();
            }

            BitmapImage thumbnail = new BitmapImage();
            thumbnail.BeginInit();
            thumbnail.UriSource = new Uri(path);
            thumbnail.CacheOption = BitmapCacheOption.OnLoad;
            if (originalWidth >= originalHeight)
            {
                thumbnail.DecodePixelWidth = Math.Min(WallpaperThumbnailSize, originalWidth);
            }
            else
            {
                thumbnail.DecodePixelHeight = Math.Min(WallpaperThumbnailSize, originalHeight);
            }

            thumbnail.EndInit();
            thumbnail.Freeze();

            return new CachedWallpaperThumbnail
            {
                LastWriteUtcTicks = lastWriteTicks,
                Thumbnail = thumbnail,
                OriginalWidth = originalWidth,
                OriginalHeight = originalHeight
            };
        }

        private static void StoreWallpaperCacheEntry(string path, CachedWallpaperThumbnail entry)
        {
            RemoveWallpaperCacheOrderEntry(path);
            _wallpaperThumbnailCache[path] = entry;
            _wallpaperCacheOrder.AddLast(path);

            while (_wallpaperCacheOrder.Count > MaxWallpaperCacheEntries)
            {
                string oldestPath = _wallpaperCacheOrder.First.Value;
                _wallpaperCacheOrder.RemoveFirst();
                _wallpaperThumbnailCache.Remove(oldestPath);
            }
        }

        private static void TouchWallpaperCacheEntry(string path)
        {
            RemoveWallpaperCacheOrderEntry(path);
            _wallpaperCacheOrder.AddLast(path);
        }

        private static void RemoveWallpaperCacheOrderEntry(string path)
        {
            for (LinkedListNode<string> node = _wallpaperCacheOrder.First; node != null; node = node.Next)
            {
                if (string.Equals(node.Value, path, StringComparison.OrdinalIgnoreCase))
                {
                    _wallpaperCacheOrder.Remove(node);
                    break;
                }
            }
        }

        private static MediaColor SampleColorFromThumbnail(
            BitmapSource thumbnail,
            double left,
            double top,
            double width,
            double height)
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return MediaColor.FromRgb(120, 120, 130);
            }

            int bitmapWidth = thumbnail.PixelWidth;
            int bitmapHeight = thumbnail.PixelHeight;
            if (bitmapWidth <= 0 || bitmapHeight <= 0)
            {
                return MediaColor.FromRgb(120, 120, 130);
            }

            int startX = Clamp((int)Math.Round(left / screenWidth * bitmapWidth), 0, bitmapWidth - 1);
            int startY = Clamp((int)Math.Round(top / screenHeight * bitmapHeight), 0, bitmapHeight - 1);
            int endX = Clamp(
                (int)Math.Round((left + Math.Max(width, 40)) / screenWidth * bitmapWidth),
                startX + 1,
                bitmapWidth);
            int endY = Clamp(
                (int)Math.Round((top + Math.Max(height, 40)) / screenHeight * bitmapHeight),
                startY + 1,
                bitmapHeight);

            int stride = bitmapWidth * 4;
            byte[] pixels = new byte[stride * bitmapHeight];
            thumbnail.CopyPixels(new Int32Rect(0, 0, bitmapWidth, bitmapHeight), pixels, stride, 0);

            long totalR = 0;
            long totalG = 0;
            long totalB = 0;
            long count = 0;
            int stepX = Math.Max(1, (endX - startX) / 8);
            int stepY = Math.Max(1, (endY - startY) / 8);

            for (int y = startY; y < endY; y += stepY)
            {
                for (int x = startX; x < endX; x += stepX)
                {
                    int index = y * stride + x * 4;
                    totalB += pixels[index];
                    totalG += pixels[index + 1];
                    totalR += pixels[index + 2];
                    count++;
                }
            }

            if (count == 0)
            {
                return MediaColor.FromRgb(120, 120, 130);
            }

            return MediaColor.FromRgb(
                (byte)(totalR / count),
                (byte)(totalG / count),
                (byte)(totalB / count));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
