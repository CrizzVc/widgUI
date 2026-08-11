using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace WidgUI
{
    public class TrayManager : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private NotifyIcon _notifyIcon;
        private Window _targetWindow;
        private IntPtr _hIcon = IntPtr.Zero;

        public TrayManager(Window window)
        {
            _targetWindow = window;
            InitializeTray();
        }

        private void InitializeTray()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "widgUI - Reloj de Escritorio";

            try
            {
                using (Bitmap bmp = new Bitmap(32, 32))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);

                        using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(230, 139, 92, 246))) // Purple badge
                        {
                            g.FillEllipse(bgBrush, 2, 2, 28, 28);
                        }
                        using (Pen pen = new Pen(Color.White, 2))
                        {
                            g.DrawEllipse(pen, 2, 2, 28, 28);
                            g.DrawLine(pen, 16, 16, 16, 9);  // Hour hand
                            g.DrawLine(pen, 16, 16, 22, 16); // Minute hand
                        }
                    }
                    _hIcon = bmp.GetHicon();
                    _notifyIcon.Icon = Icon.FromHandle(_hIcon);
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;

            // Context Menu
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem itemShowHide = new ToolStripMenuItem("Mostrar / Ocultar Widget");
            itemShowHide.Click += (s, e) =>
            {
                if (_targetWindow.IsVisible)
                    _targetWindow.Hide();
                else
                    _targetWindow.Show();
            };

            ToolStripMenuItem itemResetPos = new ToolStripMenuItem("Restablecer Posición");
            itemResetPos.Click += (s, e) =>
            {
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                _targetWindow.Left = screenWidth - _targetWindow.Width - 40;
                _targetWindow.Top = 50;
                if (!_targetWindow.IsVisible) _targetWindow.Show();
            };

            ToolStripMenuItem itemExit = new ToolStripMenuItem("Salir");
            itemExit.Click += (s, e) =>
            {
                _notifyIcon.Visible = false;
                System.Windows.Application.Current.Shutdown();
            };

            menu.Items.Add(itemShowHide);
            menu.Items.Add(itemResetPos);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => itemShowHide.PerformClick();
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }
    }
}
