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
        // The default scheme: gray box borders, the focused tab cyan (not highlighted), the other tabs dark gray
        var standard = ColorScheme.BuiltIn[0];
        Assert.Equal(ConsoleColor.Gray, standard.BoxBorderColor);
        Assert.Equal(ConsoleColor.DarkGray, standard.TabFrameColor);
        Assert.Equal(ConsoleColor.DarkGray, standard.TabColor);
        Assert.Equal(ConsoleColor.Cyan, standard.TabShownColor);
        Assert.Equal(ConsoleColor.DarkGray, standard.OtherTabsColor);
        Assert.False(standard.TabHighlight);
        // In the others the new colors are those of the dividers, and the tab is highlighted
        Assert.All(ColorScheme.BuiltIn.Skip(1), scheme =>
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
        // As the default scheme: no highlight of the focused tab
        Assert.False(read.TabHighlight);
    }

    [Fact]
    public void TheDefaults_StandForOtherColors()
    {
        var scheme = ColorScheme.BuiltIn[2].Clone();
        Assert.Equal(ConsoleColor.Blue, scheme.TabFrameColor);
        Assert.Equal(ConsoleColor.Blue, scheme.TabColor);
        // The text of the highlighted tab is the color of the focused tab
        Assert.Equal(ConsoleColor.Green, scheme.TabTextColor);
        // The names of the tabs (the one shown without highlight, and the others) are the text
        Assert.Equal(ConsoleColor.Green, scheme.TabShownColor);
        Assert.Equal(ConsoleColor.Green, scheme.OtherTabsColor);
        scheme.Set(ColorItem.OtherTabs, ConsoleColor.DarkGray);
        Assert.Equal(ConsoleColor.DarkGray, scheme.OtherTabsColor);
        Assert.Contains("othertabs=DarkGray\n", scheme.Serialize());
        Assert.Contains("tabshown=default\n", scheme.Serialize());
        Assert.False(scheme.Allowed(ColorItem.TabShown, ConsoleColor.Black));
        // The label "Unsaved": the text inverted
        Assert.Equal(scheme.Text, scheme.UnsavedColor);
        Assert.Equal(ConsoleColor.Black, scheme.UnsavedTextColor);
        Assert.False(scheme.Allowed(ColorItem.UnsavedText, scheme.Text));
        scheme.TabFrames = ConsoleColor.Red;
        Assert.Equal(ConsoleColor.Red, scheme.TabColor);
        Assert.Equal(ConsoleColor.Blue, scheme.BoxBorderColor);
        // Only some colors can be "default"
        scheme.Set(ColorItem.Text, null);
        Assert.Equal(ConsoleColor.Green, scheme.Text);
        scheme.Set(ColorItem.TabFrames, null);
        Assert.Null(scheme.TabFrames);
    }

    [Fact]
    public void TheColorsOfColoredText_HaveTheColorsTheyHadBefore_UntilTheyAreSet()
    {
        var scheme = ColorScheme.BuiltIn[2].Clone();
        // 1 keywords: the logo, 2 names: the accent, 3 strings: the favorites, 4 numbers, 5 links: the editor
        // title, 6 comments: the dividers, 7 to 12 the headings
        Assert.Equal(scheme.Logo, scheme.SyntaxColor(0));
        Assert.Equal(scheme.Accent, scheme.SyntaxColor(1));
        Assert.Equal(scheme.Favorites, scheme.SyntaxColor(2));
        Assert.Equal(ConsoleColor.Magenta, scheme.SyntaxColor(3));
        Assert.Equal(scheme.EditorTitle, scheme.SyntaxColor(4));
        Assert.Equal(scheme.Dividers, scheme.SyntaxColor(5));
        Assert.Equal(ConsoleColor.Red, scheme.SyntaxColor(6));
        Assert.Equal(ConsoleColor.Blue, scheme.SyntaxColor(11));
        scheme.Set(ColorItem.Syntax2, ConsoleColor.White);
        Assert.Equal(ConsoleColor.White, scheme.SyntaxColor(1));
        Assert.Contains("syntax2=White\n", scheme.Serialize());
        Assert.Contains("syntax12=default\n", scheme.Serialize());
        Assert.Equal(ConsoleColor.White, ColorScheme.Parse(scheme.Serialize(), "X").SyntaxColor(1));
        // A copy has colors of its own
        var copy = scheme.Clone();
        copy.Set(ColorItem.Syntax2, ConsoleColor.Red);
        Assert.Equal(ConsoleColor.White, scheme.SyntaxColor(1));
    }

    [Fact]
    public void LineNumbersAndTheSelector_HaveColorsOfTheirOwn()
    {
        var scheme = ColorScheme.BuiltIn[2].Clone();
        Assert.Equal(scheme.Dividers, scheme.LineNumbersColor);
        Assert.Equal(scheme.Text, scheme.CurrentLineNumberColor);
        Assert.Equal(scheme.Highlight, scheme.SelectorColor);
        Assert.Equal(scheme.HighlightText, scheme.SelectorTextColor);
        scheme.Set(ColorItem.Selector, ConsoleColor.Yellow);
        scheme.Set(ColorItem.LineNumbers, ConsoleColor.Gray);
        Assert.Equal(ConsoleColor.Yellow, scheme.SelectorColor);
        Assert.False(scheme.Allowed(ColorItem.SelectorText, ConsoleColor.Yellow));
        var read = ColorScheme.Parse(scheme.Serialize(), "X");
        Assert.Equal(ConsoleColor.Yellow, read.Selector);
        Assert.Equal(ConsoleColor.Gray, read.LineNumbers);
        Assert.Null(read.CurrentLineNumber);
    }

    [Fact]
    public void TheAccent_IsAlsoReadByTheNameItHadBefore()
    {
        Assert.Equal(ConsoleColor.Red, ColorScheme.Parse("previewtitle=Red\n", "X").Accent);
        Assert.Equal(ConsoleColor.Blue, ColorScheme.Parse("accent=Blue\n", "X").Accent);
        Assert.Contains("accent=", ColorScheme.Default.Serialize());
        Assert.DoesNotContain("previewtitle", ColorScheme.Default.Serialize());
    }

    [Theory]
    // The kinds of colored text share the colors Syntax 1 to 12 (counted from 0)
    [InlineData("Keyword", 0)]
    [InlineData("Bold", 0)]
    [InlineData("Name", 1)]
    [InlineData("Italic", 1)]
    [InlineData("String", 2)]
    [InlineData("CodeBlock", 2)]
    [InlineData("Number", 3)]
    [InlineData("Link", 4)]
    [InlineData("Comment", 5)]
    [InlineData("Quote", 5)]
    [InlineData("LinkUrl", 5)]
    [InlineData("Heading1", 6)]
    [InlineData("Heading6", 11)]
    public void ColoredText_UsesTheSharedColors(string style, int expected)
    {
        Assert.Equal(expected, TextArea.SyntaxColorOf(Enum.Parse<SyntaxStyle>(style)));
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
        // (the text of the highlighted tab is the color of the focused tab: cyan)
        Assert.False(scheme.Allowed(ColorItem.Tab, ConsoleColor.Cyan));
        Assert.True(scheme.Allowed(ColorItem.Tab, ConsoleColor.Yellow));
        Assert.False(scheme.Allowed(ColorItem.TabShown, ConsoleColor.DarkGray));
        scheme.TabText = ConsoleColor.White;
        Assert.True(scheme.Allowed(ColorItem.TabShown, ConsoleColor.DarkGray));
    }

    [Fact]
    public void ASchemeOfYourOwn_CanBeRenamed()
    {
        var first = ColorScheme.Default;
        first.Name = "First";
        first.Logo = ConsoleColor.Red;
        var second = ColorScheme.Default;
        second.Name = "Second";
        Assert.Equal(DialogText.Success, ColorSchemes.Save(first));
        Assert.Equal(DialogText.Success, ColorSchemes.Save(second));
        Assert.Equal(DialogText.Success, ColorSchemes.Rename("first", " Third "));
        Assert.Equal(new[] { "Second", "Third" }, ColorSchemes.CustomNames());
        Assert.Equal(ConsoleColor.Red, ColorSchemes.Load("Third")!.Logo);
        Assert.Equal("Third", ColorSchemes.Load("Third")!.Name);
        // Not to the name of another scheme, a built-in name or a name that cannot be used
        Assert.Equal(DialogText.SchemeNameTaken, ColorSchemes.Rename("Third", "second"));
        Assert.Equal(DialogText.SchemeNameBuiltIn, ColorSchemes.Rename("Third", ColorScheme.BuiltIn[1].Name));
        Assert.Equal(DialogText.SchemeNameInvalid, ColorSchemes.Rename("Third", "a/b"));
        Assert.Equal(DialogText.SchemeNameInvalid, ColorSchemes.Rename("Missing", "Fourth"));
        Assert.NotEqual(DialogText.Success, ColorSchemes.Rename(ColorScheme.BuiltIn[1].Name, "Mine"));
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
