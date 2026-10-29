namespace M4W.Core;

/// <summary>
/// The in-memory snippet list backed by <see cref="SnippetStore"/>, seeded on first load. Not
/// thread-safe: call from one thread (the UI thread), same convention as <see cref="HistoryService"/>.
/// </summary>
public sealed class SnippetService
{
    private readonly SnippetStore _store;
    private readonly ILog _log;
    private IReadOnlyList<Snippet> _items;

    public SnippetService(SnippetStore store, ILog log)
    {
        _store = store;
        _log = log;
        _items = _store.LoadAll();

        var seeded = SnippetSeeder.SeedIfEmpty(_items, () => Guid.NewGuid().ToString("N"));
        if (ReferenceEquals(seeded, _items)) return;
        foreach (var s in seeded)
        {
            _store.Insert(s);
            if (s.Value.Length == 0) _log.Info($"snippet '{s.Name}' has no value yet");
        }
        _items = seeded;
    }

    /// <summary>Current snippets. The list is replaced, never mutated, on every change.</summary>
    public IReadOnlyList<Snippet> Items => _items;

    public event Action? Changed;

    public Snippet Add(string name, string value, string hotkey, bool isSecret)
    {
        var s = new Snippet(Guid.NewGuid().ToString("N"), name, value, hotkey, isSecret);
        _store.Insert(s);
        _items = [.. _items, s];
        Changed?.Invoke();
        return s;
    }

    /// <summary>Replaces the snippet with the same <see cref="Snippet.Id"/>. No-op if it no longer exists.</summary>
    public void Update(Snippet s)
    {
        if (_items.All(x => x.Id != s.Id)) return;
        _store.Update(s);
        _items = _items.Select(x => x.Id == s.Id ? s : x).ToArray();
        Changed?.Invoke();
    }

    public void Delete(string id)
    {
        if (_items.All(x => x.Id != id)) return;
        _store.Delete(id);
        _items = _items.Where(x => x.Id != id).ToArray();
        Changed?.Invoke();
    }
}
