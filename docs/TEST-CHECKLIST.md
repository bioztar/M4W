# M4W manual test checklist (real Windows PC)

The WPF/Win32 side could not be run during development (built on macOS). Everything below needs a real Windows session. Tick each box; note what failed and what you saw. Logs: `%LOCALAPPDATA%\M4W\m4w.log`.

Setup: download `M4W.exe` from the release, put it in `C:\Tools\M4W\`, double-click, accept SmartScreen (More info → Run anyway).

- [ ] **1. Start and tray.** A blue **M** icon is in the tray (check the overflow arrow, drag it out to pin it). Hover: tooltip "M4W — clipboard history". Right-click shows: Open history, Pause capture, Clear history (unpinned), Start with Windows (ticked on first run), About M4W, Quit.
- [ ] **2. Local copy and popup.** In Notepad type `alpha one`, select, `Ctrl+C`. Copy a second line `beta two` the same way. Press **Ctrl+Alt+V**. Popup opens near the mouse, search box focused, `beta two` on top, `alpha one` below. Esc closes it. Tray left-click opens it too.
- [ ] **3. Search.** Open the popup, type `bt`: `beta two` stays, `alpha one` goes (fuzzy). Tick **Exact** (or `Alt+E`), type `bt` again: nothing matches. Clear the box, type `alpha`: matches. Typing feels instant.
- [ ] **4. Fuzzy on real text.** Copy the word `kubectl` from anywhere. Popup, type `kbctl`: it is found.
- [ ] **5. Auto-paste.** Open a fresh Notepad document, click into it, press Ctrl+Alt+V, arrow to `alpha one`, **Enter**. The text appears in Notepad without you pressing Ctrl+V, and Notepad kept focus. Then check M4W did not add a duplicate: `alpha one` moved to the top, still one copy.
- [ ] **6. Images.** Press `Win+Shift+S`, snip a region (or right-click an image in a browser → Copy image). Open the popup: a thumbnail row appears on top. Select it, Enter into Paint or a Word/Outlook body: the image pastes.
- [ ] **7. Pin survives Clear.** In the popup select `beta two`, press `Alt+P` (📌 appears). Tray → Clear history (unpinned) → Yes. Open the popup: only the pinned item remains. `Alt+P` again unpins; `Delete` (empty search box) removes the selected item.
- [ ] **8. Remote Desktop from the Mac.** RDP into the PC (same Windows account), with clipboard sharing on. (a) Copy text **on the Mac**, then Ctrl+Alt+V inside the RDP window: it is in the history. (b) Copy text **inside the RDP session**, paste on the Mac with Cmd+V: it arrives. (c) Disconnect and reconnect: the tray icon is still there and old items are still listed. Anything odd (missed copies, duplicates, errors in the log) goes here. Note whether Ctrl+Alt+V is intercepted by the RDP client on the Mac; if so, change `Hotkey` in `settings.json` to e.g. `Ctrl+Shift+V` and restart.
- [ ] **9. Autostart.** With *Start with Windows* ticked, sign out and in (or reboot). The tray icon is back without you launching anything, and history is intact. Untick it in the tray menu, reboot once more: no M4W. (Registry check: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `M4W`.)
- [ ] **10. Quit really exits.** Tray → Quit. The icon disappears immediately (no ghost) and `M4W.exe` is gone from Task Manager → Details. Launch it again, then launch `M4W.exe` a **second** time: no second tray icon, and the popup opens instead.

Extras worth a look while you are there:

- [ ] **Pause capture:** tick it, copy something, untick, open the popup: the item copied while paused is not there; the tray icon is grey while paused.
- [ ] **Multi-monitor / scaling:** open the popup with the mouse on a second monitor or a different DPI scale. It should land next to the cursor, fully on screen.
- [ ] **Password manager copy** (e.g. Bitwarden/1Password): it is recorded. Set `"RespectPrivateFlag": true`, restart, copy again: it is skipped.
- [ ] **Elevated target:** paste into an app running as Administrator. Windows blocks the synthetic Ctrl+V (log says "SendInput was rejected"); the item is still on the clipboard for a manual Ctrl+V.
- [ ] **Bad settings:** put `{ not json` in `settings.json`, restart: M4W starts with defaults and the log has a "settings" line.

## Typing capture (v0.2.0)

**Not exercisable on macOS** — the low-level keyboard hook (`SetWindowsHookEx WH_KEYBOARD_LL`), `ToUnicodeEx` char translation, and `SetWinEventHook` foreground-change detection are Windows-only and untested until this list is run on a real PC.

- [ ] **Toggle on.** Tray → Capture typing. The tray icon gets a small red dot and the tooltip reads "M4W — capturing typing".
- [ ] **Notepad, one field.** Type a sentence, press nothing else. After ~2 seconds idle, open the popup: the sentence is there with a small **⌨** marker, distinguishing it from a copy.
- [ ] **Browser form, several fields.** Fill a multi-field form (name, email, address…), pressing Tab between fields. Open the popup: each field is its own entry, each marked ⌨, in the order typed.
- [ ] **Backspace.** Type `hello`, backspace twice, type `p`: the stored chunk is `help`, not `hellp` and not two entries.
- [ ] **Password field IS recorded by default.** Type into a password box (with `TypingSkipPasswordFields` at its default `false`): the value shows up in history with the ⌨ marker, exactly as typed (right case, right characters) — this is by design, not a bug.
- [ ] **Skip-password setting works when turned on.** Set `TypingSkipPasswordFields` to `true` via Settings, type into a password box: nothing from that field appears in history, and the log has a `typed chunk skipped (password field)` line with no content.
- [ ] **Toggle off.** Tray → Capture typing (unticked). Icon returns to normal immediately; type afterward and nothing new is captured.
- [ ] **Auto-off after idle.** Set `TypingIdleOffMinutes` low (e.g. `1`) via Settings, turn capture on, type something, then wait past the idle window without typing: capture turns itself off on its own, the tray icon reverts, and a one-time balloon says "Typing capture turned off (idle)".
- [ ] **Correct characters, case, and modifiers.** Type a mixed-case sentence with digits and at least one Shift-punctuation character (e.g. `Hello, World! 123 & more?`) into one field: the stored entry matches exactly — this is the keystroke-time translation fix (review #1), confirm it actually holds on a real keyboard/layout.
- [ ] **Nothing typed is in the log, ever.** After a session of typing capture (on and off, including the password field above), `grep` `%LOCALAPPDATA%\M4W\m4w.log` for anything you typed: nothing but the "skipped"/start/stop lines should match — the log must never contain content, password or not.
- [ ] **Synthetic input is ignored.** With capture on, use a snippet (below) to paste into a field: the pasted value does not show up as a typed entry (the hook skips injected keystrokes).

## Snippets + Settings window (v0.2.0)

**Not exercisable on macOS** — `RegisterHotKey` for snippet ids and the WPF Settings window are Windows-only.

- [ ] **Open Settings.** Tray → Settings… The window opens with three tabs: General, Typing capture, Snippets.
- [ ] **Seeded snippets.** The Snippets tab lists two: Email (`Ctrl+Shift+R`, not secret, no value) and Password (`Ctrl+Shift+E`, secret, no value).
- [ ] **Set a value and test it.** Select Email, type a value in the Value box, Save. In Notepad, press `Ctrl+Shift+R`: the value appears, typed by M4W (clipboard + auto-paste). Do the same for Password with `Ctrl+Shift+E`.
- [ ] **Clipboard restore.** Before pressing a snippet hotkey, copy something distinctive (e.g. `before-snippet-test`) so it's on the clipboard. Press the snippet hotkey. A moment after the paste, check the clipboard (Ctrl+V somewhere else, or the M4W popup's "most recent copy" isn't the snippet value): it should hold `before-snippet-test` again, not the snippet's value.
- [ ] **Snippet value never in history.** After filling both snippets above, open the M4W popup and search for the values you set: they must not appear anywhere in history.
- [ ] **Secret masking.** In Settings, the Password snippet's value shows as `••••••••` until you press Reveal.
- [ ] **New snippet + custom hotkey.** Add a snippet, click into the hotkey box, press e.g. `Ctrl+Shift+N`, set a value, Save. Press the hotkey in Notepad: it fills.
- [ ] **Duplicate hotkey is flagged.** Set two snippets (or a snippet and the popup hotkey) to the same combination and press Save: a red message names both and nothing is saved until it's fixed.
- [ ] **Nothing secret in the log.** After all of the above, check `m4w.log`: only `snippet 'X' filled` / `snippet 'X' has no value yet` / `hotkey unavailable` lines, no values.
- [ ] **Paste lands clean, no stuck modifiers.** Fire a multi-modifier snippet hotkey (e.g. `Ctrl+Shift+R`) into a plain text field: the pasted value appears as-is — no stray characters from Ctrl/Shift still being held when Ctrl+V was sent (review #4).
- [ ] **Overlapping triggers: 2nd one drops, doesn't clobber.** Set two snippets' hotkeys, and press one right after the other (within ~1s). The 2nd should either fill correctly afterward or be dropped (log: "trigger dropped: another fill is already in progress") — but the clipboard restore from the 1st must never end up holding the 2nd snippet's value (review #3).
- [ ] **Slow-paste target restore.** Fire a snippet into an app that's slow to react (Electron app, Word, or over RDP). The clipboard should still be restored to what it held before, roughly a second later, not clobbered mid-paste (review #5).
- [ ] **Non-text/image clipboard isn't fabricated back.** Copy a file in Explorer (not text/image), then fire a snippet. Since M4W can only restore text/image, the clipboard is cleared afterward rather than left holding the snippet value — check it's not stuck with the snippet's value (review #6; documented limitation, not a bug).
