namespace Marbles.Tests;

/// <summary>
/// ":ol" and ":ul": the selected lines as a numbered or bulleted list
/// </summary>
public class ListTests
{
    private static string Run(string text, string keys)
    {
        var editor = new EditorEngine(text, 40, 10, false);
        editor.Type(keys);
        Assert.Equal(EditorMode.Normal, editor.Mode);
        return editor.GetText();
    }

    [Theory]
    // The selected lines (the command line starts with "'<,'>" for them). Blank lines are left alone
    [InlineData("one\ntwo\n\nthree", "VG:ol<CR>", "1. one\n2. two\n\n3. three")]
    [InlineData("one\ntwo\n\nthree", "VG:ul<CR>", "- one\n- two\n\n- three")]
    [InlineData("one\ntwo\nthree", "jvj:ul<CR>", "one\n- two\n- three")]
    [InlineData("one\ntwo\nthree", "Qj:ol<CR>", "1. one\n2. two\nthree")]
    // Without a selection: the line of the cursor. With line numbers: those lines
    [InlineData("one\ntwo", ":ul<CR>", "- one\ntwo")]
    [InlineData("a\nb\nc\nd", ":2,3ol<CR>", "a\n1. b\n2. c\nd")]
    // A list of the other kind is changed, and one of the same kind is removed
    [InlineData("- a\n* b\n+ c", "VG:ol<CR>", "1. a\n2. b\n3. c")]
    [InlineData("1. a\n2) b", "VG:ul<CR>", "- a\n- b")]
    [InlineData("1. a\n2. b", "VG:ol<CR>", "a\nb")]
    [InlineData("- a\n- b", "VG:ul<CR>", "a\nb")]
    // Some of the lines are a list already: they are all made one, numbered again
    [InlineData("3. a\nb\n1. c", "VG:ol<CR>", "1. a\n2. b\n3. c")]
    // The indentation is kept, and each level is numbered by itself
    [InlineData("a\n  b\n  c\nd\n  e", "VG:ol<CR>", "1. a\n  1. b\n  2. c\n2. d\n  1. e")]
    [InlineData("  a\n    b", "VG:ul<CR>", "  - a\n    - b")]
    // A task list item keeps its box
    [InlineData("- [ ] a\n- [x] b", "VG:ol<CR>", "1. [ ] a\n2. [x] b")]
    // A dash that is not followed by a blank space is text
    [InlineData("-5 degrees", ":ul<CR>", "- -5 degrees")]
    public void Lists(string text, string keys, string expected)
    {
        Assert.Equal(expected, Run(text, keys));
    }

    [Fact]
    public void ACommandFromASelection_WorksOnTheSelectedLines_AndIsOneStepToUndo()
    {
        var editor = new EditorEngine("one\ntwo\nthree", 40, 10, false);
        editor.Type("jVj:");
        Assert.Equal("'<,'>", editor.CommandText);
        editor.Type("ol<CR>");
        Assert.Equal("one\n1. two\n2. three", editor.GetText());
        Assert.Equal(new TextPos(1, 0), editor.Cursor);
        editor.Type("u");
        Assert.Equal("one\ntwo\nthree", editor.GetText());
        // Tab completes the command after the lines
        editor.Type("Vj:u<Tab>");
        Assert.Equal("'<,'>ul", editor.CommandText);
    }
}
