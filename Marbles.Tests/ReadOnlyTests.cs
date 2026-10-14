namespace Marbles.Tests;

/// <summary>
/// Entries that are read only: a tag in their options, and an editor that does not change their text
/// </summary>
[Collection("Static state")]
public class ReadOnlyTests
{
    public ReadOnlyTests()
    {
        Tools.ClearLineMemory(); // The registers are shared by all editors
    }

    private static EditorEngine ReadOnlyEditor(string text) => new(text, 40, 10, false) { ReadOnly = true };

    [Fact]
    public void ReadOnly_IsATagInTheOptions_KeptWithTheOtherTags()
    {
        var entry = new Entry("Title", "Text") { Options = "fp3Em" };
        Assert.False(Entries.IsReadOnly(entry));
        Entries.SetReadOnly(entry, true);
        Assert.True(Entries.IsReadOnly(entry));
        Assert.Equal("fp3Emr", entry.Options);
        Assert.Equal(SyntaxType.Markdown, EntrySyntax.Get(entry.Options));
        // Set twice: one tag
        Entries.SetReadOnly(entry, true);
        Assert.Equal("fp3Emr", entry.Options);
        Entries.SetReadOnly(entry, false);
        Assert.Equal("fp3Em", entry.Options);
        Assert.False(Entries.IsReadOnly(entry));
        // An entry without options, and a cleared one (whose options are a text)
        Assert.False(Entries.IsReadOnly(new Entry("A", "B") { Options = string.Empty }));
        Assert.False(Entries.IsReadOnly(new Entry("A", "B") { Options = DialogText.Cleared }));
    }

    [Fact]
    public void TheEditor_DoesNotChangeAReadOnlyText_AndSaysWhy()
    {
        var editor = ReadOnlyEditor("one two\nthree");
        foreach (var keys in new[] { "x", "dd", "ihello", "ohello", "Atext", "cwnew", "p", ">>", "J", "~", "rX", ":sort<CR>", ":trim<CR>", "u" })
        {
            editor.Type(keys);
            Assert.Equal("one two\nthree", editor.Buffer.GetText());
            Assert.Equal(EditorMode.Normal, editor.Mode);
        }

        editor.Type("i");
        Assert.Equal(EditorMode.Normal, editor.Mode);
        Assert.True(editor.MessageIsError);
        Assert.Equal(DialogText.EditorReadOnly, editor.Message);
        Assert.False(editor.IsModified);
    }

    [Fact]
    public void ReadingAReadOnlyText_StillWorks()
    {
        var editor = ReadOnlyEditor("one two\nthree");
        editor.Type("wyiw");
        Assert.False(editor.MessageIsError);
        editor.Type("j/thr<CR>");
        Assert.Equal(1, editor.Cursor.Line);
        Assert.Equal(EditorRequest.DocumentInfo, editor.Type(":info<CR>"));
        Assert.False(editor.MessageIsError);
        // What was yanked can be put in a text that is not read only, which is changed as usual
        var other = new EditorEngine("one", 40, 10, false);
        other.Type("$p");
        Assert.Equal("onetwo", other.Buffer.GetText());
        other.Type("x");
        Assert.Equal("onetw", other.Buffer.GetText());
    }

    [Fact]
    public void AReadOnlyText_CanHaveItsOwnMessage()
    {
        // A peek of the file browser tells that Enter opens the file
        var editor = new EditorEngine("one", 40, 10, false) { ReadOnly = true, ReadOnlyMessage = DialogText.PeekReadOnly };
        editor.Type("x");
        Assert.Equal("one", editor.Buffer.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(DialogText.PeekReadOnly, editor.Message);
    }

    [Fact]
    public void APeek_ShowsAllOfASmallFile()
    {
        Assert.Equal("one\ntwo\n", FileExplorer.PeekText("one\ntwo\n"));
    }

    [Fact]
    public void APeek_ShowsTheStartOfALargeFile_AndTellsThatThereIsMore()
    {
        var lines = Enumerable.Range(1, FileExplorer.PeekMaxLines + 50).Select(i => "line " + i);
        var peek = FileExplorer.PeekText(string.Join("\n", lines));
        var shown = peek.Split('\n');
        Assert.Equal("line 1", shown[0]);
        Assert.Equal("line " + FileExplorer.PeekMaxLines, shown[FileExplorer.PeekMaxLines - 1]);
        Assert.Equal(DialogText.PeekMore, shown[^1]);
        Assert.Equal(FileExplorer.PeekMaxLines + 2, shown.Length);

        // Long lines: no more than the size of a peek, cut between lines
        var wide = string.Join("\n", Enumerable.Repeat(new string('x', 999), 300));
        peek = FileExplorer.PeekText(wide);
        Assert.True(peek.Length < FileExplorer.PeekMaxCharacters + 100);
        Assert.All(peek.Split('\n').SkipLast(2), line => Assert.Equal(999, line.Length));
    }
}
