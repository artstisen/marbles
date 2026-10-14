namespace Marbles.Tests;

/// <summary>
/// Completing words while typing in the editor: the lists of the file of completions, the words looked up, and
/// the keys that choose and use a completion
/// </summary>
[Collection("Static state")]
public class CompletionsTests : IDisposable
{
    private const string Lists =
        "How it works (not used)\n" +
        "word = not used either\n" +
        "\n" +
        "=== HTML (html)\n" +
        "<div = <div>$0</div>\n" +
        "<dialog = <dialog>$0</dialog>\n" +
        "<dl = <dl>$0</dl>\n" +
        "<ul = <ul>\\n    <li>$0</li>\\n</ul>\n" +
        "\n" +
        "=== CSS (css)\n" +
        "display = display: $0;\n" +
        "=== JavaScript (js)\n" +
        "console.log = console.log($0);\n" +
        "=== All\n" +
        "lorem = Lorem ipsum\n" +
        "Marbles\n" +
        "path = C:\\\\data\n";

    private readonly string _directory;

    public CompletionsTests()
    {
        Tools.ClearLineMemory();
        _directory = Path.Combine(Path.GetTempPath(), "marbles-completions-" + Guid.NewGuid().ToString("N"));
        Completions.FilePathOverride = Path.Combine(_directory, Completions.FileName);
        Completions.Parse(Lists);
    }

    public void Dispose()
    {
        Completions.FilePathOverride = null;
        Completions.Parse(string.Empty);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static EditorEngine Editor(string text, SyntaxType syntax = SyntaxType.None)
    {
        var editor = new EditorEngine(text, 60, 10, false) { AutoComplete = true, CompletionSyntax = syntax };
        // An empty text starts in Insert mode: the keys of the tests start in Normal mode
        if (editor.Mode == EditorMode.Insert) editor.Type("<Esc>");
        return editor;
    }

    private static readonly ConsoleKeyInfo ShiftTab = new('\t', ConsoleKey.Tab, true, false, false);

    [Fact]
    public void TheLists_AreReadWithTheirKindOfText()
    {
        Assert.Equal(9, Completions.Items.Count);
        Assert.Equal(new Completions.Item("<div", "<div>$0</div>", SyntaxType.Html), Completions.Items[0]);
        Assert.Equal("<ul>\n    <li>$0</li>\n</ul>", Completions.Items[3].Text);
        Assert.Equal(SyntaxType.Css, Completions.Items[4].Syntax);
        // A word alone is completed to itself, and \\ is a \
        Assert.Equal(new Completions.Item("Marbles", "Marbles", SyntaxType.None), Completions.Items[7]);
        Assert.Equal("C:\\data", Completions.Items[8].Text);
        // How they are shown: the first line, without the cursor mark
        Assert.Equal("<div></div>", Completions.Items[0].Shown);
        Assert.Equal("<ul> ...", Completions.Items[3].Shown);
    }

    [Fact]
    public void AWord_IsLookedUp_InTheListOfTheKindOfText_ThenInTheListForAll()
    {
        Assert.Equal(new[] { "<div", "<dialog", "<dl" },
            Completions.Find("<d", SyntaxType.Html).Select(item => item.Word));
        Assert.Empty(Completions.Find("<d", SyntaxType.Css));
        Assert.Equal(new[] { "display" }, Completions.Find("DIS", SyntaxType.Css).Select(item => item.Word));
        // Plain text: the list for all text only
        Assert.Equal(new[] { "lorem" }, Completions.Find("lo", SyntaxType.None).Select(item => item.Word));
        // Too short, or the word is the completion already
        Assert.Empty(Completions.Find("l", SyntaxType.None));
        Assert.Empty(Completions.Find("Marbles", SyntaxType.None));
    }

    [Fact]
    public void TheWordsBeforeTheCursor_AreFound_TheLongestFirst()
    {
        string Words(string line) => string.Join("|",
            Completions.WordStarts(line, line.Length).Select(start => line[start..]));

        Assert.Equal("<di|di", Words("text <di"));
        Assert.Equal("console.lo|.lo|lo", Words("  console.lo"));
        // The longer ones are looked up first, and "Th" if they are not in a list
        Assert.Equal("end.Th|.Th|Th", Words("end.Th"));
        Assert.Equal("<!", Words("<!"));
        Assert.Equal(string.Empty, Words("end."));
        Assert.Equal(string.Empty, Words("x"));
        // Inside a word: nothing
        Assert.Empty(Completions.WordStarts("<div>", 3));
    }

    [Fact]
    public void Tab_CompletesTheOnlyCompletion_WithTheCursorAtTheMark()
    {
        var editor = Editor("", SyntaxType.Css);
        editor.Type("idis");
        Assert.Single(editor.CompletionList);
        Assert.Equal(-1, editor.CompletionIndex);
        editor.Type("<Tab>");
        Assert.Equal("display: ;", editor.GetText());
        Assert.Equal(new TextPos(0, 9), editor.Cursor);
        Assert.Equal(EditorMode.Insert, editor.Mode);
        Assert.Empty(editor.CompletionList);
        editor.Type("flex");
        Assert.Equal("display: flex;", editor.GetText());
    }

    [Fact]
    public void Tab_GoesThroughSeveral_AndEnterUsesTheOneChosen()
    {
        var editor = Editor("", SyntaxType.Html);
        editor.Type("i<lt>d");
        Assert.Equal(3, editor.CompletionList.Count);
        editor.Type("<Tab><Tab>");
        Assert.Equal(1, editor.CompletionIndex);
        Assert.Equal("<d", editor.GetText());
        editor.HandleKey(ShiftTab);
        editor.HandleKey(ShiftTab);
        Assert.Equal(2, editor.CompletionIndex);
        editor.Type("<CR>");
        Assert.Equal("<dl></dl>", editor.GetText());
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
    }

    [Fact]
    public void Enter_StartsANewLine_UntilACompletionIsChosen()
    {
        var editor = Editor("", SyntaxType.Html);
        editor.Type("i<lt>d<CR>");
        Assert.Equal("<d\n", editor.GetText());
        Assert.Empty(editor.CompletionList);
    }

    [Fact]
    public void Esc_ClosesTheList_AndTheNextLeavesInsertMode()
    {
        var editor = Editor("", SyntaxType.Html);
        editor.Type("i<lt>di<Esc>");
        Assert.Empty(editor.CompletionList);
        Assert.Equal(EditorMode.Insert, editor.Mode);
        // Tab inserts blank space again
        editor.Type("<Tab>");
        Assert.Equal("<di" + new string(' ', EditorEngine.TabWidth), editor.GetText());
        editor.Type("<Esc>");
        Assert.Equal(EditorMode.Normal, editor.Mode);
    }

    [Fact]
    public void TypingOn_AndBackspace_LookUpTheWordAgain()
    {
        var editor = Editor("", SyntaxType.Html);
        editor.Type("i<lt>di");
        Assert.Equal(2, editor.CompletionList.Count);
        editor.Type("v");
        Assert.Single(editor.CompletionList);
        editor.Type("<BS><BS>");
        Assert.Equal(3, editor.CompletionList.Count);
        editor.Type(" ");
        Assert.Empty(editor.CompletionList);
    }

    [Fact]
    public void ALongCompletion_IsIndentedAsTheLine_AndUndoneAsOneChange()
    {
        var editor = Editor("    ", SyntaxType.Html);
        editor.Type("A<lt>ul<Tab>");
        Assert.Equal("    <ul>\n        <li></li>\n    </ul>", editor.GetText());
        Assert.Equal(new TextPos(1, 12), editor.Cursor);
        editor.Type("<Esc>u");
        Assert.Equal("    <ul", editor.GetText());
        editor.Type("u");
        Assert.Equal("    ", editor.GetText());
    }

    [Fact]
    public void Off_TabInsertsBlankSpace_AsBefore()
    {
        var editor = Editor("", SyntaxType.Css);
        editor.AutoComplete = false;
        editor.Type("idis<Tab>");
        Assert.Equal("dis" + new string(' ', EditorEngine.TabWidth), editor.GetText());
        Assert.Empty(editor.CompletionList);
    }

    [Fact]
    public void TheCommand_TurnsItOnAndOff_AndOpensTheFile()
    {
        var editor = Editor("");
        Assert.Equal(EditorRequest.CompleteChanged, editor.Type(":complete<CR>"));
        Assert.False(editor.AutoComplete);
        Assert.Equal(EditorRequest.CompleteChanged, editor.Type(":complete on<CR>"));
        Assert.True(editor.AutoComplete);
        Assert.Equal(DialogText.CompleteOn, editor.Message);
        editor.Type(":complete off<CR>");
        Assert.False(editor.AutoComplete);
        Assert.Equal(EditorRequest.CompletionsEdit, editor.Type(":complete edit<CR>"));
    }

    [Fact]
    public void TheColorsOfTheText_ChooseTheList_BeforeTheKindOfFile()
    {
        var editor = Editor("", SyntaxType.Html);
        editor.Type(":syntax css<CR>idis");
        Assert.Single(editor.CompletionList);
    }

    [Fact]
    public void TheFile_IsMadeWithListsToStartWith_AndReadAgainWhenChanged()
    {
        Assert.False(Completions.Exists);
        // Without a file the lists it is made with are used
        Assert.Equal(DialogText.Success, Completions.Load());
        Assert.Contains(Completions.Items, item => item.Word == "<div" && item.Syntax == SyntaxType.Html);
        Assert.Contains(Completions.Items, item => item.Word == "display" && item.Syntax == SyntaxType.Css);
        Assert.Contains(Completions.Items, item => item.Syntax == SyntaxType.Markdown);
        Assert.Contains(Completions.Items, item => item.Syntax == SyntaxType.JavaScript);
        Assert.Contains(Completions.Items, item => item.Syntax == SyntaxType.None);

        Assert.Equal(DialogText.Success, Completions.EnsureFile());
        Assert.True(Completions.Exists);
        var count = Completions.Items.Count;
        File.WriteAllText(Completions.FilePath, "=== Mine\nhello = Hello world\n");
        File.SetLastWriteTimeUtc(Completions.FilePath, DateTime.UtcNow.AddMinutes(1));
        Completions.Refresh();
        Assert.NotEqual(count, Completions.Items.Count);
        Assert.Equal("Hello world", Completions.Find("he", SyntaxType.None).Single().Text);
    }
}
