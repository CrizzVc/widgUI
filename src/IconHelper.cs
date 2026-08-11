using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WidgUI
{
    public static class IconHelper
    {
        #region P/Invoke Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public short bmPlanes;
            public short bmBitsPixel;
            public IntPtr bmBits;
        }

        [Flags]
        private enum SIIGBF
        {
            SIIGBF_RESIZETOFIT = 0x00,
            SIIGBF_BIGGERSIZEOK = 0x01,
            SIIGBF_MEMORYONLY = 0x02,
            SIIGBF_ICONONLY = 0x04,
            SIIGBF_THUMBNAILONLY = 0x08,
            SIIGBF_INCACHEONLY = 0x10,
            SIIGBF_CROPTOSQUARE = 0x20,
            SIIGBF_WIDETHUMBNAILS = 0x40,
            SIIGBF_ICONBACKGROUND = 0x80,
            SIIGBF_SCALEUP = 0x100,
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-8a92-be33da59a9cb")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint PrivateExtractIcons(
            string szFileName, int nIconIndex, int cxIcon, int cyIcon,
            IntPtr[] phicon, IntPtr[] piconid, uint nIcons, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion

        /// <summary>
        /// Retrieves a high-quality icon for the given file path.
        /// Uses IShellItemImageFactory with proper alpha handling, 
        /// falls back to PrivateExtractIcons, then ExtractAssociatedIcon.
        /// </summary>
        public static ImageSource GetHighQualityIcon(string filePath, int size = 256)
        {
            ImageSource result = null;

            // Strategy 1: IShellItemImageFactory - best quality, handles .lnk natively
            result = TryGetIconViaShellItem(filePath, size);
            if (result != null) return result;

            // Strategy 2: PrivateExtractIcons - reliable for .exe/.dll, supports large sizes
            result = TryGetIconViaPrivateExtract(filePath, size);
            if (result != null) return result;

            // Strategy 3: Fallback to ExtractAssociatedIcon (32x32 only but always works)
            result = TryGetIconViaExtractAssociated(filePath);
            return result;
        }

        /// <summary>
        /// Uses IShellItemImageFactory.GetImage to get a high-res thumbnail/icon.
        /// Properly handles premultiplied alpha from the returned HBITMAP.
        /// </summary>
        private static ImageSource TryGetIconViaShellItem(string filePath, int size)
        {
            try
            {
                Guid iid = new Guid("bcc18b79-ba16-442f-8a92-be33da59a9cb");
                IShellItemImageFactory factory;
                SHCreateItemFromParsingName(filePath, IntPtr.Zero, iid, out factory);

                if (factory != null)
                {
                    IntPtr hbitmap;
                    // Use ICONONLY so we get the icon Windows displays, not a file thumbnail
                    int hr = factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_RESIZETOFIT | SIIGBF.SIIGBF_ICONONLY, out hbitmap);
                    if (hr == 0 && hbitmap != IntPtr.Zero)
                    {
                        BitmapSource bitmapSource = ConvertHBitmapWithAlpha(hbitmap);
                        DeleteObject(hbitmap);
                        return bitmapSource;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ShellItem icon extraction failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Converts an HBITMAP to a BitmapSource, properly preserving the alpha channel.
        /// IShellItemImageFactory returns HBITMAPs as DIB sections with premultiplied BGRA pixels.
        /// Imaging.CreateBitmapSourceFromHBitmap ignores alpha, so we read pixels manually.
        /// </summary>
        private static BitmapSource ConvertHBitmapWithAlpha(IntPtr hBitmap)
        {
            try
            {
                BITMAP bmp;
                GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), out bmp);

                if (bmp.bmBitsPixel == 32 && bmp.bmBits != IntPtr.Zero && bmp.bmWidth > 0 && bmp.bmHeight > 0)
                {
                    int stride = bmp.bmWidth * 4;
                    int totalBytes = stride * bmp.bmHeight;
                    byte[] pixels = new byte[totalBytes];
                    Marshal.Copy(bmp.bmBits, pixels, 0, totalBytes);

                    // Check if any pixel has non-zero alpha (to detect if alpha channel is real)
                    bool hasAlpha = false;
                    for (int i = 3; i < totalBytes; i += 4)
                    {
                        if (pixels[i] != 0)
                        {
                            hasAlpha = true;
                            break;
                        }
                    }

                    if (hasAlpha)
                    {
                        // DIB sections are stored bottom-up, so we need to flip vertically
                        byte[] flipped = new byte[totalBytes];
                        for (int y = 0; y < bmp.bmHeight; y++)
                        {
                            int srcOffset = y * stride;
                            int dstOffset = (bmp.bmHeight - 1 - y) * stride;
                            Array.Copy(pixels, srcOffset, flipped, dstOffset, stride);
                        }

                        var source = BitmapSource.Create(
                            bmp.bmWidth, bmp.bmHeight,
                            96, 96,
                            PixelFormats.Bgra32,
                            null,
                            flipped,
                            stride);
                        source.Freeze();
                        return source;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ConvertHBitmapWithAlpha failed: " + ex.Message);
            }

            // Fallback: no alpha detected, use standard conversion
            try
            {
                var fallback = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                fallback.Freeze();
                return fallback;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Uses PrivateExtractIcons to extract a large icon.
        /// For .lnk files, reads the shortcut's IconLocation first (the custom icon Windows displays),
        /// only falling back to the target executable if no custom icon is set.
        /// </summary>
        private static ImageSource TryGetIconViaPrivateExtract(string filePath, int size)
        {
            try
            {
                string iconPath = filePath;
                int iconIndex = 0;

                // For .lnk files, respect the shortcut's custom icon
                if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string customIconPath;
                    int customIconIndex;
                    if (GetShortcutIconInfo(filePath, out customIconPath, out customIconIndex))
                    {
                        // Shortcut has a custom icon set — use it
                        iconPath = customIconPath;
                        iconIndex = customIconIndex;
                    }
                    else
                    {
                        // No custom icon, fall back to target executable
                        string resolved = ResolveShortcutTarget(filePath);
                        if (!string.IsNullOrEmpty(resolved))
                            iconPath = resolved;
                    }
                }

                IntPtr[] phicon = new IntPtr[1];
                IntPtr[] piconid = new IntPtr[1];
                uint count = PrivateExtractIcons(iconPath, iconIndex, size, size, phicon, piconid, 1, 0);

                if (count > 0 && phicon[0] != IntPtr.Zero)
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        phicon[0], Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmapSource.Freeze();
                    DestroyIcon(phicon[0]);
                    return bitmapSource;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PrivateExtractIcons failed: " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Last resort: ExtractAssociatedIcon gives a 32x32 icon, but always works.
        /// </summary>
        private static ImageSource TryGetIconViaExtractAssociated(string filePath)
        {
            try
            {
                System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (icon != null)
                {
                    var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Reads the IconLocation property from a .lnk shortcut.
        /// Returns true if the shortcut has a valid custom icon path set.
        /// </summary>
        private static bool GetShortcutIconInfo(string shortcutPath, out string iconPath, out int iconIndex)
        {
            iconPath = null;
            iconIndex = 0;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                string iconLocation = shortcut.IconLocation;
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);

                if (!string.IsNullOrEmpty(iconLocation))
                {
                    // IconLocation format is "path,index"
                    int lastComma = iconLocation.LastIndexOf(',');
                    if (lastComma > 0)
                    {
                        string path = iconLocation.Substring(0, lastComma).Trim();
                        string indexStr = iconLocation.Substring(lastComma + 1).Trim();
                        int idx;
                        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path) && int.TryParse(indexStr, out idx))
                        {
                            iconPath = path;
                            iconIndex = idx;
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Resolves a .lnk shortcut to its target path using COM WScript.Shell.
        /// </summary>
        private static string ResolveShortcutTarget(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                string target = shortcut.TargetPath;
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
                return target;
            }
            catch
            {
                return null;
            }
        }
    }
}

