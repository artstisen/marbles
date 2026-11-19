namespace Marbles.Tests;

/// <summary>
/// ":changes": the steps of the undo history told as changes (ChangeHistory), newest first
/// </summary>
// (the edits yank to the string memory, which other tests check)
[Collection("Static state")]
public class ChangeHistoryTests
{
    private static EditorEngine Editor(string text) => new(text, 40, 10, false);

    private static List<ChangeHistory.Change> Changes(EditorEngine editor) =>
        ChangeHistory.Of(editor.Buffer.History());

    [Fact]
    public void TypedText_IsOneInsertion()
    {
        var editor = Editor("one\ntwo");
        editor.Type("jAHello there<Esc>");
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Inserted, change.Kind);
        Assert.Equal("Hello there", change.Inserted);
        Assert.Equal(new TextPos(1, 3), change.Place);
        Assert.False(change.Undone);
    }

    [Fact]
    public void ALineDeleted_IsADeletion()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.Type("jdd");
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Deleted, change.Kind);
        Assert.Contains("two", change.Removed);
    }

    [Fact]
    public void AWordChanged_IsAReplacement()
    {
        var editor = Editor("the colour red");
        editor.Type("wcwcolor<Esc>");
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Replaced, change.Kind);
        Assert.Equal("colour", change.Removed);
        Assert.Equal("color", change.Inserted);
        Assert.Equal(new TextPos(0, 4), change.Place);
    }

    [Fact]
    public void Backspace_JoinsTheTextBeforeTheTyping()
    {
        // "word": the last two letters deleted with Backspace, and "s" typed: "rd" replaced with "s"
        var editor = Editor("word");
        editor.Type("A<BS><BS>s<Esc>");
        Assert.Equal("wos", editor.GetText());
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Replaced, change.Kind);
        Assert.Equal("rd", change.Removed);
        Assert.Equal("s", change.Inserted);
        Assert.Equal(new TextPos(0, 2), change.Place);
    }

    [Fact]
    public void TextTypedAndDeletedAgain_ChangesNothing()
    {
        var editor = Editor("word");
        editor.Type("Aab<BS><BS><Esc>");
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.None, change.Kind);
    }

    [Fact]
    public void TheNewestComesFirst_AndThePlacesFollowTheLaterChanges()
    {
        var editor = Editor("a\nb\nc");
        editor.Type("GAx<Esc>");
        // A line above it: the place of the first change moves down
        editor.Type("ggOnew<Esc>");
        var changes = Changes(editor);
        Assert.Equal(2, changes.Count);
        Assert.Equal("new", changes[0].Inserted.Trim());
        Assert.Equal("x", changes[1].Inserted);
        Assert.Equal(new TextPos(3, 1), changes[1].Place);
        Assert.Equal("a", editor.Buffer[1]);
        Assert.Equal("cx", editor.Buffer[3]);
    }

    [Fact]
    public void AnUndoneStep_IsShownAsUndone_AndDoesNotMoveTheOthers()
    {
        var editor = Editor("a\nb\nc");
        editor.Type("GAx<Esc>");
        editor.Type("ggOnew<Esc>u");
        var changes = Changes(editor);
        Assert.Equal(2, changes.Count);
        Assert.True(changes[0].Undone);
        Assert.False(changes[1].Undone);
        Assert.Equal(new TextPos(2, 1), changes[1].Place);
        // Redone: in the text again
        editor.Type("<C-r>");
        Assert.All(Changes(editor), change => Assert.False(change.Undone));
    }

    [Fact]
    public void AReplaceOnSeveralLines_IsSeveralChanges()
    {
        var editor = Editor("a1\nb\na2");
        editor.Type(":%s/a/z/<CR>");
        Assert.Equal("z1\nb\nz2", editor.GetText());
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Several, change.Kind);
        Assert.Equal(2, change.Count);
    }

    [Fact]
    public void AReplaceWithLineBreaks_IsToldAtItsFirstPlace()
    {
        // ":%s" replaces from the last line up: the line breaks put in above move the lower parts down, and the step
        // is told at its first part in the text
        var editor = Editor("x\ny\nx");
        editor.Type(":%s/x/a\\rb/<CR>");
        Assert.Equal("a\nb\ny\na\nb", editor.GetText());
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Several, change.Kind);
        Assert.Equal(2, change.Count);
        Assert.Equal(new TextPos(0, 0), change.Place);
        Assert.Equal("x", change.Removed);
    }

    [Fact]
    public void TheTimeOfAStep_IsKept()
    {
        var editor = Editor("text");
        var time = new DateTime(2026, 11, 9, 14, 32, 5);
        editor.Buffer.Clock = () => time;
        editor.Type("Ax<Esc>");
        Assert.Equal(time, Assert.Single(Changes(editor)).Time);
    }

    [Fact]
    public void APlace_MovesWithTheEditsMadeBeforeIt()
    {
        var edit = new TextBuffer.Edit(new TextPos(1, 2), "ab", "xyz");
        // Before the edit: stays
        Assert.Equal(new TextPos(1, 1), ChangeHistory.Shift(new TextPos(1, 1), edit));
        // In the text removed: to the edit
        Assert.Equal(new TextPos(1, 2), ChangeHistory.Shift(new TextPos(1, 3), edit));
        // After it on the line: one column more
        Assert.Equal(new TextPos(1, 6), ChangeHistory.Shift(new TextPos(1, 5), edit));
        // A line inserted above: one line down
        Assert.Equal(new TextPos(4, 0), ChangeHistory.Shift(new TextPos(3, 0),
            new TextBuffer.Edit(new TextPos(0, 0), string.Empty, "new\n")));
    }

    [Fact]
    public void AJumpFromAList_ShowsThePlace()
    {
        // Picked in the list of ":changes" or ":marks", far below the rows shown: the view goes there
        var editor = Editor(string.Join("\n", Enumerable.Range(1, 60).Select(i => "line " + i)));
        editor.JumpToPlace(new TextPos(49, 2));
        Assert.Equal(new TextPos(49, 2), editor.Cursor);
        Assert.InRange(49 - editor.TopRow, 0, 9);
        editor.Type("ggma");
        editor.Type("G");
        editor.JumpToMark('a');
        Assert.Equal(0, editor.Cursor.Line);
        Assert.Equal(0, editor.TopRow);
    }

    [Fact]
    public void AnExcerpt_IsTheFirstLineWithText()
    {
        Assert.Equal("Hello ...", ChangeHistory.Excerpt("\n  Hello\n\nworld\n"));
        Assert.Equal("one", ChangeHistory.Excerpt("one\n"));
        Assert.Equal(string.Empty, ChangeHistory.Excerpt("\n \n"));
    }

    [Fact]
    public void AReplaceOnVeryManyLines_IsSeveralChanges_ToldQuickly()
    {
        // (the parts are not joined: comparing each edit with every part took seconds, and minutes on longer texts)
        var editor = Editor(string.Join("\n", Enumerable.Range(1, 50_000).Select(i => "line " + i)));
        editor.Type(":%s/line/row/<CR>");
        Assert.StartsWith("row 1\nrow 2\n", editor.GetText());
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var change = Assert.Single(Changes(editor));
        clock.Stop();
        Assert.True(clock.ElapsedMilliseconds < 2000, clock.ElapsedMilliseconds + " ms");
        Assert.Equal(ChangeHistory.ChangeKind.Several, change.Kind);
        Assert.Equal(50_000, change.Count);
        // At its first place in the text
        Assert.Equal(new TextPos(0, 0), change.Place);
        Assert.Equal("line 1", change.Removed);
        Assert.Equal("row 1", change.Inserted);
    }

    [Fact]
    public void AStepAfterAVeryLargeOne_IsStillMovedByIt()
    {
        // As in a short text: a line break put in on every line moves the x down a line for each line
        var small = Editor("a\na\na");
        small.Type("GAx<Esc>");
        small.Type(":%s/a/a\\rb/<CR>");
        var place = Changes(small)[1].Place;
        var editor = Editor(string.Join("\n", Enumerable.Range(1, 3000).Select(_ => "a")));
        editor.Type("GAx<Esc>");
        editor.Type(":%s/a/a\\rb/<CR>");
        var changes = Changes(editor);
        Assert.Equal(2, changes.Count);
        Assert.Equal(ChangeHistory.ChangeKind.Several, changes[0].Kind);
        Assert.Equal(3000, changes[0].Count);
        Assert.Equal(new TextPos(0, 0), changes[0].Place);
        Assert.Equal(ChangeHistory.ChangeKind.Inserted, changes[1].Kind);
        Assert.Equal(new TextPos(place.Line + 2 * (3000 - 3), place.Col), changes[1].Place);
    }

    [Fact]
    public void ALongTextTyped_IsStillOneInsertion()
    {
        // Thousands of edits, but one part of the text
        var editor = Editor("x");
        var typed = string.Concat(Enumerable.Repeat("word ", 600));
        editor.Type("A" + typed + "<Esc>");
        var change = Assert.Single(Changes(editor));
        Assert.Equal(ChangeHistory.ChangeKind.Inserted, change.Kind);
        Assert.Equal(typed, change.Inserted);
    }

    [Fact]
    public void WhenThereIsNoTimeLeft_TheStepsLeftAreToldInOneRow()
    {
        var editor = Editor("a\nb\nc");
        editor.Type("Ax<Esc>");
        editor.Type("jAy<Esc>");
        Assert.Equal(2, Changes(editor).Count);
        // No time at all: none is listed
        var change = Assert.Single(ChangeHistory.Of(editor.Buffer.History(), -1));
        Assert.Equal(ChangeHistory.ChangeKind.NotListed, change.Kind);
        Assert.Equal(2, change.Count);
    }
}
