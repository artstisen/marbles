namespace Marbles.Tests;

/// <summary>
/// The frames of the editor (EditorFrame): what the standard frame and the frame of Zen mode make of the same facts,
/// tested without a console
/// </summary>
// (the labels are in the language set, which other tests change)
[Collection("Static state")]
public class EditorFrameTests
{
    private static EditorFacts Facts(EditorMode mode = EditorMode.Normal, string message = "", bool error = false) =>
        new()
        {
            Kind = EditorKind.File,
            Top = 2,
            Mode = mode,
            Message = message,
            MessageIsError = error,
            Line = 312,
            Column = 17,
            Percent = 40,
            Title = "test1.md"
        };

    [Fact]
    public void TheFrames_TakeTheirRowsAboveTheText()
    {
        // The caption and the divider; one row in Zen mode, so the text has a row more
        Assert.Equal(2, new StandardFrame().RowsAbove);
        Assert.Equal(1, new ZenFrame().RowsAbove);
        Assert.Equal("standard", new StandardFrame().Name);
        Assert.Equal("zen", new ZenFrame().Name);
    }

    [Fact]
    public void ZenMode_ShowsTheModeAsALetter()
    {
        Assert.Equal("[N]", ZenFrame.ModeTag(Facts()));
        Assert.Equal("[I]", ZenFrame.ModeTag(Facts(EditorMode.Insert)));
        Assert.Equal("[V]", ZenFrame.ModeTag(Facts(EditorMode.Visual)));
        Assert.Equal("[V]", ZenFrame.ModeTag(Facts(EditorMode.VisualLine)));
        Assert.Equal("[V]", ZenFrame.ModeTag(Facts(EditorMode.VisualBlock)));
        Assert.Equal("[C]", ZenFrame.ModeTag(Facts(EditorMode.Command)));
        Assert.Equal("[C]", ZenFrame.ModeTag(Facts(EditorMode.Search)));
        Assert.Equal("[J]", ZenFrame.ModeTag(Facts() with { Jumping = true }));
    }

    [Fact]
    public void ZenMode_ShowsAStarForChanges()
    {
        Assert.Equal("test1.md", ZenFrame.NameOf(Facts()));
        Assert.Equal("test1.md*", ZenFrame.NameOf(Facts() with { Modified = true }));
        // A long name is cut to the width of the pane, with a column left free
        var fitted = ZenFrame.Fit("a-very-long-name-of-a-file.md", 12);
        Assert.True(TextMetrics.WidthOf(fitted) <= 11);
    }

    [Fact]
    public void ZenMode_ShowsTheLineAndColumnInTheMiddle()
    {
        var parts = ZenFrame.Status(Facts(), 78);
        Assert.Equal("312:17", parts.Position);
        Assert.Equal(string.Empty, parts.Message);
        // Centered: as much blank space before as after (one more after when it cannot be even)
        Assert.Equal((78 - 6) / 2, parts.Before.Length);
        Assert.Equal(78, Line(parts).Length);
        Assert.Equal(string.Empty, parts.Right);
    }

    [Fact]
    public void ZenMode_ShowsAMessageAtTheLeft()
    {
        var parts = ZenFrame.Status(Facts(message: "Saved"), 78);
        Assert.Equal("Saved", parts.Message);
        Assert.Equal("312:17", parts.Position);
        Assert.Equal(78, Line(parts).Length);
        Assert.Equal((78 - 6) / 2, Line(parts).IndexOf("312:17", StringComparison.Ordinal));
        // An error is highlighted, with a blank space on each side inside the highlight
        Assert.Equal(" Not found ", ZenFrame.Status(Facts(message: "Not found", error: true), 78).Message);
    }

    [Fact]
    public void ZenMode_ALongMessageTakesThePlaceOfTheLineAndColumn()
    {
        var message = new string('m', 50);
        var parts = ZenFrame.Status(Facts(message: message), 78);
        Assert.Equal(message, parts.Message);
        Assert.Equal(string.Empty, parts.Position);
        Assert.Equal(78, Line(parts).Length);
        // Longer than the line: cut
        parts = ZenFrame.Status(Facts(message: new string('m', 100)), 78);
        Assert.Equal(78, parts.Message.Length);
        Assert.Equal(78, Line(parts).Length);
    }

    [Fact]
    public void ZenMode_ShowsRecAndTheSearchCountAtTheRight()
    {
        var parts = ZenFrame.Status(Facts() with { Recording = true, SearchCount = "[2/12]" }, 78);
        Assert.Equal("REC | [2/12] ", parts.Right);
        Assert.Equal("312:17", parts.Position);
        Assert.EndsWith("REC | [2/12] ", Line(parts));
        Assert.Equal(78, Line(parts).Length);
        // Nothing else of the standard status line
        var all = Facts() with
        {
            PendingKeys = "d", LineMemory = "MEM 1/2", Spell = "SPELL en", FileLabel = "MD UTF-8 CRLF"
        };
        Assert.Equal(string.Empty, ZenFrame.Status(all, 78).Right);
        Assert.DoesNotContain("MD", Line(ZenFrame.Status(all, 78)));
        // No room at the right in a narrow window: left out
        Assert.Equal(string.Empty, ZenFrame.Status(Facts() with { Recording = true, SearchCount = "[2/12]" }, 20).Right);
    }

    [Fact]
    public void TheStandardFrame_ShowsTheModeAndAllAtTheRight()
    {
        var facts = Facts() with { SearchCount = "[2/12]", FileLabel = "MD UTF-8 CRLF", Recording = true };
        var parts = StandardFrame.Status(facts, 78);
        Assert.Equal(" " + DialogText.EditorModeNormal + " ", parts.Label);
        Assert.Equal(" REC | [2/12] | MD UTF-8 CRLF | 312:17 | 40% ", parts.Right);
        Assert.Equal(78, (parts.Label + parts.Gap + parts.Text + parts.Filler + parts.Right).Length);
        // A message that does not fit with all of it keeps only the line and column
        parts = StandardFrame.Status(facts with { Message = new string('m', 50) }, 78);
        Assert.Equal(" 312:17 ", parts.Right);
        Assert.Equal(" " + DialogText.EditorModeInsert + " ", StandardFrame.Status(Facts(EditorMode.Insert), 78).Label);
        Assert.Equal(DialogText.DividerNavigate, StandardFrame.ModeLabel(Facts()));
        Assert.Equal(DialogText.DividerJump, StandardFrame.ModeLabel(Facts() with { Jumping = true }));
    }

    [Fact]
    public void TheCommandLine_FillsTheWidth()
    {
        Assert.Equal("COMMAND:spl" + new string(' ', 9), EditorFrame.CommandLineText("COMMAND:spl", 20));
        // Too long: cut so the cursor fits after it
        var line = EditorFrame.CommandLineText("COMMAND:" + new string('x', 30), 20);
        Assert.Equal(20, line.Length);
        Assert.EndsWith(" ", line);
    }

    [Fact]
    public void ZenMode_NamesANote_ByItsTitle()
    {
        // A note of a data file has a title, not a file name: the facts carry it as the name
        var note = Facts() with { Kind = EditorKind.Note, Title = "Shopping list", Modified = true };
        Assert.Equal("Shopping list*", ZenFrame.NameOf(note));
    }

    private static string Line(ZenFrame.StatusParts parts) =>
        parts.Message + parts.Before + parts.Position + parts.After + parts.Right;
}
