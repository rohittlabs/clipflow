using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ClipFlow;

public static class AppExclusionService
{
    // ── Windows API ───────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    // ── Public API ────────────────────────────────────────────────────────────

    // Returns the process name of the active window (e.g. "chrome", "Bitwarden")
    public static string GetActiveProcessName()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "";

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "";

            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName ?? "";
        }
        catch
        {
            return "";
        }
    }

    // Returns the window title of the active window
    public static string GetActiveWindowTitle()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return "";

            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    // Check if the current foreground app should be excluded
    public static bool IsCurrentAppExcluded()
    {
        if (SettingsService.Current.ExcludedApps.Count == 0)
            return false;

        string currentApp = GetActiveProcessName();
        if (string.IsNullOrEmpty(currentApp)) return false;

        foreach (string excluded in SettingsService.Current.ExcludedApps)
        {
            if (currentApp.Equals(excluded, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}