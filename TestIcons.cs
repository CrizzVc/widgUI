using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace TestShellItem
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        [Flags]
        public enum SIIGBF
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
        public interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void SHCreateItemFromParsingName(
            [In][MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In][MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out][MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        static void Main(string[] args)
        {
            string path = @"C:\Windows\System32\notepad.exe";
            try
            {
                Guid iid = new Guid("bcc18b79-ba16-442f-8a92-be33da59a9cb");
                IShellItemImageFactory factory;
                SHCreateItemFromParsingName(path, IntPtr.Zero, iid, out factory);

                if (factory != null)
                {
                    IntPtr hbitmap;
                    int hr = factory.GetImage(new SIZE(256, 256), SIIGBF.SIIGBF_RESIZETOFIT | SIIGBF.SIIGBF_ICONONLY, out hbitmap);
                    if (hr == 0 && hbitmap != IntPtr.Zero)
                    {
                        Console.WriteLine("Success! HBITMAP obtained.");
                        DeleteObject(hbitmap);
                    }
                    else
                    {
                        Console.WriteLine("Failed to get image. HR: " + hr);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }
    }
}
