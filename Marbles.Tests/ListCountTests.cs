namespace Marbles.Tests;

/// <summary>
/// A count in front of a key in the lists of the file browser and the Jump menu (5j, 12k, 5gg, 5G)
/// </summary>
public class ListCountTests
{
    private static ConsoleKeyInfo Key(char c, ConsoleModifiers modifiers = 0) =>
        new(c, ConsoleKey.NoName, (modifiers & ConsoleModifiers.Shift) != 0, (modifiers & ConsoleModifiers.Alt) != 0,
            (modifiers & ConsoleModifiers.Control) != 0);

    [Fact]
    public void Digits_MakeACount_ThatTheNextKeyTakes()
    {
        var count = new ListCount();
        Assert.True(count.Add(Key('1')));
        Assert.True(count.Add(Key('2')));
        Assert.Equal("12", count.Shown);
        // j is not a digit: it takes the count, which is gone after it
        Assert.False(count.Add(Key('j')));
        Assert.Equal(12, count.Take());
        Assert.Null(count.Take());
        Assert.Equal(string.Empty, count.Shown);
    }

    [Fact]
    public void AZero_IsADigit_OnlyAfterAnotherDigit_AndControlKeysAreNotDigits()
    {
        var count = new ListCount();
        Assert.False(count.Add(Key('0')));
        Assert.False(count.Add(Key('5', ConsoleModifiers.Control)));
        Assert.False(count.Add(Key('5', ConsoleModifiers.Alt)));
        Assert.True(count.Add(Key('1')));
        Assert.True(count.Add(Key('0')));
        Assert.Equal(10, count.Take());
        // A count has an end
        for (var i = 0; i < 9; i++) count.Add(Key('9'));
        Assert.Equal(99999, count.Take());
    }

    [Theory]
    [InlineData(null, 8, true, 0)] // gg
    [InlineData(null, 8, false, 7)] // G
    [InlineData(5, 8, true, 4)] // 5gg
    [InlineData(5, 8, false, 4)] // 5G
    [InlineData(20, 8, true, 7)] // past the end: the last row
    [InlineData(3, 0, true, 0)] // an empty list
    public void ACountWithGgOrG_GoesToThatRow(int? count, int rows, bool first, int expected)
    {
        Assert.Equal(expected, ListCount.Row(count, rows, first));
    }

    [Theory]
    [InlineData(true, 9, 2)]
    [InlineData(true, 10, 3)]
    [InlineData(true, 120, 4)]
    [InlineData(true, 0, 0)]
    [InlineData(false, 50, 0)]
    public void TheNumbersOfTheRows_AreAsWideAsTheLargest_AndABlankSpace(bool on, int count, int width)
    {
        Assert.Equal(width, ListNumbers.Width(on, count));
    }
}