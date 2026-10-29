namespace M4W.Core;

public enum HotkeyIssueKind
{
    Unparseable,
    DuplicatesPopupHotkey,
    DuplicatesAnotherSnippet,
}

/// <summary>One hotkey problem to surface in the Settings UI, never a crash.</summary>
public sealed record HotkeyIssue(string OwnerName, string HotkeyText, HotkeyIssueKind Kind);

/// <summary>
/// Pure hotkey-conflict detection over the popup hotkey plus every enabled snippet. A blank or
/// unparseable hotkey is reported as <see cref="HotkeyIssueKind.Unparseable"/>, never thrown.
/// </summary>
public static class SnippetConflicts
{
    public static IReadOnlyList<HotkeyIssue> Find(IReadOnlyList<Snippet> snippets, string popupHotkeyText)
    {
        var issues = new List<HotkeyIssue>();
        var claimed = new Dictionary<string, string>(); // normalized hotkey -> name of whoever claimed it first

        if (Hotkey.TryParse(popupHotkeyText, out var popupHotkey) && popupHotkey is not null)
            claimed[Normalize(popupHotkey)] = "Popup";
        else
            issues.Add(new HotkeyIssue("Popup", popupHotkeyText, HotkeyIssueKind.Unparseable));

        foreach (var s in snippets.Where(s => s.Enabled))
        {
            if (!Hotkey.TryParse(s.Hotkey, out var hk) || hk is null)
            {
                issues.Add(new HotkeyIssue(s.Name, s.Hotkey, HotkeyIssueKind.Unparseable));
                continue;
            }

            var key = Normalize(hk);
            if (claimed.TryGetValue(key, out var owner))
            {
                issues.Add(new HotkeyIssue(s.Name, s.Hotkey, owner == "Popup" ? HotkeyIssueKind.DuplicatesPopupHotkey : HotkeyIssueKind.DuplicatesAnotherSnippet));
            }
            else
            {
                claimed[key] = s.Name;
            }
        }
        return issues;
    }

    private static string Normalize(Hotkey h) => $"{h.Modifiers}+{h.Key}";
}
