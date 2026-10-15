namespace Marbles;

// The directory hotlist of the file browser, shown as a list (W): directories kept to go back to (B adds the
// directory shown). Enter goes to the selected one; the order can be changed. The list itself is kept by the class
// DirectoryHotlist.
internal static partial class FileExplorer
{
    private static List<int> _hotlistShown = new(); // The directories shown (their numbers in the whole list)
    private static int _hotlistIndex;
    private static int _hotlistTop;
    private static string _hotlistFilter = string.Empty;
    private static string _hotlistMessage = string.Empty;
    private static bool _hotlistMoving; // Tab or v: j k move the selected directory
    private static (int Index, string Path)? _hotlistRemoved; // For undo

    // B: add the directory shown in the pane with the focus (whatever is selected in it)
    private static void AddToHotlist()
    {
        var directory = ActivePane.Directory;
        if (directory.Length == 0)
        {
            // The list of drives
            _message = DialogText.HotlistNoDirectory;
            return;
        }

        DirectoryHotlist.Load();
        var name = HotlistName(directory);
        switch (DirectoryHotlist.Add(directory))
        {
            case DirectoryHotlist.AddResult.AlreadyThere:
                _message = Lang.F("Already in the hotlist: {0}", name);
                return;
            case DirectoryHotlist.AddResult.Full:
                _message = Lang.F("The hotlist is full ({0} directories)", DirectoryHotlist.MaxCount);
                return;
        }

        EventLog.Add(DialogText.LogFile + "Hotlist add: " + directory);
        var result = DirectoryHotlist.Save();
        _message = result == DialogText.Success ? Lang.F("Added to the hotlist: {0}", name) : result;
    }

    // The name of a directory (a drive: its path)
    private static string HotlistName(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar));
        return name.Length > 0 ? name : path;
    }

    // Show the hotlist until it is closed. Going to a directory closes it
    private static KeyResult BrowseHotlist()
    {
        DirectoryHotlist.Load();
        _hotlistFilter = string.Empty;
        _hotlistMessage = string.Empty;
        _hotlistMoving = false;
        _hotlistRemoved = null;
        _hotlistIndex = 0;
        _hotlistTop = 0;
        var redraw = true;
        while (true)
        {
            FilterHotlist();
            _hotlistIndex = Math.Clamp(_hotlistIndex, 0, Math.Max(0, _hotlistShown.Count - 1));
            // Follow the size of the terminal window, and draw again if the page could not be drawn
            if (Init.ApplyAutoSize()) redraw = true;
            DrawHotlist(redraw);
            if (Tools.TakeRedrawRequest()) DrawHotlist(true);
            redraw = true;
            var hadMessage = _hotlistMessage.Length > 0;
            _hotlistMessage = string.Empty;
            var selected = _hotlistShown.Count > 0 ? _hotlistShown[_hotlistIndex] : -1;
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (_hotlistMoving)
            {
                // Moving the selected directory: j k move it, and Esc, Enter, Tab or v end it
                if (navigation is ConsoleKey.DownArrow or ConsoleKey.UpArrow && selected >= 0)
                {
                    _hotlistIndex = DirectoryHotlist.Move(selected,
                        selected + (navigation == ConsoleKey.DownArrow ? 1 : -1));
                    SaveHotlist();
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.Tab or ConsoleKey.V)
                {
                    _hotlistMoving = false;
                }

                if (_hotlistMoving) _hotlistMessage = DialogText.HotlistMoving;
                continue;
            }

            if (Jump.IsKey(key))
            {
                // The Jump menu
                Jump.Open();
                continue;
            }

            if (key.KeyChar == ':')
            {
                var command = CommandLine.Read(CommandContext.Hotlist);
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
                        Tools.WriteEditorHelp(false, Tools.HelpTopic.Hotlist);
                        break;
                    case "info":
                        ShowHotlistInfo(selected);
                        break;
                    case "remove":
                        RemoveFromHotlist(selected);
                        break;
                    case "u":
                    case "undo":
                        UndoHotlistRemove();
                        break;
                    case "top":
                        MoveHotlistToEnd(selected, true);
                        break;
                    case "bottom":
                        MoveHotlistToEnd(selected, false);
                        break;
                    case "move":
                        StartHotlistMove(selected);
                        break;
                    case "clear":
                        ClearHotlist();
                        break;
                    default:
                        CommandLine.ShowUnknown(command);
                        break;
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Hotlist);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the directories with a text in the path
                _hotlistFilter = CommandLine.Read('/', false) ?? string.Empty;
                _hotlistIndex = 0;
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _hotlistIndex = _hotlistShown.Count - 1;
                redraw = hadMessage;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _hotlistIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.W or ConsoleKey.Backspace ||
                     navigation == ConsoleKey.LeftArrow || key.KeyChar == '-')
            {
                // Esc removes a filter before it closes the list
                if (key.Key == ConsoleKey.Escape && _hotlistFilter.Length > 0)
                {
                    _hotlistFilter = string.Empty;
                }
                else
                {
                    return KeyResult.Handled;
                }
            }
            else if (navigation == ConsoleKey.DownArrow)
            {
                _hotlistIndex++;
                redraw = hadMessage;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _hotlistIndex--;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageDown)
            {
                _hotlistIndex += HistoryPageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageUp)
            {
                _hotlistIndex -= HistoryPageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                _hotlistIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.U)
            {
                UndoHotlistRemove();
            }
            else if (key.Key == ConsoleKey.X)
            {
                ClearHotlist();
            }
            else if (selected < 0)
            {
                // The other keys need a directory to work on
            }
            else if (key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow)
            {
                if (GoToHotlistDirectory(selected)) return KeyResult.Handled;
            }
            else if (key.Key is ConsoleKey.Tab or ConsoleKey.V)
            {
                StartHotlistMove(selected);
            }
            else if (key.Key == ConsoleKey.T)
            {
                MoveHotlistToEnd(selected, true);
            }
            else if (key.Key == ConsoleKey.E)
            {
                MoveHotlistToEnd(selected, false);
            }
            else if (key.Key == ConsoleKey.I)
            {
                ShowHotlistInfo(selected);
            }
            else if (key.Key == ConsoleKey.D)
            {
                RemoveFromHotlist(selected);
            }
            else
            {
                redraw = hadMessage;
            }
        }
    }

    private static void FilterHotlist()
    {
        _hotlistShown = new List<int>();
        for (var i = 0; i < DirectoryHotlist.Items.Count; i++)
        {
            if (_hotlistFilter.Length == 0 ||
                DirectoryHotlist.Items[i].Contains(_hotlistFilter, StringComparison.OrdinalIgnoreCase))
            {
                _hotlistShown.Add(i);
            }
        }
    }

    // The path of a directory of the list, if it is still there (else the message tells it is not)
    private static string? HotlistPath(int number)
    {
        if (number < 0) return null;
        var path = DirectoryHotlist.Items[number];
        if (Directory.Exists(path)) return path;
        _hotlistMessage = DialogText.DirectoryNotFound;
        return null;
    }

    // Enter: the directory in the pane with the focus
    private static bool GoToHotlistDirectory(int number)
    {
        var path = HotlistPath(number);
        if (path == null) return false;
        ChangeDirectory(path, null);
        return true;
    }

    private static void ShowHotlistInfo(int number)
    {
        var path = HotlistPath(number);
        if (path == null) return;
        _message = string.Empty;
        ShowInfo(new Item(HotlistName(path), path, true, 0));
        _hotlistMessage = _message;
        _message = string.Empty;
    }

    // Tab or v: j k move the selected directory (not while a filter shows only some)
    private static void StartHotlistMove(int number)
    {
        if (number < 0) return;
        if (_hotlistFilter.Length > 0)
        {
            _hotlistMessage = DialogText.HotlistMoveFiltered;
            return;
        }

        _hotlistMoving = true;
        _hotlistMessage = DialogText.HotlistMoving;
    }

    // T and E: the selected directory first or last in the list
    private static void MoveHotlistToEnd(int number, bool top)
    {
        if (number < 0) return;
        var place = DirectoryHotlist.Move(number, top ? 0 : DirectoryHotlist.Items.Count - 1);
        SaveHotlist();
        // The selection follows it (in the list as filtered)
        FilterHotlist();
        var shown = _hotlistShown.IndexOf(place);
        if (shown >= 0) _hotlistIndex = shown;
    }

    // Take the selected directory out of the list (the directory itself stays). u puts it back
    private static void RemoveFromHotlist(int number)
    {
        if (number < 0) return;
        var path = DirectoryHotlist.Items[number];
        DirectoryHotlist.RemoveAt(number);
        _hotlistRemoved = (number, path);
        EventLog.Add(DialogText.LogFile + "Hotlist remove: " + path);
        _hotlistMessage = Lang.F("Removed from the hotlist: {0} (u puts it back)", HotlistName(path));
        SaveHotlist();
    }

    private static void UndoHotlistRemove()
    {
        if (_hotlistRemoved is not { } removed) return;
        _hotlistRemoved = null;
        DirectoryHotlist.Insert(removed.Index, removed.Path);
        SaveHotlist();
        _hotlistFilter = string.Empty;
        _hotlistIndex = Math.Clamp(removed.Index, 0, Math.Max(0, DirectoryHotlist.Items.Count - 1));
        _hotlistMessage = Lang.F("Back in the hotlist: {0}", HotlistName(removed.Path));
    }

    // Empty the list, after asking
    private static void ClearHotlist()
    {
        if (DirectoryHotlist.Items.Count == 0) return;
        if (!ChoiceBox.Confirm(DialogText.CaptionsHotlist,
                Lang.F("Remove all {0} directories from the hotlist?", DirectoryHotlist.Items.Count),
                DialogText.HotlistClearNote, true))
        {
            return;
        }

        DirectoryHotlist.Clear();
        _hotlistRemoved = null;
        EventLog.Add(DialogText.LogFile + "Hotlist cleared");
        _hotlistFilter = string.Empty;
        _hotlistIndex = 0;
        _hotlistMessage = DialogText.HotlistCleared;
        SaveHotlist();
    }

    private static void SaveHotlist()
    {
        var result = DirectoryHotlist.Save();
        if (result != DialogText.Success) _hotlistMessage = result;
    }

    // A directory was renamed or moved in the browser: the hotlist follows it
    private static void HotlistPathChanged(string oldPath, string newPath)
    {
        DirectoryHotlist.Load();
        if (DirectoryHotlist.PathChanged(oldPath, newPath)) DirectoryHotlist.Save();
    }

    #region Drawing

    private static int _shownHotlistTop = -1;
    private static int _shownHotlistIndex = -1;

    private static void DrawHotlist(bool all)
    {
        if (_hotlistIndex < _hotlistTop) _hotlistTop = _hotlistIndex;
        if (_hotlistIndex >= _hotlistTop + HistoryPageSize) _hotlistTop = _hotlistIndex - HistoryPageSize + 1;
        _hotlistTop = Math.Max(0, _hotlistTop);

        Tools.HideCursor();
        if (!all && _hotlistTop == _shownHotlistTop)
        {
            // Move the selection pointer
            DrawHotlistPointer(_shownHotlistIndex, false);
            DrawHotlistPointer(_hotlistIndex, true);
            _shownHotlistIndex = _hotlistIndex;
            DrawHotlistStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsHotlist, string.Empty, 0);
        Menu.WriteNameInTopBar(false);
        Tools.SetCursorEx(1, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(Tools.TruncateString(DirectoryHotlist.Items.Count == 0
            ? DialogText.HotlistEmpty
            : Lang.F("Directories to go back to: B in the file browser adds one (at most {0})",
                DirectoryHotlist.MaxCount), Init.LineWidth - 2, true));
        Tools.SetCursorEx(2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));

        // The names in a column, followed by the paths
        var nameWidth = 0;
        for (var row = 0; row < HistoryPageSize && _hotlistTop + row < _hotlistShown.Count; row++)
        {
            nameWidth = Math.Max(nameWidth,
                TextMetrics.WidthOf(HotlistName(DirectoryHotlist.Items[_hotlistShown[_hotlistTop + row]])));
        }

        nameWidth = Math.Min(nameWidth, Init.LineWidth / 2 - 8);
        for (var row = 0; row < HistoryPageSize && _hotlistTop + row < _hotlistShown.Count; row++)
        {
            var index = _hotlistTop + row;
            var path = DirectoryHotlist.Items[_hotlistShown[index]];
            var exists = Directory.Exists(path);
            DrawHotlistPointer(index, index == _hotlistIndex);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + Tools.TruncateString(HotlistName(path), nameWidth + 1, true));

            // The path (the end of a long path is shown)
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var column = 3 + nameWidth + 4;
            var missing = exists ? string.Empty : DialogText.FavoriteMissing;
            var room = Init.LineWidth - column - TextMetrics.WidthOf(missing) - 3;
            if (room > 8)
            {
                var shownPath = path.Length > room ? "..." + path[^(room - 3)..] : path;
                Tools.SetCursorEx(ListTop + row, column);
                Console.Write(shownPath);
            }

            if (!exists)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Tools.SetCursorEx(ListTop + row, Init.LineWidth - TextMetrics.WidthOf(missing) - 1);
                Console.Write(missing);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        _shownHotlistTop = _hotlistTop;
        _shownHotlistIndex = _hotlistIndex;
        DrawHotlistStatus();
    }

    private static void DrawHotlistPointer(int index, bool selected)
    {
        var row = index - _hotlistTop;
        if (row < 0 || row >= HistoryPageSize || index >= _hotlistShown.Count) return;
        Tools.SetCursorEx(ListTop + row, 0);
        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.SelectorInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(selected ? (_hotlistMoving ? "<>" : "->") : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected directory, and a message or the keys
    private static void DrawHotlistStatus()
    {
        Tools.SetCursorEx(Init.LineCount - 2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        var status = " " + (_hotlistShown.Count == 0 ? 0 : _hotlistIndex + 1) + "/" + _hotlistShown.Count + " ";
        if (_hotlistFilter.Length > 0) status += "/" + _hotlistFilter + " ";
        Tools.SetCursorEx(Init.LineCount - 2, Init.LineWidth - status.Length - 2);
        Console.Write(status);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_hotlistMessage.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_hotlistMessage, Init.LineWidth - 4, true) + " ");
        }
        else
        {
            Menu.WriteKeyHints(DialogText.HotlistKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
