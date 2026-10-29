using System;
using System.Threading.Tasks;
using M4W.Core;

namespace M4W.App;

/// <summary>
/// Fills a snippet's value into the previously-focused field: clipboard + auto-paste, with the two
/// mandatory guardrails — the value is written via <see cref="ClipboardAccess"/>, which already tags
/// everything M4W writes with the "M4W.Internal" marker, so clipboard capture and the typing hook's own
/// synthetic-input check both ignore it; and whatever was on the clipboard before is restored a short
/// moment after the paste, or cleared if it can't be restored, so the value never lingers.
/// </summary>
internal static class SnippetFiller
{
    private const int RestoreDelayMs = 300;

    public static async Task FillAsync(Snippet snippet, IntPtr target, ILog log)
    {
        if (snippet.Value.Length == 0)
        {
            log.Warn($"snippet '{snippet.Name}' has no value yet");
            return;
        }

        var previous = await ClipboardAccess.ReadAsync(log);
        if (!await ClipboardAccess.SetTextAsync(snippet.Value, log))
        {
            log.Warn($"snippet '{snippet.Name}': clipboard was busy, not filled");
            return;
        }

        await Paster.RestoreFocusAndPasteAsync(target, paste: true, log);
        log.Info($"snippet '{snippet.Name}' filled");

        await Task.Delay(RestoreDelayMs);
        await RestorePreviousClipboardAsync(previous, log);
    }

    private static async Task RestorePreviousClipboardAsync(ClipboardAccess.Snapshot? previous, ILog log)
    {
        // previous.Text/Png are null both when there was nothing to restore and when the prior content
        // was M4W's own (unreadable by design) — either way there is nothing faithful to put back, so
        // clearing is the safe choice: never leave the snippet value sitting on the clipboard.
        var restored = previous?.Text is not null && await ClipboardAccess.SetTextAsync(previous.Text, log)
            || previous?.Png is not null && await ClipboardAccess.SetImageAsync(previous.Png, log);
        if (!restored) await ClipboardAccess.ClearAsync(log);
    }
}
