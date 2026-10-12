namespace Marbles.Tests;

/// <summary>
/// Yanks to the system clipboard ("+y, or every yank), and the paste bin (:pb, :pbappend, :pbread, :pbclear)
/// </summary>
[Collection("Static state")]
public class ClipboardTests : IDisposable
{
    private readonly string _directory;
    private readonly string _savedFile = SystemClipboard.PasteBinFile;
    private readonly bool _savedOsc52 = SystemClipboard.Osc52;
    private readonly bool _savedPasteBin = SystemClipboard.PasteBin;
    private readonly bool _savedNotes = SystemClipboard.FromNotes;

    public ClipboardTests()
    {
        Tools.ClearLineMemory();
        _directory = Path.Combine(Path.GetTempPath(), "marbles_clipboard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SystemClipboard.PasteBinFile = Path.Combine(_directory, "paste-bin.txt");
    }

    public void Dispose()
    {
        SystemClipboard.PasteBinFile = _savedFile;
        SystemClipboard.Osc52 = _savedOsc52;
        SystemClipboard.PasteBin = _savedPasteBin;
        SystemClipboard.FromNotes = _savedNotes;
        Directory.Delete(_directory, true);
    }

    private static EditorEngine Editor(string text) => new(text, 40, 10, false);

    [Fact]
    public void PlusY_SendsTheYankToTheClipboard_AndAPlainYankDoesNot()
    {
        var editor = Editor("one two\nthree");
        Assert.Equal(EditorRequest.None, editor.Type("yw"));
        Assert.Equal(EditorRequest.Clipboard, editor.Type("\"+yw"));
        Assert.Equal("one ", editor.ClipboardText);
        Assert.Equal(EditorRequest.Clipboard, editor.Type("\"+yy"));
        Assert.Equal("one two\n", editor.ClipboardText);
        // In Visual mode, and "* as "+
        Assert.Equal(EditorRequest.Clipboard, editor.Type("jv$\"*y"));
        Assert.Equal("three", editor.ClipboardText);
        // "+ is for the next command only
        Assert.Equal(EditorRequest.None, editor.Type("\"+jyy"));
        // The yank still goes to the string memory as always
        Assert.Equal("three\n", Tools.GetCurrentLineFromLineMemory());
    }

    [Fact]
    public void EveryYank_GoesToTheClipboard_ButNotDeletes()
    {
        var editor = Editor("one two");
        editor.ClipboardEveryYank = true;
        Assert.Equal(EditorRequest.Clipboard, editor.Type("yw"));
        Assert.Equal("one ", editor.ClipboardText);
        Assert.Equal(EditorRequest.None, editor.Type("dw"));
        Assert.Equal(EditorRequest.None, editor.Type("\"+dw"));
    }

    [Fact]
    public void AnotherRegister_IsNotUsed()
    {
        var editor = Editor("one");
        editor.Type("\"a");
        Assert.True(editor.MessageIsError);
        // And the yank after it is not sent to the clipboard
        Assert.Equal(EditorRequest.None, editor.Type("yy"));
    }

    [Fact]
    public void Pb_WritesTheSelection_OrTheWholeText()
    {
        var editor = Editor("one two three\nfour");
        Assert.Equal(EditorRequest.PasteBinWrite, editor.Type(":pb<CR>"));
        Assert.Equal("one two three\nfour", editor.PasteBinText);
        Assert.Null(editor.PasteBinAppend);
        Assert.Equal(EditorRequest.PasteBinWrite, editor.Type("wviw:pb<CR>"));
        Assert.Equal("two", editor.PasteBinText);
        Assert.Equal(EditorRequest.PasteBinWrite, editor.Type("jV:pbappend<CR>"));
        Assert.Equal("four", editor.PasteBinText);
        Assert.True(editor.PasteBinAppend);
        Assert.Equal(EditorRequest.PasteBinRead, editor.Type(":pbread<CR>"));
        Assert.Equal(EditorRequest.PasteBinClear, editor.Type(":pbclear<CR>"));
        Assert.Equal(EditorRequest.ClipboardSettings, editor.Type(":clipboard<CR>"));
    }

    [Fact]
    public void ThePasteBin_IsWrittenAddedToReadAndEmptied()
    {
        Assert.Equal(DialogText.PasteBinEmpty, SystemClipboard.ReadPasteBin(out _));
        Assert.Equal(DialogText.Success, SystemClipboard.WritePasteBin("one\ntwo", false));
        Assert.Equal(DialogText.Success, SystemClipboard.WritePasteBin("three", true));
        Assert.Equal("one" + Environment.NewLine + "two" + Environment.NewLine + "three" + Environment.NewLine,
            File.ReadAllText(SystemClipboard.PasteBinFile));
        Assert.Equal(DialogText.Success, SystemClipboard.ReadPasteBin(out var text));
        Assert.Equal("one\ntwo\nthree", text);
        Assert.Equal(DialogText.Success, SystemClipboard.WritePasteBin("new", false));
        SystemClipboard.ReadPasteBin(out text);
        Assert.Equal("new", text);
        Assert.Equal(DialogText.Success, SystemClipboard.ClearPasteBin());
        Assert.Equal(DialogText.PasteBinEmpty, SystemClipboard.ReadPasteBin(out _));
    }

    [Fact]
    public void Copy_RespectsTheSettings()
    {
        SystemClipboard.Osc52 = false;
        SystemClipboard.PasteBin = false;
        Assert.Equal((DialogText.ClipboardAllOff, true), SystemClipboard.Copy("x", false, true));
        Assert.Equal((string.Empty, false), SystemClipboard.Copy("x", false, false));
        SystemClipboard.PasteBin = true;
        SystemClipboard.FromNotes = false;
        Assert.Equal((DialogText.ClipboardNotesOff, true), SystemClipboard.Copy("x", true, true));
        Assert.False(File.Exists(SystemClipboard.PasteBinFile));
        Assert.Equal((DialogText.CopiedToPasteBin, false), SystemClipboard.Copy("from a file", false, true));
        SystemClipboard.ReadPasteBin(out var text);
        Assert.Equal("from a file", text);
    }

    [Fact]
    public void ThePasteBin_IsInTheDataDirectory_UnlessAFullPathIsGiven()
    {
        var data = Path.GetFullPath(FileIo.GetDataDirectoryName());
        Assert.Equal(Path.Combine(data, "paste-bin.txt"), SystemClipboard.FullPath("paste-bin.txt"));
        Assert.Equal(Path.Combine(data, "clips", "pb.txt"), SystemClipboard.FullPath(Path.Combine("clips", "pb.txt")));
        var full = Path.Combine(_directory, "pb.txt");
        Assert.Equal(full, SystemClipboard.FullPath(full));
        SystemClipboard.PasteBinFile = "paste-bin.txt";
        Assert.Equal(Path.Combine(data, "paste-bin.txt"), SystemClipboard.PasteBinPath);
    }

    [Fact]
    public void Osc52_IsTheTextInBase64()
    {
        Assert.Equal("\u001b]52;c;w6Z4w7g=\u0007", SystemClipboard.Osc52Sequence("æxø"));
    }
}
