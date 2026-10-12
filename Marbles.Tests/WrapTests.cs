namespace Marbles.Tests;

/// <summary>
/// Long lines not wrapped: the view scrolls sideways to follow the cursor (LeftCell), and zh zl zH zL zs ze
/// </summary>
[Collection("Static state")]
public class WrapTests
{
    // 20 cells wide, a line of 60 characters (0123456789 six times)
    private static EditorEngine Editor(bool wrap = false)
    {
        var editor = new EditorEngine(string.Concat(Enumerable.Repeat("0123456789", 6)) + "\nshort", 20, 5, false)
        {
            Wrap = wrap
        };
        return editor;
    }

    [Fact]
    public void WithoutWrapping_EachLineIsOneRow()
    {
        Assert.Equal(2, Editor().Layout.TotalRows());
        Assert.Equal(4, Editor(true).Layout.TotalRows());
    }

    [Fact]
    public void TheView_FollowsTheCursor_WithAMarginOfOneCell()
    {
        var editor = Editor();
        Assert.Equal(0, editor.LeftCell);
        editor.Type("18l");
        Assert.Equal(0, editor.LeftCell);
        // The cursor may not go into the last cell, where the mark of hidden text is
        editor.Type("l");
        Assert.Equal(1, editor.LeftCell);
        editor.Type("$");
        Assert.Equal(59 - 18, editor.LeftCell);
        editor.Type("0");
        Assert.Equal(0, editor.LeftCell);
        // On a short line the view goes back to the start
        editor.Type("$j");
        Assert.Equal(0, editor.LeftCell);
    }

    [Fact]
    public void Z_ScrollsSideways_AndKeepsTheCursorInView()
    {
        var editor = Editor();
        editor.Type("10l");
        editor.Type("zl");
        Assert.Equal(1, editor.LeftCell);
        Assert.Equal(10, editor.Cursor.Col);
        editor.Type("5zh");
        Assert.Equal(0, editor.LeftCell);
        // The cursor at the left side, and at the right side
        editor.Type("zs");
        Assert.Equal(9, editor.LeftCell);
        editor.Type("ze");
        Assert.Equal(0, editor.LeftCell);
        // Half the width: the cursor is moved into the view
        editor.Type("0zL");
        Assert.Equal(10, editor.LeftCell);
        Assert.Equal(11, editor.Cursor.Col);
        editor.Type("zH");
        Assert.Equal(0, editor.LeftCell);
    }

    [Fact]
    public void WithWrapping_TheZKeysDoNothing()
    {
        var editor = Editor(true);
        editor.Type("10lzlzs");
        Assert.Equal(0, editor.LeftCell);
    }

    [Theory]
    [InlineData(":wrap<CR>", true)]
    [InlineData(":set wrap<CR>", true)]
    [InlineData(":set nowrap<CR>", false)]
    [InlineData(":nowrap<CR>", false)]
    public void TheCommands_TurnWrappingOnAndOff(string keys, bool expected)
    {
        var editor = Editor();
        Assert.Equal(EditorRequest.WrapChanged, editor.Type(keys));
        Assert.Equal(expected, editor.Wrap);
    }
}
