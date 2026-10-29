using System.Text;
using M4W.Core;

namespace M4W.Core.Tests;

public class StoreTests
{
    [Fact]
    public void HistorySurvivesReopenWithPinsAndOrder()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        using (var store = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance))
        {
            var h = new HistoryService(store, 1000, NullLog.Instance);
            h.TogglePin(h.AddText("first")!.Id);
            h.AddText("second");
            h.AddImage([1, 2, 3]);
        }
        using var again = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance);
        var h2 = new HistoryService(again, 1000, NullLog.Instance);
        Assert.Equal(3, h2.Items.Count);
        Assert.Equal(ClipKind.Image, h2.Items[0].Kind);
        Assert.Equal("second", h2.Items[1].Text);
        Assert.True(h2.Items[2].Pinned);
        // dedupe still works after reload (keyed hash is stable)
        Assert.Equal(h2.Items[1].Id, h2.AddText("second")!.Id);
    }

    [Fact]
    public void ReopenWithSmallerLimitTrimsOldest()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        using (var store = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance))
        {
            var h = new HistoryService(store, 1000, NullLog.Instance);
            foreach (var t in new[] { "a", "b", "c", "d" }) h.AddText(t);
        }
        using var again = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance);
        var h2 = new HistoryService(again, 2, NullLog.Instance);
        Assert.Equal(["d", "c"], h2.Items.Select(e => e.Text));
    }

    [Fact]
    public void NoPlaintextOnDisk()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        const string secret = "hunter2-super-secret-passphrase";
        var imageMarker = Encoding.ASCII.GetBytes("IMAGEBYTES-MARKER-1234567890");
        using (var store = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance))
        {
            var h = new HistoryService(store, 1000, NullLog.Instance);
            h.AddText(secret);
            h.AddImage(imageMarker);
        }
        SqliteConnectionPoolClear();
        var raw = File.ReadAllBytes(path);
        Assert.True(raw.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)) < 0, "text found in plaintext");
        Assert.True(raw.AsSpan().IndexOf(imageMarker) < 0, "image bytes found in plaintext");
    }

    [Fact]
    public void ProtectRoundTripThroughFakeProtector()
    {
        var p = new FakeProtector();
        var plain = Encoding.UTF8.GetBytes("round trip ✓");
        var enc = p.Protect(plain);
        Assert.NotEqual(plain, enc);
        Assert.Equal(plain, p.Unprotect(enc));
        Assert.ThrowsAny<Exception>(() => p.Unprotect([1, 2]));
    }

    [Fact]
    public void RowsFromAnotherUserAreSkippedNotFatal()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        using (var store = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance))
            new HistoryService(store, 1000, NullLog.Instance).AddText("mine");

        var log = new ListLog();
        using var other = SqliteHistoryStore.Open(path, new RejectingProtector(), log);
        var h = new HistoryService(other, 1000, log);
        Assert.Empty(h.Items);
        Assert.Contains(log.Lines, l => l.StartsWith("WARN") && l.Contains("cannot decrypt"));
        Assert.DoesNotContain(log.Lines, l => l.Contains("mine"));
        Assert.NotNull(h.AddText("new"));
    }

    [Fact]
    public void UndecryptableImageBlobReturnsNull()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        long id;
        using (var store = SqliteHistoryStore.Open(path, new FakeProtector(), NullLog.Instance))
            id = new HistoryService(store, 1000, NullLog.Instance).AddImage([1, 2, 3])!.Id;
        using var other = SqliteHistoryStore.Open(path, new RejectingProtector(), NullLog.Instance);
        Assert.Null(other.LoadBlob(id));
    }

    [Fact]
    public void CorruptDatabaseIsMovedAsideAndRecreated()
    {
        using var dir = new TempDir();
        var path = dir.File("history.db");
        File.WriteAllText(path, "this is not a sqlite database, just garbage text padding padding padding padding");
        var log = new ListLog();
        using (var store = SqliteHistoryStore.OpenOrRecover(path, new FakeProtector(), log))
            Assert.NotNull(new HistoryService(store, 1000, log).AddText("works"));
        Assert.Single(Directory.GetFiles(dir.Path, "history.db.corrupt-*"));
        Assert.Contains(log.Lines, l => l.StartsWith("ERROR"));
    }

    private static void SqliteConnectionPoolClear() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

    private sealed class RejectingProtector : IProtector
    {
        public byte[] Protect(byte[] plain) => new FakeProtector().Protect(plain);
        public byte[] Unprotect(byte[] protectedBytes) => throw new System.Security.Cryptography.CryptographicException("wrong user");
    }
}
