using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipFlow;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private NotifyIcon? _trayIcon;
    private ClipboardMonitor? _clipboardMonitor;
    private DatabaseService? _database;
    private Mutex? _mutex;

    // Tray menu items that need to be updated
    private ToolStripMenuItem? _autoStartMenuItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, "ClipFlow_SingleInstance", out bool createdNew);
        // Load settings before anything else
        SettingsService.Load();
        if (!createdNew)
        {
            MessageBox.Show(
                "ClipFlow is already running.\nCheck your system tray.",
                "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _database = new DatabaseService();
        _clipboardMonitor = new ClipboardMonitor();
        _mainWindow = new MainWindow(_clipboardMonitor, _database);

        _clipboardMonitor.TextCopied += text =>
            _mainWindow.AddClipboardItem(text);

        _clipboardMonitor.ImageCopied += image =>
            _mainWindow.AddImageItem(image);

        _clipboardMonitor.Start();
        BuildTrayIcon();

        try
        {
            NHotkey.Wpf.HotkeyManager.Current.AddOrReplace(
                "OpenClipFlow",
                System.Windows.Input.Key.V,
                System.Windows.Input.ModifierKeys.Control |
                System.Windows.Input.ModifierKeys.Shift,
                OnHotkeyPressed);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not register hotkey Ctrl+Shift+V.\n\n{ex.Message}",
                "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnHotkeyPressed(object? sender, NHotkey.HotkeyEventArgs e)
    {
        if (_mainWindow == null) return;
        if (_mainWindow.IsVisible) _mainWindow.HideWindow();
        else _mainWindow.ShowWindow();
        e.Handled = true;
    }

    private void BuildTrayIcon()
    {
        System.Drawing.Icon icon;
        try
        {
            string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appicon.ico");
            icon = System.IO.File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : SystemIcons.Application;
        }
        catch
        {
            icon = SystemIcons.Application;
        }

        _trayIcon = new NotifyIcon
        {
            Text = "ClipFlow — Clipboard Manager",
            Icon = SystemIcons.Application,
            Visible = true
        };

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("📋  Open ClipFlow");
        openItem.Font = new Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => _mainWindow?.ShowWindow();
        menu.Items.Add(openItem);

        menu.Items.Add(new ToolStripSeparator());

        // Settings
        var settingsItem = new ToolStripMenuItem("⚙  Settings");
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        // Auto-start
        _autoStartMenuItem = new ToolStripMenuItem("🚀  Start with Windows")
        {
            Checked = AutoStartService.IsEnabled()
        };
        _autoStartMenuItem.Click += (_, _) => ToggleAutoStart();
        menu.Items.Add(_autoStartMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        var clearItem = new ToolStripMenuItem("🗑  Clear History");
        clearItem.Click += (_, _) => _mainWindow?.ClearHistory();
        menu.Items.Add(clearItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("✕  Exit");
        exitItem.Click += (_, _) => ExitApp();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) =>
        {
            if (_mainWindow == null) return;
            if (_mainWindow.IsVisible) _mainWindow.HideWindow();
            else _mainWindow.ShowWindow();
        };
    }

    private void OpenSettings()
    {
        if (_mainWindow == null) return;

        var settings = new SettingsWindow(_mainWindow);
        settings.ShowDialog();
    }

    private void ToggleAutoStart()
    {
        bool wasEnabled = AutoStartService.IsEnabled();

        bool success;
        if (wasEnabled)
            success = AutoStartService.Disable();
        else
            success = AutoStartService.Enable();

        // Re-check the actual state from the registry
        bool nowEnabled = AutoStartService.IsEnabled();

        if (_autoStartMenuItem != null)
            _autoStartMenuItem.Checked = nowEnabled;

        string message;
        if (!success)
        {
            message = $"Failed to update auto-start.\n\nPath: {AutoStartService.GetDetectedPath()}";
        }
        else if (nowEnabled)
        {
            message = $"ClipFlow will start automatically with Windows.\n\nPath: {AutoStartService.GetDetectedPath()}";
        }
        else
        {
            message = "ClipFlow will no longer start automatically.";
        }

        // Use MessageBox instead of balloon — balloons can be disabled in Windows 11
        MessageBox.Show(
            message,
            "ClipFlow Auto-Start",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ExitApp()
    {
        _clipboardMonitor?.Stop();
        _database?.Dispose();
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _clipboardMonitor?.Stop();
        _database?.Dispose();
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}