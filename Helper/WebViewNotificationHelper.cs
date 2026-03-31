using All_Messenger.Services;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace All_Messenger.Helper;

public static class WebViewNotificationHelper
{
    // ── Script inject ──────────────────────────────────────────────────────────
    /// <summary>
    /// Gọi sau EnsureCoreWebView2Async, TRƯỚC khi set Source.
    /// Intercept window.Notification API và postMessage về WinUI.
    /// </summary>
    public static async Task InjectNotificationHookAsync(CoreWebView2 webView)
    {
        const string script = """
            (function() {
                if (window.__allMessengerHooked) return;
                window.__allMessengerHooked = true;

                const _Original = window.Notification;

                function HookedNotification(title, options) {
                    try {
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'notification',
                            title: title || '',
                            body:  (options && options.body)  ? options.body  : '',
                            icon:  (options && options.icon)  ? options.icon  : ''
                        }));
                    } catch(e) {
                        console.warn('[AllMessenger] postMessage failed:', e);
                    }
                    return new _Original(title, options);
                }

                HookedNotification.prototype = _Original.prototype;

                Object.defineProperty(HookedNotification, 'permission', {
                    get: () => 'granted'
                });

                HookedNotification.requestPermission = () => Promise.resolve('granted');

                window.Notification = HookedNotification;
            })();
            """;

        await webView.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    // ── Permission handler ─────────────────────────────────────────────────────
    public static void AllowNotificationPermission(
        CoreWebView2 sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        if (args.PermissionKind == CoreWebView2PermissionKind.Notifications)
            args.State = CoreWebView2PermissionState.Allow;
    }

    // ── Message parser ─────────────────────────────────────────────────────────
    /// <summary>
    /// Gọi trong WebMessageReceived handler của từng page.
    /// Tự động forward về NotificationService nếu message đúng format.
    /// </summary>
    /// <param name="appId">VD: "Teams", "Messenger", "Zalo"</param>
    public static void HandleWebMessage(
        string appId,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string raw = e.TryGetWebMessageAsString();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp) ||
                typeProp.GetString() != "notification")
                return;

            string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            string icon = root.TryGetProperty("icon", out var i) ? i.GetString() ?? "" : "";

            NotificationService.Instance.HandleWebNotification(appId, title, body, icon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[WebViewNotificationHelper:{appId}] Parse error: {ex.Message}");
        }
    }

    // ── Session detection via URL ──────────────────────────────────────────────
    /// <summary>
    /// Hook vào NavigationCompleted để tự detect session dựa theo URL.
    /// Mỗi app có logic khác nhau — truyền vào predicate.
    /// </summary>
    public static void AttachSessionDetector(
        string appId,
        CoreWebView2 webView,
        Func<string, bool> isLoggedInUrl,
        bool resetOnFalse = true)
    {
        webView.NavigationCompleted += (sender, args) =>
        {
            bool loggedIn = isLoggedInUrl(sender.Source);

            // resetOnFalse=false: chỉ set true khi detect login, không reset khi
            // navigate sang domain khác (vd: facebook.com link preview trong Messenger)
            if (loggedIn || resetOnFalse)
                NotificationService.Instance.SetSession(appId, loggedIn);

            System.Diagnostics.Debug.WriteLine(
                $"[SessionDetector:{appId}] url={sender.Source} → loggedIn={loggedIn} (reset={resetOnFalse})");
        };
    }
}