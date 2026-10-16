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
        Assert.Equal(DialogText.NameExists, FileOperations.CreateDirectory(_root, "new", out _));
        Assert.Equal(DialogText.NameCharacter('/'), FileOperations.CreateDirectory(_root, "a/b", out _));
        Assert.Equal(DialogText.InvalidFileName, FileOperations.CreateDirectory(_root, "..", out _));
        Assert.Equal(DialogText.InvalidFileName, FileOperations.CreateDirectory(_root, " ", out _));
    }

    [Fact]
    public void FileNameProblem_TellsWhatIsWrong_TheSameOnAllPlatforms()
    {
        Assert.Null(Tools.FileNameProblem("notes.md"));
        Assert.Null(Tools.FileNameProblem("my notes (2).txt"));
        Assert.Null(Tools.FileNameProblem(".gitignore"));
        Assert.Null(Tools.FileNameProblem("console.txt"));
        Assert.Equal(DialogText.InvalidFileName, Tools.FileNameProblem(""));
        Assert.Equal(DialogText.InvalidFileName, Tools.FileNameProblem(".."));
        foreach (var c in "\\/:*?\"<>|")
        {
            Assert.Equal(DialogText.NameCharacter(c), Tools.FileNameProblem("a" + c + "b.txt"));
        }

        Assert.Equal(DialogText.NameControlCharacters, Tools.FileNameProblem("a\tb"));
        Assert.Equal(DialogText.NameEnd, Tools.FileNameProblem("notes."));
        Assert.Equal(DialogText.NameEnd, Tools.FileNameProblem("notes "));
        Assert.Equal(DialogText.NameTooLong, Tools.FileNameProblem(new string('a', 256)));
        Assert.Equal(DialogText.NameReserved("CON"), Tools.FileNameProblem("con"));
        Assert.Equal(DialogText.NameReserved("LPT1"), Tools.FileNameProblem("Lpt1.txt"));
        Assert.Equal(DialogText.NameReserved("NUL"), Tools.FileNameProblem("nul.tar.gz"));
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
        Assert.Equal(DialogText.NameExists, FileOperations.Rename(renamed, "c.txt", out _));
        Assert.Equal(DialogText.NameExists, FileOperations.Rename(renamed, "other", out _));
        Assert.Equal(DialogText.NameCharacter('/'), FileOperations.Rename(renamed, "a/b", out _));
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

    #region Saving

    [Fact]
    public void SavingAFile_KeepsTheTimeItWasCreated()
    {
        var file = MakeFile("old text", "note.txt");
        var created = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetCreationTimeUtc(file, created);
        File.SetLastWriteTimeUtc(file, created);

        FileIo.WriteAllBytesSafe(file, "new text"u8.ToArray());
        Assert.Equal("new text", File.ReadAllText(file));
        // The time it was changed is now
        Assert.True(File.GetLastWriteTimeUtc(file) > DateTime.UtcNow.AddMinutes(-5));
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.Equal(created, File.GetCreationTimeUtc(file));
        }

        // A text file saved from the editor
        var textFile = TextFile.Load(file);
        Assert.Equal(DialogText.Success, textFile.Save("newer text"));
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            Assert.Equal(created, File.GetCreationTimeUtc(file));
        }

        // A new file is created now
        var other = In("new.txt");
        FileIo.WriteAllBytesSafe(other, "x"u8.ToArray());
        Assert.True(File.GetCreationTimeUtc(other) > DateTime.UtcNow.AddMinutes(-5));
        Assert.False(File.Exists(other + ".tmp"));
    }

    [Fact]
    public void SavingAFile_LeavesAFileWithItsNameAndTmp_AndKeepsItsPermissions()
    {
        // A file of the user's named as a temporary file could be
        var file = MakeFile("old text", "report.md");
        var users = MakeFile("the user's own file", "report.md.tmp");
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Hidden);
        }
        else
        {
            File.SetUnixFileMode(file, File.GetUnixFileMode(file) | UnixFileMode.UserExecute);
        }

        FileIo.WriteAllBytesSafe(file, "new text"u8.ToArray());
        Assert.Equal("new text", File.ReadAllText(file));
        Assert.Equal("the user's own file", File.ReadAllText(users));
        // Nothing is left behind
        Assert.Equal(2, Directory.GetFiles(Path.GetDirectoryName(file)!, "report.md*").Length);
        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.GetAttributes(file).HasFlag(FileAttributes.Hidden));
        }
        else
        {
            Assert.True(File.GetUnixFileMode(file).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public void MovingADirectoryWithAnOpenFile_OnTheSameDrive_IsNotCopied()
    {
        var file = MakeFile("text", "project", "open.txt");
        MakeFile("more", "project", "other.txt");
        Directory.CreateDirectory(In("target"));
        string result;
        // Open as an editor has it: others can read it, not delete it
        using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            result = FileOperations.Move(In("project"), In("target"), out _);
        }

        if (OperatingSystem.IsWindows())
        {
            // Windows cannot move it while the file is open: it stays as it was, and nothing is copied
            Assert.NotEqual(DialogText.Success, result);
            Assert.True(File.Exists(In("project", "open.txt")));
            Assert.True(File.Exists(In("project", "other.txt")));
            Assert.False(Directory.Exists(In("target", "project")));
        }
        else
        {
            // Linux and macOS move it all the same
            Assert.Equal(DialogText.Success, result);
            Assert.True(File.Exists(In("target", "project", "open.txt")));
        }
    }

    [Fact]
    public void SavingALink_WritesTheFileItPointsTo_AndTheLinkStays()
    {
        var target = MakeFile("old text", "real.txt");
        var link = In("link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return; // Links cannot be made here (Windows without Developer Mode)
        }

        FileIo.WriteAllBytesSafe(link, "new text"u8.ToArray());
        Assert.Equal("new text", File.ReadAllText(target));
        Assert.NotNull(new FileInfo(link).LinkTarget);
        Assert.Equal("new text", File.ReadAllText(link));
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
    public void WhereTheCursorWas_IsKeptInTheFavorites_InFoldersToo()
    {
        var a = MakeFile("x", "a.md");
        var b = MakeFile("y", "b.md");
        FileFavorites.Parse("Cyan\t" + a + "\n\t[Work]\n \t" + b + "\n");
        Assert.Null(FileFavorites.CaretOf(a));
        Assert.True(FileFavorites.SetCaret(a, 3, 7));
        Assert.True(FileFavorites.SetCaret(b, 20, 1));
        Assert.False(FileFavorites.SetCaret(In("other.md"), 1, 1));
        var text = FileFavorites.Serialize();
        Assert.Contains("Cyan\t3:7\t" + a + "\n", text);
        Assert.Contains("\t[Work]\n", text);
        Assert.Contains(" \t20:1\t" + b + "\n", text);

        FileFavorites.Parse(text);
        Assert.Equal((3, 7), FileFavorites.CaretOf(a));
        Assert.Equal((20, 1), FileFavorites.CaretOf(b));
        Assert.Equal(ConsoleColor.Cyan, FileFavorites.Items[0].Color);
        Assert.True(FileFavorites.Items[2].InFolder);
        Assert.Equal(text, FileFavorites.Serialize());
    }

    [Fact]
    public void TheColorOfAFavorite_IsFoundByItsPath()
    {
        var a = MakeFile("x", "a.md");
        var b = MakeFile("y", "b.md");
        FileFavorites.Parse("Magenta\t" + a + "\n\t" + b + "\n");
        Assert.Equal(ConsoleColor.Magenta, FileFavorites.ColorOf(a));
        Assert.Null(FileFavorites.ColorOf(b));
        Assert.Null(FileFavorites.ColorOf(In("other.md")));
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

    #region Folders of favorites

    // The list as text: folders in brackets (with "+" when closed), favorites in a folder after a blank space
    private static string Show()
    {
        return string.Join(" ", FileFavorites.Items.Select(item =>
            item.IsFolder ? "[" + item.Name + (item.Collapsed ? "+" : "") + "]" :
            (item.InFolder ? "." : "") + Path.GetFileName(item.Path)));
    }

    private static int Find(string name)
    {
        for (var i = 0; i < FileFavorites.Items.Count; i++)
        {
            var item = FileFavorites.Items[i];
            if (item.IsFolder ? item.Name == name : Path.GetFileName(item.Path) == name) return i;
        }

        return -1;
    }

    private static void Move(string name, bool up, string expected)
    {
        Assert.True(FileFavorites.Move(Find(name), up));
        Assert.Equal(expected, Show());
    }

    [Fact]
    public void Folders_FileFormat()
    {
        FileFavorites.Parse("\t/x/a\nGreen\t[Work]\n Red\t/x/b\n \t/x/c\n\t[Closed]\tcollapsed\n \t/x/d\n\t/x/e\n \t/x/f\n");
        // A line with a blank space in front that does not follow a folder is not in a folder
        Assert.Equal("a [Work] .b .c [Closed+] .d e f", Show());
        Assert.Equal(ConsoleColor.Green, FileFavorites.Items[1].Color);
        Assert.Equal(ConsoleColor.Red, FileFavorites.Items[2].Color);
        Assert.Equal(2, FileFavorites.FolderCount(1));
        Assert.Equal("\t/x/a\nGreen\t[Work]\n Red\t/x/b\n \t/x/c\n\t[Closed]\tcollapsed\n \t/x/d\n\t/x/e\n\t/x/f\n",
            FileFavorites.Serialize());
    }

    [Fact]
    public void Folders_OpenAndCloseAll()
    {
        FileFavorites.Parse("\t/x/a\n\t[One]\n \t/x/b\n\t[Two]\tcollapsed\n \t/x/c\n\t[Empty]\n");
        Assert.Equal("a [One] .b [Two+] .c [Empty]", Show());
        Assert.Equal(3, FileFavorites.SetAllCollapsed(true));
        Assert.Equal("a [One+] .b [Two+] .c [Empty+]", Show());
        Assert.Contains("[One]\tcollapsed", FileFavorites.Serialize());
        Assert.Equal(3, FileFavorites.SetAllCollapsed(false));
        Assert.Equal("a [One] .b [Two] .c [Empty]", Show());
        Assert.DoesNotContain("collapsed", FileFavorites.Serialize());
        // The folder a favorite is in
        Assert.Equal(Find("One"), FileFavorites.FolderOf(Find("b")));
        Assert.Equal(Find("a"), FileFavorites.FolderOf(Find("a")));
        Assert.Equal(Find("Two"), FileFavorites.FolderOf(Find("Two")));
        // A list without folders
        FileFavorites.Parse("\t/x/a\n");
        Assert.Equal(0, FileFavorites.SetAllCollapsed(true));
    }

    [Fact]
    public void Folders_AddRenameRemove()
    {
        FileFavorites.Parse("\t/x/a\n\t[One]\n \t/x/b\n\t/x/c\n");
        Assert.Equal(-1, FileFavorites.AddFolder("a[b", 0));
        Assert.Equal(-1, FileFavorites.AddFolder("  ", 0));
        // In front of the selected item. For an item in a folder: in front of the folder
        Assert.Equal(1, FileFavorites.AddFolder("Two", Find("b")));
        Assert.Equal("a [Two] [One] .b c", Show());
        Assert.Equal(5, FileFavorites.AddFolder("Last", -1));
        Assert.True(FileFavorites.RenameFolder(Find("Two"), " New "));
        Assert.False(FileFavorites.RenameFolder(Find("New"), "]"));
        Assert.False(FileFavorites.RenameFolder(Find("a"), "File"));
        Assert.Equal("a [New] [One] .b c [Last]", Show());
        // A folder is removed with the favorites in it
        FileFavorites.RemoveAt(Find("One"));
        Assert.Equal("a [New] c [Last]", Show());
        // The files of the favorites are found whether they are in a folder or not
        FileFavorites.Parse("\t[One]\n \t/x/b\n");
        Assert.True(FileFavorites.Contains("/x/b"));
        Assert.False(FileFavorites.Toggle("/x/b"));
        Assert.Equal("[One]", Show());
    }

    [Fact]
    public void Folders_AFavoriteMovesIntoAndOutOfAFolder()
    {
        FileFavorites.Parse("\t/x/a\n\t[F]\tcollapsed\n \t/x/b\n \t/x/c\n\t/x/d\n");
        // Down onto the folder: the first in it (and the folder opens)
        Move("a", false, "[F] .a .b .c d");
        Move("a", false, "[F] .b .a .c d");
        Move("a", false, "[F] .b .c .a d");
        // Past the last: out of the folder, below it
        Move("a", false, "[F] .b .c a d");
        Move("a", false, "[F] .b .c d a");
        Assert.False(FileFavorites.Move(Find("a"), false));
        // And up again: into the folder as the last, and out above it
        Move("a", true, "[F] .b .c a d");
        Move("a", true, "[F] .b .c .a d");
        Move("a", true, "[F] .b .a .c d");
        Move("a", true, "[F] .a .b .c d");
        Move("a", true, "a [F] .b .c d");
        Assert.False(FileFavorites.Move(Find("a"), true));
    }

    [Fact]
    public void Folders_EmptyAndClosedFolders()
    {
        FileFavorites.Parse("\t[Empty]\n\t/x/a\n\t[Closed]\tcollapsed\n \t/x/b\n\t/x/c\n");
        // Up into the empty folder, and on above it
        Move("a", true, "[Empty] .a [Closed+] .b c");
        Move("a", true, "a [Empty] [Closed+] .b c");
        // Up into a closed folder: it opens, so the favorite can be seen
        Move("c", true, "a [Empty] [Closed] .b .c");
    }

    [Fact]
    public void Folders_AFolderMovesWithItsFavorites()
    {
        FileFavorites.Parse("\t/x/a\n\t[F]\n \t/x/b\n \t/x/c\n\t/x/d\n\t[G]\n \t/x/e\n");
        // It is closed, and moves past the favorite or the whole folder next to it
        Move("F", false, "a d [F+] .b .c [G] .e");
        Move("F", false, "a d [G] .e [F+] .b .c");
        Assert.False(FileFavorites.Move(Find("F"), false));
        Move("F", true, "a d [F+] .b .c [G] .e");
        Move("F", true, "a [F+] .b .c d [G] .e");
        Move("F", true, "[F+] .b .c a d [G] .e");
        Assert.False(FileFavorites.Move(Find("F"), true));
        Assert.Equal(DialogText.Success, FileFavorites.Save());
        FileFavorites.Load();
        Assert.Equal("[F+] .b .c a d [G] .e", Show());
    }

    [Fact]
    public void Folders_TheLastRemovalCanBeUndone()
    {
        FileFavorites.Parse("\t/x/a\n\t[F]\n \t/x/b\n \t/x/c\n\t/x/d\n");
        Assert.Equal(-1, FileFavorites.UndoRemove());
        // A favorite in a folder goes back into it
        FileFavorites.RemoveAt(Find("b"));
        Assert.Equal("a [F] .c d", Show());
        Assert.Equal(2, FileFavorites.UndoRemove());
        Assert.Equal("a [F] .b .c d", Show());
        // Only the last removal
        Assert.Equal(-1, FileFavorites.UndoRemove());
        // A folder comes back with its favorites
        FileFavorites.RemoveAt(Find("F"));
        Assert.Equal("a d", Show());
        Assert.Equal(1, FileFavorites.UndoRemove());
        Assert.Equal("a [F] .b .c d", Show());
        // The last in the list
        FileFavorites.RemoveAt(Find("d"));
        Assert.Equal(4, FileFavorites.UndoRemove());
        Assert.Equal("a [F] .b .c d", Show());
    }

    [Fact]
    public void Folders_Undo_WhenTheListHasChanged()
    {
        FileFavorites.Parse("\t[F]\n \t/x/b\n\t/x/d\n");
        // The folder of the favorite is gone: it is put back outside a folder
        FileFavorites.RemoveAt(Find("b"));
        FileFavorites.Parse("\t/x/d\n\t[G]\n \t/x/e\n");
        Assert.Equal(-1, FileFavorites.UndoRemove());
        FileFavorites.Parse("\t[F]\n \t/x/b\n\t/x/d\n");
        FileFavorites.RemoveAt(Find("b"));
        FileFavorites.RemoveAt(Find("F"));
        // Now the folder is what was removed last
        Assert.Equal(0, FileFavorites.UndoRemove());
        Assert.Equal("[F] d", Show());
        // A favorite that is in the list again is not added twice
        FileFavorites.RemoveAt(Find("d"));
        FileFavorites.Toggle("/x/d");
        Assert.Equal(-1, FileFavorites.UndoRemove());
        Assert.Equal("[F] d", Show());
    }

    private static List<FileFavorites.Favorite> Items(params string[] names)
    {
        return names.Select(name => FileFavorites.Items[Find(name)]).ToList();
    }

    [Fact]
    public void Folders_SeveralFavoritesAreMovedToAFolder()
    {
        FileFavorites.Parse("\t/x/a\n\t/x/b\n\t[F]\tcollapsed\n \t/x/c\n\t/x/d\n\t[G]\n \t/x/e\n");
        // Last in the folder, in the order they have in the list (the folder opens)
        Assert.Equal(3, FileFavorites.MoveToFolder(Items("d", "a", "e"), FileFavorites.Items[Find("F")]));
        Assert.Equal("b [F] .c .a .d .e [G]", Show());
        // Out of the folders: last in the list
        Assert.Equal(5, FileFavorites.MoveToFolder(Items("c", "e"), null));
        Assert.Equal("b [F] .a .d [G] c e", Show());
        // Folders are not moved, and a favorite is not a folder to move to
        Assert.Equal(-1, FileFavorites.MoveToFolder(Items("G"), FileFavorites.Items[Find("F")]));
        Assert.Equal(-1, FileFavorites.MoveToFolder(Items("b"), FileFavorites.Items[Find("c")]));
        Assert.Equal("b [F] .a .d [G] c e", Show());
        // A favorite that is in the folder already goes last in it
        FileFavorites.MoveToFolder(Items("a"), FileFavorites.Items[Find("F")]);
        Assert.Equal("b [F] .d .a [G] c e", Show());
    }

    [Fact]
    public void Favorites_AreMovedToTheTopOrTheEnd_OfTheListOrTheirFolder()
    {
        FileFavorites.Parse("\t/x/a\n\t[F]\n \t/x/b\n \t/x/c\n \t/x/d\n\t/x/e\n\t/x/f\n\t[G]\n \t/x/g\n");
        // In the order they have, the ones outside the folders in the list, the others in their folder
        Assert.Equal(3, FileFavorites.MoveToEnd(Items("f", "e", "d"), true));
        Assert.Equal("e f a [F] .d .b .c [G] .g", Show());
        Assert.Equal(3, FileFavorites.MoveToEnd(Items("e", "a", "b"), false));
        Assert.Equal("f [F] .d .c .b [G] .g e a", Show());
        // A folder moves with its favorites
        Assert.Equal(1, FileFavorites.MoveToEnd(Items("F"), false));
        Assert.Equal("f [G] .g e a [F] .d .c .b", Show());
        Assert.Equal(1, FileFavorites.MoveToEnd(Items("F"), true));
        Assert.Equal("[F] .d .c .b f [G] .g e a", Show());
        // Already there: nothing changes
        FileFavorites.MoveToEnd(Items("g"), true);
        Assert.Equal("[F] .d .c .b f [G] .g e a", Show());
    }

    [Fact]
    public void Folders_SeveralFavoritesAreRemoved_AndPutBack()
    {
        FileFavorites.Parse("\t/x/a\n\t[F]\n \t/x/b\n \t/x/c\n\t/x/d\n\t/x/e\n");
        Assert.Equal(3, FileFavorites.RemoveMany(Items("a", "c", "e", "F")));
        // The folder is not removed this way
        Assert.Equal("[F] .b d", Show());
        Assert.Equal(0, FileFavorites.UndoRemove());
        Assert.Equal("a [F] .b .c d e", Show());
        Assert.Equal(-1, FileFavorites.UndoRemove());
        Assert.Equal(0, FileFavorites.RemoveMany(new List<FileFavorites.Favorite>()));
    }

    #endregion
}
