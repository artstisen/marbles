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
    public void YankAll_YanksAllTheText_AsAYank()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.ClipboardEveryYank = true;
        editor.Type("j$");
        Assert.Equal(EditorRequest.Clipboard, editor.Type(":yankall<CR>"));
        Assert.Equal("one\ntwo\nthree\n", editor.ClipboardText);
        Assert.Equal("one\ntwo\nthree", editor.Buffer.GetText());
        Assert.Equal(0, editor.Cursor.Line);
        // What is yanked is put as lines
        editor.Type("Gp");
        Assert.Equal("one\ntwo\nthree\none\ntwo\nthree", editor.Buffer.GetText());
        // Only with "+ in front when every yank is not sent to the clipboard
        var other = Editor("text");
        other.ClipboardEveryYank = false;
        Assert.Equal(EditorRequest.None, other.Type(":yankall<CR>"));
        Assert.Equal(EditorRequest.Clipboard, other.Type("\"+:yankall<CR>"));
        Assert.Equal("text\n", other.ClipboardText);
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
        // ":info": the size, the dates and the path of the text, in a box
        Assert.Equal(EditorRequest.DocumentInfo, editor.Type(":info<CR>"));
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
    public void ANameOrPath_GoesToTheStringMemory_ForPInTheEditor()
    {
        // Also with the clipboard and the paste bin turned off
        SystemClipboard.Osc52 = false;
        SystemClipboard.PasteBin = false;
        Assert.Equal((DialogText.CopiedToMemory, false), SystemClipboard.CopyWithMemory("notes.md"));
        Assert.Equal("notes.md", Tools.GetCurrentLineFromLineMemory());
        var editor = Editor("see ");
        editor.Type("$p");
        Assert.Equal("see notes.md", editor.GetText());

        // It is the current string after a yank, and the yank is still in the memory
        editor.Type("0yiw");
        SystemClipboard.PasteBin = true;
        Assert.Equal((DialogText.CopiedToPasteBinAndMemory, false), SystemClipboard.CopyWithMemory("/home/lars"));
        Assert.Equal("/home/lars", Tools.GetCurrentLineFromLineMemory());
        Assert.Contains("see", Tools.GetAllLinesFromLineMemory());
        SystemClipboard.ReadPasteBin(out var text);
        Assert.Equal("/home/lars", text);
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

    [Fact]
    public void ALongPath_IsShownInFull_OnRows()
    {
        var path = Path.Combine("C:", "a folder", new string('x', 90), "paste-bin.txt");
        var rows = ChoiceBox.Rows(path, 30);
        Assert.All(rows, row => Assert.True(row.Length <= 30));
        // A row broken at a blank space loses only that space
        Assert.Equal(path.Replace(" ", string.Empty), string.Concat(rows).Replace(" ", string.Empty));
        Assert.Equal(new[] { "short" }, ChoiceBox.Rows("short", 30));
        // An empty value (a blank row between parts of a box) is one empty row
        Assert.Equal(new[] { string.Empty }, ChoiceBox.Rows(string.Empty, 30));
        // A text (what :bom tells) goes on at the blank space between words, not inside a word
        Assert.Equal(new[] { "A byte order mark (BOM) is", "three bytes" }, ChoiceBox.Rows("A byte order mark (BOM) is three bytes", 30));
    }

    [Theory]
    // CJK: two cells each
    [InlineData("日本語のファイル名はとても長いのでいくつかの行に分かれます.txt")]
    [InlineData("文書 目録 写真 音楽 映画 資料 予定 日記 手紙 報告.md")]
    // Emoji (two chars each, two cells), with and without blank space
    [InlineData("😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀😀.txt")]
    [InlineData("notes 😀 about 🎉 the party 👍 with 🚀 rockets and 🌍 earth.md")]
    // A family emoji is one character of several chars
    [InlineData("a👨‍👩‍👧‍👦b👨‍👩‍👧‍👦c👨‍👩‍👧‍👦d👨‍👩‍👧‍👦e👨‍👩‍👧‍👦f👨‍👩‍👧‍👦.txt")]
    public void WideCharacters_FitTheRowsOfABox_AndAreNeverSplit(string value)
    {
        foreach (var room in new[] { 7, 10, 13, 30 })
        {
            var rows = ChoiceBox.Rows(value, room);
            // In cells of the screen, not chars
            Assert.All(rows, row => Assert.True(TextMetrics.WidthOf(row) <= room, row));
            // No half of a surrogate pair at the end or start of a row
            Assert.All(rows, row => Assert.False(row.Length > 0 && (char.IsHighSurrogate(row[^1]) || char.IsLowSurrogate(row[0])), row));
            // All of it is there (a row broken at a blank space loses only that space)
            Assert.Equal(value.Replace(" ", string.Empty), string.Concat(rows).Replace(" ", string.Empty));
        }

        // A row narrower than a wide character still has it, and goes on
        Assert.Equal(new[] { "日", "本" }, ChoiceBox.Rows("日本", 1));
    }

    [Fact]
    public void ABoxHigherThanTheWindow_EndsWithDots_OnTheLastRowShown()
    {
        var lines = new List<(string Label, string Value)> { ("A:", "one"), ("B:", "two"), ("C:", "three") };
        Assert.Same(lines, ChoiceBox.FitRows(lines, 3, 20));
        var fit = ChoiceBox.FitRows(lines, 2, 20);
        Assert.Equal(new[] { ("A:", "one"), ("B:", "two...") }, fit);
        // Also when the value fills the row
        fit = ChoiceBox.FitRows(new List<(string, string)> { ("A:", "日本語日本語"), ("B:", "x") }, 1, 8);
        Assert.Equal("日本...", fit[0].Value);
        Assert.True(TextMetrics.WidthOf(fit[0].Value) <= 8);
    }
}
