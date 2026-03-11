using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using Windows.Foundation;

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
            if (e.IsSuccess) _isReady = true;
        };

        WebView.Source = StartUri;
    }

    // Override để thêm logic riêng (session detector, v.v.)
    protected virtual void OnCoreWebView2Ready(CoreWebView2 core) { }

    private static void ConfigureWebView(CoreWebView2 core)
    {
        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
    }

    // ── Theme sync ─────────────────────────────────────────────────────────────
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
            _visibilityHandler = async (s, args) =>
            {
                try
                {
                    var core = WebView.CoreWebView2;
                    if (core == null || !_isReady) return;

                    if (!args.Visible)
                    {
                        if (!core.IsSuspended)
                            await core.TrySuspendAsync();
                    }
                    else
                    {
                        if (core.IsSuspended)
                            core.Resume();
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // Non-critical: WebView2 may be in a transient invalid state.
                }
            };

            App.MainWindow.VisibilityChanged += _visibilityHandler;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[OnLoaded] thow Exception: {ex.Message}");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ActualThemeChanged -= OnActualThemeChanged;

        if (App.MainWindow is not null && _visibilityHandler is not null)
            App.MainWindow.VisibilityChanged -= _visibilityHandler;
    }
}