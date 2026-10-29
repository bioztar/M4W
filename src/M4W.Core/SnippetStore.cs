using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace M4W.Core;

/// <summary>
/// SQLite persistence for snippets, in the same database file as clipboard history (a second connection
/// to the same path — SQLite allows that). Each value is encrypted through <see cref="IProtector"/>,
/// exactly like history blobs; nothing plaintext touches disk. Additive: creates its own table, never
/// touches <c>items</c>.
/// </summary>
public sealed class SnippetStore : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly IProtector _protector;
    private readonly ILog _log;

    private SnippetStore(SqliteConnection db, IProtector protector, ILog log)
    {
        _db = db;
        _protector = protector;
        _log = log;
        Exec("""
            CREATE TABLE IF NOT EXISTS snippets(
              id TEXT PRIMARY KEY,
              name TEXT NOT NULL,
              hotkey TEXT NOT NULL,
              is_secret INTEGER NOT NULL DEFAULT 0,
              enabled INTEGER NOT NULL DEFAULT 1,
              value BLOB NOT NULL);
            """);
    }

    public static SnippetStore Open(string path, IProtector protector, ILog log)
    {
        if (path != ":memory:") Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var db = new SqliteConnection($"Data Source={path}");
        try
        {
            db.Open();
            return new SnippetStore(db, protector, log);
        }
        catch
        {
            db.Dispose();
            throw;
        }
    }

    /// <summary>All snippets. A row whose value fails to decrypt (wrong user) is skipped and logged, never fatal.</summary>
    public IReadOnlyList<Snippet> LoadAll()
    {
        var result = new List<Snippet>();
        using var cmd = Command("SELECT id,name,hotkey,is_secret,enabled,value FROM snippets");
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.GetString(0);
            string value;
            try { value = Encoding.UTF8.GetString(_protector.Unprotect((byte[])r.GetValue(5))); }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                _log.Warn($"snippet {id}: cannot decrypt, skipped ({ex.GetType().Name})");
                continue;
            }
            result.Add(new Snippet(id, r.GetString(1), value, r.GetString(2), r.GetInt32(3) != 0, r.GetInt32(4) != 0));
        }
        return result;
    }

    public void Insert(Snippet s)
    {
        using var cmd = Command("INSERT INTO snippets(id,name,hotkey,is_secret,enabled,value) VALUES($id,$n,$h,$s,$e,$v)");
        BindAll(cmd, s);
        cmd.ExecuteNonQuery();
    }

    public void Update(Snippet s)
    {
        using var cmd = Command("UPDATE snippets SET name=$n,hotkey=$h,is_secret=$s,enabled=$e,value=$v WHERE id=$id");
        BindAll(cmd, s);
        cmd.ExecuteNonQuery();
    }

    public void Delete(string id)
    {
        using var cmd = Command("DELETE FROM snippets WHERE id=$id");
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();

    private void BindAll(SqliteCommand cmd, Snippet s)
    {
        cmd.Parameters.AddWithValue("$id", s.Id);
        cmd.Parameters.AddWithValue("$n", s.Name);
        cmd.Parameters.AddWithValue("$h", s.Hotkey);
        cmd.Parameters.AddWithValue("$s", s.IsSecret ? 1 : 0);
        cmd.Parameters.AddWithValue("$e", s.Enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$v", _protector.Protect(Encoding.UTF8.GetBytes(s.Value)));
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
}
