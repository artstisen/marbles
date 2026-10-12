namespace Marbles.Tests;

/// <summary>
/// "scrolloff": rows kept above and below the cursor when the view scrolls
/// </summary>
[Collection("Static state")]
public class ScrollOffTests
{
    // 100 lines, a view of 20 rows
    private static EditorEngine Editor(int scrollOff)
    {
        var text = string.Join("\n", Enumerable.Range(1, 100).Select(n => "line " + n));
        return new EditorEngine(text, 40, 20, false) { ScrollOff = scrollOff };
    }

    [Fact]
    public void WithoutIt_TheViewScrollsAtTheEdge()
    {
        var editor = Editor(0);
        editor.Type("19j");
        Assert.Equal(0, editor.TopRow);
        editor.Type("j");
        Assert.Equal(1, editor.TopRow);
    }

    [Fact]
    public void TheViewScrolls_WhenTheCursorComesNearTheEdge()
    {
        var editor = Editor(5);
        // Row 14 is the last one with 5 rows below it in the view
        editor.Type("14j");
        Assert.Equal(0, editor.TopRow);
        editor.Type("j");
        Assert.Equal(1, editor.TopRow);
        // Going up: 5 rows above the cursor
        editor.Type("10k");
        Assert.Equal(0, editor.TopRow);
        // Line 30 (row 29) with 5 rows below it; up one line does not scroll
        editor.Type("30G");
        Assert.Equal(29 - 14, editor.TopRow);
        editor.Type("k");
        Assert.Equal(29 - 14, editor.TopRow);
        editor.Type("15k");
        Assert.Equal(13 - 5, editor.TopRow);
    }

    [Fact]
    public void NotPastTheEndOfTheText()
    {
        var editor = Editor(5);
        editor.Type("G");
        Assert.Equal(100 - 20, editor.TopRow);
        editor.Type("gg");
        Assert.Equal(0, editor.TopRow);
    }

    [Fact]
    public void ALargeNumber_KeepsTheCursorInTheMiddle()
    {
        var editor = Editor(999);
        editor.Type("50G");
        // Line 50 is row 49, with 10 rows above it and 9 below it in the view of 20
        Assert.Equal(49 - 10, editor.TopRow);
        editor.Type("j");
        Assert.Equal(50 - 10, editor.TopRow);
    }

    [Theory]
    [InlineData(":scrolloff<CR>", 12)]
    [InlineData(":scrolloff 8<CR>", 8)]
    [InlineData(":set scrolloff=6<CR>", 6)]
    [InlineData(":set so=999<CR>", 999)]
    [InlineData(":set so=0<CR>", 0)]
    public void TheCommands_SetIt(string keys, int expected)
    {
        var editor = Editor(0);
        Assert.Equal(EditorRequest.ScrollOffChanged, editor.Type(keys));
        Assert.Equal(expected, editor.ScrollOff);
    }

    [Fact]
    public void Scrolloff_TurnsItOnAndOff_WithTheLastNumber()
    {
        var editor = Editor(0);
        editor.Type(":scrolloff 8<CR>");
        editor.Type(":scrolloff<CR>");
        Assert.Equal(0, editor.ScrollOff);
        editor.Type(":scrolloff<CR>");
        Assert.Equal(8, editor.ScrollOff);
        Assert.Equal(8, editor.ScrollOffLines);
    }
}
