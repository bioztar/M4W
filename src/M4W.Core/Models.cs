namespace M4W.Core;

public enum ClipKind
{
    Text = 0,
    Image = 1,
}

/// <summary>Where an entry's text came from. Stored additively; old rows read back as Copied.</summary>
public enum ClipSource
{
    Copied = 0,
    Typed = 1,
}

/// <summary>One history item. Image bytes are not held here; load them via <see cref="HistoryService.LoadImage"/>.</summary>
public sealed record ClipEntry(
    long Id,
    ClipKind Kind,
    string? Text,
    string Preview,
    string Hash,
    bool Pinned,
    long UsedAt,
    ClipSource Source = ClipSource.Copied);

/// <summary>Encrypts/decrypts item blobs. App uses DPAPI; tests use a fake.</summary>
public interface IProtector
{
    byte[] Protect(byte[] plain);
    byte[] Unprotect(byte[] protectedBytes);
}

public interface ILog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
}
