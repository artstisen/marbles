namespace Marbles;

/// <summary>
/// The file browser opened from the options of the login page. It lists the directories and files of a
/// directory, and opens text files in the editor: for editing single files outside the encrypted data files.
/// Selecting a Marbles data file returns to the login page with the file selected.
/// File management is found in FileExplorer.Manage.cs and the list of favorites in FileExplorer.Favorites.cs.
/// </summary>
internal static partial class FileExplorer
{
    private const int ListTop = 3; // The first row of the list
    private const string ParentName = "..";
    private const string CopyNameSuffix = "-utf8";

    private sealed record Item(string Name, string Path, bool IsDirectory, long Size);

    /// <summary>
    /// The directory shown when the browser is opened (setting "browsedir"). Empty = the data directory
    /// </summary>
    internal static string StartDirectory { get; set; } = string.Empty;

    private static string _directory = string.Empty; // Empty = the list of drives (Windows)
    private static List<Item> _items = new();
    private static int _index;
    private static int _top;
    private static string _filter = string.Empty;
    private static string _message = string.Empty;
    private static TextFile? _file; // The file being edited

    private static int PageSize => Init.LineCount - ListTop - 2;

    private enum BrowseMode
    {
        Edit, // Open text files in the editor
        PickFile, // Select a file for the caller
        PickDirectory // Select a directory for the caller (files are not shown)
    }

    private static BrowseMode _mode;
    private static string _caption = string.Empty;
    private static string? _picked;

    /// <summary>
    /// Show the file browser. Returns when it is closed, or when a data file was selected
    /// </summary>
    internal static void Run()
    {
        Browse(BrowseMode.Edit, DialogText.CaptionsFiles);
    }

    /// <summary>
    /// Let the user select a file
    /// </summary>
    /// <returns>The path of the file, or null if canceled</returns>
    internal static string? PickFile(string caption)
    {
        return Browse(BrowseMode.PickFile, caption);
    }

    /// <summary>
    /// Let the user select a directory
    /// </summary>
    /// <returns>The path of the directory, or null if canceled</returns>
    internal static string? PickDirectory(string caption)
    {
        return Browse(BrowseMode.PickDirectory, caption);
    }

    /// <summary>
    /// True if the last run of the file browser logged out of the data file (a file was opened)
    /// </summary>
    internal static bool SessionEnded { get; private set; }

    /// <summary>
    /// True if the last run of the file browser was closed by selecting a data file (to log in to)
    /// </summary>
    internal static bool DataFileSelected { get; private set; }

    // Opening a file from the browser ends the session of the data file that is logged in to (if any),
    // just like logging out before opening the browser from the login page
    private static void EndSession()
    {
        if (Mode.CurrentMode == Mode.SelectedMode.Login) return;
        Tools.RemovePrivileges(false);
        Mode.SetMode(Mode.SelectedMode.Login);
        SessionEnded = true;
    }

    private static string? Browse(BrowseMode mode, string caption)
    {
        _mode = mode;
        _caption = caption;
        _picked = null;
        SessionEnded = false;
        DataFileSelected = false;
        _filter = string.Empty;
        _message = string.Empty;
        if (mode == BrowseMode.Edit) FileFavorites.Load();
        ChangeDirectory(Directory.Exists(StartDirectory) ? StartDirectory : DataDirectory(), null);
        Draw(true);
        while (true)
        {
            var key = Console.ReadKey(true);
            // Moving the selection only redraws the selection pointer (unless the list has to scroll)
            var redraw = true;
            var hadMessage = _message.Length > 0;
            _message = string.Empty;
            if (key.KeyChar == ':')
            {
                // The command is read as typed, as paths can be case sensitive
                var command = CommandLine.Read(':', false);
                var name = command == null ? string.Empty : CommandLine.Name(command).ToLowerInvariant();
                if (name is "q" or "quit" or "close")
                {
                    break;
                }

                if (name is "home")
                {
                    ChangeDirectory(DataDirectory(), null);
                }
                else if (name is "h" or "help")
                {
                    Tools.WriteEditorHelp(false, DialogText.HelpSectionFiles);
                }
                else if (name == "cd" && command != null)
                {
                    // Go to a directory by path (a relative path starts from the directory shown)
                    var path = CommandLine.Argument(command);
                    if (path.Length > 0 && _directory.Length > 0)
                    {
                        path = System.IO.Path.GetFullPath(path, _directory);
                    }

                    if (Directory.Exists(path))
                    {
                        ChangeDirectory(System.IO.Path.GetFullPath(path), null);
                    }
                    else
                    {
                        _message = DialogText.DirectoryNotFound;
                    }
                }
                else if (command != null)
                {
                    var result = _mode == BrowseMode.Edit
                        ? RunFileCommand(name, CommandLine.Argument(command))
                        : KeyResult.NotHandled;
                    if (result == KeyResult.Close) break;
                    if (result == KeyResult.NotHandled) CommandLine.ShowUnknown(command);
                }
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, DialogText.HelpSectionFiles);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the names containing a text
                _filter = CommandLine.Read('/', false) ?? string.Empty;
                LoadItems(null);
            }
            else if (key.KeyChar == 'G')
            {
                _index = _items.Count - 1;
                redraw = false;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _index = 0;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Esc removes a filter before it closes the browser
                if (key.Key == ConsoleKey.Escape && _filter.Length > 0)
                {
                    _filter = string.Empty;
                    LoadItems(null);
                }
                else
                {
                    break;
                }
            }
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                _index++;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _index--;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.PageDown || Keyboard.IsControl(key, ConsoleKey.D) ||
                     Keyboard.IsControl(key, ConsoleKey.F))
            {
                _index += PageSize;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.PageUp || Keyboard.IsControl(key, ConsoleKey.U) ||
                     Keyboard.IsControl(key, ConsoleKey.B))
            {
                _index -= PageSize;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.Home)
            {
                _index = 0;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.End)
            {
                _index = _items.Count - 1;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.Backspace || key.KeyChar == '-')
            {
                GoToParent();
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.Enter)
            {
                if (_items.Count > 0 && Open(_items[_index]))
                {
                    // A file was picked, or a data file was selected: continue on the login page
                    break;
                }
            }
            else if (_mode == BrowseMode.PickDirectory && key.Key is ConsoleKey.S or ConsoleKey.Spacebar)
            {
                // Select the directory shown
                if (_directory.Length > 0)
                {
                    _picked = _directory;
                    break;
                }
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.N)
            {
                NewFile(string.Empty);
            }
            else if (key.Key == ConsoleKey.R)
            {
                LoadItems(_items.Count > 0 ? _items[_index].Name : null);
            }
            else if (_mode == BrowseMode.Edit && HandleFileKey(key) == KeyResult.Close)
            {
                // A data file was selected from the favorites
                break;
            }

            Draw(redraw || hadMessage);
        }

        // Remember the directory for next time
        if (_directory.Length > 0) StartDirectory = _directory;
        FileIo.SaveSettings();
        return _picked;
    }

    private static string DataDirectory()
    {
        return System.IO.Path.GetFullPath(FileIo.GetDataAndSubFolder());
    }

    #region Directories

    private static void ChangeDirectory(string directory, string? select)
    {
        var previous = _directory;
        _directory = directory;
        _filter = string.Empty;
        if (!LoadItems(select))
        {
            // Stay where we were if the directory cannot be read
            _directory = previous;
            var message = _message;
            LoadItems(null);
            _message = message;
        }
    }

    private static void GoToParent()
    {
        if (_directory.Length == 0) return;
        var parent = Directory.GetParent(_directory)?.FullName;
        var name = System.IO.Path.GetFileName(_directory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (parent != null)
        {
            ChangeDirectory(parent, name);
        }
        else if (OperatingSystem.IsWindows())
        {
            // Above the root of a drive is the list of drives
            ChangeDirectory(string.Empty, _directory);
        }
    }

    // Read the content of the current directory: directories first, then files, both sorted by name
    private static bool LoadItems(string? select)
    {
        var items = new List<Item>();
        try
        {
            if (_directory.Length == 0)
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady) items.Add(new Item(drive.Name, drive.Name, true, 0));
                }
            }
            else
            {
                items.Add(new Item(ParentName, string.Empty, true, 0));
                var info = new DirectoryInfo(_directory);
                foreach (var directory in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (IsShown(directory)) items.Add(new Item(directory.Name, directory.FullName, true, 0));
                }

                if (_mode != BrowseMode.PickDirectory)
                {
                    foreach (var file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (IsShown(file)) items.Add(new Item(file.Name, file.FullName, false, file.Length));
                    }
                }
            }
        }
        catch (Exception e)
        {
            _message = e.Message;
            return false;
        }

        _items = items;
        _index = Math.Max(0, select == null ? 0 : items.FindIndex(i => i.Name == select || i.Path == select));
        _top = 0;
        return true;
    }

    private static bool IsShown(FileSystemInfo info)
    {
        if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        return _filter.Length == 0 || info.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Opening files

    /// <returns>True if a data file was selected and the browser must close</returns>
    private static bool Open(Item item)
    {
        if (item.IsDirectory)
        {
            if (item.Name == ParentName)
            {
                GoToParent();
            }
            else
            {
                ChangeDirectory(item.Path, null);
            }

            return false;
        }

        if (_mode == BrowseMode.PickFile)
        {
            _picked = item.Path;
            return true;
        }

        if (item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase))
        {
            DataFileSelected = SelectDataFile(item);
            return DataFileSelected;
        }

        TextFile file;
        try
        {
            file = TextFile.Load(item.Path);
        }
        catch (Exception e)
        {
            _message = e.Message;
            return false;
        }

        var converted = false;
        if (!file.IsUtf8)
        {
            // Ask how to convert the file
            var selection = Menu.SelectionPrompt(DialogText.CaptionsFiles, DialogText.ConvertReplace,
                DialogText.ConvertCopy, DialogText.ConvertCancel,
                DialogText.NotUtf8 + file.EncodingName + ". " + DialogText.NotUtf8Note, false, ConsoleKey.A,
                ConsoleKey.B, ConsoleKey.Escape);
            if (selection == 1)
            {
                file.Path = GetCopyPath(item.Path);
            }
            else if (selection != 0)
            {
                return false;
            }

            converted = true;
        }

        Edit(file, converted);
        LoadItems(System.IO.Path.GetFileName(file.Path));
        return false;
    }

    // The path for the UTF-8 copy of a file: "name-utf8.txt" (numbered if it exists)
    private static string GetCopyPath(string path)
    {
        var directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        var name = System.IO.Path.GetFileNameWithoutExtension(path) + CopyNameSuffix;
        var extension = System.IO.Path.GetExtension(path);
        var copy = System.IO.Path.Combine(directory, name + extension);
        for (var number = 2; File.Exists(copy); number++)
        {
            copy = System.IO.Path.Combine(directory, name + number + extension);
        }

        return copy;
    }

    // Open a text file in the editor
    private static void Edit(TextFile file, bool modified)
    {
        EndSession();
        _file = file;
        Mode.SetMode(Mode.SelectedMode.Edit);
        try
        {
            Tools.ClearScreen();
            Actions.DrawEditFileMenu();
            var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                Init.LineCount - 5, file.Text, 0, false, false, false, TextArea.SaveAsFile, modified);
            textArea.ClearUndoRedoCache();
        }
        finally
        {
            Mode.SetMode(Mode.SelectedMode.Login);
            _file = null;
        }
    }

    /// <summary>
    /// The name of the file being edited (for the title of the editor)
    /// </summary>
    internal static string CurrentFileName => _file == null ? string.Empty : System.IO.Path.GetFileName(_file.Path);

    /// <summary>
    /// Save the text of the file being edited (called by the editor)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string SaveCurrentFile(string text)
    {
        return _file == null ? DialogText.StatusUnableToSave : _file.Save(text);
    }

    // Create a file in the directory shown and open it in the editor. The name is asked for if it is not given
    private static void NewFile(string name)
    {
        if (_directory.Length == 0) return;
        name = name.Trim();
        if (name.Length == 0)
        {
            var asked = AskName(DialogText.NewFile, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var path = System.IO.Path.Combine(_directory, name);
        if (!Tools.IsValidFileName(name) || File.Exists(path) || Directory.Exists(path))
        {
            _message = DialogText.InvalidFileName;
            return;
        }

        try
        {
            File.WriteAllBytes(path, Array.Empty<byte>());
        }
        catch (Exception e)
        {
            _message = e.Message;
            return;
        }

        if (name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase))
        {
            // Not a text file: data files are created from the options of the login page
            LoadItems(name);
            return;
        }

        Edit(TextFile.Create(path), false);
        LoadItems(name);
    }

    // A Marbles data file: select it on the login page. A data file outside the data directory can be copied to it
    private static bool SelectDataFile(Item item)
    {
        var root = System.IO.Path.GetFullPath(FileIo.GetDataDirectoryName());
        var directory = System.IO.Path.GetDirectoryName(item.Path) ?? string.Empty;
        var subFolder = string.Empty;
        if (!SamePath(directory, root))
        {
            if (SamePath(System.IO.Path.GetDirectoryName(directory) ?? string.Empty, root))
            {
                // In a directory of the data directory
                subFolder = System.IO.Path.GetFileName(directory);
            }
            else
            {
                if (!Menu.YesNoPrompt(DialogText.CaptionsFiles, DialogText.CopyDataFileYn, DialogText.CopyDataFileNote,
                        false))
                {
                    return false;
                }

                var target = System.IO.Path.Combine(root, item.Name);
                if (File.Exists(target))
                {
                    _message = DialogText.ErrorFileAlreadyExist;
                    return false;
                }

                try
                {
                    File.Copy(item.Path, target, false);
                    Init.Log("Copy file: " + item.Name + " -> " + FileIo.GetDataDirectoryName(), false);
                }
                catch (Exception e)
                {
                    _message = e.Message;
                    return false;
                }
            }
        }

        // Select the file for the login page
        EndSession();
        FileIo.ResetSubFolder();
        FileIo.SetSubFolder(subFolder);
        FileIo.SetFileName(item.Name);
        FileBrowser.ResolveSubFolderIndex();
        return true;
    }

    private static bool SamePath(string a, string b)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(a.TrimEnd(System.IO.Path.DirectorySeparatorChar),
            b.TrimEnd(System.IO.Path.DirectorySeparatorChar), comparison);
    }

    #endregion

    #region Drawing

    private static int _shownTop = -1; // What is on screen: the first item shown and the item selected
    private static int _shownIndex = -1;

    /// <summary>
    /// Draw the file browser
    /// </summary>
    /// <param name="all">False if only the selection has moved: then only the selection pointer and the
    /// status are drawn (the whole screen is still drawn if the list has to scroll)</param>
    private static void Draw(bool all)
    {
        // Keep the selection within the list and the view
        _index = Math.Clamp(_index, 0, Math.Max(0, _items.Count - 1));
        if (_index < _top) _top = _index;
        if (_index >= _top + PageSize) _top = _index - PageSize + 1;

        Tools.HideCursor();
        if (!all && _top == _shownTop)
        {
            // Move the selection pointer
            DrawPointer(_shownIndex, false);
            DrawPointer(_index, true);
            _shownIndex = _index;
            DrawStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(_caption, string.Empty, 0);
        Menu.WriteNameInTopBar(false);

        // The directory (the end of a long path is shown)
        var width = Init.LineWidth - 2;
        var path = _directory.Length == 0 ? DialogText.Drives : _directory;
        if (path.Length > width) path = "..." + path[^(width - 3)..];
        Tools.SetCursorEx(1, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(path);
        Tools.SetCursorEx(2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));

        for (var row = 0; row < PageSize && _top + row < _items.Count; row++)
        {
            var item = _items[_top + row];
            DrawPointer(_top + row, _top + row == _index);
            var isDataFile = !item.IsDirectory &&
                             item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase);
            var info = item.IsDirectory ? DialogText.DirectoryMark :
                isDataFile ? DialogText.DataFileMark + " " + FileSizeFormatter.FormatSize(item.Size) :
                FileSizeFormatter.FormatSize(item.Size);
            var name = Tools.TruncateString(item.Name + (item.IsDirectory && item.Name != ParentName ? "/" : ""),
                Init.LineWidth - info.Length - 8, true);
            ConsoleColors.SetColors(item.IsDirectory ? ConsoleColors.ColorPreset.HeaderColor :
                isDataFile ? ConsoleColors.ColorPreset.Favorites : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + name);
            if (_mode == BrowseMode.Edit && item.Path.Length > 0 && FileFavorites.Contains(item.Path))
            {
                // A favorite
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
                Console.Write(" *");
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(ListTop + row, Init.LineWidth - info.Length - 1);
            Console.Write(info);
        }

        _shownTop = _top;
        _shownIndex = _index;
        DrawStatus();
    }

    // Draw (or remove) the selection pointer in front of an item, leaving the cursor after it
    private static void DrawPointer(int index, bool selected)
    {
        var row = index - _top;
        if (row < 0 || row >= PageSize || index >= _items.Count) return;
        Tools.SetCursorEx(ListTop + row, 0);
        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.HighlightInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected item, and a message or the keys
    private static void DrawStatus()
    {
        Tools.SetCursorEx(Init.LineCount - 2, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        var status = " " + (_items.Count == 0 ? 0 : _index + 1) + "/" + _items.Count + " ";
        if (_filter.Length > 0) status += "/" + _filter + " ";
        Tools.SetCursorEx(Init.LineCount - 2, Init.LineWidth - status.Length - 2);
        Console.Write(status);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_message.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_message, Init.LineWidth - 4, true) + " ");
        }
        else
        {
            Console.Write(" " + _mode switch
            {
                BrowseMode.PickFile => DialogText.FilePickKeys,
                BrowseMode.PickDirectory => DialogText.DirectoryPickKeys,
                _ => DialogText.FileBrowserKeys
            });
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
