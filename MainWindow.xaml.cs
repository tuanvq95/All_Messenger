using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using All_Messenger.Helper;
using All_Messenger.Pages;
using WinRT.Interop;
using Microsoft.UI.Xaml.Media.Animation;

namespace All_Messenger
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private AppWindow _appWindow = null!;

        // ── Theme ────────────────────────────────────────────────────────────────
        private const string ThemeKey = "AppTheme";
        private const string ThemeDark = "Dark";
        private const string ThemeLight = "Light";
        private const string ThemeSystem = "System";

        // ── Glyph ────────────────────────────────────────────────────────────────
        private const string GlyphMoon = "\uE708";
        private const string GlyphSun = "\uF08C";
        private const string GlyphSunAlt = "\uE706";
        private const string GlyphMoonAlt = "\uF0CE";

        // ── Tab tags (must match NavigationViewItem.Tag in XAML) ─────────────────
        private const string TabZalo = "ZaloPage";
        private const string TabTeams = "TeamsPage";
        private const string TabSettings = "SettingPage";
        private const string TabMessenger = "MessengerPage";

        // ── App IDs ──────────────────────────────────────────────────────────────
        private const string AppIdZalo = "Zalo";
        private const string AppIdTeams = "Teams";
        private const string AppIdMessenger = "Messenger";

        // ── Assets ───────────────────────────────────────────────────────────────
        private const string AssetMessengerLight = "ms-appx:///Assets/messenger_light.png";
        private const string AssetMessengerDark = "ms-appx:///Assets/messenger_dark.png";
        private const string AssetZaloLight = "ms-appx:///Assets/zalo_light.png";
        private const string AssetZaloDark = "ms-appx:///Assets/zalo_dark.png";
        private const string AssetTeamsLight = "ms-appx:///Assets/teams_light.png";
        private const string AssetTeamsDark = "ms-appx:///Assets/teams_dark.png";

        private readonly BitmapImage _messengerLight;
        private readonly BitmapImage _messengerDark;
        private readonly BitmapImage _zaloLight;
        private readonly BitmapImage _zaloDark;
        private readonly BitmapImage _teamsLight;
        private readonly BitmapImage _teamsDark;

        private Dictionary<string, (FrameworkElement Page, string AppId)> _tabs = null!;
        private readonly Dictionary<string, CustomServerPage> _customPages = new();
        private string _activeTab = string.Empty;

        public MainWindow()
        {
            InitializeComponent();

            _messengerLight = new(new Uri(AssetMessengerLight));
            _messengerDark = new(new Uri(AssetMessengerDark));
            _zaloLight = new(new Uri(AssetZaloLight));
            _zaloDark = new(new Uri(AssetZaloDark));
            _teamsLight = new(new Uri(AssetTeamsLight));
            _teamsDark = new(new Uri(AssetTeamsDark));

            _tabs = new()
            {
                [TabTeams] = (TeamsPage, AppIdTeams),
                [TabMessenger] = (MessengerPage, AppIdMessenger),
                [TabZalo] = (ZaloPage, AppIdZalo),
                [TabSettings] = (SettingPage, string.Empty),
            };

            // Nạp các custom server đã lưu
            foreach (var server in AppSettings.GetCustomServers())
                AddCustomServerTab(server);

            var hwnd = WindowNative.GetWindowHandle(this);
            _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));

            ApplyTheme(LoadTheme());
            UpdateIcons();
            this.SystemBackdrop = new MicaBackdrop();
            ((FrameworkElement)Content).Loaded += OnWindowLoaded;
            this.Activated += (_, a) =>
            {
                bool active = a.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
                Services.NotificationService.Instance.SetWindowActive(active);
            };

            Services.NotificationService.Instance.TabBadgeChanged += OnTabBadgeChanged;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)Content).Loaded -= OnWindowLoaded;

            // Wait until ALL WebViews finish their first navigation (up to 30s)
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                var (_, messenger) = GetWebViewInfo(AppIdMessenger);
                var (_, teams) = GetWebViewInfo(AppIdTeams);
                if (messenger && teams) break;
                await Task.Delay(100);
            }

            await HideSplashAsync();
            WelcomeView.Visibility = Visibility.Visible;
        }

        private Task HideSplashAsync()
        {
            var tcs = new TaskCompletionSource();
            var fade = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(1200)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var storyboard = new Storyboard();
            Storyboard.SetTarget(fade, SplashView);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);
            storyboard.Completed += (_, _) =>
            {
                SplashView.Visibility = Visibility.Collapsed;
                tcs.SetResult();
            };
            storyboard.Begin();
            return tcs.Task;
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeTab)) return;

            if (_tabs.TryGetValue(_activeTab, out var entry))
            {
                var (webView, _) = GetWebViewInfo(entry.AppId);
                webView?.Reload();
            }
        }

        #region Menu
        private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                if (_activeTab == TabSettings) return;
                await SwitchTab(TabSettings);
                return;
            }

            string tag = args.SelectedItem is NavigationViewItem item
                ? item.Tag?.ToString() ?? string.Empty : string.Empty;

            if (tag == _activeTab) return; // guard: no-op nếu click lại tab đang active

            await SwitchTab(tag);
        }

        private async Task SwitchTab(string page)
        {
            if (WelcomeView.Visibility == Visibility.Visible)
                WelcomeView.Visibility = Visibility.Collapsed;

            var suspendTasks = new List<Task>();

            foreach (var (tag, (element, appId)) in _tabs)
            {
                if (tag == page)
                {
                    element.Visibility = Visibility.Visible;
                    await OnTabShown(appId);
                }
                else if (element.Visibility == Visibility.Visible)
                {
                    // Chỉ suspend tab đang visible, bỏ qua tab đã collapsed rồi
                    element.Visibility = Visibility.Collapsed;
                    suspendTasks.Add(OnTabHidden(appId));
                }
            }

            // Suspend song song tất cả tab bị ẩn
            await Task.WhenAll(suspendTasks);
            _activeTab = page;
        }

        private Task OnTabShown(string appId)
        {
            Services.NotificationService.Instance.SetActiveTab(appId);

            var (webView, isReady) = GetWebViewInfo(appId);
            try
            {
                if (isReady && webView?.CoreWebView2 != null && webView.CoreWebView2.IsSuspended)
                    webView.CoreWebView2.Resume();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnTabShown:{appId}] Resume failed: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(appId))
                Services.NotificationService.Instance.ClearBadge(appId);

            return Task.CompletedTask;
        }

        private async Task OnTabHidden(string appId)
        {
            try
            {
                var (webView, isReady) = GetWebViewInfo(appId);
                if (webView?.CoreWebView2 != null && isReady && !webView.CoreWebView2.IsSuspended)
                {
                    await webView.CoreWebView2.TrySuspendAsync();
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // TrySuspendAsync fails with COMException (0x8007139F) when the WebView
                // is mid-navigation or in an invalid state for suspension.
                // This is expected and safe to ignore — suspend is a best-effort optimization.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[OnTabHidden:{appId}] Failed: {ex.Message}");
            }
        }

        private (WebView2? WebView, bool IsReady) GetWebViewInfo(string appId) => appId switch
        {
            AppIdZalo => (ZaloPage.WebView, ZaloPage.IsReady),
            AppIdTeams => (TeamsPage.WebView, TeamsPage.IsReady),
            AppIdMessenger => (MessengerPage.WebView, MessengerPage.IsReady),
            _ => _customPages.TryGetValue(appId, out var p) ? (p.WebView, p.IsReady) : (null, false)
        };

        // ── Badge trên nav item ────────────────────────────────────────────────────────

        private void OnTabBadgeChanged(string appId, int count)
        {
            // Tìm tag tương ứng với appId
            string? tag = _tabs.FirstOrDefault(kv => kv.Value.AppId == appId).Key;
            if (tag is null) return;

            var navItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == tag);

            if (navItem is null) return;

            navItem.InfoBadge = count > 0 ? new InfoBadge { Value = count } : null;
        }

        // ── Custom Server tabs ────────────────────────────────────────────────────

        internal void AddCustomServerTab(CustomServerInfo info)
        {
            if (_tabs.ContainsKey(info.Id)) return;

            var page = new CustomServerPage(info);
            page.Visibility = Visibility.Collapsed;
            ContentGrid.Children.Add(page);

            var navItem = new NavigationViewItem
            {
                Content = info.Name,
                Tag = info.Id,
                Icon = new FontIcon
                {
                    Glyph = info.IconGlyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets")
                }
            };
            NavView.MenuItems.Add(navItem);

            _customPages[info.Id] = page;
            _tabs[info.Id] = (page, info.Id);
        }

        internal void RemoveCustomServerTab(string id)
        {
            if (!_tabs.TryGetValue(id, out var entry)) return;

            // If it's the active tab, show welcome screen
            if (_activeTab == id)
            {
                WelcomeView.Visibility = Visibility.Visible;
                NavView.SelectedItem = null;
                _activeTab = string.Empty;
            }

            // Remove page from visual tree
            ContentGrid.Children.Remove((UIElement)entry.Page);

            // Remove NavItem
            var navItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == id);
            if (navItem is not null)
                NavView.MenuItems.Remove(navItem);

            _tabs.Remove(id);
            _customPages.Remove(id);
        }

        internal void UpdateCustomServerTabIcon(string id, string glyph)
        {
            var navItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == id);
            if (navItem?.Icon is FontIcon fi)
                fi.Glyph = glyph;
        }

        internal void UpdateCustomServerTab(string id, string name, string glyph, string url)
        {
            var navItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == id);
            if (navItem is not null)
            {
                navItem.Content = name;
                if (navItem.Icon is FontIcon fi)
                    fi.Glyph = glyph;
            }

            // Cập nhật URL cho WebView nếu thay đổi
            if (_customPages.TryGetValue(id, out var page))
                page.NavigateTo(url);
        }

        internal async void NavigateToSettings()
        {
            NavView.SelectedItem = NavView.SettingsItem;
            await SwitchTab(TabSettings);
        }

        internal async void NavigateToTab(string appId)
        {
            string? tag = _tabs.FirstOrDefault(kv => kv.Value.AppId == appId).Key;
            if (tag is null) return;

            var navItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == tag);

            if (navItem is not null)
                NavView.SelectedItem = navItem;

            await SwitchTab(tag);
        }
        #endregion

        #region Theme
        private static void SaveTheme(string theme)
        {
            AppSettings.Set(ThemeKey, theme);
        }

        private static string LoadTheme()
        {
            return AppSettings.Get(ThemeKey) ?? ThemeSystem;
        }

        private void ApplyTheme(string theme)
        {
            switch (theme)
            {
                case ThemeDark:
                    DarkModeToggle.IsChecked = true;
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
                    ThemeIcon.Glyph = GlyphMoon;
                    ApplyTitleBarTheme(true);
                    break;

                case ThemeLight:
                    ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
                    ThemeIcon.Glyph = GlyphSunAlt;
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
            ThemeIcon.Glyph = GlyphMoonAlt;
            ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
            SaveTheme(ThemeDark);
            UpdateIcons();
            ApplyTitleBarTheme(true);
        }

        private void DarkMode_Unchecked(object sender, RoutedEventArgs e)
        {
            ThemeIcon.Glyph = GlyphSun;
            ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
            SaveTheme(ThemeLight);
            UpdateIcons();
            ApplyTitleBarTheme(false);
        }

        private void ApplyTitleBarTheme(bool isDark)
        {
            if (isDark)
            {
                _appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

                _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
                _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(90, 255, 255, 255);

                _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 80, 80, 80);
            }
            else
            {
                _appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
                _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

                _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
                _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 0, 0, 0);

                _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);
            }
        }
        private void UpdateIcons()
        {
            ElementTheme theme = ((FrameworkElement)Content).ActualTheme;

            if (theme == ElementTheme.Dark)
            {
                MessengerIcon.Source = _messengerLight;
                ZaloIcon.Source = _zaloLight;
                TeamsIcon.Source = _teamsLight;
            }
            else
            {
                MessengerIcon.Source = _messengerDark;
                ZaloIcon.Source = _zaloDark;
                TeamsIcon.Source = _teamsDark;
            }
        }
        #endregion
    }
}
