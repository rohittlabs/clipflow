using System;
using System.IO;
using Microsoft.Win32;

namespace ClipFlow;

public static class AutoStartService
{
    private const string AppName = "ClipFlow";
    private const string RunKey  = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Get the actual ClipFlow executable path
    // When running with "dotnet run", Environment.ProcessPath returns dotnet.exe
    // We need to find ClipFlow.exe in the output folder
    private static string? GetAppExePath()
    {
        // First try Environment.ProcessPath
        string? path = Environment.ProcessPath;

        if (!string.IsNullOrEmpty(path) &&
            Path.GetFileNameWithoutExtension(path)
                .Equals("ClipFlow", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        // Fall back to looking for ClipFlow.exe in the assembly folder
        string? assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            string? folder = Path.GetDirectoryName(assemblyLocation);
            if (folder != null)
            {
                string exePath = Path.Combine(folder, "ClipFlow.exe");
                if (File.Exists(exePath))
                    return exePath;
            }
        }

        // Last fallback: AppContext.BaseDirectory
        string baseDir = AppContext.BaseDirectory;
        string fallback = Path.Combine(baseDir, "ClipFlow.exe");
        if (File.Exists(fallback))
            return fallback;

        return null;
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            if (key == null) return false;

            string? value = key.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool Enable()
    {
        try
        {
            string? exePath = GetAppExePath();
            if (string.IsNullOrEmpty(exePath))
                return false;

            // CreateSubKey ensures the key exists and is writable
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key == null) return false;

            key.SetValue(AppName, $"\"{exePath}\"");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return false;

            if (key.GetValue(AppName) != null)
                key.DeleteValue(AppName, throwOnMissingValue: false);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Toggle()
    {
        if (IsEnabled())
        {
            return !Disable();  // After disable, IsEnabled is false
        }
        else
        {
            return Enable();   // After enable, IsEnabled is true
        }
    }

    // Diagnostic — returns the path that would be registered
    public static string GetDetectedPath()
        => GetAppExePath() ?? "(not found)";
}