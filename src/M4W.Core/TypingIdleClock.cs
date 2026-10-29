namespace M4W.Core;

/// <summary>
/// Decides when typing capture should auto-disable after a stretch of no keystrokes. Pure function of
/// two timestamps so it is testable with an injected clock; no timer lives here.
/// </summary>
public static class TypingIdleClock
{
    /// <summary>True once <paramref name="now"/> is at least <paramref name="idleOffMinutes"/> past <paramref name="lastKeystroke"/>.</summary>
    public static bool ShouldAutoOff(DateTimeOffset lastKeystroke, DateTimeOffset now, int idleOffMinutes) =>
        now - lastKeystroke >= TimeSpan.FromMinutes(idleOffMinutes);
}
