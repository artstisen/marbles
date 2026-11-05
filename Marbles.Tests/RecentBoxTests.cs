namespace Marbles.Tests;

/// <summary>
/// The Recent files box (Ctrl+K, :recent): the newest files of the file history, without the file being edited
/// </summary>
[Collection("Static state")]
public class RecentBoxTests
{
    private static string In(string name) => Path.Combine(Path.GetTempPath(), "marbles_recent", name);

    [Fact]
    public void TheRecentFiles_AreTheNewestOfTheHistory_WithoutTheFileEdited()
    {
        var history = Enumerable.Range(1, 12).Select(i => In(i + ".md")).ToList();
        Assert.Equal(history.Take(RecentBox.MaxFiles), RecentBox.Files(history, null));
        // The file being edited is left out: the one before it comes first
        var files = RecentBox.Files(history, In("1.md"));
        Assert.Equal(In("2.md"), files[0]);
        Assert.Equal(RecentBox.MaxFiles, files.Count);
        Assert.DoesNotContain(In("1.md"), files);
        Assert.Empty(RecentBox.Files(new[] { In("1.md") }, In("1.md")));
    }

    [Fact]
    public void ATextFile_IsOpened_AndADataFileOutsideTheDataDirectory_IsOpenedFromTheFileBrowser()
    {
        Assert.Equal(new JumpTarget(JumpKind.File, In("notes.md")), RecentBox.TargetOf(In("notes.md")));
        // (it is offered to be copied to the data directory there, as when it is opened in the file browser)
        Assert.Equal(JumpKind.File, RecentBox.TargetOf(In("elsewhere.mbls")).Kind);
    }

    [Fact]
    public void ADataFileInTheDataDirectory_IsSelectedOnTheLoginPage()
    {
        var path = Path.Combine(FileIo.GetDataDirectoryName(), "work.mbls");
        var target = RecentBox.TargetOf(path);
        Assert.Equal(JumpKind.DataFile, target.Kind);
        Assert.Equal("work.mbls", target.Target);
    }

    [Fact]
    public void CtrlKAndRecentInTheEditor_AskForTheLatestFiles()
    {
        var editor = new EditorEngine("text", 40, 10, false);
        Assert.Equal(EditorRequest.Recent, editor.Type(":recent<CR>"));
        Assert.Equal(EditorRequest.Recent,
            editor.HandleKey(new ConsoleKeyInfo('\u000b', ConsoleKey.K, false, false, true)));
        // In INSERT mode it is the snippets, as before
        editor.Type("i");
        Assert.Equal(EditorRequest.Snippets,
            editor.HandleKey(new ConsoleKeyInfo('\u000b', ConsoleKey.K, false, false, true)));
        Assert.Equal(EditorMode.Insert, editor.Mode);
        // A line of a box has no files to go to
        var line = new EditorEngine("text", 40, 1, true);
        Assert.NotEqual(EditorRequest.Recent,
            line.HandleKey(new ConsoleKeyInfo('\u000b', ConsoleKey.K, false, false, true)));
    }
}
