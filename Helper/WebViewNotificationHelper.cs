using All_Messenger.Services;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace All_Messenger.Helper;

public static class WebViewNotificationHelper
{
    // ── Inject script ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Gọi sau EnsureCoreWebView2Async, TRƯỜC khi set Source.
    /// Chặn bắt window.Notification API và gửi message về WinUI.
    /// </summary>
    public static async Task InjectNotificationHookAsync(CoreWebView2 webView)
    {
        const string script = """
            (function() {
                if (window.__allMessengerHooked) return;
                window.__allMessengerHooked = true;

                function postNotification(title, body, icon) {
                    try {
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'notification',
                            title: title || '',
                            body:  body  || '',
                            icon:  icon  || ''
                        }));
                    } catch(e) {
                        console.warn('[AllMessenger] postMessage failed:', e);
                    }
                }

                // ── Hook 1: window.Notification constructor ──────────────────────────────
                // Bắt Messenger & Zalo (dùng new Notification() từ page context).
                const _OriginalNotification = window.Notification;

                function HookedNotification(title, options) {
                    postNotification(
                        title,
                        options && options.body ? options.body : '',
                        options && options.icon ? options.icon : ''
                    );
                    return new _OriginalNotification(title, options);
                }

                HookedNotification.prototype = _OriginalNotification.prototype;
                Object.defineProperty(HookedNotification, 'permission', { get: () => 'granted' });
                HookedNotification.requestPermission = () => Promise.resolve('granted');
                window.Notification = HookedNotification;

                // ── Hook 2: ServiceWorkerRegistration.showNotification ───────────────────────────────────
                // Bắt thông báo của Teams & các app gọi showNotification() từ page/worker context.
                // (Service Worker chạy trong thread riêng – không thể hook trực tiếp,
                //  nhưng nhiều app vẫn gọi qua registration object trong page context.)
                if (window.ServiceWorkerRegistration &&
                    ServiceWorkerRegistration.prototype.showNotification) {
                    const _origShow = ServiceWorkerRegistration.prototype.showNotification;
                    ServiceWorkerRegistration.prototype.showNotification = function(title, options) {
                        postNotification(
                            title,
                            options && options.body ? options.body : '',
                            options && options.icon ? options.icon : ''
                        );
                        return _origShow.call(this, title, options);
                    };
                }

                // ── Hook 3: Theo dõi badge số trên document.title ────────────────────────────────────
                // Fallback cho Teams (thường không dùng new Notification() khi app đang mở):
                // title thay đổi thành "(N) Microsoft Teams", "(N) Messenger", "(N) Zalo".
                // Chỉ fire khi count TĂNG so với lần cuối → tránh noise lúc load lần đầu.
                let _prevCount = -1;

                function onTitleChange() {
                    const match = document.title.match(/^\((\d+)\)/);
                    const count = match ? parseInt(match[1], 10) : 0;
                    if (_prevCount === -1) {           // lần đầu: ghi nhớ giá trị ban đầu, không phát thông báo
                        _prevCount = count;
                        return;
                    }
                    if (count > _prevCount) {
                        postNotification('New messages', '', '');
                    }
                    _prevCount = count;
                }

                function attachTitleObserver() {
                    const titleEl = document.querySelector('title');
                    if (!titleEl) return false;
                    new MutationObserver(onTitleChange).observe(titleEl, { childList: true, characterData: true, subtree: true });
                    return true;
                }

                if (!attachTitleObserver()) {
                    // <title> chưa tồn tại (SPA thêm vào sau) – đợi đến khi <head> thay đổi
                    const headObserver = new MutationObserver(() => {
                        if (attachTitleObserver()) headObserver.disconnect();
                    });
                    headObserver.observe(document.head || document.documentElement, { childList: true });
                }
            })();
            """;

        await webView.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    // ── Xử lý quyền thông báo ─────────────────────────────────────────────────────
    // Tự động cấp quyền thông báo khi trang web yêu cầu
    public static void AllowNotificationPermission(
        CoreWebView2 sender,
        CoreWebView2PermissionRequestedEventArgs args)
    {
        if (args.PermissionKind == CoreWebView2PermissionKind.Notifications)
            args.State = CoreWebView2PermissionState.Allow;
    }

    // ── Xử lý message từ WebView ─────────────────────────────────────────────────────────
    /// <summary>
    /// Gọi trong WebMessageReceived handler của từng trang.
    /// Tự động chuyển tiếp sang NotificationService nếu message đúng định dạng.
    /// </summary>
    /// <param name="appId">Ví dụ: "Teams", "Messenger", "Zalo"</param>
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

    // ── Theo dõi session qua URL ──────────────────────────────────────────────────────
    /// <summary>
    /// Hook vào NavigationCompleted để tự phát hiện trạng thái đăng nhập dựa theo URL.
    /// Mỗi app có logic URL khác nhau — truyền vào qua predicate.
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

            // resetOnFalse=false: chỉ set true khi phát hiện đăng nhập, không reset khi
            // navigate sang domain khác (ví dụ: facebook.com link preview trong Messenger)
            if (loggedIn || resetOnFalse)
                NotificationService.Instance.SetSession(appId, loggedIn);

            System.Diagnostics.Debug.WriteLine(
                $"[SessionDetector:{appId}] url={sender.Source} → loggedIn={loggedIn} (reset={resetOnFalse})");
        };
    }
}