# All-in-One Messenger

Ứng dụng Windows tổng hợp nhiều nền tảng chat vào một cửa sổ duy nhất, xây dựng trên **WinUI 3** (Windows App SDK) và **WebView2**.

---

## Tính năng

- **Giao diện tích hợp**: Facebook Messenger, Zalo, Microsoft Teams nằm trên thanh điều hướng bên trái — chuyển tab tức thì, không cần mở nhiều trình duyệt.
- **Thêm server chat tuỳ chỉnh**: Hỗ trợ thêm bất kỳ trang web chat nào (Mattermost, Rocket.Chat, Discord, …) bằng cách nhập URL và chọn icon từ bộ Segoe MDL2 Assets.
- **Phiên đăng nhập độc lập**: Mỗi ứng dụng/server có profile WebView2 riêng (lưu tại `%LOCALAPPDATA%\AllinOneMessenger\Profiles\`) — đăng nhập một lần, lưu mãi, không ảnh hưởng lẫn nhau.
- **Thông báo hệ thống**: Hook JavaScript `window.Notification` và `ServiceWorkerRegistration.showNotification` để chuyển thông báo từ trang web thành Windows Toast Notification.
- **Badge đếm tin nhắn**: Hiển thị số tin chưa đọc trên taskbar và icon tab điều hướng.
- **Chế độ tối / sáng**: Chuyển đổi ngay trên thanh tiêu đề, lưu tự động.
- **Mica backdrop**: Hiệu ứng trong suốt Mica của Windows 11.
- **Tự động lưu cài đặt**: Toàn bộ cài đặt (theme, chế độ thông báo, danh sách server tùy chỉnh) được lưu vào `%LOCALAPPDATA%\AllMessenger\settings.json`.
- **Splash screen**: Màn hình khởi động fade-out sau khi tất cả WebView tải xong.

---

## Công nghệ sử dụng

| Thành phần | Chi tiết |
|---|---|
| UI Framework | WinUI 3 — Windows App SDK 1.8 |
| Web Engine | Microsoft WebView2 1.0.3179+ |
| Runtime | .NET 8, x64 |
| Packaging | Unpackaged (không cần MSIX) |
| Notification | Microsoft.Toolkit.Uwp.Notifications 7.1.3 |
| Installer | Inno Setup |
| Ngôn ngữ | C# |

---

## Cấu trúc dự án

```
All_Messenger/
├── App.xaml / App.xaml.cs          # Điểm khởi động ứng dụng
├── MainWindow.xaml / .cs           # Cửa sổ chính, NavigationView, title bar, theme
│
├── Pages/
│   ├── MessengerPage.xaml/.cs      # Tab Facebook Messenger
│   ├── ZaloPage.xaml/.cs           # Tab Zalo
│   ├── TeamsPage.xaml/.cs          # Tab Microsoft Teams
│   ├── CustomServerPage.cs         # Tab server tuỳ chỉnh (tạo động, không có XAML)
│   └── SettingPage.xaml/.cs        # Trang cài đặt: thông báo + quản lý server
│
├── Helper/
│   ├── WebViewPageBase.cs          # Lớp cơ sở chung cho mọi tab WebView
│   ├── WebViewProfileHelper.cs     # Quản lý CoreWebView2Environment / profile độc lập
│   ├── WebViewNotificationHelper.cs# Inject JS hook bắt Notification API
│   ├── AppSettings.cs              # Lưu/đọc cài đặt từ settings.json
│   └── CustomServerInfo.cs         # Model thông tin server tuỳ chỉnh
│
├── Services/
│   └── NotificationService.cs      # Singleton: toast, taskbar badge, trạng thái session
│
├── Assets/                         # Icon app, logo, icon Messenger/Zalo/Teams (light & dark)
└── installer/
    └── AllMessenger_Setup.iss      # Script Inno Setup đóng gói cài đặt
```

---

## Yêu cầu hệ thống

- Windows 10 (1903 / build 18362) trở lên; Windows 11 khuyến nghị (Mica backdrop)
- Kiến trúc **x64**
- WebView2 Runtime (thường đã có sẵn trên Windows 11)

---

## Build & chạy

```powershell
# Debug
dotnet build -c Debug

# Release (self-contained, không cần cài .NET trên máy người dùng)
dotnet publish -c Release -r win-x64
```

Kết quả publish nằm tại `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`.

Để đóng gói installer, mở `installer\AllMessenger_Setup.iss` bằng **Inno Setup** và chọn **Build**.

---

## Thêm server chat tuỳ chỉnh

1. Mở **Settings** (biểu tượng bánh răng cuối thanh điều hướng).
2. Nhấn **Thêm server** → điền tên hiển thị, URL và chọn icon.
3. Server mới được lưu vào `settings.json` và xuất hiện ngay trên thanh điều hướng.
4. Mỗi server có profile WebView2 riêng — cookie, session, cache độc lập hoàn toàn.

---

## Lưu trữ dữ liệu

| Dữ liệu | Đường dẫn |
|---|---|
| Cài đặt ứng dụng | `%LOCALAPPDATA%\AllMessenger\settings.json` |
| Profile WebView (cookie, cache) | `%LOCALAPPDATA%\AllinOneMessenger\Profiles\<AppId>\` |
