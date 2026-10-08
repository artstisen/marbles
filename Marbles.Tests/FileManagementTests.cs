namespace Marbles.Tests;

[Collection("Static state")]
public class FileManagementTests : IDisposable
{
    private readonly string _root;
    private readonly string? _savedFavoritesPath = FileFavorites.FilePathOverride;

    public FileManagementTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "marbles_files_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        FileFavorites.FilePathOverride = Path.Combine(_root, "favorites.txt");
        FileFavorites.Parse(string.Empty);
    }

    public void Dispose()
    {
        FileFavorites.FilePathOverride = _savedFavoritesPath;
        FileFavorites.Parse(string.Empty);
        Directory.Delete(_root, true);
    }

    private string In(params string[] parts) => Path.Combine(new[] { _root }.Concat(parts).ToArray());

    private string MakeFile(string text, params string[] parts)
    {
        var path = In(parts);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    #region File operations

    [Fact]
    public void Paths_AreCompared()
    {
        Assert.True(FileOperations.SamePath(In("a"), In("a") + Path.DirectorySeparatorChar));
        Assert.False(FileOperations.SamePath(In("a"), In("b")));
        Assert.True(FileOperations.IsInside(In("a", "b.txt"), In("a")));
        Assert.True(FileOperations.IsInside(In("a", "b", "c"), In("a")));
        Assert.False(FileOperations.IsInside(In("a"), In("a")));
        Assert.False(FileOperations.IsInside(In("ab", "c"), In("a")));
    }

    [Fact]
    public void CreateDirectory_RefusesBadAndUsedNames()
    {
        Assert.Equal(DialogText.Success, FileOperations.CreateDirectory(_root, " new ", out var path));
        Assert.Equal(In("new"), path);
        Assert.True(Directory.Exists(path));
        Assert.Equal(DialogText.ErrorFileAlreadyExist, FileOperations.CreateDirectory(_root, "new", out _));
        Assert.Equal(DialogText.InvalidFileName, FileOperations.CreateDirectory(_root, "a/b", out _));
        Assert.Equal(DialogText.InvalidFileName, FileOperations.CreateDirectory(_root, "..", out _));
        Assert.Equal(DialogText.InvalidFileName, FileOperations.CreateDirectory(_root, " ", out _));
    }

    [Fact]
    public void Rename_FilesAndDirectories()
    {
        var file = MakeFile("text", "a.txt");
        Assert.Equal(DialogText.Success, FileOperations.Rename(file, "b.txt", out var renamed));
        Assert.Equal("text", File.ReadAllText(renamed));
        Assert.False(File.Exists(file));

        MakeFile("x", "dir", "inside.txt");
        Assert.Equal(DialogText.Success, FileOperations.Rename(In("dir"), "other", out var directory));
        Assert.True(File.Exists(Path.Combine(directory, "inside.txt")));

        // Nothing is overwritten
        MakeFile("other", "c.txt");
        Assert.Equal(DialogText.ErrorFileAlreadyExist, FileOperations.Rename(renamed, "c.txt", out _));
        Assert.Equal(DialogText.ErrorFileAlreadyExist, FileOperations.Rename(renamed, "other", out _));
        Assert.Equal(DialogText.InvalidFileName, FileOperations.Rename(renamed, "a/b", out _));
        Assert.Equal("text", File.ReadAllText(renamed));
    }

    [Fact]
    public void Rename_OnlyTheCase()
    {
        var file = MakeFile("text", "notes.txt");
        Assert.Equal(DialogText.Success, FileOperations.Rename(file, "Notes.txt", out _));
        Assert.Contains("Notes.txt", Directory.GetFiles(_root).Select(Path.GetFileName));
        MakeFile("x", "folder", "f.txt");
        Assert.Equal(DialogText.Success, FileOperations.Rename(In("folder"), "Folder", out _));
        Assert.Contains("Folder", Directory.GetDirectories(_root).Select(Path.GetFileName));
        Assert.True(File.Exists(In("Folder", "f.txt")));
    }

    [Fact]
    public void Copy_ToAnotherDirectory()
    {
        var file = MakeFile("text", "a.txt");
        Directory.CreateDirectory(In("target"));
        Assert.Equal(DialogText.Success, FileOperations.Copy(file, In("target"), out var copy));
        Assert.Equal(In("target", "a.txt"), copy);
        Assert.Equal("text", File.ReadAllText(copy));
        Assert.True(File.Exists(file));
        // Nothing is overwritten
        Assert.Equal(DialogText.ErrorFileAlreadyExist, FileOperations.Copy(file, In("target"), out _));
        Assert.Equal(DialogText.DirectoryNotFound, FileOperations.Copy(file, In("missing"), out _));
        Assert.Equal(DialogText.FileNotFound, FileOperations.Copy(In("missing.txt"), In("target"), out _));
    }

    [Fact]
    public void Copy_InTheSameDirectory_GetsANewName()
    {
        var file = MakeFile("text", "a.txt");
        Assert.Equal(DialogText.Success, FileOperations.Copy(file, _root, out var first));
        Assert.Equal(In("a-copy.txt"), first);
        Assert.Equal(DialogText.Success, FileOperations.Copy(file, _root, out var second));
        Assert.Equal(In("a-copy2.txt"), second);

        MakeFile("x", "dir", "f.txt");
        Assert.Equal(DialogText.Success, FileOperations.Copy(In("dir"), _root, out var directory));
        Assert.Equal(In("dir-copy"), directory);
        Assert.True(File.Exists(In("dir-copy", "f.txt")));
    }

    [Fact]
    public void Copy_Directory_WithAllItsContent()
    {
        MakeFile("1", "dir", "one.txt");
        MakeFile("2", "dir", "sub", "two.txt");
        Directory.CreateDirectory(In("dir", "empty"));
        Directory.CreateDirectory(In("target"));
        var names = new List<string>();
        Assert.Equal(DialogText.Success, FileOperations.Copy(In("dir"), In("target"), out var copy, name =>
        {
            names.Add(name);
            return true;
        }));
        Assert.Equal("1", File.ReadAllText(Path.Combine(copy, "one.txt")));
        Assert.Equal("2", File.ReadAllText(Path.Combine(copy, "sub", "two.txt")));
        Assert.True(Directory.Exists(Path.Combine(copy, "empty")));
        Assert.Equal(new[] { "one.txt", "two.txt" }, names.OrderBy(n => n));
        // Not into itself
        Assert.Equal(DialogText.FileIntoItself, FileOperations.Copy(In("dir"), In("dir", "sub"), out _));
        Assert.Equal(DialogText.FileIntoItself, FileOperations.Copy(In("dir"), In("dir"), out _));
    }

    [Fact]
    public void Copy_Canceled_LeavesNothing()
    {
        MakeFile("1", "dir", "one.txt");
        MakeFile("2", "dir", "sub", "two.txt");
        Directory.CreateDirectory(In("target"));
        Assert.Equal(DialogText.FileCanceled,
            FileOperations.Copy(In("dir"), In("target"), out _, name => name != "two.txt"));
        Assert.False(Directory.Exists(In("target", "dir")));
        Assert.True(File.Exists(In("dir", "sub", "two.txt")));
    }

    [Fact]
    public void Move_FilesAndDirectories()
    {
        var file = MakeFile("text", "a.txt");
        MakeFile("x", "dir", "sub", "f.txt");
        Directory.CreateDirectory(In("target"));
        Assert.Equal(DialogText.Success, FileOperations.Move(file, In("target"), out var moved));
        Assert.Equal("text", File.ReadAllText(moved));
        Assert.False(File.Exists(file));
        Assert.Equal(DialogText.Success, FileOperations.Move(In("dir"), In("target"), out var directory));
        Assert.True(File.Exists(Path.Combine(directory, "sub", "f.txt")));
        Assert.False(Directory.Exists(In("dir")));

        // Refused: already there, into itself, and a name that is in use
        Assert.Equal(DialogText.FileAlreadyThere, FileOperations.Move(moved, In("target"), out _));
        Assert.Equal(DialogText.FileIntoItself,
            FileOperations.Move(In("target", "dir"), In("target", "dir", "sub"), out _));
        MakeFile("other", "a.txt");
        Assert.Equal(DialogText.ErrorFileAlreadyExist, FileOperations.Move(In("a.txt"), In("target"), out _));
        Assert.Equal("text", File.ReadAllText(moved));
    }

    [Fact]
    public void Delete_AndCount()
    {
        var file = MakeFile("text", "a.txt");
        MakeFile("1", "dir", "one.txt");
        MakeFile("2", "dir", "sub", "two.txt");
        Assert.Equal((2, 1), FileOperations.Count(In("dir")));
        Assert.Equal(DialogText.Success, FileOperations.Delete(file));
        Assert.False(File.Exists(file));
        Assert.Equal(DialogText.Success, FileOperations.Delete(In("dir")));
        Assert.False(Directory.Exists(In("dir")));
        Assert.Equal(DialogText.FileNotFound, FileOperations.Delete(file));
    }

    #endregion

    #region Favorites

    [Fact]
    public void Favorites_AreSavedWithOrderAndColors()
    {
        var a = MakeFile("a", "a.txt");
        var b = MakeFile("b", "b.txt");
        Assert.True(FileFavorites.Toggle(a));
        Assert.True(FileFavorites.Toggle(b));
        Assert.True(FileFavorites.Toggle(_root));
        Assert.True(FileFavorites.Contains(a));
        FileFavorites.SetColor(1, ConsoleColor.Yellow);
        Assert.True(FileFavorites.Move(2, true));
        Assert.False(FileFavorites.Move(0, true));
        Assert.False(FileFavorites.Move(2, false));
        Assert.Equal(DialogText.Success, FileFavorites.Save());

        FileFavorites.Parse(string.Empty);
        Assert.Empty(FileFavorites.Items);
        FileFavorites.Load();
        Assert.Equal(new[] { a, _root, b }, FileFavorites.Items.Select(f => f.Path));
        Assert.Equal(new ConsoleColor?[] { null, null, ConsoleColor.Yellow }, FileFavorites.Items.Select(f => f.Color));

        // Toggling again removes it
        Assert.False(FileFavorites.Toggle(a));
        Assert.False(FileFavorites.Contains(a));
        FileFavorites.RemoveAt(0);
        Assert.Equal(new[] { b }, FileFavorites.Items.Select(f => f.Path));
    }

    [Fact]
    public void Favorites_FileFormat()
    {
        FileFavorites.Parse("Red\t/one/a.txt\r\n\t/two/b.txt\n/three/c.txt\nNoColor\t/four\n\n\t/one/a.txt\n");
        Assert.Equal(new[] { "/one/a.txt", "/two/b.txt", "/three/c.txt", "/four" },
            FileFavorites.Items.Select(f => f.Path));
        Assert.Equal(new ConsoleColor?[] { ConsoleColor.Red, null, null, null },
            FileFavorites.Items.Select(f => f.Color));
        Assert.Equal("Red\t/one/a.txt\n\t/two/b.txt\n\t/three/c.txt\n\t/four\n", FileFavorites.Serialize());
    }

    [Fact]
    public void Favorites_FollowRenamedAndDeletedFiles()
    {
        var file = MakeFile("a", "dir", "a.txt");
        var other = MakeFile("b", "b.txt");
        FileFavorites.Toggle(file);
        FileFavorites.Toggle(In("dir"));
        FileFavorites.Toggle(other);

        // The directory is renamed: the directory and the file in it follow
        Assert.True(FileFavorites.PathChanged(In("dir"), In("new")));
        Assert.Equal(new[] { In("new", "a.txt"), In("new"), other }, FileFavorites.Items.Select(f => f.Path));
        Assert.False(FileFavorites.PathChanged(In("unknown"), In("x")));

        // The directory is deleted
        Assert.True(FileFavorites.PathDeleted(In("new")));
        Assert.Equal(new[] { other }, FileFavorites.Items.Select(f => f.Path));
        Assert.False(FileFavorites.PathDeleted(In("new")));
    }

    #endregion
}
