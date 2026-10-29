using System;
using System.Threading.Tasks;
using M4W.Core;

namespace M4W.App;

/// <summary>Turns clipboard-changed notifications into history entries. Runs on the UI thread.</summary>
internal sealed class CaptureService
{
    private readonly HistoryService _history;
    private readonly Func<AppSettings> _settings;
    private readonly ILog _log;
    private bool _busy;
    private bool _again;

    public CaptureService(HistoryService history, Func<AppSettings> settings, ILog log)
    {
        _history = history;
        _settings = settings;
        _log = log;
    }

    public bool Paused { get; set; }

    /// <summary>Call on every WM_CLIPBOARDUPDATE. Overlapping notifications are coalesced into one more pass.</summary>
    public async void OnClipboardUpdated()
    {
        if (Paused) return;
        if (_busy) { _again = true; return; }

        _busy = true;
        try
        {
            do
            {
                _again = false;
                await CaptureOnceAsync();
            } while (_again && !Paused);
        }
        catch (Exception ex)
        {
            _log.Error("capture failed", ex); // async void: must never escape or the process dies
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task CaptureOnceAsync()
    {
        var snap = await ClipboardAccess.ReadAsync(_log);
        if (snap is null || snap.IsOwn) return;
        if (snap.IsPrivate && _settings().RespectPrivateFlag) return;

        if (snap.Text is not null) _history.AddText(snap.Text);
        else if (snap.Png is not null) _history.AddImage(snap.Png);
    }
}
