namespace Marbles.Tests;

/// <summary>
/// The macros of the editor: recording (":record", stopped by the Esc that would close the editor), the lines a
/// recording is written as, playing (":play name"), what a macro is not allowed to do, and the files of the macros
/// </summary>
[Collection("Static state")]
public class MacroTests : IDisposable
{
    private readonly string _directory;
    private readonly Dictionary<string, string> _macros = new(StringComparer.OrdinalIgnoreCase);

    public MacroTests()
    {
        Tools.ClearLineMemory();
        _directory = Path.Combine(Path.GetTempPath(), "marbles-macros-" + Guid.NewGuid().ToString("N"));
        MacroFiles.FolderOverride = _directory;
    }

    public void Dispose()
    {
        MacroFiles.FolderOverride = null;
        CommandCompletion.Arguments.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private EditorEngine Editor(string text)
    {
        return new EditorEngine(text, 40, 10, false)
        {
            MacroLoader = name => _macros.TryGetValue(name, out var macro) ? macro : null
        };
    }

    [Fact]
    public void Recording_IsWrittenAsLinesThatCanBeRead()
    {
        var editor = Editor("one two\nthree");
        Assert.Equal(EditorRequest.MacroRecord, editor.Type(":record<CR>"));
        editor.StartRecording();
        Assert.True(editor.IsRecording);
        editor.Type("wdwA!<Esc>jjk0ihi \"you\"<CR><Esc>3x:s/e/E/<CR>/thr<CR>");
        editor.Type("ciwX<Esc>");
        // The Esc that would close the editor asks what to do with the recording (it is not recorded)
        // (the first Esc takes away the highlight of the search)
        Assert.Equal(EditorRequest.MacroStop, editor.Type("<Esc><Esc>"));
        var lines = editor.StopRecording();
        Assert.False(editor.IsRecording);
        Assert.Equal(new[]
        {
            "word right", "delete word", "append at line end \"!\"", "down 2", "up", "line start",
            "insert \"hi \\\"you\\\"\\n\"", "delete character 3", "command s/e/E/", "find \"thr\"", "keys ciwX<Esc>"
        }, lines);
    }

    [Fact]
    public void AMacro_IsPlayed_AndUndoneAsOneStep()
    {
        _macros["bullet"] = "# Make a line a bullet\nline start\ninsert \"- \"\ndown\n";
        var editor = Editor("a\nb\nc\nd");
        editor.Type(":play bullet 3<CR>");
        Assert.Equal("- a\n- b\n- c\nd", editor.GetText());
        Assert.False(editor.MessageIsError);
        Assert.Contains("bullet", editor.Message);
        // All of it is one step
        editor.Type("u");
        Assert.Equal("a\nb\nc\nd", editor.GetText());
        // ":macro name" plays it too
        editor.Type(":macro bullet<CR>");
        Assert.Equal("- a\nb\nc\nd", editor.GetText());
    }

    [Fact]
    public void AMacro_CannotSaveOrClose_AndStopsAtALineNotUnderstood()
    {
        _macros["save"] = "insert \"x\"\ncommand w\ninsert \"y\"";
        var editor = Editor("text");
        editor.Type(":play save<CR>");
        Assert.True(editor.MessageIsError);
        Assert.Contains("2", editor.Message);
        Assert.Equal("xtext", editor.GetText());
        Assert.False(editor.IsModified && editor.GetText().Contains('y'));

        _macros["bad"] = "down\njump over the moon";
        editor.Type(":play bad<CR>");
        Assert.True(editor.MessageIsError);
        Assert.Contains("2", editor.Message);

        editor.Type(":play nothing<CR>");
        Assert.True(editor.MessageIsError);
        // A macro that plays itself is stopped
        _macros["loop"] = "play loop";
        editor.Type(":play loop<CR>");
        Assert.True(editor.MessageIsError);
    }

    [Fact]
    public void AnUndoInAMacro_UndoesOneStep_AsWhenItWasRecorded()
    {
        _macros["undoing"] = "insert \"a\"\ninsert \"b\"\nundo";
        var editor = Editor("x");
        editor.Type(":play undoing<CR>");
        Assert.Equal("ax", editor.GetText());
        // The macro is still undone as one step
        editor.Type("u");
        Assert.Equal("x", editor.GetText());
    }

    [Fact]
    public void Recording_KeepsAltGrText_TextBeforeCtrlO_AndLeavesOutCommandsForTheHost()
    {
        var editor = Editor("x");
        editor.StartRecording();
        // AltGr+E (€) comes as Ctrl+Alt with the letter E
        editor.HandleKey(new ConsoleKeyInfo('i', ConsoleKey.I, false, false, false));
        editor.HandleKey(new ConsoleKeyInfo('€', ConsoleKey.E, false, true, true));
        // Ctrl+O (the Jump menu) leaves INSERT mode: the text typed is kept, the key is not
        Assert.Equal(EditorRequest.Jump, editor.Type("5<C-o>"));
        // A command for the host (":w") is left out whole, and so is a recording asked about with ":q"
        Assert.Equal(EditorRequest.Save, editor.Type(":w<CR>"));
        Assert.Equal(EditorRequest.MacroStop, editor.Type(":q<CR>"));
        Assert.True(editor.IsRecording);
        Assert.Equal(new[] { "insert \"€5\"" }, editor.StopRecording());
    }

    [Fact]
    public void Names_CannotEndWithANumber_AndChainsOfMacrosAreStopped()
    {
        Assert.NotNull(MacroFiles.NameProblem("copy 2"));
        Assert.Null(MacroFiles.NameProblem("copy2"));
        // Each step counts, also one that only plays another macro
        _macros["a"] = "play b 1000";
        _macros["b"] = "play c 1000";
        _macros["c"] = "# nothing";
        var editor = Editor("x");
        editor.Type(":play a<CR>");
        Assert.True(editor.MessageIsError);
    }

    [Fact]
    public void AMacro_PlaysAnother_AndKeysWorkAsTyped()
    {
        _macros["inner"] = "keys A;<Esc>";
        _macros["outer"] = "play inner\ndown\nplay inner";
        var editor = Editor("a\nb");
        editor.Type(":play outer<CR>");
        Assert.Equal("a;\nb;", editor.GetText());
    }

    [Fact]
    public void Keys_AndTextsInQuotes_AreWrittenAndReadBack()
    {
        var keys = MacroLanguage.ParseKeys("ci<lt>x<Esc><C-r> <CR>");
        Assert.Equal("ci<lt>x<Esc><C-r><Space><CR>", string.Concat(keys.Select(MacroLanguage.FormatKey)));
        Assert.Equal("\"a \\\"b\\\" \\\\ \\n\\t\"", MacroLanguage.Quote("a \"b\" \\ \n\t"));
        var steps = MacroLanguage.Compile("insert \"a \\\"b\\\" \\\\ \\n\"", out var error);
        Assert.Equal(string.Empty, error);
        var typed = string.Concat(steps[0].Keys.Select(MacroLanguage.FormatKey));
        Assert.Equal("ia<Space>\"b\"<Space>\\<Space><CR><Esc>", typed);
    }

    [Fact]
    public void TheStepsOfTheLanguage_AreCompletedAndColored()
    {
        var found = Completions.Find("del", SyntaxType.Macro).Select(item => item.Word).ToList();
        Assert.Contains("delete line", found);
        Assert.Contains("delete word", found);
        Assert.Equal(SyntaxType.Macro, SyntaxHighlighter.TypeOfFile("bullet.macro"));

        var styles = new SyntaxStyle[16];
        MacroLanguage.Scan("insert \"x\" # no", styles);
        Assert.Equal(SyntaxStyle.Keyword, styles[0]);
        Assert.Equal(SyntaxStyle.String, styles[7]);
        var comment = new SyntaxStyle[6];
        MacroLanguage.Scan("# note", comment);
        Assert.All(comment, style => Assert.Equal(SyntaxStyle.Comment, style));
    }

    [Fact]
    public void Macros_AreSavedAsFiles_AndTheirNamesComplete()
    {
        Assert.Empty(MacroFiles.Names());
        Assert.Equal(DialogText.Success, MacroFiles.Save("Bullet list", new[] { "line start", "insert \"- \"" }));
        Assert.Equal(DialogText.Success, MacroFiles.Save("tidy", new[] { "down" }));
        Assert.Equal(new[] { "Bullet list", "tidy" }, MacroFiles.Names());
        Assert.StartsWith("# Macro: Bullet list", MacroFiles.Load("Bullet list"));
        Assert.True(File.Exists(Path.Combine(_directory, "tidy.macro")));
        Assert.Null(MacroFiles.Load("../tidy"));
        Assert.False(MacroFiles.IsValidName("a/b"));
        Assert.False(MacroFiles.IsValidName(" a"));

        // The macro saved is played as it was recorded
        var editor = new EditorEngine("x", 40, 10, false) { MacroLoader = MacroFiles.Load };
        editor.Type(":play Bullet list<CR>");
        Assert.Equal("- x", editor.GetText());

        // Tab after ":play " completes the names
        CommandCompletion.Arguments["play"] = MacroFiles.Names;
        var completion = new CommandCompletion(CommandContext.Editor);
        Assert.Equal("play Bullet list", completion.Next("play b", true));
        Assert.Equal("play tidy", new CommandCompletion(CommandContext.Editor).Next("play t", true));
    }
}
