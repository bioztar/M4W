using System.Diagnostics;
using M4W.Core;

namespace M4W.Core.Tests;

public class HistoryServiceTests
{
    [Fact]
    public void AddsNewestFirst()
    {
        var h = Make.History();
        h.AddText("one"); h.AddText("two");
        Assert.Equal(["two", "one"], h.Items.Select(e => e.Text));
    }

    [Fact]
    public void RecopyBumpsInsteadOfDuplicating()
    {
        var h = Make.History();
        var first = h.AddText("one")!;
        h.AddText("two");
        var again = h.AddText("one")!;
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(["one", "two"], h.Items.Select(e => e.Text));
    }

    [Fact]
    public void RecopyKeepsPin()
    {
        var h = Make.History();
        var e = h.AddText("one")!;
        h.TogglePin(e.Id);
        h.AddText("two");
        h.AddText("one");
        Assert.True(h.Items[0].Pinned);
    }

    [Fact]
    public void DuplicateImagesDedupe()
    {
        var h = Make.History();
        h.AddImage([1, 2, 3]); h.AddImage([9]); h.AddImage([1, 2, 3]);
        Assert.Equal(2, h.Items.Count);
        Assert.Equal(ClipKind.Image, h.Items[0].Kind);
    }

    [Fact]
    public void TextAndImageWithSameBytesAreDifferentItems()
    {
        var h = Make.History();
        h.AddText("abc"); h.AddImage("abc"u8.ToArray());
        Assert.Equal(2, h.Items.Count);
    }

    [Fact]
    public void RetentionDropsOldestUnpinned()
    {
        var h = Make.History(max: 3);
        h.AddText("a"); h.AddText("b"); h.AddText("c"); h.AddText("d");
        Assert.Equal(["d", "c", "b"], h.Items.Select(e => e.Text));
    }

    [Fact]
    public void RetentionSkipsPinned()
    {
        var h = Make.History(max: 3);
        var a = h.AddText("a")!;
        h.TogglePin(a.Id);
        h.AddText("b"); h.AddText("c"); h.AddText("d"); h.AddText("e");
        Assert.Equal(["e", "d", "a"], h.Items.Select(e => e.Text));
    }

    [Fact]
    public void PinnedFillingTheLimitStillKeepsTheNewestCopy()
    {
        var h = Make.History(max: 2);
        foreach (var t in new[] { "a", "b" }) h.TogglePin(h.AddText(t)!.Id);
        h.AddText("c");
        Assert.Equal(["c", "b", "a"], h.Items.Select(e => e.Text));
        h.AddText("d"); // c is now the oldest unpinned
        Assert.Equal(["d", "b", "a"], h.Items.Select(e => e.Text));
    }

    [Fact]
    public void ClearUnpinnedKeepsPins()
    {
        var h = Make.History();
        var keep = h.AddText("keep")!;
        h.AddText("drop1"); h.AddText("drop2");
        h.TogglePin(keep.Id);
        Assert.Equal(2, h.ClearUnpinned());
        Assert.Equal(["keep"], h.Items.Select(e => e.Text));
        Assert.Equal(0, h.ClearUnpinned());
    }

    [Fact]
    public void DeleteRemovesItem()
    {
        var h = Make.History();
        var a = h.AddText("a")!;
        h.AddText("b");
        h.Delete(a.Id);
        h.Delete(a.Id); // no-op
        Assert.Equal(["b"], h.Items.Select(e => e.Text));
    }

    [Fact]
    public void TouchUnknownIdReturnsNull()
    {
        var h = Make.History();
        Assert.Null(h.Touch(999));
        h.TogglePin(999); // no-op, no throw
    }

    [Fact]
    public void IgnoresBlankEmptyOversizeAndEmptyImage()
    {
        var log = new ListLog();
        var h = Make.History(log: log);
        Assert.Null(h.AddText(null));
        Assert.Null(h.AddText("   \n\t"));
        Assert.Null(h.AddText(new string('x', HistoryService.MaxTextChars + 1)));
        Assert.Null(h.AddImage([]));
        Assert.Null(h.AddImage(new byte[HistoryService.MaxImageBytes + 1]));
        Assert.Empty(h.Items);
        Assert.Equal(2, log.Lines.Count);
    }

    [Fact]
    public void ChangedFiresOnEveryMutation()
    {
        var h = Make.History();
        var n = 0;
        h.Changed += () => n++;
        var e = h.AddText("a")!;
        h.Touch(e.Id); h.TogglePin(e.Id); h.Delete(e.Id);
        Assert.Equal(4, n);
    }

    [Fact]
    public void ImageRoundTripsThroughEncryptedStore()
    {
        var h = Make.History();
        var e = h.AddImage([5, 6, 7])!;
        Assert.Equal("Image", e.Preview);
        Assert.Equal<byte>([5, 6, 7], h.LoadImage(e.Id)!);
        Assert.Null(h.LoadImage(12345));
    }

    [Fact]
    public void PreviewCollapsesWhitespaceAndTruncates()
    {
        Assert.Equal("a b c", HistoryService.MakePreview("  a \n\t b   c  "));
        var p = HistoryService.MakePreview(new string('x', 500));
        Assert.Equal(201, p.Length);
        Assert.EndsWith("…", p);
    }

    [Fact]
    public void ExactSearchIsSubstringCaseInsensitiveAndKeepsRecencyOrder()
    {
        var h = Make.History();
        h.AddText("Hello World"); h.AddText("nothing"); h.AddText("say hello");
        h.AddImage([1]);
        Assert.Equal(["say hello", "Hello World"], h.Search("HELLO", exact: true).Select(e => e.Text));
        Assert.Empty(h.Search("hlo", exact: true));
    }

    [Fact]
    public void FuzzySearchRanksBetterMatchFirstAndSkipsImages()
    {
        var h = Make.History();
        h.AddText("k-u-b-e-c-t-l scattered");
        h.AddText("kubectl");
        h.AddImage([1]);
        var hits = h.Search("kbctl", exact: false);
        Assert.Equal(2, hits.Count);
        Assert.Equal("kubectl", hits[0].Text);
        Assert.Equal(h.Items.Count, h.Search("", exact: false).Count);
    }

    [Fact]
    public void SearchOn1000ItemsIsUnder50ms()
    {
        var h = Make.History(max: 1000);
        for (var i = 0; i < 1000; i++)
            h.AddText($"item {i} " + string.Concat(Enumerable.Repeat("lorem ipsum dolor sit amet ", 20)));
        h.Search("lorem", false); // warm up JIT
        var sw = Stopwatch.StartNew();
        var hits = h.Search("kbctl", false);
        h.Search("ipsum dolor", false);
        sw.Stop();
        Assert.Empty(hits);
        Assert.True(sw.ElapsedMilliseconds < 50, $"two searches took {sw.ElapsedMilliseconds} ms");
    }
}
