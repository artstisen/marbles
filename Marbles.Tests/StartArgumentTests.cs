namespace Marbles.Tests;

/// <summary>
/// Starting Marbles with a file or directory ("marbles notes.md"), and the data directory it finds wherever it is
/// started from
/// </summary>
[Collection("Static state")]
public class StartArgumentTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _root;

    public StartArgumentTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "marbles-start-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        File.WriteAllText(Path.Combine(_root, "notes", "a.md"), "x");
        File.WriteAllText(Path.Combine(_root, "notes", "other.mbls"), "x");
        // As the current directory tells it (on macOS the temporary directory is reached by a link)
        _root = FileOperations.RealPath(_root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private StartArgument.Result Resolve(string argument, StartMenu.OnlyMode only = StartMenu.OnlyMode.None) =>
        StartArgument.Resolve(argument, _root, only);

    [Fact]
    public void AFile_IsOpened_AndARelativePathIsFromTheStartDirectory()
    {
        var result = Resolve(Path.Combine("notes", "a.md"));
        Assert.Equal(new JumpTarget(JumpKind.File, Path.Combine(_root, "notes", "a.md")), result.Target);
        Assert.False(result.AskToCreate);
        Assert.Equal(string.Empty, result.Message);
        // A full path, also in quotes
        Assert.Equal(JumpKind.File, Resolve("\"" + Path.Combine(_root, "notes", "a.md") + "\"").Target!.Kind);
    }

    [Fact]
    public void APlusLine_OpensTheFileAtTheLine_BeforeOrAfterIt()
    {
        var file = Path.Combine("notes", "a.md");
        Assert.Equal(new StartArgument.Arguments(file, 12), StartArgument.Parse(new[] { "+12", file }, _root));
        Assert.Equal(new StartArgument.Arguments(file, 3), StartArgument.Parse(new[] { file, "+3" }, _root));
        Assert.Equal(new StartArgument.Arguments(file, null), StartArgument.Parse(new[] { file }, _root));
        Assert.Equal(new StartArgument.Arguments(null, null), StartArgument.Parse(System.Array.Empty<string>(), _root));
        // A line without a file: told
        Assert.Equal("No file to open at line 5", StartArgument.Parse(new[] { "+5" }, _root).Message);
        // A line before the first is the first; a number too large for a line is the end
        Assert.Equal(1, StartArgument.Parse(new[] { "+0", file }, _root).Line);
        Assert.Equal(int.MaxValue, StartArgument.Parse(new[] { "+99999999999", file }, _root).Line);
        // The line goes with the file, not with a directory
        Assert.Equal((12, 1), StartArgument.Resolve(file, _root, StartMenu.OnlyMode.None, 12).Target!.At);
        Assert.Null(StartArgument.Resolve("notes", _root, StartMenu.OnlyMode.None, 12).Target!.At);
        // Not a line: "+" alone, or with more than digits
        Assert.Equal("+x", StartArgument.Parse(new[] { "+x" }, _root).Path);
        Assert.Equal("+", StartArgument.Parse(new[] { "+" }, _root).Path);
        // Two paths: told
        Assert.NotEqual(string.Empty, StartArgument.Parse(new[] { file, "notes" }, _root).Message);
    }

    [Fact]
    public void APlusLine_IsLeftOut_ForADataFile()
    {
        // A data file has notes, not lines: it is opened as without the line
        var result = StartArgument.Resolve(Path.Combine("notes", "other.mbls"), _root, StartMenu.OnlyMode.None, 12);
        Assert.Equal(string.Empty, result.Message);
        Assert.Null(result.Target!.At);
        // A file to make gets the line too (it is opened at the end of it)
        Assert.Equal((4, 1), StartArgument.Resolve(Path.Combine("notes", "new.md"), _root, StartMenu.OnlyMode.None, 4)
            .Target!.At);
    }

    [Fact]
    public void AnOption_IsFound_WhereverItIsGiven()
    {
        var file = Path.Combine("notes", "a.md");
        Assert.Equal("--help", StartArgument.FirstOption(new[] { "+12", "--help" }, _root));
        Assert.Equal("-v", StartArgument.FirstOption(new[] { file, "-v" }, _root));
        Assert.Equal("-h", StartArgument.FirstOption(new[] { "-h", file }, _root));
        Assert.Null(StartArgument.FirstOption(new[] { "+12", file }, _root));
        Assert.Null(StartArgument.FirstOption(System.Array.Empty<string>(), _root));
        // A file named as an option that is there is the file
        File.WriteAllText(Path.Combine(_root, "-notes.md"), "x");
        Assert.Null(StartArgument.FirstOption(new[] { "+3", "-notes.md" }, _root));
    }

    [Fact]
    public void AFileNamedAsALine_IsTheFile()
    {
        File.WriteAllText(Path.Combine(_root, "+7"), "x");
        Assert.Equal(new StartArgument.Arguments("+7", null), StartArgument.Parse(new[] { "+7" }, _root));
    }

    [Fact]
    public void ADirectory_IsShownInTheBrowser()
    {
        Assert.Equal(new JumpTarget(JumpKind.Directory, Path.Combine(_root, "notes")), Resolve("notes").Target);
        Assert.Equal(JumpKind.Directory, Resolve(".").Target!.Kind);
    }

    [Fact]
    public void AFileThatIsNotThere_CanBeMade_InADirectoryThatIs()
    {
        var result = Resolve(Path.Combine("notes", "new.md"));
        Assert.True(result.AskToCreate);
        Assert.Equal(Path.Combine(_root, "notes", "new.md"), result.Path);
        // In a directory that is not there: not found
        var missing = Resolve(Path.Combine("nowhere", "new.md"));
        Assert.Null(missing.Target);
        Assert.Contains("nowhere", missing.Message);
    }

    [Fact]
    public void AModeThatIsNotUsed_IsRefused()
    {
        Assert.Equal(DialogText.EditorNotUsed, Resolve(Path.Combine("notes", "a.md"), StartMenu.OnlyMode.Notes).Message);
        Assert.Equal(DialogText.EditorNotUsed, Resolve("notes", StartMenu.OnlyMode.Notes).Message);
        Assert.Equal(DialogText.NotesNotUsed, Resolve(Path.Combine("notes", "other.mbls"), StartMenu.OnlyMode.Editor).Message);
    }

    [Fact]
    public void ADataFile_InTheDataDirectory_IsSelectedOnTheLoginPage_AnOtherOneIsShownInTheBrowser()
    {
        // Started in a directory with a data directory: that one is used
        Directory.CreateDirectory(Path.Combine(_root, "data", "work"));
        File.WriteAllText(Path.Combine(_root, "data", "work", "w.mbls"), "x");
        Directory.SetCurrentDirectory(_root);
        Assert.Equal(Path.Combine(_root, "data"), Path.GetFullPath(FileIo.GetDataDirectoryName()));
        Assert.Equal(new JumpTarget(JumpKind.DataFile, "work/w.mbls"), Resolve(Path.Combine("data", "work", "w.mbls")).Target);
        // Outside the data directory: the browser, which offers to copy it there
        Assert.Equal(JumpKind.File, Resolve(Path.Combine("notes", "other.mbls")).Target!.Kind);
    }

    [Fact]
    public void StartedInADirectoryWithADataDirectory_ThatOneIsUsed_WhenItIsMarbles()
    {
        // A folder "data" of something else (a web site) is not used
        Directory.CreateDirectory(Path.Combine(_root, "site", "data"));
        File.WriteAllText(Path.Combine(_root, "site", "data", "index.json"), "{}");
        Directory.SetCurrentDirectory(Path.Combine(_root, "site"));
        Assert.NotEqual(Path.Combine(_root, "site", "data"), Path.GetFullPath(FileIo.GetDataDirectoryName()));
        // One with the settings of Marbles is (chosen again in another directory, as when Marbles is started there)
        Directory.CreateDirectory(Path.Combine(_root, "notes", "data"));
        File.WriteAllText(Path.Combine(_root, "notes", "data", FileIo.SettingsFileName), "[SETTINGS]");
        Directory.SetCurrentDirectory(Path.Combine(_root, "notes"));
        Assert.Equal(Path.Combine(_root, "notes", "data"), Path.GetFullPath(FileIo.GetDataDirectoryName()));
        // And one with data files only
        Directory.CreateDirectory(Path.Combine(_root, "old", "data"));
        File.WriteAllText(Path.Combine(_root, "old", "data", "notes.mbls"), "x");
        Directory.SetCurrentDirectory(Path.Combine(_root, "old"));
        Assert.Equal(Path.Combine(_root, "old", "data"), Path.GetFullPath(FileIo.GetDataDirectoryName()));
    }

    [Fact]
    public void WithoutADataDirectoryWhereItIsStarted_TheOneNextToTheProgramIsUsed()
    {
        Directory.SetCurrentDirectory(Path.Combine(_root, "notes"));
        var data = Path.GetFullPath(FileIo.GetDataDirectoryName());
        Assert.NotEqual(Path.Combine(_root, "notes", "data"), data);
        Assert.Equal("data", Path.GetFileName(data));
        // The spell files are next to it
        Assert.Equal(Path.Combine(Path.GetDirectoryName(data)!, "spell"), SpellChecker.DirectoryName);
    }
}
