// Copied from Blyant Validator 0.6.2 (tests/Blyant.Validator.Tests/XmlFormatterTests.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using Marbles.Format.Core;
using Marbles.Format.Xml;

namespace Marbles.Format.Tests;

public class XmlFormatterTests
{
    private static string Format(string xml, string? newline = "\n") =>
        new XmlFormatter().Format(new SourceDocument("t.xml", xml.ReplaceLineEndings("\n"), DocumentKind.Xml),
            new FormatOptions { NewLine = newline });

    private const string Messy = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!-- The pages -->
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>https://example.org/</loc>
        <lastmod>2026-11-10</lastmod></url>


              <url   ><loc>https://example.org/a?x=1&amp;y=2</loc><priority>0.5</priority></url></urlset>
        """;

    [Fact]
    public void Format_ProducesExpectedLayout()
    {
        const string expected = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!-- The pages -->
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url>
                <loc>https://example.org/</loc>
                <lastmod>2026-11-10</lastmod>
              </url>

              <url>
                <loc>https://example.org/a?x=1&amp;y=2</loc>
                <priority>0.5</priority>
              </url>
            </urlset>

            """;
        Assert.Equal(expected.ReplaceLineEndings("\n"), Format(Messy));
    }

    [Fact]
    public void Format_IsStable()
    {
        var once = Format(Messy);
        Assert.Equal(once, Format(once));
    }

    private const string Svg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 40"><defs><linearGradient id="g"><stop offset="0" stop-color="red"/></linearGradient></defs>
        <text x="5" y="20">Harbour <tspan font-weight="bold">Lights</tspan>  !</text><style><![CDATA[ text { fill: url(#g) } ]]></style>
        <g xml:space="preserve"><text>  two  spaces  </text>
           </g></svg>
        """;

    [Fact]
    public void TextAndPreservedSpace_AreKeptAsWritten()
    {
        var result = Format(Svg);
        Assert.Contains("<text x=\"5\" y=\"20\">Harbour <tspan font-weight=\"bold\">Lights</tspan>  !</text>", result);
        Assert.Contains("<style><![CDATA[ text { fill: url(#g) } ]]></style>", result);
        Assert.Contains("<g xml:space=\"preserve\"><text>  two  spaces  </text>\n   </g>", result);
        Assert.Contains("\n  <defs>\n    <linearGradient id=\"g\">\n      <stop offset=\"0\" stop-color=\"red\"/>\n", result);
    }

    [Theory]
    [InlineData(Messy)]
    [InlineData(Svg)]
    public void Format_OnlyChangesWhitespace(string xml)
    {
        static string NoWhitespace(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
        Assert.Equal(NoWhitespace(xml), NoWhitespace(Format(xml)));
    }

    [Fact]
    public void Format_RefusesXmlThatIsNotWellFormed()
    {
        Assert.Throws<Core.FormatException>(() => Format("<a><b></a>"));
    }

    [Fact]
    public void Format_KeepsTheFilesLineEnding_AndTheDoctype()
    {
        var result = new XmlFormatter().Format(
            new SourceDocument("t.xml", "<!DOCTYPE note>\r\n<note><to>A</to></note>", DocumentKind.Xml), new FormatOptions());
        Assert.Equal("<!DOCTYPE note>\r\n<note>\r\n  <to>A</to>\r\n</note>\r\n", result);
    }

    [Fact]
    public void Format_NormalizesSpaceInTags_NotInValues()
    {
        Assert.Equal("<a b=\"1  2\" c='x'/>\n", Format("<a   b=\"1  2\"\n   c='x'/>"));
        Assert.Equal("<a b=\"1\" />\n", Format("<a b=\"1\" />"));
    }
}
