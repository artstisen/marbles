using System.Globalization;

namespace Marbles.Tests;

/// <summary>
/// Fixes of the editor brought over from Blyant: blocks of code in Markdown closed by their own kind of fence, case
/// changes and smartcase, texts cut for the screen, the layout that follows edits inside lines, options given at
/// startup, and a bookmark added in the Jump menu after one was removed
/// </summary>
[Collection("Static state")]
public class EditorFixTests : IDisposable
{
    private const string Codes = ".123456BICKQLAUR#SWN9";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "marbles-editorfix-" + Guid.NewGuid().ToString("N"));
    private readonly string? _bookmarksPath = Bookmarks.FilePathOverride;

    public EditorFixTests()
    {
        Directory.CreateDirectory(_directory);
        Bookmarks.FilePathOverride = Path.Combine(_directory, Bookmarks.FileName);
        Bookmarks.Clear();
    }

    public void Dispose()
    {
        Bookmarks.FilePathOverride = _bookmarksPath;
        Bookmarks.Clear();
        Directory.Delete(_directory, true);
    }

    private static string[] Styles(SyntaxType type, params string[] lines)
    {
        var result = new string[lines.Length];
        var state = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var styles = SyntaxHighlighter.Highlight(type, lines[i], state, out state);
            result[i] = styles == null
                ? new string('.', lines[i].Length)
                : string.Concat(styles.Select(style => Codes[(int)style]));
        }

        return result;
    }

    [Fact]
    public void ABlockOfCode_EndsOnlyAtAFenceOfItsOwnKind()
    {
        var tilde = MarkdownHighlighter.NextFenceState("~~~", 0);
        Assert.NotEqual(0, tilde);
        Assert.Equal(tilde, MarkdownHighlighter.NextFenceState("```", tilde));
        Assert.Equal(0, MarkdownHighlighter.NextFenceState("~~~", tilde));
        // A longer fence is closed by one at least as long, with nothing after it
        var four = MarkdownHighlighter.NextFenceState("````md", 0);
        Assert.Equal(four, MarkdownHighlighter.NextFenceState("```", four));
        Assert.Equal(four, MarkdownHighlighter.NextFenceState("```` x", four));
        Assert.Equal(0, MarkdownHighlighter.NextFenceState("`````", four));
        // In the editor: the heading after the block is a heading again
        var lines = Styles(SyntaxType.Markdown, "~~~", "```", "code", "~~~", "# Title");
        Assert.Equal("KKKK", lines[2]);
        Assert.Equal("1111111", lines[4]);
    }

    [Fact]
    public void ChangingCase_IsTheSameInEveryLanguageOfTheComputer()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            var editor = new EditorEngine("istanbul IZMIR", 40, 10, false);
            editor.Type("gUiw");
            Assert.Equal("ISTANBUL IZMIR", editor.Buffer.GetText());
            editor.Type("wguiw");
            Assert.Equal("ISTANBUL izmir", editor.Buffer.GetText());
            editor.Type("0g~iw");
            Assert.Equal("istanbul izmir", editor.Buffer.GetText());
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Smartcase_DoesNotCountTheLetterAfterABackslash()
    {
        // \S is a class: "foo" still ignores case
        var editor = new EditorEngine("abc\nxFOO", 40, 10, false);
        editor.Type("/\\Sfoo<CR>");
        Assert.Equal(1, editor.Cursor.Line);
        // An uppercase letter of its own makes the search keep case
        editor.Type("gg/\\SFoo<CR>");
        Assert.Equal(0, editor.Cursor.Line);
    }

    [Theory]
    [InlineData("abcdefghij", 10, true, "abcdefghij")]
    [InlineData("abcdefghijk", 10, true, "abcdefg...")]
    [InlineData("abcdefghijk", 10, false, "abcdefghij")]
    [InlineData("abcdef", 2, true, "ab")]
    [InlineData("abc", 0, true, "")]
    // Wide characters take two cells each
    [InlineData("日本語テキスト", 6, true, "日...")]
    [InlineData("日本語", 6, true, "日本語")]
    public void ATextCutForTheScreen_FitsItsCells(string text, int cells, bool dots, string expected)
    {
        Assert.Equal(expected, Tools.TruncateString(text, cells, dots));
    }

    [Fact]
    public void TheLayout_FollowsEditsInsideLines_AndLinesAddedOrTakenAway()
    {
        // Rows of 10 cells: "one two three four" takes two. After each change the rows are those of the text laid
        // out from the start
        var editor = new EditorEngine("one two three four\nfive", 10, 10, false) { Wrap = true };
        Assert.Equal(3, editor.Layout.TotalRows());
        foreach (var keys in new[] { "dd", "u", "ggd$", "u", "yy3p", "ggAmore text here<Esc>", "jJ", ":%j<CR>", "u",
                     "ggVGd", "u", "ix<CR>y<CR><Esc>" })
        {
            editor.Type(keys);
            var fresh = new EditorEngine(editor.Buffer.GetText(), 10, 10, false) { Wrap = true };
            Assert.Equal(fresh.Layout.TotalRows(), editor.Layout.TotalRows());
            for (var line = 0; line < editor.Buffer.LineCount; line++)
            {
                Assert.Equal(fresh.Layout.Segments(line), editor.Layout.Segments(line));
            }
        }
    }

    [Fact]
    public void AnOption_IsAnsweredAndNeverTakenForAFileToMake()
    {
        Assert.True(StartArgument.IsOption("--help", _directory));
        Assert.True(StartArgument.IsOption("-v", _directory));
        File.WriteAllText(Path.Combine(_directory, "-notes.md"), "x");
        Assert.False(StartArgument.IsOption("-notes.md", _directory));
        Assert.False(StartArgument.IsOption("-", _directory));
        Assert.False(StartArgument.IsOption("notes.md", _directory));
        var output = new StringWriter();
        Assert.Equal(0, StartArgument.AnswerOption("--version", output));
        Assert.Equal(DialogText.ApplicationName + " " + DialogText.ApplicationVersion, output.ToString().Trim());
        output = new StringWriter();
        Assert.Equal(0, StartArgument.AnswerOption("-h", output));
        Assert.StartsWith("Usage: marbles", output.ToString());
        Assert.All(output.ToString().Split('\n'), line => Assert.True(line.TrimEnd('\r').Length <= 80, line));
        Assert.Equal(1, StartArgument.AnswerOption("--nope", new StringWriter()));
    }

    [Fact]
    public void ABookmarkAddedInTheJumpMenu_KeepsTheRemovedOneToPutBack()
    {
        foreach (var name in new[] { "a", "b" }) Bookmarks.Add(new JumpTarget(JumpKind.File, Path.Combine(_directory, name)));
        Assert.Equal(DialogText.Success, Bookmarks.Save());
        Bookmarks.RemoveAt(0);
        // B in the Jump menu, on the list it has read: u still puts "a" back, where it was
        Bookmarks.AddWithMessage(new JumpTarget(JumpKind.File, Path.Combine(_directory, "c")), false);
        Assert.Equal(0, Bookmarks.UndoRemove());
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown).OrderBy(name => name));
    }
}