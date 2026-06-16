using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;

namespace ClipFlow;

public sealed class DatabaseService : IDisposable
{
    private static readonly string DbFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipFlow");

    private static readonly string ImagesFolder =
        Path.Combine(DbFolder, "images");

    private static readonly string DbPath =
        Path.Combine(DbFolder, "history.db");

    private readonly SqliteConnection _connection;

    public DatabaseService()
    {
        Directory.CreateDirectory(DbFolder);
        Directory.CreateDirectory(ImagesFolder);

        _connection = new SqliteConnection($"Data Source={DbPath}");
        _connection.Open();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS ClipboardItems (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            Icon        TEXT    NOT NULL DEFAULT '📝',
            Title       TEXT    NOT NULL,
            Preview     TEXT    NOT NULL,
            Content     TEXT    NOT NULL,
            ImagePath   TEXT    NOT NULL DEFAULT '',
            IsImage     INTEGER NOT NULL DEFAULT 0,
            TimeLabel   TEXT    NOT NULL DEFAULT 'Just now',
            IsPinned    INTEGER NOT NULL DEFAULT 0,
            CreatedAt   TEXT    NOT NULL,
            Hash        TEXT    NOT NULL UNIQUE
        );

        CREATE INDEX IF NOT EXISTS idx_created
            ON ClipboardItems(CreatedAt DESC);

        CREATE INDEX IF NOT EXISTS idx_pinned
            ON ClipboardItems(IsPinned DESC);
        """;
        cmd.ExecuteNonQuery();

        // Auto-migration: add columns if they don't exist
        // This handles upgrading old databases safely
        EnsureColumn("ImagePath", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn("IsImage", "INTEGER NOT NULL DEFAULT 0");
    }

    private void EnsureColumn(string columnName, string definition)
    {
        // Check if column exists
        using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "PRAGMA table_info(ClipboardItems);";

        bool exists = false;
        using (var reader = checkCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists) return;

        // Add the missing column
        using var addCmd = _connection.CreateCommand();
        addCmd.CommandText = $"ALTER TABLE ClipboardItems ADD COLUMN {columnName} {definition};";
        addCmd.ExecuteNonQuery();
    }

    // ── Image saving ──────────────────────────────────────────────────────────

    public string SaveImageToFile(BitmapSource image)
    {
        string fileName = $"clip_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png";
        string filePath = Path.Combine(ImagesFolder, fileName);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));

        using var stream = File.Create(filePath);
        encoder.Save(stream);

        return filePath;
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    public void SaveItem(ClipItem item)
    {
        string hash = ComputeHash(item.IsImage ? item.ImagePath : item.Content);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ClipboardItems
                (Icon, Title, Preview, Content, ImagePath, IsImage,
                 TimeLabel, IsPinned, CreatedAt, Hash)
            VALUES
                ($icon, $title, $preview, $content, $imagePath, $isImage,
                 $timeLabel, $isPinned, $createdAt, $hash)
            ON CONFLICT(Hash) DO UPDATE SET
                CreatedAt = $createdAt,
                TimeLabel = $timeLabel;
            """;

        cmd.Parameters.AddWithValue("$icon", item.Icon);
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$preview", item.Preview);
        cmd.Parameters.AddWithValue("$content", item.Content);
        cmd.Parameters.AddWithValue("$imagePath", item.ImagePath);
        cmd.Parameters.AddWithValue("$isImage", item.IsImage ? 1 : 0);
        cmd.Parameters.AddWithValue("$timeLabel", item.TimeLabel);
        cmd.Parameters.AddWithValue("$isPinned", item.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$hash", hash);

        cmd.ExecuteNonQuery();
    }

    public void UpdatePin(string content, bool isPinned)
    {
        string hash = ComputeHash(content);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ClipboardItems SET IsPinned = $isPinned WHERE Hash = $hash;
            """;
        cmd.Parameters.AddWithValue("$isPinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.ExecuteNonQuery();
    }

    public void DeleteItem(string content)
    {
        string hash = ComputeHash(content);

        // If it's an image, delete the file too
        using var selectCmd = _connection.CreateCommand();
        selectCmd.CommandText = """
            SELECT ImagePath, IsImage FROM ClipboardItems WHERE Hash = $hash;
            """;
        selectCmd.Parameters.AddWithValue("$hash", hash);
        using var reader = selectCmd.ExecuteReader();
        if (reader.Read())
        {
            bool isImage = reader.GetInt32(1) == 1;
            if (isImage)
            {
                string path = reader.GetString(0);
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            }
        }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardItems WHERE Hash = $hash;";
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.ExecuteNonQuery();
    }

    public void ClearAll()
    {
        // Delete all image files
        try
        {
            foreach (var file in Directory.GetFiles(ImagesFolder))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardItems;";
        cmd.ExecuteNonQuery();
    }

    public void ClearUnpinned()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardItems WHERE IsPinned = 0;";
        cmd.ExecuteNonQuery();
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public List<ClipItem> LoadAll(int limit = 200)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Icon, Title, Preview, Content, ImagePath, IsImage,
                   TimeLabel, IsPinned
            FROM ClipboardItems
            ORDER BY IsPinned DESC, CreatedAt DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var results = new List<ClipItem>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string imagePath = reader.GetString(4);
            bool isImage = reader.GetInt32(5) == 1;

            // Skip image entries whose files no longer exist
            if (isImage && !File.Exists(imagePath))
                continue;

            results.Add(new ClipItem(
                icon: reader.GetString(0),
                title: reader.GetString(1),
                preview: reader.GetString(2),
                content: reader.GetString(3),
                timeLabel: reader.GetString(6),
                pinLabel: reader.GetInt32(7) == 1 ? "📌" : "",
                isPinned: reader.GetInt32(7) == 1,
                imagePath: imagePath,
                isImage: isImage));
        }

        return results;
    }

    public int GetTotalCount()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ClipboardItems;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void TrimToLimit(int limit = 200)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM ClipboardItems
            WHERE IsPinned = 0
            AND Id NOT IN (
                SELECT Id FROM ClipboardItems
                WHERE IsPinned = 0
                ORDER BY CreatedAt DESC
                LIMIT $limit
            );
            """;
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.ExecuteNonQuery();
    }

    private static string ComputeHash(string content)
        => content.GetHashCode(StringComparison.Ordinal).ToString();

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}