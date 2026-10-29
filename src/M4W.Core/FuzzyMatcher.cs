namespace M4W.Core;

/// <summary>
/// Case-insensitive fuzzy subsequence matcher in the fzf v1 style: find the earliest complete match
/// forwards, tighten it backwards, then score the window. Higher is better; null means no match.
/// </summary>
public static class FuzzyMatcher
{
    private const int CharScore = 16;
    private const int ConsecutiveBonus = 12;
    private const int BoundaryBonus = 10;
    private const int FirstCharBonus = 8;
    private const int GapPenalty = 3;
    /// <summary>Search window per item; keeps 1000 large copies under the keystroke budget.</summary>
    public const int MaxSearchChars = 600;

    public static int? Score(string query, string text)
    {
        if (query.Length == 0) return 0;
        var t = text.Length > MaxSearchChars ? text.AsSpan(0, MaxSearchChars) : text.AsSpan();

        // forward: earliest end of a complete subsequence match
        var qi = 0;
        var end = -1;
        for (var i = 0; i < t.Length; i++)
        {
            if (Eq(t[i], query[qi]) && ++qi == query.Length) { end = i; break; }
        }
        if (end < 0) return null;

        // backward: tightest start for that end
        qi = query.Length - 1;
        var start = end;
        for (var i = end; i >= 0; i--)
        {
            if (Eq(t[i], query[qi]) && --qi < 0) { start = i; break; }
        }

        return ScoreWindow(query, t, start, end);
    }

    private static int ScoreWindow(string query, ReadOnlySpan<char> t, int start, int end)
    {
        var score = 0;
        var qi = 0;
        var prevMatch = -2;
        for (var i = start; i <= end && qi < query.Length; i++)
        {
            if (!Eq(t[i], query[qi])) continue;
            score += CharScore;
            if (i == prevMatch + 1) score += ConsecutiveBonus;
            else if (prevMatch >= 0) score -= GapPenalty * Math.Min(i - prevMatch - 1, 5);
            if (IsBoundary(t, i)) score += BoundaryBonus;
            if (i == 0) score += FirstCharBonus;
            prevMatch = i;
            qi++;
        }
        return score;
    }

    private static bool IsBoundary(ReadOnlySpan<char> t, int i)
    {
        if (i == 0) return true;
        var prev = t[i - 1];
        return !char.IsLetterOrDigit(prev) || (char.IsLower(prev) && char.IsUpper(t[i]));
    }

    private static bool Eq(char a, char b) => a == b || char.ToLowerInvariant(a) == char.ToLowerInvariant(b);
}
