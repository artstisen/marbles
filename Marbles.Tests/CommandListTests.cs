namespace Marbles.Tests;

/// <summary>
/// The list of commands above the command line: the commands that start with what is typed (also after the lines a
/// command works on), what can follow a command, the names laid out in columns, and what a command does, from the
/// help
/// </summary>
[Collection("Static state")]
public class CommandListTests : IDisposable
{
    public void Dispose()
    {
        CommandCompletion.Arguments.Remove("sur");
        Completions.Parse(string.Empty);
    }

    [Fact]
    public void WithNothingTyped_AllTheCommandsOfThePlaceAreListed_WithoutTheShortForms()
    {
        var (names, typed, column) = CommandList.Matches(CommandContext.Start, string.Empty);
        Assert.Equal(Commands.Names(CommandContext.Start), names);
        Assert.Equal(string.Empty, typed);
        Assert.Equal(Commands.Names(CommandContext.Start).Max(name => name.Length) + 2, column);
        Assert.DoesNotContain("q", names);
    }

    [Theory]
    [InlineData("du", "dup duplicate")]
    [InlineData("'<,'>du", "dup duplicate")]
    [InlineData("%sor", "sort sort!")]
    [InlineData("xyz", "")]
    public void TheCommandsThatStartWithWhatIsTyped_AreListed(string text, string expected)
    {
        var (names, _, _) = CommandList.Matches(CommandContext.Editor, text);
        Assert.Equal(expected, string.Join(" ", names));
    }

    [Fact]
    public void AfterASpace_WhatCanFollowTheCommandIsListed_WhereThereIsAListOfIt()
    {
        Completions.Parse("=== Markdown (md)\n/bold = **$0**\n/bolditalic = ***$0***\n/link = [$0]()\n");
        CommandCompletion.Arguments["sur"] = () => Completions.SurroundWords(SyntaxType.Markdown);
        var (names, typed, _) = CommandList.Matches(CommandContext.Editor, "'<,'>sur /bo");
        Assert.Equal(new[] { "/bold", "/bolditalic" }, names);
        Assert.Equal("/bo", typed);
        // A command of another place, or one without a list of what follows it, lists nothing
        Assert.Empty(CommandList.Matches(CommandContext.Start, "sur /b").Names);
        Assert.Empty(CommandList.Matches(CommandContext.Editor, "dup 5").Names);
    }

    [Fact]
    public void EveryPlaceWithACommandLine_HasItsCommandsListed_AndAHelpRowForEach()
    {
        foreach (var context in Enum.GetValues<CommandContext>())
        {
            var (names, _, _) = CommandList.Matches(context, string.Empty);
            Assert.NotEmpty(names);
        }

        // The main menu and the lists of entries (the notes) too
        Assert.Contains("logout", CommandList.Matches(CommandContext.Menu, "lo").Names);
        Assert.Contains("title", CommandList.Matches(CommandContext.List, "ti").Names);
        Assert.NotNull(Tools.CommandDescription(Tools.HelpTopic.Menus, "title"));
    }

    [Fact]
    public void TheNames_AreLaidOutInRows_AndWhatDoesNotFitIsCounted()
    {
        var names = new[] { "a", "b", "c", "d", "e", "f", "g" };
        // Columns of 10 cells in 30: three in a row
        Assert.Equal(new[] { "a b c", "d" }, CommandList.Layout(names[..4], 10, 30, 2).Select(row => string.Join(" ", row)));
        var full = CommandList.Layout(names, 10, 30, 2);
        Assert.Equal("a b c", string.Join(" ", full[0]));
        Assert.Equal(new[] { "d", "e", Lang.F("+{0} more", 2) }, full[1]);
        // At least one column, however narrow
        Assert.Equal(2, CommandList.Layout(names[..2], 50, 30, 5).Count);
    }

    [Fact]
    public void WhatACommandDoes_IsTakenFromItsRowInTheHelp()
    {
        Assert.Equal(Lang.T("Duplicate the selection (or the line)"),
            Tools.CommandDescription(Tools.HelpTopic.Editor, "dup"));
        Assert.Equal(DialogText.ConfigReset, Tools.CommandDescription(Tools.HelpTopic.Start, "reset"));
        // The same command in two places: the row of the place
        Assert.Equal(Lang.T("Duplicate the selected entry"), Tools.CommandDescription(Tools.HelpTopic.Menus, "duplicate"));
        // One of the commands of a row with several keys ("u :clearbook")
        Assert.NotNull(Tools.CommandDescription(Tools.HelpTopic.Jump, "clearbook"));
        Assert.Null(Tools.CommandDescription(Tools.HelpTopic.Editor, "nothing"));
    }
}
