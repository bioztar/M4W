using M4W.Core;

namespace M4W.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void DefaultsMatchSpec()
    {
        var s = new AppSettings();
        Assert.Equal("Ctrl+Alt+V", s.Hotkey);
        Assert.Equal(1000, s.MaxItems);
        Assert.True(s.FuzzyByDefault);
        Assert.True(s.AutoPaste);
        Assert.False(s.RespectPrivateFlag);
        Assert.False(s.CaptureTyping);
        Assert.Equal(30, s.TypingIdleOffMinutes);
        Assert.False(s.TypingSkipPasswordFields);
        Assert.Equal(3, s.MinTypedChars);
        Assert.Equal(2, s.IdleFlushSeconds);
    }

    [Fact]
    public void ValidFileIsHonoured()
    {
        var s = SettingsLoader.Parse("""{"Hotkey":"ctrl+shift+F5","MaxItems":50,"FuzzyByDefault":false,"AutoPaste":false,"RespectPrivateFlag":true,"TypingIdleOffMinutes":45,"TypingSkipPasswordFields":true,"MinTypedChars":5,"IdleFlushSeconds":4}""", NullLog.Instance);
        Assert.Equal("ctrl+shift+F5", s.Hotkey);
        Assert.Equal(50, s.MaxItems);
        Assert.False(s.FuzzyByDefault);
        Assert.False(s.AutoPaste);
        Assert.True(s.RespectPrivateFlag);
        Assert.Equal(45, s.TypingIdleOffMinutes);
        Assert.True(s.TypingSkipPasswordFields);
        Assert.Equal(5, s.MinTypedChars);
        Assert.Equal(4, s.IdleFlushSeconds);
    }

    [Fact]
    public void CaptureTypingNeverLoadsAsTrueEvenWhenStoredTrue()
    {
        var s = SettingsLoader.Parse("""{"CaptureTyping":true}""", NullLog.Instance);
        Assert.False(s.CaptureTyping);
    }

    [Fact]
    public void PartialFileFillsRestWithDefaults()
    {
        var s = SettingsLoader.Parse("""{"maxitems": 200}""", NullLog.Instance);
        Assert.Equal(200, s.MaxItems);
        Assert.Equal(new AppSettings() with { MaxItems = 200 }, s);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    public void GarbageFallsBackToDefaultsAndLogs(string json)
    {
        var log = new ListLog();
        Assert.Equal(new AppSettings(), SettingsLoader.Parse(json, log));
        Assert.Single(log.Lines);
    }

    [Theory]
    [InlineData("""{"Hotkey":"V"}""")]            // no modifier
    [InlineData("""{"Hotkey":"Ctrl+Alt"}""")]     // no key
    [InlineData("""{"Hotkey":"Ctrl+Alt+VV"}""")]  // bad key
    [InlineData("""{"Hotkey":5}""")]
    [InlineData("""{"MaxItems":3}""")]            // below minimum
    [InlineData("""{"MaxItems":99999999}""")]     // above ceiling
    [InlineData("""{"MaxItems":"many"}""")]
    [InlineData("""{"MaxItems":1.5}""")]
    [InlineData("""{"AutoPaste":"yes"}""")]
    [InlineData("""{"FuzzyByDefault":1}""")]
    [InlineData("""{"RespectPrivateFlag":null}""")]
    [InlineData("""{"TypingIdleOffMinutes":0}""")]      // below minimum
    [InlineData("""{"TypingIdleOffMinutes":1441}""")]   // above ceiling
    [InlineData("""{"TypingIdleOffMinutes":"soon"}""")]
    [InlineData("""{"TypingSkipPasswordFields":"nope"}""")]
    [InlineData("""{"MinTypedChars":-1}""")]
    [InlineData("""{"MinTypedChars":1001}""")]
    [InlineData("""{"CaptureTyping":"on"}""")]
    [InlineData("""{"IdleFlushSeconds":0}""")]
    [InlineData("""{"IdleFlushSeconds":301}""")]
    public void BadValueFallsBackToDefaultAndLogs(string json)
    {
        var log = new ListLog();
        Assert.Equal(new AppSettings(), SettingsLoader.Parse(json, log));
        Assert.Single(log.Lines);
    }

    [Fact]
    public void OneBadValueDoesNotDiscardTheGoodOnes()
    {
        var s = SettingsLoader.Parse("""{"MaxItems":-1,"AutoPaste":false}""", NullLog.Instance);
        Assert.Equal(1000, s.MaxItems);
        Assert.False(s.AutoPaste);
    }

    [Fact]
    public void SaveThenLoadRoundTrips()
    {
        using var dir = new TempDir();
        var path = dir.File("sub/settings.json");
        var saved = new AppSettings { MaxItems = 77, Hotkey = "Win+Shift+C", RespectPrivateFlag = true };
        SettingsLoader.Save(path, saved);
        Assert.Equal(saved, SettingsLoader.Load(path, NullLog.Instance));
    }

    [Fact]
    public void MissingFileGivesDefaultsSilently()
    {
        var log = new ListLog();
        Assert.Equal(new AppSettings(), SettingsLoader.Load("/nonexistent/m4w/settings.json", log));
        Assert.Empty(log.Lines);
    }

    [Fact]
    public void CorruptFileOnDiskFallsBack()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "}{");
        var log = new ListLog();
        Assert.Equal(new AppSettings(), SettingsLoader.Load(dir.File("settings.json"), log));
        Assert.Single(log.Lines);
    }
}

public class HotkeyTests
{
    [Fact]
    public void ParsesDefault()
    {
        Assert.True(Hotkey.TryParse("Ctrl+Alt+V", out var h));
        Assert.Equal(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, h!.Modifiers);
        Assert.Equal("V", h.Key);
        Assert.Equal(0x56, h.VirtualKey);
    }

    [Theory]
    [InlineData("Control + Shift + 7", HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, 0x37)]
    [InlineData("win+f1", HotkeyModifiers.Win, 0x70)]
    [InlineData("Windows+Alt+F24", HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x87)]
    public void ParsesVariants(string text, HotkeyModifiers mods, int vk)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        Assert.Equal(mods, h!.Modifiers);
        Assert.Equal(vk, h.VirtualKey);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("V")] [InlineData("Ctrl+")] [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+A+B")] [InlineData("Ctrl+Enter")] [InlineData("Ctrl+Alt")]
    public void RejectsInvalid(string? text)
    {
        Assert.False(Hotkey.TryParse(text, out var h));
        Assert.Null(h);
    }
}

public class FileLogTests
{
    [Fact]
    public void WritesLevelsAndRollsWhenOverCap()
    {
        using var dir = new TempDir();
        var path = dir.File("logs/m4w.log");
        var log = new FileLog(path, maxBytes: 200);
        log.Info("hello");
        log.Warn("careful");
        log.Error("boom", new InvalidOperationException("bad"));
        var text = File.ReadAllText(path);
        Assert.Contains("INFO hello", text);
        Assert.Contains("WARN careful", text);
        Assert.Contains("ERROR boom: InvalidOperationException: bad", text);

        for (var i = 0; i < 10; i++) log.Info(new string('x', 50));
        Assert.True(File.Exists(path + ".1"));
        Assert.True(new FileInfo(path).Length < 400);
    }

    [Fact]
    public void ErrorWithoutExceptionAndUnwritablePathNeverThrow()
    {
        using var dir = new TempDir();
        var blocker = dir.File("blocker");
        File.WriteAllText(blocker, "x");
        var log = new FileLog(Path.Combine(blocker, "m4w.log")); // parent is a file: IOException
        log.Error("plain");
        log.Info("still alive");
    }

    [Fact]
    public void NullLogIsInert()
    {
        NullLog.Instance.Info("a"); NullLog.Instance.Warn("b"); NullLog.Instance.Error("c");
    }
}
