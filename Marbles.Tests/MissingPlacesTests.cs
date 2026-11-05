namespace Marbles.Tests;

/// <summary>
/// The entries of the lists whose file or directory is gone (":missing", ":prune"): an entry on a drive that is not
/// connected is missing, but kept by ":prune"
/// </summary>
[Collection("Static state")]
public class MissingPlacesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "marbles-missing-" + Guid.NewGuid().ToString("N"));

    public MissingPlacesTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void AFileOrDirectoryThatIsThere_IsNotMissing()
    {
        var file = Path.Combine(_directory, "notes.md");
        File.WriteAllText(file, "x");
        var check = new MissingPlaces.Checker();
        Assert.False(check.IsMissing(file, true));
        Assert.False(check.IsMissing(_directory));
        Assert.True(check.IsMissing(Path.Combine(_directory, "gone.md"), true));
        Assert.True(check.IsMissing(Path.Combine(_directory, "gone", "deeper")));
        // A folder that is there: the drive is too
        Assert.False(check.OnAbsentDrive(Path.Combine(_directory, "gone.md")));
        // Not a full path
        Assert.False(MissingPlaces.OnAbsentDrive("notes.md", _ => false));
        Assert.False(MissingPlaces.OnAbsentDrive(string.Empty, _ => false));
    }

    [Fact]
    public void ADriveMountedInMediaMntOrVolumes_IsAbsentWhenItsFolderIsGone()
    {
        var there = new HashSet<string> { "/media", "/media/anna", "/mnt", "/Volumes", "/run/media", "/run/media/anna" };
        bool Exists(string path) => there.Contains(path);
        // The drive is not mounted: its folder is not there
        Assert.True(MissingPlaces.OnAbsentDrive("/media/anna/USB/book/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/run/media/anna/USB/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/mnt/backup/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/Volumes/USB/ch1.md", Exists));
        // The directory of the drive itself (as in the hotlist), also with a separator at the end
        Assert.True(MissingPlaces.OnAbsentDrive("/Volumes/USB", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/Volumes/USB/", Exists));
        // A file directly in a mount folder is not a drive: it is gone
        Assert.False(MissingPlaces.OnAbsentDrive("/mnt/notes.md", Exists, true));
        Assert.True(MissingPlaces.OnAbsentDrive("/mnt/notes.md", Exists));
        // Shorter than a drive: the folders that are there
        Assert.False(MissingPlaces.OnAbsentDrive("/media/anna", Exists));
        // The drive is there: the file is gone from it
        there.Add("/media/anna/USB");
        there.Add("/Volumes/USB");
        Assert.False(MissingPlaces.OnAbsentDrive("/media/anna/USB/book/ch1.md", Exists));
        Assert.False(MissingPlaces.OnAbsentDrive("/Volumes/USB/ch1.md", Exists));
        // Not on a drive mounted there
        Assert.False(MissingPlaces.OnAbsentDrive("/home/anna/gone/ch1.md", Exists));
    }

    [Fact]
    public void ANetworkShareOfGnomeOrAutofs_IsAbsentWhenItIsNotMounted()
    {
        var there = new HashSet<string> { "/run/user", "/run/user/1000", "/run/user/1000/gvfs", "/net" };
        bool Exists(string path) => there.Contains(path);
        Assert.True(MissingPlaces.OnAbsentDrive("/run/user/1000/gvfs/smb-share:server=nas,share=books/ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive("/net/nas/books/ch1.md", Exists));
        there.Add("/run/user/1000/gvfs/smb-share:server=nas,share=books");
        Assert.False(MissingPlaces.OnAbsentDrive("/run/user/1000/gvfs/smb-share:server=nas,share=books/ch1.md", Exists));
    }

    [Fact]
    public void TheChecker_LooksAtEachDriveOnce()
    {
        var asked = new List<string>();
        var check = new MissingPlaces.Checker(path =>
        {
            asked.Add(path);
            return false;
        });
        // Three files on a drive that is not mounted: its folder is looked at once, and the files not at all
        foreach (var name in new[] { "a.md", "b.md", "c.md" }) Assert.True(check.IsMissing("/Volumes/USB/" + name, true));
        Assert.Equal(new[] { "/Volumes/USB" }, asked);
    }

    [WindowsFact]
    public void ADriveLetterOrShareThatIsNotThere_IsAbsent()
    {
        var there = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\", @"\\server\books" };
        bool Exists(string path) => there.Contains(path);
        Assert.True(MissingPlaces.OnAbsentDrive(@"E:\Book\ch1.md", Exists));
        Assert.True(MissingPlaces.OnAbsentDrive(@"\\server\share\Book\ch1.md", Exists));
        Assert.False(MissingPlaces.OnAbsentDrive(@"\\server\books\Book\ch1.md", Exists));
        Assert.False(MissingPlaces.OnAbsentDrive(@"C:\Book\ch1.md", Exists));
        // (a Unix path on Windows has the root "\": not a drive)
        Assert.False(MissingPlaces.OnAbsentDrive("/home/anna/ch1.md", Exists));
        // A drive letter that is not used, on this computer
        var unused = "QRSTUVWXYZ".Select(letter => letter + @":\").FirstOrDefault(drive => !Directory.Exists(drive));
        if (unused != null) Assert.True(new MissingPlaces.Checker().IsMissing(unused + @"Book\ch1.md", true));
    }

    [Fact]
    public void Sort_PrunesTheMissing_AndKeepsThoseOnDrivesThatAreNotThere()
    {
        var there = new HashSet<string> { "/media", "/media/anna", "/mnt" };
        var check = new MissingPlaces.Checker(path => there.Contains(path));
        var (prune, kept) = MissingPlaces.Sort(
            new[] { "/home/anna/gone.md", "/media/anna/USB/ch1.md", "/mnt/backup/old", "/home/anna/old/" },
            path => check.OnAbsentDrive(path));
        Assert.Equal(new[] { "/home/anna/gone.md", "/home/anna/old/" }, prune);
        Assert.Equal(new[] { "/media/anna/USB/ch1.md", "/mnt/backup/old" }, kept);
    }

    [Fact]
    public void TheMessage_TellsWhyNothingIsRemoved()
    {
        Lang.Set(Lang.English);
        Assert.Equal(MissingPlaces.NothingMissing, MissingPlaces.NothingToPrune(0));
        Assert.Equal("Nothing removed (on drives not connected: 2)", MissingPlaces.NothingToPrune(2));
    }
}
