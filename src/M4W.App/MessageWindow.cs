using System;
using System.Windows.Interop;
using M4W.Core;

namespace M4W.App;

/// <summary>Hidden message-only window: receives WM_CLIPBOARDUPDATE and the global hotkey.</summary>
internal sealed class MessageWindow : IDisposable
{
    private const int HotkeyId = 0x4D34; // "M4"

    private readonly HwndSource _source;
    private readonly ILog _log;
    private bool _hotkeyRegistered;

    public MessageWindow(ILog log)
    {
        _log = log;
        _source = new HwndSource(new HwndSourceParameters("M4W.MessageWindow")
        {
            ParentWindow = Native.HWND_MESSAGE,
            Width = 0,
            Height = 0,
        });
        _source.AddHook(WndProc);

        if (!Native.AddClipboardFormatListener(_source.Handle))
            log.Error("AddClipboardFormatListener failed; clipboard changes will not be captured");
    }

    public event Action? ClipboardUpdated;
    public event Action? HotkeyPressed;

    /// <summary>Registers the global hotkey; returns false (and logs) if another app already owns it.</summary>
    public bool RegisterHotkey(Hotkey hotkey)
    {
        if (_hotkeyRegistered) Native.UnregisterHotKey(_source.Handle, HotkeyId);
        // HotkeyModifiers values equal the Win32 MOD_* flags (Alt=1, Ctrl=2, Shift=4, Win=8).
        _hotkeyRegistered = Native.RegisterHotKey(_source.Handle, HotkeyId, (uint)hotkey.Modifiers | Native.MOD_NOREPEAT, (uint)hotkey.VirtualKey);
        if (!_hotkeyRegistered) _log.Error($"RegisterHotKey({hotkey.Modifiers}+{hotkey.Key}) failed; another app may own it");
        return _hotkeyRegistered;
    }

    public void Dispose()
    {
        if (_hotkeyRegistered) Native.UnregisterHotKey(_source.Handle, HotkeyId);
        Native.RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case Native.WM_CLIPBOARDUPDATE:
                ClipboardUpdated?.Invoke();
                handled = true;
                break;
            case Native.WM_HOTKEY when wParam.ToInt32() == HotkeyId:
                HotkeyPressed?.Invoke();
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }
}
