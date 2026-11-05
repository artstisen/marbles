namespace Marbles.Tests;

/// <summary>
/// Moving in the editor: marks ("ma", "`a", "'a", "``", ":marks"), "f" "t" "F" "T" across lines with the character
/// found shown, and the jump to a word with Enter. Also ":trim" and ":whitespace"
/// </summary>
[Collection("Static state")]
public class EditorNavigationTests
{
    public EditorNavigationTests()
    {
        Tools.ClearLineMemory();
    }

    private static EditorEngine Editor(string text, int width = 40, int height = 10)
    {
        return new EditorEngine(text, width, height, false);
    }

    #region f t F T

    [Theory]
    [InlineData("fa", 1, 1)]
    [InlineData("fz", 2, 2)]
    [InlineData("fa;", 2, 1)]
    [InlineData("fa;,", 1, 1)]
    [InlineData("2fa", 2, 1)]
    [InlineData("fq", 0, 0)] // Not in the text: the cursor stays
    [InlineData("tb", 0, 2)] // The first of the next line: to the end of the line above
    [InlineData("tb;", 1, 2)]
    [InlineData("G$Fo", 0, 2)]
    [InlineData("GTo", 1, 0)] // The last of the line above: to the start of the next line
    [InlineData("GTo;", 0, 2)]
    [InlineData("G$Ta", 2, 2)] // Past an empty line
    [InlineData("jjtb", 4, 3)]
    public void FindChar_GoesOnToTheOtherLines(string keys, int line, int col)
    {
        var editor = Editor("foo\nbar\nbaz\n\nend b");
        editor.Type(keys);
        Assert.Equal(new TextPos(line, col), editor.Cursor);
    }

    [Theory]
    [InlineData("dfa", "r\nbaz")]
    [InlineData("dta", "ar\nbaz")]
    [InlineData("jdfz", "foo\n")]
    [InlineData("G$dFr", "foo\nbaz")]
    public void FindChar_WithAnOperator_ChangesAcrossLines(string keys, string text)
    {
        var editor = Editor("foo\nbar\nbaz");
        editor.Type(keys);
        Assert.Equal(text, editor.GetText());
    }

    [Fact]
    public void TheCharacterFound_IsShownOnAllLines_UntilAnotherKey()
    {
        var editor = Editor("a-b\nc-d-e\nf");
        editor.Type("f-");
        Assert.True(editor.IsFoundShown);
        Assert.Equal(new[] { (1, 2) }, editor.FoundMatches(0));
        Assert.Equal(new[] { (1, 2), (3, 4) }, editor.FoundMatches(1));
        Assert.Empty(editor.FoundMatches(2));
        // ";" "," a count and a new "f" keep it
        editor.Type(";,2;");
        Assert.Equal(new TextPos(1, 3), editor.Cursor);
        Assert.True(editor.IsFoundShown);
        // ";" with nothing more to find
        editor.Type(";");
        Assert.True(editor.IsFoundShown);
        editor.Type("Fd");
        Assert.Equal(new[] { (2, 3) }, editor.FoundMatches(1));
        // Another key hides it
        editor.Type("l");
        Assert.False(editor.IsFoundShown);
        // Esc hides it, and only then closes the editor
        editor.Type("gg");
        Assert.Equal(EditorRequest.None, editor.Type("f-<Esc>"));
        Assert.False(editor.IsFoundShown);
        Assert.Equal(EditorRequest.Quit, editor.Type("<Esc>"));
    }

    [Fact]
    public void TheCharacterFound_IsNotShown_AfterAnOperator()
    {
        var editor = Editor("a-b-c");
        editor.Type("df-");
        Assert.False(editor.IsFoundShown);
        editor.Type("f-d;");
        Assert.False(editor.IsFoundShown);
    }

    #endregion

    #region The jump to a word

    [Fact]
    public void JumpLabels_AreSpreadEvenly_AndSharedLettersGetASecond()
    {
        Assert.Equal(new[] { "aaa", "aab", "ab", "ac", "ba", "bb", "bc", "ca", "cb", "cc" },
            EditorEngine.JumpLabelsFor(10, "abc"));
        Assert.Equal(new[] { "a", "b", "c" }, EditorEngine.JumpLabelsFor(3, "abc"));
        Assert.Equal(new[] { string.Empty }, EditorEngine.JumpLabelsFor(1, "abc"));
        // A screen full of words: two letters at the most
        var labels = EditorEngine.JumpLabelsFor(676);
        Assert.All(labels, label => Assert.Equal(2, label.Length));
        Assert.Equal(676, labels.Distinct().Count());
        // 30 spots: a b c d are shared by two each, e to z have one
        var thirty = EditorEngine.JumpLabelsFor(30);
        Assert.Equal(new[] { "aa", "ab", "ba", "bb", "ca", "cb", "da", "db", "e" }, thirty.Take(9));
        Assert.Equal("z", thirty[^1]);
    }

    [Theory]
    [InlineData("one two", 0, true)]
    [InlineData("one two", 4, true)]
    [InlineData("one two", 1, false)]
    [InlineData("foo.bar", 4, true)] // After punctuation
    [InlineData("(boo)", 1, true)]
    [InlineData("don't", 4, false)] // An apostrophe between letters is in the word
    [InlineData("'quoted'", 1, true)]
    [InlineData("# Heading", 0, false)]
    [InlineData("v2 is 2nd", 1, false)]
    [InlineData("v2 is 2nd", 6, true)]
    public void AWord_StartsAtALetterOrDigitThatDoesNotFollowOne(string text, int col, bool start)
    {
        Assert.Equal(start, EditorEngine.IsWordStart(text, col));
    }

    [Fact]
    public void Enter_ShowsTheLabels_AndALetterJumpsToTheWord()
    {
        var editor = Editor("one two three\nfour five");
        editor.Type("<CR>");
        Assert.True(editor.IsJumping);
        // The word under the cursor is left out
        Assert.Equal(new[] { (4, "a"), (8, "b") }, editor.JumpLabels(0));
        Assert.Equal(new[] { (0, "c"), (5, "d") }, editor.JumpLabels(1));
        editor.Type("c");
        Assert.False(editor.IsJumping);
        Assert.Equal(new TextPos(1, 0), editor.Cursor);
        // A capital letter is the same letter (from "four": one two three five)
        editor.Type("<CR>B");
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
    }

    [Fact]
    public void AJump_TakesTwoLetters_WhenThereAreMoreWordsThanLetters()
    {
        // 31 words: 30 spots, the first four letters are shared by two words each
        var editor = Editor(string.Join(" ", Enumerable.Range(0, 31).Select(i => "w" + i)), 40, 10);
        editor.Type("<CR>b");
        Assert.True(editor.IsJumping);
        Assert.Equal(new[] { (9, "a"), (12, "b") }, editor.JumpLabels(0));
        editor.Type("b");
        Assert.Equal(new TextPos(0, 12), editor.Cursor); // w4
        // A letter of one spot jumps at once
        editor.Type("0<CR>e");
        Assert.Equal(new TextPos(0, 27), editor.Cursor); // w9
    }

    [Fact]
    public void AJump_IsCanceledWithEsc_AndOtherKeysDoNothing()
    {
        var editor = Editor("one two three");
        editor.Type("<CR>?1");
        Assert.True(editor.IsJumping);
        // Esc cancels the jump, and does not close the editor
        Assert.Equal(EditorRequest.None, editor.Type("<Esc>"));
        Assert.False(editor.IsJumping);
        Assert.Equal(new TextPos(0, 0), editor.Cursor);
        // Enter goes to the first word
        editor.Type("<CR><CR>");
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
    }

    [Fact]
    public void Enter_WithACountOrAnOperator_StillGoesDownALine()
    {
        var editor = Editor("one\n  two\nthree\nfour");
        editor.Type("2<CR>");
        Assert.False(editor.IsJumping);
        Assert.Equal(new TextPos(2, 0), editor.Cursor);
        editor.Type("gg");
        editor.Type("d<CR>");
        Assert.Equal("three\nfour", editor.GetText());
    }

    [Fact]
    public void AJump_InVisualMode_ExtendsTheSelection_AndASingleWordIsJumpedToAtOnce()
    {
        var editor = Editor("one two three");
        editor.Type("v<CR>b");
        Assert.Equal(EditorMode.Visual, editor.Mode);
        Assert.Equal(new TextPos(0, 8), editor.Cursor);
        editor = Editor("one two");
        editor.Type("<CR>");
        Assert.False(editor.IsJumping);
        Assert.Equal(new TextPos(0, 4), editor.Cursor);
    }

    [Fact]
    public void OnlyTheWordsOnTheScreen_AreSpots()
    {
        // Height 2: the third line is not shown
        var editor = Editor("one two\nthree\nfour five", 40, 2);
        editor.Type("<CR>");
        Assert.Equal(2, editor.JumpLabels(0).Count() + editor.JumpLabels(1).Count());
        Assert.Empty(editor.JumpLabels(2));
    }

    [Fact]
    public void OnAVeryLongLineWithoutWrap_OnlyThePartShownHasSpots_AndTheyAreFoundAtOnce()
    {
        // About 240,000 chars on one line (as a minified file), scrolled to its end
        var text = string.Join(" ", Enumerable.Range(0, 40000).Select(i => "w" + i));
        var editor = new EditorEngine(text, 76, 20, false);
        editor.Type(":nowrap<CR>$");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        editor.Type("<CR>");
        watch.Stop();
        // It took 51 seconds when the cells were counted from the start of the line for each word
        Assert.True(watch.ElapsedMilliseconds < 3000, watch.ElapsedMilliseconds + " ms");
        var spots = editor.JumpLabels(0).ToList();
        Assert.NotEmpty(spots);
        Assert.All(spots, spot =>
        {
            var cell = TextMetrics.WidthOfRange(text, 0, spot.Col);
            Assert.InRange(cell, editor.LeftCell, editor.LeftCell + 75);
        });
    }

    [Fact]
    public void WhenTheViewIsScrolledDown_TheSpotsAreOnTheRowsShown()
    {
        var editor = Editor(string.Join("\n", Enumerable.Range(0, 30).Select(i => "line " + i)), 40, 5);
        editor.Type("G<CR>");
        Assert.True(editor.TopRow > 0);
        // Two words on each of the five lines shown, less the one under the cursor
        var shown = Enumerable.Range(0, 30).Where(line => editor.JumpLabels(line).Any()).ToList();
        Assert.Equal(Enumerable.Range(editor.TopRow, 5), shown);
        Assert.Equal(9, shown.Sum(line => editor.JumpLabels(line).Count()));
    }

    [Theory]
    [InlineData("Qj$<CR>bd", "wo three\nfour five six")] // The jump: to "two"
    [InlineData("Qj$Ftd", "hree\nfour five six")] // F: to the "t" of "three"
    public void ABlockSelectedToTheEnd_StopsAtTheCursor_AfterAJumpOrFind(string keys, string text)
    {
        var editor = Editor("one two three\nfour five six");
        editor.Type(keys);
        Assert.Equal(text, editor.GetText());
    }

    [Fact]
    public void TheJumpToAWord_IsNotMadeWhileRecording()
    {
        var editor = Editor("one two three");
        editor.StartRecording();
        editor.Type("<CR>");
        Assert.False(editor.IsJumping);
        Assert.True(editor.MessageIsError);
    }

    #endregion

    #region Marks

    [Fact]
    public void Marks_AreSetWithM_AndJumpedTo()
    {
        var editor = Editor("one\n  two words\nthree\nfour");
        editor.Type("jwwmb");
        Assert.Equal("Mark b: line 2", editor.Message);
        editor.Type("gg`b");
        Assert.Equal(new TextPos(1, 6), editor.Cursor);
        // ' goes to the start of the line (its first character)
        editor.Type("G'b");
        Assert.Equal(new TextPos(1, 2), editor.Cursor);
        // `` back to where the cursor was before the jump, and again
        editor.Type("``");
        Assert.Equal(new TextPos(3, 0), editor.Cursor);
        editor.Type("''");
        Assert.Equal(new TextPos(1, 2), editor.Cursor);
        editor.Type("`c");
        Assert.True(editor.MessageIsError);
        Assert.Contains("mc", editor.Message);
    }

    [Fact]
    public void AMark_FollowsTheChangesOfTheText_AndUndo()
    {
        var editor = Editor("a\nb\nc\nd");
        editor.Type("2jma");
        // A line above it: it moves down with its line
        editor.Type("ggOnew<Esc>");
        editor.Type("G`a");
        Assert.Equal(new TextPos(3, 0), editor.Cursor);
        Assert.Equal("c", editor.Buffer[editor.Cursor.Line]);
        // Two lines above it deleted: it moves up
        editor.Type("gg2dd`a");
        Assert.Equal("c", editor.Buffer[editor.Cursor.Line]);
        Assert.Equal(1, editor.Cursor.Line);
        // Undone: back with its line
        editor.Type("u`a");
        Assert.Equal("c", editor.Buffer[editor.Cursor.Line]);
        // Its own line deleted: it stays where the line was
        editor.Type("dd`a");
        Assert.Equal("d", editor.Buffer[editor.Cursor.Line]);
    }

    [Fact]
    public void GAndSearches_AreJumps_ThatBacktickBacktickGoesBackFrom()
    {
        var editor = Editor("one\ntwo\nthree\nfour");
        editor.Type("jG``");
        Assert.Equal(1, editor.Cursor.Line);
        editor.Type("/four<CR>``");
        Assert.Equal(1, editor.Cursor.Line);
        editor.Type(":3<CR>``");
        Assert.Equal(1, editor.Cursor.Line);
        // A move that is not a jump keeps the place to go back to
        editor.Type("G" + "k``");
        Assert.Equal(1, editor.Cursor.Line);
    }

    [Fact]
    public void MarkCommands_SetListAndDelete()
    {
        var editor = Editor("one\ntwo\nthree");
        editor.Type("j:mark a<CR>G:k c<CR>");
        Assert.Equal(new[] { 'a', 'c' }, editor.MarkList.Select(mark => mark.Name));
        Assert.Equal(1, editor.MarkList[0].At.Line);
        Assert.Equal(EditorRequest.Marks, editor.Type(":marks<CR>"));
        editor.Type(":mark A<CR>");
        Assert.True(editor.MessageIsError);
        editor.Type(":delmarks a-b<CR>");
        Assert.Equal(new[] { 'c' }, editor.MarkList.Select(mark => mark.Name));
        editor.Type(":delmarks!<CR>");
        Assert.Empty(editor.MarkList);
        Assert.Equal('\0', editor.MarkOnLine(2));
        editor.Type("mz");
        Assert.Equal('z', editor.MarkOnLine(2));
    }

    [Fact]
    public void WithTheSetting_TheApostropheJumpsToTheExactPlace()
    {
        try
        {
            EditorEngine.MarksApostrophe = true;
            var editor = Editor("one\n  two words");
            editor.Type("jwwmagg'a");
            Assert.Equal(new TextPos(1, 6), editor.Cursor);
            editor.Type("''");
            Assert.Equal(new TextPos(0, 0), editor.Cursor);
        }
        finally
        {
            EditorEngine.MarksApostrophe = false;
        }
    }

    [Fact]
    public void TheMarksOfAText_AreKeptAndSetAgain()
    {
        var editor = Editor("a\nb\nc");
        editor.Type("jmb");
        var kept = editor.CopyMarks();
        var again = Editor("a\nb\nc");
        again.LoadMarks(kept);
        again.Type("`b");
        Assert.Equal(1, again.Cursor.Line);
    }

    [Fact]
    public void TheMarks_AreNotLogged()
    {
        // The log is read without a password: a command is logged by its name only, not the letter of a mark
        var editor = Editor("a\nb");
        editor.Type(":k b<CR>:delmarks b<CR>");
        Assert.DoesNotContain(EventLog.All, line => line.Contains("k b") || line.Contains("delmarks b"));
    }

    [Theory]
    [InlineData(2, 3, 1, 0, 1, 0, 2, 3)] // Before the insert: stays
    [InlineData(2, 3, 2, 1, 2, 4, 2, 6)] // On the line after it: moves along
    [InlineData(2, 3, 1, 5, 3, 0, 4, 3)] // A later line: down by the lines put in
    public void AMark_AfterAnInsert(int line, int col, int atLine, int atCol, int endLine, int endCol, int toLine,
        int toCol)
    {
        Assert.Equal(new TextPos(toLine, toCol), TextMarks.AfterInsert(new TextPos(line, col),
            new TextPos(atLine, atCol), new TextPos(endLine, endCol)));
    }

    [Theory]
    [InlineData(5, 2, 1, 0, 3, 0, 3, 2)] // After it: up by the lines taken out
    [InlineData(2, 2, 1, 0, 3, 0, 1, 0)] // In it: where it was
    [InlineData(3, 4, 1, 2, 3, 1, 1, 5)] // On its last line, after it: along the line
    [InlineData(0, 1, 1, 0, 3, 0, 0, 1)] // Before it: stays
    public void AMark_AfterADelete(int line, int col, int fromLine, int fromCol, int toLine, int toCol, int newLine,
        int newCol)
    {
        Assert.Equal(new TextPos(newLine, newCol), TextMarks.AfterDelete(new TextPos(line, col),
            new TextPos(fromLine, fromCol), new TextPos(toLine, toCol)));
    }

    #endregion

    #region :trim and :whitespace

    [Fact]
    public void Trim_RemovesBlankSpaceAtLineEnds_AndKeepsIndentation()
    {
        var editor = Editor("  one \t\n    two  \n\t\nthree");
        editor.Type(":trim<CR>");
        Assert.Equal("  one\n    two\n\nthree", editor.GetText());
        Assert.Equal("Lines trimmed: 3", editor.Message);
        // Undone as one step
        editor.Type("u");
        Assert.Equal("  one \t\n    two  \n\t\nthree", editor.GetText());
    }

    [Fact]
    public void Trim_KeepsTheLineBreaksOfMarkdown_UnlessTrimBang()
    {
        // Two spaces after text, with a line of text after it: a line break. Before an empty line, or at the end of
        // the text, they break nothing
        const string text = "Roses are red  \nViolets are blue  \n\nEnd  ";
        var editor = new EditorEngine(text, 40, 10, false) { CompletionSyntax = SyntaxType.Markdown };
        editor.Type(":trim<CR>");
        Assert.Equal("Roses are red  \nViolets are blue\n\nEnd", editor.GetText());
        Assert.Contains(":trim!", editor.Message);
        editor.Type(":trim!<CR>");
        Assert.Equal("Roses are red\nViolets are blue\n\nEnd", editor.GetText());
        // Not Markdown: all is trimmed
        var plain = Editor(text);
        plain.Type(":trim<CR>");
        Assert.Equal("Roses are red\nViolets are blue\n\nEnd", plain.GetText());
        // A note colored as Markdown (":syntax md") keeps them too
        var note = Editor(text);
        note.Type(":syntax md<CR>:trim<CR>");
        Assert.Equal("Roses are red  \nViolets are blue\n\nEnd", note.GetText());
    }

    [Fact]
    public void Trim_WorksOnTheSelectedLines()
    {
        var editor = Editor("a \nb \nc ");
        editor.Type("jVj:trim<CR>");
        Assert.Equal("a \nb\nc", editor.GetText());
        editor.Type(":1,1trim<CR>");
        Assert.Equal("a\nb\nc", editor.GetText());
        editor.Type(":trim<CR>");
        Assert.Equal("No blank space at line ends", editor.Message);
    }

    [Fact]
    public void Trim_DoesNotChangeAReadOnlyText()
    {
        var editor = new EditorEngine("a  \nb", 40, 10, false) { ReadOnly = true };
        editor.Type(":trim<CR>");
        Assert.Equal("a  \nb", editor.GetText());
    }

    [Fact]
    public void Whitespace_IsAskedOfTheHost()
    {
        var editor = Editor("text");
        Assert.Equal(EditorRequest.WhitespaceToggle, editor.Type(":whitespace<CR>"));
    }

    [Fact]
    public void TheSpacesOfAMarkdownLineBreak_AreKnown_AsTrimKeepsThem()
    {
        var editor = new EditorEngine("Roses  \nViolets  \n\nEnd \t", 40, 10, false)
            { CompletionSyntax = SyntaxType.Markdown };
        Assert.True(editor.IsLineBreakSpace(0));
        // Before an empty line, at the end of the text, or with a tab: no line break
        Assert.False(editor.IsLineBreakSpace(1));
        Assert.False(editor.IsLineBreakSpace(3));
        Assert.False(editor.IsLineBreakSpace(9));
        // The line after it emptied: no longer one
        editor.Type("jdd");
        Assert.False(editor.IsLineBreakSpace(0));
        // Not Markdown
        Assert.False(Editor("Roses  \nViolets").IsLineBreakSpace(0));
    }

    #endregion
}
