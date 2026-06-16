<div align="center">

# ClipFlow

A beautiful, lightweight clipboard manager for Windows

[Download](#download) • [Features](#features) • [Screenshots](#screenshots) • [Privacy](#privacy)

</div>

---

## Features

- 📋 **Automatic clipboard history** — text and images
- 🔍 **Instant search** — find any past clipboard item
- 📌 **Pin important items** — keep snippets always accessible
- 🌓 **Dark & light themes** — beautiful in both modes
- ⌨️ **Global hotkey** — `Ctrl + Shift + V` from anywhere
- 🛡️ **Privacy first** — blocks passwords, API keys, credit cards automatically
- 🚫 **App exclusions** — never save from password managers
- 🚀 **Auto start** — runs quietly with Windows
- 💾 **Persistent storage** — clipboard history survives restarts
- 🎨 **Polished UI** — smooth animations and glassmorphism

## Download

Get the latest installer from the [Releases page](../../releases).

System requirements:
- Windows 10 or Windows 11
- 100 MB free disk space

## Usage

1. Install and launch ClipFlow
2. Copy anything as usual (`Ctrl + C`)
3. Press `Ctrl + Shift + V` to open the clipboard history
4. Click or press Enter on any item to copy it back
5. Use `Ctrl + P` to pin frequently used items

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + V` | Open ClipFlow |
| `↑ / ↓` | Navigate items |
| `Enter` | Copy selected item |
| `Delete` | Remove selected item |
| `Ctrl + P` | Pin / unpin item |
| `Ctrl + T` | Toggle dark / light theme |
| `Esc` | Close popup |

## Privacy

ClipFlow takes privacy seriously:

- All data is stored **locally** in `%AppData%\ClipFlow`
- **No cloud sync, no telemetry, no analytics**
- Automatically blocks credit cards, passwords, API keys, and JWT tokens
- Pre-excludes popular password managers (1Password, Bitwarden, KeePass, etc.)
- Pause monitoring anytime from the tray menu

## Built With

- C# / .NET 8
- WPF
- SQLite
- NHotkey

## Development

```bash
# Clone the repo
git clone https://github.com/yourusername/clipflow.git
cd clipflow

# Run it
dotnet run

# Build release
dotnet publish -c Release -r win-x64 --self-contained true -o publish-standalone