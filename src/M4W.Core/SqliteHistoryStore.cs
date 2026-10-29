using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace M4W.Core;

/// <summary>
/// SQLite persistence. Every content blob is encrypted through <see cref="IProtector"/>; the only
/// derived column is a keyed hash (HMAC-SHA256, key itself stored protected) used for dedupe, so no
/// plaintext or unkeyed digest of clipboard content ever touches the disk.
/// </summary>
public sealed class SqliteHistoryStore : IDisposable
{
    public sealed record Row(long Id, ClipKind Kind, string Hash, bool Pinned, long UsedAt, string? Text);

    private readonly SqliteConnection _db;
    private readonly IProtector _protector;
    private readonly ILog _log;
    private readonly byte[] _hashKey;

    private SqliteHistoryStore(SqliteConnection db, IProtector protector, ILog log)
    {
        _db = db;
        _protector = protector;
        _log = log;
        Exec("""
            CREATE TABLE IF NOT EXISTS items(
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              kind INTEGER NOT NULL,
              hash TEXT NOT NULL,
              blob BLOB NOT NULL,
              pinned INTEGER NOT NULL DEFAULT 0,
              used_at INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_items_used ON items(used_at DESC);
            CREATE TABLE IF NOT EXISTS meta(k TEXT PRIMARY KEY, v BLOB NOT NULL);
            """);
        _hashKey = LoadOrCreateHashKey();
    }

    private const int SqliteCorrupt = 11;
    private const int SqliteNotADb = 26;

    /// <summary>Only these mean the file itself is damaged. BUSY, READONLY, IOERR, FULL, CANTOPEN are environmental and must never cost the user their history.</summary>
    internal static bool IsCorruption(SqliteException ex) => ex.SqliteErrorCode is SqliteCorrupt or SqliteNotADb;

    /// <summary>
    /// Opens the database and reads every row once, so corruption anywhere surfaces here. A corrupt file
    /// (and only that) is moved aside and a fresh one created; any other failure is logged and rethrown.
    /// </summary>
    public static SqliteHistoryStore OpenOrRecover(string path, IProtector protector, ILog log)
    {
        try
        {
            var store = Open(path, protector, log);
            try
            {
                store.LoadAll(); // probe: HistoryService reads this again, outside any recovery
                return store;
            }
            catch
            {
                store.Dispose();
                throw;
            }
        }
        catch (SqliteException ex) when (IsCorruption(ex) && path != ":memory:" && File.Exists(path))
        {
            var aside = $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            log.Error($"history db corrupt (sqlite code {ex.SqliteErrorCode}), moving to {Path.GetFileName(aside)} and starting fresh", ex);
            SqliteConnection.ClearAllPools();
            File.Move(path, aside);
            return Open(path, protector, log);
        }
        catch (SqliteException ex)
        {
            log.Error($"history db could not be opened (sqlite code {ex.SqliteErrorCode}); file left untouched", ex);
            throw;
        }
    }

    public static SqliteHistoryStore Open(string path, IProtector protector, ILog log)
    {
        if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var db = new SqliteConnection($"Data Source={path}");
        try
        {
            db.Open();
            return new SqliteHistoryStore(db, protector, log);
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    public string ComputeHash(ClipKind kind, byte[] content)
    {
        using var h = new HMACSHA256(_hashKey);
        h.TransformBlock([(byte)kind], 0, 1, null, 0);
        h.TransformFinalBlock(content, 0, content.Length);
        return Convert.ToHexString(h.Hash!);
    }

    public long Insert(ClipKind kind, string hash, byte[] content, long usedAt)
    {
        using var cmd = Command("INSERT INTO items(kind,hash,blob,pinned,used_at) VALUES($k,$h,$b,0,$u); SELECT last_insert_rowid();");
        cmd.Parameters.AddWithValue("$k", (int)kind);
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$b", _protector.Protect(content));
        cmd.Parameters.AddWithValue("$u", usedAt);
        return (long)cmd.ExecuteScalar()!;
    }

    public void SetUsedAt(long id, long usedAt) => Run("UPDATE items SET used_at=$v WHERE id=$id", id, usedAt);
    public void SetPinned(long id, bool pinned) => Run("UPDATE items SET pinned=$v WHERE id=$id", id, pinned ? 1 : 0);
    public void Delete(long id) => Run("DELETE FROM items WHERE id=$id", id, null);

    public void DeleteMany(IEnumerable<long> ids)
    {
        using var tx = _db.BeginTransaction();
        foreach (var id in ids) Run("DELETE FROM items WHERE id=$id", id, null);
        tx.Commit();
    }

    /// <summary>All rows, most recently used first. Text is decrypted; rows that fail to decrypt are skipped and logged.</summary>
    public IReadOnlyList<Row> LoadAll()
    {
        var rows = new List<Row>();
        using var cmd = Command("SELECT id,kind,hash,blob,pinned,used_at FROM items ORDER BY used_at DESC, id DESC");
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.GetInt64(0);
            var kind = (ClipKind)r.GetInt32(1);
            string? text = null;
            if (kind == ClipKind.Text)
            {
                try { text = Encoding.UTF8.GetString(_protector.Unprotect((byte[])r.GetValue(3))); }
                catch (Exception ex) when (ex is CryptographicException or ArgumentException)
                {
                    _log.Warn($"item {id}: cannot decrypt, skipped ({ex.GetType().Name})");
                    continue;
                }
            }
            rows.Add(new Row(id, kind, r.GetString(2), r.GetInt32(4) != 0, r.GetInt64(5), text));
        }
        return rows;
    }

    public byte[]? LoadBlob(long id)
    {
        using var cmd = Command("SELECT blob FROM items WHERE id=$id");
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteScalar() is not byte[] blob) return null;
        try { return _protector.Unprotect(blob); }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            _log.Warn($"item {id}: cannot decrypt blob ({ex.GetType().Name})");
            return null;
        }
    }

    public void Dispose() => _db.Dispose();

    private byte[] LoadOrCreateHashKey()
    {
        using (var get = Command("SELECT v FROM meta WHERE k='hmac_key'"))
        {
            if (get.ExecuteScalar() is byte[] stored)
            {
                try { return _protector.Unprotect(stored); }
                catch (Exception ex) when (ex is CryptographicException or ArgumentException)
                {
                    _log.Warn($"hash key unreadable, generating a new one ({ex.GetType().Name})");
                }
            }
        }
        var key = RandomNumberGenerator.GetBytes(32);
        using var put = Command("INSERT OR REPLACE INTO meta(k,v) VALUES('hmac_key',$v)");
        put.Parameters.AddWithValue("$v", _protector.Protect(key));
        put.ExecuteNonQuery();
        return key;
    }

    private SqliteCommand Command(string sql)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    private void Exec(string sql)
    {
        using var cmd = Command(sql);
        cmd.ExecuteNonQuery();
    }

    private void Run(string sql, long id, long? value)
    {
        using var cmd = Command(sql);
        cmd.Parameters.AddWithValue("$id", id);
        if (value is not null) cmd.Parameters.AddWithValue("$v", value.Value);
        cmd.ExecuteNonQuery();
    }
}
