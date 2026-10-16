namespace Marbles.Tests;

/// <summary>
/// Drives the editor with key presses written like in Vim documentation: "dw", "ciwnew&lt;Esc&gt;", "&lt;C-r&gt;" ...
/// </summary>
internal static class Keys
{
    private static readonly Dictionary<string, ConsoleKeyInfo> Named = new()
    {
        ["Esc"] = new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false),
        ["CR"] = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false),
        ["BS"] = new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false),
        ["Del"] = new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false),
        ["Tab"] = new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false),
        ["Left"] = new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false),
        ["Right"] = new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false),
        ["Up"] = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false),
        ["Down"] = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false),
        ["Home"] = new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false),
        ["End"] = new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false),
        ["PgDn"] = new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false),
        ["PgUp"] = new ConsoleKeyInfo('\0', ConsoleKey.PageUp, false, false, false),
        ["lt"] = new ConsoleKeyInfo('<', 0, false, false, false)
    };

    internal static IEnumerable<ConsoleKeyInfo> Parse(string keys)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            if (keys[i] == '<')
            {
                var end = keys.IndexOf('>', i);
                var name = end > 0 ? keys.Substring(i + 1, end - i - 1) : string.Empty;
                if (Named.TryGetValue(name, out var named))
                {
                    yield return named;
                    i = end;
                    continue;
                }

                if (name.Length == 3 && name.StartsWith("C-"))
                {
                    // CTRL + letter
                    var letter = char.ToUpper(name[2]);
                    yield return new ConsoleKeyInfo((char)(letter - 'A' + 1), ConsoleKey.A + (letter - 'A'), false,
                        false, true);
                    i = end;
                    continue;
                }
            }

            yield return new ConsoleKeyInfo(keys[i], 0, false, false, false);
        }
    }

    internal static EditorRequest Type(this EditorEngine editor, string keys)
    {
        var request = EditorRequest.None;
        foreach (var key in Parse(keys))
        {
            request = editor.HandleKey(key);
        }

        return request;
    }
}

[Collection("Static state")]
public class EditorTests
{
    public EditorTests()
    {
        Tools.ClearLineMemory(); // The registers are shared by all editors
    }

    private static EditorEngine Editor(string text, int width = 40, int height = 10)
    {
        return new EditorEngine(text, width, height, false);
    }

    private static void AssertState(EditorEngine editor, string text, int line, int col)
    {
        Assert.Equal(text, editor.GetText());
        Assert.Equal(new TextPos(line, col), editor.Cursor);
    }

    #region Modes and inserting

    [Fact]
    public void StartsInNormalMode_UnlessEmpty()
    {
        Assert.Equal(EditorMode.Normal, Editor("text").Mode);
        Assert.Equal(EditorMode.Insert, Editor("").Mode);
    }

    [Fact]
    public void Insert_TypesTextAndEscReturnsToNormal()
    {
        var editor = Editor("");
        editor.Type("Hello<CR>World");
        Assert.Equal(EditorMode.Insert, editor.Mode);
        editor.Type("<Esc>");
        Assert.Equal(EditorMode.Normal, editor.Mode);
        AssertState(editor, "Hello\nWorld", 1, 4);
    }

    [Theory]
    [InlineData("abc", "iX<Esc>", "Xabc", 0, 0)]
    [InlineData("abc", "aX<Esc>", "aXbc", 0, 1)]
    [InlineData("abc", "llaX<Esc>", "abcX", 0, 3)]
    [InlineData("  abc\nnext", "$IX<Esc>", "  Xabc\nnext", 0, 2)]
    [InlineData("  abc\nnext", "AX<Esc>", "  abcX\nnext", 0, 5)]
    [InlineData("  abc\nnext", "oX<Esc>", "  abc\nX\nnext", 1, 0)]
    [InlineData("  abc\nnext", "OX<Esc>", "X\n  abc\nnext", 0, 0)]
    public void InsertCommands(string text, string keys, string expected, int line, int col)
    {
        var editor = Editor(text);
        editor.Type(keys);
        AssertState(editor, expected, line, col);
    }

    [Fact]
    public void Insert_BackspaceAndDeleteJoinLines()
    {
        var editor = Editor("ab\ncd");
        editor.Type("ji<BS>");
        AssertState(editor, "abcd", 0, 2);
        editor.Type("<Esc>u");
        Assert.Equal("ab\ncd", editor.GetText());
        editor.Type("ggA<Del>");
        Assert.Equal("abcd", editor.GetText());
    }

    [Fact]
    public void Insert_ControlKeys()
    {
        var editor = Editor("one two three");
        editor.Type("A<C-w>");
        Assert.Equal("one two ", editor.GetText());
        editor.Type("<C-u>");
        Assert.Equal("", editor.GetText());
        editor.Type("x<Tab>y<Esc>");
        Assert.Equal("x    y", editor.GetText());
    }

    [Fact]
    public void Insert_AltGrCharactersAreText()
    {
        // On many keyboard layouts characters like @ and { are typed with AltGr, reported as CTRL+ALT
        var editor = Editor("");
        editor.HandleKey(new ConsoleKeyInfo('@', ConsoleKey.D2, false, true, true));
        editor.HandleKey(new ConsoleKeyInfo('{', ConsoleKey.D7, false, true, true));
        Assert.Equal("@{", editor.GetText());
    }

    #endregion

    #region Movement

    [Theory]
    [InlineData("w", 0, 4)]
    [InlineData("2w", 0, 7)]
    [InlineData("3w", 0, 9)]
    [InlineData("W", 0, 4)]
    [InlineData("e", 0, 2)]
    [InlineData("$", 0, 12)]
    [InlineData("$b", 0, 9)]
    [InlineData("5w", 1, 2)]
    [InlineData("j", 1, 0)]
    [InlineData("jw", 1, 2)]
    [InlineData("G", 2, 0)]
    [InlineData("Ggg", 0, 0)]
    [InlineData("2G", 1, 2)]
    [InlineData("G$", 2, 3)]
    [InlineData("fb", 0, 4)]
    [InlineData("tb", 0, 3)]
    [InlineData("$Fo", 0, 11)]
    [InlineData("fo;", 0, 2)]
    [InlineData("fo;;,", 0, 2)]
    [InlineData("to;", 0, 1)]
    [InlineData("$_", 0, 0)]
    [InlineData("jj0", 2, 0)]
    public void Motions(string keys, int line, int col)
    {
        //                   0123456789012
        var editor = Editor("foo bar.(boo)\n  second\nlast");
        editor.Type(keys);
        Assert.Equal(new TextPos(line, col), editor.Cursor);
    }

    [Fact]
    public void WordMotions_CrossLinesAndStopAtEmptyLines()
    {
        var editor = Editor("one\n\ntwo");
        editor.Type("w");
        Assert.Equal(new TextPos(1, 0), editor.Cursor);
        editor.Type("w");
        Assert.Equal(new TextPos(2, 0), editor.Cursor);
        editor.Type("bb");
        Assert.Equal(new TextPos(0, 0), editor.Cursor);
    }

    [Fact]
    public void CursorStaysOnCharactersInNormalMode()
    {
        var editor = Editor("abc\n\nx");
        editor.Type("lllll");
        Assert.Equal(new TextPos(0, 2), editor.Cursor);
        editor.Type("jj");
        Assert.Equal(new TextPos(2, 0), editor.Cursor);
        editor.Type("kk"); // The column is remembered when passing shorter lines
        Assert.Equal(new TextPos(0, 2), editor.Cursor);
    }

    [Fact]
    public void UpAndDown_MoveByRowsOfWrappedLines()
    {
        // Wrapped at blank spaces to rows of 10 cells: "aaaa bbbb ", "cccc dddd ", "eeee"
        var editor = Editor("aaaa bbbb cccc dddd eeee\nnext", 10);
        Assert.Equal(new[] { 0, 10, 20 }, editor.Layout.Segments(0));
        editor.Type("llj");
        Assert.Equal(new TextPos(0, 12), editor.Cursor);
        editor.Type("jj");
        Assert.Equal(new TextPos(1, 2), editor.Cursor);
        editor.Type("3k");
        Assert.Equal(new TextPos(0, 2), editor.Cursor);
        editor.Type("gj"); // By line of the document
        Assert.Equal(new TextPos(1, 2), editor.Cursor);
    }

    [Fact]
    public void LongWordsAreWrappedInTheMiddle()
    {
        var editor = Editor("abcdefghijklmno pq", 10);
        Assert.Equal(new[] { 0, 10 }, editor.Layout.Segments(0));
    }

    [Fact]
    public void View_FollowsTheCursor()
    {
        var editor = Editor(string.Join("\n", Enumerable.Range(1, 50)), 40, 10);
        editor.Type("G");
        Assert.Equal(40, editor.TopRow);
        editor.Type("gg");
        Assert.Equal(0, editor.TopRow);
        editor.Type("<PgDn>");
        Assert.Equal(10, editor.TopRow);
        Assert.Equal(10, editor.Cursor.Line);
        editor.Type("zt5k");
        Assert.Equal(5, editor.TopRow);
    }

    #endregion

    #region Operators

    [Theory]
    [InlineData("dw", "bar.(boo) end", 0)]
    [InlineData("2dw", ".(boo) end", 0)]
    [InlineData("de", " bar.(boo) end", 0)]
    [InlineData("x", "oo bar.(boo) end", 0)]
    [InlineData("3x", " bar.(boo) end", 0)]
    [InlineData("wX", "foobar.(boo) end", 3)]
    [InlineData("wD", "foo ", 3)]
    [InlineData("wd0", "bar.(boo) end", 0)]
    [InlineData("d$", "", 0)]
    [InlineData("dfb", "ar.(boo) end", 0)]
    [InlineData("dtb", "bar.(boo) end", 0)]
    [InlineData("wdiw", "foo .(boo) end", 4)]
    [InlineData("wdaw", "foo.(boo) end", 3)]
    [InlineData("daw", "bar.(boo) end", 0)]
    [InlineData("f(di(", "foo bar.() end", 9)]
    [InlineData("fbfbdi(", "foo bar.() end", 9)]
    [InlineData("f)da(", "foo bar. end", 8)]
    [InlineData("$daw", "foo bar.(boo)", 12)]
    [InlineData("wrX", "foo Xar.(boo) end", 4)]
    [InlineData("w3rX", "foo XXX.(boo) end", 6)]
    [InlineData("~", "Foo bar.(boo) end", 1)]
    [InlineData("3~", "FOO bar.(boo) end", 3)]
    [InlineData("gUw", "FOO bar.(boo) end", 0)]
    [InlineData("gUU", "FOO BAR.(BOO) END", 0)]
    [InlineData(">>", "    foo bar.(boo) end", 4)]
    public void Operators_OnOneLine(string keys, string expected, int col)
    {
        //                   01234567890123456
        var editor = Editor("foo bar.(boo) end");
        editor.Type(keys);
        AssertState(editor, expected, 0, col);
        // Everything is one undo step
        editor.Type("u");
        Assert.Equal("foo bar.(boo) end", editor.GetText());
        Assert.False(editor.IsModified);
    }

    [Theory]
    [InlineData("gUUguu", "foo bar")]
    [InlineData("gUUg~~", "foo bar")]
    [InlineData(">>3>><lt><lt>", "    foo bar")]
    [InlineData("ix<Esc>u", "foo bar")]
    public void Operators_InSequence(string keys, string expected)
    {
        var editor = Editor("foo bar");
        editor.Type(keys);
        Assert.Equal(expected, editor.GetText());
    }

    [Theory]
    [InlineData("cwnew<Esc>", "new bar \"quoted text\"")]
    [InlineData("ciwnew<Esc>", "new bar \"quoted text\"")]
    [InlineData("Cnew<Esc>", "new")]
    [InlineData("ccnew<Esc>", "new")]
    [InlineData("Snew<Esc>", "new")]
    [InlineData("snew<Esc>", "newoo bar \"quoted text\"")]
    [InlineData("ci\"new<Esc>", "foo bar \"new\"")]
    [InlineData("$ci\"new<Esc>", "foo bar \"new\"")]
    public void Change(string keys, string expected)
    {
        var editor = Editor("foo bar \"quoted text\"");
        editor.Type(keys);
        Assert.Equal(expected, editor.GetText());
        Assert.Equal(EditorMode.Normal, editor.Mode);
    }

    [Theory]
    [InlineData("dd", "two\nthree\nfour", 0, 0)]
    [InlineData("jdd", "one\nthree\nfour", 1, 0)]
    [InlineData("2dd", "three\nfour", 0, 0)]
    [InlineData("d2d", "three\nfour", 0, 0)]
    [InlineData("Gdd", "one\ntwo\nthree", 2, 0)]
    [InlineData("jdj", "one\nfour", 1, 0)]
    [InlineData("jdG", "one", 0, 0)]
    [InlineData("jjdgg", "four", 0, 0)]
    [InlineData("dG", "", 0, 0)]
    [InlineData("J", "one two\nthree\nfour", 0, 3)]
    [InlineData("3J", "one two three\nfour", 0, 7)]
    [InlineData("$dw", "on\ntwo\nthree\nfour", 0, 1)]
    public void Operators_OnLines(string keys, string expected, int line, int col)
    {
        var editor = Editor("one\ntwo\nthree\nfour");
        editor.Type(keys);
        AssertState(editor, expected, line, col);
        editor.Type("u");
        Assert.Equal("one\ntwo\nthree\nfour", editor.GetText());
    }

    [Fact]
    public void DeletingAWord_NeverJoinsLines()
    {
        var editor = Editor("one two\nthree");
        editor.Type("wdw");
        AssertState(editor, "one \nthree", 0, 3);
    }

    #endregion

    #region Registers (string memory)

    [Fact]
    public void YankAndPutLines()
    {
        var editor = Editor("one\ntwo");
        editor.Type("yyp");
        AssertState(editor, "one\none\ntwo", 1, 0);
        editor.Type("GP");
        AssertState(editor, "one\none\none\ntwo", 2, 0);
        editor.Type("Gp");
        AssertState(editor, "one\none\none\ntwo\none", 4, 0);
        editor.Type("2p");
        Assert.Equal("one\none\none\ntwo\none\none\none", editor.GetText());
    }

    [Fact]
    public void PutWithACount_ThatWouldBeTooMuch_IsRefused()
    {
        // 1000 characters 99999 times: about 100 million characters
        var editor = Editor(new string('x', 999));
        editor.Type("yy99999p");
        Assert.True(editor.MessageIsError);
        Assert.Equal(new string('x', 999), editor.GetText());
        // A count that fits still works
        editor.Type("3p");
        Assert.Equal(4, editor.GetText().Split('\n').Length);
    }

    [Fact]
    public void YankAndPutText()
    {
        var editor = Editor("one two");
        editor.Type("yw$p");
        AssertState(editor, "one twoone ", 0, 10);
        editor.Type("0P");
        AssertState(editor, "one one twoone ", 0, 3);
    }

    [Fact]
    public void DeletedCharactersCanBePut_ButDoNotReplaceYankedText()
    {
        var editor = Editor("ab");
        editor.Type("xp");
        Assert.Equal("ba", editor.GetText());
        Assert.False(Tools.HasLineMemory());

        editor = Editor("word x");
        editor.Type("yw$x");
        Assert.Equal("word ", Tools.GetCurrentLineFromLineMemory());
    }

    [Fact]
    public void YankedText_IsSharedBetweenEditors_AndCanBeSelected()
    {
        var first = Editor("alpha\nbeta");
        first.Type("yyjyy");
        Assert.Equal("2/2", Tools.GetLineMemoryPosition());

        var second = Editor("x");
        second.Type("p");
        Assert.Equal("x\nbeta", second.GetText());
        second.Type("<C-p>");
        Assert.Contains("1/2", second.Message);
        second.Type("p");
        Assert.Equal("x\nbeta\nalpha", second.GetText());
        second.Type("ggi<C-r><Esc>"); // Insert mode: text only, without the line break
        Assert.Equal("alphax\nbeta\nalpha", second.GetText());
    }

    [Fact]
    public void YankingTheSameTextAgain_SelectsItInsteadOfAddingIt()
    {
        var editor = Editor("one\ntwo");
        editor.Type("yyjyykyy");
        Assert.Equal("1/2", Tools.GetLineMemoryPosition());
    }

    #endregion

    #region Visual mode

    [Theory]
    [InlineData("ved", " two three\nsecond line", 0, 0)]
    [InlineData("vwd", "wo three\nsecond line", 0, 0)]
    [InlineData("wvey0P", "twoone two three\nsecond line", 0, 2)]
    [InlineData("Vd", "second line", 0, 0)]
    [InlineData("Vjd", "", 0, 0)]
    [InlineData("vjd", "econd line", 0, 0)]
    [InlineData("wviwU", "one TWO three\nsecond line", 0, 4)]
    [InlineData("wvawd", "one three\nsecond line", 0, 4)]
    [InlineData("vjJ", "one two three second line", 0, 13)]
    [InlineData("wveohd", "one three\nsecond line", 0, 3)]
    [InlineData("vecnew<Esc>", "new two three\nsecond line", 0, 2)]
    [InlineData("Vj>", "    one two three\n    second line", 0, 4)]
    public void VisualMode(string keys, string expected, int line, int col)
    {
        var editor = Editor("one two three\nsecond line");
        editor.Type(keys);
        AssertState(editor, expected, line, col);
        Assert.Equal(EditorMode.Normal, editor.Mode);
    }

    [Theory]
    [InlineData("jVJ", "one\nthree\ntwo\nfour\nfive", 2, 2)]
    [InlineData("jVK", "two\none\nthree\nfour\nfive", 0, 0)]
    [InlineData("jVjJ", "one\nfour\ntwo\nthree\nfive", 2, 3)]
    [InlineData("jVjJJ", "one\nfour\nfive\ntwo\nthree", 3, 4)]
    [InlineData("jVjJJJ", "one\nfour\nfive\ntwo\nthree", 3, 4)] // Stops at the end
    [InlineData("jVj2J", "one\nfour\nfive\ntwo\nthree", 3, 4)]
    [InlineData("jVjKK", "two\nthree\none\nfour\nfive", 0, 1)] // Stops at the top
    [InlineData("GVkK", "one\ntwo\nfour\nfive\nthree", 2, 3)]
    [InlineData("VJK", "one\ntwo\nthree\nfour\nfive", 0, 0)]
    public void SelectedLines_AreMovedWithJAndK(string keys, string expected, int firstLine, int lastLine)
    {
        var editor = Editor("one\ntwo\nthree\nfour\nfive");
        editor.Type(keys);
        Assert.Equal(expected, editor.GetText());
        // The lines stay selected
        Assert.Equal(EditorMode.VisualLine, editor.Mode);
        for (var line = 0; line < 5; line++)
        {
            Assert.Equal(line >= firstLine && line <= lastLine, editor.Selection(line) != null);
        }
    }

    [Fact]
    public void MovingSelectedLines_IsUndoneOneMoveAtATime()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.Type("VJJ<Esc>u");
        Assert.Equal("two\none\nthree", editor.GetText());
        editor.Type("u");
        Assert.Equal("one\ntwo\nthree", editor.GetText());
    }

    [Fact]
    public void Selection_IsReportedForDrawing()
    {
        var editor = Editor("one two\nthree");
        editor.Type("wvj");
        Assert.Equal((4, 7), editor.Selection(0));
        Assert.Equal((0, 5), editor.Selection(1));
        editor.Type("<Esc>V");
        Assert.Equal((0, 5), editor.Selection(1));
        Assert.Null(editor.Selection(0));
        editor.Type("<Esc>");
        Assert.Null(editor.Selection(1));
    }

    [Fact]
    public void PuttingOverASelection_ReplacesIt()
    {
        var editor = Editor("one two");
        editor.Type("yiwwviwp");
        Assert.Equal("one one", editor.GetText());
    }

    #endregion

    #region Undo, redo and repeat

    [Fact]
    public void UndoAndRedo()
    {
        var editor = Editor("one");
        editor.Type("A two<Esc>A three<Esc>");
        Assert.True(editor.IsModified);
        editor.Type("u");
        Assert.Equal("one two", editor.GetText());
        editor.Type("u");
        Assert.Equal("one", editor.GetText());
        Assert.False(editor.IsModified);
        editor.Type("u");
        Assert.Contains("oldest", editor.Message);
        editor.Type("<C-r>U");
        Assert.Equal("one two three", editor.GetText());
        editor.Type("2u");
        Assert.Equal("one", editor.GetText());
    }

    [Fact]
    public void IsModified_FollowsTheSavedState()
    {
        var editor = Editor("one");
        editor.Type("x");
        Assert.True(editor.IsModified);
        editor.MarkSaved();
        Assert.False(editor.IsModified);
        editor.Type("u");
        Assert.True(editor.IsModified);
        editor.Type("<C-r>");
        Assert.False(editor.IsModified);
    }

    [Theory]
    [InlineData("dw..", "four")]
    [InlineData("x3.", "two three four")]
    [InlineData("cwX<Esc>w.w.", "X X X four")]
    [InlineData("A!<Esc>..", "one two three four!!!")]
    [InlineData("dwu.", "two three four")]
    public void Repeat(string keys, string expected)
    {
        var editor = Editor("one two three four");
        editor.Type(keys);
        Assert.Equal(expected, editor.GetText());
    }

    [Fact]
    public void Repeat_WorksOnOtherLines()
    {
        var editor = Editor("a\nb\nc");
        editor.Type("A;<Esc>j.j.");
        Assert.Equal("a;\nb;\nc;", editor.GetText());
    }

    #endregion

    #region Search and command line

    [Fact]
    public void Search_FindsNextAndPreviousAndWraps()
    {
        var editor = Editor("one Two\nthree two\nTWO");
        editor.Type("/two<CR>");
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
        Assert.Equal(new[] { (4, 7) }, editor.SearchMatches(0));
        editor.Type("n");
        Assert.Equal(new TextPos(1, 6), editor.Cursor);
        editor.Type("nn"); // Wraps around
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
        editor.Type("N");
        Assert.Equal(new TextPos(2, 0), editor.Cursor);
        editor.Type("?three<CR>");
        Assert.Equal(new TextPos(1, 0), editor.Cursor);
        editor.Type("<Esc>");
        Assert.Empty(editor.SearchMatches(1));
    }

    [Fact]
    public void Search_NotFoundGivesAMessage()
    {
        var editor = Editor("one");
        editor.Type("/zzz<CR>");
        Assert.True(editor.MessageIsError);
        Assert.Equal(new TextPos(0, 0), editor.Cursor);
    }

    [Fact]
    public void Search_CanBeCanceled()
    {
        var editor = Editor("one two");
        editor.Type("/tw");
        Assert.Equal(EditorMode.Search, editor.Mode);
        Assert.Equal("tw", editor.CommandText);
        editor.Type("<BS><BS><BS>");
        Assert.Equal(EditorMode.Normal, editor.Mode);
        editor.Type("/two<Esc>");
        Assert.Equal(new TextPos(0, 0), editor.Cursor);
    }

    [Fact]
    public void Star_SearchesForTheWordUnderTheCursor()
    {
        var editor = Editor("foo bar\nbar foo");
        editor.Type("*");
        Assert.Equal(new TextPos(1, 4), editor.Cursor);
    }

    [Theory]
    [InlineData(":w<CR>", nameof(EditorRequest.Save))]
    [InlineData(":q<CR>", nameof(EditorRequest.Quit))]
    [InlineData(":q!<CR>", nameof(EditorRequest.ForceQuit))]
    [InlineData(":wq<CR>", nameof(EditorRequest.SaveAndQuit))]
    [InlineData(":x<CR>", nameof(EditorRequest.SaveAndQuit))]
    [InlineData("ZZ", nameof(EditorRequest.SaveAndQuit))]
    [InlineData("ZQ", nameof(EditorRequest.ForceQuit))]
    [InlineData(":help<CR>", nameof(EditorRequest.Help))]
    [InlineData(":cal<CR>", nameof(EditorRequest.InsertCalendar))]
    [InlineData(":clear<CR>", nameof(EditorRequest.ClearAll))]
    [InlineData(":w<Esc>", nameof(EditorRequest.None))]
    [InlineData("<Esc>", nameof(EditorRequest.Quit))] // Esc in Normal mode closes the editor
    [InlineData("i<Esc>", nameof(EditorRequest.None))]
    [InlineData("i<Esc><Esc>", nameof(EditorRequest.Quit))]
    [InlineData("v<Esc>", nameof(EditorRequest.None))]
    [InlineData("2d<Esc>", nameof(EditorRequest.None))]
    [InlineData("/te<CR><Esc>", nameof(EditorRequest.None))] // Removes the search highlighting
    [InlineData("/te<CR><Esc><Esc>", nameof(EditorRequest.Quit))]
    public void Commands_AskTheHost(string keys, string expected)
    {
        Assert.Equal(expected, Editor("text").Type(keys).ToString());
    }

    [Fact]
    public void Commands_EditTheText()
    {
        var editor = Editor("  one  \ntwo\nthree");
        editor.Type(":3<CR>");
        Assert.Equal(2, editor.Cursor.Line);
        editor.Type(":trim<CR>");
        Assert.Equal("one\ntwo\nthree", editor.GetText());
        editor.Type(":nonsense<CR>");
        Assert.True(editor.MessageIsError);
        editor.Type(":%d<CR>");
        Assert.Equal("", editor.GetText());
        editor.Type("P");
        Assert.Equal("one\ntwo\nthree\n", editor.GetText());
        editor.Type("gg:ts<CR>");
        Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\done", editor.GetText());
    }

    [Fact]
    public void PendingKeys_AreShownAndCanceledWithEsc()
    {
        var editor = Editor("one two three");
        editor.Type("2d");
        Assert.Equal("2d", editor.PendingKeys);
        editor.Type("<Esc>w");
        AssertState(editor, "one two three", 0, 4);
        Assert.Equal("", editor.PendingKeys);
    }

    #endregion

    #region Text handling

    [Fact]
    public void LineEndings_AreNormalized()
    {
        var editor = Editor("one\r\ntwo\rthree");
        Assert.Equal("one\ntwo\nthree", editor.GetText());
        Assert.False(editor.IsModified);
    }

    [Fact]
    public void CursorIndex_RoundTrips()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.SetCursorIndex(5);
        Assert.Equal(new TextPos(1, 1), editor.Cursor);
        Assert.Equal(5, editor.GetCursorIndex());
        editor.SetCursorIndex(1000);
        Assert.Equal(new TextPos(2, 4), editor.Cursor);
    }

    [Fact]
    public void Emoji_TheCursorAndWordMotionsNeverStopInsideOne()
    {
        // gj from column 2 onto a line where column 2 is the second half of 😀: on its first half
        var editor = Editor("abc\na😀b");
        editor.Type("llgj");
        Assert.Equal(new TextPos(1, 1), editor.Cursor);
        editor.Type("x");
        Assert.Equal("abc\nab", editor.GetText());

        // ❤️ is a symbol and a variation selector: w goes over both, and dw takes both
        editor = Editor("a ❤️ b");
        editor.Type("w");
        Assert.Equal(new TextPos(0, 2), editor.Cursor);
        editor.Type("w");
        Assert.Equal(new TextPos(0, 5), editor.Cursor);
        editor.Type("0wdw");
        Assert.Equal("a b", editor.GetText());
    }

    [Fact]
    public void Emoji_IsFoundAndReplacedWithF_T_AndR()
    {
        // The console gives 😀 as two keys (its surrogate halves): the commands take both
        var editor = Editor("ab😀cd😀e");
        editor.Type("f😀");
        Assert.Equal(new TextPos(0, 2), editor.Cursor);
        editor.Type(";");
        Assert.Equal(new TextPos(0, 6), editor.Cursor);
        editor.Type("0t😀");
        Assert.Equal(new TextPos(0, 1), editor.Cursor);
        editor.Type("0r🎉");
        Assert.Equal("🎉b😀cd😀e", editor.GetText());
        editor.Type("$F😀");
        Assert.Equal(new TextPos(0, 7), editor.Cursor);
    }

    [Fact]
    public void Emoji_ReplacingABlock_KeepsItsWidth()
    {
        // Three columns: one emoji (two columns) and a blank space
        var editor = Editor("abcd\nefgh");
        editor.Type("lQjllr😀");
        Assert.Equal("a😀 \ne😀 ", editor.GetText());
        Assert.Equal(4, TextMetrics.WidthOf(editor.GetText().Split('\n')[0]));
    }

    [Fact]
    public void WordMotions_GoOverACharacterWrittenWithACombiningAccent()
    {
        // "e" and a combining accent: one character, in the middle of a word
        var editor = Editor("café au lait");
        editor.Type("w");
        Assert.Equal(new TextPos(0, 6), editor.Cursor);
        editor.Type("b");
        Assert.Equal(new TextPos(0, 0), editor.Cursor);
    }

    [Fact]
    public void TextCutToFitTheScreen_HasNoControlCharacters()
    {
        Assert.Equal(" [31mname", Tools.TruncateString("\u001b[31mname", 40, true));
        Assert.DoesNotContain(Tools.TruncateString("a\u001b[2J" + new string('b', 50), 20, true), char.IsControl);
        Assert.Equal("... [0m.txt", TextMetrics.KeepEnd("/home/\u001b[0m.txt", 11));
    }

    [Fact]
    public void Emoji_SymbolsShownAsEmoji_AreTwoColumnsWide()
    {
        foreach (var symbol in new[] { "✅", "❌", "⭐", "⚡", "☕", "⌚", "🟢", "1️⃣", "❤️" })
        {
            Assert.Equal(2, TextMetrics.WidthOf(symbol));
        }

        // Text symbols stay one column
        foreach (var symbol in new[] { "✓", "→", "★", "❤", "1", "é" })
        {
            Assert.Equal(1, TextMetrics.WidthOf(symbol));
        }
    }

    [Fact]
    public void ControlCharacters_AreNotWrittenToTheTerminal()
    {
        // An escape code in a file name or a yanked line would change the colors or move the cursor
        Assert.Equal(" [31mred ", TextMetrics.Printable("\u001b[31mred\r"));
        Assert.DoesNotContain(TextMetrics.Printable("a\u001b[2Jb\rc\u0007"), c => char.IsControl(c));
        Assert.Equal("a b", TextMetrics.Printable("a\tb"));
        Assert.Equal("plain æøå 日本", TextMetrics.Printable("plain æøå 日本"));
    }

    [Fact]
    public void WideAndCombinedCharacters_AreHandledAsOne()
    {
        var text = "a\U0001F600b日本e\u0301x"; // a, emoji, b, two wide characters, e + combining accent, x
        var editor = Editor(text);
        Assert.Equal(1 + 2 + 1 + 2 + 2 + 1 + 1, TextMetrics.WidthOf(text));
        editor.Type("l");
        Assert.Equal(1, editor.Cursor.Col);
        editor.Type("l");
        Assert.Equal(3, editor.Cursor.Col); // Past both chars of the emoji
        editor.Type("hx");
        Assert.Equal("ab日本e\u0301x", editor.GetText());
        editor.Type("$hx");
        Assert.Equal("ab日本x", editor.GetText());
        Assert.Equal(1 + 1 + 2 + 2, editor.Layout.CellOf(new TextPos(0, 4)));
    }

    [Fact]
    public void WideCharacters_WrapByCells()
    {
        var editor = Editor("日本語日本語日本語", 10); // 9 characters of 2 cells
        Assert.Equal(new[] { 0, 5 }, editor.Layout.Segments(0));
    }

    [Fact]
    public void InsertAtCursor_AndClearAll()
    {
        var editor = Editor("one");
        editor.InsertAtCursor("a\r\nb\t");
        Assert.Equal("a\nb    one", editor.GetText());
        editor.ClearAll();
        Assert.Equal("", editor.GetText());
        editor.Type("u");
        Assert.Equal("a\nb    one", editor.GetText());
    }

    [Fact]
    public void LargeText_IsHandled()
    {
        var text = string.Join("\n", Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetuer adipiscing elit.", 20000));
        var editor = Editor(text, 78, 18);
        editor.Type("G");
        Assert.Equal(19999, editor.Cursor.Line);
        editor.Type("ddggdd:10000<CR>ix<Esc>");
        Assert.Equal(19998, editor.Buffer.LineCount);
        Assert.StartsWith("xLorem", editor.Buffer[9999]);
    }

    #endregion

    #region Single line editor

    private static EditorEngine SingleLine(string text, int maxCells = 10)
    {
        var editor = new EditorEngine(text, maxCells + 2, 1, true) { MaxCells = maxCells };
        editor.CursorToEnd();
        return editor;
    }

    [Fact]
    public void SingleLine_TypesAtTheEndAndAccepts()
    {
        var editor = SingleLine("abc");
        Assert.Equal(EditorMode.Insert, editor.Mode);
        Assert.Equal(EditorRequest.None, editor.Type("d<Left><Left>X<Home>1<End>2"));
        Assert.Equal("1abXcd2", editor.GetText());
        Assert.Equal(EditorRequest.Accept, editor.Type("<CR>"));
    }

    [Fact]
    public void SingleLine_EscCancels()
    {
        var editor = SingleLine("abc");
        Assert.Equal(EditorRequest.Cancel, editor.Type("<Esc>"));
        Assert.Equal(EditorMode.Insert, editor.Mode);
    }

    [Fact]
    public void SingleLine_IsLimitedInWidthAndNeverGetsLineBreaks()
    {
        var editor = SingleLine("one\ntwo");
        Assert.Equal("one two", editor.GetText());
        editor.Type("12345");
        Assert.Equal("one two123", editor.GetText());
        editor.Type("<BS><BS><BS><BS>日日日");
        Assert.Equal("one tw日日", editor.GetText());
        Assert.Equal(EditorRequest.None, editor.Type(":q"));
        Assert.Equal(EditorMode.Insert, editor.Mode);
    }

    #endregion

    #region Line numbers

    [Fact]
    public void LineNumberCommands()
    {
        var editor = Editor("text");
        Assert.False(editor.LineNumbers);
        Assert.Equal(EditorRequest.LineNumbersChanged, editor.Type(":number<CR>"));
        Assert.True(editor.LineNumbers);
        Assert.Equal(DialogText.LineNumbersOn, editor.Message);
        editor.Type(":nu<CR>");
        Assert.False(editor.LineNumbers);
        editor.Type(":set number<CR>");
        Assert.True(editor.LineNumbers);
        editor.Type(":set nu<CR>");
        Assert.True(editor.LineNumbers);
        editor.Type(":set nonumber<CR>");
        Assert.False(editor.LineNumbers);
        Assert.Equal(DialogText.LineNumbersOff, editor.Message);
    }

    [Fact]
    public void LayoutWidth_CanChange_AndTheTextIsWrappedAgain()
    {
        var editor = Editor("aaaa bbbb cccc dddd eeee ffff", 20, 5);
        Assert.Equal(2, editor.Layout.RowCount(0));
        editor.Type("$");
        // Narrower, as when line numbers take some of the width
        editor.SetLayoutWidth(10);
        Assert.Equal(3, editor.Layout.RowCount(0));
        Assert.Equal(new TextPos(0, 28), editor.Cursor);
        Assert.Equal(2, editor.Layout.RowOf(editor.Cursor));
        editor.SetLayoutWidth(40);
        Assert.Equal(1, editor.Layout.RowCount(0));
        Assert.Equal(0, editor.TopRow);
    }

    #endregion

    #region Reading the file again

    [Theory]
    [InlineData(":e<CR>", nameof(EditorRequest.Reload))]
    [InlineData(":edit<CR>", nameof(EditorRequest.Reload))]
    [InlineData(":reload<CR>", nameof(EditorRequest.Reload))]
    [InlineData(":e!<CR>", nameof(EditorRequest.ReloadForce))]
    public void ReloadCommands_AskTheHost(string keys, string expected)
    {
        Assert.Equal(expected, Editor("text").Type(keys).ToString());
    }

    [Fact]
    public void ReloadText_ReplacesTheText_AsSaved()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.Type("jlix<Esc>");
        Assert.True(editor.IsModified);
        editor.ReloadText("first\r\nsecond line\r\nthird\r\nfourth");
        Assert.Equal("first\nsecond line\nthird\nfourth", editor.GetText());
        Assert.False(editor.IsModified);
        Assert.Equal(EditorMode.Normal, editor.Mode);
        // The cursor stays on its line
        Assert.Equal(1, editor.Cursor.Line);
        // What was done before the file was read cannot be undone
        editor.Type("u");
        Assert.Equal("first\nsecond line\nthird\nfourth", editor.GetText());
        Assert.False(editor.IsModified);
        // A shorter text: the cursor is moved into it
        editor.Type("G");
        editor.ReloadText("only");
        AssertState(editor, "only", 0, 0);
    }

    #endregion
}

public class TextBufferTests
{
    [Fact]
    public void InsertAndDelete_AcrossLines()
    {
        var buffer = new TextBuffer("hello world");
        var end = buffer.Insert(new TextPos(0, 5), ",\nnew line\nand");
        Assert.Equal(new TextPos(2, 3), end);
        Assert.Equal("hello,\nnew line\nand world", buffer.GetText());
        Assert.Equal(3, buffer.LineCount);
        Assert.Equal(",\nnew line\nand", buffer.Delete(new TextPos(0, 5), end));
        Assert.Equal("hello world", buffer.GetText());
        Assert.Equal("lo w", buffer.GetText(new TextPos(0, 3), new TextPos(0, 7)));
    }

    [Fact]
    public void Steps_AreUndoneAsOne()
    {
        var buffer = new TextBuffer("abc");
        buffer.BeginStep(new TextPos(0, 1));
        buffer.Insert(new TextPos(0, 3), "d");
        buffer.Delete(new TextPos(0, 0), new TextPos(0, 1));
        buffer.Insert(new TextPos(0, 0), "X\nY");
        buffer.EndStep(new TextPos(1, 1));
        Assert.Equal("X\nYbcd", buffer.GetText());
        Assert.True(buffer.Undo(out var cursor));
        Assert.Equal("abc", buffer.GetText());
        Assert.Equal(new TextPos(0, 1), cursor);
        Assert.True(buffer.Redo(out cursor));
        Assert.Equal("X\nYbcd", buffer.GetText());
        Assert.Equal(new TextPos(1, 1), cursor);
        Assert.False(buffer.Redo(out _));
    }

    [Fact]
    public void UndoHistory_IsLimited()
    {
        var buffer = new TextBuffer("");
        for (var i = 0; i < TextBuffer.MaxUndoSteps + 10; i++)
        {
            buffer.Insert(buffer.End, "x");
        }

        var undone = 0;
        while (buffer.Undo(out _)) undone++;
        Assert.Equal(TextBuffer.MaxUndoSteps, undone);
        Assert.Equal(10, buffer.GetText().Length);
        Assert.True(buffer.IsModified); // The original text can no longer be reached
    }

    [Fact]
    public void IndexAndPosition_Convert()
    {
        var buffer = new TextBuffer("ab\n\ncde");
        Assert.Equal(new TextPos(2, 1), buffer.FromIndex(5));
        Assert.Equal(5, buffer.ToIndex(new TextPos(2, 1)));
        Assert.Equal(new TextPos(1, 0), buffer.FromIndex(3));
        Assert.Equal(buffer.End, buffer.FromIndex(99));
    }
}
