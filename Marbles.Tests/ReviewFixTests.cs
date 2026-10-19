namespace Marbles.Tests;

/// <summary>
/// The fixes of the final review: a list that cannot be read is not written over, a tab or line break is kept out
/// of the lists, macros record command lines as they are given and refuse steps too long for the command line, and
/// a macro run of many steps is undone as one
/// </summary>
[Collection("Static state")]
public class ReviewFixTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "marbles-review-" + Guid.NewGuid().ToString("N"));
    private readonly string? _favoritesPath = FileFavorites.FilePathOverride;
    private readonly string? _bookmarksPath = Bookmarks.FilePathOverride;

    public ReviewFixTests()
    {
        Directory.CreateDirectory(_directory);
        FileFavorites.FilePathOverride = Path.Combine(_directory, "favorites.txt");
        Bookmarks.FilePathOverride = Path.Combine(_directory, "bookmarks.txt");
        Tools.ClearLineMemory();
        CommandHistory.Clear();
    }

    public void Dispose()
    {
        // (read again, so a list that could not be read in a test does not stop the next ones from saving)
        FileFavorites.Load();
        FileFavorites.FilePathOverride = _favoritesPath;
        Bookmarks.FilePathOverride = _bookmarksPath;
        CommandHistory.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void AListThatCouldNotBeRead_IsNotWrittenOver()
    {
        // (a file is only locked so on Windows)
        if (!OperatingSystem.IsWindows()) return;
        var path = FileFavorites.FilePathOverride!;
        File.WriteAllText(path, "first.txt\nsecond.txt\n");
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            FileFavorites.Load();
        }

        // Locked for a moment while it was read: a favorite added after it does not replace the others
        Assert.Empty(FileFavorites.Items);
        FileFavorites.Toggle(Path.Combine(_directory, "new.txt"));
        Assert.Equal(DialogText.ListNotRead, FileFavorites.Save());
        Assert.Equal("first.txt\nsecond.txt\n", File.ReadAllText(path));

        // Read again: it can be saved
        FileFavorites.Load();
        Assert.Equal(2, FileFavorites.Items.Count);
        Assert.Equal(DialogText.Success, FileFavorites.Save());
    }

    [Fact]
    public void ATabOrALineBreak_IsKeptOutOfTheBookmarks()
    {
        Bookmarks.Load();
        Assert.Equal(Lang.T("A path with a tab or a line break is not kept"),
            Bookmarks.AddWithMessage(new JumpTarget(JumpKind.Directory, Path.Combine(_directory, "a\tb"))));
        Assert.Empty(Bookmarks.Items);
        Assert.StartsWith(Lang.F("Bookmarked: {0}", string.Empty),
            Bookmarks.AddWithMessage(new JumpTarget(JumpKind.Directory, _directory)));
        Assert.True(Bookmarks.Rename(0, "one\ntwo\r\nthree"));
        Assert.Equal("one two  three", Bookmarks.Items[0].Name);
    }

    [Fact]
    public void ACommandOrSearchOfAMacro_ThatIsTooLongForTheCommandLine_IsNotUnderstood()
    {
        var longest = new string('x', EditorEngine.MaxCommandLength);
        Assert.Single(MacroLanguage.Compile("command " + longest, out var error));
        Assert.Equal(string.Empty, error);
        Assert.Empty(MacroLanguage.Compile("command " + longest + "x", out error));
        Assert.NotEqual(string.Empty, error);
        Assert.Empty(MacroLanguage.Compile("find \"" + longest + "x\"", out error));
        Assert.NotEqual(string.Empty, error);
    }

    [Fact]
    public void ACommandLine_IsRecordedAsItWasGiven_WithTheSelectionBeforeIt()
    {
        var editor = new EditorEngine("a1\na2\na3", 40, 10, false);
        editor.Type("Vj:s/a/b/<CR>");
        Assert.Equal("b1\nb2\na3", editor.GetText());

        // Brought back with Up on a command line opened from VISUAL: the selection is kept, and the range is not
        // written twice
        editor.Type("u");
        editor.StartRecording();
        editor.Type("Vj:<Up><CR>");
        var lines = editor.StopRecording();
        var line = Assert.Single(lines);
        Assert.Contains("Vj:s/a/b/", line);
        Assert.DoesNotContain("'<,'>", line);
        Assert.DoesNotContain("Up", line);

        // Played: the same change
        editor.Type("u");
        var macro = string.Join("\n", lines);
        var played = new EditorEngine("a1\na2\na3", 40, 10, false) { MacroLoader = _ => macro };
        played.Type(":play m<CR>");
        Assert.Equal("b1\nb2\na3", played.GetText());
    }

    [Fact]
    public void ARangeOfASelectionThatWasErased_IsErasedWhenTheMacroIsPlayed()
    {
        var editor = new EditorEngine("a1\na2\na3", 40, 10, false);
        editor.StartRecording();
        editor.Type("Vj:<BS><BS><BS><BS><BS>%s/a/b/<CR>");
        Assert.Equal("b1\nb2\nb3", editor.GetText());
        var macro = string.Join("\n", editor.StopRecording());
        var played = new EditorEngine("a1\na2\na3", 40, 10, false) { MacroLoader = _ => macro };
        played.Type(":play m<CR>");
        Assert.Equal("b1\nb2\nb3", played.GetText());
    }

    [Fact]
    public void AGroupOfMoreStepsThanAreKept_IsUndoneAsOne()
    {
        var buffer = new TextBuffer(string.Empty);
        buffer.BeginGroup(new TextPos(0, 0));
        for (var i = 0; i < TextBuffer.MaxUndoSteps + 100; i++)
        {
            buffer.BeginStep(new TextPos(0, i));
            buffer.Insert(new TextPos(0, i), "x");
            buffer.EndStep(new TextPos(0, i + 1));
        }

        buffer.EndGroup(new TextPos(0, TextBuffer.MaxUndoSteps + 100));
        Assert.True(buffer.Undo(out _));
        Assert.Equal(string.Empty, buffer.GetText());
    }
}
