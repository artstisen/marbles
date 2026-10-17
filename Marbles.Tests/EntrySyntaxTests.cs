namespace Marbles.Tests;

public class EntrySyntaxTests
{
    [Fact]
    public void TheSyntax_IsKeptAsATagInTheOptions()
    {
        Assert.Equal(SyntaxType.None, EntrySyntax.Get(null));
        Assert.Equal(SyntaxType.None, EntrySyntax.Get("fp3E"));
        Assert.Equal("fp3Em", EntrySyntax.Set("fp3E", SyntaxType.Markdown));
        Assert.Equal(SyntaxType.Markdown, EntrySyntax.Get("fp3Em"));
        // Another syntax replaces it, None removes it
        Assert.Equal("fp3Ex", EntrySyntax.Set("fp3Em", SyntaxType.Xml));
        Assert.Equal("fp3E", EntrySyntax.Set("fp3Ex", SyntaxType.None));
        // (a macro is a file of its own, not a note)
        foreach (var type in Enum.GetValues<SyntaxType>().Where(type => type is not (SyntaxType.None or SyntaxType.Macro)))
        {
            Assert.Equal(type, EntrySyntax.Get(EntrySyntax.Set("f", type)));
            Assert.NotEmpty(EntrySyntax.ShortName(type));
        }
    }

    [Fact]
    public void TheTags_DoNotChangeTheOtherOptions()
    {
        // Favorite, pinned, the order of the pinned entries and the color stay as they are
        var options = EntrySyntax.Set("fp12" + "E", SyntaxType.JavaScript);
        Assert.Contains(Entries.FavoritesTag, options);
        Assert.Contains(Entries.PinnedTag, options);
        Assert.Contains("12", options);
        Assert.Equal(ConsoleColor.DarkRed, PinColors.Get(options));
        // And the color tags are not taken as a syntax
        Assert.Equal(SyntaxType.None, EntrySyntax.Get("ABCDEFGHIJKLMNOPhij0123456789fp"));
    }
}
