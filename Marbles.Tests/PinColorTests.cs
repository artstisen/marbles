namespace Marbles.Tests;

// The names of the colors are translated: not while other tests change the language
[Collection("Static state")]
public class PinColorTests
{
    [Fact]
    public void NoColor_UnlessSet()
    {
        Assert.Null(PinColors.Get(null));
        Assert.Null(PinColors.Get(""));
        Assert.Null(PinColors.Get("fp12"));
    }

    [Fact]
    public void Color_IsKeptAsALetter_BesideTheOtherOptions()
    {
        var options = PinColors.Set("fp3", ConsoleColor.Magenta);
        Assert.Equal("fp3N", options);
        Assert.Equal(ConsoleColor.Magenta, PinColors.Get(options));
        Assert.Equal(3, Entries.ExtractRank(options));

        options = PinColors.Set(options, ConsoleColor.Black);
        Assert.Equal("fp3A", options);
        Assert.Equal(ConsoleColor.Black, PinColors.Get(options));
        Assert.Equal("fp3", PinColors.Set(options, null));
    }

    [Fact]
    public void AllColors_CanBeSet()
    {
        Assert.Equal(ColorPicker.Count, Enum.GetValues<ConsoleColor>().Length);
        foreach (var color in Enum.GetValues<ConsoleColor>())
        {
            Assert.Equal(color, PinColors.Get(PinColors.Set("fp0", color)));
        }
    }

    [Theory]
    [InlineData("fph0", ConsoleColor.Cyan)]
    [InlineData("fp1i", ConsoleColor.Yellow)]
    [InlineData("jfp", ConsoleColor.Red)]
    public void HighlightStylesOfEarlierVersions_AreShownAsColors(string options, ConsoleColor expected)
    {
        Assert.Equal(expected, PinColors.Get(options));
        // Setting a color replaces the old tag
        Assert.Equal(ConsoleColor.Green, PinColors.Get(PinColors.Set(options, ConsoleColor.Green)));
        Assert.DoesNotContain(PinColors.Set(options, null), c => "hij".Contains(c));
    }

    [Fact]
    public void Names()
    {
        Assert.Equal("Dark Blue", ColorPicker.Name(ConsoleColor.DarkBlue));
        Assert.Equal("Yellow", ColorPicker.Name(ConsoleColor.Yellow));
    }
}
