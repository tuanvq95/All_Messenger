using All_Messenger.Helper;
using All_Messenger.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace All_Messenger.Pages;

public sealed partial class SettingPage : Page
{
    private bool _isLoading;

    /// <summary>Danh sách server tùy chỉnh — bound tới ListView trong XAML.</summary>
    public ObservableCollection<CustomServerInfo> CustomServers { get; } = new();

    // 30 icon (6 × 5) từ Segoe MDL2 Assets, ưu tiên icon phù hợp với chat server
    private static readonly (string Label, string Glyph)[] s_iconOptions =
    [
        // ── Nhắn tin ──────────────────────────────────
        ("Quả địa cầu",  "\uE774"),   // Globe
        ("Chat",          "\uE8BD"),   // Chat bubble
        ("Tin nhắn",      "\uE715"),   // Message
        ("Bình luận",     "\uE8F2"),   // Comment
        ("Micro",         "\uE720"),   // Microphone — voice channel
        // ── Giao tiếp ─────────────────────────────────
        ("Điện thoại",    "\uE717"),   // Phone
        ("Video call",    "\uE714"),   // Video camera
        ("Tai nghe",      "\uE95B"),   // Headset — Discord / voice
        ("Email",         "\uE8A0"),   // Mail
        ("Gửi",           "\uE8C8"),   // Send
        // ── Cộng đồng ─────────────────────────────────
        ("Người dùng",    "\uE77B"),   // Person
        ("Nhóm",          "\uE716"),   // People / group
        ("Thích",         "\uE899"),   // Like / thumbs up
        ("Yêu thích",     "\uE734"),   // Heart favorite
        ("Ngôi sao",      "\uE735"),   // Star (solid)
        // ── Giải trí ──────────────────────────────────
        ("Trò chơi",      "\uE7FC"),   // Game controller
        ("Camera",        "\uE722"),   // Camera
        ("Âm nhạc",       "\uE8D6"),   // Music note — music bot servers
        ("Đám mây",       "\uE753"),   // Cloud
        ("Thông báo",     "\uE7A7"),   // Bell
        // ── Công nghệ ─────────────────────────────────
        ("Thế giới",      "\uE909"),   // Globe2 / World
        ("Di động",       "\uE8EA"),   // Mobile phone
        ("Lập trình",     "\uE8F4"),   // Code / library
        ("Công việc",     "\uE8A5"),   // Briefcase
        ("Chia sẻ",       "\uE72D"),   // Share
        // ── Tiện ích ──────────────────────────────────
        ("Ghim",          "\uE718"),   // Pin
        ("Liên kết",      "\uE71B"),   // Link
        ("Bookmark",      "\uE8A4"),   // Bookmark
        ("Bảo mật",       "\uE72E"),   // Shield / security
        ("Lịch",          "\uE787"),   // Calendar — event servers
    ];

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

        CustomServers.Clear();
        foreach (var server in AppSettings.GetCustomServers())
            CustomServers.Add(server);
    }

    private void NotificationModeGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (RadioSilent.IsChecked == true)
            SaveNotificationMode(NotificationService.NotificationModeSilent);
        else
            SaveNotificationMode(NotificationService.NotificationModeToast);
    }

    private static string LoadNotificationMode() =>
        AppSettings.Get(NotificationService.NotificationModeKey)
            ?? NotificationService.NotificationModeToast;

    private static void SaveNotificationMode(string mode) =>
        AppSettings.Set(NotificationService.NotificationModeKey, mode);

    // ── Icon Picker ───────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo lưới icon 5 cột. Người dùng chọn một icon — loại trừ nhau kiểu radio.
    /// onChanged được gọi mỗi khi lựa chọn thay đổi.
    /// </summary>
    private UIElement BuildIconPicker(string initialGlyph, Action<string> onChanged)
    {
        const int cols = 5;
        var toggles = new Dictionary<string, ToggleButton>(s_iconOptions.Length);
        bool updating = false;

        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

        for (int i = 0; i < s_iconOptions.Length; i++)
        {
            int row = i / cols;
            int col = i % cols;
            if (col == 0)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });

            var (label, glyph) = s_iconOptions[i];
            var g = glyph; // capture per-iteration

            var tb = new ToggleButton
            {
                Width = 44,
                Height = 44,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                IsChecked = g == initialGlyph
            };
            ToolTipService.SetToolTip(tb, label);
            tb.Content = new FontIcon
            {
                Glyph = g,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18
            };

            tb.Checked += (_, _) =>
            {
                if (updating) return;
                updating = true;
                foreach (var (k, v) in toggles)
                    if (k != g) v.IsChecked = false;
                updating = false;
                onChanged(g);
            };

            toggles[g] = tb;
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        return grid;
    }

    // ── Custom Servers ────────────────────────────────────────────────────────

    private async void AddServer_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            Header = "Tên hiển thị",
            PlaceholderText = "e.g. Discord"
        };
        var urlBox = new TextBox
        {
            Header = "URL",
            PlaceholderText = "e.g. https://discord.com/app"
        };
        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };

        string selectedIcon = s_iconOptions[0].Glyph; // mặc định Globe
        var iconPicker = BuildIconPicker(selectedIcon, g => selectedIcon = g);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(nameBox);
        panel.Children.Add(urlBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Chọn icon",
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        });
        panel.Children.Add(iconPicker);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Thêm chat server",
            Content = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 520
            },
            PrimaryButtonText = "Thêm",
            CloseButtonText = "Hủy",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var name = nameBox.Text.Trim();
            var url = urlBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
            {
                errorText.Text = "Vui lòng điền đầy đủ tên và URL.";
                errorText.Visibility = Visibility.Visible;
                continue;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                errorText.Text = "URL không hợp lệ.";
                errorText.Visibility = Visibility.Visible;
                continue;
            }

            var info = new CustomServerInfo { Name = name, Url = url, IconGlyph = selectedIcon };

            var servers = AppSettings.GetCustomServers();
            servers.Add(info);
            AppSettings.SaveCustomServers(servers);

            CustomServers.Add(info);
            App.MainWindow?.AddCustomServerTab(info);
            break;
        }
    }

    private async void EditServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        var server = CustomServers.FirstOrDefault(s => s.Id == id);
        if (server is null) return;

        var nameBox = new TextBox
        {
            Header = "Tên hiển thị",
            Text = server.Name
        };
        var urlBox = new TextBox
        {
            Header = "URL",
            Text = server.Url
        };
        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };

        string selectedGlyph = server.IconGlyph;
        var iconPicker = BuildIconPicker(selectedGlyph, g => selectedGlyph = g);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(nameBox);
        panel.Children.Add(urlBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Chọn icon",
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        });
        panel.Children.Add(iconPicker);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Chỉnh sửa server",
            Content = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 520
            },
            PrimaryButtonText = "Lưu",
            CloseButtonText = "Hủy",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var name = nameBox.Text.Trim();
            var url = urlBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
            {
                errorText.Text = "Vui lòng điền đầy đủ tên và URL.";
                errorText.Visibility = Visibility.Visible;
                continue;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                errorText.Text = "URL không hợp lệ.";
                errorText.Visibility = Visibility.Visible;
                continue;
            }

            // Cập nhật model — INPC tự refresh ListView
            server.Name = name;
            server.Url = url;
            server.IconGlyph = selectedGlyph;

            // Persist
            var servers = AppSettings.GetCustomServers();
            var saved = servers.FirstOrDefault(s => s.Id == id);
            if (saved is not null)
            {
                saved.Name = name;
                saved.Url = url;
                saved.IconGlyph = selectedGlyph;
            }
            AppSettings.SaveCustomServers(servers);

            // Cập nhật NavigationViewItem
            App.MainWindow?.UpdateCustomServerTab(id, name, selectedGlyph, url);
            break;
        }
    }

    private void DeleteServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var server = CustomServers.FirstOrDefault(s => s.Id == id);
        if (server is null) return;

        CustomServers.Remove(server);

        var servers = AppSettings.GetCustomServers();
        servers.RemoveAll(s => s.Id == id);
        AppSettings.SaveCustomServers(servers);

        App.MainWindow?.RemoveCustomServerTab(id);
    }
}
