using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using Windows.Foundation;
using Windows.UI.WebUI;

namespace All_Messenger.Helper;

public abstract class WebViewPageBase : Page
{
    private bool _isReady = false;
    public bool IsReady => _isReady;

    public abstract WebView2 WebView { get; }
    public abstract string AppId { get; }
    public abstract Uri StartUri { get; }

    private TypedEventHandler<object, WindowVisibilityChangedEventArgs>? _visibilityHandler;

    protected WebViewPageBase()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected async void InitWebView()
    {
        var env = await WebViewProfileHelper.GetOrCreateAsync(AppId);
        await WebView.EnsureCoreWebView2Async(env);

        var core = WebView.CoreWebView2;
        // TODO: Debug
        //if (AppId == "Teams")
        //{
        //    core.OpenDevToolsWindow();
        //}

        ConfigureWebView(core);

        core.PermissionRequested += (s, a) =>
            WebViewNotificationHelper.AllowNotificationPermission(s, a);

        core.WebMessageReceived += (s, e) =>
            WebViewNotificationHelper.HandleWebMessage(AppId, e);

        await WebViewNotificationHelper.InjectNotificationHookAsync(core);

        // Hook cho page-specific setup (session detector, v.v.)
        OnCoreWebView2Ready(core);

        // Apply app theme vào WebView ngay khi khởi tạo xong
        ApplyColorSchemeFromCurrentTheme();

        core.NavigationCompleted += (s, e) =>
        {
            // Đánh dấu WebView đã sẵn sàng sau lần đầu tiên hoàn thành navigation (dù success hay error)
            if (!_isReady) _isReady = true;
        };

        WebView.Source = StartUri;
    }

    // Override để thêm logic riêng từng trang (ví dụ: session detector, cấu hình đặc biệt)
    protected virtual void OnCoreWebView2Ready(CoreWebView2 core) { }

    private static void ConfigureWebView(CoreWebView2 core)
    {
        // Tắt các tính năng trình duyệt không cần thiết để giảm nhiễu và tối ưu hiệu năng
        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
    }

    // ── Đồng bộ theme ──────────────────────────────────────────────────────────────
    private void ApplyColorSchemeFromCurrentTheme()
    {
        if (WebView.CoreWebView2 is null) return;
        WebView.CoreWebView2.Profile.PreferredColorScheme = ActualTheme == ElementTheme.Dark
            ? CoreWebView2PreferredColorScheme.Dark
            : CoreWebView2PreferredColorScheme.Light;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyColorSchemeFromCurrentTheme();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ActualThemeChanged += OnActualThemeChanged;

        if (App.MainWindow is null) return;

        try
        {
            _visibilityHandler = (s, args) =>
            {
                // Không suspend WebView khi ẩn cửa sổ — WebView phải tiếp tục
                // chạy JS để nhận push notification qua hook và postMessage.
                // Nếu suspend, các JS hook sẽ dừng → không có toast khi app bị ẩn.
            };

            App.MainWindow.VisibilityChanged += _visibilityHandler;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[OnLoaded] throw Exception: {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ActualThemeChanged -= OnActualThemeChanged;

        if (App.MainWindow is not null && _visibilityHandler is not null)
            App.MainWindow.VisibilityChanged -= _visibilityHandler;
    }
}