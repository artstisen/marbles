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
    public void Places_AreAddedAtTheEnd_Once_AndThereIsALimit()
    {
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(FileAt("a.md")));
        Assert.Equal(Bookmarks.AddResult.Added, Bookmarks.Add(new JumpTarget(JumpKind.Page, "schemes")));
        Assert.Equal(Bookmarks.AddResult.AlreadyThere, Bookmarks.Add(FileAt("a.md")));
        Assert.Equal(2, Bookmarks.Items.Count);
        for (var i = Bookmarks.Items.Count; i < Bookmarks.MaxCount; i++) Bookmarks.Add(FileAt("f" + i + ".md"));
        Assert.Equal(Bookmarks.AddResult.Full, Bookmarks.Add(FileAt("one more.md")));
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
        Assert.Equal(2, Bookmarks.Move(0, int.MaxValue));
        Assert.Equal(new[] { "b", "c", "a" }, Bookmarks.Items.Select(entry => entry.Shown));
        Assert.Equal(0, Bookmarks.Move(2, 0));
        Bookmarks.Rename(1, "  Mine\there ");
        Assert.Equal("Mine here", Bookmarks.Items[1].Shown);
        // An empty name, or the name of the place, shows its own name again
        Bookmarks.Rename(1, string.Empty);
        Assert.Equal("b", Bookmarks.Items[1].Shown);
        var removed = Bookmarks.Items[1];
        Bookmarks.RemoveAt(1);
        Bookmarks.Insert(1, removed);
        Assert.Equal(new[] { "a", "b", "c" }, Bookmarks.Items.Select(entry => entry.Shown));
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
        Assert.Equal(Lang.F("Bookmarked: {0}", "a.md"), Bookmarks.AddWithMessage(FileAt("a.md")));
        Assert.Equal(Lang.F("Already bookmarked: {0}", "a.md"), Bookmarks.AddWithMessage(FileAt("a.md")));
        Assert.Contains("a.md", File.ReadAllText(Bookmarks.FilePath));
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
