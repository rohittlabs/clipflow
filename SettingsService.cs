using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClipFlow;

public sealed class AppSettings
{
    public bool         DarkTheme         { get; set; } = true;
    public int          MaxHistoryItems   { get; set; } = 200;
    public int          AutoClearDays     { get; set; } = 0;
    public bool         MonitoringPaused  { get; set; } = false;

    // New privacy settings
    public List<string> ExcludedApps      { get; set; } = new()
    {
        "1Password",
        "Bitwarden",
        "KeePass",
        "KeePassXC",
        "LastPass",
        "Dashlane",
        "Enpass"
    };
    public bool         BlockSensitiveContent { get; set; } = true;
}

public static class SettingsService
{
    private static readonly string Folder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipFlow");

    private static readonly string FilePath =
        Path.Combine(Folder, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Current = new AppSettings();
                Save();
                return;
            }

            string json = File.ReadAllText(FilePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string json = JsonSerializer.Serialize(Current,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public static string GetDataFolder() => Folder;
}