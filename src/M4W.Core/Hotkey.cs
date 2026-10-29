using System.Text.RegularExpressions;

namespace M4W.Core;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Ctrl = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>Parsed "Ctrl+Alt+V" style hotkey. Key is an upper-case letter, digit or F1-F24.</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    private static readonly Regex KeyPattern = new("^([A-Z0-9]|F([1-9]|1[0-9]|2[0-4]))$", RegexOptions.Compiled);

    public static bool TryParse(string? text, out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = HotkeyModifiers.None;
        string? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            var part = raw.ToUpperInvariant();
            switch (part)
            {
                case "CTRL" or "CONTROL": mods |= HotkeyModifiers.Ctrl; break;
                case "ALT": mods |= HotkeyModifiers.Alt; break;
                case "SHIFT": mods |= HotkeyModifiers.Shift; break;
                case "WIN" or "WINDOWS": mods |= HotkeyModifiers.Win; break;
                default:
                    if (key is not null || !KeyPattern.IsMatch(part)) return false;
                    key = part;
                    break;
            }
        }

        if (key is null || mods == HotkeyModifiers.None) return false;
        hotkey = new Hotkey(mods, key);
        return true;
    }

    /// <summary>Windows virtual-key code for <see cref="Key"/> (letters/digits are their ASCII code, F-keys start at 0x70).</summary>
    public int VirtualKey => Key.Length > 1 && Key[0] == 'F' ? 0x70 + int.Parse(Key[1..]) - 1 : Key[0];
}
