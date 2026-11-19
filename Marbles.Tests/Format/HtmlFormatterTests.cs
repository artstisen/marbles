// Copied from Blyant Validator 0.6.2 (tests/Blyant.Validator.Tests/HtmlFormatterTests.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using Marbles.Format.Core;
using Marbles.Format.Html;

namespace Marbles.Format.Tests;

public class HtmlFormatterTests
{
    // The raw string literals below get the line endings of this file, so inputs are made LF first.
    private static string Format(string html, FormatOptions? options = null) =>
        new HtmlFormatter().Format(new SourceDocument("t.html", html.ReplaceLineEndings("\n"), DocumentKind.Html), options ?? new FormatOptions { NewLine = "\n" });

    private const string Page = """
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><title>  My   page </title>
        <link rel="stylesheet" href="site.css">
        </head>
        <body>
        <header class="top"><nav><ul><li><a href="/">Home</a><li><a href="/about">About <b>us</b></a></ul></nav></header>


        <main>
        <h1>Hello,   <em>world</em>!</h1>
        <p>First line<br>Second line with a <a href="#">link</a>.
        <pre>
          keep   this
        </pre>
        <button type="button"><svg viewBox="0 0 16 16"><path d="M0 0h16v16H0z"/><circle cx="8" cy="8" r="4"/></svg>Save</button>
        <label>Size <select name="s"><option>S<option selected>M</select></label>
        <!-- a comment -->
        <table><tr><td>1<td>2</table>
        <textarea>
         raw   text </textarea>
        </main>
        <script>
          if (a < b) { console.log("</div>"); }
        </script>
        </body></html>
        """;

    [Fact]
    public void Format_ProducesExpectedLayout()
    {
        const string expected = """
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <title>My page</title>
                <link rel="stylesheet" href="site.css">
              </head>
              <body>
                <header class="top">
                  <nav>
                    <ul>
                      <li><a href="/">Home</a>
                      <li><a href="/about">About <b>us</b></a>
                    </ul>
                  </nav>
                </header>

                <main>
                  <h1>Hello, <em>world</em>!</h1>
                  <p>
                    First line<br>
                    Second line with a <a href="#">link</a>.
                  <pre>
              keep   this
            </pre>
                  <button type="button"><svg viewBox="0 0 16 16">
                    <path d="M0 0h16v16H0z"/>
                    <circle cx="8" cy="8" r="4"/>
                  </svg>Save</button>
                  <label>Size <select name="s">
                    <option>S
                    <option selected>M
                  </select></label>
                  <!-- a comment -->
                  <table>
                    <tr>
                      <td>1
                      <td>2
                  </table>
                  <textarea>
             raw   text </textarea>
                </main>
                <script>
              if (a < b) { console.log("</div>"); }
            </script>
              </body>
            </html>

            """;

        Assert.Equal(expected.ReplaceLineEndings("\n"), Format(Page));
    }

    [Fact]
    public void Format_IsStable()
    {
        var once = Format(Page);
        Assert.Equal(once, Format(once));
    }

    [Fact]
    public void Format_OnlyChangesWhitespace()
    {
        static string NoWhitespace(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
        Assert.Equal(NoWhitespace(Page), NoWhitespace(Format(Page)));
    }

    [Theory]
    [InlineData("<div><p>Hi   there</p><p>Bye</p></div>", "<div>\n  <p>Hi there</p>\n  <p>Bye</p>\n</div>\n")]
    [InlineData("<p>a<b>b</b>c</p>", "<p>a<b>b</b>c</p>\n")]
    [InlineData("<div></span>text</div>", "<div></span>text</div>\n")]
    [InlineData("<a href=x><div>block</div></a>", "<a href=x>\n  <div>block</div>\n</a>\n")]
    [InlineData("<p>no&nbsp;break kept</p>", "<p>no&nbsp;break kept</p>\n")]
    [InlineData("<img src=a.png alt='' />", "<img src=a.png alt='' />\n")]
    public void Format_Examples(string input, string expected)
    {
        Assert.Equal(expected, Format(input));
    }

    [Fact]
    public void Format_FormatsStyleElements()
    {
        var result = Format("<head><style>a{color:red}</style></head>");
        Assert.Equal("<head>\n  <style>\n    a {\n      color: red;\n    }\n  </style>\n</head>\n", result);
    }

    [Fact]
    public void Format_KeepsBrokenStyleAsItIs()
    {
        var result = Format("<style>a{color:red</style>");
        Assert.Equal("<style>a{color:red</style>\n", result);
    }

    [Fact]
    public void Format_KeepsTheFilesLineEnding()
    {
        var result = new HtmlFormatter().Format(
            new SourceDocument("t.html", "<div>\r\n<p>x</p></div>", DocumentKind.Html), new FormatOptions());
        Assert.Equal("<div>\r\n  <p>x</p>\r\n</div>\r\n", result);
    }

    [Fact]
    public void Format_BreaksLongInlineContentOntoItsOwnLine()
    {
        var text = new string('x', 120);
        Assert.Equal($"<p>\n  {text}\n</p>\n", Format($"<p>{text}</p>"));
    }
}
