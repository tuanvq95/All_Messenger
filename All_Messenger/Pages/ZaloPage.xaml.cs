using All_Messenger.Helper;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;

namespace All_Messenger.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ZaloPage : WebViewPageBase
{
    public override WebView2 WebView => ZaloWebView;
    public override string AppId => "Zalo";
    public override Uri StartUri => new("https://chat.zalo.me/");

    public ZaloPage()
    {
        InitializeComponent();
        InitWebView();
    }

    protected override void OnCoreWebView2Ready(CoreWebView2 core)
    {
        // Login/OAuth c?a Zalo n?m tr?n id.zalo.me, kh?ng ph?i chat.zalo.me
        // ¨ ch? c?n check chat.zalo.me l? ?? ?? x?c ??nh logged-in.
        // resetOnFalse=false: tr?nh reset session khi Zalo navigate sang
        // zalo.me (sticker store, b?i vi?t, v.v.) ho?c link ngo?i.
        WebViewNotificationHelper.AttachSessionDetector(
            AppId, core,
            url => url.Contains("chat.zalo.me"),
            resetOnFalse: false);
    }
}
