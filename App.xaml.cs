using All_Messenger.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// Tìm hiểu thêm về WinUI, cấu trúc dự án WinUI và các template tại: http://aka.ms/winui-project-info

namespace All_Messenger
{
    /// <summary>
    /// Lớp ứng dụng chính — khởi tạo và quản lý vòng đời toàn bộ app.
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }

        // Đường dẫn file log lỗi
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
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                ShowFatalError("Lỗi khởi tạo giao diện (InitializeComponent)", ex);
                throw;
            }

            // Bắt exception trên UI thread
            this.UnhandledException += App_UnhandledException;

            try
            {
                // Đăng ký AppNotificationManager — hoạt động cả packaged lẫn unpackaged
                AppNotificationManager.Default.NotificationInvoked += (_, _) =>
                    MainWindow?.DispatcherQueue.TryEnqueue(BringWindowToFront);
                AppNotificationManager.Default.Register();
            }
            catch (Exception ex)
            {
                WriteLog("AppNotificationManager.Register", ex);
                // Không fatal — app vẫn chạy được, chỉ mất toast notification
            }

            // Bắt exception trên background thread và native interop
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                WriteLog("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            // Bắt Task bị bỏ sót không được await (unobserved async exception)
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
            catch { /* bỏ qua lỗi I/O khi ghi log, tránh làm crash app */ }
        }


        private static void BringWindowToFront()
        {
            if (MainWindow is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
            // Nếu đang bị minimize → restore; nếu đang bị che → đưa lên top
            if (IsIconic(hwnd))
                ShowWindow(hwnd, 9); // SW_RESTORE
            else
                ShowWindow(hwnd, 5); // SW_SHOW
            SetForegroundWindow(hwnd);
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(nint hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(nint hWnd);

        /// <summary>
        /// Được gọi khi ứng dụng khởi động.
        /// </summary>
        /// <param name="args">Thông tin về yêu cầu khởi động.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            if (MainWindow is not null)
            {
                // App đã chạy (VD: activated từ toast) → chỉ focus, không tạo mới
                MainWindow.Activate();
                return;
            }

            MainWindow = new MainWindow();

            // ── Init NotificationService với UI DispatcherQueue ──────────────────
            // Phải gọi sau khi MainWindow được tạo
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
            NotificationService.Instance.Initialize(MainWindow.DispatcherQueue, hwnd);

            MainWindow.Activate();
        }
    }
}
