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
