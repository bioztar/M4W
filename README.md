# M4W

Clipboard history for Windows that lives in the system tray. Every copy is recorded, searchable, and one keystroke away from being pasted again, at the desk or over Remote Desktop.

Inspired by [Maccy](https://github.com/p0deje/Maccy) for macOS, not affiliated.

## What it does

- Records every **text** and **image** you copy in your Windows session. Re-copying something moves it to the top instead of adding a duplicate.
- Press **Ctrl+Alt+V** anywhere: a popup opens next to the cursor with the search box focused. Pick an item and it is pasted straight into the window you were in.
- Fuzzy search by default (`kbctl` finds `kubectl`), with an exact-substring toggle.
- Keeps the last 1000 items. Pinned items are never dropped.
- **Opt-in typing capture**: turn it on from the tray before filling a long form, and what you *type* (not just copy) is saved too, split one entry per field. Off by default, auto-off after idle, never recorded for password fields.
- **Snippets**: saved values behind their own global hotkey (e.g. your email address, or a password), filled into whatever field has focus. Edited from the in-app Settings window.
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

Tray: left-click opens the popup. Right-click: Open history, Pause capture, Capture typing, Clear history (unpinned), Settings…, Start with Windows, About, Quit. Launching `M4W.exe` a second time opens the popup of the running copy.

## Typing capture

Off by default. Tick **Capture typing** in the tray before filling a long form: the tray icon gets a red dot and the tooltip says "M4W — capturing typing". From then on, what you type is split into its own history entry on Enter, Tab, switching fields, or ~2 seconds of no typing, and stored with a small **⌨** marker in the popup so you can tell it apart from a copy. A chunk under 3 characters, or empty/whitespace, isn't stored.

Untick it (tray, or it turns itself off after 30 minutes idle — with a one-time balloon) and the keyboard hook is removed immediately; nothing typed before or after is in `m4w.log`. A focused password field is never recorded while capturing (`TypingSkipPasswordFields`, on by default) — if the password-field check itself fails for any reason, M4W treats the field as a password and skips it rather than risk recording one.

## Snippets

A snippet is a saved value behind its own global hotkey: press it anywhere, and the value fills into the field you're in (clipboard + auto-paste). Two are seeded empty on first run — give them a value from Settings:

| Name | Hotkey | Secret |
|---|---|---|
| Email | `Ctrl+Shift+R` | No |
| Password | `Ctrl+Shift+E` | Yes |

A secret snippet's value renders masked (`••••••••`) in Settings until you press **Reveal**. Filling a snippet never touches history or the log: the value only ever goes briefly onto the clipboard (tagged so M4W's own capture ignores it), and whatever was on the clipboard before is put back about 300ms after the paste — or cleared, if it can't be restored exactly — so nothing lingers.

Open **Settings…** from the tray to add, edit or delete snippets, set their hotkey (click the box, press the keys), and change the main popup hotkey and every other setting below. Save checks for hotkey clashes and bad values before applying; Cancel discards every edit.

## Remote Desktop

M4W runs in your Windows user session. If you RDP into the same account, the session (and M4W) carries on, and RDP's clipboard sharing feeds what you copy on your Mac into the Windows clipboard, where M4W records it like any other copy. The clipboard is often briefly locked while RDP syncs, so M4W retries a few times before giving up (and logs it).

## Where data lives

Everything is under `%LOCALAPPDATA%\M4W\`:

| File | Content |
|---|---|
| `history.db` | SQLite history, every item encrypted |
| `settings.json` | Your settings (below) |
| `m4w.log` | Small rolling log. Never contains clipboard content |

`settings.json` (all values optional; a bad value falls back to its default and is logged) — edit it from the tray's **Settings…** window, or by hand:

```json
{
  "Hotkey": "Ctrl+Alt+V",
  "MaxItems": 1000,
  "FuzzyByDefault": true,
  "AutoPaste": true,
  "RespectPrivateFlag": false,
  "TypingIdleOffMinutes": 30,
  "TypingSkipPasswordFields": true,
  "MinTypedChars": 3,
  "IdleFlushSeconds": 2
}
```

| Setting | Default | Meaning |
|---|---|---|
| `Hotkey` | `Ctrl+Alt+V` | Opens the popup |
| `MaxItems` | `1000` | History items kept (10–100000); pinned items are never dropped |
| `FuzzyByDefault` | `true` | Popup search starts in fuzzy mode |
| `AutoPaste` | `true` | Ctrl+V is sent automatically after picking an item |
| `RespectPrivateFlag` | `false` | Skip copies password managers mark private |
| `TypingIdleOffMinutes` | `30` | Typing capture turns itself off after this long with no keystrokes (1–1440) |
| `TypingSkipPasswordFields` | `true` | Never record a focused password field while capturing typing |
| `MinTypedChars` | `3` | A typed chunk shorter than this is discarded (0–1000) |
| `IdleFlushSeconds` | `2` | Seconds of no typing before the current chunk is stored as its own entry (1–300) |

`CaptureTyping` is **not** a persisted setting: the tray's "Capture typing" toggle is always off at launch, on purpose, so a value saved as `true` in the file is ignored — same reason it has no field in the Settings window.

Hotkey is modifiers (`Ctrl`, `Alt`, `Shift`, `Win`) plus one letter, digit or `F1`–`F24`. Settings-window changes apply immediately; hand edits to `settings.json` need a restart.

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
