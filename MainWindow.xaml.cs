using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Media.Animation;

using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using WpfApplication = System.Windows.Application;
using WpfClipboard = System.Windows.Clipboard;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfBrush = System.Windows.Media.Brush;
using WpfLinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using Visibility = System.Windows.Visibility;

namespace ClipFlow;

public partial class MainWindow : Window
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly ObservableCollection<ClipItem> _items = new();
    private readonly ClipboardMonitor? _clipboardMonitor;
    private readonly DatabaseService? _database;
    private ICollectionView? _view;
    private bool _darkTheme = true;
    private string _lastHash = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindow(
    ClipboardMonitor? clipboardMonitor = null,
    DatabaseService? database = null)
    {
        InitializeComponent();

        _clipboardMonitor = clipboardMonitor;
        _database = database;

        // Load theme from settings
        _darkTheme = SettingsService.Current.DarkTheme;
        ApplyTheme(_darkTheme);

        LoadFromDatabase();

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = FilterItems;
        ItemsList.ItemsSource = _view;

        UpdateItemCount();
        UpdateEmptyState();
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void LoadFromDatabase()
    {
        if (_database == null)
        {
            LoadSampleData();
            return;
        }

        try
        {
            foreach (var item in _database.LoadAll(200))
                _items.Add(item);
        }
        catch { /* start empty if db fails */ }
    }

    private void LoadSampleData()
    {
        var samples = new[]
        {
            new ClipItem("📌","Email signature",
                "Best regards, Alex — Product Designer at TechCorp",
                "Pinned","📌",true,
                "Best regards,\nAlex Johnson\nProduct Designer @ TechCorp"),

            new ClipItem("💻","git push -u origin main",
                "Push with upstream tracking",
                "Pinned","📌",true,
                "git push -u origin main"),

            new ClipItem("🔗","supaste.com",
                "https://www.supaste.com",
                "2m ago","",false,
                "https://www.supaste.com"),

            new ClipItem("📝","Meeting notes — Q4 planning",
                "Launch date Jan 15, budget approved $450K",
                "8m ago","",false,
                "Q4 Planning Meeting\n\nLaunch date: January 15\nBudget: $450K"),

            new ClipItem("💻","docker-compose up -d",
                "Start all services in background",
                "15m ago","",false,
                "docker-compose up -d"),

            new ClipItem("📧","Support reply template",
                "Hi [Name], thank you for reaching out...",
                "32m ago","",false,
                "Hi {{customer_name}},\n\nThank you for reaching out.\n\nBest regards,\nSupport Team"),

            new ClipItem("🏠","Home address",
                "742 Evergreen Terrace, Springfield, IL",
                "1h ago","",false,
                "742 Evergreen Terrace\nSpringfield, IL 62701"),

            new ClipItem("✅","Task list",
                "Ship v2.1, write changelog, prepare demo",
                "Yesterday","",false,
                "Ship v2.1\nWrite changelog\nPrepare demo video"),
        };

        foreach (var s in samples)
            _items.Add(s);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void AddClipboardItem(string text)
    {
        Dispatcher.Invoke(() =>
        {
            string hash = text.GetHashCode(StringComparison.Ordinal).ToString();
            if (hash == _lastHash) return;
            _lastHash = hash;

            var item = new ClipItem(
                DetectIcon(text),
                BuildTitle(text),
                BuildPreview(text),
                "Just now", "", false, text);

            _database?.SaveItem(item);

            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i].Content == text) { _items.RemoveAt(i); break; }

            _items.Insert(0, item);

            while (_items.Count > 200)
                _items.RemoveAt(_items.Count - 1);

            _database?.TrimToLimit(200);
            _view?.Refresh();
            UpdateItemCount();
            UpdateEmptyState();

            if (IsVisible) SelectFirstItem();
        });
    }

    // ── Image handling ────────────────────────────────────────────────────────

    public void AddImageItem(BitmapSource image)
    {
        if (_database == null) return;

        try
        {
            // Save image to file
            string imagePath = _database.SaveImageToFile(image);

            int width = image.PixelWidth;
            int height = image.PixelHeight;

            var item = new ClipItem(
                icon: "🖼",
                title: $"Image  {width} × {height}",
                preview: $"Screenshot — {width}×{height} pixels",
                timeLabel: "Just now",
                pinLabel: "",
                isPinned: false,
                content: imagePath,
                imagePath: imagePath,
                isImage: true);

            _database.SaveItem(item);

            // Remove duplicate if exists
            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i].ImagePath == imagePath)
                { _items.RemoveAt(i); break; }

            _items.Insert(0, item);

            while (_items.Count > 200)
                _items.RemoveAt(_items.Count - 1);

            _database.TrimToLimit(200);
            _view?.Refresh();
            UpdateItemCount();
            UpdateEmptyState();

            if (IsVisible) SelectFirstItem();
        }
        catch
        {
            // Failed to save image
        }
    }

    // Add somewhere in the public methods section

    public void ApplyThemeFromSettings()
    {
        _darkTheme = SettingsService.Current.DarkTheme;
        ApplyTheme(_darkTheme);
    }

    public void SetMonitoringPaused(bool paused)
    {
        if (_clipboardMonitor == null) return;

        if (paused)
            _clipboardMonitor.Stop();
        else
            _clipboardMonitor.Start();
    }

    public void ClearHistory()
    {
        _database?.ClearAll();
        _items.Clear();
        _lastHash = string.Empty;
        _view?.Refresh();
        UpdateItemCount();
        UpdateEmptyState();
        ShowStatus("History cleared");
    }

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        PositionWindow();

        // Animate in — fade + slide up
        RootGrid.Opacity = 0;
        RootTranslate.Y = 16;

        var fade = MakeAnimation(0, 1, 200);
        var slide = MakeAnimation(16, 0, 220, new CubicEase { EasingMode = EasingMode.EaseOut });

        RootGrid.BeginAnimation(OpacityProperty, fade);
        RootTranslate.BeginAnimation(TranslateTransform.YProperty, slide);

        Activate();
        SearchBox.Focus();
        SearchBox.Clear();
        SearchPlaceholder.Visibility = Visibility.Visible;
        _view?.Refresh();
        UpdateEmptyState();
        SelectFirstItem();
    }

    public void HideWindow()
    {
        var fade = MakeAnimation(1, 0, 140);
        fade.Completed += (_, _) => Hide();
        RootGrid.BeginAnimation(OpacityProperty, fade);
    }

    // ── Window events ─────────────────────────────────────────────────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
        => SearchBox.Focus();

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        HideWindow();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideWindow();
    private void ThemeButton_Click(object sender, RoutedEventArgs e) => ToggleTheme();
    private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearHistory();

    // ── Search ────────────────────────────────────────────────────────────────

    private bool FilterItems(object obj)
    {
        if (obj is not ClipItem c) return false;
        string q = SearchBox?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(q)) return true;
        return c.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.Preview.Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.Content.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool empty = string.IsNullOrWhiteSpace(SearchBox.Text);
        SearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        SectionLabel.Text = empty ? "RECENT" : "RESULTS";
        _view?.Refresh();
        UpdateItemCount();
        UpdateEmptyState();
        SelectFirstItem();
    }

    // ── Keyboard ─────────────────────────────────────────────────────────────

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.T: ToggleTheme(); e.Handled = true; return;
                case Key.P: TogglePin(); e.Handled = true; return;
            }
        }

        switch (e.Key)
        {
            case Key.Escape: HideWindow(); e.Handled = true; break;
            case Key.Down: MoveSelection(+1); e.Handled = true; break;
            case Key.Up: MoveSelection(-1); e.Handled = true; break;
            case Key.Enter: PasteSelectedItem(); e.Handled = true; break;
            case Key.Delete: DeleteSelectedItem(); e.Handled = true; break;
        }
    }

    // ── List helpers ──────────────────────────────────────────────────────────

    private void MoveSelection(int delta)
    {
        int count = ItemsList.Items.Count;
        if (count == 0) return;

        int index = ItemsList.SelectedIndex < 0
            ? 0
            : (ItemsList.SelectedIndex + delta + count) % count;

        ItemsList.SelectedIndex = index;
        ItemsList.ScrollIntoView(ItemsList.SelectedItem);
    }

    private void SelectFirstItem()
    {
        if (ItemsList.Items.Count > 0)
        {
            ItemsList.SelectedIndex = 0;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
        else
        {
            ItemsList.SelectedIndex = -1;
        }
    }

    private void PasteSelectedItem()
    {
        if (ItemsList.SelectedItem is not ClipItem item) return;

        try
        {
            _clipboardMonitor?.SuppressNext();

            if (item.IsImage && System.IO.File.Exists(item.ImagePath))
            {
                // Load image and put it back on clipboard
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(item.ImagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                WpfClipboard.SetImage(bitmap);
                ShowStatus($"✓  Copied image — {item.Title}");
            }
            else
            {
                WpfClipboard.SetText(item.Content);
                ShowStatus($"✓  Copied — {Truncate(item.Title, 28)}");
            }

            HideWindow();
        }
        catch
        {
            ShowStatus("Could not copy item");
        }
    }

    private void DeleteSelectedItem()
    {
        if (ItemsList.SelectedItem is not ClipItem item) return;

        string deleteKey = item.IsImage ? item.ImagePath : item.Content;
        _database?.DeleteItem(deleteKey);

        int index = ItemsList.SelectedIndex;
        _items.Remove(item);
        _view?.Refresh();
        UpdateItemCount();
        UpdateEmptyState();

        int count = ItemsList.Items.Count;
        if (count > 0)
            ItemsList.SelectedIndex = Math.Min(index, count - 1);

        ShowStatus("Item removed");
    }

    private void TogglePin()
    {
        if (ItemsList.SelectedItem is not ClipItem item) return;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != item) continue;

            bool pinned = !item.IsPinned;
            var updated = new ClipItem(
                item.Icon, item.Title, item.Preview, item.TimeLabel,
                pinned ? "📌" : "", pinned,
                item.Content, item.ImagePath, item.IsImage);

            string pinKey = item.IsImage ? item.ImagePath : item.Content;
            _database?.UpdatePin(pinKey, pinned);
            _items[i] = updated;

            if (pinned)
            {
                _items.RemoveAt(i);
                _items.Insert(0, updated);
            }

            _view?.Refresh();
            ShowStatus(pinned ? "📌 Pinned" : "Unpinned");
            return;
        }
    }

    private void ItemsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => PasteSelectedItem();

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void PositionWindow()
    {
        double sw = SystemParameters.WorkArea.Width;
        double sh = SystemParameters.WorkArea.Height;
        Left = (sw - Width) / 2;
        Top = (sh - Height) / 2 - 40;
    }

    private void UpdateItemCount()
    {
        int total = _items.Count;
        int visible = ItemsList.Items.Count;
        ItemCountText.Text = total == 0
            ? "No items"
            : visible == total
                ? $"{total} item{(total == 1 ? "" : "s")}"
                : $"{visible} of {total} items";
    }

    private void UpdateEmptyState()
    {
        bool empty = ItemsList.Items.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ItemsList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private System.Windows.Threading.DispatcherTimer? _statusTimer;

    private void ShowStatus(string message, bool isAccent = true)
    {
        FooterStatus.Text = message;
        FooterStatus.Foreground = isAccent
            ? (WpfBrush)FindResource("Accent")
            : (WpfBrush)FindResource("TertiaryText");

        _statusTimer?.Stop();
        _statusTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _statusTimer.Tick += (_, _) =>
        {
            FooterStatus.Text = "Ready";
            FooterStatus.Foreground = (WpfBrush)FindResource("Accent");
            _statusTimer.Stop();
        };
        _statusTimer.Start();
    }

    private void ToggleTheme()
    {
        _darkTheme = !_darkTheme;
        ApplyTheme(_darkTheme);

        SettingsService.Current.DarkTheme = _darkTheme;
        SettingsService.Save();

        ShowStatus(_darkTheme ? "Dark theme" : "Light theme");
    }

    // ── Animation factory ─────────────────────────────────────────────────────

    private static DoubleAnimation MakeAnimation(
        double from, double to, int ms,
        IEasingFunction? ease = null)
    {
        var a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms));
        if (ease != null) a.EasingFunction = ease;
        return a;
    }

    // ── Content detection ─────────────────────────────────────────────────────

    private static string DetectIcon(string text)
    {
        string t = text.Trim();

        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "🔗";

        if (t.Contains('@') && t.Contains('.') && !t.Contains(' '))
            return "📧";

        if (t.StartsWith('{') || t.StartsWith('[') ||
            t.StartsWith("git ") || t.StartsWith("docker") ||
            t.StartsWith("npm ") || t.StartsWith("yarn ") ||
            t.StartsWith("pip ") || t.StartsWith("dotnet "))
            return "💻";

        if (t.Length > 400) return "📄";

        return "📝";
    }

    private static string BuildTitle(string text)
    {
        string t = text.Trim();

        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try { return new Uri(t).Host; }
            catch { return "Link"; }
        }

        string first = t.Split('\n')[0].Trim();
        return first.Length > 55 ? first[..55] + "…" : first;
    }

    private static string BuildPreview(string text)
    {
        string t = text.Trim();

        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return t.Length > 90 ? t[..90] + "…" : t;

        int nl = t.IndexOf('\n');
        if (nl > 0 && nl < t.Length - 1)
        {
            string rest = t[(nl + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(rest))
                return rest.Length > 90 ? rest[..90] + "…" : rest;
        }

        return t.Length > 90 ? t[..90] + "…" : t;
    }

    private static string Truncate(string s, int max)
        => s.Length > max ? s[..max] + "…" : s;

    // ── Theme ─────────────────────────────────────────────────────────────────

    private void ApplyTheme(bool dark)
    {
        if (dark)
        {
            Set("Surface", "#0D0D10");
            Set("SurfaceOverlay", "#10FFFFFF");
            Set("BorderColor", "#252530");
            Set("InnerBorderColor", "#18FFFFFF");
            Set("DividerColor", "#1E1E28");
            Set("PrimaryText", "#EEEEF5");
            Set("SecondaryText", "#64647A");
            Set("TertiaryText", "#42424F");
            Set("SearchBg", "#141418");
            Set("SearchBorder", "#252530");
            Set("ItemHover", "#161620");
            Set("ItemSelected", "#1E1E2E");
            Set("ItemSelectedBorder", "#2E2E48");
            Set("IconBg", "#1A1A26");
            Set("FooterBg", "#0A0A0E");
            Set("BadgeBg", "#161620");
            Set("BadgeBorder", "#252530");
            Set("ButtonHover", "#1A1A24");
            Set("ButtonPressed", "#141420");
            Set("ScrollThumb", "#2A2A38");
            Set("Accent", "#7B68FF");
            SetGradient("AccentGradient", "#8B7AFF", "#5B4DE0");
        }
        else
        {
            Set("Surface", "#FAFAFA");
            Set("SurfaceOverlay", "#70FFFFFF");
            Set("BorderColor", "#E2E2EC");
            Set("InnerBorderColor", "#90FFFFFF");
            Set("DividerColor", "#EAEAF2");
            Set("PrimaryText", "#18181E");
            Set("SecondaryText", "#72728A");
            Set("TertiaryText", "#A0A0B4");
            Set("SearchBg", "#F2F2F8");
            Set("SearchBorder", "#E2E2EC");
            Set("ItemHover", "#F0F0F8");
            Set("ItemSelected", "#EAE8FF");
            Set("ItemSelectedBorder", "#C8C4FF");
            Set("IconBg", "#EEEEF6");
            Set("FooterBg", "#F5F5FA");
            Set("BadgeBg", "#EAEAF2");
            Set("BadgeBorder", "#DCDCE8");
            Set("ButtonHover", "#EBEBF4");
            Set("ButtonPressed", "#E4E4EE");
            Set("ScrollThumb", "#C8C8D8");
            Set("Accent", "#6B55F5");
            SetGradient("AccentGradient", "#7B6AFF", "#5B4DE0");
        }

        ThemeButton.Content = dark ? "◐" : "◑";
    }

    private static void Set(string key, string hex)
    {
        WpfApplication.Current.Resources[key] =
            new WpfSolidColorBrush(
                (WpfColor)WpfColorConverter.ConvertFromString(hex));
    }

    private static void SetGradient(string key, string from, string to)
    {
        WpfApplication.Current.Resources[key] =
            new WpfLinearGradientBrush(
                (WpfColor)WpfColorConverter.ConvertFromString(from),
                (WpfColor)WpfColorConverter.ConvertFromString(to),
                angle: 135);
    }
}

// ── Model ─────────────────────────────────────────────────────────────────────
public sealed class ClipItem
{
    public string Icon { get; }
    public string Title { get; }
    public string Preview { get; }
    public string TimeLabel { get; }
    public string PinLabel { get; }
    public bool IsPinned { get; }
    public string Content { get; }
    public string ImagePath { get; }
    public bool IsImage { get; }

    public Visibility PinVisibility =>
        string.IsNullOrEmpty(PinLabel) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TextVisibility =>
        IsImage ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ImageVisibility =>
        IsImage ? Visibility.Visible : Visibility.Collapsed;

    public ClipItem(
        string icon, string title, string preview,
        string timeLabel, string pinLabel, bool isPinned,
        string content, string imagePath = "", bool isImage = false)
    {
        Icon = icon;
        Title = title;
        Preview = preview;
        TimeLabel = timeLabel;
        PinLabel = pinLabel;
        IsPinned = isPinned;
        Content = content;
        ImagePath = imagePath;
        IsImage = isImage;
    }
}