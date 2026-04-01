using All_Messenger.Helper;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Concurrent;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace All_Messenger.Services;

public sealed class NotificationService
{
    // ── Setting keys (dùng chung với SettingPage) ──────────────────────────────
    public const string NotificationModeKey = "NotificationMode";
    public const string NotificationModeToast = "Toast";
    public const string NotificationModeSilent = "Silent";

    // ── Singleton ──────────────────────────────────────────────────────────────
    private static readonly Lazy<NotificationService> _instance =
        new(() => new NotificationService());
    public static NotificationService Instance => _instance.Value;

    // ── Trạng thái ──────────────────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, bool> _sessionMap = new();
    private readonly ConcurrentDictionary<string, int> _badgeCounts = new();

    private DispatcherQueue? _dispatcherQueue; private bool _isWindowActive = false;
    private NotificationService() { }

    // ── Khởi tạo ──────────────────────────────────────────────────────────────
    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }
    // ── Trạng thái cửa sổ ───────────────────────────────────────────────────
    public void SetWindowActive(bool active)
    {
        _isWindowActive = active;
        if (active) ClearAllBadges();
    }

    public void ClearAllBadges()
    {
        foreach (var key in _badgeCounts.Keys)
            _badgeCounts[key] = 0;
        UpdateTaskbarBadge();
    }
    // ── Quản lý session ─────────────────────────────────────────────────
    public void SetSession(string appId, bool hasSession)
    {
        _sessionMap[appId] = hasSession;
    }

    public bool HasSession(string appId) =>
        _sessionMap.TryGetValue(appId, out bool v) && v;

    // ── Chế độ thông báo ──────────────────────────────────────────────────
    private static string GetNotificationMode()
    {
        return AppSettings.Get(NotificationModeKey) ?? NotificationModeToast;
    }

    // ── Quản lý badge ───────────────────────────────────────────────────────────
    public void ClearBadge(string appId)
    {
        _badgeCounts[appId] = 0;
        UpdateTaskbarBadge();
    }

    private void IncrementBadge(string appId)
    {
        _badgeCounts.AddOrUpdate(appId, 1, (_, old) => old + 1);
        UpdateTaskbarBadge();
    }

    private void UpdateTaskbarBadge()
    {
        try
        {
            int total = 0;
            foreach (var c in _badgeCounts.Values) total += c;

            var badgeXml = BadgeUpdateManager.GetTemplateContent(
                total > 0 ? BadgeTemplateType.BadgeNumber : BadgeTemplateType.BadgeGlyph);

            var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
            if (total > 0)
                badgeElement?.SetAttribute("value", total.ToString());
            else
                badgeElement?.SetAttribute("value", "none");

            BadgeUpdateManager.CreateBadgeUpdaterForApplication()
                              .Update(new BadgeNotification(badgeXml));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] Badge update failed: {ex.Message}");
        }
    }

    // ── Điểm nhập thông báo ─────────────────────────────────────────────────────
    public void HandleWebNotification(string appId, string title, string body, string? icon = null)
    {
        if (!HasSession(appId))
        {
            // Bỏ qua khi chưa đăng nhập — tránh hiện thông báo sai
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] Bỏ qua '{appId}' — chưa có session.");
            return;
        }

        // App đang được focus — không hiện thông báo, không tăng badge
        if (_isWindowActive) return;

        IncrementBadge(appId);

        if (GetNotificationMode() != NotificationModeSilent)
            ShowToast(appId, title, body, icon);
    }

    // ── Hiển thị toast ───────────────────────────────────────────────────────────
    private void ShowToast(string appId, string title, string body, string? icon)
    {
        void Show()
        {
            try
            {
                var displayTitle = !string.IsNullOrWhiteSpace(title)
                    ? $"[{appId}] {title}" : appId;

                var builder = new AppNotificationBuilder()
                    .AddText(displayTitle);

                if (!string.IsNullOrWhiteSpace(body))
                    builder.AddText(body);

                if (!string.IsNullOrWhiteSpace(icon) &&
                    Uri.TryCreate(icon, UriKind.Absolute, out var iconUri))
                    builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);

                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NotificationService] Toast failed: {ex.Message}");
            }
        }

        if (_dispatcherQueue is not null)
            _dispatcherQueue.TryEnqueue(Show);
        else
            Show();
    }
}