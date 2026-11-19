// Copied from Blyant Validator 0.6.2 (tests/Blyant.Validator.Tests/CssFormatterTests.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using Marbles.Format.Core;
using Marbles.Format.Css;
using Marbles.Format.Css.Syntax;

namespace Marbles.Format.Tests;

public class CssFormatterTests
{
    // The raw string literals below get the line endings of this file, so inputs are made LF first.
    private static string Format(string css, FormatOptions? options = null) =>
        new CssFormatter().Format(new SourceDocument("t.css", css.ReplaceLineEndings("\n"), DocumentKind.Css), options ?? new FormatOptions { NewLine = "\n" });

    private const string Messy = """
        /* header */
        a,b>c{color:red;margin:0 auto!important}


        @media (min-width:600px){.x{padding:1px 2px}}
        .y{--raw:  {a b}  ;background:url( "a b.png" )} /* trailing */
        @import url(x.css);
        .empty{}
        """;

    [Fact]
    public void Format_ProducesExpectedLayout()
    {
        const string expected = """
            /* header */
            a,
            b>c {
              color: red;
              margin: 0 auto !important;
            }

            @media (min-width:600px) {
              .x {
                padding: 1px 2px;
              }
            }
            .y {
              --raw: {a b};
              background: url( "a b.png" );
            } /* trailing */
            @import url(x.css);
            .empty {}

            """;

        Assert.Equal(expected.ReplaceLineEndings("\n"), Format(Messy));
    }

    [Fact]
    public void Format_IsStable()
    {
        var once = Format(Messy);
        Assert.Equal(once, Format(once));
    }

    [Fact]
    public void Format_KeepsTheTokens()
    {
        static List<string> Tokens(string css) =>
            CssTokenizer.Tokenize(css, out _)
                .Where(t => !t.IsTrivia && t.Type != CssTokenType.Semicolon)
                .Select(t => t.Text(css))
                .ToList();

        Assert.Equal(Tokens(Messy), Tokens(Format(Messy)));
    }

    [Fact]
    public void Format_UsesIndentOptionsAndLineEnding()
    {
        var result = Format("a{color:red}", new FormatOptions { UseTabs = true, NewLine = "\r\n" });
        Assert.Equal("a {\r\n\tcolor: red;\r\n}\r\n", result);
    }

    [Fact]
    public void Format_KeepsTheFilesLineEnding()
    {
        var result = new CssFormatter().Format(new SourceDocument("t.css", "a{color:red}\r\nb{}", DocumentKind.Css), new FormatOptions());
        Assert.Equal("a {\r\n  color: red;\r\n}\r\nb {}\r\n", result);
    }

    [Theory]
    [InlineData("a { color: red")]
    [InlineData("a { color: red } }")]
    public void Format_RefusesBrokenStructure(string css)
    {
        Assert.Throws<Core.FormatException>(() => Format(css));
    }
}
