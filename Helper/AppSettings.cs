using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace All_Messenger.Helper;

/// <summary>
/// Simple file-based settings store for unpackaged apps.
/// Replaces Windows.Storage.ApplicationData.Current.LocalSettings
/// which requires MSIX package identity.
/// Settings are persisted to %LOCALAPPDATA%\AllMessenger\settings.json
/// </summary>
internal static class AppSettings
{
  private static readonly string _settingsFile = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "AllMessenger",
      "settings.json");

  private static Dictionary<string, string> _cache = Load();

  private static Dictionary<string, string> Load()
  {
    try
    {
      if (File.Exists(_settingsFile))
      {
        var json = File.ReadAllText(_settingsFile);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
      }
    }
    catch { }
    return new();
  }

  private static void Save()
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
      File.WriteAllText(_settingsFile,
          JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
    }
    catch { }
  }

  public static string? Get(string key) =>
      _cache.TryGetValue(key, out var v) ? v : null;

  public static void Set(string key, string value)
  {
    _cache[key] = value;
    Save();
  }

  // ── Custom Servers ───────────────────────────────────────────────────────────
  private const string CustomServersKey = "CustomServers";

  public static List<CustomServerInfo> GetCustomServers()
  {
    var json = Get(CustomServersKey);
    if (string.IsNullOrEmpty(json)) return new();
    try { return JsonSerializer.Deserialize<List<CustomServerInfo>>(json) ?? new(); }
    catch { return new(); }
  }

  public static void SaveCustomServers(List<CustomServerInfo> servers)
  {
    Set(CustomServersKey, JsonSerializer.Serialize(servers));
  }
}
