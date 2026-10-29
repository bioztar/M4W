using M4W.Core;

namespace M4W.Core.Tests;

public class SnippetStoreTests
{
    [Fact]
    public void InsertLoadUpdateDeleteRoundTripThroughDpapi()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        using var store = SnippetStore.Open(path, new FakeProtector(), NullLog.Instance);

        var s = new Snippet("id1", "Test", "test-value-123", "Ctrl+Shift+T", IsSecret: false);
        store.Insert(s);
        Assert.Equal([s], store.LoadAll());

        var updated = s with { Value = "test-value-456", Enabled = false };
        store.Update(updated);
        Assert.Equal([updated], store.LoadAll());

        store.Delete("id1");
        Assert.Empty(store.LoadAll());
    }

    [Fact]
    public void ValueNeverTouchesDiskInPlaintext()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        const string secret = "test-value-should-not-appear-plaintext";
        using (var store = SnippetStore.Open(path, new FakeProtector(), NullLog.Instance))
            store.Insert(new Snippet("id1", "Password", secret, "Ctrl+Shift+E", IsSecret: true));
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var raw = File.ReadAllBytes(path);
        Assert.True(raw.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(secret)) < 0);
    }

    [Fact]
    public void RowThatFailsToDecryptIsSkippedNotFatal()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        using (var store = SnippetStore.Open(path, new FakeProtector(), NullLog.Instance))
            store.Insert(new Snippet("id1", "Email", "a@b.test", "Ctrl+Shift+R", IsSecret: false));

        var log = new ListLog();
        using var other = SnippetStore.Open(path, new RejectingProtector(), log);
        Assert.Empty(other.LoadAll());
        Assert.Contains(log.Lines, l => l.StartsWith("WARN") && l.Contains("cannot decrypt"));
    }

    [Fact]
    public void SharesTheSameDatabaseFileAsHistory()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        var protector = new FakeProtector();
        using var historyStore = SqliteHistoryStore.Open(path, protector, NullLog.Instance);
        new HistoryService(historyStore, 1000, NullLog.Instance).AddText("a history item");

        using var snippetStore = SnippetStore.Open(path, protector, NullLog.Instance);
        snippetStore.Insert(new Snippet("id1", "Email", "", "Ctrl+Shift+R", IsSecret: false));

        Assert.Single(snippetStore.LoadAll());
        Assert.Single(historyStore.LoadAll());
    }

    private sealed class RejectingProtector : IProtector
    {
        public byte[] Protect(byte[] plain) => new FakeProtector().Protect(plain);
        public byte[] Unprotect(byte[] protectedBytes) => throw new System.Security.Cryptography.CryptographicException("wrong user");
    }
}

public class SnippetServiceTests
{
    private static SnippetService Make(ILog? log = null) =>
        new(SnippetStore.Open(":memory:", new FakeProtector(), log ?? NullLog.Instance), log ?? NullLog.Instance);

    [Fact]
    public void SeedsTheTwoDefaultsOnFirstLoad()
    {
        var log = new ListLog();
        var svc = Make(log);
        Assert.Equal(2, svc.Items.Count);
        Assert.Contains(svc.Items, s => s.Name == "Email" && s.Hotkey == "Ctrl+Shift+R" && !s.IsSecret && s.Value == "");
        Assert.Contains(svc.Items, s => s.Name == "Password" && s.Hotkey == "Ctrl+Shift+E" && s.IsSecret && s.Value == "");
        Assert.Contains(log.Lines, l => l.Contains("snippet 'Email' has no value yet"));
        Assert.Contains(log.Lines, l => l.Contains("snippet 'Password' has no value yet"));
    }

    [Fact]
    public void DoesNotReseedOnceSomethingExists()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        var protector = new FakeProtector();
        using (var store = SnippetStore.Open(path, protector, NullLog.Instance))
            store.Insert(new Snippet("id1", "Signature", "Best, V", "Ctrl+Shift+S", IsSecret: false));

        using var reopened = SnippetStore.Open(path, protector, NullLog.Instance);
        var svc = new SnippetService(reopened, NullLog.Instance);
        Assert.Equal(["Signature"], svc.Items.Select(s => s.Name));
    }

    [Fact]
    public void AddUpdateDeleteChangeTheInMemoryList()
    {
        var svc = Make();
        var before = svc.Items.Count;
        var added = svc.Add("Note", "test-value", "Ctrl+Shift+N", isSecret: false);
        Assert.Equal(before + 1, svc.Items.Count);

        svc.Update(added with { Value = "test-value-2" });
        Assert.Equal("test-value-2", svc.Items.Single(s => s.Id == added.Id).Value);

        svc.Delete(added.Id);
        Assert.Equal(before, svc.Items.Count);
    }
}

public class SnippetSeederTests
{
    [Fact]
    public void SeedsWhenEmpty()
    {
        var seeded = SnippetSeeder.SeedIfEmpty([], () => "fixed-id");
        Assert.Equal(2, seeded.Count);
        Assert.All(seeded, s => Assert.Equal("", s.Value));
        Assert.Contains(seeded, s => s.Name == "Email" && s.Hotkey == "Ctrl+Shift+R" && !s.IsSecret);
        Assert.Contains(seeded, s => s.Name == "Password" && s.Hotkey == "Ctrl+Shift+E" && s.IsSecret);
    }

    [Fact]
    public void LeavesNonEmptyListAlone()
    {
        IReadOnlyList<Snippet> existing = [new Snippet("id1", "Other", "test-value", "Ctrl+Shift+O", IsSecret: false)];
        var result = SnippetSeeder.SeedIfEmpty(existing, () => throw new InvalidOperationException("must not be called"));
        Assert.Same(existing, result);
    }
}

public class SnippetConflictsTests
{
    [Fact]
    public void NoIssuesWhenEverythingIsDistinctAndParseable()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("1", "Email", "", "Ctrl+Shift+R", IsSecret: false)];
        Assert.Empty(SnippetConflicts.Find(snippets, "Ctrl+Alt+V"));
    }

    [Fact]
    public void FlagsBlankOrUnparseableHotkeysWithoutThrowing()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("1", "Broken", "", "not a hotkey", IsSecret: false)];
        var issues = SnippetConflicts.Find(snippets, "Ctrl+Alt+V");
        Assert.Single(issues);
        Assert.Equal(HotkeyIssueKind.Unparseable, issues[0].Kind);
        Assert.Equal("Broken", issues[0].OwnerName);
    }

    [Fact]
    public void FlagsPopupHotkeyItselfWhenUnparseable()
    {
        var issues = SnippetConflicts.Find([], "");
        Assert.Single(issues);
        Assert.Equal("Popup", issues[0].OwnerName);
        Assert.Equal(HotkeyIssueKind.Unparseable, issues[0].Kind);
    }

    [Fact]
    public void FlagsSnippetClashingWithThePopupHotkey()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("1", "Email", "", "Ctrl+Alt+V", IsSecret: false)];
        var issues = SnippetConflicts.Find(snippets, "Ctrl+Alt+V");
        Assert.Single(issues);
        Assert.Equal(HotkeyIssueKind.DuplicatesPopupHotkey, issues[0].Kind);
    }

    [Fact]
    public void FlagsTwoSnippetsClashingWithEachOther()
    {
        IReadOnlyList<Snippet> snippets =
        [
            new Snippet("1", "Email", "", "Ctrl+Shift+R", IsSecret: false),
            new Snippet("2", "Password", "", "Ctrl+Shift+R", IsSecret: true),
        ];
        var issues = SnippetConflicts.Find(snippets, "Ctrl+Alt+V");
        Assert.Single(issues);
        Assert.Equal("Password", issues[0].OwnerName);
        Assert.Equal(HotkeyIssueKind.DuplicatesAnotherSnippet, issues[0].Kind);
    }

    [Fact]
    public void DisabledSnippetsAreIgnored()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("1", "Email", "", "Ctrl+Alt+V", IsSecret: false, Enabled: false)];
        Assert.Empty(SnippetConflicts.Find(snippets, "Ctrl+Alt+V"));
    }

    [Fact]
    public void CaseAndSpacingDifferencesStillCountAsTheSameHotkey()
    {
        IReadOnlyList<Snippet> snippets = [new Snippet("1", "Email", "", "ctrl + alt + v", IsSecret: false)];
        var issues = SnippetConflicts.Find(snippets, "Ctrl+Alt+V");
        Assert.Single(issues);
        Assert.Equal(HotkeyIssueKind.DuplicatesPopupHotkey, issues[0].Kind);
    }
}

public class HotkeyFormatTests
{
    [Theory]
    [InlineData("Ctrl+Alt+V")]
    [InlineData("Ctrl+Shift+R")]
    [InlineData("Win+F1")]
    [InlineData("Ctrl+Alt+Shift+Win+9")]
    public void FormatThenParseRoundTrips(string text)
    {
        Assert.True(Hotkey.TryParse(text, out var h));
        var formatted = h!.ToText();
        Assert.True(Hotkey.TryParse(formatted, out var reparsed));
        Assert.Equal(h, reparsed);
    }

    [Fact]
    public void FormatOrdersModifiersCanonically()
    {
        Assert.True(Hotkey.TryParse("Win+Alt+Shift+Ctrl+Q", out var h));
        Assert.Equal("Ctrl+Alt+Shift+Win+Q", h!.ToText());
    }
}
