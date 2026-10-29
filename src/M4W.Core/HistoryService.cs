using System.Text;

namespace M4W.Core;

/// <summary>
/// The clipboard history: dedupe/bump, retention, pinning and search over an in-memory snapshot backed
/// by <see cref="SqliteHistoryStore"/>. Not thread-safe: call from one thread (the UI thread).
/// </summary>
public sealed class HistoryService
{
    public const int MaxTextChars = 1_000_000;
    public const int MaxImageBytes = 25 * 1024 * 1024;
    private const int PreviewChars = 200;

    private readonly SqliteHistoryStore _store;
    private readonly ILog _log;
    private IReadOnlyList<ClipEntry> _items = [];
    private long _lastStamp;

    public HistoryService(SqliteHistoryStore store, int maxItems, ILog log)
    {
        _store = store;
        _log = log;
        MaxItems = maxItems;
        Load();
    }

    public int MaxItems { get; private set; }

    /// <summary>Changes the retention limit live and immediately trims if the new limit is smaller.</summary>
    public void SetMaxItems(int maxItems)
    {
        MaxItems = maxItems;
        EnforceRetention();
        Changed?.Invoke();
    }

    /// <summary>Current items, most recently used first. The list is replaced, never mutated, on every change.</summary>
    public IReadOnlyList<ClipEntry> Items => _items;

    public event Action? Changed;

    public ClipEntry? AddText(string? text, ClipSource source = ClipSource.Copied)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Length > MaxTextChars)
        {
            _log.Warn($"text copy skipped: {text.Length} chars exceeds limit");
            return null;
        }
        return Add(ClipKind.Text, Encoding.UTF8.GetBytes(text), text, source);
    }

    public ClipEntry? AddImage(byte[] png)
    {
        if (png.Length == 0) return null;
        if (png.Length > MaxImageBytes)
        {
            _log.Warn($"image copy skipped: {png.Length} bytes exceeds limit");
            return null;
        }
        return Add(ClipKind.Image, png, null, ClipSource.Copied);
    }

    /// <summary>Marks an item as just used (moves it to the top). Returns the updated entry, or null if gone.</summary>
    public ClipEntry? Touch(long id)
    {
        var current = _items.FirstOrDefault(e => e.Id == id);
        if (current is null) return null;
        var bumped = current with { UsedAt = NextStamp() };
        _store.SetUsedAt(id, bumped.UsedAt);
        _items = [bumped, .. _items.Where(e => e.Id != id)];
        Changed?.Invoke();
        return bumped;
    }

    public void TogglePin(long id)
    {
        var current = _items.FirstOrDefault(e => e.Id == id);
        if (current is null) return;
        var updated = current with { Pinned = !current.Pinned };
        _store.SetPinned(id, updated.Pinned);
        _items = _items.Select(e => e.Id == id ? updated : e).ToArray();
        Changed?.Invoke();
    }

    public void Delete(long id)
    {
        if (_items.All(e => e.Id != id)) return;
        _store.Delete(id);
        _items = _items.Where(e => e.Id != id).ToArray();
        Changed?.Invoke();
    }

    /// <summary>Removes every unpinned item.</summary>
    public int ClearUnpinned()
    {
        var doomed = _items.Where(e => !e.Pinned).Select(e => e.Id).ToArray();
        if (doomed.Length == 0) return 0;
        _store.DeleteMany(doomed);
        _items = _items.Where(e => e.Pinned).ToArray();
        Changed?.Invoke();
        return doomed.Length;
    }

    public byte[]? LoadImage(long id) => _store.LoadBlob(id);

    /// <summary>Filters the history. Empty query returns everything; image items only match the empty query.</summary>
    public IReadOnlyList<ClipEntry> Search(string query, bool exact)
    {
        if (query.Length == 0) return _items;
        if (exact)
        {
            return _items
                .Where(e => e.Text is not null && e.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        return _items
            .Select(e => (Entry: e, Score: e.Text is null ? null : FuzzyMatcher.Score(query, e.Text)))
            .Where(x => x.Score is not null)
            .OrderByDescending(x => x.Score) // stable: ties keep most-recent-first
            .Select(x => x.Entry)
            .ToArray();
    }

    public static string MakePreview(string text)
    {
        var sb = new StringBuilder(Math.Min(text.Length, PreviewChars + 1));
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c)) { pendingSpace = sb.Length > 0; continue; }
            if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
            sb.Append(c);
            if (sb.Length > PreviewChars) return sb.ToString(0, PreviewChars) + "…";
        }
        return sb.ToString();
    }

    private ClipEntry Add(ClipKind kind, byte[] content, string? text, ClipSource source)
    {
        var hash = _store.ComputeHash(kind, content);
        var existing = _items.FirstOrDefault(e => e.Hash == hash);
        if (existing is not null) return Touch(existing.Id)!;

        var stamp = NextStamp();
        var id = _store.Insert(kind, hash, content, stamp, source);
        var entry = new ClipEntry(id, kind, text, text is null ? "Image" : MakePreview(text), hash, false, stamp, source);
        _items = [entry, .. _items];
        EnforceRetention(keepId: id);
        Changed?.Invoke();
        return entry;
    }

    private void Load()
    {
        var entries = _store.LoadAll()
            .Select(r => new ClipEntry(r.Id, r.Kind, r.Text, r.Text is null ? "Image" : MakePreview(r.Text), r.Hash, r.Pinned, r.UsedAt, r.Source))
            .ToArray();
        _lastStamp = entries.Length == 0 ? 0 : entries.Max(e => e.UsedAt);
        _items = entries;
        EnforceRetention();
    }

    /// <summary>
    /// Drops the oldest unpinned items until within the limit. Pinned items are never dropped, even over
    /// the limit, and neither is <paramref name="keepId"/> (the copy that was just made).
    /// </summary>
    private void EnforceRetention(long? keepId = null)
    {
        if (_items.Count <= MaxItems) return;
        var excess = _items.Count - MaxItems;
        var doomed = _items.Where(e => !e.Pinned && e.Id != keepId).TakeLast(excess).Select(e => e.Id).ToHashSet();
        if (doomed.Count == 0) return;
        _store.DeleteMany(doomed);
        _items = _items.Where(e => !doomed.Contains(e.Id)).ToArray();
    }

    /// <summary>Strictly increasing timestamp so ordering never ties, even for copies in the same tick.</summary>
    private long NextStamp() => _lastStamp = Math.Max(DateTime.UtcNow.Ticks, _lastStamp + 1);
}
