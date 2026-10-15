namespace Marbles.Tests;

/// <summary>
/// The directory hotlist of the file browser: the file it is kept in, adding, moving and removing directories,
/// and directories that are renamed or moved
/// </summary>
[Collection("Static state")]
public class HotlistTests : IDisposable
{
    private readonly string _directory;

    public HotlistTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "marbles-hotlist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        DirectoryHotlist.FilePathOverride = Path.Combine(_directory, DirectoryHotlist.FileName);
        DirectoryHotlist.Clear();
    }

    public void Dispose()
    {
        DirectoryHotlist.FilePathOverride = null;
        DirectoryHotlist.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.GetFullPath(Path.Combine(_directory, name));

    [Fact]
    public void Directories_AreAddedAtTheEnd_Once()
    {
        Assert.Equal(DirectoryHotlist.AddResult.Added, DirectoryHotlist.Add(In("a")));
        Assert.Equal(DirectoryHotlist.AddResult.Added, DirectoryHotlist.Add(In("b")));
        Assert.Equal(DirectoryHotlist.AddResult.AlreadyThere, DirectoryHotlist.Add(In("a")));
        Assert.Equal(new[] { In("a"), In("b") }, DirectoryHotlist.Items);
    }

    [Fact]
    public void TheList_HasAtMostMaxCount()
    {
        for (var i = 0; i < DirectoryHotlist.MaxCount; i++) DirectoryHotlist.Add(In("d" + i));
        Assert.Equal(DirectoryHotlist.AddResult.Full, DirectoryHotlist.Add(In("one more")));
        Assert.Equal(DirectoryHotlist.MaxCount, DirectoryHotlist.Items.Count);
    }

    [Fact]
    public void TheList_IsSavedAndReadAgain_AsOnePathOnEachLine()
    {
        DirectoryHotlist.Add(In("a"));
        DirectoryHotlist.Add(In("b c"));
        Assert.Equal(DialogText.Success, DirectoryHotlist.Save());
        Assert.Equal(In("a") + "\n" + In("b c") + "\n", File.ReadAllText(DirectoryHotlist.FilePath));
        DirectoryHotlist.Clear();
        DirectoryHotlist.Load();
        Assert.Equal(new[] { In("a"), In("b c") }, DirectoryHotlist.Items);
        // Edited by hand: blank lines, spaces around and a directory twice are not used
        DirectoryHotlist.Parse("\r\n  " + In("x") + "  \r\n\r\n" + In("x") + "\n" + In("y"));
        Assert.Equal(new[] { In("x"), In("y") }, DirectoryHotlist.Items);
    }

    [Fact]
    public void Directories_AreMoved_RemovedAndPutBack()
    {
        foreach (var name in new[] { "a", "b", "c", "d" }) DirectoryHotlist.Add(In(name));
        Assert.Equal(0, DirectoryHotlist.Move(2, 0));
        Assert.Equal(new[] { In("c"), In("a"), In("b"), In("d") }, DirectoryHotlist.Items);
        Assert.Equal(3, DirectoryHotlist.Move(0, 9));
        Assert.Equal(new[] { In("a"), In("b"), In("d"), In("c") }, DirectoryHotlist.Items);
        DirectoryHotlist.RemoveAt(1);
        Assert.Equal(new[] { In("a"), In("d"), In("c") }, DirectoryHotlist.Items);
        DirectoryHotlist.Insert(1, In("b"));
        Assert.Equal(new[] { In("a"), In("b"), In("d"), In("c") }, DirectoryHotlist.Items);
    }

    [Fact]
    public void ARenamedOrMovedDirectory_IsFollowed_WithTheDirectoriesInIt()
    {
        DirectoryHotlist.Add(In("work"));
        DirectoryHotlist.Add(In(Path.Combine("work", "notes")));
        DirectoryHotlist.Add(In("other"));
        Assert.True(DirectoryHotlist.PathChanged(In("work"), In("job")));
        Assert.Equal(new[] { In("job"), In(Path.Combine("job", "notes")), In("other") }, DirectoryHotlist.Items);
        // A directory with a name that starts the same is not in it
        Assert.False(DirectoryHotlist.PathChanged(In("oth"), In("x")));
    }
}
