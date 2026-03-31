; ============================================================
;  All-in-One Messenger — Inno Setup Script
;  Yêu cầu: Inno Setup 6.x  https://jrsoftware.org/isinfo.php
;  Build app trước:
;    dotnet publish -c Release -r win-x64 --self-contained true
;  Sau đó compile file .iss này bằng Inno Setup Compiler
; ============================================================

#define AppName      "All-in-One Messenger"
#define AppVersion   "1.0.0.0"
#define AppPublisher "MrRom"
#define AppExeName   "All_Messenger.exe"
#define AppIcon      "..\Assets\icon.ico"
; Đường dẫn tới thư mục publish (tương đối từ vị trí file .iss này)
#define PublishDir   "..\bin\win-x64\publish"

[Setup]
AppId={{9172cfc1-06cb-4d83-9285-4e52f0d3bd7d}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/
AppSupportURL=https://github.com/
AppUpdatesURL=https://github.com/

; Thư mục cài đặt mặc định
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; File icon
SetupIconFile={#AppIcon}

; Output
OutputDir=output
OutputBaseFilename=AllMessenger_Setup_{#AppVersion}

; Nén tốt nhất
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Yêu cầu Windows 10 1809+ (build 17763) x64
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Không cần quyền admin (cài vào AppData nếu user thường)
; Đổi thành "admin" nếu muốn cài vào Program Files cho tất cả user
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Hiện license, wizard, v.v.
WizardStyle=modern
WizardSmallImageFile={#AppIcon}

; Cho phép uninstall
Uninstallable=yes
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
CreateUninstallRegKey=yes

[Languages]
Name: "vietnamese"; MessagesFile: "compiler:Languages\Default.isl"
Name: "english";    MessagesFile: "compiler:Default.isl"

[Tasks]
; Tuỳ chọn khi cài
Name: "desktopicon";    Description: "Tạo shortcut trên Desktop";       GroupDescription: "Shortcut:"; Flags: unchecked
Name: "startupicon";    Description: "Khởi động cùng Windows";           GroupDescription: "Tùy chọn:"; Flags: unchecked

[Files]
; ── Toàn bộ thư mục publish ──────────────────────────────────
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── WebView2 bootstrapper (tải về và đặt vào thư mục installer\) ──────
; Tải tại: https://developer.microsoft.com/en-us/microsoft-edge/webview2/
; Bỏ comment 2 dòng dưới nếu bạn muốn bundle WebView2
; Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
; Start Menu
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Gỡ cài đặt {#AppName}";  Filename: "{uninstallexe}"

; Desktop (nếu chọn task)
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Startup cùng Windows (nếu chọn task)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; ── Cài WebView2 nếu chưa có (bỏ comment nếu bundle bootstrapper) ──
; Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; \
;   Parameters: "/silent /install"; \
;   StatusMsg: "Đang cài Microsoft WebView2 Runtime..."; \
;   Flags: waituntilterminated

; ── Chạy app sau khi cài xong ────────────────────────────────
Filename: "{app}\{#AppExeName}"; \
  Description: "Chạy {#AppName} ngay bây giờ"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Xoá toàn bộ thư mục còn lại sau uninstall
Type: filesandordirs; Name: "{app}"

[Code]
// ── Kiểm tra WebView2 Runtime đã cài chưa ─────────────────────────────
function IsWebView2Installed(): Boolean;
var
  version: String;
begin
  Result := RegQueryStringValue(
    HKLM,
    'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv', version) and (version <> '') and (version <> '0.0.0.0');

  if not Result then
    Result := RegQueryStringValue(
      HKCU,
      'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
      'pv', version) and (version <> '') and (version <> '0.0.0.0');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsWebView2Installed() then
  begin
    MsgBox(
      'Ứng dụng cần Microsoft WebView2 Runtime.' + #13#10 +
      'Vui lòng tải và cài đặt tại:' + #13#10 +
      'https://developer.microsoft.com/en-us/microsoft-edge/webview2/' + #13#10#13#10 +
      'Hoặc cập nhật Microsoft Edge lên phiên bản mới nhất.',
      mbInformation, MB_OK);
    // Vẫn cho cài tiếp vì Edge thường đã có sẵn trên Win10/11
  end;
end;
