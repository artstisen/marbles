namespace Marbles.Tests;

[Collection("Static state")]
public class MarkdownTests
{
    // The styles of a line as text, one character for each character of the line:
    // . none, 1-6 headings, B bold, I italic, C code, K block of code, Q quote, L list marker, A link,
    // U address of a link, R rule
    private static string Styles(string line, bool inCodeBlock = false)
    {
        var styles = MarkdownHighlighter.Highlight(line, inCodeBlock);
        if (styles == null) return new string('.', line.Length);
        return string.Concat(styles.Select(style => ".123456BICKQLAUR"[(int)style]));
    }

    [Theory]
    [InlineData("# Title", "1111111")]
    [InlineData("## Two", "222222")]
    [InlineData("###### Six", "6666666666")]
    [InlineData("  ### In", "33333333")]
    [InlineData("#", "1")]
    [InlineData("#NoSpace", "........")]
    [InlineData("####### Seven", ".............")]
    [InlineData("text # not", "..........")]
    public void Headings(string line, string expected)
    {
        Assert.Equal(expected, Styles(line));
    }

    [Theory]
    [InlineData("**bold**", "BBBBBBBB")]
    [InlineData("a **bold** b", "..BBBBBBBB..")]
    [InlineData("*it*", "IIII")]
    [InlineData("a *it* b", "..IIII..")]
    [InlineData("__bold__ _it_", "BBBBBBBB.IIII")]
    [InlineData("***both***", "BBBBBBBBBB")]
    [InlineData("a * b * c", ".........")]
    [InlineData("2 * 3 = 6", ".........")]
    [InlineData("**open only", "...........")]
    [InlineData("my_file_name", "............")]
    [InlineData("\\*not\\*", ".......")]
    [InlineData("*a* and **b**", "III.....BBBBB")]
    [InlineData("*a **b** c*", "IIIIIIIIIII")] // Emphasis inside emphasis is shown as the outer one
    public void Emphasis(string line, string expected)
    {
        Assert.Equal(expected, Styles(line));
    }

    [Theory]
    [InlineData("`code`", "CCCCCC")]
    [InlineData("a `c` b", "..CCC..")]
    [InlineData("``a ` b``", "CCCCCCCCC")]
    [InlineData("`*not bold*`", "CCCCCCCCCCCC")]
    [InlineData("`open", ".....")]
    public void Code(string line, string expected)
    {
        Assert.Equal(expected, Styles(line));
    }

    [Theory]
    [InlineData("[text](http://x)", "AAAAAAUUUUUUUUUU")]
    [InlineData("![img](a.png)", "AAAAAAUUUUUUU")]
    [InlineData("see [a][1].", "....AAAUUU.")]
    [InlineData("<https://x.dk>", "UUUUUUUUUUUUUU")]
    [InlineData("go https://x.dk now", "...UUUUUUUUUUUU....")]
    [InlineData("a < b > c", ".........")]
    public void Links(string line, string expected)
    {
        Assert.Equal(expected, Styles(line));
    }

    [Theory]
    [InlineData("- item", "L.....")]
    [InlineData("* item", "L.....")]
    [InlineData("  + item", "..L.....")]
    [InlineData("1. one", "LL....")]
    [InlineData("12) x", "LLL..")]
    [InlineData("- **b**", "L.BBBBB")]
    [InlineData("- [x] done", "L.LLL.....")]
    [InlineData("-no", "...")]
    [InlineData("1.5 kg", "......")]
    public void Lists(string line, string expected)
    {
        Assert.Equal(expected, Styles(line));
    }

    [Theory]
    [InlineData("> quote", "QQQQQQQ")]
    [InlineData("---", "RRR")]
    [InlineData("* * *", "RRRRR")]
    [InlineData("___", "RRR")]
    [InlineData("--", "..")]
    [InlineData("", "")]
    [InlineData("   ", "...")]
    [InlineData("plain text", "..........")]
    public void QuotesRulesAndPlainText(string line, string expected)
    {
        Assert.Equal(expected, Styles(line));
    }

    [Fact]
    public void BlocksOfCode()
    {
        Assert.True(MarkdownHighlighter.IsFence("```"));
        Assert.True(MarkdownHighlighter.IsFence("```csharp"));
        Assert.True(MarkdownHighlighter.IsFence("  ~~~"));
        Assert.False(MarkdownHighlighter.IsFence("``"));
        Assert.False(MarkdownHighlighter.IsFence("     ```"));
        Assert.False(MarkdownHighlighter.IsFence("text ```"));
        Assert.Equal("KKK", Styles("```"));
        // Nothing is read as Markdown in a block of code
        Assert.Equal("KKKKKKK", Styles("# **x**", true));
    }

    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("README.MD", true)]
    [InlineData("a.markdown", true)]
    [InlineData("a.txt", false)]
    [InlineData("md", false)]
    public void MarkdownFiles(string name, bool expected)
    {
        Assert.Equal(expected, MarkdownHighlighter.IsMarkdownFile(name));
        Assert.Equal(expected, SyntaxHighlighter.TypeOfFile(name) == SyntaxType.Markdown);
    }

    #region In the editor

    private static EditorEngine Editor(string text)
    {
        return new EditorEngine(text, 40, 10, false);
    }

    private static string Styles(EditorEngine editor, int line)
    {
        var styles = editor.SyntaxStyles(line);
        return styles == null ? "" : string.Concat(styles.Select(style => ".123456BICKQLAUR"[(int)style]));
    }

    [Fact]
    public void Editor_SyntaxCommand_TurnsTheColorsOnAndOff()
    {
        var editor = Editor("# Title");
        Assert.False(editor.Markdown);
        Assert.Null(editor.SyntaxStyles(0));
        Assert.Equal(EditorRequest.SyntaxChanged, editor.Type(":syntax<CR>"));
        Assert.True(editor.Markdown);
        Assert.Equal(DialogText.SyntaxOn + "Markdown", editor.Message);
        Assert.Equal("1111111", Styles(editor, 0));
        editor.Type(":syntax<CR>");
        Assert.False(editor.Markdown);
        editor.Type(":syntax on<CR>");
        Assert.True(editor.Markdown);
        editor.Type(":syntax off<CR>");
        Assert.False(editor.Markdown);
        editor.Type(":syntax maybe<CR>");
        Assert.True(editor.MessageIsError);
    }

    [Fact]
    public void Editor_BlocksOfCode_FollowTheText()
    {
        var editor = Editor("# a\n```\n# b\n```\n# c");
        editor.Markdown = true;
        Assert.Equal("111", Styles(editor, 0));
        Assert.Equal("KKK", Styles(editor, 1));
        Assert.Equal("KKK", Styles(editor, 2));
        Assert.Equal("KKK", Styles(editor, 3));
        Assert.Equal("111", Styles(editor, 4));
        // The first line of the block is removed: what was the end of the block now starts one
        editor.Type("jdd");
        Assert.Equal("# a\n# b\n```\n# c", editor.GetText());
        Assert.Equal("111", Styles(editor, 1));
        Assert.Equal("KKK", Styles(editor, 2));
        Assert.Equal("KKK", Styles(editor, 3));
        editor.Type("u");
        Assert.Equal("KKK", Styles(editor, 2));
        Assert.Equal("111", Styles(editor, 4));
    }

    #endregion
}
