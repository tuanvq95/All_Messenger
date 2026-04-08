using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace All_Messenger.Helper;

public static class WebViewProfileHelper
{
    // Cache environment theo tên profile — tránh tạo lại mỗi lần navigate
    private static readonly ConcurrentDictionary<string, CoreWebView2Environment> _cache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    // Thư mục gốc chứa tất cả profile
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllinOneMessenger",
        "Profiles"
    );

    /// <summary>
    /// Lấy hoặc tạo mới CoreWebView2Environment cho profile đã chỉ định.
    /// Mỗi profile có 1 thư mục userData riêng — đảm bảo session độc lập giữa các ứng dụng.
    /// </summary>
    /// <param name="profileName">Tên profile, ví dụ: "Teams", "Messenger", "Zalo"</param>
    public static async Task<CoreWebView2Environment> GetOrCreateAsync(string profileName)
    {
        // Trả ngay nếu đã có trong cache
        if (_cache.TryGetValue(profileName, out var cached))
            return cached;

        // Dùng lock riêng từng profile — tránh tạo 2 Environment song song cho cùng một profile
        var sem = _locks.GetOrAdd(profileName, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_cache.TryGetValue(profileName, out cached))
                return cached;

            string profilePath = Path.Combine(BasePath, profileName);
            Directory.CreateDirectory(profilePath);

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = string.Join(" ",
                [
                    "--disable-features=msSmartScreen",
                    // NOTE: --disable-renderer-backgrounding bị bỏ vì xung đột với TrySuspendAsync
                    "--disable-background-networking",
                    "--disable-background-timer-throttling",
                    "--disable-backgrounding-occluded-windows",
                    "--disable-sync",
                    "--disable-translate",
                    "--disable-default-apps",
                    "--no-first-run",
                    "--autoplay-policy=no-user-gesture-required",
                    // Dùng password store đơn giản, tránh gọi DPAPI/Windows Credential Manager
                    // → ngăn app bị treo ~5s khi WebView2 lưu password lần đầu
                    "--password-store=basic",
                ])
            };

            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                null,
                profilePath,
                options
            );

            _cache[profileName] = env;
            return env;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Xóa cache của một profile khỏi bộ nhớ — dùng khi muốn reset session (ví dụ sau khi user đăng xuất).
    /// </summary>
    public static void InvalidateProfile(string profileName)
    {
        _cache.TryRemove(profileName, out _);
    }

    /// <summary>
    /// Xoá toàn bộ dữ liệu (cookies, cache, localStorage) của 1 profile trên disk.
    /// Gọi khi user muốn logout hoàn toàn.
    /// </summary>
    public static void DeleteProfileData(string profileName)
    {
        InvalidateProfile(profileName);

        string profilePath = Path.Combine(BasePath, profileName);
        if (Directory.Exists(profilePath))
        {
            try { Directory.Delete(profilePath, recursive: true); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WebViewProfileHelper] Cannot delete '{profileName}': {ex.Message}");
            }
        }
    }
}