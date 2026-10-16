namespace Marbles;

/// <summary>
/// The Jump menu: a box on top of the page with the tabs Bookmarks (places kept with B or ":bookmark"), Recent (the
/// jump history), File browser (with its lists History, Favorites and Hotlist as sub-tabs), Marbles (the pages of
/// Marbles) and Data files. Tab and Shift+Tab go from tab to tab, h and l through all the lists in a row (the
/// sub-tabs too); B bookmarks the selected place, and the Bookmarks tab has keys to arrange, rename and remove them. A place is picked with the
/// selection arrow (j/k, gg/G) and Enter; h/l go from tab to tab, "/" filters the list of the tab, I shows
/// information about the selected place and D takes a place out of the jump history. Only names are shown, not
/// paths. With one mode only (see StartMenu.Only) the tabs of the other mode are left out
/// </summary>
internal static class JumpMenu
{
    // The lists, in the order h and l go through them
    internal enum Tab
    {
        Bookmarks,
        Jumps, // Recent: the jump history
        History, // The file history of the file browser
        Favorites,
        Hotlist,
        Configuration, // The pages of Marbles
        DataFiles
    }

    // The tabs at the top: each has one list, or (the file browser) several as sub-tabs
    internal enum Group
    {
        Bookmarks,
        Recent,
        FileBrowser,
        Marbles,
        DataFiles
    }

    internal static Group GroupOf(Tab tab) => tab switch
    {
        Tab.Bookmarks => Group.Bookmarks,
        Tab.Jumps => Group.Recent,
        Tab.History or Tab.Favorites or Tab.Hotlist => Group.FileBrowser,
        Tab.Configuration => Group.Marbles,
        _ => Group.DataFiles
    };

    /// <summary>
    /// Tab and Shift+Tab: the first list of the next (or the previous) tab at the top, going round
    /// </summary>
    internal static int NextGroup(IReadOnlyList<Tab> tabs, int tab, bool back)
    {
        var groups = tabs.Select(GroupOf).Distinct().ToList();
        var next = groups[TabLayout.Next(groups.IndexOf(GroupOf(tabs[tab])), groups.Count, back)];
        return tabs.ToList().FindIndex(list => GroupOf(list) == next);
    }

    // In the language in use
    private static string GroupName(Group group) => group switch
    {
        Group.Bookmarks => Lang.T("Bookmarks"),
        Group.Recent => Lang.T("Recent"),
        Group.FileBrowser => DialogText.HelpSectionFiles,
        Group.Marbles => DialogText.ApplicationName,
        _ => Lang.T("Data files")
    };

    // The short name of a tab, used when the names do not all fit in a row (a long language)
    private static string ShortGroupName(Group group) => group switch
    {
        Group.FileBrowser => DialogText.CaptionsFiles,
        Group.DataFiles => Lang.T("Data"),
        _ => GroupName(group)
    };

    // The cells a row of tabs takes (as TabLayout.SubTabStarts places them)
    private static int TabRowWidth(IEnumerable<string> names) => names.Sum(name => TextMetrics.WidthOf(name) + 4);

    // The bookmarks: j k move the selected one while this is on (v)
    private static bool _moving;
    private static (int Index, Bookmarks.Entry Entry)? _removed; // For undo
    private static string _message = string.Empty;

    // A place in the list of a tab, and when it was jumped to or opened (DateTime.MinValue: not known). A file or
    // directory that is a favorite with a color of its own is shown in it
    private sealed record Row(JumpTarget Target, DateTime Time)
    {
        // The name shown, if it is not the name of the place (a directory of the hotlist with a name of its own)
        internal string? Label { get; init; }

        internal string Name => Label ?? Target.Name;

        internal ConsoleColor? Color { get; init; } = Target.Kind is JumpKind.File or JumpKind.Directory
            ? FileFavorites.ColorOf(Target.Target)
            : null;
    }

    // The list the menu was on when it was closed: it is shown again the next time it is opened (while Marbles
    // runs; it is not saved)
    private static Tab _lastTab = Tab.Bookmarks;

    /// <summary>
    /// Open the menu on a list the next time it is shown (":book" alone: the bookmarks)
    /// </summary>
    internal static void ShowNextOn(Tab tab)
    {
        _lastTab = tab;
    }

    // ":book 3" on a row of the Bookmarks tab: its number in the whole list (also when a filter shows some)
    private static string BookCommand(Row row) => ":book " + (BookmarkIndex(row) + 1); // English: a command

    private const int MarkWidth = 4; // "FILE", "DIR", "PAGE", "DATA"
    private const int TimeWidth = 10; // "09:41" today, else "2026-10-12"

    /// <summary>
    /// The lists that are shown: with one mode only (see StartMenu.Only) the lists of the other mode are left out
    /// </summary>
    internal static List<Tab> Tabs(StartMenu.OnlyMode only)
    {
        var tabs = new List<Tab> { Tab.Bookmarks, Tab.Jumps };
        if (only != StartMenu.OnlyMode.Notes)
        {
            tabs.Add(Tab.History);
            tabs.Add(Tab.Favorites);
            tabs.Add(Tab.Hotlist);
        }

        tabs.Add(Tab.Configuration);
        if (only != StartMenu.OnlyMode.Editor) tabs.Add(Tab.DataFiles);
        return tabs;
    }

    // The name of a sub-tab, in the language in use
    private static string TabName(Tab tab) => tab switch
    {
        Tab.History => Lang.T("History"),
        Tab.Favorites => Lang.T("Favorites"),
        Tab.Hotlist => DialogText.CaptionsHotlist,
        _ => GroupName(GroupOf(tab))
    };

    // The places a mode that is not used leads to are left out
    internal static bool Usable(JumpTarget target) => target.Kind switch
    {
        JumpKind.File or JumpKind.Directory => StartMenu.Only != StartMenu.OnlyMode.Notes,
        JumpKind.DataFile => StartMenu.Only != StartMenu.OnlyMode.Editor,
        _ => true
    };

    // The places of a tab
    private static List<Row> RowsOf(Tab tab)
    {
        switch (tab)
        {
            case Tab.Bookmarks:
                Bookmarks.Load();
                return Bookmarks.Items.Where(entry => Usable(entry.Target)).Select(entry =>
                    new Row(entry.Target, DateTime.MinValue) { Label = entry.Name.Length > 0 ? entry.Name : null })
                    .ToList();
            case Tab.Jumps:
                JumpHistory.Load();
                return JumpHistory.Items.Where(entry => Usable(entry.Target))
                    .Select(entry => new Row(entry.Target, entry.Jumped)).ToList();
            case Tab.History:
                FileHistory.Load();
                return FileHistory.Items.Select(entry => new Row(new JumpTarget(JumpKind.File, entry.Path), entry.Opened))
                    .ToList();
            case Tab.Favorites:
                FileFavorites.Load();
                return FileFavorites.Items.Where(favorite => !favorite.IsFolder)
                    .Select(favorite => new Row(new JumpTarget(
                        Directory.Exists(favorite.Path) ? JumpKind.Directory : JumpKind.File, favorite.Path),
                        DateTime.MinValue)).ToList();
            case Tab.Hotlist:
                DirectoryHotlist.Load();
                return DirectoryHotlist.Items.Select(entry =>
                    new Row(new JumpTarget(JumpKind.Directory, entry.Path), DateTime.MinValue)
                    {
                        Label = entry.Name.Length > 0 ? entry.Name : null
                    }).ToList();
            case Tab.Configuration:
                return JumpPages.Ids.Select(id => new Row(new JumpTarget(JumpKind.Page, id), DateTime.MinValue))
                    .ToList();
            default:
                return DataFiles().Select(path => new Row(new JumpTarget(JumpKind.DataFile, path), DateTime.MinValue))
                    .ToList();
        }
    }

    /// <summary>
    /// The data files in the data directory and in the directories in it, by their paths in the data directory
    /// ("work.mbls", "notes/work.mbls")
    /// </summary>
    internal static List<string> DataFiles()
    {
        var files = new List<string>();
        try
        {
            var root = FileIo.GetDataDirectoryName();
            var pattern = "*" + FileIo.GetFileNameExtension();
            files.AddRange(Directory.GetFiles(root, pattern).Select(Path.GetFileName).OfType<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            foreach (var directory in Directory.GetDirectories(root).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                var folder = Path.GetFileName(directory);
                files.AddRange(Directory.GetFiles(directory, pattern).Select(Path.GetFileName).OfType<string>()
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Select(name => folder + "/" + name));
            }
        }
        catch
        {
            // A data directory that cannot be read has no data files to show
        }

        return files;
    }

    /// <summary>
    /// A name shortened to a width with "..." in the middle, so its start and its end (the extension) are seen
    /// </summary>
    internal static string ShortName(string name, int width)
    {
        // Counted in terminal cells: a wide character takes two, and none is split. For the screen: control
        // characters are blank spaces
        name = TextMetrics.Printable(name);
        if (TextMetrics.WidthOf(name) <= width) return name;
        if (width <= 3) return TextMetrics.StartPart(name, Math.Max(0, width));
        var end = (width - 3) / 2;
        var start = width - 3 - end;
        return TextMetrics.StartPart(name, start) + "..." + TextMetrics.EndPart(name, end);
    }

    // The kind of a place, in short (English, as the kinds of text in the lists of entries)
    private static string Mark(JumpTarget target) => target.Kind switch
    {
        JumpKind.Directory => "DIR",
        JumpKind.Page => "PAGE",
        JumpKind.DataFile => "DATA",
        _ => "FILE"
    };

    // When a place was jumped to or opened: the time today, else the date
    private static string When(DateTime time) =>
        time == DateTime.MinValue ? string.Empty
        : time.Date == DateTime.Today ? time.ToString("HH:mm")
        : time.ToString("yyyy-MM-dd");

    /// <summary>
    /// Show the Jump menu until a place is picked (Enter) or it is closed (Esc)
    /// </summary>
    /// <returns>The place picked, or null</returns>
    internal static JumpTarget? Pick()
    {
        // In the color of text in frames and boxes
        using var frameText = ConsoleColors.FrameTextScope();
        var tabs = Tabs(StartMenu.Only);
        // The favorites, for the colors of the names
        FileFavorites.Load();
        var filters = new Dictionary<Tab, string>();
        var tab = Math.Max(0, tabs.IndexOf(_lastTab));
        var selected = 0;
        var scroll = 0;
        _moving = false;
        _message = string.Empty;
        while (true)
        {
            var current = tabs[tab];
            _lastTab = current;
            var filter = filters.GetValueOrDefault(current, string.Empty);
            var rows = RowsOf(current).Where(row => filter.Length == 0 ||
                row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            selected = Math.Clamp(selected, 0, Math.Max(0, rows.Count - 1));
            var visible = Draw(tabs, tab, filter, rows, selected, ref scroll);
            _message = _moving ? DialogText.HotlistMoving : string.Empty;
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (_moving)
            {
                // Moving the selected bookmark: j k move it, and Esc, Enter or v end it
                if (navigation is ConsoleKey.DownArrow or ConsoleKey.UpArrow && rows.Count > 0)
                {
                    // Past the next row shown: the rows are not all the bookmarks (filtered, or some cannot be used
                    // here), so it goes to the place of that bookmark in the whole list
                    var next = selected + (navigation == ConsoleKey.DownArrow ? 1 : -1);
                    if (next >= 0 && next < rows.Count)
                    {
                        MoveBookmark(rows[selected], BookmarkIndex(rows[next]));
                        selected = next;
                    }
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.V)
                {
                    _moving = false;
                    _message = string.Empty;
                }

                continue;
            }

            if (Jump.IsKey(key)) return null;
            if (key.Key == ConsoleKey.Tab)
            {
                // The next (Shift: the previous) tab at the top
                tab = NextGroup(tabs, tab, (key.Modifiers & ConsoleModifiers.Shift) != 0);
                selected = 0;
                scroll = 0;
                continue;
            }

            if (key.Key is ConsoleKey.Escape || key.KeyChar is 'q' or 'Q')
            {
                // Esc takes the filter away before it closes the menu
                if (filter.Length > 0 && key.Key == ConsoleKey.Escape)
                {
                    filters[current] = string.Empty;
                    continue;
                }

                return null;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                if (rows.Count == 0) continue;
                var target = rows[selected].Target;
                if (target.Exists) return target;
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.CaptionsJump, DialogText.FileNotFound, target.FullPath);
                continue;
            }

            switch (navigation)
            {
                case ConsoleKey.DownArrow:
                    selected = Math.Min(selected + 1, Math.Max(0, rows.Count - 1));
                    continue;
                case ConsoleKey.UpArrow:
                    selected = Math.Max(selected - 1, 0);
                    continue;
                case ConsoleKey.RightArrow:
                    tab = TabLayout.Next(tab, tabs.Count, false);
                    selected = 0;
                    scroll = 0;
                    continue;
                case ConsoleKey.LeftArrow:
                    tab = TabLayout.Next(tab, tabs.Count, true);
                    selected = 0;
                    scroll = 0;
                    continue;
            }

            switch (key.KeyChar)
            {
                case 'G':
                    selected = Math.Max(0, rows.Count - 1);
                    break;
                case 'g':
                    if (Console.ReadKey(true).KeyChar == 'g') selected = 0;
                    break;
                case '/':
                    // Only show the places with a text in their names
                    filters[current] = CommandLine.Read('/', false) ?? string.Empty;
                    selected = 0;
                    scroll = 0;
                    break;
                case 'i':
                case 'I':
                    if (rows.Count > 0) ShowInfo(current, rows[selected]);
                    break;
                case 'b':
                case 'B':
                    // Keep the selected place as a bookmark
                    if (rows.Count == 0) break;
                    _message = Bookmarks.AddWithMessage(rows[selected].Target);
                    break;
                case 'v':
                case 'V':
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    if (filter.Length > 0)
                    {
                        _message = DialogText.HotlistMoveFiltered;
                        break;
                    }

                    _moving = true;
                    _message = DialogText.HotlistMoving;
                    break;
                case 't':
                case 'T':
                case 'e':
                case 'E':
                    // The selected bookmark first or last
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    // First or last of all the bookmarks: also of the rows shown
                    MoveBookmark(rows[selected], key.KeyChar is 't' or 'T' ? 0 : int.MaxValue);
                    selected = key.KeyChar is 't' or 'T' ? 0 : rows.Count - 1;
                    break;
                case 'x':
                case 'X':
                    if (current == Tab.Bookmarks && rows.Count > 0) RenameBookmark(rows[selected]);
                    break;
                case 'u':
                case 'U':
                    if (current == Tab.Bookmarks) UndoBookmarkRemove();
                    break;
                case 'c':
                case 'C':
                    if (current == Tab.Bookmarks) ClearBookmarks();
                    break;
                case 'd':
                case 'D':
                    if (current == Tab.Bookmarks && rows.Count > 0)
                    {
                        RemoveBookmark(rows[selected]);
                        break;
                    }

                    // A place is taken out of the jump history
                    if (current != Tab.Jumps || rows.Count == 0) break;
                    JumpHistory.Load();
                    var index = JumpHistory.Items.ToList().FindIndex(entry => entry.Target.SameAs(rows[selected].Target));
                    JumpHistory.RemoveAt(index);
                    var status = JumpHistory.Save();
                    if (status != DialogText.Success)
                    {
                        ChoiceBox.Forget();
                        ChoiceBox.Message(DialogText.CaptionsJump, status, string.Empty);
                    }

                    break;
                case '?':
                    Tools.WriteEditorHelp(false, Tools.HelpTopic.Jump);
                    Tools.ClearScreen();
                    break;
            }

            if (key.Key == ConsoleKey.PageDown) selected = Math.Min(selected + visible, Math.Max(0, rows.Count - 1));
            if (key.Key == ConsoleKey.PageUp) selected = Math.Max(selected - visible, 0);
        }
    }

    // Draw the menu: the box, its tabs, the filter and the places of the tab. Returns how many places are seen
    private static int Draw(List<Tab> tabs, int tab, string filter, List<Row> rows, int selected, ref int scroll)
    {
        ChoiceBox.Forget();
        var inner = Math.Max(30, Init.LineWidth - 6);
        // Down to the row above the bottom line, so it covers a box shown before it
        var height = Math.Max(8, Init.LineCount - 5);
        var (top, left, width, _) = ChoiceBox.DrawFrame(DialogText.CaptionsJump, inner, height);

        // The tabs at the top, and below them the lists of the tab shown (its sub-tabs), each row with the keys that
        // change it at the end
        var group = GroupOf(tabs[tab]);
        var groups = tabs.Select(GroupOf).Distinct().ToList();
        // The names of the tabs, or their short names if they do not fit next to the keys
        var groupNames = groups.Select(GroupName).ToList();
        if (TabRowWidth(groupNames) > width - 7) groupNames = groups.Select(ShortGroupName).ToList();
        DrawTabRow(top + 1, left, width, groupNames, groups.IndexOf(group), "Tab"); // English
        var lists = tabs.Where(list => GroupOf(list) == group).ToList();
        DrawTabRow(top + 2, left, width, lists.Count > 1 ? lists.Select(TabName).ToList() : new List<string>(),
            lists.IndexOf(tabs[tab]), "h/l"); // English: the keys
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.TabFrame);
        Tools.SetCursorEx(top + 3, left + 2);
        Console.Write(new string('-', Math.Max(0, width - 2)));

        // The filter
        Tools.SetCursorEx(top + 4, left + 3);
        if (filter.Length == 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Lang.T("/ Filter"));
        }
        else
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(Tools.TruncateString(Lang.F("Filter: {0}", filter), width - 4, true));
        }

        // A message (a bookmark added ...), or what the list is
        var room = left + width - 1 - Console.CursorLeft - 2;
        var note = _message.Length > 0 ? _message
            : tabs[tab] == Tab.Jumps ? Lang.T("Only jumps made in this menu, the latest first")
            : tabs[tab] == Tab.Bookmarks && rows.Count == 0 && filter.Length == 0 ? DialogText.BookmarksEmpty
            : string.Empty;
        if (note.Length > 0 && room > 10)
        {
            if (_message.Length > 0)
            {
                // A message: highlighted, with a blank space on each side in the highlight
                Console.Write(" ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write(" " + Tools.TruncateString(note, room - 2, true) + " ");
            }
            else
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write("  " + Tools.TruncateString(note, room, true));
            }
        }

        // The places
        var first = top + 6;
        var visible = Math.Max(1, top + height - first + 1);
        scroll = Math.Clamp(scroll, Math.Max(0, selected - visible + 1), selected);
        var nameWidth = Math.Max(10, width - 4 - MarkWidth - TimeWidth - 4);
        if (rows.Count == 0)
        {
            Tools.SetCursorEx(first, left + 4);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Tools.TruncateString(Lang.T("Nothing to show"), width - 5, true));
        }

        for (var line = 0; line < visible && scroll + line < rows.Count; line++)
        {
            var row = rows[scroll + line];
            var index = scroll + line;
            Tools.SetCursorEx(first + line, left + 1);
            if (index == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(index == selected ? (_moving ? "<>" : "->") : "  ");

            // The name, in the color of a favorite that has one (a place that is not there any more is dimmed and
            // marked)
            var exists = row.Target.Exists;
            var name = exists ? row.Name : row.Name + " " + DialogText.FavoriteMissing;
            name = ShortName(name, nameWidth);
            Tools.SetCursorEx(first + line, left + 4);
            if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else if (row.Color is { } color) ConsoleColors.SetTextColor(color);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(name);

            // The kind, and when (a bookmark: the command that jumps to it)
            Tools.SetCursorEx(first + line, left + width - MarkWidth - TimeWidth - 2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var last = tabs[tab] == Tab.Bookmarks ? BookCommand(row) : When(row.Time);
            Console.Write(Mark(row.Target).PadRight(MarkWidth) + "  " + last.PadLeft(TimeWidth));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        ChoiceBox.WriteHints(tabs[tab] == Tab.Bookmarks ? DialogText.JumpBookmarkKeys : DialogText.JumpKeys);
        return visible;
    }

    // A row of tabs (the names, the one shown highlighted) with the keys that change them at its end
    private static void DrawTabRow(int row, int left, int width, List<string> names, int shown, string keys)
    {
        var starts = TabLayout.SubTabStarts(names);
        for (var i = 0; i < names.Count; i++)
        {
            Tools.SetCursorEx(row, left + 2 + starts[i] - 1);
            if (i == shown) ConsoleColors.SetTabColors();
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.OtherTabs);
            Console.Write(" " + names[i] + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.TabFrame);
        Tools.SetCursorEx(row, left + width - keys.Length - 1);
        Console.Write(keys);
    }

    // The number of a bookmark in the whole list (a row can be in a filtered list)
    private static int BookmarkIndex(Row row) =>
        Bookmarks.Items.ToList().FindIndex(entry => entry.Target.SameAs(row.Target));

    // Move a bookmark to a place in the list, and save it. Returns where it is now
    private static int MoveBookmark(Row row, int to)
    {
        var index = BookmarkIndex(row);
        if (index < 0) return 0;
        var place = Bookmarks.Move(index, to);
        SaveBookmarks();
        return place;
    }

    // X: the name the bookmark is shown with (empty: the name of the place)
    private static void RenameBookmark(Row row)
    {
        var index = BookmarkIndex(row);
        if (index < 0) return;
        ChoiceBox.Forget();
        var name = ChoiceBox.AskText(DialogText.BookmarkRename, DialogText.BookmarkRenameNote, DialogText.NameColon,
            Bookmarks.Items[index].Shown);
        if (name == null) return;
        Bookmarks.Rename(index, name);
        SaveBookmarks();
    }

    // D: take the bookmark out of the list (u puts it back)
    private static void RemoveBookmark(Row row)
    {
        var index = BookmarkIndex(row);
        if (index < 0) return;
        var entry = Bookmarks.Items[index];
        Bookmarks.RemoveAt(index);
        _removed = (index, entry);
        SaveBookmarks();
        _message = Lang.F("Removed from the bookmarks: {0} (u puts it back)", entry.Shown);
    }

    private static void UndoBookmarkRemove()
    {
        if (_removed is not { } removed) return;
        _removed = null;
        Bookmarks.Load();
        Bookmarks.Insert(removed.Index, removed.Entry);
        SaveBookmarks();
        _message = Lang.F("Back in the bookmarks: {0}", removed.Entry.Shown);
    }

    // C: empty the list, after asking
    private static void ClearBookmarks()
    {
        Bookmarks.Load();
        if (Bookmarks.Items.Count == 0) return;
        ChoiceBox.Forget();
        if (!ChoiceBox.Confirm(Lang.T("Bookmarks"),
                Lang.F("Remove all {0} bookmarks?", Bookmarks.Items.Count), DialogText.BookmarksClearNote, true))
        {
            return;
        }

        Bookmarks.Clear();
        _removed = null;
        SaveBookmarks();
        _message = DialogText.BookmarksCleared;
    }

    private static void SaveBookmarks()
    {
        var status = Bookmarks.Save();
        if (status != DialogText.Success) _message = status;
    }

    // Information about a place: its name, kind and path, whether it is there, and when it was jumped to or opened
    private static void ShowInfo(Tab tab, Row row)
    {
        var target = row.Target;
        var rows = new List<(string Label, string Value)>
        {
            (DialogText.InfoName, target.Name),
            (DialogText.InfoType, target.Kind switch
            {
                JumpKind.Directory => DialogText.InfoTypeDirectory,
                JumpKind.Page => Lang.T("Page of Marbles"),
                JumpKind.DataFile => DialogText.InfoTypeDataFile,
                _ => Lang.T("File")
            })
        };
        if (target.Kind != JumpKind.Page)
        {
            rows.Add((DialogText.InfoPath, target.FullPath));
            rows.Add((Lang.T("Found:"), target.Exists ? DialogText.ChoiceYes : DialogText.ChoiceNo));
        }

        if (row.Time != DateTime.MinValue)
        {
            rows.Add((tab == Tab.Jumps ? Lang.T("Jumped to:") : Lang.T("Opened:"),
                row.Time.ToString("yyyy-MM-dd HH:mm")));
        }

        ChoiceBox.Forget();
        ChoiceBox.ShowRows(DialogText.CaptionsJumpInfo, rows);
    }
}
