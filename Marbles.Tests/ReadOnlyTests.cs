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
}
