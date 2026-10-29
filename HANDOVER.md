# HANDOVER — M4W — 2026-10-29
## State: v0.2.0 code complete + helm review fixes applied, building clean on macOS (0 warnings); Core green (133/133). Windows-only parts (keyboard hook, snippet hotkeys, Settings window) unverified — need Vitaly's checklist. Local-only, nothing pushed.
## helm review fixes (this pass, commits 808dc71..7b2e6fd)
- **Scope change from Vitaly**: `TypingSkipPasswordFields` default flipped to `false` — he wants
  everything he types captured, passwords included (those are exactly what he forgets). Setting stays,
  settable `true`. This supersedes review item #2 (password-skip failing open on focus-change-after-
  password): no longer a blocker, not hardened further.
- **#1 FIXED**: char translation was reading modifier state off the wrong thread, after a dispatcher hop
  — came out lowercase with shift/caps lost. Now reads live GetAsyncKeyState/GetKeyState + the real
  ScanCode + the foreground window's own layout right in the hook callback (still non-blocking), with
  ToUnicodeEx's do-not-change-state flag so it never corrupts dead-key state.
- **#3 FIXED**: snippet fills serialized with a non-blocking SemaphoreSlim(1,1); an overlapping trigger
  is dropped (logged), never queued.
- **#4 FIXED**: Paster polls GetAsyncKeyState for Alt/Shift/Win (300ms timeout) before SendCtrlV, so a
  snippet hotkey's own held modifiers don't turn Ctrl+V into Ctrl+Shift+V in the target.
- **#5 FIXED**: restore delay raised 300ms→1000ms, and only restores if
  GetClipboardSequenceNumber is unchanged since right after the fill — if it changed, something else
  touched the clipboard meanwhile and restoring would clobber that instead.
- **#6 DOCUMENTED**: snippet clipboard restore only covers text/image; other formats are cleared, not
  restored. Noted in README + checklist.
- **DestroyIcon**: verified already correct (shared Render() method's try/finally covers every icon
  variant including the capturing-state one) — no change needed.
## Done this session
- **Part 1, typing capture:** `TypingBuffer`, `TypingIdleClock`, `TypingPrivacyGuard` in Core (pure,
  tested); `TypingCaptureService` in App installs/removes `WH_KEYBOARD_LL` only while on, hook proc
  forwards first and defers all work to the dispatcher, ignores `LLKHF_INJECTED` (synthetic) keystrokes,
  flushes on Enter/Tab/focus-change/idle via `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, skips password
  fields via UI Automation `IsPasswordProperty` (fail-safe on any check failure). 4 new settings
  (`CaptureTyping` forced false on load, `TypingIdleOffMinutes`, `TypingSkipPasswordFields`,
  `MinTypedChars`, `IdleFlushSeconds`). Additive `source` column (`ClipSource.Copied|Typed`) on `items`,
  PRAGMA-checked migration (bundled sqlite doesn't support `ADD COLUMN IF NOT EXISTS`). Tray badge icon +
  balloon on idle auto-off; popup shows a ⌨ marker on typed entries.
- **Part 2, snippets + Settings window:** `Snippet`/`SnippetStore`/`SnippetSeeder`/`SnippetConflicts`/
  `SnippetService` in Core (pure/tested), a second sqlite connection to the same `history.db`. App:
  `SnippetHotkeyManager` registers each enabled snippet's hotkey on its own Win32 id (popup keeps its
  own); `SnippetFiller` does clipboard+auto-paste fill, reusing `ClipboardAccess`'s existing
  `M4W.Internal` tag (so capture/the typing hook already ignore it) and restoring the previous clipboard
  ~300ms later or clearing it if that's not possible. `SettingsWindow` (WPF, 3 tabs) is the single place
  to edit snippets, the popup hotkey, and every other setting; Save validates (ranges +
  `SnippetConflicts`) and applies live (re-registers hotkeys, live `MaxItems`/fuzzy-default via new
  `HistoryService.SetMaxItems`/`PopupWindow.SetFuzzyDefault`, `CaptureService` now reads settings via
  `Func<AppSettings>`); Cancel discards everything including snippet add/delete.
- README: Typing capture section, Snippets section (seeded hotkeys, secret handling, clipboard-restore),
  full settings table. `docs/TEST-CHECKLIST.md`: Typing section + Snippets/Settings section.
- 133/133 Core tests green, `dotnet build M4W.sln -c Release` clean (0 warnings).
## In flight / partially done
- Nothing half-done. Everything in the mission's done criteria is implemented; only real-PC verification
  is outstanding (expected — this was built on macOS).
## Next steps (ordered)
1. helm: review, merge `m4w-typing`, push, tag `v0.2.0` (or next appropriate tag) to trigger the release job.
2. Vitaly: run the new Typing capture + Snippets/Settings sections of `docs/TEST-CHECKLIST.md` on the
   real PC; report failures.
3. Likely first fixes: `ToUnicodeEx` with scan code 0 may mistranslate dead keys / some non-US layouts
   (noted as a `ponytail:` comment in `TypingCaptureService.TranslateChar` — upgrade path: pass the real
   scan code from `KBDLLHOOKSTRUCT`); WinEventHook/keyboard hook interaction with elevated target windows
   untested (same UIPI limits as the existing SendInput paste).
## Blockers / needs human
- All Windows-specific behavior (keyboard hook under real typing load, snippet hotkey registration,
  WPF Settings window rendering/validation, UI Automation password detection against real apps) is
  untested beyond compilation.
## Key files touched
- Core: TypingBuffer.cs, TypingIdleClock.cs, TypingPrivacyGuard.cs, Settings.cs, Models.cs,
  HistoryService.cs, SqliteHistoryStore.cs — typing capture + schema migration
- Core: Snippet.cs, SnippetStore.cs, SnippetSeeder.cs, SnippetConflicts.cs, SnippetService.cs,
  Hotkey.cs (`ToText()`) — snippets
- App: Native.cs (hook/WinEvent P/Invoke), TypingCaptureService.cs, TrayIcon.cs, PopupItem.cs,
  PopupWindow.xaml(.cs), AppHost.cs — typing capture glue + wiring
- App: MessageWindow.cs, SnippetHotkeyManager.cs, SnippetFiller.cs, ClipboardAccess.cs,
  SettingsWindow.xaml(.cs), SnippetDraft.cs — snippets + Settings window
- README.md, docs/TEST-CHECKLIST.md
