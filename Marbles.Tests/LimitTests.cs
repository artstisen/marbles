namespace Marbles.Tests;

/// <summary>
/// Limits brought over from Blyant: ":dup" puts in no more than "p" does, a ":s" that takes too long changes
/// nothing, the paste bin is replaced safely, and a file that may not be written is not replaced (Linux, macOS)
/// </summary>
[Collection("Static state")]
public class LimitTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "marbles-limits-" + Guid.NewGuid().ToString("N"));
    private readonly string _savedFile = SystemClipboard.PasteBinFile;
    private readonly bool _savedOsc52 = SystemClipboard.Osc52;
    private readonly bool _savedPasteBin = SystemClipboard.PasteBin;
    private readonly bool _savedAppend = SystemClipboard.PasteBinAppend;

    public LimitTests()
    {
        Directory.CreateDirectory(_directory);
        SystemClipboard.PasteBinFile = Path.Combine(_directory, "paste-bin.txt");
    }

    public void Dispose()
    {
        SystemClipboard.PasteBinFile = _savedFile;
        SystemClipboard.Osc52 = _savedOsc52;
        SystemClipboard.PasteBin = _savedPasteBin;
        SystemClipboard.PasteBinAppend = _savedAppend;
        if (Directory.Exists(_directory))
        {
            foreach (var file in Directory.EnumerateFiles(_directory)) File.SetAttributes(file, FileAttributes.Normal);
            if (!OperatingSystem.IsWindows())
            {
                foreach (var file in Directory.EnumerateFiles(_directory))
                {
                    File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }

            Directory.Delete(_directory, true);
        }
    }

    [Fact]
    public void Dup_PutsInNoMoreThanPutDoes()
    {
        var line = new string('a', 100_000);
        var editor = new EditorEngine(line, 40, 10, false);
        editor.Type(":dup 200<CR>");
        Assert.Equal(1, editor.Buffer.LineCount);
        Assert.True(editor.MessageIsError);
        editor.Type(":dup 3<CR>");
        Assert.Equal(4, editor.Buffer.LineCount);
    }

    [Fact]
    public void AReplacementThatTakesTooLong_ChangesNothing()
    {
        // The last line is found and replaced first (from the bottom); the first takes too long, so it is put back
        var text = new string('x', 30) + "\nmid\nxy";
        var editor = new EditorEngine(text, 40, 10, false);
        editor.Type(":%s/(x+x+)+y/z/<CR>");
        Assert.Equal(text, editor.Buffer.GetText());
        Assert.True(editor.MessageIsError);
    }

    [Fact]
    public void ThePasteBin_IsReplacedWhole_AndAddedTo()
    {
        SystemClipboard.Osc52 = false;
        SystemClipboard.PasteBin = true;
        SystemClipboard.PasteBinAppend = false;
        Assert.False(SystemClipboard.Copy("first", false, false).IsError);
        Assert.False(SystemClipboard.Copy("second", false, false).IsError);
        Assert.Equal("second", File.ReadAllText(SystemClipboard.PasteBinPath).TrimEnd('\r', '\n'));
        SystemClipboard.PasteBinAppend = true;
        Assert.False(SystemClipboard.Copy("third", false, false).IsError);
        Assert.Equal("second" + Environment.NewLine + "third",
            File.ReadAllText(SystemClipboard.PasteBinPath).TrimEnd('\r', '\n'));
        Assert.Equal(DialogText.Success, SystemClipboard.ClearPasteBin());
        Assert.Equal(string.Empty, File.ReadAllText(SystemClipboard.PasteBinPath));
        // Only the paste bin: no temporary file is left
        Assert.Single(Directory.EnumerateFiles(_directory));
    }

    // (UnixFact skips it on Windows; the attribute tells the analyzer, as File.SetUnixFileMode is not on Windows)
    [UnixFact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void AFileThatMayNotBeWritten_IsNotReplaced()
    {
        var path = Path.Combine(_directory, "locked.md");
        File.WriteAllText(path, "kept");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        Assert.Throws<UnauthorizedAccessException>(() => FileIo.WriteAllBytesSafe(path, "new"u8.ToArray()));
        Assert.Equal("kept", File.ReadAllText(path));
        Assert.Single(Directory.EnumerateFiles(_directory));
    }
}