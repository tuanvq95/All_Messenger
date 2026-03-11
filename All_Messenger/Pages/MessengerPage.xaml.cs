using All_Messenger.Helper;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;

namespace All_Messenger.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MessengerPage : WebViewPageBase
{
    public override WebView2 WebView => MessengerWebView;
    public override string AppId => "Messenger";
    public override Uri StartUri => new("https://www.messenger.com/");

    public MessengerPage()
    {
        InitializeComponent();
        InitWebView();
    }

    protected override void OnCoreWebView2Ready(CoreWebView2 core)
    {
        WebViewNotificationHelper.AttachSessionDetector(
            AppId, core,
            url => url.Contains("messenger.com") &&
                   !url.Contains("login") &&
                   !url.Contains("oauth"));
    }
}