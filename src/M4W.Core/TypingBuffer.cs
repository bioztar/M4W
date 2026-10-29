using System.Text;

namespace M4W.Core;

/// <summary>What ends a run of typed characters and forces it out as a chunk.</summary>
public enum TypingBoundary
{
    Enter,
    Tab,
    FocusChanged,
    IdleFlush,
}

/// <summary>
/// Accumulates typed characters between boundaries and emits a finished chunk at each one. Pure and
/// deterministic: no clock, no Win32, nothing to mock but this class. Not thread-safe; the App calls it
/// from one thread (the UI thread), same as everything else fed by Win32 callbacks.
/// </summary>
public sealed class TypingBuffer
{
    private readonly StringBuilder _buffer = new();

    public TypingBuffer(int minTypedChars) => MinTypedChars = minTypedChars;

    public int MinTypedChars { get; }

    public void AppendChar(char c) => _buffer.Append(c);

    /// <summary>Removes the last character, if any. A Backspace past an empty buffer is a no-op.</summary>
    public void Backspace()
    {
        if (_buffer.Length > 0) _buffer.Length--;
    }

    /// <summary>
    /// Ends the current run at <paramref name="boundary"/> and returns the accumulated text, or null if
    /// it was empty, whitespace-only, or shorter than <see cref="MinTypedChars"/>. The buffer is cleared
    /// either way.
    /// </summary>
    public string? Flush(TypingBoundary boundary)
    {
        var text = _buffer.ToString();
        _buffer.Clear();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length < MinTypedChars) return null;
        return text;
    }
}
