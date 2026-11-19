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
}
