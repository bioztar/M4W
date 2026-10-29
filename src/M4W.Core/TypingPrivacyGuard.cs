namespace M4W.Core;

/// <summary>
/// Decides whether a typed chunk must be discarded for privacy. Pure: the App supplies whether the
/// focused control looks like a password field (from UI Automation), this just applies the setting.
/// If the App's own check is unreliable it must pass true (fail safe, never fail open) — that policy
/// lives at the call site, not here.
/// </summary>
public static class TypingPrivacyGuard
{
    public static bool ShouldSkip(bool skipPasswordFields, bool isPasswordFocused) =>
        skipPasswordFields && isPasswordFocused;
}
