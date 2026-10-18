namespace Marbles.Tests;

/// <summary>
/// The Jump menu: the jump history (jump.txt), the places it goes to, and the names it shows
/// </summary>
[Collection("Static state")]
public class JumpTests : IDisposable
{
    private readonly string _root;
    private readonly string? _savedPath = JumpHistory.FilePathOverride;

    public JumpTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "marbles_jump_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        JumpHistory.FilePathOverride = Path.Combine(_root, JumpHistory.FileName);
        JumpHistory.Clear();
    }

    public void Dispose()
    {
        JumpHistory.FilePathOverride = _savedPath;
        JumpHistory.Clear();
        Directory.Delete(_root, true);
    }

    private string In(string name) => Path.Combine(_root, name);

    [Fact]
    public void TheJumpHistory_HasTheLatestFirst_AndAPlaceOnce()
    {
        var time = new DateTime(2026, 10, 14, 9, 40, 0);
        JumpHistory.Add(new JumpTarget(JumpKind.File, In("report.md")), time);
        JumpHistory.Add(new JumpTarget(JumpKind.Page, "config/editor"), time.AddMinutes(1));
        JumpHistory.Add(new JumpTarget(JumpKind.DataFile, "notes/work.mbls"), time.AddMinutes(2));
        JumpHistory.Add(new JumpTarget(JumpKind.Directory, _root), time.AddMinutes(3));
        // Jumped to again: to the top
        JumpHistory.Add(new JumpTarget(JumpKind.File, In("report.md")), time.AddMinutes(4));
        Assert.Equal(new[] { JumpKind.File, JumpKind.Directory, JumpKind.DataFile, JumpKind.Page },
            JumpHistory.Items.Select(entry => entry.Target.Kind));
        Assert.Equal(time.AddMinutes(4), JumpHistory.Items[0].Jumped);

        // The file: a line for each place, read again the same
        var text = JumpHistory.Serialize();
        Assert.StartsWith("2026-10-14 09:44\tfile\t" + In("report.md") + "\n", text);
        Assert.Contains("2026-10-14 09:42\tdata\tnotes/work.mbls\n", text);
        Assert.Contains("2026-10-14 09:41\tpage\tconfig/editor\n", text);
        Assert.Equal(DialogText.Success, JumpHistory.Save());
        JumpHistory.Clear();
        JumpHistory.Load();
        Assert.Equal(text, JumpHistory.Serialize());

        // Lines that cannot be read are left out, and a place only once
        JumpHistory.Parse("nonsense\n2026-10-14 09:00\tother\tx\n2026-10-14 09:00\tpage\tschemes\n" +
                          "2026-10-14 08:00\tpage\tSCHEMES\nnot a time\tpage\tlogos\n");
        Assert.Equal(new[] { "schemes", "logos" }, JumpHistory.Items.Select(entry => entry.Target.Target));
        Assert.Equal(DateTime.MinValue, JumpHistory.Items[1].Jumped);

        JumpHistory.RemoveAt(0);
        Assert.Equal("logos", Assert.Single(JumpHistory.Items).Target.Target);
    }

    [Fact]
    public void TheJumpHistory_HoldsAtMost100Places()
    {
        for (var i = 0; i < JumpHistory.MaxCount + 5; i++)
        {
            JumpHistory.Add(new JumpTarget(JumpKind.File, In("file" + i + ".txt")), DateTime.Now);
        }

        Assert.Equal(JumpHistory.MaxCount, JumpHistory.Items.Count);
        Assert.Equal("file104.txt", JumpHistory.Items[0].Target.Name);
    }

    [Fact]
    public void APlace_IsShownByItsName_NotItsPath()
    {
        var file = In("a-long-file-name.md");
        File.WriteAllText(file, "x");
        Assert.Equal("a-long-file-name.md", new JumpTarget(JumpKind.File, file).Name);
        Assert.True(new JumpTarget(JumpKind.File, file).Exists);
        Assert.False(new JumpTarget(JumpKind.File, In("gone.md")).Exists);
        Assert.Equal(Path.GetFileName(_root), new JumpTarget(JumpKind.Directory, _root + Path.DirectorySeparatorChar).Name);
        Assert.True(new JumpTarget(JumpKind.Directory, _root).Exists);
        Assert.Equal("notes/work.mbls", new JumpTarget(JumpKind.DataFile, "notes/work.mbls").Name);
        // A page by its name in the language in use, and a page that is not one is not there
        Assert.Equal(Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("Editor")),
            new JumpTarget(JumpKind.Page, "config/editor").Name);
        Assert.True(new JumpTarget(JumpKind.Page, "about").Exists);
        Assert.False(new JumpTarget(JumpKind.Page, "nowhere").Exists);

        // Long names are shortened in the middle, so the end (the extension) is seen
        Assert.Equal("a-long...me.md", JumpMenu.ShortName("a-long-file-name.md", 14));
        Assert.Equal(14, JumpMenu.ShortName("a-long-file-name.md", 14).Length);
        Assert.Equal("short.md", JumpMenu.ShortName("short.md", 14));
    }

    [Fact]
    public void ThePages_OfTheConfigurationTab_AreLeft_TheOthersOpenOnTop()
    {
        Assert.Equal(new[] { 0, 1, 2, 3 }, new[] { "config/general", "config/layout", "config/editor", "config/system" }
            .Select(JumpPages.ConfigurationPage));
        Assert.Equal(-1, JumpPages.ConfigurationPage("schemes"));
        foreach (var page in new[] { "schemes", "logos", "clipboard", "snippets", "completions", "help", "about" })
        {
            Assert.True(Jump.OpensInPlace(new JumpTarget(JumpKind.Page, page)));
        }

        Assert.False(Jump.OpensInPlace(new JumpTarget(JumpKind.Page, "config/system")));
        Assert.False(Jump.OpensInPlace(new JumpTarget(JumpKind.File, In("a.md"))));
        Assert.Equal(11, JumpPages.Ids.Count);
        Assert.All(JumpPages.Ids, id => Assert.NotEqual(id, JumpPages.Label(id)));
    }

    [Fact]
    public void ARenamedFile_IsFollowed_ByTheJumpsAndTheHistory()
    {
        var time = new DateTime(2026, 10, 14, 9, 0, 0);
        JumpHistory.Add(new JumpTarget(JumpKind.File, In("old.md")), time);
        JumpHistory.Add(new JumpTarget(JumpKind.File, In(Path.Combine("dir", "inside.md"))), time);
        JumpHistory.Add(new JumpTarget(JumpKind.Page, "about"), time);
        Assert.True(JumpHistory.PathChanged(In("old.md"), In("new.md")));
        Assert.Contains(JumpHistory.Items, entry => entry.Target.Target == In("new.md"));
        Assert.DoesNotContain(JumpHistory.Items, entry => entry.Target.Target == In("old.md"));
        // A file in a renamed directory follows it, and a page is not touched
        Assert.True(JumpHistory.PathChanged(In("dir"), In("renamed")));
        Assert.Contains(JumpHistory.Items, entry => entry.Target.Target == In(Path.Combine("renamed", "inside.md")));
        Assert.False(JumpHistory.PathChanged(In("other.md"), In("x.md")));
        Assert.Equal("about", JumpHistory.Items.Single(entry => entry.Target.Kind == JumpKind.Page).Target.Target);

        var savedHistory = FileHistory.FilePathOverride;
        try
        {
            FileHistory.FilePathOverride = In("history.txt");
            FileHistory.Clear();
            FileHistory.Add(In("old.md"), time);
            FileHistory.SetCaret(In("old.md"), 4, 2);
            Assert.True(FileHistory.PathChanged(In("old.md"), In("new.md")));
            // The place of the cursor goes with it
            Assert.Equal((4, 2, ""), FileHistory.CaretOf(In("new.md")));
            Assert.Null(FileHistory.CaretOf(In("old.md")));
        }
        finally
        {
            FileHistory.Clear();
            FileHistory.FilePathOverride = savedHistory;
        }
    }

    [Fact]
    public void RenameAndTitle_AskTheEditorToRename_WithTheNameAsTyped()
    {
        var editor = new EditorEngine("text", 40, 10, false);
        Assert.Equal(EditorRequest.Rename, editor.Type(":rename Notes 2026.MD<CR>"));
        Assert.Equal("Notes 2026.MD", editor.RenameText);
        Assert.False(editor.RenameIsTitle);
        Assert.Equal(EditorRequest.Rename, editor.Type(":title A New Title<CR>"));
        Assert.Equal("A New Title", editor.RenameText);
        Assert.True(editor.RenameIsTitle);
        // Without a name: it is asked for
        Assert.Equal(EditorRequest.Rename, editor.Type(":rename<CR>"));
        Assert.Equal(string.Empty, editor.RenameText);
        Assert.Equal("text", editor.Buffer.GetText());
    }

    [Fact]
    public void CtrlO_OpensTheJumpMenu_InTheEditorToo()
    {
        Assert.True(Jump.IsKey(new ConsoleKeyInfo('\u000f', ConsoleKey.O, false, false, true)));
        Assert.False(Jump.IsKey(new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false)));
        Assert.False(Jump.IsKey(new ConsoleKeyInfo('O', ConsoleKey.O, true, false, false)));
        var editor = new EditorEngine("one two", 40, 10, false);
        Assert.Equal(EditorRequest.Jump, editor.Type(":jump<CR>"));
        Assert.Equal(EditorRequest.Jump, editor.Type(":ju<CR>"));
        Assert.Equal(EditorRequest.Jump, editor.HandleKey(new ConsoleKeyInfo('\u000f', ConsoleKey.O, false, false, true)));
        // From INSERT mode, which is left
        editor.Type("i");
        Assert.Equal(EditorMode.Insert, editor.Mode);
        Assert.Equal(EditorRequest.Jump, editor.HandleKey(new ConsoleKeyInfo('\u000f', ConsoleKey.O, false, false, true)));
        Assert.Equal(EditorMode.Normal, editor.Mode);
        Assert.Equal("one two", editor.Buffer.GetText());
    }
}
