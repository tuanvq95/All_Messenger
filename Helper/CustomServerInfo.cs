using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace All_Messenger.Helper;

/// <summary>
/// Thông tin một chat server tùy chỉnh do người dùng thêm vào.
/// </summary>
public class CustomServerInfo : INotifyPropertyChanged
{
  public event PropertyChangedEventHandler? PropertyChanged;

  private void Notify([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  private string _id = Guid.NewGuid().ToString("N")[..8];
  private string _name = string.Empty;
  private string _url = string.Empty;
  private string _iconGlyph = "\uE774"; // Globe

  /// <summary>ID duy nhất (short GUID), dùng làm tag cho NavigationViewItem và profile WebView.</summary>
  public string Id
  {
    get => _id;
    set { if (_id != value) { _id = value; Notify(); } }
  }

  /// <summary>Tên hiển thị trên menu.</summary>
  public string Name
  {
    get => _name;
    set { if (_name != value) { _name = value; Notify(); } }
  }

  /// <summary>URL trang web chat.</summary>
  public string Url
  {
    get => _url;
    set { if (_url != value) { _url = value; Notify(); } }
  }

  /// <summary>Glyph icon từ Segoe MDL2 Assets.</summary>
  public string IconGlyph
  {
    get => _iconGlyph;
    set { if (_iconGlyph != value) { _iconGlyph = value; Notify(); } }
  }
}
