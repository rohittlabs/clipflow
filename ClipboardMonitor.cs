using System;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using WpfClipboard = System.Windows.Clipboard;

namespace ClipFlow;

public sealed class ClipboardMonitor : IDisposable
{
    public event Action<string>? TextCopied;
    public event Action<BitmapSource>? ImageCopied;

    private DispatcherTimer? _timer;
    private string _lastText = string.Empty;
    private int _lastImageHash;
    private bool _suppressNext;
    private bool _debugMode = false;

    public void Start()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += CheckClipboard;
        _timer.Start();

        if (_debugMode)
            MessageBox("🔍 Clipboard Monitor Started");
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void SuppressNext()
    {
        _suppressNext = true;
    }

    private void CheckClipboard(object? sender, EventArgs e)
    {
        try
        {
            if (_suppressNext)
            {
                _suppressNext = false;
                UpdateLastValues();
                return;
            }

            // Privacy: skip if monitoring is paused
            if (SettingsService.Current.MonitoringPaused)
                return;

            // Privacy: skip if the active app is in exclusion list
            if (AppExclusionService.IsCurrentAppExcluded())
            {
                UpdateLastValues();
                return;
            }

            // Image check
            if (WpfClipboard.ContainsImage())
            {
                var image = WpfClipboard.GetImage();
                if (image != null)
                {
                    int hash = GetImageHash(image);
                    if (hash != _lastImageHash)
                    {
                        _lastImageHash = hash;
                        ImageCopied?.Invoke(image);
                        return;
                    }
                }
            }

            // Text check
            if (WpfClipboard.ContainsText())
            {
                string text = WpfClipboard.GetText();

                if (!string.IsNullOrWhiteSpace(text) && text != _lastText)
                {
                    _lastText = text;

                    // Privacy: skip sensitive content if enabled
                    if (SettingsService.Current.BlockSensitiveContent)
                    {
                        var sensitivity = SensitiveDetector.Detect(text);
                        if (sensitivity != SensitiveDetector.SensitiveType.None)
                        {
                            // Don't save sensitive items
                            return;
                        }
                    }

                    TextCopied?.Invoke(text);
                }
            }
        }
        catch
        {
            // Clipboard locked — skip
        }
    }

    private void UpdateLastValues()
    {
        try
        {
            if (WpfClipboard.ContainsText())
                _lastText = WpfClipboard.GetText() ?? "";

            if (WpfClipboard.ContainsImage())
            {
                var img = WpfClipboard.GetImage();
                if (img != null)
                    _lastImageHash = GetImageHash(img);
            }
        }
        catch { }
    }

    private static int GetImageHash(BitmapSource image)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + image.PixelWidth;
            hash = hash * 31 + image.PixelHeight;
            hash = hash * 31 + image.Format.BitsPerPixel;

            try
            {
                int stride = image.PixelWidth * ((image.Format.BitsPerPixel + 7) / 8);
                byte[] pixels = new byte[Math.Min(stride, 128)];
                image.CopyPixels(new System.Windows.Int32Rect(0, 0, Math.Min(image.PixelWidth, 48), 1), pixels, stride, 0);

                for (int i = 0; i < pixels.Length; i++)
                    hash = hash * 31 + pixels[i];
            }
            catch { }

            return hash;
        }
    }

    private static void MessageBox(string message)
    {
        System.Windows.MessageBox.Show(message, "ClipFlow Debug", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    public void Dispose() => Stop();
}