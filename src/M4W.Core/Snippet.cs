namespace M4W.Core;

/// <summary>
/// A saved value behind its own global hotkey. Immutable: editing a snippet means building a new
/// record with <c>with</c>, never mutating this one in place.
/// </summary>
public sealed record Snippet(
    string Id,
    string Name,
    string Value,
    string Hotkey,
    bool IsSecret,
    bool Enabled = true);
