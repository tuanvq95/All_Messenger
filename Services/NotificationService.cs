using All_Messenger.Helper;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
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

    // BadgeUpdateManager chỉ hoạt động với packaged app (có package identity)
    private static readonly bool _isPackaged = CheckIsPackaged();
    private static bool CheckIsPackaged()
    {
        try { _ = Windows.ApplicationModel.Package.Current; return true; }
        catch { return false; }
    }

    // ── Trạng thái ──────────────────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, bool> _sessionMap = new();
    private readonly ConcurrentDictionary<string, int> _badgeCounts = new();

    private DispatcherQueue? _dispatcherQueue;
    private nint _hwnd;
    private bool _isWindowActive = false;
    private string _activeTabAppId = string.Empty;

    // Fired on the UI thread whenever a tab's badge count changes (appId, count)
    public event Action<string, int>? TabBadgeChanged;
    private NotificationService() { }

    // ── Khởi tạo ──────────────────────────────────────────────────────────────
    public void Initialize(DispatcherQueue dispatcherQueue, nint hwnd)
    {
        _dispatcherQueue = dispatcherQueue;
        _hwnd = hwnd;
    }
    // ── Tab đang hiển thị ────────────────────────────────────────────────────
    public void SetActiveTab(string appId) => _activeTabAppId = appId;

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
        _dispatcherQueue?.TryEnqueue(() => TabBadgeChanged?.Invoke(appId, 0));
    }

    // Đặt badge tuyệt đối từ title (Hook 3) — không hiển thị toast
    public void SetBadgeDirect(string appId, int count)
    {
        if (!HasSession(appId)) return;

        // Taskbar badge: không hiển thị khi cửa sổ đang focus
        if (_isWindowActive)
            _badgeCounts[appId] = 0;
        else
            _badgeCounts[appId] = count;

        UpdateTaskbarBadge();

        // Tab nav badge: hiển thị nếu tab không phải tab đang active (bất kể window focus)
        if (appId != _activeTabAppId)
            _dispatcherQueue?.TryEnqueue(() => TabBadgeChanged?.Invoke(appId, count));
    }

    private void IncrementBadge(string appId)
    {
        _badgeCounts.AddOrUpdate(appId, 1, (_, old) => old + 1);
        UpdateTaskbarBadge();
    }

    private void UpdateTaskbarBadge()
    {
        int total = 0;
        foreach (var c in _badgeCounts.Values) total += c;

        if (_isPackaged)
        {
            void ApplyPackaged()
            {
                try
                {
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

            if (_dispatcherQueue is not null)
                _dispatcherQueue.TryEnqueue(ApplyPackaged);
            else
                ApplyPackaged();
        }
        else
        {
            // Unpackaged: dùng ITaskbarList3 SetOverlayIcon
            void ApplyOverlay()
            {
                try
                {
                    var taskbar = (ITaskbarList3)new TaskbarListInstance();
                    taskbar.HrInit();

                    nint hIcon = total > 0 ? CreateBadgeIcon(total) : nint.Zero;
                    taskbar.SetOverlayIcon(_hwnd, hIcon, total > 0 ? total.ToString() : null);

                    if (hIcon != nint.Zero)
                        DestroyIcon(hIcon);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NotificationService] Overlay icon failed: {ex.Message}");
                }
            }

            if (_dispatcherQueue is not null)
                _dispatcherQueue.TryEnqueue(ApplyOverlay);
            else
                ApplyOverlay();
        }
    }

    // ── Tạo badge icon bằng GDI+ ────────────────────────────────────────────────
    private static nint CreateBadgeIcon(int count)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.Transparent);

        // Nền tròn đỏ
        using var bgBrush = new SolidBrush(Color.FromArgb(220, 53, 53));
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // Chữ số
        string text = count > 99 ? "99+" : count.ToString();
        float fontSize = text.Length > 2 ? 9f : text.Length > 1 ? 11f : 14f;
        using var font = new Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(text, font, textBrush, new RectangleF(0, 0, size, size), sf);

        return bmp.GetHicon();
    }

    // ── ITaskbarList3 COM interop ────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint hwnd);
        void DeleteTab(nint hwnd);
        void ActivateTab(nint hwnd);
        void SetActiveAlt(nint hwnd);
        void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        void SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(nint hwnd, int tbpFlags);
        void RegisterTab(nint hwndTab, nint hwndMDI);
        void UnregisterTab(nint hwndTab);
        void SetTabOrder(nint hwndTab, nint hwndInsertBefore);
        void SetTabActive(nint hwndTab, nint hwndMDI, uint dwReserved);
        void ThumbBarAddButtons(nint hwnd, uint cButtons, nint pButton);
        void ThumbBarUpdateButtons(nint hwnd, uint cButtons, nint pButton);
        void ThumbBarSetImageList(nint hwnd, nint himl);
        void SetOverlayIcon(nint hwnd, nint hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? pszDescription);
        void SetThumbnailTooltip(nint hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? pszTip);
        void SetThumbnailClip(nint hwnd, nint prcClip);
    }

    [ComImport, Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListInstance { }

    // ── Điểm nhập thông báo ─────────────────────────────────────────────────────
    public void HandleWebNotification(string appId, string title, string body, string? icon = null)
    {
        if (!HasSession(appId)) return;
        if (_isWindowActive) return;

        if (GetNotificationMode() != NotificationModeSilent)
            ShowToast(appId, title, body, icon);
    }

    // ── Lấy tên hiển thị từ settings (cho custom server) ─────────────────────────
    private static string GetAppDisplayName(string appId)
    {
        // Các app tích hợp sẵn đã có tên đúng làm appId
        if (appId is "Teams" or "Messenger" or "Zalo")
            return appId;

        // Custom server: appId là short GUID → tra tên từ settings
        var servers = All_Messenger.Helper.AppSettings.GetCustomServers();
        var match = servers.Find(s => s.Id == appId);
        return match?.Name is { Length: > 0 } name ? name : appId;
    }

    // ── Hiển thị toast ───────────────────────────────────────────────────────────
    private void ShowToast(string appId, string title, string body, string? icon)
    {
        void Show()
        {
            try
            {
                string displayName = GetAppDisplayName(appId);
                var displayTitle = !string.IsNullOrWhiteSpace(title)
                    ? $"[{displayName}] {title}" : displayName;

                var builder = new AppNotificationBuilder()
                    .AddArgument("appId", appId)
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