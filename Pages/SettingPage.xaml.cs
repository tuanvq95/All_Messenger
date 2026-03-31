using All_Messenger.Helper;
using All_Messenger.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace All_Messenger.Pages;

public sealed partial class SettingPage : Page
{
    private bool _isLoading;

    public SettingPage()
    {
        InitializeComponent();
        Loaded += SettingPage_Loaded;
    }

    private void SettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        var mode = LoadNotificationMode();
        if (mode == NotificationService.NotificationModeSilent)
            RadioSilent.IsChecked = true;
        else
            RadioToast.IsChecked = true;
        _isLoading = false;
    }

    private void NotificationModeGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (RadioSilent.IsChecked == true)
            SaveNotificationMode(NotificationService.NotificationModeSilent);
        else
            SaveNotificationMode(NotificationService.NotificationModeToast);
    }

    private static string LoadNotificationMode()
    {
        return AppSettings.Get(NotificationService.NotificationModeKey)
            ?? NotificationService.NotificationModeToast;
    }

    private static void SaveNotificationMode(string mode)
    {
        AppSettings.Set(NotificationService.NotificationModeKey, mode);
    }
}
