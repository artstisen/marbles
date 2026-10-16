using System.Text.RegularExpressions;

namespace Marbles.Tests;

public class CommandTests
{
    #region Completion

    // Press Tab a number of times and show what the command line holds after each
    private static string Tabs(CommandContext context, string typed, int times, bool forward = true)
    {
        var completion = new CommandCompletion(context);
        var shown = new List<string>();
        var text = typed;
        for (var i = 0; i < times; i++)
        {
            text = completion.Next(text, forward);
            shown.Add(text);
        }

        return string.Join(" ", shown);
    }

    [Fact]
    public void Tab_CompletesTheCommand_AndGoesThroughTheOnesThatStartTheSame()
    {
        // After the last of them, what was typed is shown again
        Assert.Equal("collapse color co collapse", Tabs(CommandContext.Favorites, "co", 4));
        Assert.Equal("expand ex expand", Tabs(CommandContext.Favorites, "ex", 3));
        // Shift+Tab goes the other way
        Assert.Equal("color collapse co color", Tabs(CommandContext.Favorites, "co", 4, false));
        // Each place has its own commands
        Assert.Equal("scheme sc", Tabs(CommandContext.Menu, "sc", 2));
        Assert.Equal("copy co", Tabs(CommandContext.Files, "co", 2));
        // Capital letters are completed too
        Assert.Equal("undelete", Tabs(CommandContext.Menu, "UND", 1));
    }

    [Fact]
    public void Tab_WithNothingTyped_GoesThroughAllTheCommands()
    {
        var names = Commands.Names(CommandContext.Search);
        Assert.Equal(new[] { "book", "close", "favorite", "find", "goto", "help", "info", "jump", "quit" }, names);
        Assert.Equal(string.Join(" ", names) + "  book", Tabs(CommandContext.Search, "", names.Count + 2));
    }

    [Fact]
    public void Tab_LeavesTheTextAlone_WhenNothingStartsWithIt()
    {
        Assert.Equal("xyz xyz", Tabs(CommandContext.Menu, "xyz", 2));
        // A command that is complete, and has no longer one
        Assert.Equal("undelete", Tabs(CommandContext.Menu, "undelete", 1));
        // Only the name is completed, not what follows it
        Assert.Equal("new he", Tabs(CommandContext.Menu, "new he", 1));
        Assert.Equal("syntax h", Tabs(CommandContext.Editor, "syntax h", 1));
    }

    [Fact]
    public void Tab_StartsOver_WhenTheTextIsChanged()
    {
        var completion = new CommandCompletion(CommandContext.Favorites);
        Assert.Equal("collapse", completion.Next("co", true));
        // A letter is removed and another typed
        Assert.Equal("collapse", completion.Next("col", true));
        Assert.Equal("remove", completion.Next("r", true));
        Assert.Equal("rename", completion.Next("remove", true));
        Assert.Equal("r", completion.Next("rename", true));
        // The same text typed again after the command line was opened anew
        completion.Reset();
        Assert.Equal("remove", completion.Next("r", true));
    }

    [Fact]
    public void Tab_InTheEditor_KeepsTheLinesInFrontOfTheCommand()
    {
        Assert.Equal("%delete %diff %dup %duplicate %d", Tabs(CommandContext.Editor, "%d", 5));
        Assert.Equal("'<,'>yank", Tabs(CommandContext.Editor, "'<,'>y", 1));
        Assert.Equal("2,5replace", Tabs(CommandContext.Editor, "2,5rep", 1));
        // "quit" is a command, and there is one more that starts with it
        Assert.Equal("quit! quit", Tabs(CommandContext.Editor, "quit", 2));
    }

    [Fact]
    public void Tab_OnTheCommandLineOfTheEditor()
    {
        var editor = new EditorEngine("one\ntwo", 40, 10, false);
        editor.Type(":spelln<Tab>");
        Assert.Equal("spellnext", editor.CommandText);
        editor.Type("<Esc>:nu<Tab>");
        Assert.Equal("number", editor.CommandText);
        editor.Type("<Tab>");
        Assert.Equal("nu", editor.CommandText);
        // A search text is not completed
        editor.Type("<Esc>/nu<Tab>");
        Assert.Equal("nu", editor.CommandText);
        // The completed command is run
        editor.Type("<Esc>:2<CR>:del<Tab><CR>");
        Assert.Equal("one", editor.Buffer.GetText());
        editor.Type(":y<Tab><CR>p");
        Assert.Equal("one\none", editor.Buffer.GetText());
    }

    #endregion

    #region The tables and the code that runs the commands

    private static string Source(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Marbles.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, "Marbles", file)).Replace("\r\n", "\n");
    }

    // The text from a line of code to the first line after it that ends another
    private static string Part(string file, string from, string to)
    {
        var source = Source(file);
        var start = source.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, file + ": not found: " + from);
        var end = source.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, file + ": not found after \"" + from + "\": " + to);
        return source[start..end];
    }

    private static string Method(string file, string name)
    {
        return Part(file, " " + name + "(string ", "\n    }\n");
    }

    // The commands compared with in a piece of code: case "name":, is "name" or "other", == "name" ...
    private static SortedSet<string> Compared(params string[] code)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in string.Join("\n", code).Split('\n'))
        {
            if (!Regex.IsMatch(line, "(case |is |is not \\(|== |!= |or |StartsWith\\()\"")) continue;
            foreach (Match match in Regex.Matches(line, "\"([a-z]+!?)[ \"]"))
            {
                names.Add(match.Groups[1].Value);
            }
        }

        return names;
    }

    private static void AssertSame(CommandContext context, SortedSet<string> inCode, params string[] notCompared)
    {
        var table = new SortedSet<string>(Commands.All(context), StringComparer.Ordinal);
        inCode.UnionWith(notCompared);
        Assert.True(table.SetEquals(inCode),
            context + ": the table of commands (Commands.cs) and the code that runs them differ. Only in the table: " +
            string.Join(" ", table.Except(inCode)) + ". Only in the code: " + string.Join(" ", inCode.Except(table)));
    }

    [Fact]
    public void TheTables_HaveTheCommandsOfTheMenusAndLists()
    {
        var common = Method("Actions.cs", "RunCommonCommand");
        AssertSame(CommandContext.Menu, Compared(common, Method("Actions.cs", "RunManageCommand")));
        AssertSame(CommandContext.List, Compared(common, Method("Actions.cs", "RunListCommand")));
    }

    [Fact]
    public void TheTables_HaveTheCommandsOfTheFileBrowser()
    {
        const string read = "var command = CommandLine.Read(";
        const string help = "else if (key.KeyChar == '?')";
        var browser = Part("FileExplorer.cs", read, help);
        AssertSame(CommandContext.Files, Compared(browser, Method("FileExplorer.Manage.cs", "RunFileCommand")));
        AssertSame(CommandContext.Favorites, Compared(Part("FileExplorer.Favorites.cs", read, help)));
        AssertSame(CommandContext.History, Compared(Part("FileExplorer.History.cs", read, help)));
        AssertSame(CommandContext.Hotlist, Compared(Part("FileExplorer.Hotlist.cs", read, help)));
        AssertSame(CommandContext.Search, Compared(Part("FileExplorer.Search.cs", read, help)));
        // When a file or directory is picked, only the commands for getting around are run
        var picker = Commands.All(CommandContext.FilePicker);
        Assert.Subset(Compared(browser), new SortedSet<string>(picker));
        Assert.DoesNotContain(picker, name => Compared(Method("FileExplorer.Manage.cs", "RunFileCommand")).Contains(name));
    }

    [Fact]
    public void TheTables_HaveTheCommandsOfTheStartPage()
    {
        AssertSame(CommandContext.Start, Compared(Method("StartMenu.cs", "RunCommand")));
    }

    [Fact]
    public void TheTables_HaveTheCommandsOfTheEditor()
    {
        // (":s/old/new/" is found by its first letter, and ":diff" and ":book" by a pattern, as they can have something
        // after them)
        AssertSame(CommandContext.Editor, Compared(Method("Editor/EditorEngine.cs", "ExecuteCommand")), "s", "diff",
            "book");
    }

    [Fact]
    public void TheCommandsCompletedTo_AreWholeWords()
    {
        foreach (var context in Enum.GetValues<CommandContext>())
        {
            var names = Commands.Names(context);
            Assert.Equal(names.OrderBy(name => name, StringComparer.Ordinal), names);
            Assert.All(names, name => Assert.Matches("^[a-z]{2,}!?$", name));
            Assert.All(names, name => Assert.True(Commands.IsKnown(context, name)));
            Assert.All(Commands.All(context), name => Assert.True(Commands.IsKnown(name)));
        }

        Assert.False(Commands.IsKnown("mypassword"));
        Assert.False(Commands.IsKnown(CommandContext.Menu, "collapse"));
    }

    #endregion
}
