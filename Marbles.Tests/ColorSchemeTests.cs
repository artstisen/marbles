namespace Marbles.Tests;

/// <summary>
/// The color schemes: the built-in ones, the files of the user's own, and the colors that can be used
/// </summary>
[Collection("Static state")]
public class ColorSchemeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "marbles-schemes-" + Guid.NewGuid());

    public ColorSchemeTests()
    {
        ColorSchemes.DirectoryOverride = _directory;
    }

    public void Dispose()
    {
        ColorSchemes.DirectoryOverride = null;
        ConsoleColors.Use(ColorScheme.Default);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void TheBuiltInSchemes_HaveTheirNames_AndDeepBlueABackground()
    {
        Assert.Equal(new[]
        {
            "Default", "Deep Blue", "Marbles Quest", "Night Writer", "Yellow Monochrome", "Green Monochrome",
            "Ice Warp", "Water Sport"
        }, ColorScheme.BuiltIn.Select(scheme => scheme.Name));
        Assert.Equal(ConsoleColor.DarkBlue, ColorScheme.BuiltIn[1].Background);
        Assert.All(ColorScheme.BuiltIn.Where(scheme => scheme.Name != "Deep Blue"),
            scheme => Assert.Null(scheme.Background));
        // The new colors are those of the dividers, and the tab is highlighted
        Assert.All(ColorScheme.BuiltIn, scheme =>
        {
            Assert.Equal(scheme.Dividers, scheme.TabFrameColor);
            Assert.Equal(scheme.Dividers, scheme.BoxBorderColor);
            Assert.True(scheme.TabHighlight);
        });
    }

    [Fact]
    public void AScheme_IsWrittenAndReadByName()
    {
        var scheme = ColorScheme.BuiltIn[2].Clone();
        scheme.Background = ConsoleColor.DarkBlue;
        scheme.TabFrames = ConsoleColor.Yellow;
        scheme.TabText = ConsoleColor.White;
        scheme.TabHighlight = false;
        var text = scheme.Serialize();
        Assert.Contains("text=Green\n", text);
        Assert.Contains("boxborders=default\n", text);
        Assert.Contains("tabhighlight=false\n", text);
        var read = ColorScheme.Parse(text, "Copy");
        Assert.Equal("Copy", read.Name);
        Assert.Equal(text.Replace("Marbles Quest", "Copy"), read.Serialize());
    }

    [Fact]
    public void WhatIsMissingOrWrong_HasTheDefaultColors()
    {
        var read = ColorScheme.Parse("; a comment\ntext = white\nlogo=Purple\nbackground=default\nnonsense\n", "X");
        Assert.Equal(ConsoleColor.White, read.Text);
        Assert.Equal(ColorScheme.BuiltIn[0].Logo, read.Logo);
        Assert.Null(read.Background);
        Assert.True(read.TabHighlight);
    }

    [Fact]
    public void TheDefaults_StandForOtherColors()
    {
        var scheme = ColorScheme.Default;
        Assert.Equal(ConsoleColor.DarkGray, scheme.TabFrameColor);
        Assert.Equal(ConsoleColor.DarkGray, scheme.TabColor);
        Assert.Equal(ConsoleColor.Black, scheme.TabTextColor);
        // The names of the tabs (the one shown without highlight, and the others) are the text
        Assert.Equal(ConsoleColor.Gray, scheme.TabShownColor);
        Assert.Equal(ConsoleColor.Gray, scheme.OtherTabsColor);
        scheme.Set(ColorItem.OtherTabs, ConsoleColor.DarkGray);
        Assert.Equal(ConsoleColor.DarkGray, scheme.OtherTabsColor);
        Assert.Contains("othertabs=DarkGray\n", scheme.Serialize());
        Assert.Contains("tabshown=default\n", scheme.Serialize());
        Assert.False(scheme.Allowed(ColorItem.TabShown, ConsoleColor.Black));
        scheme.TabFrames = ConsoleColor.Blue;
        Assert.Equal(ConsoleColor.Blue, scheme.TabColor);
        Assert.Equal(ConsoleColor.DarkGray, scheme.BoxBorderColor);
        // Only some colors can be "default"
        scheme.Set(ColorItem.Text, null);
        Assert.Equal(ConsoleColor.Gray, scheme.Text);
        scheme.Set(ColorItem.TabFrames, null);
        Assert.Null(scheme.TabFrames);
    }

    [Fact]
    public void TextCannotHaveTheColorOfItsBackground()
    {
        var scheme = ColorScheme.Default;
        // The background cannot have the color of the text on it
        Assert.False(scheme.Allowed(ColorItem.Background, ConsoleColor.Gray));
        Assert.False(scheme.Allowed(ColorItem.Background, ConsoleColor.Cyan));
        Assert.True(scheme.Allowed(ColorItem.Background, ConsoleColor.DarkBlue));
        // Nor the text the color of the background (black, without one of its own)
        Assert.False(scheme.Allowed(ColorItem.BoxBorders, ConsoleColor.Black));
        // The text and background of a highlight differ
        Assert.False(scheme.Allowed(ColorItem.HighlightText, ConsoleColor.Cyan));
        Assert.False(scheme.Allowed(ColorItem.Tab, ConsoleColor.Black));
        Assert.True(scheme.Allowed(ColorItem.Tab, ConsoleColor.Yellow));
    }

    [Fact]
    public void SchemesOfYourOwn_AreFiles()
    {
        Assert.Empty(ColorSchemes.CustomNames());
        var mine = ColorScheme.Default;
        mine.Name = "My scheme";
        mine.Logo = ConsoleColor.Red;
        Assert.Equal(DialogText.Success, ColorSchemes.Save(mine));
        Assert.True(File.Exists(Path.Combine(_directory, "My scheme.txt")));
        Assert.Equal(new[] { "My scheme" }, ColorSchemes.CustomNames());
        Assert.Equal(9, ColorSchemes.AllNames().Count);
        Assert.Equal(ConsoleColor.Red, ColorSchemes.Load("my scheme")!.Logo);
        Assert.True(ColorSchemes.Exists("My scheme"));
        Assert.Equal(DialogText.Success, ColorSchemes.Delete("My scheme"));
        Assert.Null(ColorSchemes.Load("My scheme"));
        // A built-in scheme by its name, a copy
        var deepBlue = ColorSchemes.Load("deep blue")!;
        deepBlue.Text = ConsoleColor.Red;
        Assert.Equal(ConsoleColor.Gray, ColorScheme.BuiltIn[1].Text);
    }

    [Theory]
    [InlineData("Mine", true)]
    [InlineData("  ", false)]
    [InlineData("Default", false)]
    [InlineData("ice warp", false)]
    [InlineData("a/b", false)]
    [InlineData("..", false)]
    public void TheNameOfASchemeOfYourOwn(string name, bool valid)
    {
        Assert.Equal(valid, ColorSchemes.IsValidName(name));
    }
}
