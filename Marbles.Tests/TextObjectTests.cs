namespace Marbles.Tests;

/// <summary>
/// Text objects (iw, i", i', ib, iB, ip ...), :sort and gm/gM
/// </summary>
[Collection("Static state")]
public class TextObjectTests
{
    public TextObjectTests()
    {
        Tools.ClearLineMemory();
    }

    private static EditorEngine Run(string text, string keys, int width = 40)
    {
        var editor = new EditorEngine(text, width, 10, false);
        editor.Type(keys);
        return editor;
    }

    [Theory]
    // Selected and deleted (the selection is deleted with "d")
    [InlineData("one two three", "wviwd", "one  three")]
    [InlineData("say \"hello there\" now", "fhvi\"d", "say \"\" now")]
    [InlineData("say \"hello there\" now", "fhva\"d", "say  now")]
    [InlineData("say 'quoted text' here", "fqvi'd", "say '' here")]
    [InlineData("f(a, b)", "fbvibd", "f()")]
    [InlineData("x = {a, b}", "fbviBd", "x = {}")]
    [InlineData("x = {a, b}", "fbvaBd", "x = ")]
    // The same with an operator
    [InlineData("one two three", "wdiw", "one  three")]
    [InlineData("say \"hello\" now", "fhci\"bye<Esc>", "say \"bye\" now")]
    // Brackets over several lines: "iB" takes the lines between them
    [InlineData("if {\n  a;\n  b;\n}\nend", "jdiB", "if {\n}\nend")]
    [InlineData("if {\n  a;\n  b;\n}\nend", "jdaB", "if \nend")]
    [InlineData("f(a,\n  b)", "jdib", "f()")]
    // In Visual mode the inside of such a block is selected as whole lines
    [InlineData("if {\n  a;\n  b;\n}\nend", "jviBd", "if {\n}\nend")]
    // Paragraphs: the lines that are not blank around the cursor (ap: and the blank lines after them)
    [InlineData("a\nb\n\nc\nd", "dip", "\nc\nd")]
    [InlineData("a\nb\n\nc\nd", "dap", "c\nd")]
    [InlineData("a\n\nc\nd", "Gdip", "a\n")]
    [InlineData("a\nb\n\nc", "vipd", "\nc")]
    public void TextObjects(string text, string keys, string expected)
    {
        Assert.Equal(expected, Run(text, keys).GetText());
    }

    [Fact]
    public void YankingATextObject_KeepsItInTheStringMemory()
    {
        Run("one two three", "wyiw");
        Assert.Equal("two", Tools.GetCurrentLineFromLineMemory());
        Run("a\nb\n\nc", "yip");
        Assert.Equal("a\nb\n", Tools.GetCurrentLineFromLineMemory());
    }

    [Fact]
    public void Vip_SelectsWholeLines()
    {
        var editor = Run("a\nb\n\nc", "jvip");
        Assert.Equal(EditorMode.VisualLine, editor.Mode);
    }

    [Theory]
    [InlineData("b\nC\na", ":sort<CR>", "C\na\nb")]
    [InlineData("b\nC\na", ":sort!<CR>", "b\na\nC")]
    [InlineData("b\nC\na", ":sort i<CR>", "a\nb\nC")]
    // The selected lines only
    [InlineData("z\nc\nb\na", "jVj:sort<CR>", "z\nb\nc\na")]
    [InlineData("z\nc\nb\na", ":2,4sort<CR>", "z\na\nb\nc")]
    public void Sort(string text, string keys, string expected)
    {
        var editor = Run(text, keys);
        Assert.Equal(expected, editor.GetText());
        editor.Type("u");
        Assert.Equal(text, editor.GetText());
    }

    [Fact]
    public void Gm_AndGM_MoveHalfTheWidthOfTheText()
    {
        // The text is 40 cells wide: half is 20
        var editor = Run(new string('x', 60), "gm", 40);
        Assert.Equal(20, editor.Cursor.Col);
        editor.Type("gm");
        Assert.Equal(40, editor.Cursor.Col);
        editor.Type("gM");
        Assert.Equal(20, editor.Cursor.Col);
        // Not past the end of the line, nor before its start
        editor.Type("$gm");
        Assert.Equal(59, editor.Cursor.Col);
        editor.Type("0gM");
        Assert.Equal(0, editor.Cursor.Col);
    }
}
