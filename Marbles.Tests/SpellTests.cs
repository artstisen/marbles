using System.Text;

namespace Marbles.Tests;

/// <summary>
/// Writes small spell files in the format of Vim (.spl), so the spell checker can be tested without
/// the real dictionaries
/// </summary>
internal sealed class SpellFileBuilder
{
    internal const int OneCap = 0x02;
    internal const int Banned = 0x10;
    internal const int KeepCap = 0x80;
    internal const int NeedCompound = 0x0200;

    private sealed class Node
    {
        internal readonly SortedDictionary<byte, Node> Children = new();
        internal readonly List<int> WordEnds = new(); // The flags of the words ending here
    }

    private readonly Node _fold = new();
    private readonly Node _keep = new();
    private string _midWord = string.Empty;
    private string _compoundRules = string.Empty;
    private int _compoundMinLength;
    private readonly List<(string From, string To)> _replacements = new();

    /// <summary>
    /// Add a word (in lowercase)
    /// </summary>
    /// <param name="word">The word</param>
    /// <param name="flags">The flags of the word</param>
    /// <param name="compoundFlag">The compound flag of the word (a character used in the compound rules)</param>
    internal SpellFileBuilder Word(string word, int flags = 0, char compoundFlag = '\0')
    {
        Add(_fold, word, flags, compoundFlag);
        return this;
    }

    /// <summary>
    /// Add a word that must be written exactly as given (like "iPhone")
    /// </summary>
    internal SpellFileBuilder KeepCaseWord(string word)
    {
        Add(_keep, word, KeepCap, '\0');
        return this;
    }

    internal SpellFileBuilder MidWord(string chars)
    {
        _midWord = chars;
        return this;
    }

    internal SpellFileBuilder Compound(string rules, int minLength)
    {
        _compoundRules = rules;
        _compoundMinLength = minLength;
        return this;
    }

    internal SpellFileBuilder Replacement(string from, string to)
    {
        _replacements.Add((from, to));
        return this;
    }

    private static void Add(Node root, string word, int flags, char compoundFlag)
    {
        if (compoundFlag != '\0') flags |= 0x20 | (compoundFlag << 24);
        var node = root;
        foreach (var b in Encoding.UTF8.GetBytes(word))
        {
            if (!node.Children.TryGetValue(b, out var child))
            {
                child = new Node();
                node.Children[b] = child;
            }

            node = child;
        }

        node.WordEnds.Add(flags);
    }

    internal byte[] Build()
    {
        var file = new List<byte>();
        file.AddRange(Encoding.ASCII.GetBytes("VIMspell"));
        file.Add(50);
        if (_midWord.Length > 0) Section(file, 2, Encoding.UTF8.GetBytes(_midWord));
        if (_replacements.Count > 0)
        {
            var content = new List<byte> { 0, (byte)_replacements.Count };
            foreach (var (from, to) in _replacements)
            {
                content.Add((byte)Encoding.UTF8.GetByteCount(from));
                content.AddRange(Encoding.UTF8.GetBytes(from));
                content.Add((byte)Encoding.UTF8.GetByteCount(to));
                content.AddRange(Encoding.UTF8.GetBytes(to));
            }

            Section(file, 4, content.ToArray());
        }

        if (_compoundRules.Length > 0)
        {
            // <compmax> <compminlen> <compsylmax> <compoptions> <comppatcount> <compflags>
            var content = new List<byte> { 0, (byte)_compoundMinLength, 0, 0, 0, 0, 0 };
            content.AddRange(Encoding.Latin1.GetBytes(_compoundRules));
            Section(file, 8, content.ToArray());
        }

        file.Add(255);
        Tree(file, _fold);
        Tree(file, _keep);
        Number(file, 0, 4); // No prefix tree
        return file.ToArray();
    }

    private static void Number(List<byte> file, int number, int byteCount)
    {
        for (var shift = (byteCount - 1) * 8; shift >= 0; shift -= 8)
        {
            file.Add((byte)(number >> shift));
        }
    }

    private static void Section(List<byte> file, byte id, byte[] content)
    {
        file.Add(id);
        file.Add(0);
        Number(file, content.Length, 4);
        file.AddRange(content);
    }

    private static void Tree(List<byte> file, Node root)
    {
        var nodes = new List<byte>();
        var count = 0;
        if (root.Children.Count > 0) WriteNode(nodes, root, ref count);
        Number(file, count, 4);
        file.AddRange(nodes);
    }

    private static void WriteNode(List<byte> output, Node node, ref int count)
    {
        output.Add((byte)(node.WordEnds.Count + node.Children.Count));
        count += 1 + node.WordEnds.Count + node.Children.Count;
        foreach (var flags in node.WordEnds)
        {
            if (flags == 0)
            {
                output.Add(0);
                continue;
            }

            if ((flags & 0xFF00) != 0)
            {
                output.Add(3);
                output.Add((byte)flags);
                output.Add((byte)(flags >> 8));
            }
            else
            {
                output.Add(2);
                output.Add((byte)flags);
            }

            if ((flags & 0x20) != 0) output.Add((byte)(flags >> 24));
        }

        foreach (var b in node.Children.Keys)
        {
            output.Add(b);
        }

        foreach (var child in node.Children.Values)
        {
            WriteNode(output, child, ref count);
        }
    }
}

[Collection("Static state")]
public class SpellTests : IDisposable
{
    private readonly string _directory;
    private readonly string _savedDirectory = SpellChecker.DirectoryName;
    private readonly string _savedLanguages = SpellChecker.Languages;

    public SpellTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "marbles_spell_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "xx.utf-8.spl"), Words().Build());
        File.WriteAllBytes(Path.Combine(_directory, "yy.utf-8.spl"),
            new SpellFileBuilder().Word("hund").Word("kat").Build());
        SpellChecker.DirectoryName = _directory;
    }

    public void Dispose()
    {
        SpellChecker.TurnOff();
        SpellChecker.DirectoryName = _savedDirectory;
        SpellChecker.Languages = _savedLanguages;
        Directory.Delete(_directory, true);
    }

    private static SpellFileBuilder Words()
    {
        return new SpellFileBuilder()
            .Word("hello").Word("world").Word("the").Word("this").Word("is").Word("a").Word("word")
            .Word("don't").Word("paris", SpellFileBuilder.OneCap).Word("blå").Word("f.eks.")
            .Word("wrong", SpellFileBuilder.Banned)
            .Word("foot", 0, 'c').Word("ball", 0, 'c')
            .Word("only", SpellFileBuilder.NeedCompound, 'c')
            .KeepCaseWord("iPhone")
            .MidWord("'")
            .Compound("c+", 3)
            .Replacement("f", "ph");
    }

    private static SpellDictionary Dictionary() => SpellDictionary.Read(Words().Build());

    private SpellChecker Checker(string languages = "xx")
    {
        var checker = SpellChecker.Load(languages, _directory, out var error);
        Assert.True(checker != null, error);
        return checker!;
    }

    #region The dictionary

    [Fact]
    public void Dictionary_FindsWords()
    {
        var dictionary = Dictionary();
        Assert.True(dictionary.Check("hello"));
        Assert.True(dictionary.Check("blå"));
        Assert.True(dictionary.Check("don't"));
        Assert.False(dictionary.Check("helo"));
        Assert.False(dictionary.Check("hell"));
        Assert.False(dictionary.Check("helloo"));
        Assert.Equal("'", dictionary.MidWordChars);
    }

    [Fact]
    public void Dictionary_RejectsFilesThatAreNotSpellFiles()
    {
        Assert.Throws<InvalidDataException>(() => SpellDictionary.Read(Encoding.ASCII.GetBytes("not a spell file")));
        var cut = Words().Build();
        Assert.Throws<InvalidDataException>(() => SpellDictionary.Read(cut.Take(cut.Length - 20).ToArray()));
    }

    [Fact]
    public void Dictionary_Capitalization()
    {
        var dictionary = Dictionary();
        // A plain word can start with a capital letter or be in capitals
        Assert.True(dictionary.Check("Hello"));
        Assert.True(dictionary.Check("HELLO"));
        Assert.False(dictionary.Check("hEllo"));
        // A name must have its capital letter
        Assert.True(dictionary.Check("Paris"));
        Assert.True(dictionary.Check("PARIS"));
        Assert.False(dictionary.Check("paris"));
        // A keep-case word must be written exactly as it is
        Assert.True(dictionary.Check("iPhone"));
        Assert.False(dictionary.Check("iphone"));
        Assert.False(dictionary.Check("Iphone"));
    }

    [Fact]
    public void Dictionary_BannedWord_IsWrong()
    {
        Assert.Null(Dictionary().Check("wrong"));
    }

    [Fact]
    public void Dictionary_CompoundWords()
    {
        var dictionary = Dictionary();
        Assert.True(dictionary.Check("football"));
        Assert.True(dictionary.Check("ballfoot"));
        Assert.True(dictionary.Check("Football"));
        Assert.True(dictionary.Check("footballfoot"));
        Assert.False(dictionary.Check("footBall"));
        // Words without a compound flag cannot be joined
        Assert.False(dictionary.Check("helloworld"));
        Assert.False(dictionary.Check("foothello"));
        // A word that is only good as part of a compound word
        Assert.False(dictionary.Check("only"));
        Assert.True(dictionary.Check("onlyfoot"));
    }

    [Fact]
    public void Dictionary_CompoundWords_OnlyTheLongestStartIsTried()
    {
        // As in Vim: "footb" is the longest word that can start a compound word, and "all" cannot follow it.
        // The shorter "foot" (+ "ball") is not tried when another word follows
        var dictionary = SpellDictionary.Read(new SpellFileBuilder()
            .Word("foot", 0, 'c').Word("footb", 0, 'c').Word("ball", 0, 'c').Word("xyz", 0, 'c')
            .Compound("c+", 3).Build());
        Assert.False(dictionary.Check("footballxyz"));
        Assert.True(dictionary.Check("footxyz"));
    }

    #endregion

    #region The spell checker

    [Fact]
    public void Checker_FindsMisspelledWords()
    {
        var checker = Checker();
        Assert.Empty(checker.FindMisspelled("Hello world, this is the word."));
        Assert.Equal(new List<(int, int)> { (0, 4), (11, 16) }, checker.FindMisspelled("Helo world wurld"));
        Assert.Equal(new List<(int, int)> { (4, 9) }, checker.FindMisspelled("the wrong word"));
    }

    [Fact]
    public void Checker_WordsWithApostrophes()
    {
        var checker = Checker();
        Assert.Empty(checker.FindMisspelled("don't"));
        Assert.Empty(checker.FindMisspelled("don’t")); // Typographic apostrophe
        Assert.Empty(checker.FindMisspelled("'hello' world"));
        Assert.Equal(new List<(int, int)> { (0, 7) }, checker.FindMisspelled("hello's"));
    }

    [Fact]
    public void Checker_WordsWithOtherCharacters()
    {
        var checker = Checker();
        Assert.Empty(checker.FindMisspelled("this is f.eks. a word"));
        Assert.Equal(new List<(int, int)> { (2, 5) }, checker.FindMisspelled("f.eks"));
    }

    [Fact]
    public void Checker_SkipsWhatIsNotText()
    {
        var checker = Checker();
        Assert.Empty(checker.FindMisspelled("x y z 123 abc123 x86 my_name"));
        Assert.Empty(checker.FindMisspelled("https://example.com/qwerty user@qwerty.zz C:\\qwerty\\zzz www.qwerty.zz"));
        Assert.Single(checker.FindMisspelled("qwerty"));
    }

    [Fact]
    public void Checker_SeveralLanguages()
    {
        Assert.Single(Checker().FindMisspelled("hello hund"));
        var checker = Checker("xx, yy");
        Assert.Equal("xx,yy", checker.LanguageNames);
        Assert.Empty(checker.FindMisspelled("hello hund"));
    }

    [Fact]
    public void Checker_MissingFile_GivesAnError()
    {
        Assert.Null(SpellChecker.Load("zz", _directory, out var error));
        Assert.StartsWith(DialogText.SpellFileNotFound, error);
        Assert.Null(SpellChecker.Load("../xx", _directory, out _));
        Assert.Null(SpellChecker.Load(" , ", _directory, out error));
        Assert.Equal(DialogText.SpellNoLanguage, error);
    }

    [Fact]
    public void Checker_Suggestions()
    {
        var checker = Checker();
        Assert.Equal("hello", checker.Suggest("helo", 9)[0]);
        Assert.Equal("Hello", checker.Suggest("Helo", 9)[0]);
        Assert.Equal("HELLO", checker.Suggest("HELO", 9)[0]);
        Assert.Contains("world", checker.Suggest("wurld", 9));
        Assert.Contains("Paris", checker.Suggest("paris", 9));
        Assert.DoesNotContain("wrong", checker.Suggest("wronk", 9));
        Assert.True(checker.Suggest("helo", 2).Count <= 2);
    }

    [Fact]
    public void Checker_AddWord_IsKeptInAFile()
    {
        var checker = Checker();
        Assert.Single(checker.FindMisspelled("qwerty hello"));
        Assert.Equal(DialogText.Success, checker.AddWord("qwerty"));
        // Also good when capitalized
        Assert.Empty(checker.FindMisspelled("qwerty Qwerty QWERTY"));
        Assert.Equal(DialogText.Success, checker.AddWord("Marbles"));
        Assert.Single(checker.FindMisspelled("Marbles marbles"));
        Assert.Equal("qwerty\nMarbles\n", File.ReadAllText(Path.Combine(_directory, "xx.utf-8.add")));

        // The words are read with the dictionary
        Assert.Empty(Checker().FindMisspelled("qwerty Marbles"));
        Assert.Contains("qwerty", Checker().Suggest("qwert", 9));
    }

    [Fact]
    public void Checker_WordListOfNeovim()
    {
        File.WriteAllText(Path.Combine(_directory, "xx.utf-8.add"), "# comment\ngood\nhello/!\nother/=\n");
        var checker = Checker();
        Assert.Equal(new List<(int, int)> { (5, 10) }, checker.FindMisspelled("good hello other"));
    }

    #endregion

    #region The editor

    private static EditorEngine Editor(string text)
    {
        return new EditorEngine(text, 40, 10, false);
    }

    [Fact]
    public void Editor_SpellIsOffUntilTheCommandIsGiven()
    {
        var editor = Editor("helo world");
        Assert.False(editor.IsSpellOn);
        Assert.Empty(editor.Misspelled(0));
        editor.Type("]s");
        Assert.True(editor.MessageIsError);
        Assert.Equal(DialogText.SpellOff, editor.Message);

        editor.Type(":spell xx<CR>");
        Assert.True(editor.IsSpellOn);
        Assert.Equal("xx", editor.SpellLanguages);
        Assert.Equal(new List<(int, int)> { (0, 4) }, editor.Misspelled(0));
        Assert.Contains("1 misspelled", editor.Message);

        editor.Type(":nospell<CR>");
        Assert.False(editor.IsSpellOn);
        Assert.Empty(editor.Misspelled(0));
    }

    [Fact]
    public void Editor_SpellCommand_TurnsCheckingOnAndOff()
    {
        var editor = Editor("hund hello");
        editor.Type(":spell xx<CR>");
        Assert.True(editor.IsSpellOn);
        // ":spell" again: off, and on again
        editor.Type(":spell<CR>");
        Assert.False(editor.IsSpellOn);
        Assert.Equal(DialogText.SpellTurnedOff, editor.Message);
        editor.Type(":spell<CR>");
        Assert.True(editor.IsSpellOn);
        // With languages, and ":set spell", it stays on
        editor.Type(":spell xx<CR>");
        Assert.True(editor.IsSpellOn);
        editor.Type(":set spell<CR>");
        Assert.True(editor.IsSpellOn);
    }

    [Fact]
    public void Editor_SpellCommand_KeepsTheLanguages()
    {
        var editor = Editor("hund");
        Assert.Equal(EditorRequest.SaveSettings, editor.Type(":spell yy<CR>"));
        Assert.Equal("yy", SpellChecker.Languages);
        Assert.Empty(editor.Misspelled(0));
        // Without languages the last ones are used
        var other = Editor("hund hello");
        Assert.Equal(EditorRequest.None, other.Type(":spell<CR>"));
        Assert.Equal(new List<(int, int)> { (5, 10) }, other.Misspelled(0));
    }

    [Fact]
    public void Editor_SpellCommand_UnknownLanguage()
    {
        var editor = Editor("hello");
        editor.Type(":spell zz<CR>");
        Assert.True(editor.MessageIsError);
        Assert.False(editor.IsSpellOn);
    }

    [Fact]
    public void Editor_JumpsBetweenMisspelledWords()
    {
        var editor = Editor("hello wurld\nthe wird is helo\nhello");
        editor.Type(":spell xx<CR>");
        editor.Type("]s");
        Assert.Equal(new TextPos(0, 6), editor.Cursor);
        editor.Type("]s");
        Assert.Equal(new TextPos(1, 4), editor.Cursor);
        editor.Type("2]s");
        Assert.Equal(new TextPos(0, 6), editor.Cursor); // Continues from the top
        editor.Type("[s");
        Assert.Equal(new TextPos(1, 12), editor.Cursor);
        editor.Type("[s");
        Assert.Equal(new TextPos(1, 4), editor.Cursor);

        var good = Editor("hello world");
        good.Type(":spell xx<CR>]s");
        Assert.Equal(DialogText.SpellNoMore, good.Message);
        Assert.Equal(new TextPos(0, 0), good.Cursor);
    }

    [Fact]
    public void Editor_SuggestionsReplaceTheWord()
    {
        var editor = Editor("the helo world");
        editor.Type(":spell xx<CR>");
        Assert.Equal(EditorRequest.SpellSuggest, editor.Type("wlz="));
        Assert.Equal("helo", editor.SpellWord());
        var suggestions = editor.SpellSuggestions();
        Assert.Equal("hello", suggestions[0]);
        editor.ReplaceSpellWord(suggestions[0]);
        Assert.Equal("the hello world", editor.GetText());
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
        Assert.Empty(editor.Misspelled(0));
        // One step to undo
        editor.Type("u");
        Assert.Equal("the helo world", editor.GetText());
    }

    [Fact]
    public void Editor_NoWordAtTheCursor()
    {
        var editor = Editor("   hello");
        editor.Type(":spell xx<CR>");
        Assert.Equal(EditorRequest.None, editor.Type("0z="));
        Assert.Equal(DialogText.SpellNoWord, editor.Message);
    }

    [Fact]
    public void Editor_AddsTheWordAtTheCursor()
    {
        var editor = Editor("hello qwerty");
        editor.Type(":spell xx<CR>");
        Assert.Single(editor.Misspelled(0));
        editor.Type("wzg");
        Assert.Equal(DialogText.SpellWordAdded + "qwerty", editor.Message);
        Assert.Empty(editor.Misspelled(0));
        Assert.Equal("qwerty\n", File.ReadAllText(Path.Combine(_directory, "xx.utf-8.add")));
    }

    [Fact]
    public void Editor_TheWordBeingWrittenIsNotMarked()
    {
        var editor = Editor("helo");
        editor.Type(":spell xx<CR>");
        Assert.Single(editor.Misspelled(0));
        editor.Type("A");
        Assert.Empty(editor.Misspelled(0));
        editor.Type(" wurld is");
        Assert.Equal(new List<(int, int)> { (0, 4), (5, 10) }, editor.Misspelled(0));
        editor.Type("<Esc>");
        Assert.Equal(2, editor.Misspelled(0).Count);
    }

    [Fact]
    public void Editor_SpellCommands_DoTheSameAsTheKeys()
    {
        var editor = Editor("hello wurld\nthe wird qwerty");
        editor.Type(":spell xx<CR>");
        editor.Type(":spellnext<CR>");
        Assert.Equal(new TextPos(0, 6), editor.Cursor);
        editor.Type(":spellnext<CR>");
        Assert.Equal(new TextPos(1, 4), editor.Cursor);
        editor.Type(":spellprev<CR>");
        Assert.Equal(new TextPos(0, 6), editor.Cursor);
        Assert.Equal(EditorRequest.SpellSuggest, editor.Type(":spellsuggest<CR>"));
        Assert.Equal(EditorRequest.SpellSuggest, editor.Type(":suggest<CR>"));
        editor.Type("G$:spelladd<CR>");
        Assert.Equal(DialogText.SpellWordAdded + "qwerty", editor.Message);
        editor.Type(":nospell<CR>:spellnext<CR>");
        Assert.Equal(DialogText.SpellOff, editor.Message);
    }

    [Fact]
    public void Editor_ScrollCommandsStillWork()
    {
        var editor = Editor(string.Join("\n", Enumerable.Range(1, 50).Select(i => "line " + i)));
        editor.Type("20Gzt");
        Assert.Equal(19, editor.TopRow);
    }

    #endregion
}
