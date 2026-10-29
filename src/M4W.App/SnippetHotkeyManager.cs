using System;
using System.Collections.Generic;
using System.Linq;
using M4W.Core;

namespace M4W.App;

/// <summary>
/// Maps each enabled snippet's hotkey to a Win32 hotkey id (distinct from the popup's own) and reports
/// snippet ids whose hotkey could not be registered. Call <see cref="Apply"/> again whenever snippets
/// or Settings change; it always unregisters everything first.
/// </summary>
internal sealed class SnippetHotkeyManager : IDisposable
{
    private const int BaseId = 0x4D40; // just past the popup's own id (0x4D34)

    private readonly MessageWindow _messages;
    private readonly ILog _log;
    private readonly Dictionary<int, string> _snippetIdByWinId = [];
    private int _nextId = BaseId;

    public SnippetHotkeyManager(MessageWindow messages, ILog log)
    {
        _messages = messages;
        _log = log;
        _messages.SnippetHotkeyPressed += OnWinHotkey;
    }

    /// <summary>Snippet ids whose hotkey is blank, unparseable, or already taken by another app.</summary>
    public IReadOnlySet<string> UnavailableSnippetIds { get; private set; } = new HashSet<string>();

    public event Action<string>? SnippetTriggered; // snippet id

    public void Apply(IReadOnlyList<Snippet> snippets)
    {
        foreach (var winId in _snippetIdByWinId.Keys) _messages.UnregisterSnippetHotkey(winId);
        _snippetIdByWinId.Clear();

        var unavailable = new HashSet<string>();
        foreach (var s in snippets.Where(s => s.Enabled))
        {
            if (!Hotkey.TryParse(s.Hotkey, out var hotkey) || hotkey is null)
            {
                unavailable.Add(s.Id);
                continue;
            }
            var winId = _nextId++;
            if (_messages.RegisterSnippetHotkey(winId, hotkey))
            {
                _snippetIdByWinId[winId] = s.Id;
            }
            else
            {
                unavailable.Add(s.Id);
                _log.Warn($"snippet '{s.Name}' hotkey {s.Hotkey} unavailable (already taken)");
            }
        }
        UnavailableSnippetIds = unavailable;
    }

    public void Dispose()
    {
        foreach (var winId in _snippetIdByWinId.Keys) _messages.UnregisterSnippetHotkey(winId);
        _snippetIdByWinId.Clear();
        _messages.SnippetHotkeyPressed -= OnWinHotkey;
    }

    private void OnWinHotkey(int winId)
    {
        if (_snippetIdByWinId.TryGetValue(winId, out var snippetId)) SnippetTriggered?.Invoke(snippetId);
    }
}
