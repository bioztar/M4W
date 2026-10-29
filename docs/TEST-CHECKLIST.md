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
