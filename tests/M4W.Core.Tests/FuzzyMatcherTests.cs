using M4W.Core;

namespace M4W.Core.Tests;

public class FuzzyMatcherTests
{
    [Fact] public void KbctlFindsKubectl() => Assert.NotNull(FuzzyMatcher.Score("kbctl", "kubectl get pods"));
    [Fact] public void NonSubsequenceDoesNotMatch() => Assert.Null(FuzzyMatcher.Score("kbctlx", "kubectl"));
    [Fact] public void OrderMatters() => Assert.Null(FuzzyMatcher.Score("lctbuk", "kubectl"));
    [Fact] public void CaseInsensitive() => Assert.NotNull(FuzzyMatcher.Score("KUBE", "kubectl"));
    [Fact] public void EmptyQueryMatchesEverything() => Assert.Equal(0, FuzzyMatcher.Score("", "anything"));

    [Fact]
    public void ConsecutiveBeatsScattered()
    {
        var tight = FuzzyMatcher.Score("abc", "xx abc xx")!.Value;
        var loose = FuzzyMatcher.Score("abc", "a-x-b-x-c")!.Value;
        Assert.True(tight > loose, $"{tight} should beat {loose}");
    }

    [Fact]
    public void WordBoundaryBeatsMidWord()
    {
        var boundary = FuzzyMatcher.Score("fb", "foo bar")!.Value;
        var mid = FuzzyMatcher.Score("fb", "xfxxbx")!.Value;
        Assert.True(boundary > mid);
    }

    [Fact]
    public void CamelCaseHumpCountsAsBoundary()
    {
        var hump = FuzzyMatcher.Score("fb", "fooBar")!.Value;
        var flat = FuzzyMatcher.Score("fb", "foobar")!.Value;
        Assert.True(hump > flat);
    }

    [Fact]
    public void TightensToBestWindow()
    {
        // greedy forward match starts at the first 'a'; backward pass must pick the closer pair
        var spread = FuzzyMatcher.Score("ab", "a....... ab")!.Value;
        var adjacent = FuzzyMatcher.Score("ab", "ab")!.Value;
        Assert.True(spread >= adjacent - 20); // window is "ab", not "a....... ab"
    }

    [Fact]
    public void OnlySearchesLeadingWindow()
    {
        var text = new string('x', FuzzyMatcher.MaxSearchChars) + "needle";
        Assert.Null(FuzzyMatcher.Score("needle", text));
    }
}
