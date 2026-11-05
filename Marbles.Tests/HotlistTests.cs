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

    private static IEnumerable<string> Paths() => DirectoryHotlist.Items.Select(entry => entry.Path);

    [Fact]
    public void Directories_AreAddedAtTheEnd_Once()
    {
        Assert.Equal(DirectoryHotlist.AddResult.Added, DirectoryHotlist.Add(In("a")));
        Assert.Equal(DirectoryHotlist.AddResult.Added, DirectoryHotlist.Add(In("b")));
        Assert.Equal(DirectoryHotlist.AddResult.AlreadyThere, DirectoryHotlist.Add(In("a")));
        Assert.Equal(new[] { In("a"), In("b") }, Paths());
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
        Assert.Equal(new[] { In("a"), In("b c") }, Paths());
        // Edited by hand: blank lines, spaces around and a directory twice are not used
        DirectoryHotlist.Parse("\r\n  " + In("x") + "  \r\n\r\n" + In("x") + "\n" + In("y"));
        Assert.Equal(new[] { In("x"), In("y") }, Paths());
    }

    [Fact]
    public void Directories_AreMoved_RemovedAndPutBack()
    {
        foreach (var name in new[] { "a", "b", "c", "d" }) DirectoryHotlist.Add(In(name));
        Assert.Equal(0, DirectoryHotlist.Move(2, 0));
        Assert.Equal(new[] { In("c"), In("a"), In("b"), In("d") }, Paths());
        Assert.Equal(3, DirectoryHotlist.Move(0, 9));
        Assert.Equal(new[] { In("a"), In("b"), In("d"), In("c") }, Paths());
        DirectoryHotlist.RemoveAt(1);
        Assert.Equal(new[] { In("a"), In("d"), In("c") }, Paths());
        DirectoryHotlist.Insert(1, new DirectoryHotlist.Entry(In("b")));
        Assert.Equal(new[] { In("a"), In("b"), In("d"), In("c") }, Paths());
    }

    [Fact]
    public void SeveralRemoved_ArePutBackInTheirPlaces_ButNotOneThatIsThereAgain()
    {
        foreach (var name in new[] { "a", "b", "c", "d", "e", "f" }) DirectoryHotlist.Add(In(name));
        // (the places in any order, and once each)
        var removed = DirectoryHotlist.RemoveMany([4, 1, 3, 1, 9]);
        Assert.Equal(new[] { 1, 3, 4 }, removed.Select(entry => entry.Index));
        Assert.Equal(new[] { In("a"), In("c"), In("f") }, Paths());
        Assert.Equal(3, DirectoryHotlist.PutBack(removed).Count);
        Assert.Equal(new[] { In("a"), In("b"), In("c"), In("d"), In("e"), In("f") }, Paths());
        // One of them added again before they are put back: it stays where it was added, and the others go back
        removed = DirectoryHotlist.RemoveMany([1, 3, 4]);
        DirectoryHotlist.Add(In("d"));
        var back = DirectoryHotlist.PutBack(removed);
        Assert.Equal(new[] { In("b"), In("e") }, back.Select(entry => entry.Path));
        Assert.Equal(new[] { In("a"), In("b"), In("c"), In("e"), In("f"), In("d") }, Paths());
        // A full list takes none back
        removed = DirectoryHotlist.RemoveMany([0]);
        for (var i = 0; DirectoryHotlist.Items.Count < DirectoryHotlist.MaxCount; i++) DirectoryHotlist.Add(In("more" + i));
        Assert.Empty(DirectoryHotlist.PutBack(removed));
    }

    [Fact]
    public void ADirectory_CanBeShownWithANameOfItsOwn_KeptAfterATab()
    {
        DirectoryHotlist.Add(In("a"));
        DirectoryHotlist.Add(In("b"));
        Assert.Equal("a", DirectoryHotlist.Items[0].Shown);
        DirectoryHotlist.Rename(0, "  Project\tdocs ");
        Assert.Equal("Project docs", DirectoryHotlist.Items[0].Shown);
        Assert.Equal(In("a") + "\tProject docs\n" + In("b") + "\n", DirectoryHotlist.Serialize());
        DirectoryHotlist.Parse(DirectoryHotlist.Serialize());
        Assert.Equal("Project docs", DirectoryHotlist.Items[0].Name);
        Assert.Equal(In("a"), DirectoryHotlist.Items[0].Path);
        // An empty name, or the name of the directory, shows the name of the directory again
        DirectoryHotlist.Rename(0, "");
        Assert.Equal("a", DirectoryHotlist.Items[0].Shown);
        DirectoryHotlist.Rename(1, "b");
        Assert.Equal(string.Empty, DirectoryHotlist.Items[1].Name);
        // The name stays when the directory is renamed
        DirectoryHotlist.Rename(0, "Mine");
        DirectoryHotlist.PathChanged(In("a"), In("c"));
        Assert.Equal(new DirectoryHotlist.Entry(In("c"), "Mine"), DirectoryHotlist.Items[0]);
    }

    [UnixFact]
    public void BlankSpaceAtTheEndsOfAPath_IsKept_WhenTheDirectoryHasIt()
    {
        // Linux and macOS allow a directory name that ends with blank space (Windows does not)
        var spaced = In("notes ");
        Directory.CreateDirectory(spaced);
        DirectoryHotlist.Parse(spaced + "\n  " + In("other") + "  \n");
        Assert.Equal(new[] { spaced, In("other") }, Paths());
    }

    [Fact]
    public void LongPaths_AreShortenedByTheCellsTheyTake()
    {
        // Wide characters (Japanese, Chinese) take two cells, and an emoji is not split
        Assert.Equal("/home/lars", TextMetrics.KeepEnd("/home/lars", 10));
        Assert.Equal("...資料/メモ", TextMetrics.KeepEnd("/home/lars/資料/メモ", 12));
        Assert.Equal(12, TextMetrics.WidthOf(TextMetrics.KeepEnd("/home/lars/資料/メモ", 12)));
        Assert.Equal(".../📁x", TextMetrics.KeepEnd("/home/📁/📁x", 7));
        Assert.Equal("...lars/x", TextMetrics.KeepEnd("/home/lars/x", 9));
        // The Jump menu shortens in the middle, by cells too
        Assert.Equal("資料...メモ", JumpMenu.ShortName("資料フォルダのメモ", 11));
        Assert.Equal("a-rath...ing.md", JumpMenu.ShortName("a-rather-long-name-for-testing.md", 15));
    }

    [Fact]
    public void ARenamedOrMovedDirectory_IsFollowed_WithTheDirectoriesInIt()
    {
        DirectoryHotlist.Add(In("work"));
        DirectoryHotlist.Add(In(Path.Combine("work", "notes")));
        DirectoryHotlist.Add(In("other"));
        Assert.True(DirectoryHotlist.PathChanged(In("work"), In("job")));
        Assert.Equal(new[] { In("job"), In(Path.Combine("job", "notes")), In("other") }, Paths());
        // A directory with a name that starts the same is not in it
        Assert.False(DirectoryHotlist.PathChanged(In("oth"), In("x")));
    }
}
