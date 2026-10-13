namespace Marbles.Tests;

/// <summary>
/// The layout of the frame with tabs (TabFrame): the tabs, the top line of the frame and the sub-tabs
/// </summary>
public class TabLayoutTests
{
    private static readonly string[] Names = { "Mode", "Configuration" };

    [Fact]
    public void TheTabs_AreDrawnFromTheLeftCornerOfTheFrame()
    {
        var (tops, names, starts) = TabLayout.Tabs(Names, '.');
        Assert.Equal(" .------. .---------------.", tops);
        Assert.Equal(" | Mode | | Configuration |", names);
        Assert.Equal(new[] { 3, 12 }, starts);
    }

    [Theory]
    // The top line is open below the tab shown, and closed below the others
    [InlineData(0, ".'      '-'---------------'-----.")]
    [InlineData(1, ".'------'-'               '-----.")]
    public void TheTopOfTheFrame_IsOpenBelowTheTabShown(int selected, string expected)
    {
        Assert.Equal(expected, TabLayout.Edge(Names, selected, expected.Length, '.', '\''));
    }

    [Fact]
    public void TheTopOfTheFrame_UsesTheCornersOfTheStyle()
    {
        Assert.Equal("++      +-+---------------+-----+", TabLayout.Edge(Names, 0, 33, '+', '+'));
    }

    [Fact]
    public void LongNames_AreShortened_ToFit()
    {
        // Two tabs in 20 columns: the longest name is shortened first
        var fitted = TabLayout.Fit(new[] { "Mode", "Configuration" }, 20);
        Assert.Equal("Mode", fitted[0]);
        Assert.True(1 + (fitted[0].Length + 4) + 1 + (fitted[1].Length + 4) + 1 <= 20);
        Assert.StartsWith("Conf", fitted[1]);
        // Names that fit are kept
        Assert.Equal(Names, TabLayout.Fit(Names, 78));
    }

    [Fact]
    public void TheSubTabs_HaveTwoSpacesBetweenThem()
    {
        Assert.Equal(new[] { 1, 11 }, TabLayout.SubTabStarts(new[] { "Page 1", "Page 2" }));
    }

    [Theory]
    // Tab: the next page, round from the last to the first; Shift+Tab: the one before
    [InlineData(0, 3, false, 1)]
    [InlineData(2, 3, false, 0)]
    [InlineData(0, 3, true, 2)]
    [InlineData(0, 1, false, 0)]
    public void Tab_GoesThroughAllThePages(int page, int count, bool back, int expected)
    {
        Assert.Equal(expected, TabLayout.Next(page, count, back));
    }

    [Fact]
    public void TheCornerOfATab_IsItsLeftSideCornerAndTop_WithoutTheCornerAtItsRightEnd()
    {
        var names = new[] { "One", "Three" };
        var (tops, labels, starts) = TabLayout.Tabs(names, '.');
        var (index, length) = TabLayout.Corner(names, starts, 0);
        Assert.Equal(".-----", tops.Substring(index, length));
        Assert.Equal('|', labels[index]);
        Assert.Equal('.', tops[index + length]); // The corner at the right end
        (index, length) = TabLayout.Corner(names, starts, 1);
        Assert.Equal(".-------", tops.Substring(index, length));
        Assert.Equal("| Three ", labels.Substring(index, length));
    }

    [Theory]
    [InlineData('l', ConsoleKey.L, false, 1)]
    [InlineData('h', ConsoleKey.H, false, -1)]
    [InlineData('\0', ConsoleKey.RightArrow, false, 1)]
    [InlineData('\0', ConsoleKey.LeftArrow, false, -1)]
    [InlineData('L', ConsoleKey.L, false, 1)]
    [InlineData('j', ConsoleKey.J, false, 0)]
    // A page that uses h and l itself: only H and L
    [InlineData('l', ConsoleKey.L, true, 0)]
    [InlineData('\0', ConsoleKey.LeftArrow, true, 0)]
    [InlineData('L', ConsoleKey.L, true, 1)]
    [InlineData('H', ConsoleKey.H, true, -1)]
    public void TheSubTabs_AreChangedWithHAndL(char character, ConsoleKey key, bool sideKeysTaken, int step)
    {
        var shift = char.IsUpper(character);
        Assert.Equal(step, TabLayout.SubTabStep(new ConsoleKeyInfo(character, key, shift, false, false), sideKeysTaken));
        // Not with Ctrl
        Assert.Equal(0, TabLayout.SubTabStep(new ConsoleKeyInfo(character, key, shift, false, true), sideKeysTaken));
    }
}
