using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
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

    // ── State ──────────────────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, bool> _sessionMap = new();
    private readonly ConcurrentDictionary<string, int> _badgeCounts = new();

    private DispatcherQueue? _dispatcherQueue;

    private NotificationService() { }

    // ── Init ───────────────────────────────────────────────────────────────────
    public void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    // ── Session management ─────────────────────────────────────────────────────
    public void SetSession(string appId, bool hasSession)
    {
        _sessionMap[appId] = hasSession;
    }

    public bool HasSession(string appId) =>
        _sessionMap.TryGetValue(appId, out bool v) && v;

    // ── Notification mode ──────────────────────────────────────────────────────
    private static string GetNotificationMode()
    {
        var values = Windows.Storage.ApplicationData.Current.LocalSettings.Values;
        return values.TryGetValue(NotificationModeKey, out var val) && val is string s
            ? s : NotificationModeToast;
    }

    // ── Badge management ───────────────────────────────────────────────────────
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

    // ── Sound (P/Invoke winmm) ─────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);
    private const uint MB_ICONASTERISK = 0x00000040; // "Asterisk" system sound

    // ── Notification entry point ───────────────────────────────────────────────
    public void HandleWebNotification(string appId, string title, string body, string? icon = null)
    {
        if (!HasSession(appId))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] Skipped '{appId}' — no session.");
            return;
        }

        if (GetNotificationMode() == NotificationModeSilent)
        {
            IncrementBadge(appId);
            MessageBeep(MB_ICONASTERISK);
        }
        else
        {
            ShowToast(appId, title, body, icon);
        }
    }

    // ── Internal toast ─────────────────────────────────────────────────────────
    private void ShowToast(string appId, string title, string body, string? icon)
    {
        void Show()
        {
            try
            {
                var builder = new ToastContentBuilder()
                    .AddArgument("appId", appId)
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

        if (_dispatcherQueue is not null)
            _dispatcherQueue.TryEnqueue(Show);
        else
            Show();
    }
}