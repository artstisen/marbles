namespace Marbles.Tests;

/// <summary>
/// The list of the string memory (":reglist"): the rows, the filter, and inserting the string picked
/// </summary>
[Collection("Static state")]
public class StringMemoryListTests
{
    public StringMemoryListTests()
    {
        Tools.ClearLineMemory();
    }

    [Fact]
    public void Reglist_AsksTheHost_WhenThereAreStrings()
    {
        var editor = new EditorEngine("one two", 40, 10, false);
        Assert.Equal(EditorRequest.None, editor.Type(":reglist<CR>"));
        Assert.Equal(Lang.T("String memory is empty"), editor.Message);
        editor.Type("yiw");
        Assert.Equal(EditorRequest.StringMemory, editor.Type(":reglist<CR>"));
    }

    [Fact]
    public void TheRows_ShowLineBreaks_AndWholeLines()
    {
        Assert.Equal("word", Tools.LineMemoryRow("word"));
        Assert.Equal("a | b", Tools.LineMemoryRow("a\nb"));
        Assert.Equal("<p> | x", Tools.LineMemoryRow("  <p>\n\t  x"));
        Assert.Equal(Lang.F("(lines) {0}", "a | b"), Tools.LineMemoryRow("a\nb\n"));
    }

    [Fact]
    public void TheFilter_LooksInTheRows_UpperAndLowerCaseAlike()
    {
        var rows = new[] { "Hello", "world", "help me" };
        Assert.Equal(new[] { 0, 1, 2 }, PickListBox.Filter(rows, ""));
        Assert.Equal(new[] { 0, 2 }, PickListBox.Filter(rows, "HEL"));
        Assert.Empty(PickListBox.Filter(rows, "xyz"));
    }

    [Theory]
    // Tab: the next match, going round from the last to the first; Shift+Tab: the one before
    [InlineData(0, 3, false, 1)]
    [InlineData(2, 3, false, 0)]
    [InlineData(0, 3, true, 2)]
    [InlineData(1, 3, true, 0)]
    [InlineData(0, 1, false, 0)]
    [InlineData(0, 0, false, 0)]
    public void Tab_SelectsAmongTheMatches(int selected, int count, bool back, int expected)
    {
        Assert.Equal(expected, PickListBox.NextMatch(selected, count, back));
    }

    [Fact]
    public void PutFromMemory_InsertsAsP_AndMakesItTheCurrentString()
    {
        var editor = new EditorEngine("one two\nlast", 40, 10, false);
        editor.Type("yiw");
        editor.Type("jyy");
        // "one" is the first string (0), "last\n" the second (1, current)
        editor.Type("gg");
        editor.PutFromMemory(0);
        Assert.Equal("oonene two\nlast", editor.GetText());
        Assert.Equal("one", Tools.GetCurrentLineFromLineMemory());
        // Whole lines go below the line of the cursor
        editor.PutFromMemory(1);
        Assert.Equal("oonene two\nlast\nlast", editor.GetText());
        Assert.Equal(1, editor.Cursor.Line);
        // One change each
        editor.Type("u");
        Assert.Equal("oonene two\nlast", editor.GetText());
        editor.Type("u");
        Assert.Equal("one two\nlast", editor.GetText());
    }
}
