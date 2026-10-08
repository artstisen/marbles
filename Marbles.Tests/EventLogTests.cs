namespace Marbles.Tests;

[Collection("Static state")]
public class EventLogTests
{
    public EventLogTests()
    {
        EventLog.Clear();
    }

    [Fact]
    public void Lines_HaveTheTime_AndAreKeptOnOneLine()
    {
        EventLog.Add("one\ntwo");
        Assert.Single(EventLog.All);
        Assert.Matches(@"^\[\d\d:\d\d:\d\d\] one two$", EventLog.All[0]);
    }

    [Fact]
    public void TheLog_HasALimit()
    {
        for (var i = 0; i < EventLog.MaxLines + 250; i++)
        {
            EventLog.Add("line " + i);
        }

        Assert.Equal(EventLog.MaxLines, EventLog.Count);
        // The oldest lines are removed
        Assert.EndsWith(" line 250", EventLog.All[0]);
        Assert.EndsWith(" line " + (EventLog.MaxLines + 249), EventLog.All[^1]);
        // Long lines are cut
        EventLog.Add(new string('x', 5000));
        Assert.True(EventLog.All[^1].Length < 500);
    }

    [Fact]
    public void Filter_FindsLinesContainingAText()
    {
        EventLog.Add("File: Rename: a.txt -> b.txt");
        EventLog.Add("Login OK: data.mbls");
        EventLog.Add("File: Delete: c.txt - FAILED: Access denied");
        Assert.Equal(3, EventLog.Filter("").Count);
        Assert.Equal(2, EventLog.Filter("file:").Count);
        Assert.Single(EventLog.Filter("FAILED"));
        Assert.Empty(EventLog.Filter("nothing"));
    }

    [Fact]
    public void LongLines_ContinueOnTheNextRows()
    {
        var rows = EventLog.Wrap(new List<string> { "[12:34:56] short", "[12:34:56] " + new string('a', 60) }, 40);
        Assert.Equal(new[]
        {
            "[12:34:56] short",
            "[12:34:56] " + new string('a', 29),
            new string(' ', 11) + new string('a', 29),
            new string(' ', 11) + new string('a', 2)
        }, rows);
    }

    [Theory]
    [InlineData("w", "w")]
    [InlineData("wq!", "wq!")]
    [InlineData("spell da,en", "spell ...")]
    [InlineData("new My secret title", "new ...")]
    [InlineData("NEW Title", "new ...")]
    [InlineData("s/secret/other/g", "s ...")]
    [InlineData("%s/secret/other/g", "s ...")]
    [InlineData("'<,'>s/secret/x/", "s ...")]
    [InlineData("2,5d", "d")]
    [InlineData("12", "(number)")]
    [InlineData("pin 3", "pin ...")]
    [InlineData("mypassword123", "(not a command)")]
    [InlineData("secret words here", "(not a command)")]
    [InlineData("  ", "")]
    public void Commands_AreLoggedByNameOnly(string command, string expected)
    {
        Assert.Equal(expected, EventLog.CommandName(command));
    }

    [Fact]
    public void EditorCommands_AreLogged_WithoutTheTextOfTheDocument()
    {
        var editor = new EditorEngine("a secret text", 40, 10, false);
        editor.Type(":s/secret/hidden/<CR>");
        editor.Type(":trim<CR>");
        editor.Type("/secret<CR>");
        Assert.Equal(2, EventLog.Count);
        Assert.EndsWith(" Editor: :s ...", EventLog.All[0]);
        Assert.EndsWith(" Editor: :trim", EventLog.All[1]);
        Assert.DoesNotContain(EventLog.All, line => line.Contains("secret") || line.Contains("hidden"));
    }
}
