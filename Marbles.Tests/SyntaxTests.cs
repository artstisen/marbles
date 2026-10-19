namespace Marbles.Tests;

[Collection("Static state")]
public class SyntaxTests
{
    // The styles of the lines of a text, one character for each character:
    // . none, # comment, S string, W keyword (word, tag, selector), N name, 9 number
    private const string Codes = ".123456BICKQLAUR#SWN9";

    private static string[] Styles(SyntaxType type, params string[] lines)
    {
        var result = new string[lines.Length];
        var state = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var styles = SyntaxHighlighter.Highlight(type, lines[i], state, out state);
            result[i] = styles == null
                ? new string('.', lines[i].Length)
                : string.Concat(styles.Select(style => Codes[(int)style]));
        }

        return result;
    }

    private static string Style(SyntaxType type, string line) => Styles(type, line)[0];

    #region JavaScript

    [Theory]
    [InlineData("const x = 12;", "WWWWW.....99.")]
    [InlineData("let s = 'a b';", "WWW.....SSSSS.")]
    [InlineData("s = \"it's\"; // note", "....SSSSSS..#######")]
    [InlineData("if (a) return null;", "WW.....WWWWWW.WWWW.")]
    [InlineData("x = 1.5e3 + 0xFF;", "....99999...9999.")]
    [InlineData("obj.default = iffy;", "...................")]
    [InlineData("a = 'it\\'s' + b;", "....SSSSSSS.....")]
    [InlineData("s = 'open", "....SSSSS")]
    [InlineData("a /* c */ b", "..#######..")]
    public void JavaScript_Lines(string line, string expected)
    {
        Assert.Equal(expected, Style(SyntaxType.JavaScript, line));
    }

    [Fact]
    public void JavaScript_CommentsAndTemplatesOverSeveralLines()
    {
        Assert.Equal(new[] { "WWW...#####", "#######", "#####WWW" },
            Styles(SyntaxType.JavaScript, "let a /* on", "it goes", "end*/let"));
        Assert.Equal(new[] { "....SSS", "SSSSSSSSS", "SSS...9." },
            Styles(SyntaxType.JavaScript, "s = `ab", "let ${x} ", "cd` + 1;"));
        // A string in quotes ends with its line
        Assert.Equal(new[] { "....SSS", "WWW..." }, Styles(SyntaxType.JavaScript, "s = 'ab", "let x;"));
    }

    #endregion

    #region JSON

    [Theory]
    [InlineData("{\"name\": \"value\",", ".NNNNNN..SSSSSSS.")]
    [InlineData("  \"n\": -12.5e3, \"b\": true, \"c\": null}", "..NNN..9999999..NNN..WWWW..NNN..WWWW.")]
    [InlineData("[\"a\", \"b\"]", ".SSS..SSS.")]
    [InlineData("\"key\" : 1", "NNNNN...9")]
    public void Json_Lines(string line, string expected)
    {
        Assert.Equal(expected, Style(SyntaxType.Json, line));
    }

    #endregion

    #region CSS

    [Theory]
    [InlineData("body { color: red; }", "WWWW...NNNNN........")]
    [InlineData("a:hover, .b > c {", "WWWWWWWWWWWWWWW..")]
    [InlineData("@media (max-width: 600px) {", "WWWWWW.............99999...")]
    [InlineData("@import \"a.css\";", "WWWWWWW.SSSSSSS.")]
    public void Css_Lines(string line, string expected)
    {
        Assert.Equal(expected, Style(SyntaxType.Css, line));
    }

    [Fact]
    public void Css_PropertiesAndValues()
    {
        var styles = Styles(SyntaxType.Css,
            "p {",
            "  margin: 0 auto 1.5em -2px;",
            "  color: #ff0000 !important;",
            "  font: 12px \"My Font\", h1;",
            "  /* note */ top: 50%",
            "}",
            "h1,",
            "h2 { a: b }");
        Assert.Equal("W..", styles[0]);
        Assert.Equal("..NNNNNN..9......99999.9999.", styles[1]);
        Assert.Equal("..NNNNN..9999999.WWWWWWWWWW.", styles[2]);
        Assert.Equal("..NNNN..9999.SSSSSSSSS.....", styles[3]);
        Assert.Equal("..##########.NNN..999", styles[4]);
        Assert.Equal(".", styles[5]);
        // A list of selectors that continues on the next line
        Assert.Equal("WWW", styles[6]);
        Assert.Equal("WW...N.....", styles[7]);
    }

    [Fact]
    public void Css_CommentsAndNestedBlocks()
    {
        var styles = Styles(SyntaxType.Css,
            "/* start",
            "   a { b: c } */",
            "@media print {",
            "  a:hover { top: 1px }",
            "}",
            "b { }");
        Assert.Equal("########", styles[0]);
        Assert.Equal("################", styles[1]);
        Assert.Equal("WWWWWW........", styles[2]);
        // Inside the block of @media: "a:hover" is a selector, not a property
        Assert.Equal("..WWWWWWW...NNN..999..", styles[3]);
        Assert.Equal("W....", styles[5]);
    }

    #endregion

    #region HTML

    [Theory]
    [InlineData("<p class=\"a b\">Text</p>", "WW.NNNNN.SSSSSW....WWWW")]
    [InlineData("<br/> &amp; &#169; a & b", "WWWWW.99999.999999......")]
    [InlineData("<!DOCTYPE html>", "WWWWWWWWWWWWWWW")]
    [InlineData("a <!-- c --> b", "..##########..")]
    [InlineData("<input disabled value=x>", "WWWWWW.NNNNNNNN.NNNNN.SW")]
    [InlineData("1 < 2 and a < b", "...............")]
    public void Html_Lines(string line, string expected)
    {
        Assert.Equal(expected, Style(SyntaxType.Html, line));
    }

    [Fact]
    public void Html_TagsAndCommentsOverSeveralLines()
    {
        Assert.Equal(new[] { "WW.NNNNN.SSS", "...NNNN.SSSW.." },
            Styles(SyntaxType.Html, "<a class=\"x\"", "   href='y'>ok"));
        Assert.Equal(new[] { "..####", "######", "###.WWWW" },
            Styles(SyntaxType.Html, "a <!--", "<b>no>", "-->.<b/>"));
    }

    [Fact]
    public void Html_StyleAndScriptAreCssAndJavaScript()
    {
        var styles = Styles(SyntaxType.Html,
            "<style>",
            "p { color: red; }",
            "</style><b>",
            "<script src=\"a.js\">",
            "let x = '<b>'; // c",
            "/* </script>",
            "<p>let</p>");
        Assert.Equal("WWWWWWW", styles[0]);
        Assert.Equal("W...NNNNN........", styles[1]);
        Assert.Equal("WWWWWWWWWWW", styles[2]);
        Assert.Equal("WWWWWWW.NNN.SSSSSSW", styles[3]);
        Assert.Equal("WWW.....SSSSS..####", styles[4]);
        // The script ends at its closing tag, also inside a comment (as in a web browser)
        Assert.Equal("###WWWWWWWWW", styles[5]);
        Assert.Equal("WWW...WWWW", styles[6]);
    }

    [Fact]
    public void Html_ScriptOnOneLine()
    {
        Assert.Equal("WWWWWWWWWWW.....9.WWWWWWWWWWWW",
            Style(SyntaxType.Html, "<script>let x = 1;</script><p>"));
    }

    #endregion

    #region XML

    [Theory]
    [InlineData("<?xml version=\"1.0\"?>", "WWWWW.NNNNNNN.SSSSSWW")]
    [InlineData("<a:b c=\"d\"/> &lt;", "WWWW.N.SSSWW.9999")]
    [InlineData("<script>let x;</script>", "WWWWWWWW......WWWWWWWWW")]
    [InlineData("<x><![CDATA[ <b> & ]]></x>", "WWWWWWWWWWWW.......WWWWWWW")]
    public void Xml_Lines(string line, string expected)
    {
        Assert.Equal(expected, Style(SyntaxType.Xml, line));
    }

    [Fact]
    public void Xml_CDataAndCommentsOverSeveralLines()
    {
        Assert.Equal(new[] { "WWWWWWWWW....", ".....", "..WWWWWW" },
            Styles(SyntaxType.Xml, "<![CDATA[ <a>", "<b/> ", "x ]]><c>"));
        Assert.Equal(new[] { "####", "######WW" }, Styles(SyntaxType.Xml, "<!--", "<a>--><c"));
    }

    #endregion

    #region C#

    [Theory]
    [InlineData("public int x = 12;", "WWWWWW.WWW.....99.")]
    [InlineData("var s = @\"C:\\dir\";", "WWW.....SSSSSSSSS.")]
    [InlineData("x = $\"{a} b\";", "....SSSSSSSS.")]
    [InlineData("c = 'x'; // done", "....SSS..#######")]
    [InlineData("int @class = 1;", "WWW..........9.")]
    [InlineData("obj.string = null;", ".............WWWW.")]
    [InlineData("#region Fields", "NNNNNNNNNNNNNN")]
    [InlineData("  #if DEBUG", "..NNNNNNNNN")]
    public void CSharp_Lines(string line, string expected)
    {
        Assert.Equal(expected, Style(SyntaxType.CSharp, line));
    }

    [Fact]
    public void CSharp_VerbatimAndRawStringsAndCommentsOverSeveralLines()
    {
        // In a verbatim string "" is a quote, and a backslash is only a backslash
        Assert.Equal(new[] { "....SSSSS", "SSSSSSSS", "SSSS..WWW" },
            Styles(SyntaxType.CSharp, "s = @\"one", "two \"\" x", "end\"; int"));
        Assert.Equal(new[] { "....SSS", "SSSSSSS", "SSS..WWW" },
            Styles(SyntaxType.CSharp, "s = \"\"\"", "a \"b\" c", "\"\"\"; int"));
        Assert.Equal(new[] { "WWW...####", "##.WWW" }, Styles(SyntaxType.CSharp, "int a /* x", "*/ int"));
        // A "#" inside a comment is not the preprocessor
        Assert.Equal(new[] { "##", "#####" }, Styles(SyntaxType.CSharp, "/*", "#if*/"));
    }

    [Fact]
    public void CSharp_IsReadAsJavaScriptIs_ButWithItsOwnWords()
    {
        // The same reader: what JavaScript has and C# has not (template strings, "$" in names) is left out
        Assert.Equal("WWW.........", Style(SyntaxType.CSharp, "var s = `ab`"));
        Assert.Equal("WWW.....SSSS", Style(SyntaxType.JavaScript, "var s = `ab`"));
        Assert.Equal("......", Style(SyntaxType.CSharp, "let $x"));
        Assert.Equal("WWW...", Style(SyntaxType.JavaScript, "let $x"));
        Assert.Equal(SyntaxType.CSharp, SyntaxHighlighter.ParseType("cs"));
        Assert.Equal(SyntaxType.CSharp, SyntaxHighlighter.ParseType("C#"));
        Assert.Equal("C#", SyntaxHighlighter.Name(SyntaxType.CSharp));
        // A note remembers it with the letter "s"
        Assert.Equal(SyntaxType.CSharp, EntrySyntax.Get(EntrySyntax.Set("fp1", SyntaxType.CSharp)));
    }

    #endregion

    #region Kinds of text

    [Theory]
    [InlineData("index.html", nameof(SyntaxType.Html))]
    [InlineData("PAGE.HTM", nameof(SyntaxType.Html))]
    [InlineData("site.css", nameof(SyntaxType.Css))]
    [InlineData("app.js", nameof(SyntaxType.JavaScript))]
    [InlineData("module.mjs", nameof(SyntaxType.JavaScript))]
    [InlineData("Program.cs", nameof(SyntaxType.CSharp))]
    [InlineData("script.CSX", nameof(SyntaxType.CSharp))]
    [InlineData("data.json", nameof(SyntaxType.Json))]
    [InlineData("feed.xml", nameof(SyntaxType.Xml))]
    [InlineData("icon.SVG", nameof(SyntaxType.Xml))]
    [InlineData("notes.md", nameof(SyntaxType.Markdown))]
    [InlineData("notes.txt", nameof(SyntaxType.None))]
    [InlineData("json", nameof(SyntaxType.None))]
    public void TheKindOfAFile_IsToldByItsName(string name, string expected)
    {
        Assert.Equal(expected, SyntaxHighlighter.TypeOfFile(name).ToString());
    }

    [Fact]
    public void LinesWithoutAnythingToColor_HaveNoStyles()
    {
        Assert.Null(SyntaxHighlighter.Highlight(SyntaxType.JavaScript, "", 0, out _));
        Assert.Null(SyntaxHighlighter.Highlight(SyntaxType.JavaScript, "a + b", 0, out _));
        Assert.Null(SyntaxHighlighter.Highlight(SyntaxType.None, "let x;", 0, out _));
        // The state is passed on by an empty line
        SyntaxHighlighter.Highlight(SyntaxType.JavaScript, "", JavaScriptHighlighter.InComment, out var state);
        Assert.Equal(JavaScriptHighlighter.InComment, state);
    }

    #endregion

    #region In the editor

    private static EditorEngine Editor(string text)
    {
        return new EditorEngine(text, 40, 10, false);
    }

    private static string Styles(EditorEngine editor, int line)
    {
        var styles = editor.SyntaxStyles(line);
        return styles == null
            ? new string('.', editor.Buffer[line].Length)
            : string.Concat(styles.Select(style => Codes[(int)style]));
    }

    [Fact]
    public void Editor_SyntaxCommand_ColorsAsAKind()
    {
        var editor = Editor("let x;");
        Assert.Equal(SyntaxType.None, editor.Syntax);
        Assert.Equal(EditorRequest.SyntaxChanged, editor.Type(":syntax js<CR>"));
        Assert.Equal(SyntaxType.JavaScript, editor.Syntax);
        Assert.Equal(DialogText.SyntaxOn + "JavaScript", editor.Message);
        Assert.Equal("WWW...", Styles(editor, 0));
        // Off, and on again: the kind is remembered
        editor.Type(":syntax<CR>");
        Assert.Equal(SyntaxType.None, editor.Syntax);
        Assert.Equal(DialogText.SyntaxOff, editor.Message);
        editor.Type(":syntax<CR>");
        Assert.Equal(SyntaxType.JavaScript, editor.Syntax);
        editor.Type(":syntax HTML<CR>");
        Assert.Equal(SyntaxType.Html, editor.Syntax);
        editor.Type(":syntax css<CR>:syntax json<CR>:syntax md<CR>");
        Assert.Equal(SyntaxType.Markdown, editor.Syntax);
        editor.Type(":syntax cobol<CR>");
        Assert.True(editor.MessageIsError);
        Assert.Equal(SyntaxType.Markdown, editor.Syntax);
    }

    [Fact]
    public void Editor_WithoutAKind_SyntaxOnIsMarkdown()
    {
        var editor = Editor("# Title");
        editor.Type(":syntax on<CR>");
        Assert.Equal(SyntaxType.Markdown, editor.Syntax);
        // A file of another kind sets what "on" means
        editor.SyntaxDefault = SyntaxType.Css;
        editor.Type(":syntax off<CR>:syntax<CR>");
        Assert.Equal(SyntaxType.Css, editor.Syntax);
    }

    [Fact]
    public void Editor_TheLinesBelowAChange_AreColoredAgain()
    {
        var editor = Editor("let a;\nlet b;\nlet c;\nlet d;");
        editor.Syntax = SyntaxType.JavaScript;
        Assert.Equal("WWW...", Styles(editor, 3));
        // A comment is started on the first line: the lines below are inside it
        editor.Type("ggA /*<Esc>");
        Assert.Equal("WWW....##", Styles(editor, 0));
        Assert.Equal("######", Styles(editor, 1));
        Assert.Equal("######", Styles(editor, 3));
        // It is ended on the third line
        editor.Type("jjA */ x<Esc>");
        Assert.Equal("######", Styles(editor, 1));
        Assert.Equal("#########..", Styles(editor, 2));
        Assert.Equal("WWW...", Styles(editor, 3));
        // Lines are removed and put back
        editor.Type("ggdd");
        Assert.Equal("WWW...", Styles(editor, 0));
        // (the end of the comment is not a comment without its start)
        Assert.Equal("WWW........", Styles(editor, 1));
        editor.Type("u");
        Assert.Equal("######", Styles(editor, 1));
        Assert.Equal("WWW...", Styles(editor, 3));
    }

    #endregion
}
