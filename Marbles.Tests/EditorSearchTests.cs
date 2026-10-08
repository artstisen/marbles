namespace Marbles.Tests;

[Collection("Static state")]
public class EditorSearchTests
{
    public EditorSearchTests()
    {
        Tools.ClearLineMemory();
    }

    private static EditorEngine Editor(string text)
    {
        return new EditorEngine(text, 40, 10, false);
    }

    [Theory]
    [InlineData(@"/\d+<CR>", 0, 5)] // Regular expression
    [InlineData(@"/\d+<CR>n", 1, 0)]
    [InlineData(@"/\d+<CR>nn", 1, 8)]
    [InlineData(@"/\d+<CR>nnn", 0, 5)] // Wraps around
    [InlineData(@"/\d+<CR>N", 1, 8)]
    [InlineData(@"/item|total<CR>", 1, 3)] // The cursor is on the first "item"
    [InlineData("/^\\d<CR>", 1, 0)]
    [InlineData("/s$<CR>", 0, 12)]
    [InlineData("/ITEM<CR>", 0, 0)] // Uppercase in the pattern makes the search match case: not found
    [InlineData("/Total<CR>", 2, 0)]
    [InlineData("/total<CR>", 2, 0)] // All lowercase ignores case
    [InlineData("/(<CR>", 2, 6)] // Not a valid expression: searched for as text
    [InlineData("/a.c<CR>", 2, 7)]
    [InlineData(@"/a\.c<CR>", 2, 11)]
    [InlineData(@"G$?\d+<CR>", 1, 8)]
    [InlineData(@"?\d+<CR>", 1, 8)] // Backwards from the top wraps to the last match
    public void Search_UsesRegularExpressions(string keys, int line, int col)
    {
        //                   0123456789012    0123456789012    0123456789012345
        var editor = Editor("item 12 boxes\n34 item 5\nTotal (abc a.c)");
        editor.Type(keys);
        Assert.Equal(new TextPos(line, col), editor.Cursor);
    }

    [Fact]
    public void Search_HighlightsWholeMatches()
    {
        var editor = Editor("ab12 cd345");
        editor.Type(@"/\d+<CR>");
        Assert.Equal(new[] { (2, 4), (7, 10) }, editor.SearchMatches(0));
    }

    [Fact]
    public void Star_SearchesForWholeWords()
    {
        var editor = Editor("cat concat cat");
        editor.Type("*");
        Assert.Equal(new TextPos(0, 11), editor.Cursor);
    }

    [Fact]
    public void Search_WorksAsAMotionForDeleting()
    {
        var editor = Editor("one two three");
        editor.Type("/three<CR>0dn");
        Assert.Equal("three", editor.GetText());
    }

    [Theory]
    [InlineData(":s/cat/dog/<CR>", "dog cat Cat\ncat\nbird cat")]
    [InlineData(":s/cat/dog<CR>", "dog cat Cat\ncat\nbird cat")]
    [InlineData(":s/cat/dog/g<CR>", "dog dog dog\ncat\nbird cat")]
    [InlineData(":s/cat/dog/gI<CR>", "dog dog Cat\ncat\nbird cat")]
    [InlineData(":s/Cat/dog/g<CR>", "cat cat dog\ncat\nbird cat")]
    [InlineData(":s/Cat/dog/gi<CR>", "dog dog dog\ncat\nbird cat")]
    [InlineData(":%s/cat/dog/g<CR>", "dog dog dog\ndog\nbird dog")]
    [InlineData(":2,3s/cat/dog/<CR>", "cat cat Cat\ndog\nbird dog")]
    [InlineData(":2,$s/cat/dog/<CR>", "cat cat Cat\ndog\nbird dog")]
    [InlineData("j:.,$s/cat/dog/<CR>", "cat cat Cat\ndog\nbird dog")]
    [InlineData("jVj:s/cat/dog/<CR>", "cat cat Cat\ndog\nbird dog")]
    [InlineData(":%s/cat//g<CR>", "  \n\nbird ")]
    [InlineData(":%s/c(a)t/[&|\\1]/<CR>", "[cat|a] cat Cat\n[cat|a]\nbird [cat|a]")]
    [InlineData(":%s/^/> /<CR>", "> cat cat Cat\n> cat\n> bird cat")]
    [InlineData(":%s/$/;/<CR>", "cat cat Cat;\ncat;\nbird cat;")]
    [InlineData(":%s#cat#a/b#<CR>", "a/b cat Cat\na/b\nbird a/b")]
    [InlineData(":%s/ cat/\\/&$1/<CR>", "cat/ cat$1 Cat\ncat\nbird/ cat$1")]
    [InlineData(":s/ /\\n/g<CR>", "cat\ncat\nCat\ncat\nbird cat")]
    [InlineData("/bird<CR>:%s//fish/<CR>", "cat cat Cat\ncat\nfish cat")]
    public void Substitute(string keys, string expected)
    {
        var editor = Editor("cat cat Cat\ncat\nbird cat");
        editor.Type(keys);
        Assert.Equal(expected, editor.GetText());
        Assert.False(editor.MessageIsError);
        // One command is one undo step
        editor.Type("u");
        Assert.Equal("cat cat Cat\ncat\nbird cat", editor.GetText());
    }

    [Fact]
    public void Substitute_ReportsTheResult()
    {
        var editor = Editor("cat cat\ncat\nbird");
        editor.Type(":%s/cat/dog/g<CR>");
        Assert.Equal("3 substitutions on 2 lines", editor.Message);
        Assert.Equal(1, editor.Cursor.Line);
        editor.Type(":%s/horse/dog/<CR>");
        Assert.True(editor.MessageIsError);
        editor.Type(":s/dog/cat/x<CR>");
        Assert.True(editor.MessageIsError);
        Assert.Equal("dog dog\ndog\nbird", editor.GetText());
    }

    [Fact]
    public void Substitute_CanBeRepeated()
    {
        var editor = Editor("a a\na a");
        editor.Type(":s/a/b/<CR>j.");
        Assert.Equal("b a\nb a", editor.GetText());
    }

    [Theory]
    [InlineData(":2<CR>", "one\ntwo\nthree\nfour", 1)]
    [InlineData(":$<CR>", "one\ntwo\nthree\nfour", 3)]
    [InlineData(":2,3d<CR>", "one\nfour", 1)]
    [InlineData(":%d<CR>", "", 0)]
    [InlineData(":3,$d<CR>", "one\ntwo", 1)]
    [InlineData(":2,3y<CR>Gp", "one\ntwo\nthree\nfour\ntwo\nthree", 4)]
    [InlineData("jVj:d<CR>", "one\nfour", 1)]
    public void Commands_WithLineRanges(string keys, string expected, int line)
    {
        var editor = Editor("one\ntwo\nthree\nfour");
        editor.Type(keys);
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(line, editor.Cursor.Line);
    }

    [Fact]
    public void Command_StartedFromASelectionIsPrefilled()
    {
        var editor = Editor("one\ntwo");
        editor.Type("V:");
        Assert.Equal("'<,'>", editor.CommandText);
        Assert.Equal(EditorMode.Command, editor.Mode);
    }

    #region The find-and-replace box (:replace)

    [Fact]
    public void ReplaceCommand_AsksForTheBox()
    {
        var editor = Editor("one\ntwo\nthree");
        Assert.Equal(EditorRequest.ReplaceDialog, editor.Type(":replace<CR>"));
        Assert.Null(editor.ReplaceLines);
        // With lines selected
        Assert.Equal(EditorRequest.ReplaceDialog, editor.Type("jVj:replace<CR>"));
        Assert.Equal((1, 2), editor.ReplaceLines);
    }

    [Fact]
    public void Replace_PlainText_IgnoringCase()
    {
        var editor = Editor("a.b A.B axb\na.b");
        editor.Replace("a.b", "$1 & x", false, false, 0, 1);
        // The text is not a regular expression, and the replacement is used as it is
        Assert.Equal("$1 & x $1 & x axb\n$1 & x", editor.GetText());
        Assert.Equal("3 substitutions on 2 lines", editor.Message);
        // One step to undo
        editor.Type("u");
        Assert.Equal("a.b A.B axb\na.b", editor.GetText());
    }

    [Fact]
    public void Replace_MatchCase_OnSomeLines()
    {
        var editor = Editor("cat Cat\ncat Cat\ncat Cat");
        editor.Replace("Cat", "dog", true, false, 1, 1);
        Assert.Equal("cat Cat\ncat dog\ncat Cat", editor.GetText());
    }

    [Fact]
    public void Replace_RegularExpression()
    {
        var editor = Editor("x12 y345");
        editor.Replace(@"([a-z])(\d+)", @"\2\1", false, true, 0, 0);
        Assert.Equal("12x 345y", editor.GetText());
    }

    [Fact]
    public void Replace_NotFound()
    {
        var editor = Editor("text");
        editor.Replace("a(b", "x", false, false, 0, 0);
        Assert.True(editor.MessageIsError);
        Assert.Equal("Pattern not found: a(b", editor.Message);
        Assert.Equal("text", editor.GetText());
    }

    #endregion
}
