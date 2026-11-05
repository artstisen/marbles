namespace Marbles.Tests;

/// <summary>
/// The bookmarks of the Jump menu (bookmarks.txt), and the tabs of the menu: tabs at the top, the lists of the file
/// browser as sub-tabs, Tab between the tabs and h l through all the lists
/// </summary>
[Collection("Static state")]
public class BookmarksTests : IDisposable
{
    private readonly string _directory;

    public BookmarksTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "marbles-bookmarks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Bookmarks.FilePathOverride = Path.Combine(_directory, Bookmarks.FileName);
        Bookmarks.Clear();
    }

    public void Dispose()
    {
        Bookmarks.FilePathOverride = null;
        Bookmarks.Clear();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string In(string name) => Path.GetFullPath(Path.Combine(_directory, name));
    private JumpTarget FileAt(string name) => new(JumpKind.File, In(name));

    [Fact]
    public void Places_AreAddedAtTheEnd_Once_AndThereIsNoLimit()
    {
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(FileAt("a.md")));
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(new JumpTarget(JumpKind.Page, "schemes")));
        Assert.Equal(Bookmarks.AddResult.AlreadyThere, Bookmarks.Add(FileAt("a.md")));
        Assert.Equal(2, Bookmarks.Items.Count);
        // (there were at most 100)
        for (var i = Bookmarks.Items.Count; i < 150; i++) Bookmarks.Add(FileAt("f" + i + ".md"));
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(FileAt("one more.md")));
        Assert.Equal(151, Bookmarks.Places.Count);
        Bookmarks.Parse(Bookmarks.Serialize());
        Assert.Equal(151, Bookmarks.Places.Count);
    }

    [Fact]
    public void TheList_IsSavedAndReadAgain_WithTheKindsAndNames()
    {
        Bookmarks.Add(FileAt("a.md"));
        Bookmarks.Add(new JumpTarget(JumpKind.Directory, In("docs")));
        Bookmarks.Add(new JumpTarget(JumpKind.Page, "config/editor"));
        Bookmarks.Add(new JumpTarget(JumpKind.DataFile, "notes/work.mbls"));
        Bookmarks.Rename(0, "Budget");
        Assert.Equal("file\t" + In("a.md") + "\tBudget\ndir\t" + In("docs") + "\npage\tconfig/editor\n" +
                     "data\tnotes/work.mbls\n", Bookmarks.Serialize());
        Assert.Equal(DialogText.Success, Bookmarks.Save());
        Bookmarks.Clear();
        Bookmarks.Load();
        Assert.Equal(4, Bookmarks.Items.Count);
        Assert.Equal("Budget", Bookmarks.Items[0].Shown);
        Assert.Equal(new JumpTarget(JumpKind.DataFile, "notes/work.mbls"), Bookmarks.Items[3].Target);
        // Edited by hand: an unknown kind, a blank line and a place there twice are not used
        Bookmarks.Parse("nonsense\tx\n\nfile\t" + In("a.md") + "\nfile\t" + In("a.md") + "\r\n");
        Assert.Single(Bookmarks.Items);
    }

    [Fact]
    public void Bookmarks_AreMoved_Renamed_RemovedAndPutBack()
    {
        foreach (var name in new[] { "a", "b", "c" }) Bookmarks.Add(FileAt(name));
        Bookmarks.MoveToEnd(new[] { Bookmarks.Items[0] }, false);
        Assert.Equal(new[] { "b", "c", "a" }, Bookmarks.Items.Select(entry => entry.Shown));
        Bookmarks.MoveToEnd(new[] { Bookmarks.Items[2] }, true);
        Assert.True(Bookmarks.Move(2, true));
        Assert.True(Bookmarks.Move(1, false));
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
        Bookmarks.Rename(1, "  Mine\there ");
        Assert.Equal("Mine here", Bookmarks.Items[1].Shown);
        // An empty name, or the name of the place, shows its own name again
        Bookmarks.Rename(1, string.Empty);
        Assert.Equal("b", Bookmarks.Items[1].Shown);
        Bookmarks.RemoveAt(1);
        Assert.Equal(new[] { "a", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
        Assert.Equal(1, Bookmarks.UndoRemove());
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
    }

    [Fact]
    public void Folders_AreSavedWithColors_AndTheBookmarksAreNumberedWithoutThem()
    {
        foreach (var name in new[] { "a", "b", "c" }) Bookmarks.Add(FileAt(name));
        // A folder in front of b, and b moved down into it, then the folder closed
        Assert.Equal(1, Bookmarks.AddFolder("Work", 1));
        Bookmarks.MoveToFolder(Bookmarks.Places.Where(entry => entry.Shown != "a").ToList(), Bookmarks.Items[1]);
        Bookmarks.SetColor(0, ConsoleColor.Red);
        Bookmarks.SetColor(1, ConsoleColor.Green);
        Bookmarks.Items[1].Collapsed = true;

        // The bookmarks are numbered 1, 2, 3 in the order of the list: the folder is not counted, and the bookmarks
        // in the closed folder are
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Places.Select(entry => entry.Shown).OrderBy(name => name));
        Assert.Equal(1, Bookmarks.NumberOf(Bookmarks.Places[0]));
        Assert.Equal(3, Bookmarks.Places.Count);
        Assert.Equal(0, Bookmarks.NumberOf(Bookmarks.Items[1]));

        // Saved and read again as it was
        var text = Bookmarks.Serialize();
        Assert.Contains("[Work]\tGreen\tcollapsed\n", text);
        Assert.Contains("\tRed\n", text);
        Assert.Equal(DialogText.Success, Bookmarks.Save());
        var before = Bookmarks.Items.Select(entry => (entry.Shown, entry.IsFolder, entry.InFolder, entry.Color)).ToList();
        Bookmarks.Clear();
        Bookmarks.Load();
        Assert.Equal(before, Bookmarks.Items.Select(entry => (entry.Shown, entry.IsFolder, entry.InFolder, entry.Color)));
        Assert.True(Bookmarks.Items[1].Collapsed);
        Assert.Equal(2, Bookmarks.FolderCount(1));

        // A folder is removed with its bookmarks, and put back with them
        Bookmarks.RemoveAt(1);
        Assert.Single(Bookmarks.Places);
        Bookmarks.UndoRemove();
        Assert.Equal(3, Bookmarks.Places.Count);
        Assert.True(Bookmarks.Items[2].InFolder);
    }

    [Fact]
    public void AFolderName_CannotHaveBrackets_AndBookmarksFromBefore_AreRead()
    {
        Assert.Equal(-1, Bookmarks.AddFolder("[x]", 0));
        Assert.Equal(-1, Bookmarks.AddFolder("  ", 0));
        // A file written before there were colors and folders
        Bookmarks.Parse("file\t" + In("a.md") + "\npage\thelp\tHelp me\n");
        Assert.Equal(new[] { "a.md", "Help me" }, Bookmarks.Items.Select(entry => entry.Shown));
        Assert.All(Bookmarks.Items, entry => Assert.Null(entry.Color));
    }

    [Fact]
    public void ARenamedOrMovedFileOrDirectory_IsFollowed()
    {
        Bookmarks.Add(new JumpTarget(JumpKind.Directory, In("work")));
        Bookmarks.Add(FileAt(Path.Combine("work", "notes.md")));
        Bookmarks.Add(new JumpTarget(JumpKind.Page, "help"));
        Assert.True(Bookmarks.PathChanged(In("work"), In("job")));
        Assert.Equal(In("job"), Bookmarks.Items[0].Target.Target);
        Assert.Equal(In(Path.Combine("job", "notes.md")), Bookmarks.Items[1].Target.Target);
        Assert.Equal("help", Bookmarks.Items[2].Target.Target);
    }

    [Fact]
    public void AddWithMessage_SavesAndTells()
    {
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "a.md"), Bookmarks.AddWithMessage(FileAt("a.md")));
        Assert.Equal(Lang.F("Already bookmarked ({0}): {1}", 1, "a.md"), Bookmarks.AddWithMessage(FileAt("a.md")));
        Assert.Contains("a.md", File.ReadAllText(Bookmarks.FilePath));
        // The number is the one ":book" takes
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 2, "b.md"), Bookmarks.AddWithMessage(FileAt("b.md")));
    }

    [Fact]
    public void ABookmarkMadeInTheEditor_KeepsItsLineAndColumn()
    {
        var chapter = FileAt("chapter.md");
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "chapter.md:120"),
            Bookmarks.AddWithMessage(chapter with { At = (120, 5) }));
        // Another line of the same file, and the file itself, are other bookmarks; the same line is not
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 2, "chapter.md:3"),
            Bookmarks.AddWithMessage(chapter with { At = (3, 1) }));
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 3, "chapter.md"), Bookmarks.AddWithMessage(chapter));
        Assert.Equal(Lang.F("Already bookmarked ({0}): {1}", 1, "chapter.md:120"),
            Bookmarks.AddWithMessage(chapter with { At = (120, 9) }));

        // Kept in the file after the name and color, and read again
        var text = File.ReadAllText(Bookmarks.FilePath);
        Assert.Contains("file\t" + In("chapter.md") + "\t\t\t120:5\n", text);
        Bookmarks.Load();
        Assert.Equal(3, Bookmarks.Places.Count);
        Assert.Equal((120, 5), Bookmarks.Places[0].Target.At);
        Assert.Null(Bookmarks.Places[2].Target.At);
        // The name offered when renaming (with the line), kept as it is: no name of its own
        Assert.True(Bookmarks.Rename(0, "chapter.md:120"));
        Assert.Equal(string.Empty, Bookmarks.Places[0].Name);
        // With a name
        Assert.True(Bookmarks.Rename(0, "Chapter 3"));
        Bookmarks.Parse(Bookmarks.Serialize());
        Assert.Equal("Chapter 3", Bookmarks.Places[0].Shown);
        Assert.Equal((120, 5), Bookmarks.Places[0].Target.At);
        // A place that cannot be read is a bookmark of the file
        Bookmarks.Parse("file\t" + In("x.md") + "\t\t\tnone\n");
        Assert.Null(Bookmarks.Places[0].Target.At);
    }

    [Fact]
    public void APlaceInANote_IsNeverKept()
    {
        // A data file is bookmarked by its path in the data directory only: a line and column given for it (as a
        // place in one of its notes would be) is not kept, nor read from a file edited by hand
        var data = new JumpTarget(JumpKind.DataFile, "notes/work.mbls") { At = (12, 3) };
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "notes/work.mbls"), Bookmarks.AddWithMessage(data));
        Assert.Null(Bookmarks.Places[0].Target.At);
        Assert.Equal("data\tnotes/work.mbls\n", File.ReadAllText(Bookmarks.FilePath));
        Bookmarks.Parse("data\tnotes/work.mbls\t\t\t12:3\n");
        Assert.Null(Bookmarks.Places[0].Target.At);
        Assert.Equal("data\tnotes/work.mbls\n", Bookmarks.Serialize());
        Assert.Equal(Bookmarks.AddResult.AlreadyThere, Bookmarks.Add(data));
        // A name given by the user is theirs to give
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "work"),
            Bookmarks.AddWithMessage(data, "work", false, out var failed));
        Assert.False(failed);
        Assert.Equal("data\tnotes/work.mbls\twork\n", File.ReadAllText(Bookmarks.FilePath));
    }

    [Fact]
    public void ABookmarkWithAName_IsFoundByIt_AndTheNameIsUsedOnce()
    {
        var chapter = FileAt("chapter.md") with { At = (12, 1) };
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "chapter3"),
            Bookmarks.AddWithMessage(chapter, "chapter3", false, out var failed));
        Assert.False(failed);
        Assert.Equal(":book chapter3", Bookmarks.CommandOf(Bookmarks.Places[0]));
        Assert.Same(Bookmarks.Places[0], Bookmarks.FindByName("CHAPTER3"));
        // Another place with the name: refused, unless the name is moved
        var notes = FileAt("notes.md");
        Assert.Equal(Lang.F("{0} is the name of bookmark {1}: :bookmark! moves it here", "chapter3", 1),
            Bookmarks.AddWithMessage(notes, "chapter3", false, out failed));
        Assert.True(failed);
        Assert.Equal(Lang.F("Bookmark {0} moved here: {1}", 1, "chapter3"),
            Bookmarks.AddWithMessage(notes, "chapter3", true, out failed));
        Assert.False(failed);
        Bookmarks.Load();
        Assert.Single(Bookmarks.Places);
        Assert.Equal(In("notes.md"), Bookmarks.Places[0].Target.Target);
        // A place bookmarked already gets the name
        Assert.Equal(Lang.F("Bookmarked ({0}): {1}", 1, "notes"),
            Bookmarks.AddWithMessage(notes, "notes", false, out _));
        Assert.Single(Bookmarks.Places);
        // Only digits would be a number of :book
        Assert.Equal(Lang.T("A name of only digits is the number of a bookmark: add a letter"),
            Bookmarks.AddWithMessage(FileAt("x.md"), "2024", false, out failed));
        Assert.True(failed);
        // Renaming to the name of another bookmark is refused too
        Bookmarks.AddWithMessage(FileAt("y.md"), "other", false, out _);
        Assert.False(Bookmarks.Rename(1, "NOTES"));
        Assert.Equal(Lang.F("The name {0} is used by bookmark {1}", "notes", 1),
            Bookmarks.NameProblem("NOTES", Bookmarks.Places[1]));
        Assert.True(Bookmarks.Rename(1, "y2"));
        Assert.Equal(new[] { "notes", "y2" }, Bookmarks.NamesToComplete());
        Bookmarks.Save();
        // Completing names keeps the list as it is: what "u" puts back is still there
        Bookmarks.RemoveAt(1);
        Bookmarks.NamesToComplete();
        Assert.Equal(1, Bookmarks.UndoRemove());
        Assert.Equal(2, Bookmarks.Places.Count);
        // Without a name: by its number
        Bookmarks.AddWithMessage(FileAt("z.md"));
        Assert.Equal(":book 3", Bookmarks.CommandOf(Bookmarks.Places[2]));
    }

    [Fact]
    public void BookInTheEditor_AsksForTheJump_WithTheNumber()
    {
        var editor = new EditorEngine("text", 60, 10, false);
        Assert.Equal(EditorRequest.JumpToBookmark, editor.Type(":book 3<CR>"));
        Assert.Equal("3", editor.BookArgument);
        // Alone: the Bookmarks tab of the Jump menu
        Assert.Equal(EditorRequest.JumpToBookmark, editor.Type(":book<CR>"));
        Assert.Equal(string.Empty, editor.BookArgument);
        // ":bookmark" is another command, with a name or without
        Assert.Equal(EditorRequest.Bookmark, editor.Type(":bookmark<CR>"));
        Assert.Equal(string.Empty, editor.BookmarkName);
        Assert.Equal(EditorRequest.Bookmark, editor.Type(":bookmark Chapter3<CR>"));
        Assert.Equal("Chapter3", editor.BookmarkName);
        Assert.False(editor.BookmarkMovesName);
        Assert.Equal(EditorRequest.Bookmark, editor.Type(":bookmark! Chapter3<CR>"));
        Assert.True(editor.BookmarkMovesName);
        // ":book name" by its name
        Assert.Equal(EditorRequest.JumpToBookmark, editor.Type(":book chapter3<CR>"));
        Assert.Equal("chapter3", editor.BookArgument);
    }

    [Fact]
    public void TheJumpMenu_HasTabsWithTheListsOfTheFileBrowserAsSubTabs()
    {
        var tabs = JumpMenu.Tabs(StartMenu.OnlyMode.None);
        Assert.Equal(new[]
        {
            JumpMenu.Tab.Bookmarks, JumpMenu.Tab.Jumps, JumpMenu.Tab.History, JumpMenu.Tab.Favorites,
            JumpMenu.Tab.Hotlist, JumpMenu.Tab.Configuration, JumpMenu.Tab.DataFiles
        }, tabs);
        Assert.Equal(new[] { JumpMenu.Group.FileBrowser },
            new[] { JumpMenu.Tab.History, JumpMenu.Tab.Favorites, JumpMenu.Tab.Hotlist }.Select(JumpMenu.GroupOf).Distinct());
        // Tab: from a list of the file browser to the next tab (Marbles), Shift+Tab: to the first list of the tab
        // before (Recent); from the last tab round to the first
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.Configuration), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Favorites), false));
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.Jumps), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Hotlist), true));
        Assert.Equal(tabs.IndexOf(JumpMenu.Tab.History), JumpMenu.NextGroup(tabs, tabs.IndexOf(JumpMenu.Tab.Jumps), false));
        Assert.Equal(0, JumpMenu.NextGroup(tabs, tabs.Count - 1, false));
        // One mode only: the lists of the other mode are left out
        Assert.DoesNotContain(JumpMenu.Tab.History, JumpMenu.Tabs(StartMenu.OnlyMode.Notes));
        Assert.DoesNotContain(JumpMenu.Tab.DataFiles, JumpMenu.Tabs(StartMenu.OnlyMode.Editor));
    }
}
