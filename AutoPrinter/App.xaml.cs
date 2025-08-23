using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;


namespace AutoPrinter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "AutoPrinterAppMutex";
            bool isNewInstance;

            _mutex = new Mutex(true, mutexName, out isNewInstance);

            if (!isNewInstance)
            {
                // Find window by title and bring it to front
                IntPtr hWnd = FindWindow(null, "AutoPrinter"); // window title
                if (hWnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hWnd);
                }
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

    }

}
