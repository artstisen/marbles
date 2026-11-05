using System.Text.RegularExpressions;

namespace Marbles.Tests;

/// <summary>
/// Files are written safely: a file that is there is replaced with FileIo.WriteAllBytesSafe, a new file is made with
/// FileMode.CreateNew (FileIo.CreateNewFile), and a list that only grows is appended to. The source is read for the
/// ways of writing that would empty a file first, so that an interrupted write could leave it empty or half written
/// </summary>
[Collection("Static state")]
public class SafeWriteTests
{
    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Marbles.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "Marbles");
    }

    // The ways of writing that empty a file before the new content is in it (also the Async ones, and those of
    // FileInfo: info.CreateText(), info.OpenWrite())
    private static readonly Regex Unsafe = new(
        @"\bFile\.(WriteAllText|WriteAllBytes|WriteAllLines|Create|CreateText|OpenWrite)(Async)?\s*\(|" +
        @"FileMode\.(Create|Truncate|OpenOrCreate)\b|new\s+StreamWriter\s*\(|\.(CreateText|OpenWrite)\s*\(\s*\)");

    // A stream opened with a mode: the mode is named on the line (and then checked by Unsafe), or it is set in a
    // FileStreamOptions of that name in the file (where "Mode = FileMode..." is checked by Unsafe)
    private static readonly Regex Opened = new(@"\b(File\.Open|File\.OpenHandle|new\s+FileStream)\s*\(\s*[^,]+,\s*(\w+)\s*[,)]");

    // File.Create with FileOptions.DeleteOnClose (FileIo.CanWriteIn tests a directory with a file that is gone
    // when it is closed): the only use of File.Create that is allowed
    private static bool IsAllowed(string line, Match match) =>
        match.Value.StartsWith("File.Create(", StringComparison.Ordinal) && line.Contains("FileOptions.DeleteOnClose");

    [Fact]
    public void FilesAreNotEmptiedBeforeTheyAreWritten()
    {
        var root = SourceDirectory();
        var found = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            found.AddRange(UnsafeLines(File.ReadAllText(path))
                .Select(line => Path.GetRelativePath(root, path) + ":" + line));
        }

        Assert.True(found.Count == 0, "Use FileIo.WriteAllBytesSafe (or FileIo.CreateNewFile for a new file):\n" +
                                      string.Join("\n", found));
    }

    // The lines of a source file that write in a way that empties a file first ("number: line")
    private static List<string> UnsafeLines(string text)
    {
        var found = new List<string>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var where = (i + 1) + ": " + line.Trim();
            if (Unsafe.Matches(line).Any(match => !IsAllowed(line, match))) found.Add(where);
            // A stream opened with a mode that is not named, and not set in FileStreamOptions of the file
            if (Opened.Match(line) is { Success: true } opened && !line.Contains("FileMode.") &&
                !Regex.IsMatch(text, @"\b" + opened.Groups[2].Value + @"\s*=\s*new\s+FileStreamOptions\b"))
            {
                found.Add(where + " (the mode is not seen)");
            }
        }

        return found;
    }

    [Theory]
    [InlineData("File.WriteAllTextAsync(path, text);")]
    [InlineData("await File.WriteAllBytesAsync(path, bytes);")]
    [InlineData("using var writer = info.CreateText();")]
    [InlineData("using var stream = new FileInfo(path).OpenWrite();")]
    [InlineData("using var stream = new FileStream(path, mode);")]
    [InlineData("using var stream = File.Open(path, mode, FileAccess.Write);")]
    [InlineData("var options = new FileStreamOptions { Mode = FileMode.Create };")]
    [InlineData("using (File.Create(path)) { }")]
    public void TheWaysOfWritingThatEmptyAFile_AreSeen(string line)
    {
        Assert.Single(UnsafeLines(line));
    }

    [Theory]
    [InlineData("using var stream = new FileStream(path, FileMode.Open);")]
    [InlineData("using (File.Create(test, 1, FileOptions.DeleteOnClose)) { }")]
    [InlineData("var options = new FileStreamOptions { Mode = FileMode.CreateNew };\nnew FileStream(path, options);")]
    public void TheSafeWays_AreNotSeen(string text)
    {
        Assert.Empty(UnsafeLines(text));
    }

    [Fact]
    public void ANewFile_IsNeverMadeOverOneThatIsThere()
    {
        var directory = Path.Combine(Path.GetTempPath(), "marbles_new_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "notes.md");
            FileIo.CreateNewFile(path);
            Assert.Equal(0, new FileInfo(path).Length);
            // One that has come since it was looked for is not emptied
            File.WriteAllText(path, "came in the meantime");
            Assert.ThrowsAny<IOException>(() => FileIo.CreateNewFile(path));
            Assert.Equal("came in the meantime", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SafeWrite_ReplacesTheFile_AndLeavesNothingBehind()
    {
        var directory = Path.Combine(Path.GetTempPath(), "marbles_safe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "chapter.md");
            File.WriteAllText(path, "old text");
            FileIo.WriteAllBytesSafe(path, "new text"u8.ToArray());
            Assert.Equal("new text", File.ReadAllText(path));
            // Only the file: the temporary file it was written to is gone
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    // (a read-only file cannot be replaced on Windows; on Linux and macOS see LimitTests)
    [WindowsFact]
    public void SafeWrite_ThatFails_LeavesTheFileAsItWas()
    {
        var directory = Path.Combine(Path.GetTempPath(), "marbles_safe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "chapter.md");
        try
        {
            File.WriteAllText(path, "old text");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            Assert.ThrowsAny<Exception>(() => FileIo.WriteAllBytesSafe(path, "new text"u8.ToArray()));
            Assert.Equal("old text", File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(directory, true);
        }
    }
}