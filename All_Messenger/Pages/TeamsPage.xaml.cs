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
        WebViewNotificationHelper.AttachSessionDetector(
            AppId, core,
            url => url.Contains("teams.microsoft.com") &&
                   !url.Contains("login") &&
                   !url.Contains("oauth2") &&
                   !url.Contains("microsoftonline.com"));
    }
}