namespace Marbles;

// The history of the file browser, shown as a list: the files opened in the editor from the file browser, the
// last opened first. The list itself is kept by the class FileHistory.
internal static partial class FileExplorer
{
    private static List<int> _historyShown = new(); // The files shown (their numbers in the whole list)
    private static int _historyIndex;
    private static int _historyTop;
    private static string _historyFilter = string.Empty;
    private static string _historyMessage = string.Empty;

    // Show the history until the list is closed. Opening a file returns to the list afterwards
    private static KeyResult BrowseHistory()
    {
        FileHistory.Load();
        _historyFilter = string.Empty;
        _historyMessage = string.Empty;
        _historyIndex = 0;
        _historyTop = 0;
        var redraw = true;
        while (true)
        {
            FilterHistory();
            _historyIndex = Math.Clamp(_historyIndex, 0, Math.Max(0, _historyShown.Count - 1));
            // Follow the size of the terminal window, and draw again if the page could not be drawn
            if (Init.ApplyAutoSize()) redraw = true;
            DrawHistory(redraw);
            if (Tools.TakeRedrawRequest()) DrawHistory(true);
            redraw = true;
            var message = _historyMessage;
            var hadMessage = message.Length > 0;
            _historyMessage = string.Empty;
            var selected = _historyShown.Count > 0 ? _historyShown[_historyIndex] : -1;
            var key = Keyboard.ReadKey();
            // The window was resized (or Marbles went on after Ctrl+Z): all is drawn again, with the message
            if (Keyboard.IsRedraw(key))
            {
                _historyMessage = message;
                continue;
            }

            if (Jump.IsKey(key))
            {
                // The Jump menu
                Jump.Open();
                continue;
            }

            if (RecentBox.IsKey(key))
            {
                // The latest files: the one picked is opened in the editor (a count typed before it is dropped)
                _listCount.Take();
                RecentBox.Open();
                continue;
            }

            // A count in front of a key: 5j 5k (5 rows), 5gg 5G (row 5). Esc only drops it
            if (_listCount.Add(key))
            {
                redraw = hadMessage;
                continue;
            }

            var count = _listCount.Take();
            if (count != null && key.Key == ConsoleKey.Escape)
            {
                redraw = hadMessage;
                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.KeyChar == ':')
            {
                var command = CommandLine.Read(CommandContext.History);
                if (command != null && CommandLine.Name(command) is "jump" or "ju" or "jmp")
                {
                    // The Jump menu (":jump colors": that page at once)
                    Jump.ToPage(CommandLine.Argument(command));
                    continue;
                }

                if (command != null && CommandLine.Name(command) == "book")
                {
                    // ":book 3": jump to bookmark 3
                    Jump.ToBookmark(CommandLine.Argument(command));
                    continue;
                }

                if (command != null && CommandLine.Name(command) == "recent")
                {
                    // The latest files (the list drawn first: the box of commands has just been taken away)
                    DrawHistory(true);
                    RecentBox.Open();
                    continue;
                }

                switch (command)
                {
                    case null:
                        break;
                    case "q":
                    case "quit":
                    case "close":
                        return KeyResult.Handled;
                    case "h":
                    case "help":
                        Tools.WriteEditorHelp(false, Tools.HelpTopic.History);
                        break;
                    case "info":
                        ShowHistoryInfo(selected);
                        break;
                    case "goto":
                        if (ShowHistoryInBrowser(selected)) return KeyResult.Handled;
                        break;
                    case "name":
                        if (selected >= 0)
                        {
                            _historyMessage = CopyOfList(System.IO.Path.GetFileName(FileHistory.Items[selected].Path));
                        }

                        break;
                    case "path":
                        if (selected >= 0) _historyMessage = CopyOfList(FileHistory.Items[selected].Path);
                        break;
                    case "fav":
                    case "favorite":
                        ToggleHistoryFavorite(selected);
                        break;
                    case "remove":
                        RemoveFromHistory(selected);
                        break;
                    case "clear":
                        ClearHistory();
                        break;
                    case "missing":
                        ShowMissingHistory();
                        break;
                    case "prune":
                        PruneHistory();
                        break;
                    case "number":
                    case "nonumber":
                        // The numbers of the rows on or off (kept in the settings)
                        HistoryNumbers = command == "number";
                        _historyMessage = HistoryNumbers ? DialogText.LineNumbersOn : DialogText.LineNumbersOff;
                        FileIo.SaveSettings();
                        break;
                    default:
                        CommandLine.ShowUnknown(command);
                        break;
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.History);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the files with a text in the path
                _historyFilter = CommandLine.Read('/', false) ?? string.Empty;
                _historyIndex = 0;
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _historyIndex = ListCount.Row(key.KeyChar == 'G' ? count : null, _historyShown.Count, false);
                redraw = hadMessage;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _historyIndex = ListCount.Row(count, _historyShown.Count, true);
                redraw = hadMessage;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.O or ConsoleKey.Backspace ||
                     navigation == ConsoleKey.LeftArrow || key.KeyChar == '-')
            {
                // Esc removes a filter before it closes the list
                if (key.Key == ConsoleKey.Escape && _historyFilter.Length > 0)
                {
                    _historyFilter = string.Empty;
                }
                else
                {
                    return KeyResult.Handled;
                }
            }
            else if (navigation == ConsoleKey.DownArrow)
            {
                _historyIndex += count ?? 1;
                redraw = hadMessage;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _historyIndex = Math.Max(0, _historyIndex - (count ?? 1));
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageDown)
            {
                _historyIndex += HistoryPageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageUp)
            {
                _historyIndex -= HistoryPageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                _historyIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.X)
            {
                ClearHistory();
            }
            else if (selected < 0)
            {
                // The other keys need a file to work on
            }
            else if (key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow)
            {
                if (OpenFromHistory(selected)) return KeyResult.Close;
            }
            else if (key.Key == ConsoleKey.B)
            {
                if (ShowHistoryInBrowser(selected)) return KeyResult.Handled;
            }
            else if (key.Key == ConsoleKey.I)
            {
                ShowHistoryInfo(selected);
            }
            else if (key.Key == ConsoleKey.P)
            {
                ToggleHistoryFavorite(selected);
            }
            else if (key.Key == ConsoleKey.D)
            {
                RemoveFromHistory(selected);
            }
            else
            {
                redraw = hadMessage;
            }
        }
    }

    private static int HistoryPageSize => Init.LineCount - ListTop - 2;

    private static void FilterHistory()
    {
        _historyShown = new List<int>();
        for (var i = 0; i < FileHistory.Items.Count; i++)
        {
            if (_historyFilter.Length == 0 ||
                FileHistory.Items[i].Path.Contains(_historyFilter, StringComparison.OrdinalIgnoreCase))
            {
                _historyShown.Add(i);
            }
        }
    }

    // The path of a file of the history, if the file is still there (else the message tells it is not)
    private static string? HistoryPath(int number)
    {
        if (number < 0) return null;
        var path = FileHistory.Items[number].Path;
        if (File.Exists(path)) return path;
        _historyMessage = DialogText.FileNotFound;
        return null;
    }

    // Open a file in the editor. True if the browser must close (a data file was selected)
    private static bool OpenFromHistory(int number)
    {
        var path = HistoryPath(number);
        if (path == null) return false;
        _message = string.Empty;
        var close = Open(new Item(System.IO.Path.GetFileName(path), path, false, new FileInfo(path).Length));
        _historyMessage = _message;
        _message = string.Empty;
        // The file is now first in the list
        _historyFilter = string.Empty;
        _historyIndex = 0;
        return close;
    }

    // Show the file in the pane with the focus: its directory, with the file selected
    private static bool ShowHistoryInBrowser(int number)
    {
        var path = HistoryPath(number);
        if (path == null) return false;
        var parent = System.IO.Path.GetDirectoryName(path);
        if (parent == null || !Directory.Exists(parent))
        {
            _historyMessage = DialogText.DirectoryNotFound;
            return false;
        }

        ChangeDirectory(parent, path);
        return true;
    }

    private static void ShowHistoryInfo(int number)
    {
        var path = HistoryPath(number);
        if (path == null) return;
        _message = string.Empty;
        ShowInfo(new Item(System.IO.Path.GetFileName(path), path, false, new FileInfo(path).Length));
        _historyMessage = _message;
        _message = string.Empty;
    }

    private static void ToggleHistoryFavorite(int number)
    {
        var path = HistoryPath(number);
        if (path == null) return;
        _historyMessage = FileFavorites.Toggle(path) ? DialogText.FavoriteAdded : DialogText.FavoriteRemoved;
        var saved = FileFavorites.Save();
        if (saved != DialogText.Success) _historyMessage = saved;
    }

    // Take the selected file out of the history (the file itself stays)
    private static void RemoveFromHistory(int number)
    {
        if (number < 0) return;
        var path = FileHistory.Items[number].Path;
        var name = System.IO.Path.GetFileName(path);
        FileHistory.RemoveAt(number);
        EventLog.Add(DialogText.LogFile + "History remove: " + path);
        _historyMessage = Lang.F("Removed from the history: {0}", name);
        SaveHistory();
    }

    // The numbers of the files of the history that are not there (each drive looked at once by the checker)
    private static List<int> MissingHistory(MissingPlaces.Checker check) => Enumerable.Range(0, FileHistory.Items.Count)
        .Where(i => check.IsMissing(FileHistory.Items[i].Path, true)).ToList();

    // ":missing": the missing files in a box. The one picked is selected in the list
    private static void ShowMissingHistory()
    {
        var check = new MissingPlaces.Checker();
        var missing = MissingHistory(check);
        if (missing.Count == 0)
        {
            _historyMessage = MissingPlaces.NothingMissing;
            return;
        }

        // (the list drawn first: the box of commands has just been taken away)
        DrawHistory(true);
        var picked = MissingPlaces.Pick(missing.Select(i => FileHistory.Items[i].Path)
            .Select(path => (System.IO.Path.GetFileName(path), path, check.OnAbsentDrive(path, true))).ToList());
        if (picked < 0) return;
        _historyFilter = string.Empty;
        FilterHistory();
        _historyIndex = Math.Max(0, _historyShown.IndexOf(missing[picked]));
    }

    // ":prune": take the missing files out of the history, after asking. Those on a drive that is not connected are
    // kept. Only their number is logged
    private static void PruneHistory()
    {
        var check = new MissingPlaces.Checker();
        var (prune, kept) = MissingPlaces.Sort(MissingHistory(check),
            i => check.OnAbsentDrive(FileHistory.Items[i].Path, true));
        if (prune.Count == 0)
        {
            _historyMessage = MissingPlaces.NothingToPrune(kept.Count);
            return;
        }

        DrawHistory(true);
        if (!MissingPlaces.Confirm(DialogText.CaptionsFileHistory, prune.Count, kept.Count, false)) return;
        FileHistory.RemoveMany(prune);
        EventLog.Add(DialogText.LogFile + "History prune: " + prune.Count);
        _historyFilter = string.Empty;
        _historyMessage = Lang.F("Files removed from the history: {0}", prune.Count);
        SaveHistory();
    }

    // Empty the history, after asking
    private static void ClearHistory()
    {
        if (FileHistory.Items.Count == 0) return;
        if (!ChoiceBox.Confirm(DialogText.HistoryClearCaption,
                Lang.F("Remove all the files from the history ({0})?", FileHistory.Items.Count),
                DialogText.HistoryClearNote, true))
        {
            return;
        }

        FileHistory.Clear();
        EventLog.Add(DialogText.LogFile + "History cleared");
        _historyFilter = string.Empty;
        _historyIndex = 0;
        _historyMessage = DialogText.HistoryCleared;
        SaveHistory();
    }

    private static void SaveHistory()
    {
        var result = FileHistory.Save();
        if (result != DialogText.Success) _historyMessage = result;
    }

    // Add a file opened in the editor to the history
    private static void AddToHistory(string path)
    {
        FileHistory.Load();
        FileHistory.Add(path, DateTime.Now);
        var result = FileHistory.Save();
        if (result != DialogText.Success) EventLog.Add(DialogText.LogFile + "History: " + result);
    }

    #region Drawing

    private static int _shownHistoryTop = -1;
    private static int _shownHistoryIndex = -1;

    private const int HistoryTimeWidth = 16; // The width of "2026-10-10 20:45"

    // The columns of the lines between the columns of the list (none when it is empty)
    private static int[] _historyLines = Array.Empty<int>();

    private static void DrawHistory(bool all)
    {
        if (_historyIndex < _historyTop) _historyTop = _historyIndex;
        if (_historyIndex >= _historyTop + HistoryPageSize) _historyTop = _historyIndex - HistoryPageSize + 1;
        _historyTop = Math.Max(0, _historyTop);

        Tools.HideCursor();
        if (!all && _historyTop == _shownHistoryTop)
        {
            // Move the selection pointer
            DrawHistoryPointer(_shownHistoryIndex, false);
            DrawHistoryPointer(_historyIndex, true);
            _shownHistoryIndex = _historyIndex;
            DrawHistoryStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsFileHistory, string.Empty, 0);
        Menu.WriteNameInTopBar(false);
        Tools.SetCursorEx(1, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(Tools.TruncateString(FileHistory.Items.Count == 0
            ? DialogText.HistoryEmpty
            : Lang.F("Files opened from the file browser, the last opened first (at most {0})",
                FileHistory.MaxCount), Init.LineWidth - 2, true));
        Tools.SetCursorEx(2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth - 1));

        // The columns: the names (with a * after a favorite), the directories they are in, and the time each was
        // opened, with lines between them and their labels in the divider line (as in the favorites)
        var nameWidth = 0;
        foreach (var number in _historyShown)
        {
            nameWidth = Math.Max(nameWidth, System.IO.Path.GetFileName(FileHistory.Items[number].Path).Length);
        }

        nameWidth = Math.Min(nameWidth, Init.LineWidth / 2 - 8 - HistoryNumberWidth);
        var infoWidth = Math.Max(HistoryTimeWidth, Math.Max(TextMetrics.WidthOf(DialogText.FavoriteMissing),
            TextMetrics.WidthOf(DialogText.ColumnOpened) + 2));
        var nameLine = 3 + HistoryNumberWidth + nameWidth + 4;
        var infoLine = Init.LineWidth - 2 - infoWidth - 1;
        _historyLines = _historyShown.Count > 0 ? new[] { nameLine, infoLine } : Array.Empty<int>();
        if (_historyShown.Count > 0)
        {
            DrawColumnLabels(2, new[]
            {
                (1 + HistoryNumberWidth, DialogText.ColumnName), (nameLine, DialogText.ColumnPath),
                (infoLine, DialogText.ColumnOpened)
            }, _historyLines);
            for (var row = 0; row < HistoryPageSize; row++)
            {
                foreach (var line in _historyLines) DrawColumnLine(ListTop + row, line);
            }
        }

        for (var row = 0; row < HistoryPageSize && _historyTop + row < _historyShown.Count; row++)
        {
            var index = _historyTop + row;
            var entry = FileHistory.Items[_historyShown[index]];
            var exists = File.Exists(entry.Path);
            var info = !exists ? DialogText.FavoriteMissing :
                entry.Opened == DateTime.MinValue ? string.Empty : entry.Opened.ToString("yyyy-MM-dd HH:mm");
            DrawHistoryPointer(index, index == _historyIndex);
            ConsoleColors.SetColors(entry.Path.EndsWith(FileIo.GetFileNameExtension(),
                StringComparison.OrdinalIgnoreCase)
                ? ConsoleColors.ColorPreset.Favorites
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + Tools.TruncateString(System.IO.Path.GetFileName(entry.Path), nameWidth + 1, true));
            if (FileFavorites.Contains(entry.Path))
            {
                // A favorite
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
                Console.Write(" *");
            }

            // The directory it is in (the end of a long path is shown), between the lines
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var column = nameLine + 2;
            var room = infoLine - 1 - column;
            if (room > 8)
            {
                var directory = System.IO.Path.GetDirectoryName(entry.Path) ?? string.Empty;
                directory = TextMetrics.KeepEnd(directory, room);
                Tools.SetCursorEx(ListTop + row, column);
                Console.Write(directory);
            }

            if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Tools.SetCursorEx(ListTop + row, Init.LineWidth - info.Length - 1);
            Console.Write(info);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        _shownHistoryTop = _historyTop;
        _shownHistoryIndex = _historyIndex;
        DrawHistoryStatus();
    }

    // The width of the numbers of the rows (when they are on)
    private static int HistoryNumberWidth => ListNumbers.Width(HistoryNumbers, _historyShown.Count);

    // The selection pointer of a row (and before it the number of the row, when the numbers are on)
    private static void DrawHistoryPointer(int index, bool selected)
    {
        var row = index - _historyTop;
        if (row < 0 || row >= HistoryPageSize || index >= _historyShown.Count) return;
        Tools.SetCursorEx(ListTop + row, 0);
        ListNumbers.Write(index, HistoryNumberWidth, selected);
        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.SelectorInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected file, and a message or the keys
    private static void DrawHistoryStatus()
    {
        Tools.SetCursorEx(Init.LineCount - 2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth - 1));
        DrawColumnEnds(Init.LineCount - 2, _historyLines);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        var status = " " + (_historyShown.Count == 0 ? 0 : _historyIndex + 1) + "/" + _historyShown.Count + " ";
        if (_listCount.Shown.Length > 0) status = " " + _listCount.Shown + status;
        if (_historyFilter.Length > 0) status += "/" + _historyFilter + " ";
        Tools.SetCursorEx(Init.LineCount - 2, Init.LineWidth - status.Length - 2);
        Console.Write(status);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_historyMessage.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_historyMessage, Init.LineWidth - 4, true) + " ");
        }
        else
        {
            Menu.WriteKeyHints(DialogText.HistoryKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
