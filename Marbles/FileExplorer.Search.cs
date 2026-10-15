namespace Marbles;

// Searching for files from the file browser: the box to fill in, the search with its progress, and the list
// of the files found. The search itself is done by the class FileSearch.
internal static partial class FileExplorer
{
    private static List<FileSearch.Result> _found = new(); // The files found by the last search
    private static List<int> _foundShown = new(); // The ones shown (their numbers in the list)
    private static string _foundRoot = string.Empty; // The directory that was searched
    private static string _foundSummary = string.Empty;
    private static bool _foundHasText; // The content of the files was searched
    private static int _foundIndex;
    private static int _foundTop;
    private static string _foundFilter = string.Empty;
    private static string _foundMessage = string.Empty;

    // Where the cursor is placed in the next file opened in the editor
    private static int _openAtIndex;
    private static bool _openAtFound; // A file is opened at the line found by the search (not where it was left)

    // Fill in what to search for, search the directory of the pane with the focus, and show the files found
    private static KeyResult SearchFiles()
    {
        var root = ActivePane.Directory;
        if (root.Length == 0)
        {
            _message = DialogText.DirectoryNotFound;
            return KeyResult.Handled;
        }

        var hasResults = false;
        while (true)
        {
            if (FileSearchDialog.Show(root))
            {
                RunSearch(root);
                hasResults = true;
            }
            else if (!hasResults)
            {
                return KeyResult.Handled;
            }

            // (closing the box of a new search returns to the files found before)
            var result = BrowseFound();
            // "S" in the list of files found: search again
            if (result != null) return result.Value;
            Draw(true);
        }
    }

    private static void RunSearch(string root)
    {
        var options = FileSearchDialog.Options;
        EventLog.Add(DialogText.LogFile + "Search: " + root + (options.Subfolders ? " (with subfolders)" : ""));
        var outcome = FileSearch.Search(root, options, ShowSearchProgress);
        _found = outcome.Results;
        _foundRoot = root;
        _foundHasText = options.Text.Length > 0;
        _foundIndex = 0;
        _foundTop = 0;
        _foundFilter = string.Empty;
        _foundSummary = (_found.Count == 1 ? Lang.F("{0} file found", 1) : Lang.F("{0} files found", _found.Count)) +
                        (outcome.Capped ? DialogText.SearchCapped : string.Empty) +
                        (outcome.Stopped ? DialogText.SearchStopped : string.Empty) +
                        (outcome.Unreadable > 0 ? " (" + Lang.F("{0} directories could not be read", outcome.Unreadable) + ")" : "");
        _foundMessage = string.Empty;
    }

    // Show the directory being searched on the bottom line. Esc stops the search
    private static bool ShowSearchProgress(string directory, int count)
    {
        var text = DialogText.SearchProgress.Replace("{0}", count.ToString());
        var room = Init.LineWidth - 4 - text.Length;
        if (room > 8) directory = TextMetrics.KeepEnd(directory, room);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Console.Write(" " + Tools.TruncateString(text + directory, Init.LineWidth - 4, true) + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        try
        {
            while (Console.KeyAvailable)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.Escape) return false;
            }
        }
        catch
        {
            // Without a keyboard to read there is nothing to stop with
        }

        return true;
    }

    private static void FilterFound()
    {
        _foundShown = new List<int>();
        for (var i = 0; i < _found.Count; i++)
        {
            if (_foundFilter.Length == 0 ||
                _found[i].Path.Contains(_foundFilter, StringComparison.OrdinalIgnoreCase) ||
                _found[i].LineText.Contains(_foundFilter, StringComparison.OrdinalIgnoreCase))
            {
                _foundShown.Add(i);
            }
        }
    }

    // Show the files found until the list is closed. Opening a file returns to the list afterwards.
    // Returns null to search again
    private static KeyResult? BrowseFound()
    {
        var redraw = true;
        while (true)
        {
            FilterFound();
            _foundIndex = Math.Clamp(_foundIndex, 0, Math.Max(0, _foundShown.Count - 1));
            if (Init.ApplyAutoSize()) redraw = true;
            DrawFound(redraw);
            if (Tools.TakeRedrawRequest()) DrawFound(true);
            redraw = true;
            var hadMessage = _foundMessage.Length > 0;
            _foundMessage = string.Empty;
            var selected = _foundShown.Count > 0 ? _found[_foundShown[_foundIndex]] : null;
            var key = Console.ReadKey(true);
            if (Jump.IsKey(key))
            {
                // The Jump menu
                Jump.Open();
                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.KeyChar == ':')
            {
                var command = CommandLine.Read(CommandContext.Search);
                if (command is "jump" or "ju")
                {
                    // The Jump menu
                    Jump.Open();
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
                        Tools.WriteEditorHelp(false, Tools.HelpTopic.Search);
                        break;
                    case "find":
                        return null;
                    case "info":
                        ShowFoundInfo(selected);
                        break;
                    case "goto":
                        if (selected != null && ShowFoundInBrowser(selected)) return KeyResult.Handled;
                        break;
                    case "fav":
                    case "favorite":
                        ToggleFoundFavorite(selected);
                        break;
                    default:
                        CommandLine.ShowUnknown(command);
                        break;
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Search);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the files with a text in the path (or in the line found)
                _foundFilter = CommandLine.Read('/', false) ?? string.Empty;
                _foundIndex = 0;
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _foundIndex = _foundShown.Count - 1;
                redraw = hadMessage;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _foundIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.Backspace ||
                     navigation == ConsoleKey.LeftArrow || key.KeyChar == '-')
            {
                // Esc removes a filter before it closes the list
                if (key.Key == ConsoleKey.Escape && _foundFilter.Length > 0)
                {
                    _foundFilter = string.Empty;
                }
                else
                {
                    return KeyResult.Handled;
                }
            }
            else if (navigation == ConsoleKey.DownArrow)
            {
                _foundIndex++;
                redraw = hadMessage;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _foundIndex--;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageDown)
            {
                _foundIndex += FoundPageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageUp)
            {
                _foundIndex -= FoundPageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                _foundIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.S)
            {
                return null;
            }
            else if (selected == null)
            {
                // The other keys need a file to work on
            }
            else if (key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow)
            {
                if (selected.IsDirectory)
                {
                    if (ShowFoundInBrowser(selected)) return KeyResult.Handled;
                }
                else if (OpenFound(selected))
                {
                    return KeyResult.Close;
                }
            }
            else if (key.Key == ConsoleKey.B)
            {
                if (ShowFoundInBrowser(selected)) return KeyResult.Handled;
            }
            else if (key.Key == ConsoleKey.I)
            {
                ShowFoundInfo(selected);
            }
            else if (key.Key == ConsoleKey.P)
            {
                ToggleFoundFavorite(selected);
            }
            else
            {
                redraw = hadMessage;
            }
        }
    }

    private static bool FoundExists(FileSearch.Result result)
    {
        if (result.IsDirectory ? Directory.Exists(result.Path) : File.Exists(result.Path)) return true;
        _foundMessage = DialogText.FileNotFound;
        return false;
    }

    // Open a file in the editor, at the line that was found. True if the browser must close (a data file)
    private static bool OpenFound(FileSearch.Result result)
    {
        if (!FoundExists(result)) return false;
        _openAtIndex = 0;
        _openAtFound = result.Line > 0;
        if (result.Line > 0)
        {
            try
            {
                // The place of the line in the text as the editor has it (with one character between lines)
                var lines = TextFile.Load(result.Path).Text.Split('\n');
                for (var i = 0; i < result.Line && i < lines.Length; i++)
                {
                    _openAtIndex += lines[i].TrimEnd('\r').Length + 1;
                }
            }
            catch
            {
                _openAtIndex = 0;
            }
        }

        _message = string.Empty;
        var close = Open(new Item(System.IO.Path.GetFileName(result.Path), result.Path, false, result.Size));
        _openAtIndex = 0;
        _openAtFound = false;
        _foundMessage = _message;
        _message = string.Empty;
        return close;
    }

    // Show the file in the pane with the focus: its directory, with the file selected
    private static bool ShowFoundInBrowser(FileSearch.Result result)
    {
        if (!FoundExists(result)) return false;
        var parent = System.IO.Path.GetDirectoryName(result.Path);
        if (parent == null || !Directory.Exists(parent))
        {
            _foundMessage = DialogText.DirectoryNotFound;
            return false;
        }

        ChangeDirectory(parent, result.Path);
        return true;
    }

    private static void ShowFoundInfo(FileSearch.Result? result)
    {
        if (result == null || !FoundExists(result)) return;
        _message = string.Empty;
        ShowInfo(new Item(System.IO.Path.GetFileName(result.Path), result.Path, result.IsDirectory, result.Size));
        _foundMessage = _message;
        _message = string.Empty;
    }

    private static void ToggleFoundFavorite(FileSearch.Result? result)
    {
        if (result == null || !FoundExists(result)) return;
        _foundMessage = FileFavorites.Toggle(result.Path) ? DialogText.FavoriteAdded : DialogText.FavoriteRemoved;
        var saved = FileFavorites.Save();
        if (saved != DialogText.Success) _foundMessage = saved;
    }

    #region Drawing

    private static int FoundPageSize => Init.LineCount - ListTop - 2;
    private static int _shownFoundTop = -1;
    private static int _shownFoundIndex = -1;

    private static void DrawFound(bool all)
    {
        if (_foundIndex < _foundTop) _foundTop = _foundIndex;
        if (_foundIndex >= _foundTop + FoundPageSize) _foundTop = _foundIndex - FoundPageSize + 1;
        _foundTop = Math.Max(0, _foundTop);

        Tools.HideCursor();
        if (!all && _foundTop == _shownFoundTop)
        {
            // Move the selection pointer
            DrawFoundPointer(_shownFoundIndex, false);
            DrawFoundPointer(_foundIndex, true);
            _shownFoundIndex = _foundIndex;
            DrawFoundStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.SearchResultCaption, string.Empty, 0);
        Menu.WriteNameInTopBar(false);
        // What was found, and where
        var header = _foundSummary + " " + Lang.T("in") + " ";
        var room = Init.LineWidth - 2 - header.Length;
        var root = _foundRoot;
        if (room > 8) root = TextMetrics.KeepEnd(root, room);
        Tools.SetCursorEx(1, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(Tools.TruncateString(header + root, Init.LineWidth - 2, true));
        Tools.SetCursorEx(2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));

        // The names in a column, followed by the directory (or the line that was found)
        var nameWidth = 0;
        for (var row = 0; row < FoundPageSize && _foundTop + row < _foundShown.Count; row++)
        {
            nameWidth = Math.Max(nameWidth, FoundName(_found[_foundShown[_foundTop + row]]).Length);
        }

        nameWidth = Math.Min(nameWidth, Init.LineWidth / 2 - 8);
        for (var row = 0; row < FoundPageSize && _foundTop + row < _foundShown.Count; row++)
        {
            var index = _foundTop + row;
            var result = _found[_foundShown[index]];
            var info = result.IsDirectory ? DialogText.DirectoryMark : FileSizeFormatter.FormatSize(result.Size);
            DrawFoundPointer(index, index == _foundIndex);
            ConsoleColors.SetColors(result.IsDirectory ? ConsoleColors.ColorPreset.HeaderColor :
                result.Path.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase)
                    ? ConsoleColors.ColorPreset.Favorites
                    : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + Tools.TruncateString(FoundName(result), nameWidth + 1, true));
            if (FileFavorites.Contains(result.Path))
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
                Console.Write(" *");
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var column = 3 + nameWidth + 4;
            room = Init.LineWidth - column - SizeColumnWidth - 3;
            if (room > 8)
            {
                Tools.SetCursorEx(ListTop + row, column);
                if (_foundHasText && result.Line >= 0)
                {
                    // The line with the text, and its number
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(Tools.TruncateString(
                        (result.Line + 1) + ": " + result.LineText.Replace('\t', ' '), room, true));
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                }
                else
                {
                    // The directory it is in, from the directory that was searched
                    var directory = System.IO.Path.GetRelativePath(_foundRoot,
                        System.IO.Path.GetDirectoryName(result.Path) ?? _foundRoot);
                    directory = TextMetrics.KeepEnd(directory, room);
                    Console.Write(directory);
                }
            }

            Tools.SetCursorEx(ListTop + row, Init.LineWidth - info.Length - 1);
            Console.Write(info);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        _shownFoundTop = _foundTop;
        _shownFoundIndex = _foundIndex;
        DrawFoundStatus();
    }

    // The name of a file found. When the content was searched the directory is part of it, as the line
    // found is shown where the directory would be
    private static string FoundName(FileSearch.Result result)
    {
        var name = System.IO.Path.GetFileName(result.Path) + (result.IsDirectory ? "/" : "");
        if (!_foundHasText) return name;
        var directory = System.IO.Path.GetRelativePath(_foundRoot,
            System.IO.Path.GetDirectoryName(result.Path) ?? _foundRoot);
        return directory == "." ? name : System.IO.Path.Combine(directory, name);
    }

    private static void DrawFoundPointer(int index, bool selected)
    {
        var row = index - _foundTop;
        if (row < 0 || row >= FoundPageSize || index >= _foundShown.Count) return;
        Tools.SetCursorEx(ListTop + row, 0);
        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.SelectorInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected file, and a message or the keys
    private static void DrawFoundStatus()
    {
        Tools.SetCursorEx(Init.LineCount - 2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        var status = " " + (_foundShown.Count == 0 ? 0 : _foundIndex + 1) + "/" + _foundShown.Count + " ";
        if (_foundFilter.Length > 0) status += "/" + _foundFilter + " ";
        Tools.SetCursorEx(Init.LineCount - 2, Init.LineWidth - status.Length - 2);
        Console.Write(status);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_foundMessage.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_foundMessage, Init.LineWidth - 4, true) + " ");
        }
        else
        {
            Menu.WriteKeyHints(DialogText.SearchResultKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
