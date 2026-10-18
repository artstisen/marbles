namespace Marbles.Tests;

/// <summary>
/// A split of the editor (":split"): a second editor on the same text, which follows the changes made in the first
/// while it does not have the focus, and the requests the host acts on (Tab, ":q" and ":only" in a split)
/// </summary>
public class SplitTests
{
    private static string Text(int lines) =>
        string.Join("\n", Enumerable.Range(1, lines).Select(i => "line " + i));

    [Fact]
    public void ASecondPane_ShowsTheSameText_AndSeesTheChanges()
    {
        var editor = new EditorEngine("one\ntwo", 30, 10, false);
        var other = new EditorEngine(editor, 30, 10);
        other.StartFollowing();

        Assert.Same(editor.Buffer, other.Buffer);
        editor.Type("ccchanged<Esc>");
        Assert.Equal("changed\ntwo", other.GetText());
        Assert.True(other.IsModified);
        // Undo is shared, as the text is
        editor.Type("u");
        Assert.Equal("one\ntwo", other.GetText());
    }

    [Fact]
    public void ThePaneWithoutTheFocus_KeepsItsPlace_WhenLinesAreAddedOrRemovedAboveIt()
    {
        var editor = new EditorEngine(Text(50), 30, 10, false);
        var other = new EditorEngine(editor, 30, 10);
        other.Type("40G");
        var topLine = other.Layout.LineAtRow(other.TopRow).Line;
        other.StartFollowing();

        editor.Type("ggOnew<Esc>");
        other.UpdateFollowingView();
        Assert.Equal(new TextPos(40, 0), other.Cursor);
        Assert.Equal("line 40", other.Buffer[other.Cursor.Line]);
        Assert.Equal(topLine + 1, other.Layout.LineAtRow(other.TopRow).Line);

        editor.Type("gg3dd");
        Assert.Equal("line 40", other.Buffer[other.Cursor.Line]);
        // Undone in the other pane: back where it was
        editor.Type("u");
        Assert.Equal("line 40", other.Buffer[other.Cursor.Line]);

        // Lines changed below it: nothing moves
        editor.Type("Gdd");
        Assert.Equal("line 40", other.Buffer[other.Cursor.Line]);

        // Its own line removed (and the lines after it): the cursor stays in the text, as it is followed
        editor.Type("39GdG");
        Assert.InRange(other.Cursor.Line, 0, other.Buffer.LineCount - 1);
    }

    [Fact]
    public void ThePaneWithTheFocus_IsNotMovedByTheChangesOfTheOther()
    {
        // Only the pane that follows is moved: here the other one changes the text above the cursor of a pane that
        // does not follow
        var editor = new EditorEngine(Text(20), 30, 10, false);
        var other = new EditorEngine(editor, 30, 10);
        editor.Type("10G");
        other.Type("ggOnew<Esc>");
        Assert.Equal(9, editor.Cursor.Line);
        Assert.Equal("line 9", editor.Buffer[editor.Cursor.Line]);
    }

    [Fact]
    public void AClosedPane_NoLongerFollows()
    {
        var editor = new EditorEngine(Text(20), 30, 10, false);
        var other = new EditorEngine(editor, 30, 10);
        other.Type("10G");
        other.StartFollowing();
        other.Detach();
        editor.Type("ggOnew<Esc>");
        Assert.Equal(9, other.Cursor.Line);
    }

    [Fact]
    public void Tab_InNormalMode_AsksForTheOtherPane_ButNotWhileRecording()
    {
        var editor = new EditorEngine("one", 30, 10, false);
        Assert.Equal(EditorRequest.SwitchPane, editor.Type("<Tab>"));
        Assert.Equal(EditorRequest.SwitchPane,
            editor.HandleKey(new ConsoleKeyInfo('	', ConsoleKey.Tab, true, false, false)));
        // In Insert mode Tab is blank space, as before
        Assert.Equal(EditorRequest.None, editor.Type("i<Tab><Esc>"));
        Assert.Equal("    one", editor.GetText());

        // Refused while recording, and not a step of the macro
        editor.StartRecording();
        Assert.Equal(EditorRequest.None, editor.Type("x<Tab>x"));
        Assert.Equal(new[] { "delete character", "delete character" }, editor.StopRecording());
    }

    [Fact]
    public void Quit_InASplit_ClosesThePane_AndSplitAndOnlyAreAsked()
    {
        var editor = new EditorEngine("one", 30, 10, false);
        Assert.Equal(EditorRequest.Split, editor.Type(":split<CR>"));
        Assert.Equal(EditorRequest.OnlyPane, editor.Type(":only<CR>"));
        Assert.Equal(EditorRequest.Quit, editor.Type(":q<CR>"));
        editor.InSplit = true;
        Assert.Equal(EditorRequest.ClosePane, editor.Type(":q<CR>"));
        // While recording, it asks about the macro first (as Esc does)
        editor.StartRecording();
        Assert.Equal(EditorRequest.MacroStop, editor.Type(":q<CR>"));
        editor.StopRecording();
        // Esc closes the editor, also in a split
        Assert.Equal(EditorRequest.Quit, editor.Type("<Esc>"));
    }
}
