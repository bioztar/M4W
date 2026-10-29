using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using M4W.Core;

namespace M4W.App;

/// <summary>Wires the pieces together: storage, capture, popup, tray, hotkey. Lives on the UI thread.</summary>
internal sealed class AppHost : IDisposable
{
    private readonly ILog _log;
    private readonly AppSettings _settings;
    private readonly SqliteHistoryStore _store;
    private readonly HistoryService _history;
    private readonly CaptureService _capture;
    private readonly MessageWindow _messages;
    private readonly PopupWindow _popup;
    private readonly TrayIcon _tray;
    private RegisteredWaitHandle? _showWait;
    private IntPtr _previousWindow;

    public AppHost(ILog log)
    {
        _log = log;
        var firstRun = !File.Exists(AppPaths.Settings);
        _settings = SettingsLoader.Load(AppPaths.Settings, log);
        if (firstRun) FirstRunSetup();
        else Guard("repair autostart path", Autostart.RepairPath);

        _store = SqliteHistoryStore.OpenOrRecover(AppPaths.Database, new DpapiProtector(), log);
        _history = new HistoryService(_store, _settings.MaxItems, log);
        _capture = new CaptureService(_history, _settings, log);

        _popup = new PopupWindow(_history, _settings.FuzzyByDefault);
        _popup.ItemChosen += OnItemChosen;

        _messages = new MessageWindow(log);
        _messages.ClipboardUpdated += _capture.OnClipboardUpdated;
        _messages.HotkeyPressed += TogglePopup;

        _tray = new TrayIcon(SafeAutostartState());
        _tray.OpenRequested += ShowPopup;
        _tray.PauseChanged += OnPauseChanged;
        _tray.ClearRequested += OnClearRequested;
        _tray.AutostartChanged += OnAutostartChanged;
        _tray.AboutRequested += ShowAbout;
        _tray.QuitRequested += () => Application.Current.Shutdown();

        log.Info($"started v{Version}, {_history.Items.Count} items loaded");
    }

    private static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    /// <summary>Registers the hotkey and starts listening for "second launch" signals.</summary>
    public void Start(EventWaitHandle showSignal)
    {
        Hotkey.TryParse(_settings.Hotkey, out var hotkey);
        hotkey ??= Hotkey.TryParse(AppSettings.DefaultHotkey, out var fallback) ? fallback : null;
        if (hotkey is not null && !_messages.RegisterHotkey(hotkey))
            _tray.ShowBalloon($"Hotkey {_settings.Hotkey} is taken by another app. Use the tray icon, or change Hotkey in settings.json.");

        var dispatcher = Application.Current.Dispatcher;
        _showWait = ThreadPool.RegisterWaitForSingleObject(
            showSignal, (_, _) => dispatcher.BeginInvoke(ShowPopup), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _showWait?.Unregister(null);
        _tray.Dispose();
        _messages.Dispose();
        _popup.Close();
        _store.Dispose();
        _log.Info("stopped");
    }

    private void TogglePopup()
    {
        if (_popup.IsVisible) _popup.Hide();
        else ShowPopup();
    }

    private void ShowPopup()
    {
        var foreground = Native.GetForegroundWindow();
        // Always re-decide the paste target on open. A tray click makes the taskbar the foreground
        // window (and our own window can be it too); there is no real target then, so clear it rather
        // than reuse a stale one: the pick is then only put on the clipboard, never pasted.
        _previousWindow = IsPasteTarget(foreground) ? foreground : IntPtr.Zero;
        _popup.ShowAtCursor();
    }

    private static bool IsPasteTarget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || Native.ProcessIdOf(hwnd) == Environment.ProcessId) return false;
        var name = Native.ClassNameOf(hwnd);
        return !name.StartsWith("Shell_", StringComparison.Ordinal) && !name.Contains("Overflow", StringComparison.Ordinal);
    }

    private async void OnItemChosen(ClipEntry entry)
    {
        try
        {
            _history.Touch(entry.Id);
            var ok = entry.Kind == ClipKind.Text
                ? await ClipboardAccess.SetTextAsync(entry.Text!, _log)
                : await SetImageAsync(entry);
            if (!ok)
            {
                _tray.ShowBalloon("The clipboard was busy, so nothing was pasted. Try again.");
                return;
            }
            if (_previousWindow == IntPtr.Zero)
            {
                _log.Info("no paste target (opened from tray), item is on the clipboard");
                return;
            }
            await Paster.RestoreFocusAndPasteAsync(_previousWindow, _settings.AutoPaste, _log);
        }
        catch (Exception ex)
        {
            _log.Error("selecting item failed", ex); // async void: must not escape
        }
    }

    private async System.Threading.Tasks.Task<bool> SetImageAsync(ClipEntry entry)
    {
        var png = _history.LoadImage(entry.Id);
        if (png is null)
        {
            _log.Warn($"item {entry.Id}: image data missing");
            return false;
        }
        return await ClipboardAccess.SetImageAsync(png, _log);
    }

    private void OnPauseChanged(bool paused)
    {
        _capture.Paused = paused;
        _tray.SetPaused(paused);
        _log.Info(paused ? "capture paused" : "capture resumed");
    }

    private void OnClearRequested()
    {
        var answer = MessageBox.Show("Delete all unpinned clipboard history?", "M4W", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _log.Info($"history cleared: {_history.ClearUnpinned()} items removed");
    }

    private void OnAutostartChanged(bool enabled)
    {
        try
        {
            Autostart.Set(enabled);
            _log.Info($"autostart {(enabled ? "enabled" : "disabled")}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _log.Error("autostart change failed", ex);
            _tray.ShowBalloon("Could not change the Start with Windows setting.");
            _tray.SetAutostartChecked(SafeAutostartState());
        }
    }

    private void ShowAbout() => MessageBox.Show(
        $"M4W {Version}\nClipboard history for Windows.\n\nData: {AppPaths.Dir}\nInspired by Maccy, not affiliated.",
        "About M4W", MessageBoxButton.OK, MessageBoxImage.Information);

    private void FirstRunSetup()
    {
        Guard("write default settings", () => SettingsLoader.Save(AppPaths.Settings, _settings));
        Guard("enable autostart", () => Autostart.Set(true));
    }

    private bool SafeAutostartState()
    {
        try { return Autostart.IsEnabled(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _log.Error("cannot read autostart state", ex);
            return false;
        }
    }

    private void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _log.Error($"{what} failed", ex);
        }
    }
}
