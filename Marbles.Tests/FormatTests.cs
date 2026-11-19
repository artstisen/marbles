using Marbles.Format.Core;
using Marbles.Format.Css;
using Marbles.Format.Html;
using Marbles.Format.Xml;

namespace Marbles.Tests;

/// <summary>
/// ":format" in the editor: an HTML, CSS or XML file (or a note colored as one) formatted as Blyant Validator formats it (the formatters
/// themselves are tested in Format/, with the tests of Blyant Validator). In the collection "Static state": the
/// commands typed go in the command history, which all editors share
/// </summary>
[Collection("Static state")]
public class FormatTests
{
    // The editor of a file of this name: the kind of file is told by the name, as Blyant Validator tells it
    private static EditorEngine Editor(string text, string fileName) =>
        new(text, 60, 10, false) { FormatKind = SourceDocument.DetectKind(fileName) };

    // What the formatter of Blyant Validator makes of the text (lines ending in "\n")
    private static string Formatted(IFormatter formatter, string text, DocumentKind kind) =>
        formatter.Format(new SourceDocument(string.Empty, text, kind), new FormatOptions { NewLine = "\n" });

    [Theory]
    [InlineData("page.html", "<div><p>One</p><ul><li>a</li><li>b</li></ul></div>\n")]
    [InlineData("site.css", "a,b>c{color:red;margin:0 auto}\n@media (min-width:600px){.x{padding:1px 2px}}\n")]
    [InlineData("sitemap.xml", "<urlset><url><loc>https://example.com/</loc></url></urlset>\n")]
    public void Format_FormatsTheText_AsBlyantValidator_AndUndoTakesItBack(string fileName, string text)
    {
        var kind = SourceDocument.DetectKind(fileName);
        IFormatter formatter = kind switch
        {
            DocumentKind.Html => new HtmlFormatter(),
            DocumentKind.Css => new CssFormatter(),
            _ => new XmlFormatter()
        };
        var expected = Formatted(formatter, text, kind);
        Assert.NotEqual(text, expected);

        var editor = Editor(text, fileName);
        editor.Type(":format<CR>");
        Assert.Equal(expected, editor.GetText());
        Assert.False(editor.MessageIsError);
        Assert.Equal(Lang.T("Formatted (u undoes it)"), editor.Message);
        // One change: one undo
        editor.Type("u");
        Assert.Equal(text, editor.GetText());
        // Formatted again, it is the same
        editor.Type(":format<CR>:format<CR>");
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(Lang.T("Already formatted"), editor.Message);
    }

    [Theory]
    // As Blyant Validator: XHTML is XML, and the files of other kinds are not formatted
    [InlineData("page.xhtml", DocumentKind.Xml)]
    [InlineData("PAGE.HTM", DocumentKind.Html)]
    [InlineData("logo.svg", DocumentKind.Xml)]
    [InlineData("feed.rss", DocumentKind.Xml)]
    [InlineData("Marbles.csproj", DocumentKind.Unknown)]
    [InlineData("notes.md", DocumentKind.Unknown)]
    [InlineData("book.typ", DocumentKind.Unknown)]
    [InlineData("index.php", DocumentKind.Unknown)]
    public void Format_TellsTheKindOfFile_ByItsName_AsBlyantValidator(string fileName, DocumentKind expected)
    {
        var kind = SourceDocument.DetectKind(fileName);
        Assert.Equal(expected, kind is DocumentKind.Markdown or DocumentKind.Typst ? DocumentKind.Unknown : kind);
    }

    [Fact]
    public void Format_OfAFileWithCrLf_GivesTheSameLines()
    {
        // The editor keeps lines (the line endings of the file are written when it is saved)
        var lf = Editor("<div><p>One</p></div>\n", "a.html");
        var crlf = Editor("<div><p>One</p></div>\r\n", "a.html");
        lf.Type(":format<CR>");
        crlf.Type(":format<CR>");
        Assert.Equal(lf.GetText(), crlf.GetText());
        Assert.DoesNotContain('\r', crlf.GetText());
    }

    [Fact]
    public void Format_KeepsTheCursorOnItsLine_AtItsFirstCharacter()
    {
        var editor = Editor("a{color:red}\nb{color:blue}\nc{color:green}\n", "a.css");
        editor.Type("jj:format<CR>");
        Assert.Equal(2, editor.Cursor.Line);
        Assert.Equal(editor.Buffer[2].Length - editor.Buffer[2].TrimStart().Length, editor.Cursor.Col);
    }

    [Fact]
    public void Format_LeavesTextItCannotFormatSafely_AndTellsWhy()
    {
        // A block that is never closed, and XML that is not well-formed
        var css = Editor("a{color:red\n", "a.css");
        css.Type(":format<CR>");
        Assert.Equal("a{color:red\n", css.GetText());
        Assert.True(css.MessageIsError);
        Assert.StartsWith(Lang.F("Not formatted: {0}", string.Empty), css.Message);
        var xml = Editor("<a><b></a>\n", "a.xml");
        xml.Type(":format<CR>");
        Assert.Equal("<a><b></a>\n", xml.GetText());
        Assert.True(xml.MessageIsError);
        Assert.Contains("line", xml.Message);
    }

    [Theory]
    [InlineData("notes.md")]
    [InlineData("book.typ")]
    [InlineData("index.php")]
    [InlineData("Marbles.csproj")]
    [InlineData("readme.txt")]
    public void Format_OnlyFormatsHtmlCssAndXml(string fileName)
    {
        var editor = Editor("<div><p>One</p></div>\n", fileName);
        editor.Type(":format<CR>");
        Assert.Equal("<div><p>One</p></div>\n", editor.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(Lang.T("Only HTML, CSS and XML files can be formatted"), editor.Message);
    }

    [Fact]
    public void Format_IsRefused_InAReadOnlyText()
    {
        var editor = new EditorEngine("a{color:red}\n", 60, 10, false)
            { FormatKind = DocumentKind.Css, ReadOnly = true };
        editor.Type(":format<CR>");
        Assert.Equal("a{color:red}\n", editor.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(DialogText.EditorReadOnly, editor.Message);
    }

    [Fact]
    public void Format_ChangesOnlyTheLinesThatDiffer()
    {
        // The lines before and after the change stay (and a mark on them)
        const string text = "a {\n  color: red;\n}\n\nb{color:blue}\n\nc {\n  color: green;\n}\n";
        var editor = Editor(text, "a.css");
        editor.Type("Gkkma:format<CR>"); // (the mark on "  color: green;")
        Assert.Equal(Formatted(new CssFormatter(), text, DocumentKind.Css), editor.GetText());
        editor.Type("gg'a");
        Assert.Equal("  color: green;", editor.Buffer[editor.Cursor.Line]);
    }

    [Fact]
    public void Format_KeepsCharactersWhole_WhereTheChangeStartsAndEnds()
    {
        // The change starts and ends next to characters of two chars (an emoji) and a letter with a combining mark:
        // whole lines are replaced, so none of them is cut in two, and undo gives them back whole
        const string text = "a::after{content:\"😀\";color:red}\nb{content:\"e\u0301\"}\n";
        var editor = Editor(text, "a.css");
        editor.Type(":format<CR>");
        Assert.Equal(Formatted(new CssFormatter(), text, DocumentKind.Css), editor.GetText());
        Assert.Contains("😀", editor.GetText());
        Assert.Contains("e\u0301", editor.GetText());
        editor.Type("u");
        Assert.Equal(text, editor.GetText());
    }

    // The editor of a note of a data file, colored as a syntax (":syntax html")
    private static EditorEngine Note(string text, SyntaxType syntax) =>
        new(text, 60, 10, false) { FormatBySyntax = true, Syntax = syntax };

    [Theory]
    [InlineData("html", "<div><p>One</p><ul><li>a</li></ul></div>\n")]
    [InlineData("css", "a,b>c{color:red;margin:0 auto}\n")]
    [InlineData("xml", "<urlset><url><loc>https://example.com/</loc></url></urlset>\n")]
    public void Format_InANote_FormatsBySyntax_AndUndoTakesItBack(string syntax, string text)
    {
        var editor = Note(text, SyntaxHighlighter.ParseType(syntax));
        editor.Type(":format<CR>");
        Assert.NotEqual(text, editor.GetText());
        Assert.False(editor.MessageIsError);
        Assert.Equal(Lang.T("Formatted (u undoes it)"), editor.Message);
        editor.Type("u");
        Assert.Equal(text, editor.GetText());
    }

    [Fact]
    public void Format_InANote_FollowsTheSyntaxCommand()
    {
        // Not colored as HTML, CSS or XML: refused, with how to color it
        var editor = Note("<div><p>One</p></div>\n", SyntaxType.Markdown);
        editor.Type(":format<CR>");
        Assert.Equal("<div><p>One</p></div>\n", editor.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(Lang.T("Color the note as HTML, CSS or XML to format it: :syntax html"), editor.Message);
        // ":syntax html": now it is
        editor.Type(":syntax html<CR>:format<CR>");
        Assert.NotEqual("<div><p>One</p></div>\n", editor.GetText());
        Assert.False(editor.MessageIsError);
    }

    [Fact]
    public void Format_ChangesOnlyTheText_AndWritesNothing()
    {
        // The text is changed (modified, to be saved as any change): ":format" asks nothing of the host
        var editor = Editor("<div><p>One</p></div>\n", "a.html");
        Assert.Equal(EditorRequest.None, editor.Type(":format<CR>"));
        Assert.True(editor.IsModified);
    }

    [Fact]
    public void Bom_IsAskedOfTheHost_WithWhatWasTyped()
    {
        var editor = new EditorEngine("text", 60, 10, false);
        Assert.Equal(EditorRequest.Bom, editor.Type(":bom ON<CR>"));
        Assert.Equal("on", editor.BomArgument);
        Assert.Equal(EditorRequest.Bom, editor.Type(":bom off<CR>"));
        Assert.Equal("off", editor.BomArgument);
        Assert.Equal(EditorRequest.Bom, editor.Type(":bom<CR>"));
        Assert.Equal(string.Empty, editor.BomArgument);
    }
}
