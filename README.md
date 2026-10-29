# M4W

Clipboard history for Windows that lives in the system tray. Every copy is recorded, searchable, and one keystroke away from being pasted again, at the desk or over Remote Desktop.

Inspired by [Maccy](https://github.com/p0deje/Maccy) for macOS, not affiliated.

## What it does

- Records every **text** and **image** you copy in your Windows session. Re-copying something moves it to the top instead of adding a duplicate.
- Press **Ctrl+Alt+V** anywhere: a popup opens next to the cursor with the search box focused. Pick an item and it is pasted straight into the window you were in.
- Fuzzy search by default (`kbctl` finds `kubectl`), with an exact-substring toggle.
- Keeps the last 1000 items. Pinned items are never dropped.
- One portable `M4W.exe`. No installer, no admin rights.

## Install

1. Download `M4W.exe` from the [latest release](../../releases/latest) and put it somewhere permanent (e.g. `C:\Tools\M4W\`).
2. Double-click it. Windows SmartScreen will say "Windows protected your PC" because the exe is not code-signed: click **More info → Run anyway**.
3. A blue **M** appears in the tray. On first run M4W also turns on *Start with Windows*; toggle it from the tray menu.

If you move the exe, launch it once from the new place: the autostart entry repairs itself to point there.

## Keys

| Where | Key | Does |
|---|---|---|
| Anywhere | `Ctrl+Alt+V` | Open the popup (again to close) |
| Popup | type | Filter (fuzzy, or exact when toggled) |
| Popup | `↑` `↓` `PgUp` `PgDn` | Move selection |
| Popup | `Enter` / double-click | Put on clipboard and paste into the previous window |
| Popup | `Alt+P` | Pin / unpin (pinned items survive Clear and the item limit) |
| Popup | `Delete` | Remove the item (when the cursor is at the end of the search text) |
| Popup | `Alt+E` | Toggle exact-substring search |
| Popup | `Esc` | Close |

Tray: left-click opens the popup. Right-click: Open history, Pause capture, Clear history (unpinned), Start with Windows, About, Quit. Launching `M4W.exe` a second time opens the popup of the running copy.

## Remote Desktop

M4W runs in your Windows user session. If you RDP into the same account, the session (and M4W) carries on, and RDP's clipboard sharing feeds what you copy on your Mac into the Windows clipboard, where M4W records it like any other copy. The clipboard is often briefly locked while RDP syncs, so M4W retries a few times before giving up (and logs it).

## Where data lives

Everything is under `%LOCALAPPDATA%\M4W\`:

| File | Content |
|---|---|
| `history.db` | SQLite history, every item encrypted |
| `settings.json` | Your settings (below) |
| `m4w.log` | Small rolling log. Never contains clipboard content |

`settings.json` (all values optional; a bad value falls back to its default and is logged):

```json
{
  "Hotkey": "Ctrl+Alt+V",
  "MaxItems": 1000,
  "FuzzyByDefault": true,
  "AutoPaste": true,
  "RespectPrivateFlag": false
}
```

Hotkey is modifiers (`Ctrl`, `Alt`, `Shift`, `Win`) plus one letter, digit or `F1`–`F24`. Restart M4W after editing.

## Security note

M4W records **everything you copy, including passwords from password managers**. That is deliberate. Set `"RespectPrivateFlag": true` to skip copies that password managers mark as private.

History is encrypted at rest with Windows DPAPI, tied to your Windows login: the database is useless on another machine or under another account. It is not protected from software running as you, which can read your clipboard anyway. Use *Clear history* from the tray, or pin nothing sensitive.

## Build

Requires the .NET 10 SDK.

```
dotnet build M4W.sln
dotnet test tests/M4W.Core.Tests
```

The WPF app builds on macOS/Linux too (`EnableWindowsTargeting`), but only runs on Windows. Pushing a `v*` tag makes GitHub Actions publish a self-contained single-file `M4W.exe` and attach it to a release.

License: MIT
