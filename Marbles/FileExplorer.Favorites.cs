namespace Marbles;

// The favorites of the file browser, shown as a list: the files and directories the user has marked, in the
// order and with the colors set by the user. The list itself is kept by the class FileFavorites.
internal static partial class FileExplorer
{
    private enum FavoriteAction
    {
        Close,
        Open, // Open the file (or go to the directory)
        GoTo // Show where the file is, in the browser
    }

    private const int FavoritePickerLeft = 28;

    private static List<int> _favorites = new(); // The favorites shown (their numbers in the whole list)
    private static int _favoriteIndex;
    private static int _favoriteTop;
    private static string _favoriteFilter = string.Empty;
    private static string _favoriteMessage = string.Empty;
    private static bool _favoriteMoving; // The selected favorite follows j/k up and down the list

    // Show the favorites until the list is closed. Opening a file returns to the list afterwards
    private static KeyResult BrowseFavorites()
    {
        _favoriteFilter = string.Empty;
        _favoriteMessage = string.Empty;
        _favoriteMoving = false;
        while (true)
        {
            var action = ShowFavorites(out var path);
            if (action == FavoriteAction.Close) return KeyResult.Handled;
            var isDirectory = Directory.Exists(path);
            if (!isDirectory && !File.Exists(path))
            {
                _favoriteMessage = DialogText.FileNotFound;
                continue;
            }

            if (action == FavoriteAction.GoTo || isDirectory)
            {
                // Show the directory, or the directory of the file with the file selected
                var parent = isDirectory && action == FavoriteAction.Open
                    ? path
                    : System.IO.Path.GetDirectoryName(path);
                if (parent == null || !Directory.Exists(parent))
                {
                    _favoriteMessage = DialogText.DirectoryNotFound;
                    continue;
                }

                ChangeDirectory(parent, isDirectory && action == FavoriteAction.Open ? null : path);
                return KeyResult.Handled;
            }

            _message = string.Empty;
            if (Open(new Item(System.IO.Path.GetFileName(path), path, false, new FileInfo(path).Length)))
            {
                return KeyResult.Close;
            }

            _favoriteMessage = _message;
            _message = string.Empty;
        }
    }

    private static int FavoritePageSize => Init.LineCount - ListTop - 2;

    private static void FilterFavorites()
    {
        _favorites = new List<int>();
        for (var i = 0; i < FileFavorites.Items.Count; i++)
        {
            if (_favoriteFilter.Length == 0 ||
                FileFavorites.Items[i].Path.Contains(_favoriteFilter, StringComparison.OrdinalIgnoreCase))
            {
                _favorites.Add(i);
            }
        }
    }

    private static FavoriteAction ShowFavorites(out string path)
    {
        path = string.Empty;
        var redraw = true;
        while (true)
        {
            FilterFavorites();
            _favoriteIndex = Math.Clamp(_favoriteIndex, 0, Math.Max(0, _favorites.Count - 1));
            DrawFavorites(redraw);
            redraw = true;
            var hadMessage = _favoriteMessage.Length > 0;
            _favoriteMessage = string.Empty;
            var selected = _favorites.Count > 0 ? _favorites[_favoriteIndex] : -1;
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;

            if (_favoriteMoving)
            {
                // The selected favorite is moved until Esc is pressed
                if (navigation is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.J or ConsoleKey.K)
                {
                    var up = navigation is ConsoleKey.UpArrow or ConsoleKey.K;
                    if (FileFavorites.Move(selected, up))
                    {
                        _favoriteIndex += up ? -1 : 1;
                        SaveFavoriteList();
                    }
                }
                else if (key.Key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.Tab or ConsoleKey.V
                         or ConsoleKey.Q)
                {
                    _favoriteMoving = false;
                }

                continue;
            }

            if (key.KeyChar == ':')
            {
                var command = CommandLine.Read();
                switch (command)
                {
                    case null:
                        break;
                    case "q":
                    case "quit":
                    case "close":
                        return FavoriteAction.Close;
                    case "h":
                    case "help":
                        Tools.WriteEditorHelp(false, DialogText.HelpSectionFavorites);
                        break;
                    case "color":
                        PickFavoriteColor(selected);
                        break;
                    case "info":
                        ShowFavoriteInfo(selected);
                        break;
                    case "remove":
                        RemoveFavorite(selected);
                        break;
                    case "goto":
                        if (selected < 0) break;
                        path = FileFavorites.Items[selected].Path;
                        return FavoriteAction.GoTo;
                    default:
                        CommandLine.ShowUnknown(command);
                        break;
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, DialogText.HelpSectionFavorites);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the paths containing a text
                _favoriteFilter = CommandLine.Read('/', false) ?? string.Empty;
                _favoriteIndex = 0;
            }
            else if (key.KeyChar == 'G')
            {
                _favoriteIndex = _favorites.Count - 1;
                redraw = hadMessage;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _favoriteIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q or ConsoleKey.F ||
                     navigation == ConsoleKey.LeftArrow)
            {
                // Esc removes a filter before it closes the list
                if (key.Key == ConsoleKey.Escape && _favoriteFilter.Length > 0)
                {
                    _favoriteFilter = string.Empty;
                }
                else
                {
                    return FavoriteAction.Close;
                }
            }
            else if (navigation == ConsoleKey.DownArrow)
            {
                _favoriteIndex++;
                redraw = hadMessage;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _favoriteIndex--;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageDown)
            {
                _favoriteIndex += FavoritePageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.PageUp)
            {
                _favoriteIndex -= FavoritePageSize;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                _favoriteIndex = 0;
                redraw = hadMessage;
            }
            else if (key.Key == ConsoleKey.End)
            {
                _favoriteIndex = _favorites.Count - 1;
                redraw = hadMessage;
            }
            else if (selected < 0)
            {
                // The other keys need a favorite to work on
            }
            else if (key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow)
            {
                path = FileFavorites.Items[selected].Path;
                return FavoriteAction.Open;
            }
            else if (key.Key == ConsoleKey.B)
            {
                path = FileFavorites.Items[selected].Path;
                return FavoriteAction.GoTo;
            }
            else if (key.Key == ConsoleKey.Tab || key.KeyChar == 'V')
            {
                if (_favoriteFilter.Length > 0)
                {
                    // The order is that of the whole list
                    _favoriteMessage = DialogText.FavoriteMoveFiltered;
                }
                else
                {
                    _favoriteMoving = true;
                }
            }
            else if (key.Key is ConsoleKey.P or ConsoleKey.D)
            {
                RemoveFavorite(selected);
            }
            else if (key.Key == ConsoleKey.C)
            {
                PickFavoriteColor(selected);
            }
            else if (key.Key == ConsoleKey.I)
            {
                ShowFavoriteInfo(selected);
            }
        }
    }

    private static void SaveFavoriteList()
    {
        var result = FileFavorites.Save();
        if (result != DialogText.Success) _favoriteMessage = result;
    }

    private static void RemoveFavorite(int number)
    {
        if (number < 0) return;
        var name = System.IO.Path.GetFileName(FileFavorites.Items[number].Path);
        FileFavorites.RemoveAt(number);
        _favoriteMessage = DialogText.FavoriteRemoved + ": " + name;
        SaveFavoriteList();
    }

    // Show information about the file or directory of a favorite
    private static void ShowFavoriteInfo(int number)
    {
        if (number < 0) return;
        var path = FileFavorites.Items[number].Path;
        var isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            _favoriteMessage = DialogText.FileNotFound;
            return;
        }

        _message = string.Empty;
        ShowInfo(new Item(FavoriteName(path), path, isDirectory, isDirectory ? 0 : new FileInfo(path).Length));
        _favoriteMessage = _message;
        _message = string.Empty;
    }

    private static void PickFavoriteColor(int number)
    {
        if (number < 0) return;
        if (ColorPicker.Pick(DialogText.ColorCaption, FavoritePickerLeft, true, FileFavorites.Items[number].Color,
                null, null, out var color))
        {
            FileFavorites.SetColor(number, color);
            SaveFavoriteList();
        }
    }

    private static int _shownFavoriteTop = -1;
    private static int _shownFavoriteIndex = -1;

    private static void DrawFavorites(bool all)
    {
        if (_favoriteIndex < _favoriteTop) _favoriteTop = _favoriteIndex;
        if (_favoriteIndex >= _favoriteTop + FavoritePageSize) _favoriteTop = _favoriteIndex - FavoritePageSize + 1;
        _favoriteTop = Math.Max(0, _favoriteTop);

        Tools.HideCursor();
        if (!all && _favoriteTop == _shownFavoriteTop)
        {
            // Move the selection pointer
            DrawFavoritePointer(_shownFavoriteIndex, false);
            DrawFavoritePointer(_favoriteIndex, true);
            _shownFavoriteIndex = _favoriteIndex;
            DrawFavoriteStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsFileFavorites, string.Empty, 0);
        Menu.WriteNameInTopBar(false);
        Tools.SetCursorEx(1, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(_favorites.Count == 0 && _favoriteFilter.Length == 0
            ? DialogText.FavoritesEmpty
            : DialogText.FavoritesHeader);
        Tools.SetCursorEx(2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));

        // The names in a column, followed by the directories
        var nameWidth = 0;
        foreach (var number in _favorites)
        {
            nameWidth = Math.Max(nameWidth, FavoriteName(FileFavorites.Items[number].Path).Length);
        }

        nameWidth = Math.Min(nameWidth, Init.LineWidth / 2 - 6);
        for (var row = 0; row < FavoritePageSize && _favoriteTop + row < _favorites.Count; row++)
        {
            var index = _favoriteTop + row;
            var favorite = FileFavorites.Items[_favorites[index]];
            var isDirectory = Directory.Exists(favorite.Path);
            var exists = isDirectory || File.Exists(favorite.Path);
            var info = !exists ? DialogText.FavoriteMissing :
                isDirectory ? DialogText.DirectoryMark : FileSizeFormatter.FormatSize(new FileInfo(favorite.Path).Length);
            DrawFavoritePointer(index, index == _favoriteIndex);
            var name = Tools.TruncateString(FavoriteName(favorite.Path) + (isDirectory ? "/" : ""), nameWidth + 1,
                true);
            var moving = _favoriteMoving && index == _favoriteIndex;
            if (moving)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            }
            else if (favorite.Color != null)
            {
                ConsoleColors.SetTextColor(favorite.Color.Value);
            }
            else
            {
                ConsoleColors.SetColors(isDirectory
                    ? ConsoleColors.ColorPreset.HeaderColor
                    : ConsoleColors.ColorPreset.Normal);
            }

            Console.Write(" " + name);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            // The directory it is in (the end of a long path is shown)
            var directory = System.IO.Path.GetDirectoryName(favorite.Path) ?? string.Empty;
            var column = 3 + nameWidth + 3;
            var room = Init.LineWidth - column - info.Length - 3;
            if (room > 8)
            {
                if (directory.Length > room) directory = "..." + directory[^(room - 3)..];
                Tools.SetCursorEx(ListTop + row, column);
                Console.Write(directory);
            }

            if (!exists) ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Tools.SetCursorEx(ListTop + row, Init.LineWidth - info.Length - 1);
            Console.Write(info);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        _shownFavoriteTop = _favoriteTop;
        _shownFavoriteIndex = _favoriteIndex;
        DrawFavoriteStatus();
    }

    private static string FavoriteName(string path)
    {
        var name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        return name.Length > 0 ? name : path;
    }

    private static void DrawFavoritePointer(int index, bool selected)
    {
        var row = index - _favoriteTop;
        if (row < 0 || row >= FavoritePageSize || index >= _favorites.Count) return;
        Tools.SetCursorEx(ListTop + row, 0);
        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.HighlightInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected favorite, and a message or the keys
    private static void DrawFavoriteStatus()
    {
        Tools.SetCursorEx(Init.LineCount - 2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        var status = " " + (_favorites.Count == 0 ? 0 : _favoriteIndex + 1) + "/" + _favorites.Count + " ";
        if (_favoriteFilter.Length > 0) status += "/" + _favoriteFilter + " ";
        Tools.SetCursorEx(Init.LineCount - 2, Init.LineWidth - status.Length - 2);
        Console.Write(status);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_favoriteMessage.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_favoriteMessage, Init.LineWidth - 4, true) + " ");
        }
        else if (_favoriteMoving)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(" " + DialogText.MovePinnedKeys + " ");
        }
        else
        {
            Console.Write(" " + DialogText.FavoritesKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
