namespace Marbles.Tests;

public class GuideTests
{
    // The guides by their place in QuickGuide.All (the type of a guide is internal)
    public static TheoryData<int> Guides()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < QuickGuide.All.Length; i++) data.Add(i);
        return data;
    }

    [Theory]
    [MemberData(nameof(Guides))]
    public void AGuide_StartsWithItsTitle(int index)
    {
        var guide = QuickGuide.All[index];
        var lines = QuickGuide.Lines(80, guide);
        Assert.Equal(QuickGuide.Title(guide), lines[0].Text);
        Assert.True(lines.Count > 10);
    }

    [Theory]
    [MemberData(nameof(Guides))]
    public void TheTextsOfTheItems_StartInTheSameColumn(int index)
    {
        var guide = QuickGuide.All[index];
        // Kind 2 is an item: its key is padded to the width of the keys of the guide
        var widths = QuickGuide.Lines(80, guide).Where(line => line.Kind == 2).Select(line => line.Key.Length)
            .Distinct().ToList();
        Assert.Single(widths);
    }

    [Theory]
    [MemberData(nameof(Guides))]
    public void TheLines_FitTheWindow(int index)
    {
        var guide = QuickGuide.All[index];
        foreach (var (kind, key, text) in QuickGuide.Lines(60, guide))
        {
            // As drawn: an item after three blanks, its key and a blank; other lines after one blank
            var width = kind == 2 ? 3 + key.Length + 1 + text.Length : 1 + text.Length;
            Assert.True(width <= 60, text);
        }
    }

    [Fact]
    public void TheGuides_HaveDifferentTitles()
    {
        Assert.Equal(QuickGuide.All.Length, QuickGuide.All.Select(QuickGuide.Title).Distinct().Count());
    }
}
