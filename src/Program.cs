using System;
using System.IO;
using System.Windows;

namespace WidgUI
{
    public class Program
    {
        private static TrayManager _trayManager;

        [STAThread]
        public static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                File.WriteAllText("error.log", e.ExceptionObject.ToString());
            };

            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (s, e) =>
                {
                    File.WriteAllText("error.log", e.Exception.ToString());
                    e.Handled = true;
                };

                MainWindow mainWindow = new MainWindow();
                EdgeMenuWindow edgeMenu = new EdgeMenuWindow(mainWindow);
                _trayManager = new TrayManager(mainWindow);

                mainWindow.Show();
                edgeMenu.Show();
                app.Run();

                if (_trayManager != null)
                {
                    _trayManager.Dispose();
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("error.log", ex.ToString());
            }
        }
    }
}
