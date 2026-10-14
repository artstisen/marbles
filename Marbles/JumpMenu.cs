namespace Marbles;

/// <summary>
/// The Jump menu: a box on top of the page with the tabs Jumps (the jump history), Files (the files opened from
/// the file browser), Favorites, Configuration (pages of Marbles) and Data files. A place is picked with the
/// selection arrow (j/k, gg/G) and Enter; h/l go from tab to tab, "/" filters the list of the tab, I shows
/// information about the selected place and D takes a place out of the jump history. Only names are shown, not
/// paths. With one mode only (see StartMenu.Only) the tabs of the other mode are left out
/// </summary>
internal static class JumpMenu
{
    private enum Tab
    {
        Jumps,
        Files,
        Favorites,
        Configuration,
        DataFiles
    }

    // A place in the list of a tab, and when it was jumped to or opened (DateTime.MinValue: not known). A file or
    // directory that is a favorite with a color of its own is shown in it
    private sealed record Row(JumpTarget Target, DateTime Time)
    {
        internal ConsoleColor? Color { get; init; } = Target.Kind is JumpKind.File or JumpKind.Directory
            ? FileFavorites.ColorOf(Target.Target)
            : null;
    }

    private const int MarkWidth = 4; // "FILE", "DIR", "PAGE", "DATA"
    private const int TimeWidth = 10; // "09:41" today, else "2026-10-12"

    // The tabs that are shown
    private static List<Tab> Tabs()
    {
        var tabs = new List<Tab> { Tab.Jumps };
        if (StartMenu.Only != StartMenu.OnlyMode.Notes)
        {
            tabs.Add(Tab.Files);
            tabs.Add(Tab.Favorites);
        }

        tabs.Add(Tab.Configuration);
        if (StartMenu.Only != StartMenu.OnlyMode.Editor) tabs.Add(Tab.DataFiles);
        return tabs;
    }

    // In the language in use
    private static string TabName(Tab tab) => tab switch
    {
        Tab.Jumps => Lang.T("Jumps"),
        Tab.Files => DialogText.CaptionsFiles,
        Tab.Favorites => Lang.T("Favorites"),
        Tab.Configuration => DialogText.StartConfiguration,
        _ => Lang.T("Data files")
    };

    // The places a mode that is not used leads to are left out
    private static bool Usable(JumpTarget target) => target.Kind switch
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
            case Tab.Jumps:
                JumpHistory.Load();
                return JumpHistory.Items.Where(entry => Usable(entry.Target))
                    .Select(entry => new Row(entry.Target, entry.Jumped)).ToList();
            case Tab.Files:
                FileHistory.Load();
                return FileHistory.Items.Select(entry => new Row(new JumpTarget(JumpKind.File, entry.Path), entry.Opened))
                    .ToList();
            case Tab.Favorites:
                FileFavorites.Load();
                return FileFavorites.Items.Where(favorite => !favorite.IsFolder)
                    .Select(favorite => new Row(new JumpTarget(
                        Directory.Exists(favorite.Path) ? JumpKind.Directory : JumpKind.File, favorite.Path),
                        DateTime.MinValue)).ToList();
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
        if (name.Length <= width) return name;
        if (width <= 3) return name[..Math.Max(0, width)];
        var end = (width - 3) / 2;
        var start = width - 3 - end;
        return name[..start] + "..." + name[^end..];
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
        var tabs = Tabs();
        // The favorites, for the colors of the names
        FileFavorites.Load();
        var filters = new Dictionary<Tab, string>();
        var tab = 0;
        var selected = 0;
        var scroll = 0;
        while (true)
        {
            var current = tabs[tab];
            var filter = filters.GetValueOrDefault(current, string.Empty);
            var rows = RowsOf(current).Where(row => filter.Length == 0 ||
                row.Target.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            selected = Math.Clamp(selected, 0, Math.Max(0, rows.Count - 1));
            var visible = Draw(tabs, tab, filter, rows, selected, ref scroll);
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (Jump.IsKey(key)) return null;
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
                case 'd':
                case 'D':
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

        // The tabs, in a row as the sub-tabs of a frame with tabs, with the keys that change them at the end
        var names = tabs.Select(TabName).ToList();
        var starts = TabLayout.SubTabStarts(names);
        for (var i = 0; i < names.Count; i++)
        {
            Tools.SetCursorEx(top + 1, left + 2 + starts[i] - 1);
            if (i == tab) ConsoleColors.SetTabColors();
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.OtherTabs);
            Console.Write(" " + names[i] + " ");
        }

        const string tabKeys = "h/l"; // English: the keys
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.TabFrame);
        Tools.SetCursorEx(top + 1, left + width - tabKeys.Length - 1);
        Console.Write(tabKeys);
        Tools.SetCursorEx(top + 2, left + 2);
        Console.Write(new string('-', Math.Max(0, width - 2)));

        // The filter
        Tools.SetCursorEx(top + 3, left + 3);
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

        // The jump history only holds the jumps made in the menu (not the files and pages opened in other ways)
        var room = left + width - 1 - Console.CursorLeft - 2;
        if (tabs[tab] == Tab.Jumps && room > 10)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write("  " + Tools.TruncateString(Lang.T("Only jumps made in this menu, the latest first"), room, true));
        }

        // The places
        var first = top + 5;
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
            Console.Write(index == selected ? "->" : "  ");

            // The name, in the color of a favorite that has one (a place that is not there any more is dimmed and
            // marked)
            var exists = row.Target.Exists;
            var name = exists ? row.Target.Name : row.Target.Name + " " + DialogText.FavoriteMissing;
            name = ShortName(name, nameWidth);
            Tools.SetCursorEx(first + line, left + 4);
            if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else if (row.Color is { } color) ConsoleColors.SetTextColor(color);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(name);

            // The kind, and when
            Tools.SetCursorEx(first + line, left + width - MarkWidth - TimeWidth - 2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Mark(row.Target).PadRight(MarkWidth) + "  " + When(row.Time).PadLeft(TimeWidth));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        ChoiceBox.WriteHints(DialogText.JumpKeys);
        return visible;
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
