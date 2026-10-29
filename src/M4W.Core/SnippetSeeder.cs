namespace M4W.Core;

/// <summary>
/// First-run seeding: if there are no snippets yet, offer the two Vitaly asked for, both with an empty
/// value (he fills them in later). Pure: takes what's there, returns what should be there; the caller
/// persists the result and logs. Never touches a real email or password — the seeded values are blank.
/// </summary>
public static class SnippetSeeder
{
    public const string EmailName = "Email";
    public const string EmailHotkey = "Ctrl+Shift+R";
    public const string PasswordName = "Password";
    public const string PasswordHotkey = "Ctrl+Shift+E";

    /// <summary>Returns <paramref name="existing"/> unchanged unless it is empty, in which case it returns the two seed snippets.</summary>
    public static IReadOnlyList<Snippet> SeedIfEmpty(IReadOnlyList<Snippet> existing, Func<string> newId)
    {
        if (existing.Count > 0) return existing;
        return
        [
            new Snippet(newId(), EmailName, Value: "", EmailHotkey, IsSecret: false),
            new Snippet(newId(), PasswordName, Value: "", PasswordHotkey, IsSecret: true),
        ];
    }
}
