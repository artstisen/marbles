namespace Marbles.Tests;

/// <summary>
/// The snippets (":snip"): the file, the titles, the order, and inserting them in the editor
/// </summary>
[Collection("Static state")]
public class SnippetsTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "marbles-snippets-" + Guid.NewGuid() + ".txt");

    private const string Text =
        "How to use them\n" +
        "\n" +
        "=== Card (html)\n" +
        "<div class=\"card\">\n" +
        "    $0\n" +
        "</div>\n" +
        "\n" +
        "\n" +
        "=== Center (css)\n" +
        "display: flex;\n" +
        "\\=== not a title\n" +
        "=== Alert\n" +
        "alert(1);\n";

    public SnippetsTests()
    {
        Snippets.FilePathOverride = _file;
        Snippets.ForgetUsed();
        Snippets.Parse(Text);
    }

    public void Dispose()
    {
        Snippets.FilePathOverride = null;
        Snippets.ForgetUsed();
        if (File.Exists(_file)) File.Delete(_file);
    }

    [Fact]
    public void TheFile_IsReadAsTitlesAndTexts()
    {
        Assert.Equal(new[] { "Card (html)", "Center (css)", "Alert" }, Snippets.Items.Select(s => s.Title));
        // Blank lines at the end are not part of a snippet, and "\===" is a line starting with "==="
        Assert.Equal("<div class=\"card\">\n    $0\n</div>", Snippets.Items[0].Text);
        Assert.Equal("display: flex;\n=== not a title", Snippets.Items[1].Text);
        // The lines of the titles in the file
        Assert.Equal(new[] { 2, 8, 11 }, Snippets.Items.Select(s => s.Line));
    }

    [Fact]
    public void Writing_KeepsTheTextAboveTheTitles_AndTheLinesStartingWithTheMark()
    {
        var written = Snippets.Format();
        Assert.StartsWith("How to use them\n\n=== Card (html)\n", written);
        Assert.Contains("\n\\=== not a title\n", written);
        Snippets.Parse(written);
        Assert.Equal(written, Snippets.Format());
        Assert.Equal("display: flex;\n=== not a title", Snippets.Items[1].Text);
    }

    [Fact]
    public void Create_MakesAFileWithExamples()
    {
        Assert.False(Snippets.Exists);
        Assert.Equal(DialogText.Success, Snippets.Create());
        Assert.True(Snippets.Exists);
        Assert.Equal(DialogText.Success, Snippets.Load());
        Assert.Equal(2, Snippets.Items.Count);
        Assert.Contains("$0", Snippets.Items[0].Text);
    }

    [Fact]
    public void AddRenameAndDelete_AreSaved()
    {
        Assert.True(Snippets.Add("Log", "console.log($0);\n\n"));
        // A title that is taken (upper and lower case alike) or empty
        Assert.False(Snippets.Add("log", "x"));
        Assert.False(Snippets.Add("  ", "x"));
        Assert.False(Snippets.Rename(0, "ALERT"));
        Assert.True(Snippets.Rename(0, "Card with padding (html)"));
        Snippets.Delete(2);
        Assert.Equal(DialogText.Success, Snippets.Save());
        Assert.Equal(DialogText.Success, Snippets.Load());
        Assert.Equal(new[] { "Card with padding (html)", "Center (css)", "Log" }, Snippets.Items.Select(s => s.Title));
        Assert.Equal("console.log($0);", Snippets.Items[2].Text);
    }

    [Theory]
    [InlineData("Card (html)", "Html")]
    [InlineData("Center (CSS) ", "Css")]
    [InlineData("Fetch (js)", "JavaScript")]
    [InlineData("Note (todo)", "None")]
    [InlineData("Alert", "None")]
    public void TheTag_TellsTheKindOfText(string title, string expected)
    {
        Assert.Equal(Enum.Parse<SyntaxType>(expected), Snippets.Tag(title));
    }

    [Fact]
    public void TheView_IsFilteredAndSorted_WithTheSnippetsForTheTextFirst()
    {
        Assert.Equal(new[] { 0, 1, 2 }, Snippets.View("", Snippets.SortOrder.File, SyntaxType.None));
        Assert.Equal(new[] { 2, 0, 1 }, Snippets.View("", Snippets.SortOrder.Title, SyntaxType.None));
        Assert.Equal(new[] { 1, 2, 0 }, Snippets.View("", Snippets.SortOrder.Title, SyntaxType.Css));
        Assert.Equal(new[] { 0, 1 }, Snippets.View("C", Snippets.SortOrder.File, SyntaxType.None));
        Assert.Empty(Snippets.View("nothing", Snippets.SortOrder.File, SyntaxType.None));
        // The last used first
        Snippets.MarkUsed("Center (css)");
        Snippets.MarkUsed("Alert");
        Assert.Equal(new[] { 2, 1, 0 }, Snippets.View("", Snippets.SortOrder.Used, SyntaxType.None));
    }

    [Theory]
    [InlineData("file", "File")]
    [InlineData("title", "Title")]
    [InlineData("used", "Used")]
    [InlineData("", "File")]
    public void TheOrder_IsReadAndWrittenByName(string name, string expected)
    {
        var order = Enum.Parse<Snippets.SortOrder>(expected);
        Assert.Equal(order, Snippets.ParseSort(name));
        if (name.Length > 0) Assert.Equal(name, Snippets.SortName(order));
    }

    [Fact]
    public void IndexOfLine_IsWhereTheLineStarts()
    {
        Assert.Equal(0, Snippets.IndexOfLine("a\nbc\nd", 0));
        Assert.Equal(5, Snippets.IndexOfLine("a\nbc\nd", 2));
        Assert.Equal(5, Snippets.IndexOfLine("a\r\nbc\r\nd", 9));
    }

    [Theory]
    // After the cursor (as "p"). The lines after the first are indented as the line of the cursor, and the
    // cursor goes to $0 (Insert mode)
    [InlineData("    x", "$", "<b>$0</b>", "    x<b></b>", 8, true)]
    [InlineData("  ", "$", "a\nb\n\nc", "  a\n  b\n\n  c", 2, false)]
    [InlineData("one", "", "{\n\t$0\n}", "o{\n    \n}ne", 4, true)]
    [InlineData("", "", "x$0", "x", 1, true)]
    public void InsertSnippet(string text, string keys, string snippet, string expected, int cursorCol,
        bool insertMode)
    {
        var editor = new EditorEngine(text, 40, 10, false);
        editor.Type(keys);
        editor.InsertSnippet(snippet);
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(cursorCol, editor.Cursor.Col);
        Assert.Equal(insertMode ? EditorMode.Insert : EditorMode.Normal, editor.Mode);
        // One change: one undo
        editor.Type("<Esc>u");
        Assert.Equal(text, editor.GetText());
    }

    [Theory]
    // Selected text: the snippet is put in its place, with the text at the mark
    [InlineData("say hello world", "wviw:snip<CR>", "<b>$0</b>", "say <b>hello</b> world", 0, 4)]
    [InlineData("  say hello", "wwv$:snip<CR>", "<a href=\"\">\n$0\n</a>", "  say <a href=\"\">\n  hello\n  </a>", 0, 6)]
    // Selected lines: replaced by the lines of the snippet, indented as they were. The lines of the text get the
    // indent the mark has in the snippet
    [InlineData("x\n  a\n    b\ny", "jVj:snip<CR>", "<div>\n    $0\n</div>", "x\n  <div>\n      a\n        b\n  </div>\ny", 1, 2)]
    [InlineData("a\n\nb", "Vjj:snip<CR>", "<p>$0</p>", "<p>a\n\nb</p>", 0, 0)]
    [InlineData("a\nb\nc", ":2,3snip<CR>", "[$0]", "a\n[b\nc]", 1, 0)]
    public void ASnippetWithTheMark_IsPutAroundTheSelection(string text, string keys, string snippet,
        string expected, int line, int col)
    {
        var editor = new EditorEngine(text, 40, 10, false);
        Assert.Equal(EditorRequest.Snippets, editor.Type(keys));
        editor.InsertSnippet(snippet);
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(new TextPos(line, col), editor.Cursor);
        Assert.Equal(EditorMode.Normal, editor.Mode);
        editor.Type("u");
        Assert.Equal(text, editor.GetText());
    }

    [Fact]
    public void WithoutTheMark_OrWithoutASelection_TheSnippetIsInserted()
    {
        // No mark: inserted after the cursor (at the end of the selection), the selected text stays
        var editor = new EditorEngine("one two", 40, 10, false);
        editor.Type("viw:snip<CR>");
        editor.InsertSnippet("<hr>");
        Assert.Equal("one<hr> two", editor.GetText());
        // ":snip" without a selection forgets the one before
        editor = new EditorEngine("one two", 40, 10, false);
        editor.Type("viw:snip<CR>");
        editor.Type(":snip<CR>");
        editor.InsertSnippet("<b>$0</b>");
        Assert.Equal("one<b></b> two", editor.GetText());
        Assert.Equal(EditorMode.Insert, editor.Mode);
    }

    [Fact]
    public void TheCommands_AskTheHost()
    {
        var editor = new EditorEngine("one\ntwo", 40, 10, false);
        Assert.Equal(EditorRequest.Snippets, editor.Type(":snip<CR>"));
        Assert.Equal(string.Empty, editor.SnippetText);
        Assert.Equal(EditorRequest.SnippetAdd, editor.Type(":snip add<CR>"));
        Assert.Equal("one", editor.SnippetText);
        Assert.Equal(EditorRequest.SnippetAdd, editor.Type("Vj:snip add<CR>"));
        Assert.Equal("one\ntwo", editor.SnippetText);
        Assert.Equal(EditorRequest.Snippets, editor.Type("ggvl:snip<CR>"));
        Assert.Equal("on", editor.SnippetText);
        Assert.Equal(EditorRequest.SnippetsEdit, editor.Type(":snip edit<CR>"));
    }
}
