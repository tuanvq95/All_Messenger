using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Concurrent;

namespace All_Messenger.Services;

public sealed class NotificationService
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    private static readonly Lazy<NotificationService> _instance =
        new(() => new NotificationService());
    public static NotificationService Instance => _instance.Value;

    // ── State ──────────────────────────────────────────────────────────────────
    // appId → có session hay chưa (đã login)
    private readonly ConcurrentDictionary<string, bool> _sessionMap = new();

    private DispatcherQueue? _dispatcherQueue;

    private NotificationService() { }

    // ── Init ───────────────────────────────────────────────────────────────────
    /// <summary>
    /// Gọi 1 lần duy nhất từ MainWindow hoặc App.xaml.cs
    /// để lưu DispatcherQueue của UI thread chính.
    /// </summary>
    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    // ── Session management ─────────────────────────────────────────────────────
    /// <summary>
    /// Đánh dấu một app/page đã có session (đã login).
    /// Chỉ khi có session thì notification mới được hiển thị.
    /// </summary>
    /// <param name="appId">VD: "Teams", "Messenger", "Zalo"</param>
    public void SetSession(string appId, bool hasSession)
    {
        _sessionMap[appId] = hasSession;
    }

    public bool HasSession(string appId) =>
        _sessionMap.TryGetValue(appId, out bool v) && v;

    // ── Notification entry point ───────────────────────────────────────────────
    /// <summary>
    /// Được gọi từ bất kỳ WebView nào khi nhận WebMessage loại "notification".
    /// Tự động kiểm tra session trước khi hiển thị.
    /// </summary>
    public void HandleWebNotification(string appId, string title, string body, string? icon = null)
    {
        if (!HasSession(appId))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] Skipped '{appId}' — no session.");
            return;
        }

        ShowToast(appId, title, body, icon);
    }

    // ── Internal toast ─────────────────────────────────────────────────────────
    private void ShowToast(string appId, string title, string body, string? icon)
    {
        void Show()
        {
            try
            {
                var builder = new ToastContentBuilder()
                    .AddArgument("appId", appId)   // để handle click về đúng page
                    .AddText($"[{appId}] {title}");

                if (!string.IsNullOrWhiteSpace(body))
                    builder.AddText(body);

                if (!string.IsNullOrWhiteSpace(icon))
                    builder.AddAppLogoOverride(new Uri(icon), ToastGenericAppLogoCrop.Circle);

                builder.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] Toast failed: {ex.Message}");
            }
        }

        // Đảm bảo chạy trên UI thread
        if (_dispatcherQueue is not null)
            _dispatcherQueue.TryEnqueue(Show);
        else
            Show(); // fallback nếu chưa init (hiếm)
    }
}