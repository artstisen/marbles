namespace Marbles.Tests;

/// <summary>
/// Comparisons (":diff"): the lines that differ, the unified diff written of them, its colors, and the keys and
/// commands of the editor for them
/// </summary>
[Collection("Static state")]
public class DiffTests
{
    public DiffTests()
    {
        Tools.ClearLineMemory();
    }

    private static string[] Lines(string text) => text.Length == 0 ? Array.Empty<string>() : text.Split('\n');

    // The lines of the old text are those not added, those of the new text those not removed
    private static void AssertRebuilds(string[] a, string[] b, List<LineDiff.Line> lines)
    {
        Assert.Equal(a, lines.Where(line => line.Kind != LineDiff.Kind.Added).Select(line => line.Kind == LineDiff.Kind.Same ? a[line.Old] : line.Text));
        Assert.Equal(b, lines.Where(line => line.Kind != LineDiff.Kind.Removed).Select(line => line.Text));
        Assert.All(lines.Where(line => line.Kind == LineDiff.Kind.Same), line => Assert.Equal(a[line.Old], b[line.New]));
    }

    // The fewest differences there can be: the lines of both minus twice the longest common part
    private static int Fewest(string[] a, string[] b)
    {
        var table = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return a.Length + b.Length - 2 * table[0, 0];
    }

    [Fact]
    public void TheLinesThatDiffer_AreFound()
    {
        var a = Lines("one\ntwo\nthree\nfour");
        var b = Lines("one\n2\nthree\nfour\nfive");
        var lines = LineDiff.Compare(a, b, false)!;
        Assert.Equal(new[] { "Same one", "Removed two", "Added 2", "Same three", "Same four", "Added five" },
            lines.Select(line => line.Kind + " " + line.Text));
        AssertRebuilds(a, b, lines);
    }

    [Fact]
    public void RandomTexts_AreRebuilt_WithTheFewestDifferences()
    {
        var random = new Random(7);
        for (var round = 0; round < 400; round++)
        {
            var a = Enumerable.Range(0, random.Next(0, 14)).Select(_ => "abcd"[random.Next(4)].ToString()).ToArray();
            var b = Enumerable.Range(0, random.Next(0, 14)).Select(_ => "abcd"[random.Next(4)].ToString()).ToArray();
            var lines = LineDiff.Compare(a, b, false)!;
            AssertRebuilds(a, b, lines);
            Assert.Equal(Fewest(a, b), lines.Count(line => line.Kind != LineDiff.Kind.Same));
        }
    }

    [Fact]
    public void BlankSpace_CanBeIgnored()
    {
        var a = Lines("if (x)\n    y = 1;");
        var b = Lines("if  (x)\n\ty = 1;  ");
        Assert.Contains(LineDiff.Compare(a, b, false)!, line => line.Kind != LineDiff.Kind.Same);
        Assert.All(LineDiff.Compare(a, b, true)!, line => Assert.Equal(LineDiff.Kind.Same, line.Kind));
    }

    [Fact]
    public void TooManyDifferences_AreNotCompared()
    {
        var a = Enumerable.Range(0, LineDiff.MaxDifferences).Select(i => "a" + i).ToArray();
        var b = Enumerable.Range(0, LineDiff.MaxDifferences).Select(i => "b" + i).ToArray();
        Assert.Null(LineDiff.Compare(a, b, false));
        // Many lines that are the same around a few that differ are quick
        var same = Enumerable.Range(0, 50000).Select(i => "line " + i).ToArray();
        var changed = same.ToArray();
        changed[25000] = "changed";
        Assert.Equal(2, LineDiff.Compare(same, changed, false)!.Count(line => line.Kind != LineDiff.Kind.Same));
    }

    [Fact]
    public void TheUnifiedDiff_HasTheChangesWithThreeLinesAround()
    {
        var a = Lines("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13\n14\n15\n16\n17\n18\n19\n20");
        var b = Lines("1\n2\n3\n4\nfive\n6\n7\n8\n9\n10\n11\n12\n13\n14\n15\n16\n17\n18\n19\n20\n21");
        var unified = LineDiff.Format(LineDiff.Compare(a, b, false)!, "old.txt", "new.txt");
        Assert.Equal(
            "--- old.txt\n+++ new.txt\n" +
            "@@ -2,7 +2,7 @@\n 2\n 3\n 4\n-5\n+five\n 6\n 7\n 8\n" +
            "@@ -18,3 +18,4 @@\n 18\n 19\n 20\n+21", unified.Text);
        Assert.Equal(new[] { 6, 15 }, unified.Changes);
        Assert.Equal(2, unified.Added);
        Assert.Equal(1, unified.Removed);
        // The places of the lines: "-5" in the old text, "+five" and " 6" in the new one
        Assert.Equal((true, 4, 4), unified.Places[6]);
        Assert.Equal((false, 4, 4), unified.Places[7]);
        Assert.Equal((false, 5, 5), unified.Places[8]);
        Assert.Equal((false, 20, 20), unified.Places[15]);
    }

    [Fact]
    public void ChangesCloseTogether_AreOnePart_AndAPartWithoutOldLinesTellsTheLineBefore()
    {
        var unified = LineDiff.Format(LineDiff.Compare(Lines("a\nb\nc\nd\ne"), Lines("a\nB\nc\nd\nE"), false)!, "x", "y");
        Assert.Single(unified.Text.Split('\n'), line => line.StartsWith("@@"));
        Assert.Equal(2, unified.Changes.Count);
        var added = LineDiff.Format(LineDiff.Compare(Lines("a"), Lines("a\nb"), false)!, "x", "y");
        Assert.Contains("@@ -1,1 +1,2 @@", added.Text);
        var first = LineDiff.Format(LineDiff.Compare(Array.Empty<string>(), Lines("a"), false)!, "x", "y");
        Assert.Contains("@@ -0,0 +1,1 @@", first.Text);
    }

    [Fact]
    public void AComparison_IsColored()
    {
        string Style(string line, int state) =>
            SyntaxHighlighter.Highlight(SyntaxType.Diff, line, state, out _)?[0].ToString() ?? "None";

        Assert.Equal("DiffFile", Style("--- old.txt", 0));
        Assert.Equal("DiffPart", Style("@@ -1,2 +1,2 @@", 0));
        Assert.Equal("DiffRemoved", Style("--- a line removed that starts with --", 1));
        Assert.Equal("DiffAdded", Style("+added", 1));
        Assert.Equal("None", Style(" same", 1));
        Assert.Equal(SyntaxType.Diff, SyntaxHighlighter.TypeOfFile("fix.patch"));
        Assert.Equal(SyntaxType.Diff, SyntaxHighlighter.ParseType("diff"));
        Assert.Equal(SyntaxType.Diff, EntrySyntax.Get(EntrySyntax.Set("fp", SyntaxType.Diff)));
    }

    private static EditorEngine Comparison()
    {
        var text = "--- a\n+++ b\n@@ -1,3 +1,3 @@\n x\n-y\n+Y\n z\n@@ -9,2 +9,2 @@\n-p\n+P";
        var editor = new EditorEngine(text, 60, 10, false) { ReadOnly = true, ChangeLines = new[] { 4, 9 } };
        return editor;
    }

    [Fact]
    public void NAndBrackets_GoToTheChanges()
    {
        var editor = Comparison();
        editor.Type("n");
        Assert.Equal(4, editor.Cursor.Line);
        editor.Type("n");
        Assert.Equal(9, editor.Cursor.Line);
        Assert.Equal(Lang.F("Change {0} of {1}", 2, 2), editor.Message);
        editor.Type("n");
        Assert.Equal(9, editor.Cursor.Line);
        Assert.Equal(DialogText.DiffNoMoreChanges, editor.Message);
        editor.Type("N");
        Assert.Equal(4, editor.Cursor.Line);
        editor.Type("]c");
        Assert.Equal(9, editor.Cursor.Line);
        editor.Type("[c");
        Assert.Equal(4, editor.Cursor.Line);
        editor.Type("gg2n");
        Assert.Equal(9, editor.Cursor.Line);
    }

    [Fact]
    public void AfterASearch_NFollowsIt_UntilEscRemovesTheHighlighting()
    {
        var editor = Comparison();
        editor.Type("/z<CR>");
        Assert.Equal(6, editor.Cursor.Line);
        editor.Type("n");
        Assert.Equal(6, editor.Cursor.Line);
        editor.Type("<Esc>n");
        Assert.Equal(9, editor.Cursor.Line);
    }

    [Fact]
    public void NIsTheSearch_InTextThatIsNotAComparison()
    {
        var editor = new EditorEngine("a\nb\na", 60, 10, false);
        editor.Type("/a<CR>");
        Assert.Equal(2, editor.Cursor.Line);
        editor.Type("n");
        Assert.Equal(0, editor.Cursor.Line);
        // "]c" does nothing there
        editor.Type("]c");
        Assert.Equal(0, editor.Cursor.Line);
    }

    [Fact]
    public void TheCommands_AskTheHost()
    {
        var editor = new EditorEngine("text", 60, 10, false);
        Assert.Equal(EditorRequest.Diff, editor.Type(":diff<CR>"));
        Assert.False(editor.DiffIgnoreSpace);
        Assert.Equal(EditorRequest.Diff, editor.Type(":diff -w<CR>"));
        Assert.True(editor.DiffIgnoreSpace);
        Assert.Equal(EditorRequest.WriteCopy, editor.Type(":w Changes.diff<CR>"));
        Assert.Equal("Changes.diff", editor.WriteName);
        Assert.Equal(EditorRequest.Save, editor.Type(":w<CR>"));
    }
}
