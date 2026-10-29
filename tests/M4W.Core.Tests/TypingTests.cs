using M4W.Core;

namespace M4W.Core.Tests;

public class TypingBufferTests
{
    [Fact]
    public void AccumulatesAndFlushesOnBoundary()
    {
        var b = new TypingBuffer(minTypedChars: 3);
        foreach (var c in "hello") b.AppendChar(c);
        Assert.Equal("hello", b.Flush(TypingBoundary.Enter));
    }

    [Fact]
    public void FlushClearsTheBuffer()
    {
        var b = new TypingBuffer(minTypedChars: 1);
        foreach (var c in "ab") b.AppendChar(c);
        b.Flush(TypingBoundary.Tab);
        b.AppendChar('x');
        Assert.Equal("x", b.Flush(TypingBoundary.Tab));
    }

    [Fact]
    public void BackspaceRemovesLastChar()
    {
        var b = new TypingBuffer(minTypedChars: 1);
        foreach (var c in "abc") b.AppendChar(c);
        b.Backspace();
        b.AppendChar('d');
        Assert.Equal("abd", b.Flush(TypingBoundary.FocusChanged));
    }

    [Fact]
    public void BackspaceOnEmptyBufferIsNoOp()
    {
        var b = new TypingBuffer(minTypedChars: 1);
        b.Backspace();
        b.Backspace();
        b.AppendChar('x');
        Assert.Equal("x", b.Flush(TypingBoundary.IdleFlush));
    }

    [Fact]
    public void EmptyBufferFlushesNull()
    {
        var b = new TypingBuffer(minTypedChars: 1);
        Assert.Null(b.Flush(TypingBoundary.Enter));
    }

    [Fact]
    public void WhitespaceOnlyChunkIsDropped()
    {
        var b = new TypingBuffer(minTypedChars: 1);
        foreach (var c in "   \t  ") b.AppendChar(c);
        Assert.Null(b.Flush(TypingBoundary.Tab));
    }

    [Theory]
    [InlineData("ab", 3, null)]   // below MinTypedChars: discarded
    [InlineData("abc", 3, "abc")] // exactly at MinTypedChars: kept
    public void ShorterThanMinTypedCharsIsDiscarded(string typed, int min, string? expected)
    {
        var b = new TypingBuffer(minTypedChars: min);
        foreach (var c in typed) b.AppendChar(c);
        Assert.Equal(expected, b.Flush(TypingBoundary.Enter));
    }

    [Theory]
    [InlineData(TypingBoundary.Enter)]
    [InlineData(TypingBoundary.Tab)]
    [InlineData(TypingBoundary.FocusChanged)]
    [InlineData(TypingBoundary.IdleFlush)]
    public void EveryBoundaryKindFlushes(TypingBoundary boundary)
    {
        var b = new TypingBuffer(minTypedChars: 1);
        b.AppendChar('x');
        Assert.Equal("x", b.Flush(boundary));
    }
}

public class TypingIdleClockTests
{
    [Fact]
    public void BeforeIdleWindowStaysOn()
    {
        var last = DateTimeOffset.UtcNow;
        Assert.False(TypingIdleClock.ShouldAutoOff(last, last.AddMinutes(29), idleOffMinutes: 30));
    }

    [Fact]
    public void AtOrPastIdleWindowTurnsOff()
    {
        var last = DateTimeOffset.UtcNow;
        Assert.True(TypingIdleClock.ShouldAutoOff(last, last.AddMinutes(30), idleOffMinutes: 30));
        Assert.True(TypingIdleClock.ShouldAutoOff(last, last.AddMinutes(45), idleOffMinutes: 30));
    }

    [Fact]
    public void FreshKeystrokeResetsTheWindow()
    {
        var t0 = DateTimeOffset.UtcNow;
        var t1 = t0.AddMinutes(29); // a keystroke lands here, resetting "last"
        Assert.False(TypingIdleClock.ShouldAutoOff(t1, t1.AddMinutes(29), idleOffMinutes: 30));
    }
}

public class TypingPrivacyGuardTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void SkipsOnlyWhenSettingOnAndFieldIsPassword(bool skipSetting, bool isPassword, bool expectedSkip) =>
        Assert.Equal(expectedSkip, TypingPrivacyGuard.ShouldSkip(skipSetting, isPassword));
}
