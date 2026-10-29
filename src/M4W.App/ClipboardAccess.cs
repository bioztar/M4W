using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using M4W.Core;

namespace M4W.App;

/// <summary>
/// Clipboard reads and writes with retry. The clipboard is routinely locked by another process (and
/// by RDP clipboard sync), so every call is retried briefly; a call that never succeeds is logged.
/// </summary>
internal static class ClipboardAccess
{
    /// <summary>Marker format put on everything M4W writes so the capture side can ignore its own pastes.</summary>
    public const string OwnFormat = "M4W.Internal";
    public const string PrivateFormat = "ExcludeClipboardContentFromMonitorProcessing";
    private const int Attempts = 8;

    public sealed record Snapshot(string? Text, byte[]? Png, bool IsOwn, bool IsPrivate);

    public static Task<Snapshot?> ReadAsync(ILog log) => RetryAsync(Read, log, "read");

    public static async Task<bool> SetTextAsync(string text, ILog log) =>
        await RetryAsync(() => Write(Wrap(d => d.SetText(text, TextDataFormat.UnicodeText))), log, "write");

    public static async Task<bool> SetImageAsync(byte[] png, ILog log) =>
        await RetryAsync(() => Write(Wrap(d => d.SetImage(Decode(png)))), log, "write");

    /// <summary>Used when a snippet fill can't faithfully restore the previous clipboard contents, so nothing lingers.</summary>
    public static Task<bool> ClearAsync(ILog log) => RetryAsync(() => { Clipboard.Clear(); return true; }, log, "clear");

    private static Snapshot? Read()
    {
        var data = Clipboard.GetDataObject();
        if (data is null) return null;

        var isPrivate = data.GetDataPresent(PrivateFormat);
        if (data.GetDataPresent(OwnFormat)) return new Snapshot(null, null, true, isPrivate);

        if (data.GetDataPresent(DataFormats.UnicodeText, true) && data.GetData(DataFormats.UnicodeText, true) is string text
            && !string.IsNullOrWhiteSpace(text))
            return new Snapshot(text, null, false, isPrivate);

        if (data.GetDataPresent(DataFormats.Bitmap, true) && Clipboard.GetImage() is { } image)
            return new Snapshot(null, Encode(image), false, isPrivate);

        return new Snapshot(null, null, false, isPrivate);
    }

    private static DataObject Wrap(Action<DataObject> fill)
    {
        var data = new DataObject();
        fill(data);
        data.SetData(OwnFormat, "1");
        return data;
    }

    private static bool Write(DataObject data)
    {
        Clipboard.SetDataObject(data, copy: true);
        return true;
    }

    private static BitmapSource Decode(byte[] png)
    {
        using var ms = new MemoryStream(png);
        var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }

    private static byte[] Encode(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static async Task<T?> RetryAsync<T>(Func<T> operation, ILog log, string what)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (ex is COMException or ExternalException)
            {
                if (attempt == Attempts)
                {
                    log.Error($"clipboard {what} still busy after {Attempts} attempts, giving up", ex);
                    return default;
                }
                await Task.Delay(40 * attempt);
            }
        }
    }
}
