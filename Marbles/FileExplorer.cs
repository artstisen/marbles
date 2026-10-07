namespace Marbles;

/// <summary>
/// The file browser opened from the options of the login page. It lists the directories and files of a
/// directory, and opens text files in the editor: for editing single files outside the encrypted data files.
/// Selecting a Marbles data file returns to the login page with the file selected.
/// </summary>
internal static class FileExplorer
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

    /// <summary>
    /// Show the file browser. Returns when it is closed, or when a data file was selected
    /// </summary>
    internal static void Run()
    {
        _filter = string.Empty;
        _message = string.Empty;
        ChangeDirectory(Directory.Exists(StartDirectory) ? StartDirectory : DataDirectory(), null);
        Draw();
        while (true)
        {
            var key = Console.ReadKey(true);
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
                    ShowHelp();
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
                    CommandLine.ShowUnknown(command);
                }
            }
            else if (key.KeyChar == '?')
            {
                ShowHelp();
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
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _index = 0;
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
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _index--;
            }
            else if (key.Key == ConsoleKey.PageDown || Keyboard.IsControl(key, ConsoleKey.D) ||
                     Keyboard.IsControl(key, ConsoleKey.F))
            {
                _index += PageSize;
            }
            else if (key.Key == ConsoleKey.PageUp || Keyboard.IsControl(key, ConsoleKey.U) ||
                     Keyboard.IsControl(key, ConsoleKey.B))
            {
                _index -= PageSize;
            }
            else if (key.Key is ConsoleKey.Home)
            {
                _index = 0;
            }
            else if (key.Key is ConsoleKey.End)
            {
                _index = _items.Count - 1;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.Backspace)
            {
                GoToParent();
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.Enter)
            {
                if (_items.Count > 0 && Open(_items[_index]))
                {
                    // A data file was selected: continue on the login page
                    break;
                }
            }
            else if (key.Key == ConsoleKey.N)
            {
                NewFile();
            }
            else if (key.Key == ConsoleKey.R)
            {
                LoadItems(_items.Count > 0 ? _items[_index].Name : null);
            }

            Draw();
        }

        // Remember the directory for next time
        if (_directory.Length > 0) StartDirectory = _directory;
        FileIo.SaveSettings();
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

                foreach (var file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (IsShown(file)) items.Add(new Item(file.Name, file.FullName, false, file.Length));
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

        if (item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase))
        {
            return SelectDataFile(item);
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

    private static void NewFile()
    {
        if (_directory.Length == 0) return;
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.NewFile, DialogText.EnterFileNameAndPressEnter + " " + DialogText.PressEscToExitP,
            1);
        Tools.CursorDown(3);
        Console.Write(" " + DialogText.NameColon);
        var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, 56, 1, string.Empty, 0, false, false,
            false, 0);
        var name = textArea.GetText().Trim();
        if (textArea.Canceled || name.Length == 0) return;
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

    private static void Draw()
    {
        // Keep the selection within the list and the view
        _index = Math.Clamp(_index, 0, Math.Max(0, _items.Count - 1));
        if (_index < _top) _top = _index;
        if (_index >= _top + PageSize) _top = _index - PageSize + 1;

        Tools.HideCursor();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsFiles, string.Empty, 0);
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
            var selected = _top + row == _index;
            Tools.SetCursorEx(ListTop + row, 0);
            if (selected)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write("->");
            }
            else
            {
                Console.Write("  ");
            }

            var isDataFile = !item.IsDirectory &&
                             item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase);
            var info = item.IsDirectory ? DialogText.DirectoryMark :
                isDataFile ? DialogText.DataFileMark + " " + FileSizeFormatter.FormatSize(item.Size) :
                FileSizeFormatter.FormatSize(item.Size);
            var name = Tools.TruncateString(item.Name + (item.IsDirectory && item.Name != ParentName ? "/" : ""),
                Init.LineWidth - info.Length - 6, true);
            ConsoleColors.SetColors(item.IsDirectory ? ConsoleColors.ColorPreset.HeaderColor :
                isDataFile ? ConsoleColors.ColorPreset.Favorites : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + name);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(ListTop + row, Init.LineWidth - info.Length - 1);
            Console.Write(info);
        }

        // Status and keys
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
            Console.Write(" " + DialogText.FileBrowserKeys);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void ShowHelp()
    {
        Tools.ClearScreen();
        var shortcuts = new List<Shortcut>
        {
            new(string.Empty, DialogText.CaptionsFiles, true),
            new("j k", "Select (as Down/Up)", false),
            new("gg G", "First/Last", false),
            new("PgUp PgDn", "One page up/down", false),
            new("l Enter", "Open the directory or file", false),
            new("h Backspace", "Go to the directory above", false),
            new("/text", "Only show names containing \"text\" (Esc: show all)", false),
            new("N", "Create a new file and open it", false),
            new("R", "Refresh", false),
            new(":cd path", "Go to a directory", false),
            new(":home", "Go to the Marbles data directory", false),
            new("Esc Q :q", "Close the file browser", false)
        };
        foreach (var shortcut in shortcuts)
        {
            shortcut.WriteShortcut(true, true);
        }

        Console.WriteLine();
        Console.WriteLine(" Text files are opened in the editor and saved as UTF-8, with their line");
        Console.WriteLine(" endings kept. They are NOT encrypted. A file that is not UTF-8 can be");
        Console.WriteLine(" converted when it is opened.");
        Console.WriteLine(" Opening a data file (" + FileIo.GetFileNameExtension() + ") selects it on the login page.");
        Tools.CursorDown(1);
        Tools.CursorRight(1);
        Keyboard.AwaitAnyKey();
    }

    #endregion
}
