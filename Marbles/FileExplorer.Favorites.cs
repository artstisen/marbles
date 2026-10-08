namespace Marbles;

// The favorites of the file browser, shown as a list: the files and directories the user has marked, in the
// order and with the colors set by the user, and grouped in folders. The list itself is kept by the class
// FileFavorites.
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
        var hidden = false; // In a folder that is closed
        for (var i = 0; i < FileFavorites.Items.Count; i++)
        {
            var item = FileFavorites.Items[i];
            if (_favoriteFilter.Length > 0)
            {
                // The favorites found are shown without the folders
                if (!item.IsFolder && item.Path.Contains(_favoriteFilter, StringComparison.OrdinalIgnoreCase))
                {
                    _favorites.Add(i);
                }

                continue;
            }

            if (item.IsFolder)
            {
                hidden = item.Collapsed;
                _favorites.Add(i);
            }
            else if (!item.InFolder || !hidden)
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
            // Follow the size of the terminal window, and draw again if the page could not be drawn
            if (Init.ApplyAutoSize()) redraw = true;
            DrawFavorites(redraw);
            if (Tools.TakeRedrawRequest()) DrawFavorites(true);
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
                    var moved = FileFavorites.Items[selected];
                    if (FileFavorites.Move(selected, up))
                    {
                        // The selection follows it (a folder it has moved into is opened)
                        FilterFavorites();
                        _favoriteIndex = Math.Max(0,
                            _favorites.FindIndex(number => ReferenceEquals(FileFavorites.Items[number], moved)));
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
                // A name can follow the command
                if (command != null && CommandLine.Name(command) is "folder" or "rename")
                {
                    command = CommandLine.Name(command);
                }

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
                        Tools.WriteEditorHelp(false, Tools.HelpTopic.Favorites);
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
                    case "folder":
                        NewFavoriteFolder(selected, CommandLine.LastArgument);
                        break;
                    case "rename":
                        RenameFavoriteFolder(selected, CommandLine.LastArgument);
                        break;
                    case "goto":
                        if (selected < 0 || FileFavorites.Items[selected].IsFolder) break;
                        path = FileFavorites.Items[selected].Path;
                        return FavoriteAction.GoTo;
                    default:
                        CommandLine.ShowUnknown(command);
                        break;
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Favorites);
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
            else if (key.Key == ConsoleKey.A)
            {
                NewFavoriteFolder(selected, string.Empty);
            }
            else if (selected < 0)
            {
                // The other keys need a favorite to work on
            }
            else if (key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow)
            {
                if (FileFavorites.Items[selected].IsFolder)
                {
                    // Open or close the folder
                    FileFavorites.Items[selected].Collapsed = !FileFavorites.Items[selected].Collapsed;
                    SaveFavoriteList();
                    continue;
                }

                path = FileFavorites.Items[selected].Path;
                return FavoriteAction.Open;
            }
            else if (key.Key == ConsoleKey.X)
            {
                RenameFavoriteFolder(selected, string.Empty);
            }
            else if (key.Key == ConsoleKey.B)
            {
                if (FileFavorites.Items[selected].IsFolder) continue;
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

    // Ask for the name of a new folder (if it is not given) and add it in front of the selected item
    private static void NewFavoriteFolder(int selected, string name)
    {
        if (_favoriteFilter.Length > 0)
        {
            _favoriteMessage = DialogText.FavoriteMoveFiltered;
            return;
        }

        if (name.Length == 0)
        {
            var asked = AskName(DialogText.FavoriteNewFolder, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var index = FileFavorites.AddFolder(name, selected);
        if (index < 0)
        {
            _favoriteMessage = DialogText.FavoriteFolderName;
            return;
        }

        FilterFavorites();
        _favoriteIndex = Math.Max(0, _favorites.IndexOf(index));
        SaveFavoriteList();
    }

    private static void RenameFavoriteFolder(int number, string name)
    {
        if (number < 0) return;
        if (!FileFavorites.Items[number].IsFolder)
        {
            // A file is renamed where it is
            _favoriteMessage = DialogText.FavoriteRenameFile;
            return;
        }

        if (name.Length == 0)
        {
            var asked = AskName(DialogText.FavoriteRenameFolder, FileFavorites.Items[number].Name);
            if (asked == null) return;
            name = asked;
        }

        if (FileFavorites.RenameFolder(number, name))
        {
            SaveFavoriteList();
        }
        else
        {
            _favoriteMessage = DialogText.FavoriteFolderName;
        }
    }

    private static void RemoveFavorite(int number)
    {
        if (number < 0) return;
        var item = FileFavorites.Items[number];
        if (item.IsFolder)
        {
            // The favorites in a folder are removed with it
            var count = FileFavorites.FolderCount(number);
            if (count > 0 && !Menu.YesNoPrompt(DialogText.FavoriteDeleteFolder,
                    DialogText.DeleteNamedYn.Replace("{0}", Tools.TruncateString(item.Name, 40, true)),
                    (count == 1 ? DialogText.FavoriteDeleteFolderNoteOne : DialogText.FavoriteDeleteFolderNote)
                    .Replace("{0}", count.ToString()), true))
            {
                return;
            }

            FileFavorites.RemoveAt(number);
            _favoriteMessage = DialogText.FavoriteFolderRemoved + item.Name;
            SaveFavoriteList();
            return;
        }

        var name = System.IO.Path.GetFileName(FileFavorites.Items[number].Path);
        FileFavorites.RemoveAt(number);
        _favoriteMessage = DialogText.FavoriteRemoved + ": " + name;
        SaveFavoriteList();
    }

    // Show information about the file or directory of a favorite
    private static void ShowFavoriteInfo(int number)
    {
        if (number < 0 || FileFavorites.Items[number].IsFolder) return;
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
            nameWidth = Math.Max(nameWidth, FavoriteLabel(number).Length);
        }

        nameWidth = Math.Min(nameWidth, Init.LineWidth / 2 - 6);
        for (var row = 0; row < FavoritePageSize && _favoriteTop + row < _favorites.Count; row++)
        {
            var index = _favoriteTop + row;
            var favorite = FileFavorites.Items[_favorites[index]];
            var isDirectory = !favorite.IsFolder && Directory.Exists(favorite.Path);
            var exists = favorite.IsFolder || isDirectory || File.Exists(favorite.Path);
            var count = favorite.IsFolder ? FileFavorites.FolderCount(_favorites[index]) : 0;
            var info = favorite.IsFolder ? count + (count == 1 ? DialogText.FavoriteFolderOne : DialogText.FavoriteFolderCount) :
                !exists ? DialogText.FavoriteMissing :
                isDirectory ? DialogText.DirectoryMark : FileSizeFormatter.FormatSize(new FileInfo(favorite.Path).Length);
            DrawFavoritePointer(index, index == _favoriteIndex);
            var name = Tools.TruncateString(FavoriteLabel(_favorites[index]) + (isDirectory ? "/" : ""),
                nameWidth + 1, true);
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
                ConsoleColors.SetColors(isDirectory || favorite.IsFolder
                    ? ConsoleColors.ColorPreset.HeaderColor
                    : ConsoleColors.ColorPreset.Normal);
            }

            Console.Write(" " + name);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            // The directory it is in (the end of a long path is shown)
            var directory = favorite.IsFolder
                ? string.Empty
                : System.IO.Path.GetDirectoryName(favorite.Path) ?? string.Empty;
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

    // What is shown for an item of the list: the name of the file, with a blank space in front if it is in a
    // folder, or the name of a folder with a mark that tells if it is open
    private static string FavoriteLabel(int number)
    {
        var item = FileFavorites.Items[number];
        if (item.IsFolder) return (item.Collapsed ? "[+] " : "[-] ") + item.Name;
        // The folders are not shown when the list is filtered
        return (item.InFolder && _favoriteFilter.Length == 0 ? " " : string.Empty) + FavoriteName(item.Path);
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
            Menu.WriteKeyHints(DialogText.FavoritesKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
