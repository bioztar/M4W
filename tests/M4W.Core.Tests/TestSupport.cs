using System.Security.Cryptography;
using M4W.Core;

namespace M4W.Core.Tests;

/// <summary>Reversible fake for DPAPI: XOR plus a magic prefix, so a wrong-user blob fails like the real thing.</summary>
public sealed class FakeProtector : IProtector
{
    private static readonly byte[] Magic = [0x4D, 0x34, 0x57];

    public byte[] Protect(byte[] plain) => [.. Magic, .. plain.Select(b => (byte)(b ^ 0x5A))];

    public byte[] Unprotect(byte[] protectedBytes)
    {
        if (protectedBytes.Length < Magic.Length || !protectedBytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new CryptographicException("not protected by this user");
        return protectedBytes.Skip(Magic.Length).Select(b => (byte)(b ^ 0x5A)).ToArray();
    }
}

public sealed class ListLog : ILog
{
    public List<string> Lines { get; } = [];
    public void Info(string message) => Lines.Add("INFO " + message);
    public void Warn(string message) => Lines.Add("WARN " + message);
    public void Error(string message, Exception? ex = null) => Lines.Add("ERROR " + message);
}

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "m4w-tests-" + Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(Path, true); } catch (IOException) { }
    }
}

public static class Make
{
    public static HistoryService History(int max = 1000, ILog? log = null, IProtector? protector = null) =>
        new(SqliteHistoryStore.Open(":memory:", protector ?? new FakeProtector(), log ?? NullLog.Instance), max, log ?? NullLog.Instance);
}
