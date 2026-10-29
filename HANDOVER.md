# HANDOVER — M4W — 2026-10-29
## State: v1 code complete and building on macOS; Core tested (green), Windows side unverified (needs Vitaly's checklist). Local-only, nothing pushed.
## Done this session
- `M4W.sln`: `src/M4W.Core` (net10.0), `src/M4W.App` (net10.0-windows, WPF + NotifyIcon), `tests/M4W.Core.Tests` (xUnit, 71 tests, ~99% line coverage)
- Core: HistoryService (dedupe/bump, retention w/ pins, search), SqliteHistoryStore (DPAPI-able blobs, HMAC dedupe hash), FuzzyMatcher, SettingsLoader (validated), Hotkey parser, FileLog
- App: clipboard listener + retrying read/write, popup, auto-paste, tray, autostart, single instance, DPAPI protector
- CI workflow, README, `docs/TEST-CHECKLIST.md`; verified `dotnet publish -r win-x64` single-file gives a 70 MB `M4W.exe` on macOS
## In flight / partially done
- Nothing half-done.
## Next steps (ordered)
1. helm: review, merge `m4w-v1`, push, make the repo's first tag `v0.1.0` to trigger the release job (workflow unrun so far)
2. Vitaly: run `docs/TEST-CHECKLIST.md` on the real PC; report failures
3. Likely first fixes: RDP client may swallow Ctrl+Alt+V (alt hotkey), popup position on mixed-DPI monitors
## Blockers / needs human
- Windows behaviour (hotkey, SendInput paste, tray, RDP capture, autostart) is untested: only compiled
## Key files touched
- src/M4W.Core/HistoryService.cs — history rules; src/M4W.Core/SqliteHistoryStore.cs — encrypted storage
- src/M4W.App/AppHost.cs — wiring; ClipboardAccess.cs — retry; PopupWindow.xaml(.cs) — UI; Native.cs — Win32
- .github/workflows/ci.yml — build/test + release on `v*` tag
