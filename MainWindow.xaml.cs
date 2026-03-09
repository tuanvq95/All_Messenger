using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using Windows.Storage;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace All_Messenger
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const string ThemeKey = "AppTheme";

        public MainWindow()
        {
            this.InitializeComponent();

            string theme = LoadTheme();
            UpdateIcons();
            ApplyTheme(theme);

            ContentFrame.Navigate(typeof(Pages.MessengerPage));
            this.SystemBackdrop = new MicaBackdrop();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            // Ví dụ reload frame
            ContentFrame.Navigate(ContentFrame.CurrentSourcePageType);
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string page = item.Tag.ToString();

                switch (page)
                {
                    case "MessengerPage":
                        ContentFrame.Navigate(typeof(Pages.MessengerPage));
                        break;

                    case "ZaloPage":
                        ContentFrame.Navigate(typeof(Pages.ZaloPage));
                        break;

                    case "TeamsPage":
                        ContentFrame.Navigate(typeof(Pages.TeamsPage));
                        break;
                }
            }
        }

        #region Theme
        private void SaveTheme(string theme)
        {
            ApplicationData.Current.LocalSettings.Values[ThemeKey] = theme;
        }

        private string LoadTheme()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;

            if (settings.ContainsKey(ThemeKey))
                return settings[ThemeKey]?.ToString();

            return "System";
        }

        private void ApplyTheme(string theme)
        {
            switch (theme)
            {
                case "Dark":
                    DarkModeToggle.IsChecked = true;
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
                    ThemeIcon.Glyph = "\uE708";
                    ApplyTitleBarTheme(true);
                    break;

                case "Light":
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
                    ThemeIcon.Glyph = "\uE706";
                    ApplyTitleBarTheme(false);
                    break;

                default:
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Default;
                    ApplyTitleBarTheme(false);
                    break;
            }
        }

        private void DarkMode_Checked(object sender, RoutedEventArgs e)
        {
            ThemeIcon.Glyph = "\uF0CE";
            ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;

            SaveTheme("Dark");
            UpdateIcons();
            ApplyTitleBarTheme(true);
        }

        private void DarkMode_Unchecked(object sender, RoutedEventArgs e)
        {
            ThemeIcon.Glyph = "\uF08C"; // Sun
            ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;

            SaveTheme("Light");
            UpdateIcons();
            ApplyTitleBarTheme(false);
        }

        private void ApplyTitleBarTheme(bool isDark)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Hidden title bar default
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            if (isDark)
            {
                appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

                appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
                appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(90, 255, 255, 255);

                appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 80, 80, 80);
            }
            else
            {
                appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
                appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

                appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
                appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 0, 0, 0);

                appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);
            }
        }
        private void UpdateIcons()
        {
            ElementTheme theme = ((FrameworkElement)Content).ActualTheme;

            if (theme == ElementTheme.Dark)
            {
                MessengerIcon.Source =
                    new BitmapImage(new Uri("ms-appx:///Assets/messenger_light.png"));
                ZaloIcon.Source =
                   new BitmapImage(new Uri("ms-appx:///Assets/zalo_light.png"));
                TeamsIcon.Source =
                   new BitmapImage(new Uri("ms-appx:///Assets/teams_light.png"));
            }
            else
            {
                MessengerIcon.Source =
                    new BitmapImage(new Uri("ms-appx:///Assets/messenger_dark.png"));
                ZaloIcon.Source =
                    new BitmapImage(new Uri("ms-appx:///Assets/zalo_dark.png"));
                TeamsIcon.Source =
                    new BitmapImage(new Uri("ms-appx:///Assets/teams_dark.png"));
            }
        }
        #endregion
    }
}
