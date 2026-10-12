namespace Marbles.Tests;

/// <summary>
/// ":dup": a copy of the selection right after it (of the line, or the lines of a range, without a selection)
/// </summary>
public class DuplicateTests
{
    private static EditorEngine Run(string text, string keys)
    {
        var editor = new EditorEngine(text, 40, 10, false);
        editor.Type(keys);
        Assert.Equal(EditorMode.Normal, editor.Mode);
        return editor;
    }

    [Theory]
    // Text selected in Visual mode: the copy follows the last selected char
    [InlineData("one two three", "wviw:dup<CR>", "one twotwo three")]
    [InlineData("ab\ncd\nef", "lvj:dup<CR>", "ab\ncdb\ncd\nef")]
    // "$" selects up to the last char. On an empty line the line break is what is selected
    [InlineData("ab\ncd", "v$:dup<CR>", "abab\ncd")]
    [InlineData("ab\n\ncd", "jv:dup<CR>", "ab\n\n\ncd")]
    // Visual Line mode: the lines below the last of them
    [InlineData("a\nb\nc", "Vj:dup<CR>", "a\nb\na\nb\nc")]
    [InlineData("a\nb\nc", "GV:dup<CR>", "a\nb\nc\nc")]
    // Visual Block mode: the block to the right of it, and the text to the right stays in line
    [InlineData("abcd\nefgh", "lQjl:dup<CR>", "abcbcd\nefgfgh")]
    [InlineData("ab\nefgh", "lQjl:dup<CR>", "abb\nefgfgh")]
    // Without a selection: the line of the cursor. With line numbers: those lines
    [InlineData("a\nb", ":dup<CR>", "a\na\nb")]
    [InlineData("a\nb\nc\nd", ":2,3duplicate<CR>", "a\nb\nc\nb\nc\nd")]
    // A number of copies
    [InlineData("<li></li>\n</ul>", ":dup 3<CR>", "<li></li>\n<li></li>\n<li></li>\n<li></li>\n</ul>")]
    [InlineData("a\nb\nc", "Vj:dup 2<CR>", "a\nb\na\nb\na\nb\nc")]
    [InlineData("a\nb\nc", ":2,3duplicate 2<CR>", "a\nb\nc\nb\nc\nb\nc")]
    [InlineData("one two", "wviw:dup 3<CR>", "one twotwotwotwo")]
    [InlineData("abcd\nefgh", "lQjl:dup 2<CR>", "abcbcbcd\nefgfgfgh")]
    [InlineData("a", ":dup 0<CR>", "a\na")]
    public void Duplicate(string text, string keys, string expected)
    {
        Assert.Equal(expected, Run(text, keys).GetText());
    }

    [Fact]
    public void TheCursorGoesToTheCopy_AndUndoRemovesIt()
    {
        var editor = Run("one two three", "wviw:dup<CR>");
        Assert.Equal(new TextPos(0, 7), editor.Cursor);
        editor.Type("u");
        Assert.Equal("one two three", editor.GetText());
        editor = Run("a\nb\nc", "Vj:dup<CR>");
        Assert.Equal(new TextPos(2, 0), editor.Cursor);
        // A command without the selection range works on the line of the cursor
        editor = Run("x\ny", "vj:<BS><BS><BS><BS><BS>dup<CR>");
        Assert.Equal("x\ny\ny", editor.GetText());
    }
}
