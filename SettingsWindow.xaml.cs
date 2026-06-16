using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MessageBox = System.Windows.MessageBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace ClipFlow;

public partial class SettingsWindow : Window
{
    private readonly MainWindow _mainWindow;
    private bool _loading = true;

    public SettingsWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Load current settings into UI
        AutoStartToggle.IsChecked = AutoStartService.IsEnabled();
        DarkThemeToggle.IsChecked = SettingsService.Current.DarkTheme;
        PauseToggle.IsChecked = SettingsService.Current.MonitoringPaused;
        DataFolderPath.Text = SettingsService.GetDataFolder();

        // History limit
        SelectComboByTag(HistoryLimitCombo, SettingsService.Current.MaxHistoryItems.ToString());

        // Auto-clear
        SelectComboByTag(AutoClearCombo, SettingsService.Current.AutoClearDays.ToString());

        SensitiveBlockerToggle.IsChecked = SettingsService.Current.BlockSensitiveContent;
        RefreshExcludedApps();

        _loading = false;
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AutoStartToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        if (AutoStartToggle.IsChecked == true)
            AutoStartService.Enable();
        else
            AutoStartService.Disable();

        // Update toggle to actual state in case it failed
        AutoStartToggle.IsChecked = AutoStartService.IsEnabled();
    }

    private void DarkThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        SettingsService.Current.DarkTheme = DarkThemeToggle.IsChecked == true;
        SettingsService.Save();

        _mainWindow.ApplyThemeFromSettings();
    }

    private void SensitiveBlockerToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        SettingsService.Current.BlockSensitiveContent = SensitiveBlockerToggle.IsChecked == true;
        SettingsService.Save();
    }

    private void AddCurrentAppButton_Click(object sender, RoutedEventArgs e)
    {
        // Give the user time to switch focus before we capture
        var result = MessageBox.Show(
            "After clicking OK, switch to the app you want to exclude.\n\n" +
            "You have 3 seconds to focus that app before it gets added.",
            "Add app to exclusions",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.OK) return;

        // Wait so user can switch apps
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();

            string app = AppExclusionService.GetActiveProcessName();

            if (string.IsNullOrWhiteSpace(app))
            {
                MessageBox.Show("Could not detect the active app.",
                    "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Skip if it's our own app
            if (app.Equals("ClipFlow", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot exclude ClipFlow itself.",
                    "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!SettingsService.Current.ExcludedApps.Contains(app, StringComparer.OrdinalIgnoreCase))
            {
                SettingsService.Current.ExcludedApps.Add(app);
                SettingsService.Save();
                RefreshExcludedApps();

                MessageBox.Show($"\"{app}\" added to excluded apps.",
                    "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"\"{app}\" is already excluded.",
                    "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        timer.Start();
    }

    private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.Tag is not string app) return;

        SettingsService.Current.ExcludedApps.RemoveAll(
            x => x.Equals(app, StringComparison.OrdinalIgnoreCase));
        SettingsService.Save();
        RefreshExcludedApps();
    }

    private void RefreshExcludedApps()
    {
        ExcludedAppsList.ItemsSource = null;
        ExcludedAppsList.ItemsSource = SettingsService.Current.ExcludedApps;

        NoExclusionsText.Visibility =
            SettingsService.Current.ExcludedApps.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void HistoryLimit_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        if (HistoryLimitCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int limit))
        {
            SettingsService.Current.MaxHistoryItems = limit;
            SettingsService.Save();
        }
    }

    private void AutoClear_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        if (AutoClearCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int days))
        {
            SettingsService.Current.AutoClearDays = days;
            SettingsService.Save();
        }
    }

    private void PauseToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        bool paused = PauseToggle.IsChecked == true;
        SettingsService.Current.MonitoringPaused = paused;
        SettingsService.Save();

        _mainWindow.SetMonitoringPaused(paused);
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to permanently delete all clipboard history?\n\nThis cannot be undone.",
            "Clear all history",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _mainWindow.ClearHistory();
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SettingsService.GetDataFolder(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open folder.\n\n{ex.Message}",
                "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}