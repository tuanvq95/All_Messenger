using All_Messenger.Helper;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;

namespace All_Messenger.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class TeamsPage : WebViewPageBase
{
    public override WebView2 WebView => TeamsWebView;
    public override string AppId => "Teams";
    public override Uri StartUri => new("https://teams.microsoft.com/");

    public TeamsPage()
    {
        InitializeComponent();
        InitWebView();
    }

    protected override void OnCoreWebView2Ready(CoreWebView2 core)
    {
        // Dùng cả teams.microsoft.com (work/school) lẫn teams.live.com (personal account).
        // Không check oauth2/microsoftonline.com vì đã bị lọc bởi condition đầu.
        // Dùng "/login" thay "login" để tránh false negative với query param login_hint=...
        // resetOnFalse=false: Teams có thể mở link SharePoint/OneDrive sang domain khác,
        // không được reset session khi đó.
        WebViewNotificationHelper.AttachSessionDetector(
            AppId, core,
            url => (url.Contains("teams.microsoft.com") || url.Contains("teams.live.com")) &&
                   !url.Contains("/login"),
            resetOnFalse: false);
    }
}