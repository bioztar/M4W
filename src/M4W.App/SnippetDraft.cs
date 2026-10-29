using System.ComponentModel;
using System.Runtime.CompilerServices;
using M4W.Core;

namespace M4W.App;

/// <summary>Editable draft of a snippet for the Settings window. Nothing here is persisted until Save commits it via <see cref="SnippetService.Put"/>.</summary>
internal sealed class SnippetDraft : INotifyPropertyChanged
{
    private string _name = "";
    private string _value = "";
    private string _hotkeyText = "";
    private bool _isSecret;
    private bool _enabled = true;

    public string Id { get; init; } = "";
    /// <summary>True if this snippet's hotkey was unavailable the last time it was registered (set when the window opens; not live).</summary>
    public bool WasUnavailable { get; init; }

    public string Name { get => _name; set => SetField(ref _name, value, nameof(MaskedPreview)); }
    public string Value { get => _value; set => SetField(ref _value, value, nameof(MaskedPreview)); }
    public string HotkeyText { get => _hotkeyText; set => SetField(ref _hotkeyText, value); }
    public bool IsSecret { get => _isSecret; set => SetField(ref _isSecret, value, nameof(MaskedPreview)); }
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value, nameof(StatusText)); }

    public string MaskedPreview => Value.Length == 0 ? "(no value)" : IsSecret ? "••••••••" : Value;
    public string StatusText => WasUnavailable ? "hotkey unavailable" : Enabled ? "" : "disabled";

    public static SnippetDraft From(Snippet s, bool wasUnavailable) => new()
    {
        Id = s.Id, Name = s.Name, Value = s.Value, HotkeyText = s.Hotkey, IsSecret = s.IsSecret, Enabled = s.Enabled, WasUnavailable = wasUnavailable,
    };

    public Snippet ToSnippet() => new(Id, Name, Value, HotkeyText, IsSecret, Enabled);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, params string[] alsoChanged)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); // null: refresh every binding on this item, cheap for a settings list
        foreach (var name in alsoChanged) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
