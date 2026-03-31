using All_Messenger.Services;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace All_Messenger
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }

        // Packaged (MSIX): %LocalAppData%\Packages\{PFN}\LocalState\error.log
        // Unpackaged (exe): %LocalAppData%\AllinOneMessenger\error.log
        private static readonly string LogPath = GetLogPath();

        private static string GetLogPath()
        {
            try
            {
                var _ = Windows.ApplicationModel.Package.Current;
                return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "error.log");
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AllinOneMessenger", "error.log");
            }
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            // WinUI UI-thread exceptions
            this.UnhandledException += App_UnhandledException;

            // Non-UI thread exceptions (background threads, native interop)
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                WriteLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            // Unobserved async Task exceptions
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                WriteLog("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            WriteLog("App.UnhandledException", e.Exception);
            e.Handled = true;
        }

        private static void WriteLog(string source, Exception? ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
                File.AppendAllText(LogPath, entry);
            }
            catch { /* không làm crash app vì lỗi ghi log */ }
        }
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            // ── Init NotificationService với UI DispatcherQueue ──────────────────
            // Phải gọi sau khi MainWindow được tạo
            NotificationService.Instance.Initialize(MainWindow.DispatcherQueue);

            MainWindow.Activate();
        }
    }
}
