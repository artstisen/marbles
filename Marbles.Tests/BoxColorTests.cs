namespace Marbles.Tests;

/// <summary>
/// The colors of the boxes on top of a page: what they follow without colors of their own, which colors can be picked,
/// and that a box with a background keeps it to itself
/// </summary>
[Collection("Static state")]
public class BoxColorTests : IDisposable
{
    private readonly ColorScheme _saved = ConsoleColors.Current;

    public void Dispose()
    {
        ConsoleColors.Use(_saved);
    }

    private static ColorScheme Scheme() => ColorScheme.Parse(string.Empty, "Test");

    [Fact]
    public void WithoutColorsOfTheirOwn_TheBoxes_LookAsBefore()
    {
        var scheme = Scheme();
        scheme.Dividers = ConsoleColor.DarkGray;
        scheme.FrameText = ConsoleColor.Green;
        scheme.Tab = ConsoleColor.Blue;
        scheme.TabText = ConsoleColor.Yellow;
        scheme.Background = ConsoleColor.DarkBlue;
        Assert.Equal(ConsoleColor.DarkGray, scheme.BoxBorderColor);
        Assert.Equal(scheme.BoxBorderColor, scheme.BoxCornerColor);
        Assert.Equal(ConsoleColor.DarkBlue, scheme.BoxBackgroundColor);
        Assert.Equal(ConsoleColor.Green, scheme.BoxTextColor);
        Assert.Equal(ConsoleColor.Blue, scheme.BoxTitleColor);
        Assert.Equal(ConsoleColor.Yellow, scheme.BoxTitleTextColor);
    }

    [Fact]
    public void TheDefaultScheme_LeavesTheNewColorsOpen()
    {
        foreach (var item in new[]
                 {
                     ColorItem.BoxCorner, ColorItem.BoxBackground, ColorItem.BoxText, ColorItem.BoxTitle,
                     ColorItem.BoxTitleText
                 })
        {
            Assert.Null(ColorScheme.Default.Get(item));
        }
    }

    [Fact]
    public void TheColors_AreKeptInASchemeFile()
    {
        var scheme = Scheme();
        scheme.Set(ColorItem.BoxCorner, ConsoleColor.White);
        scheme.Set(ColorItem.BoxBackground, ConsoleColor.DarkBlue);
        scheme.Set(ColorItem.BoxText, ConsoleColor.Yellow);
        scheme.Set(ColorItem.BoxTitle, ConsoleColor.Cyan);
        scheme.Set(ColorItem.BoxTitleText, ConsoleColor.Black);
        var text = scheme.Serialize();
        Assert.Contains("boxcorner=White\n", text);
        Assert.Contains("boxbackground=DarkBlue\n", text);
        Assert.Contains("boxtext=Yellow\n", text);
        Assert.Contains("boxtitle=Cyan\n", text);
        Assert.Contains("boxtitletext=Black\n", text);
        var read = ColorScheme.Parse(text, "Read");
        Assert.Equal(ConsoleColor.DarkBlue, read.BoxBackground);
        Assert.Equal(ConsoleColor.Black, read.BoxTitleText);
        // A file written before there were these colors: they follow the others
        var older = ColorScheme.Parse("boxborders=White\n", "Older");
        Assert.Null(older.BoxBackground);
        Assert.Equal(ConsoleColor.White, older.BoxCornerColor);
    }

    [Fact]
    public void TextInABox_CannotHaveTheColorItIsOn()
    {
        var scheme = Scheme();
        scheme.Set(ColorItem.BoxText, ConsoleColor.Yellow);
        Assert.False(scheme.Allowed(ColorItem.BoxBackground, ConsoleColor.Yellow));
        Assert.True(scheme.Allowed(ColorItem.BoxBackground, ConsoleColor.DarkBlue));
        scheme.Set(ColorItem.BoxBackground, ConsoleColor.DarkBlue);
        Assert.False(scheme.Allowed(ColorItem.BoxText, ConsoleColor.DarkBlue));
        Assert.False(scheme.Allowed(ColorItem.BoxBorders, ConsoleColor.DarkBlue));
        Assert.False(scheme.Allowed(ColorItem.BoxCorner, ConsoleColor.DarkBlue));
        scheme.Set(ColorItem.BoxTitle, ConsoleColor.Cyan);
        Assert.False(scheme.Allowed(ColorItem.BoxTitleText, ConsoleColor.Cyan));
        Assert.True(scheme.Allowed(ColorItem.BoxTitleText, ConsoleColor.Black));
    }

    [Fact]
    public void WithTheHighlightOff_TheTitleIsOnTheBackgroundOfTheBox()
    {
        var scheme = Scheme();
        scheme.TabHighlight = false;
        scheme.Set(ColorItem.BoxBackground, ConsoleColor.DarkBlue);
        Assert.Equal(ConsoleColor.DarkBlue, scheme.BoxTitleColor);
        Assert.False(scheme.Allowed(ColorItem.BoxTitleText, ConsoleColor.DarkBlue));
        Assert.True(scheme.Allowed(ColorItem.BoxTitleText, ConsoleColor.Yellow));
        // A background of its own is used all the same
        scheme.Set(ColorItem.BoxTitle, ConsoleColor.Cyan);
        Assert.Equal(ConsoleColor.Cyan, scheme.BoxTitleColor);
    }

    [Fact]
    public void TheCornerOfTheBoxOfCommands_FollowsItsBorders_AndIsKept()
    {
        var scheme = Scheme();
        scheme.Set(ColorItem.CommandBorders, ConsoleColor.White);
        Assert.Null(scheme.CommandCorner);
        Assert.Equal(ConsoleColor.White, scheme.CommandCornerColor);
        Assert.True(ColorScheme.CanBeDefault(ColorItem.CommandCorner));
        scheme.Set(ColorItem.CommandBox, ConsoleColor.DarkBlue);
        Assert.False(scheme.Allowed(ColorItem.CommandCorner, ConsoleColor.DarkBlue));
        Assert.True(scheme.Allowed(ColorItem.CommandCorner, ConsoleColor.Yellow));
        scheme.Set(ColorItem.CommandCorner, ConsoleColor.Yellow);
        Assert.Contains("commandcorner=Yellow\n", scheme.Serialize());
        Assert.Equal(ConsoleColor.Yellow, ColorScheme.Parse(scheme.Serialize(), "Read").CommandCorner);
        // A file written before there was this color: it follows the borders
        Assert.Null(ColorScheme.Parse("commandborders=Cyan\n", "Older").CommandCorner);
        Assert.Equal(ConsoleColor.Cyan, ColorScheme.Parse("commandborders=Cyan\n", "Older").CommandCornerColor);
    }

    [Fact]
    public void AColorThatCannotBeSeenInABox_HasTheTextOfTheBox()
    {
        var scheme = Scheme();
        // No background of its own: colors stay as they are
        Assert.Equal(ConsoleColor.DarkGray, scheme.TextInBox(ConsoleColor.DarkGray));
        scheme.Set(ColorItem.BoxBackground, ConsoleColor.DarkGray);
        scheme.Set(ColorItem.BoxText, ConsoleColor.White);
        Assert.Equal(ConsoleColor.White, scheme.TextInBox(ConsoleColor.DarkGray));
        Assert.Equal(ConsoleColor.Yellow, scheme.TextInBox(ConsoleColor.Yellow));
    }

    [Fact]
    public void WhatIsHidden_DependsOnWhereTheTextIs()
    {
        var scheme = Scheme();
        scheme.Set(ColorItem.Background, ConsoleColor.Black);
        scheme.Set(ColorItem.BoxBackground, ConsoleColor.DarkBlue);
        ConsoleColors.Use(scheme);
        Assert.True(ConsoleColors.IsHidden(ConsoleColor.Black));
        Assert.False(ConsoleColors.IsHidden(ConsoleColor.DarkBlue));
        using (ConsoleColors.BoxScope())
        {
            Assert.False(ConsoleColors.IsHidden(ConsoleColor.Black));
            Assert.True(ConsoleColors.IsHidden(ConsoleColor.DarkBlue));
            using (ConsoleColors.OutsideBox())
            {
                // The bottom line, the command line, a page opened from the box
                Assert.True(ConsoleColors.IsHidden(ConsoleColor.Black));
            }

            Assert.True(ConsoleColors.IsHidden(ConsoleColor.DarkBlue));
        }

        Assert.True(ConsoleColors.IsHidden(ConsoleColor.Black));
        // A logo is drawn on the page, also when its color is picked in a box
        using (ConsoleColors.BoxScope()) Assert.True(ConsoleColors.IsBackgroundColor(ConsoleColor.Black));
    }
}
