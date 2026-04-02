; ============================================================
;  All-in-One Messenger — Inno Setup Script
;  Yêu cầu: Inno Setup 6.x  https://jrsoftware.org/isinfo.php
;
;  Build app trước (chạy từ thư mục project, KHÔNG dùng -o):
;    dotnet publish AllinOneMessenger.csproj -c Release -r win-x64 --self-contained true
;  Sau đó compile file .iss này bằng IsCC.exe hoặc Inno Setup Compiler GUI
; ============================================================

#define AppName      "All-in-One Messenger"
#define AppVersion   "1.1.0"
#define AppPublisher "MrRom"
#define AppExeName   "AllinOneMessenger.exe"
#define AppIcon      "..\Assets\icon.ico"
; Đường dẫn publish (tương đối từ vị trí file .iss này)
#define PublishDir   "..\bin\Release\net8.0-windows10.0.19041.0\win-x64"

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

; Icon
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

; Không cần quyền admin (cài vào %LocalAppData% nếu user thường)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Wizard
WizardStyle=modern

; Uninstall
Uninstallable=yes
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
CreateUninstallRegKey=yes

; Restart nếu cần (hiếm khi xảy ra)
RestartIfNeededByRun=no

[Languages]
Name: "english";    MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo shortcut trên Desktop";  GroupDescription: "Shortcut:"; Flags: unchecked
Name: "startupicon"; Description: "Khởi động cùng Windows";      GroupDescription: "Tùy chọn:"; Flags: unchecked

[Files]
; Toàn bộ thư mục publish (self-contained, bao gồm runtime .NET)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; WebView2 bootstrapper — tải về tay tại link dưới rồi đặt cạnh file .iss:
; https://go.microsoft.com/fwlink/p/?LinkId=2124703
; Bỏ comment 2 dòng dưới để bundle file bootstrapper vào installer:
; Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
; Start Menu
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Gỡ cài đặt {#AppName}"; Filename: "{uninstallexe}"

; Desktop
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Khởi động cùng Windows
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupicon

; Đăng ký AppUserModelID để Windows App SDK notification hoạt động (unpackaged)
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\AllInOneMessenger"; \
  ValueType: string; ValueName: "DisplayName"; ValueData: "{#AppName}"; \
  Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\AllInOneMessenger"; \
  ValueType: string; ValueName: "IconUri"; ValueData: "{app}\{#AppExeName}"; \
  Flags: uninsdeletekey

[Run]
; Cài WebView2 nếu chưa có — bỏ comment nếu bundle bootstrapper (xem [Files] ở trên)
; Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; \
;   Parameters: "/silent /install"; \
;   StatusMsg: "Đang cài Microsoft WebView2 Runtime..."; \
;   Flags: waituntilterminated; Check: not IsWebView2Installed

; Chạy app sau khi cài xong
Filename: "{app}\{#AppExeName}"; \
  Description: "Chạy {#AppName} ngay bây giờ"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Xoá toàn bộ thư mục còn lại sau uninstall
Type: filesandordirs; Name: "{app}"

[Code]
// ── Kiểm tra WebView2 Runtime đã cài chưa ───────────────────────────────────
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

  // Microsoft Edge (Chromium) cũng cung cấp WebView2 Runtime
  if not Result then
    Result := RegQueryStringValue(
      HKLM,
      'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}',
      'pv', version) and (version <> '') and (version <> '0.0.0.0');
end;

function InitializeSetup(): Boolean;
var
  ErrCode: Integer;
begin
  Result := True;
  if not IsWebView2Installed() then
  begin
    if MsgBox(
      'Ứng dụng cần Microsoft WebView2 Runtime để hoạt động.' + #13#10#13#10 +
      'Bạn có muốn mở trang tải về WebView2 không?' + #13#10 +
      '(Sau khi cài WebView2 xong, hãy chạy lại bộ cài này)',
      mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://go.microsoft.com/fwlink/p/?LinkId=2124703', '', '', SW_SHOW, ewNoWait, ErrCode);
      Result := False; // Dừng cài, chờ user cài WebView2 trước
    end;
  end;
end;
