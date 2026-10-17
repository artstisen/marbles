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

    // The bookmarks: j k move the selected one while this is on (V)
    private static bool _moving;
    private static string _message = string.Empty;

    // The bookmarks marked with Space (several are moved to a folder, to the top or the end, or removed at once)
    private static readonly HashSet<Bookmarks.Entry> Marks = new();

    // The bookmark (or folder) to select when the rows are made again (after it was moved, added or put back)
    private static Bookmarks.Entry? _select;

    // The color of a file or directory that is a favorite with a color of its own
    private static ConsoleColor? FavoriteColor(JumpTarget target) => target.Kind is JumpKind.File or JumpKind.Directory
        ? FileFavorites.ColorOf(target.Target)
        : null;

    // A place in the list of a tab, and when it was jumped to or opened (DateTime.MinValue: not known). A file or
    // directory that is a favorite with a color of its own is shown in it. On the Bookmarks tab a row is a bookmark
    // or a folder, and the color of a bookmark is used before that of a favorite
    private sealed record Row(JumpTarget Target, DateTime Time)
    {
        // The name shown, if it is not the name of the place (a directory of the hotlist with a name of its own)
        internal string? Label { get; init; }

        internal string Name => Label ?? Target.Name;

        internal ConsoleColor? Color { get; init; } = FavoriteColor(Target);

        // The bookmark or folder of a row of the Bookmarks tab
        internal Bookmarks.Entry? Bookmark { get; init; }

        internal bool IsFolder => Bookmark?.IsFolder == true;
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

    // ":book 3" on a row of the Bookmarks tab: its number among all the bookmarks (also when a filter shows some, and
    // the bookmarks in closed folders count); nothing for a folder
    private static string BookCommand(Row row) =>
        row.Bookmark is { IsFolder: false } entry ? ":book " + Bookmarks.NumberOf(entry) : string.Empty; // English

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
                return BookmarkRows(string.Empty);
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

    // The rows of the Bookmarks tab (as the list is now; it is read when the menu opens). Without a filter: the folders,
    // and the bookmarks in the open ones two spaces in. With a filter: the bookmarks with the text in their names,
    // without the folders. The bookmarks of a mode that is not used are left out
    private static List<Row> BookmarkRows(string filter)
    {
        var rows = new List<Row>();
        var closed = false; // In a closed folder
        foreach (var entry in Bookmarks.Items)
        {
            if (entry.IsFolder)
            {
                closed = entry.Collapsed;
                if (filter.Length > 0) continue;
                var start = Bookmarks.Items.ToList().IndexOf(entry);
                var count = Bookmarks.Items.Skip(start + 1).Take(Bookmarks.FolderCount(start))
                    .Count(item => Usable(item.Target));
                rows.Add(new Row(entry.Target, DateTime.MinValue)
                {
                    Bookmark = entry,
                    Label = (entry.Collapsed ? "[+] " : "[-] ") + entry.Name + " (" + count + ")",
                    Color = entry.Color
                });
                continue;
            }

            if (!entry.InFolder) closed = false;
            if (!Usable(entry.Target)) continue;
            if (filter.Length > 0 && !entry.Shown.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            if (filter.Length == 0 && entry.InFolder && closed) continue;
            rows.Add(new Row(entry.Target, DateTime.MinValue)
            {
                Bookmark = entry,
                Label = (filter.Length == 0 && entry.InFolder ? "  " : string.Empty) + entry.Shown,
                Color = entry.Color ?? FavoriteColor(entry.Target)
            });
        }

        return rows;
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
        _select = null;
        Marks.Clear();
        // The bookmarks are read once: the rows refer to them (marks, moving)
        Bookmarks.Load();
        while (true)
        {
            var current = tabs[tab];
            _lastTab = current;
            var filter = filters.GetValueOrDefault(current, string.Empty);
            var rows = current == Tab.Bookmarks
                ? BookmarkRows(filter)
                : RowsOf(current).Where(row => filter.Length == 0 ||
                    row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            if (_select != null)
            {
                // The bookmark or folder that was moved, added or put back
                var found = rows.FindIndex(row => ReferenceEquals(row.Bookmark, _select));
                if (found >= 0) selected = found;
                _select = null;
            }

            selected = Math.Clamp(selected, 0, Math.Max(0, rows.Count - 1));
            var visible = Draw(tabs, tab, filter, rows, selected, ref scroll);
            _message = _moving ? DialogText.Moving : string.Empty;
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (_moving)
            {
                // Moving the selected bookmark or folder: j k move it (into and out of folders), and Esc, Enter or V
                // end it
                if (navigation is ConsoleKey.DownArrow or ConsoleKey.UpArrow && rows.Count > 0 &&
                    rows[selected].Bookmark is { } moved)
                {
                    MoveBookmark(moved, navigation == ConsoleKey.UpArrow);
                    _select = moved;
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
                Marks.Clear();
                selected = 0;
                scroll = 0;
                continue;
            }

            if (key.Key is ConsoleKey.Escape || key.KeyChar is 'q' or 'Q')
            {
                // Esc takes the marks away, then the filter, before it closes the menu
                if (Marks.Count > 0 && key.Key == ConsoleKey.Escape && current == Tab.Bookmarks)
                {
                    Marks.Clear();
                    continue;
                }

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
                if (rows[selected].Bookmark is { IsFolder: true } folder)
                {
                    // A folder is opened or closed
                    folder.Collapsed = !folder.Collapsed;
                    SaveBookmarks();
                    continue;
                }

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
                    Marks.Clear();
                    selected = 0;
                    scroll = 0;
                    continue;
                case ConsoleKey.LeftArrow:
                    tab = TabLayout.Next(tab, tabs.Count, true);
                    Marks.Clear();
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
                    if (rows.Count > 0 && !rows[selected].IsFolder) ShowInfo(current, rows[selected]);
                    break;
                case 'b':
                case 'B':
                    // Keep the selected place as a bookmark (the list is read again: the marks are left)
                    if (rows.Count == 0 || rows[selected].IsFolder) break;
                    _message = Bookmarks.AddWithMessage(rows[selected].Target);
                    Marks.Clear();
                    break;
                case ':':
                    if (RunBookmarkCommand(current, rows, selected, filter)) return null;
                    break;
                case 'v':
                case 'V':
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    if (filter.Length > 0)
                    {
                        _message = DialogText.BookmarkMoveFiltered;
                        break;
                    }

                    _moving = true;
                    _message = DialogText.Moving;
                    break;
                case ' ':
                    // Mark (or unmark) the bookmark and go to the next
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    if (rows[selected].Bookmark is not { IsFolder: false } marked)
                    {
                        _message = DialogText.BookmarkMarkFolder;
                        break;
                    }

                    if (!Marks.Remove(marked)) Marks.Add(marked);
                    selected = Math.Min(selected + 1, rows.Count - 1);
                    break;
                case 'm':
                case 'M':
                    if (current == Tab.Bookmarks) MoveBookmarksToFolder(rows, selected);
                    break;
                case 't':
                case 'T':
                case 'e':
                case 'E':
                    // The marked bookmarks (or the selected bookmark or folder) first or last (in a folder: of it)
                    if (current != Tab.Bookmarks || rows.Count == 0) break;
                    MoveBookmarksToEnd(rows, selected, key.KeyChar is 't' or 'T');
                    break;
                case 'a':
                case 'A':
                    if (current == Tab.Bookmarks) NewBookmarkFolder(rows, selected, filter, string.Empty);
                    break;
                case 'x':
                case 'X':
                    if (current == Tab.Bookmarks && rows.Count > 0) RenameBookmark(rows[selected], string.Empty);
                    break;
                case 'c':
                case 'C':
                    if (current == Tab.Bookmarks && rows.Count > 0) PickBookmarkColor(rows[selected]);
                    break;
                case 'u':
                case 'U':
                    if (current == Tab.Bookmarks) UndoBookmarkRemove();
                    break;
                case 'd':
                case 'D':
                    if (current == Tab.Bookmarks && rows.Count > 0)
                    {
                        RemoveBookmarks(rows, selected);
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

        // The names of the columns, between divider lines: the name, the kind, and the command that jumps to a
        // bookmark (Bookmarks) or when it was jumped to or opened (Recent, History)
        var typeLabel = Lang.T("Type");
        var markWidth = Math.Max(MarkWidth, TextMetrics.WidthOf(typeLabel));
        var lastLabel = tabs[tab] == Tab.Bookmarks ? Lang.T("Command")
            : tabs[tab] is Tab.Jumps or Tab.History ? Lang.T("When")
            : string.Empty;
        var typeColumn = left + width - markWidth - TimeWidth - 2;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(top + 5, left + 4);
        Console.Write(Tools.TruncateString(Lang.T("Name"), Math.Max(1, typeColumn - left - 6), true));
        Tools.SetCursorEx(top + 5, typeColumn);
        Console.Write(typeLabel.PadRight(markWidth) + "  " +
                      Tools.TruncateString(lastLabel, TimeWidth, false).PadLeft(TimeWidth));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.TabFrame);
        Tools.SetCursorEx(top + 6, left + 2);
        Console.Write(new string('-', Math.Max(0, width - 2)));

        // The places
        var first = top + 7;
        var visible = Math.Max(1, top + height - first + 1);
        scroll = Math.Clamp(scroll, Math.Max(0, selected - visible + 1), selected);
        var nameWidth = Math.Max(10, width - 4 - markWidth - TimeWidth - 4);
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

            // A marked bookmark has a "+" in front
            if (row.Bookmark != null && Marks.Contains(row.Bookmark))
            {
                Tools.SetCursorEx(first + line, left + 3);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write("+");
            }

            // The name, in the color of the bookmark or of a favorite (a place that is not there any more is dimmed
            // and marked). A folder is shown with its name and the number of bookmarks in it
            var exists = row.IsFolder || row.Target.Exists;
            var name = exists ? row.Name : row.Name + " " + DialogText.FavoriteMissing;
            name = ShortName(name, nameWidth);
            Tools.SetCursorEx(first + line, left + 4);
            // The bookmark that is moved is highlighted, from the blank space before it (as in the other lists)
            if (_moving && index == selected)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Tools.SetCursorEx(first + line, left + 3);
                Console.Write(" ");
            }
            else if (row.IsFolder && row.Color == null) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
            else if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else if (row.Color is { } color) ConsoleColors.SetTextColor(color);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(name);

            // The kind, and when (a bookmark: the command that jumps to it; a folder: nothing)
            Tools.SetCursorEx(first + line, typeColumn);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var last = tabs[tab] == Tab.Bookmarks ? BookCommand(row) : When(row.Time);
            Console.Write((row.IsFolder ? string.Empty : Mark(row.Target)).PadRight(markWidth) + "  " +
                          last.PadLeft(TimeWidth));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // With bookmarks marked: what can be done with them
        ChoiceBox.WriteHints(tabs[tab] != Tab.Bookmarks ? DialogText.JumpKeys
            : Marks.Count > 0 ? DialogText.FavoritesMarkedKeys
            : DialogText.JumpBookmarkKeys);
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

    // The place of a bookmark or folder in the whole list
    private static int IndexOf(Bookmarks.Entry entry) => Bookmarks.Items.ToList().IndexOf(entry);

    // The bookmarks (and folders) shown, in their order, and if they are in a folder: a move is seen when one of them
    // changes place among them or goes into or out of a folder
    private static List<(Bookmarks.Entry Entry, bool InFolder)> ShownOrder() => BookmarkRows(string.Empty)
        .Select(row => (row.Bookmark!, row.Bookmark!.InFolder)).ToList();

    // j k while moving: one place up or down among the rows shown, into and out of folders. Bookmarks that are not
    // shown (of a mode that is not used, or in a closed folder) are passed over
    private static void MoveBookmark(Bookmarks.Entry entry, bool up)
    {
        // A folder moves closed (Move closes it): closed before the rows are compared, so its first step is seen
        if (entry.IsFolder) entry.Collapsed = true;
        var before = ShownOrder();
        for (var step = 0; step < Bookmarks.Items.Count + 2; step++)
        {
            if (!Bookmarks.Move(IndexOf(entry), up) || !ShownOrder().SequenceEqual(before)) break;
        }

        SaveBookmarks();
    }

    // The marked bookmarks, or the selected one (not a folder)
    private static List<Bookmarks.Entry> MarkedOrSelected(List<Row> rows, int selected)
    {
        if (Marks.Count > 0) return Bookmarks.Items.Where(Marks.Contains).ToList();
        return rows.Count > 0 && rows[selected].Bookmark is { IsFolder: false } entry
            ? new List<Bookmarks.Entry> { entry }
            : new List<Bookmarks.Entry>();
    }

    // M or ":move": the marked bookmarks (or the selected one) to a folder picked from a list: one of the folders,
    // a new folder, or out of the folders
    private static void MoveBookmarksToFolder(List<Row> rows, int selected)
    {
        var items = MarkedOrSelected(rows, selected);
        if (items.Count == 0)
        {
            _message = DialogText.BookmarkMarkFolder;
            return;
        }

        var folders = Bookmarks.Items.Where(item => item.IsFolder).ToList();
        var choices = new List<string> { DialogText.FavoriteNewFolder + "...", DialogText.FavoriteNoFolder };
        choices.AddRange(folders.Select(folder => "[" + folder.Name + "]"));
        var caption = items.Count == 1 ? DialogText.FavoriteMoveOne : Lang.F("Move {0} bookmarks to folder", items.Count);
        ChoiceBox.Forget();
        var choice = FileExplorer.PickFromList(caption, choices, folders.Count > 0 ? 2 : 0);
        ChoiceBox.Forget();
        if (choice < 0) return;
        Bookmarks.Entry? folder = null;
        if (choice == 0)
        {
            // A new folder, where the first of them is
            ChoiceBox.Forget();
            var name = ChoiceBox.AskText(DialogText.FavoriteNewFolder, DialogText.TypeNameAndPressEnter,
                DialogText.NameColon, string.Empty);
            if (name == null) return;
            var index = Bookmarks.AddFolder(name, IndexOf(items[0]));
            if (index < 0)
            {
                _message = DialogText.FavoriteFolderName;
                return;
            }

            folder = Bookmarks.Items[index];
        }
        else if (choice >= 2)
        {
            folder = folders[choice - 2];
        }

        if (Bookmarks.MoveToFolder(items, folder) < 0) return;
        Marks.Clear();
        _select = folder ?? items[0];
        var where = folder == null ? DialogText.FavoriteNoFolder : folder.Name;
        _message = items.Count == 1 ? DialogText.FavoriteMovedOne + where
            : Lang.F("{0} bookmarks moved to: {1}", items.Count, where);
        SaveBookmarks();
    }

    // T E, ":top" ":bottom": the marked bookmarks (or the selected bookmark or folder) to the top or the end (a
    // bookmark in a folder: of its folder)
    private static void MoveBookmarksToEnd(List<Row> rows, int selected, bool top)
    {
        var items = MarkedOrSelected(rows, selected);
        if (items.Count == 0 && rows.Count > 0 && rows[selected].Bookmark is { } entry) items.Add(entry);
        if (items.Count == 0) return;
        Bookmarks.MoveToEnd(items, top);
        Marks.Clear();
        _select = items[0];
        _message = top ? Lang.T("Moved to the top") : Lang.T("Moved to the end");
        SaveBookmarks();
    }

    // A or ":folder name": a new folder in front of the selected bookmark (or its folder)
    private static void NewBookmarkFolder(List<Row> rows, int selected, string filter, string name)
    {
        if (filter.Length > 0)
        {
            _message = DialogText.BookmarkMoveFiltered;
            return;
        }

        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            var asked = ChoiceBox.AskText(DialogText.FavoriteNewFolder, DialogText.TypeNameAndPressEnter,
                DialogText.NameColon, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var before = rows.Count > 0 && rows[selected].Bookmark is { } entry ? IndexOf(entry) : -1;
        var index = Bookmarks.AddFolder(name, before);
        if (index < 0)
        {
            _message = DialogText.FavoriteFolderName;
            return;
        }

        _select = Bookmarks.Items[index];
        SaveBookmarks();
    }

    // X or ":rename name": the name the bookmark is shown with (empty: the name of the place), or the name of a
    // folder
    private static void RenameBookmark(Row row, string name)
    {
        if (row.Bookmark is not { } entry) return;
        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            var asked = entry.IsFolder
                ? ChoiceBox.AskText(DialogText.FavoriteRenameFolder, DialogText.TypeNameAndPressEnter,
                    DialogText.NameColon, entry.Name)
                : ChoiceBox.AskText(DialogText.BookmarkRename, DialogText.BookmarkRenameNote, DialogText.NameColon,
                    entry.Shown);
            if (asked == null) return;
            name = asked;
        }

        if (!Bookmarks.Rename(IndexOf(entry), name))
        {
            _message = DialogText.FavoriteFolderName;
            return;
        }

        SaveBookmarks();
    }

    // C or ":color": the text color of the bookmark or folder (it is used before the color of a favorite)
    private static void PickBookmarkColor(Row row)
    {
        if (row.Bookmark is not { } entry) return;
        ChoiceBox.Forget();
        if (!ColorPicker.Pick(DialogText.ColorCaption, (Init.LineWidth - 26) / 2, true, entry.Color, null, null,
                out var color))
        {
            return;
        }

        Bookmarks.SetColor(IndexOf(entry), color);
        SaveBookmarks();
    }

    // D or ":remove": the marked bookmarks, or the selected bookmark, or a folder with its bookmarks (u puts them back)
    private static void RemoveBookmarks(List<Row> rows, int selected)
    {
        if (Marks.Count > 0)
        {
            var marked = Bookmarks.Items.Where(Marks.Contains).ToList();
            ChoiceBox.Forget();
            if (marked.Count > 1 && !ChoiceBox.Confirm(Lang.T("Remove bookmarks"),
                    Lang.F("Remove the {0} marked bookmarks from the list?", marked.Count),
                    DialogText.BookmarksClearNote, true))
            {
                return;
            }

            var count = Bookmarks.RemoveMany(marked);
            Marks.Clear();
            _message = Lang.F("{0} removed from the bookmarks (u: Undo)", count);
            SaveBookmarks();
            return;
        }

        if (rows.Count == 0 || rows[selected].Bookmark is not { } entry) return;
        var index = IndexOf(entry);
        if (entry.IsFolder)
        {
            var count = Bookmarks.FolderCount(index);
            ChoiceBox.Forget();
            if (count > 0 && !ChoiceBox.Confirm(DialogText.FavoriteDeleteFolder,
                    DialogText.DeleteNamed.Replace("{0}", Tools.TruncateString(entry.Name, 40, true)),
                    count == 1 ? Lang.T("The bookmark in it is removed from the list too (the place stays)")
                        : Lang.F("The {0} bookmarks in it are removed from the list too (the places stay)", count),
                    true))
            {
                return;
            }

            Bookmarks.RemoveAt(index);
            _message = Lang.F("Removed the folder: {0}", entry.Name);
            SaveBookmarks();
            return;
        }

        Bookmarks.RemoveAt(index);
        SaveBookmarks();
        _message = Lang.F("Removed from the bookmarks: {0} (u puts it back)", entry.Shown);
    }

    // u or ":undo": put back what was removed last
    private static void UndoBookmarkRemove()
    {
        var index = Bookmarks.UndoRemove();
        if (index < 0)
        {
            _message = DialogText.FavoriteNothingToUndo;
            return;
        }

        var entry = Bookmarks.Items[index];
        _select = entry;
        SaveBookmarks();
        _message = Lang.F("Back in the bookmarks: {0}", entry.Shown);
    }

    // ":clearbook": empty the list, after asking
    private static void ClearBookmarks()
    {
        if (Bookmarks.Items.Count == 0) return;
        ChoiceBox.Forget();
        if (!ChoiceBox.Confirm(Lang.T("Bookmarks"),
                Lang.F("Remove all {0} bookmarks?", Bookmarks.Places.Count), DialogText.BookmarksClearNote, true))
        {
            return;
        }

        Bookmarks.Clear();
        Marks.Clear();
        SaveBookmarks();
        _message = DialogText.BookmarksCleared;
    }

    // ":expand" ":collapse": open or close all the folders
    private static void SetBookmarkFolders(bool collapsed)
    {
        if (Bookmarks.SetAllCollapsed(collapsed) == 0)
        {
            _message = DialogText.FavoritesNoFolders;
            return;
        }

        _message = collapsed ? DialogText.FavoritesCollapsed : DialogText.FavoritesExpanded;
        SaveBookmarks();
    }

    // ":" in the Jump menu: a command. The commands on the bookmarks are run on the Bookmarks tab. Returns true if
    // the menu is closed
    private static bool RunBookmarkCommand(Tab current, List<Row> rows, int selected, string filter)
    {
        var command = CommandLine.Read(CommandContext.Jump);
        if (command == null) return false;
        // A name can follow the command
        if (CommandLine.Name(command) is "folder" or "rename") command = CommandLine.Name(command);
        if (command is "q" or "quit" or "close") return true;
        if (command is "h" or "help")
        {
            Tools.WriteEditorHelp(false, Tools.HelpTopic.Jump);
            Tools.ClearScreen();
            return false;
        }

        if (command is "info")
        {
            if (rows.Count > 0 && !rows[selected].IsFolder) ShowInfo(current, rows[selected]);
            return false;
        }

        if (!Commands.IsKnown(CommandContext.Jump, command))
        {
            CommandLine.ShowUnknown(command);
            return false;
        }

        if (current != Tab.Bookmarks)
        {
            _message = Lang.T("This command is used on the Bookmarks tab");
            return false;
        }

        var row = rows.Count > 0 ? rows[selected] : null;
        switch (command)
        {
            case "folder":
                NewBookmarkFolder(rows, selected, filter, CommandLine.LastArgument);
                break;
            case "rename":
                if (row != null) RenameBookmark(row, CommandLine.LastArgument);
                break;
            case "color":
                if (row != null) PickBookmarkColor(row);
                break;
            case "remove":
                RemoveBookmarks(rows, selected);
                break;
            case "move":
                MoveBookmarksToFolder(rows, selected);
                break;
            case "top":
            case "bottom":
                if (row != null) MoveBookmarksToEnd(rows, selected, command == "top");
                break;
            case "u":
            case "undo":
                UndoBookmarkRemove();
                break;
            case "expand":
            case "collapse":
                SetBookmarkFolders(command == "collapse");
                break;
            case "clearbook":
                ClearBookmarks();
                break;
        }

        // (the end of the commands)
        return false;
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
