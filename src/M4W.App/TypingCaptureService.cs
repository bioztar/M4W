using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Threading;
using M4W.Core;

namespace M4W.App;

/// <summary>
/// Installs a low-level keyboard hook while typing capture is on, turns keystrokes into history
/// entries via <see cref="TypingBuffer"/>, and removes the hook the instant capture turns off. The hook
/// callback itself does nothing but read the key and call <see cref="Native.CallNextHookEx"/>: all real
/// work is deferred to the UI thread via <see cref="Dispatcher.BeginInvoke(Delegate)"/> so a slow or
/// throwing handler here can never degrade system-wide typing.
/// </summary>
internal sealed class TypingCaptureService : IDisposable
{
    private readonly HistoryService _history;
    private readonly Func<AppSettings> _settings;
    private readonly Func<bool> _isPaused;
    private readonly ILog _log;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _idleTimer;

    private Native.LowLevelKeyboardProc? _hookProc; // field, not local: must outlive the hook or the GC can collect it
    private IntPtr _hookHandle;
    private Native.WinEventProc? _winEventProc;
    private IntPtr _winEventHandle;
    private TypingBuffer? _buffer;
    private DateTimeOffset _lastKeystroke;

    public TypingCaptureService(HistoryService history, Func<AppSettings> settings, Func<bool> isPaused, ILog log)
    {
        _history = history;
        _settings = settings;
        _isPaused = isPaused;
        _log = log;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _idleTimer.Tick += (_, _) => OnIdleTick();
    }

    public bool IsCapturing { get; private set; }

    /// <summary>Fired once when the idle timer turns capture off on its own. The caller still owns calling <see cref="Stop"/>.</summary>
    public event Action? AutoOffTriggered;

    public void Start()
    {
        if (IsCapturing) return;
        _buffer = new TypingBuffer(_settings().MinTypedChars);
        _lastKeystroke = DateTimeOffset.UtcNow;

        _hookProc = HookCallback;
        _hookHandle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _hookProc, Native.GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            _log.Error("typing capture: SetWindowsHookEx failed, capture not started");
            _hookProc = null;
            _buffer = null;
            return;
        }

        _winEventProc = OnForegroundChanged;
        _winEventHandle = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);

        _idleTimer.Start();
        IsCapturing = true;
        _log.Info("typing capture started");
    }

    public void Stop()
    {
        if (!IsCapturing) return;
        FlushAndStore(TypingBoundary.FocusChanged); // don't lose whatever was mid-field when turned off

        if (_hookHandle != IntPtr.Zero) { Native.UnhookWindowsHookEx(_hookHandle); _hookHandle = IntPtr.Zero; }
        _hookProc = null;
        if (_winEventHandle != IntPtr.Zero) { Native.UnhookWinEvent(_winEventHandle); _winEventHandle = IntPtr.Zero; }
        _winEventProc = null;
        _idleTimer.Stop();
        _buffer = null;
        IsCapturing = false;
        _log.Info("typing capture stopped");
    }

    public void Dispose() => Stop();

    /// <summary>
    /// The hook proc: forwards immediately, then does its state-dependent reading (modifiers, real scan
    /// code, the foreground window's layout) right here at keystroke time — not after dispatching, by
    /// which point the physical key state may have moved on — and only hands the already-translated
    /// result to the dispatcher. Still never blocks: every step here is a cheap, non-blocking user32 call.
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        var result = Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        if (nCode < 0) return result;

        var msg = wParam.ToInt32();
        if (msg is not (Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN)) return result; // keyup carries nothing new

        var info = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
        if ((info.Flags & Native.LLKHF_INJECTED) != 0) return result; // synthetic (e.g. our own snippet paste): never captured

        var vk = info.VkCode;
        char? ch = vk is Native.VK_RETURN or Native.VK_TAB or Native.VK_BACK ? null : TranslateChar(vk, info.ScanCode);
        _dispatcher.BeginInvoke(() => OnKeyEvent(vk, ch));
        return result;
    }

    private void OnKeyEvent(uint vk, char? ch)
    {
        if (_buffer is null) return;
        _lastKeystroke = DateTimeOffset.UtcNow;
        switch (vk)
        {
            case Native.VK_RETURN: FlushAndStore(TypingBoundary.Enter); return;
            case Native.VK_TAB: FlushAndStore(TypingBoundary.Tab); return;
            case Native.VK_BACK: _buffer.Backspace(); return;
        }
        if (ch is not null) _buffer.AppendChar(ch.Value);
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime) =>
        FlushAndStore(TypingBoundary.FocusChanged);

    private void OnIdleTick()
    {
        if (_buffer is null) return;
        var settings = _settings();
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastKeystroke).TotalSeconds >= settings.IdleFlushSeconds) FlushAndStore(TypingBoundary.IdleFlush);
        if (TypingIdleClock.ShouldAutoOff(_lastKeystroke, now, settings.TypingIdleOffMinutes)) AutoOffTriggered?.Invoke();
    }

    private void FlushAndStore(TypingBoundary boundary)
    {
        var chunk = _buffer?.Flush(boundary);
        if (chunk is null || _isPaused()) return;

        var settings = _settings();
        if (TypingPrivacyGuard.ShouldSkip(settings.TypingSkipPasswordFields, IsPasswordFieldFocused()))
        {
            _log.Info("typed chunk skipped (password field)");
            return;
        }
        _history.AddText(chunk, ClipSource.Typed);
    }

    /// <summary>
    /// Fail-safe: any UI Automation failure is treated as "yes, a password field" so a broken check
    /// skips recording rather than silently recording a password. Never fails open.
    /// </summary>
    private bool IsPasswordFieldFocused()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null) return false;
            return element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true;
        }
        catch (Exception ex) when (ex is not (StackOverflowException or OutOfMemoryException))
        {
            _log.Warn($"typing capture: password-field check failed, treating as password ({ex.GetType().Name})");
            return true;
        }
    }

    /// <summary>
    /// Translates a keystroke using the real modifier/caps-lock state and the real scan code, both read
    /// right now rather than later off the (wrong-thread) UI queue — <c>GetKeyboardState</c> reports
    /// M4W's own thread, not the app the user is actually typing into, which is how this used to come
    /// out lowercase with shift/caps lost. <c>TOUNICODE_DO_NOT_CHANGE_STATE</c> keeps dead-key state
    /// untouched so the user's own typing isn't corrupted by our reading it.
    /// </summary>
    private static char? TranslateChar(uint vk, uint scanCode)
    {
        var keyState = new byte[256];
        SetKeyFlag(keyState, Native.VK_SHIFT);
        SetKeyFlag(keyState, Native.VK_CONTROL);
        SetKeyFlag(keyState, Native.VK_MENU);
        if ((Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0) keyState[Native.VK_CAPITAL] = 1;

        var foreground = Native.GetForegroundWindow();
        var foregroundThreadId = Native.GetWindowThreadProcessId(foreground, out _);
        var layout = Native.GetKeyboardLayout(foregroundThreadId);

        var buffer = new StringBuilder(8);
        var rc = Native.ToUnicodeEx(vk, scanCode, keyState, buffer, buffer.Capacity, Native.TOUNICODE_DO_NOT_CHANGE_STATE, layout);
        if (rc <= 0 || buffer.Length == 0) return null;
        var c = buffer[0];
        return char.IsControl(c) ? null : c;
    }

    private static void SetKeyFlag(byte[] keyState, int vk)
    {
        if ((Native.GetAsyncKeyState(vk) & 0x8000) != 0) keyState[vk] = 0x80;
    }
}
