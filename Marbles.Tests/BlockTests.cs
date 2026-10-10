namespace Marbles.Tests;

/// <summary>
/// Visual Block mode: "Q" or ":block" selects a rectangle of text
/// </summary>
[Collection("Static state")]
public class BlockTests
{
    private const string Text = "abcdef\ngh\nijklmn";

    public BlockTests()
    {
        Tools.ClearLineMemory(); // The registers are shared by all editors
    }

    private static EditorEngine Editor(string text)
    {
        return new EditorEngine(text, 40, 10, false);
    }

    [Fact]
    public void Q_SelectsTheSameColumnsOnEachLine()
    {
        var editor = Editor(Text);
        editor.Type("lQjjl");
        Assert.Equal(EditorMode.VisualBlock, editor.Mode);
        Assert.Equal((1, 3), editor.Selection(0));
        // A shorter line has what reaches into the block
        Assert.Equal((1, 2), editor.Selection(1));
        Assert.Equal((1, 3), editor.Selection(2));
        // "$": every line to its end
        editor.Type("$");
        Assert.Equal((1, 6), editor.Selection(0));
        Assert.Equal((1, 2), editor.Selection(1));
        Assert.Equal((1, 6), editor.Selection(2));
        // Moving sideways ends it
        editor.Type("h");
        Assert.Equal((1, 5), editor.Selection(2));
        Assert.Equal((1, 5), editor.Selection(0));
        editor.Type("<Esc>");
        Assert.Equal(EditorMode.Normal, editor.Mode);
        Assert.Null(editor.Selection(0));
    }

    [Fact]
    public void Q_ASelectionOnALineThatEndsBeforeTheBlock_IsEmpty()
    {
        var editor = Editor("abcdef\ng\nijklmn");
        editor.Type("llQjj");
        Assert.Equal((2, 3), editor.Selection(0));
        Assert.Null(editor.Selection(1));
        Assert.Equal((2, 3), editor.Selection(2));
    }

    [Fact]
    public void Switching_BetweenTheVisualModes()
    {
        var editor = Editor(Text);
        editor.Type("vQ");
        Assert.Equal(EditorMode.VisualBlock, editor.Mode);
        editor.Type("V");
        Assert.Equal(EditorMode.VisualLine, editor.Mode);
        editor.Type("Q");
        Assert.Equal(EditorMode.VisualBlock, editor.Mode);
        editor.Type("v");
        Assert.Equal(EditorMode.Visual, editor.Mode);
        editor.Type("QQ");
        Assert.Equal(EditorMode.Normal, editor.Mode);
    }

    [Theory]
    [InlineData(":block<CR>", "VisualBlock")]
    [InlineData(":vline<CR>", "VisualLine")]
    [InlineData(":vsel<CR>", "Visual")]
    public void Commands_StartTheSelection(string keys, string mode)
    {
        var editor = Editor(Text);
        editor.Type("l" + keys);
        Assert.Equal(mode, editor.Mode.ToString());
        Assert.Equal(new TextPos(0, 1), editor.Cursor);
        // Commands are completed with Tab
        editor.Type("<Esc>:blo<Tab>");
        Assert.Equal("block", editor.CommandText);
    }

    [Theory]
    [InlineData("lQjjld", "adef\ng\nilmn", 0, 1)]
    [InlineData("lQjjlx", "adef\ng\nilmn", 0, 1)]
    [InlineData("lQjjD", "a\ng\ni", 0, 0)]
    [InlineData("lQjjlrx", "axxdef\ngx\nixxlmn", 0, 1)]
    [InlineData("lQjjl~", "aBCdef\ngH\niJKlmn", 0, 1)]
    [InlineData("lQjjlU", "aBCdef\ngH\niJKlmn", 0, 1)]
    [InlineData("lQjjlI--<Esc>", "a--bcdef\ng--h\ni--jklmn", 0, 2)]
    [InlineData("lQjjlA|<Esc>", "abc|def\ngh |\nijk|lmn", 0, 3)]
    [InlineData("Qjj$A;<Esc>", "abcdef;\ngh;\nijklmn;", 0, 6)]
    [InlineData("lQjjlcX<Esc>", "aXdef\ngX\niXlmn", 0, 1)]
    [InlineData("lQjjCX<Esc>", "aX\ngX\niX", 0, 1)]
    [InlineData("lQjj>", "    abcdef\n    gh\n    ijklmn", 0, 4)]
    public void Changes(string keys, string expected, int line, int col)
    {
        var editor = Editor(Text);
        editor.Type(keys);
        Assert.Equal(EditorMode.Normal, editor.Mode);
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(new TextPos(line, col), editor.Cursor);
        // A change is one step to undo
        editor.Type("u");
        Assert.Equal(Text, editor.GetText());
    }

    [Fact]
    public void Insert_LeavesLinesThatEndBeforeTheBlock_AndAppendFillsThemUp()
    {
        var editor = Editor("abcdef\ng\nijklmn");
        editor.Type("llQjjIX<Esc>");
        Assert.Equal("abXcdef\ng\nijXklmn", editor.GetText());
        editor.Type("u0llQjjAY<Esc>");
        Assert.Equal("abcYdef\ng  Y\nijkYlmn", editor.GetText());
    }

    [Fact]
    public void Insert_OnlyTextTypedOnTheFirstLine_IsRepeated()
    {
        var editor = Editor(Text);
        // A line break: the text stays where it was typed
        editor.Type("QjjIx<CR>y<Esc>");
        Assert.Equal("x\nyabcdef\ngh\nijklmn", editor.GetText());
        // Nothing typed
        editor.Type("uQjjI<Esc>");
        Assert.Equal(Text, editor.GetText());
    }

    [Fact]
    public void Yank_AndPut_AsABlock()
    {
        var editor = Editor(Text);
        editor.Type("lQjjly");
        Assert.Equal(EditorMode.Normal, editor.Mode);
        Assert.Equal(new TextPos(0, 1), editor.Cursor);
        Assert.Equal("bc\nh\njk", Tools.GetCurrentLineFromLineMemory());
        Assert.Equal(Text, editor.GetText());
        // Put after the cursor: the text to the right stays in line (no blank spaces at the end of a line)
        editor.Type("$p");
        Assert.Equal("abcdefbc\ngh    h\nijklmnjk", editor.GetText());
        editor.Type("u");
        // Put before the cursor, in the middle of the lines
        editor.Type("0P");
        Assert.Equal("bcabcdef\nh gh\njkijklmn", editor.GetText());
        // A count puts it several times
        editor.Type("u02P");
        Assert.Equal("bcbcabcdef\nh h gh\njkjkijklmn", editor.GetText());
    }

    [Fact]
    public void DeleteAndPut_GivesTheTextBack()
    {
        var editor = Editor(Text);
        editor.Type("lQjjld");
        Assert.Equal("adef\ng\nilmn", editor.GetText());
        editor.Type("P");
        Assert.Equal(Text, editor.GetText());
    }

    [Fact]
    public void Put_LeavesAShortLineAlone_WhenItsPartOfTheBlockIsEmpty()
    {
        var editor = Editor("abcd\nx\nefgh");
        // The block of the third column has nothing on the second line
        editor.Type("llQjjy$p");
        Assert.Equal("abcdc\nx\nefghg", editor.GetText());
    }

    [Fact]
    public void Put_AddsLinesAtTheEnd()
    {
        var editor = Editor(Text);
        editor.Type("Qjy");
        editor.Type("GP");
        Assert.Equal("abcdef\ngh\naijklmn\ng", editor.GetText());
    }

    [Fact]
    public void Put_OverABlock_ReplacesIt()
    {
        var editor = Editor(Text);
        editor.Type("Qjjy");
        Assert.Equal("a\ng\ni", Tools.GetCurrentLineFromLineMemory());
        editor.Type("llQjjp");
        Assert.Equal("abadef\nghg\nijilmn", editor.GetText());
    }

    [Fact]
    public void OtherCorner()
    {
        var editor = Editor(Text);
        editor.Type("lQjjl");
        Assert.Equal(new TextPos(2, 2), editor.Cursor);
        editor.Type("O");
        Assert.Equal(new TextPos(2, 1), editor.Cursor);
        Assert.Equal((1, 3), editor.Selection(0));
        editor.Type("o");
        Assert.Equal(new TextPos(0, 2), editor.Cursor);
        Assert.Equal((1, 3), editor.Selection(2));
    }

    [Fact]
    public void WideCharacters_AreTakenWhole()
    {
        // "中" takes two cells: the block of the first column on the second line covers it
        var editor = Editor("中文ab\nabcd");
        editor.Type("Qjl");
        Assert.Equal((0, 1), editor.Selection(0));
        Assert.Equal((0, 2), editor.Selection(1));
        editor.Type("d");
        Assert.Equal("文ab\ncd", editor.GetText());
    }

    [Fact]
    public void Repeat_DoesTheChangeAgainAtTheCursor()
    {
        var editor = Editor("a\nb\nc\nd");
        editor.Type("QjI#<Esc>");
        Assert.Equal("#a\n#b\nc\nd", editor.GetText());
        editor.Type("jj.");
        Assert.Equal("#a\n#b\n#c\n#d", editor.GetText());
    }
}
