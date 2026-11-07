using System.Diagnostics;

namespace Marbles.Tests;

/// <summary>
/// The fixes of the review before v1.5.4: the paste bin file cannot be a data file or a file of Marbles, the put
/// limit holds for a block, a search and ":s" stop in time on one long line, text from the notes is forgotten at
/// logout, special files are not read, and a directory is not copied into itself through a link
/// </summary>
[Collection("Static state")]
public class ReleaseFixTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "marbles-release-" + Guid.NewGuid().ToString("N"));

    private readonly string _data;
    private readonly string _savedPasteBin = SystemClipboard.PasteBinFile;

    public ReleaseFixTests()
    {
        _data = Path.Combine(_directory, "data");
        Directory.CreateDirectory(_data);
        FileIo.DataDirectoryOverride = _data;
        Tools.ClearLineMemory();
    }

    public void Dispose()
    {
        SystemClipboard.PasteBinFile = _savedPasteBin;
        FileIo.DataDirectoryOverride = null;
        EditorEngine.CommandTimeoutMilliseconds = EditorEngine.DefaultCommandTimeoutMilliseconds;
        EditorEngine.ForgetBlockRegister();
        try
        {
            // The links first (only the link is removed, not what it leads to)
            foreach (var link in new DirectoryInfo(_directory).EnumerateDirectories()
                         .Where(directory => directory.LinkTarget != null))
            {
                link.Delete();
            }

            Directory.Delete(_directory, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left in the temporary directory
        }
    }

    private static EditorEngine Editor(string text) => new(text, 40, 10, false);

    #region The paste bin file

    [Fact]
    public void PasteBin_CannotBeADirectoryADataFileOrAFileOfMarbles()
    {
        // A directory
        Assert.NotNull(SystemClipboard.RefusedPasteBin(_directory));
        // A data file, in any case, and the copy of an old one
        Assert.Equal(DialogText.PasteBinNotDataFile, SystemClipboard.RefusedPasteBin("notes.mbls"));
        Assert.Equal(DialogText.PasteBinNotDataFile,
            SystemClipboard.RefusedPasteBin(Path.Combine(_directory, "WORK.MBLS")));
        Assert.Equal(DialogText.PasteBinNotDataFile, SystemClipboard.RefusedPasteBin("notes.mbls.legacy"));
        // The files of Marbles in the data directory (a name that is not a full path is in it)
        foreach (var own in new[]
                 {
                     "settings.ini", "favorites.txt", "history.txt", "bookmarks.txt", "hotlist.txt",
                     "completions.txt", "snippets.txt", "jump.txt"
                 })
        {
            Assert.Equal(DialogText.PasteBinNotThisFile, SystemClipboard.RefusedPasteBin(own));
            Assert.Equal(DialogText.PasteBinNotThisFile, SystemClipboard.RefusedPasteBin(Path.Combine(_data, own)));
        }

        // The files in the folders of Marbles
        foreach (var folder in new[] { "macros", "colorschemes", "logos", "lang", "trash" })
        {
            Assert.Equal(DialogText.PasteBinNotThisFile,
                SystemClipboard.RefusedPasteBin(Path.Combine(_data, folder, "file.txt")));
        }

        Assert.Equal(DialogText.PasteBinNotThisFile,
            SystemClipboard.RefusedPasteBin(Path.Combine(_data, "trash", FileOperations.TrashListName)));
        Assert.Equal(DialogText.PasteBinNotThisFile,
            SystemClipboard.RefusedPasteBin(Path.Combine(SpellChecker.DirectoryName, "da.utf-8.add")));

        // Other files can be
        Assert.Null(SystemClipboard.RefusedPasteBin(SystemClipboard.DefaultPasteBinFile));
        Assert.Null(SystemClipboard.RefusedPasteBin("clip.txt"));
        Assert.Null(SystemClipboard.RefusedPasteBin(Path.Combine(_directory, "other.txt")));
    }

    [Fact]
    public void PasteBin_ADataFileIsNeverWrittenOrEmptied_EvenWhenSetSo()
    {
        var dataFile = Path.Combine(_data, "notes.mbls");
        File.WriteAllText(dataFile, "encrypted");
        // (set past the checks, as by a hand-edited settings file read before this version)
        SystemClipboard.PasteBinFile = dataFile;
        Assert.Equal(DialogText.PasteBinNotDataFile, SystemClipboard.WritePasteBin("yanked", false));
        Assert.Equal(DialogText.PasteBinNotDataFile, SystemClipboard.WritePasteBin("yanked", true));
        Assert.Equal(DialogText.PasteBinNotDataFile, SystemClipboard.ClearPasteBin());
        Assert.Equal("encrypted", File.ReadAllText(dataFile));

        var settings = Path.Combine(_data, FileIo.SettingsFileName);
        File.WriteAllText(settings, "[SETTINGS]");
        SystemClipboard.PasteBinFile = FileIo.SettingsFileName;
        Assert.Equal(DialogText.PasteBinNotThisFile, SystemClipboard.WritePasteBin("yanked", false));
        Assert.Equal("[SETTINGS]", File.ReadAllText(settings));
    }

    [Fact]
    public void PasteBin_AnotherFileWithText_IsAskedAbout()
    {
        SystemClipboard.PasteBinFile = Path.Combine(_directory, "paste.txt");
        File.WriteAllText(SystemClipboard.PasteBinFile, "old yank");
        var other = Path.Combine(_directory, "letter.txt");
        File.WriteAllText(other, "Dear ...");
        var empty = Path.Combine(_directory, "empty.txt");
        File.WriteAllText(empty, string.Empty);
        Assert.True(SystemClipboard.IsOtherFileWithText(other));
        // Not the paste bin itself, an empty file or one that is not there
        Assert.False(SystemClipboard.IsOtherFileWithText(SystemClipboard.PasteBinFile));
        Assert.False(SystemClipboard.IsOtherFileWithText(empty));
        Assert.False(SystemClipboard.IsOtherFileWithText(Path.Combine(_directory, "new.txt")));
    }

    #endregion

    #region The editor

    [Fact]
    public void PutBlock_TheLimitCountsThePaddingOfEachLine()
    {
        // One wide line and many short ones: the register is small, but each line is padded to the widest
        var lines = new List<string> { new('b', 2000) };
        lines.AddRange(Enumerable.Repeat("a", 1999));
        var text = string.Join("\n", lines);
        var editor = Editor(text);
        editor.Type("Q");
        editor.Type("G$y");
        editor.Type("gg100p");
        Assert.Contains("Too much text", editor.Message);
        Assert.Equal(text, editor.Buffer.GetText());
        // A block that fits is put
        var small = Editor("ab\ncd\nef");
        small.Type("QjyP");
        Assert.Equal("aab\nccd\nef", small.Buffer.GetText());
        small.Type("3P");
        Assert.Equal("aaaaab\ncccccd\nef", small.Buffer.GetText());
    }

    [Fact]
    public void PutBlock_TheLimitCountsTheBlankSpaceUpToTheColumn()
    {
        // A narrow block of 10 000 lines put after a line of 2000 characters: the short lines below it get 2000
        // spaces each, twice the limit, though the register is small
        var text = new string('w', 2000) + "\n" + string.Join("\n", Enumerable.Repeat("x", 10000));
        var editor = Editor(text);
        editor.Type("2GQGy");
        editor.Type("gg$p");
        Assert.Contains("Too much text", editor.Message);
        Assert.Equal(text, editor.Buffer.GetText());
    }

    // A pattern that is slow on a long line of "a": each match looks to the end of the line, so the line takes
    // seconds in all, but no match alone is slow enough for the timeout of a match
    private const string SlowPattern = "a(?=a*$)";
    private static readonly string LongLine = new('a', 40_000);

    [Fact]
    public void Substitute_StopsInTime_OnOneLongLineWithManyMatches()
    {
        EditorEngine.CommandTimeoutMilliseconds = 200;
        var editor = Editor("first\n" + LongLine + "\nlast");
        var watch = Stopwatch.StartNew();
        editor.Type(":%s/" + SlowPattern + "/b/g<CR>");
        Assert.True(watch.ElapsedMilliseconds < 5000, "took " + watch.ElapsedMilliseconds + " ms");
        Assert.Equal("Search took too long: nothing was replaced", editor.Message);
        Assert.Equal("first\n" + LongLine + "\nlast", editor.Buffer.GetText());
    }

    [Fact]
    public void Substitute_CountsTheMatches_WithTheReplacementOfDotNet()
    {
        var editor = Editor("one two one\none");
        editor.Type(":%s/(o)ne/[\\1]/g<CR>");
        Assert.Equal("[o] two [o]\n[o]", editor.Buffer.GetText());
        Assert.Equal(Lang.F("Substitutions: {0}, on lines: {1}", 3, 2), editor.Message);
        var first = Editor("aa aa");
        first.Type(":s/a/b/<CR>");
        Assert.Equal("ba aa", first.Buffer.GetText());
        Assert.Equal(Lang.F("Substitutions: {0}, on lines: {1}", 1, 1), first.Message);
    }

    [Fact]
    public void SearchBackward_StopsInTime_OnOneLongLineWithManyMatches()
    {
        EditorEngine.CommandTimeoutMilliseconds = 200;
        var editor = Editor("first\n" + LongLine);
        var watch = Stopwatch.StartNew();
        editor.Type("?" + SlowPattern + "<CR>");
        Assert.True(watch.ElapsedMilliseconds < 5000, "took " + watch.ElapsedMilliseconds + " ms");
        Assert.Equal("Search took too long", editor.Message);
        Assert.Equal(0, editor.Cursor.Line);
    }

    [Fact]
    public void ACountedN_HasOneTimeLimitForAllItsJumps()
    {
        // Each jump is quick, but very many of them on a line with many matches are not
        EditorEngine.CommandTimeoutMilliseconds = 200;
        var editor = Editor(string.Join("\n", Enumerable.Repeat(new string('x', 2000), 50)));
        editor.Type("/x<CR>");
        var watch = Stopwatch.StartNew();
        editor.Type("99999999n");
        Assert.True(watch.ElapsedMilliseconds < 5000, "took " + watch.ElapsedMilliseconds + " ms");
        Assert.Equal("Search took too long", editor.Message);
    }

    [Fact]
    public void TheCountAndTheHighlighting_GiveUpInTime_OnOneLongLine()
    {
        var editor = Editor(LongLine);
        editor.Type("/" + SlowPattern + "<CR>");
        Assert.True(editor.IsSearchHighlighted);
        var watch = Stopwatch.StartNew();
        Assert.False(editor.GetSearchCount(out _, out _));
        Assert.True(watch.ElapsedMilliseconds < 3000, "count took " + watch.ElapsedMilliseconds + " ms");
        watch.Restart();
        editor.StartHighlighting();
        Assert.Empty(editor.SearchMatches(0));
        Assert.True(watch.ElapsedMilliseconds < 3000, "highlighting took " + watch.ElapsedMilliseconds + " ms");
        // Turned off, as for a line that is too slow (n still works)
        Assert.False(editor.IsSearchHighlighted);
    }

    [Fact]
    public void TheHighlighting_StillShowsTheMatchesOfAQuickSearch()
    {
        var editor = Editor("one two one\ntwo");
        editor.Type("/one<CR>");
        editor.StartHighlighting();
        Assert.Equal(new[] { (0, 3), (8, 11) }, editor.SearchMatches(0).ToArray());
        Assert.Empty(editor.SearchMatches(1));
        Assert.True(editor.GetSearchCount(out var current, out var total));
        Assert.Equal((2, 2), (current, total));
    }

    [Fact]
    public void ALongWrappedLine_IsSearchedOncePerRedraw_AndStaysHighlighted()
    {
        // A line of 300 000 characters is shown on many rows, and its matches are asked for twice for each of them
        var editor = Editor(string.Concat(Enumerable.Repeat("some text here ", 20000)));
        editor.Type("/e<CR>");
        editor.StartHighlighting();
        var first = editor.SearchMatches(0).ToArray();
        for (var row = 0; row < 80; row++) Assert.Equal(first.Length, editor.SearchMatches(0).Count());
        Assert.Equal(80000, first.Length);
        Assert.True(editor.IsSearchHighlighted);
        // A changed line is searched again
        editor.Type("0x");
        editor.StartHighlighting();
        Assert.Equal(80000, editor.SearchMatches(0).Count());
        editor.Type("I e<Esc>");
        Assert.Equal(80001, editor.SearchMatches(0).Count());
    }

    #endregion

    #region Logout

    [Fact]
    public void Logout_ForgetsTheTextsOfTheNotesKeptByTheEditorAndTheCommandLines()
    {
        // The block register (and the string memory, which the logout clears too)
        var editor = Editor("secret\ntext");
        editor.Type("Qjly");
        var put = Editor("x");
        put.Type("p");
        Assert.StartsWith("xse\n", put.Buffer.GetText());
        Assert.EndsWith("te", put.Buffer.GetText());
        EditorEngine.ForgetBlockRegister();
        Tools.ClearLineMemory();
        var after = Editor("x");
        after.Type("p");
        Assert.Equal("x", after.Buffer.GetText());

        ReplaceDialog.ForgetTexts();
        Assert.Equal(string.Empty, ReplaceDialog.Find);
        Assert.Equal(string.Empty, ReplaceDialog.Replacement);
        CommandLine.ForgetLastArgument();
        Assert.Equal(string.Empty, CommandLine.LastArgument);

        // All of them are forgotten by the logout itself
        var source = File.ReadAllText(Path.Combine(SourceDirectory(), "Tools.cs"));
        var removePrivileges = source[source.IndexOf("internal static void RemovePrivileges", StringComparison.Ordinal)..];
        removePrivileges = removePrivileges[..removePrivileges.IndexOf("\n    }", StringComparison.Ordinal)];
        foreach (var call in new[]
                 {
                     "ReplaceDialog.ForgetTexts()", "EditorEngine.ForgetBlockRegister()",
                     "CommandLine.ForgetLastArgument()", "Actions.ClearAtLogout()", "ClearScrollback()"
                 })
        {
            Assert.Contains(call, removePrivileges);
        }
    }

    // The folder of the source files of the program
    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Marbles.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "Marbles");
    }

    [Fact]
    public void TheAlternateScreen_IsUsedInATerminalOnLinuxAndMacOS()
    {
        Assert.True(Tools.UsesAlternateScreen(false, false, "xterm-256color"));
        Assert.True(Tools.UsesAlternateScreen(false, false, null));
        // Not to a file or a pipe, not in a terminal without escape sequences, and not on Windows
        Assert.False(Tools.UsesAlternateScreen(false, true, "xterm"));
        Assert.False(Tools.UsesAlternateScreen(false, false, "dumb"));
        Assert.False(Tools.UsesAlternateScreen(true, false, "xterm"));
        Assert.Equal("\u001b[?1049h", Tools.AlternateScreenOn);
        Assert.Equal("\u001b[?1049l", Tools.AlternateScreenOff);
        Assert.Equal("\u001b[3J", Tools.ClearScrollbackSequence);
    }

    [Fact]
    public void TheScrollback_IsClearedOnlyWhenMarblesIsNotOnTheAlternateScreen()
    {
        // On the alternate screen nothing went to the scrollback, and clearing it would take the shell's history
        Assert.False(Tools.ClearsScrollback(true, true));
        // The alternate screen could not be used (or was left): what Marbles showed may be in the scrollback
        Assert.True(Tools.ClearsScrollback(true, false));
        // Windows (the console clears all of its buffer), a file or a pipe
        Assert.False(Tools.ClearsScrollback(false, false));
    }

    [Theory]
    [InlineData(@"C:\data\notes.mbls::$DATA", true)]
    [InlineData(@"C:\data\settings.ini:stream", true)]
    [InlineData(@"\\?\C:\data\notes.mbls:x", true)]
    [InlineData(@"C:\data\paste-bin.txt", false)]
    [InlineData(@"\\?\C:\data\paste-bin.txt", false)]
    [InlineData(@"\\server\share\paste-bin.txt", false)]
    public void AStreamOfAFile_IsNamedByAColonAfterTheDrive(string path, bool stream)
    {
        Assert.Equal(stream, SystemClipboard.NamesAStream(path));
    }

    #endregion

    #region Files

    [Fact]
    public void AnEmptyFile_OpensEmpty_AndCanBeSaved()
    {
        var path = Path.Combine(_directory, "empty.txt");
        File.WriteAllBytes(path, []);
        var file = TextFile.Load(path);
        Assert.Equal(string.Empty, file.Text);
        FileIo.WriteAllBytesSafe(path, file.GetBytes("now with text"));
        Assert.Equal("now with text", TextFile.Load(path).Text);
    }

    [Fact]
    public void ReadBytes_StopsAfterTheLimit()
    {
        var path = Path.Combine(_directory, "large.txt");
        File.WriteAllBytes(path, new byte[1001]);
        Assert.Throws<InvalidDataException>(() => TextFile.ReadBytes(path, 1000));
        Assert.Equal(1001, TextFile.ReadBytes(path, 1001).Length);
        File.WriteAllText(path, "abc");
        Assert.Equal("abc"u8.ToArray(), TextFile.ReadBytes(path, 1000));
    }

    [Fact]
    public void ReadBytes_AFileOpenForWritingInAnotherProgram_CanBeRead()
    {
        var path = Path.Combine(_directory, "live.log");
        using var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        writer.Write("line one\n"u8);
        writer.Flush();
        Assert.Equal("line one\n", TextFile.Load(path).Text);
    }

    // (/dev/zero tells a length of 0, and would never end if it was read)
    [UnixFact]
    public void ASpecialFile_IsNotRead()
    {
        Assert.Equal(string.Empty, TextFile.Load("/dev/zero").Text);
        Assert.Empty(TextFile.ReadBytes("/dev/zero", TextFile.MaxSize));
    }

    [Fact]
    public void TheTextSearch_SkipsTheFoldersOfTheSystem()
    {
        Assert.True(FileSearch.IsSystemDirectory("/proc", true));
        Assert.True(FileSearch.IsSystemDirectory("/sys/", true));
        Assert.True(FileSearch.IsSystemDirectory("/dev", true));
        Assert.False(FileSearch.IsSystemDirectory("/home/dev", true));
        Assert.False(FileSearch.IsSystemDirectory("/devices", true));
        Assert.False(FileSearch.IsSystemDirectory("/dev", false));

        // An empty file is not searched (and has nothing to find)
        File.WriteAllText(Path.Combine(_directory, "empty.txt"), string.Empty);
        File.WriteAllText(Path.Combine(_directory, "full.txt"), "find me");
        var outcome = FileSearch.Search(_directory, new FileSearch.Options { Text = "e", IsRegex = false });
        Assert.Equal(new[] { "full.txt" }, outcome.Results.Select(result => Path.GetFileName(result.Path)));
    }

    [Fact]
    public void Copy_ADirectoryWithAnEmptyFile_CopiesIt()
    {
        var source = Path.Combine(_directory, "source");
        Directory.CreateDirectory(Path.Combine(source, "sub"));
        File.WriteAllText(Path.Combine(source, "empty.txt"), string.Empty);
        File.WriteAllText(Path.Combine(source, "sub", "full.txt"), "text");
        var target = Path.Combine(_directory, "target");
        Directory.CreateDirectory(target);
        Assert.Equal(DialogText.Success, FileOperations.Copy(source, target, out var copy));
        Assert.Equal(string.Empty, File.ReadAllText(Path.Combine(copy, "empty.txt")));
        Assert.Equal("text", File.ReadAllText(Path.Combine(copy, "sub", "full.txt")));
    }

    // A junction on Windows (made without administrator rights), a symbolic link elsewhere
    private static bool MakeDirectoryLink(string link, string target)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(link, target);
                return true;
            }

            using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
                                      System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [Fact]
    public void Copy_IntoItselfThroughAJunctionOrALink_IsRefused()
    {
        var source = Path.Combine(_directory, "project");
        Directory.CreateDirectory(Path.Combine(source, "inner"));
        File.WriteAllText(Path.Combine(source, "inner", "file.txt"), "text");
        // A link outside the directory that leads into it
        var link = Path.Combine(_directory, "shortcut");
        if (!MakeDirectoryLink(link, Path.Combine(source, "inner")))
        {
            // (no junctions or links here: nothing to test)
            return;
        }

        Assert.True(FileOperations.IntoItself(source, link, true));
        Assert.Equal(DialogText.FileIntoItself, FileOperations.Copy(source, link, out _));
        Assert.Equal(DialogText.FileIntoItself, FileOperations.Move(source, link, out _));
        Assert.False(Directory.Exists(Path.Combine(source, "inner", "project")));
        // The real path follows the link (both sides with links followed: on macOS the temp folder "/var/..." is
        // "/private/var/...")
        Assert.True(FileOperations.SamePath(FileOperations.RealPath(Path.Combine(link, "file.txt")),
            FileOperations.RealPath(Path.Combine(source, "inner", "file.txt"))));

        // The link itself can be moved into the directory it leads to (only the link moves)
        Assert.False(FileOperations.IntoItself(link, source, false));
        // Another directory is copied as before
        var other = Path.Combine(_directory, "other");
        Directory.CreateDirectory(other);
        Assert.Equal(DialogText.Success, FileOperations.Copy(source, other, out var copy));
        Assert.True(File.Exists(Path.Combine(copy, "inner", "file.txt")));
    }

    #endregion
}
