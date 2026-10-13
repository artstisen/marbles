namespace Marbles;

/// <summary>
/// The file browser opened from the options of the login page. It lists the directories and files of a
/// directory, and opens text files in the editor: for editing single files outside the encrypted data files.
/// Selecting a Marbles data file returns to the login page with the file selected.
/// File management is found in FileExplorer.Manage.cs, the list of favorites in FileExplorer.Favorites.cs,
/// the files opened before in FileExplorer.History.cs and the search for files in FileExplorer.Search.cs.
/// </summary>
internal static partial class FileExplorer
{
    private const int ListTop = 3; // The first row of the list
    private const string ParentName = "..";
    private const string CopyNameSuffix = "-utf8";

    private sealed record Item(string Name, string Path, bool IsDirectory, long Size);

    /// <summary>
    /// The directories shown in the two panes when the browser is opened (settings "browsedir1" and
    /// "browsedir2"). Empty = the data directory
    /// </summary>
    internal static string StartDirectory { get; set; } = string.Empty;

    internal static string StartDirectory2 { get; set; } = string.Empty;

    /// <summary>
    /// The pane that has the focus when the browser is opened: 0 or 1 (setting "browsepane")
    /// </summary>
    internal static int StartPane { get; set; }

    /// <summary>
    /// True to show two panes side by side, false to show one (setting "browsedual")
    /// </summary>
    internal static bool DualPane { get; set; } = true;

    // A list of the content of a directory
    private sealed class Pane
    {
        internal string Directory = string.Empty; // Empty = the list of drives (Windows)
        internal List<Item> Items = new();
        internal int Index; // The selected item
        internal int Top; // The first item shown
        internal string Filter = string.Empty;
        internal int ShownTop = -1; // What is on screen: the first item shown and the item selected
        internal int ShownIndex = -1;
        internal readonly HashSet<string> Marks = new(); // The paths of the marked files and directories
        internal string MarksDirectory = string.Empty; // The directory the marks were made in
    }

    // The two panes of the browser, and the one used when a file or directory is picked for something else
    // (always a single pane)
    private static readonly Pane[] Panes = { new(), new() };
    private static readonly Pane PickPane = new();
    private static int _active; // The pane with the focus

    private static bool IsDual => _mode == BrowseMode.Edit && DualPane;

    // The pane the keys work on
    private static Pane ActivePane => _mode == BrowseMode.Edit ? Panes[_active] : PickPane;

    // The other pane (when two are shown)
    private static Pane OtherPane => Panes[1 - _active];

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
    /// <param name="favorites">True to open the favorites list first</param>
    internal static void Run(bool favorites = false)
    {
        _openFavorites = favorites;
        Browse(BrowseMode.Edit, DialogText.CaptionsFiles);
    }

    private static bool _openFavorites; // The favorites list is opened as soon as the file browser is shown

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

    // Show a directory in a pane (the data directory if it does not exist)
    private static void StartPaneIn(Pane pane, string directory)
    {
        var active = _active;
        if (_mode == BrowseMode.Edit) _active = Array.IndexOf(Panes, pane);
        pane.Filter = string.Empty;
        pane.Directory = string.Empty;
        ChangeDirectory(System.IO.Directory.Exists(directory) ? directory : DataDirectory(), null);
        pane.ShownTop = -1;
        _active = active;
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
        _pending = null;
        _mode = mode;
        _caption = caption;
        _picked = null;
        SessionEnded = false;
        DataFileSelected = false;
        _message = string.Empty;
        if (mode == BrowseMode.Edit)
        {
            FileFavorites.Load();
            _active = Math.Clamp(StartPane, 0, 1);
            StartPaneIn(Panes[0], StartDirectory);
            // The second pane starts where the first is until it has a directory of its own
            StartPaneIn(Panes[1], StartDirectory2.Length > 0 ? StartDirectory2 : Panes[0].Directory);
        }
        else
        {
            StartPaneIn(PickPane, StartDirectory);
        }

        Init.ApplyAutoSize();
        Draw(true);
        if (Tools.TakeRedrawRequest()) Draw(true);
        while (true)
        {
            // Starting in the favorites list is the same as pressing F (closing the list shows the file browser)
            var key = _openFavorites && _mode == BrowseMode.Edit
                ? new ConsoleKeyInfo('F', ConsoleKey.F, true, false, false)
                : Console.ReadKey(true);
            _openFavorites = false;
            // Moving the selection only redraws the selection pointer (unless the list has to scroll)
            var redraw = true;
            var hadMessage = _message.Length > 0;
            _message = string.Empty;
            if (_pending != null && key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Not copied or moved after all
                StopSelectingTarget();
            }
            else if (_pending != null && key.Key is ConsoleKey.S or ConsoleKey.Spacebar)
            {
                CompleteSelectingTarget();
            }
            else if (key.KeyChar == ':')
            {
                // The command is read as typed, as paths can be case sensitive
                var command = CommandLine.Read(':', false,
                    _mode == BrowseMode.Edit ? CommandContext.Files : CommandContext.FilePicker);
                var name = command == null ? string.Empty : CommandLine.Name(command).ToLowerInvariant();
                if (_pending != null && command != null && name is not ("cd" or "home" or "data" or "h" or "help" or "panes"))
                {
                    // Only going to a directory while the place to copy or move to is selected
                    _message = DialogText.FileSelectTarget;
                    command = null;
                    name = string.Empty;
                }

                if (command != null)
                {
                    // File names and paths are logged, but not something typed that is not a command
                    EventLog.Add(DialogText.LogFilesCommand + ": :" +
                                 (Commands.IsKnown(CommandContext.Files, name) ? command : "(not a command)"));
                }

                if (name is "q" or "quit" or "close")
                {
                    break;
                }

                if (name == "exit" && _mode == BrowseMode.Edit)
                {
                    // Leave Marbles (if the user wants that)
                    if (ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false))
                    {
                        Tools.ExitApplication();
                    }

                    Draw(true);
                    continue;
                }

                if (name is "home")
                {
                    ChangeDirectory(DataDirectory(), null);
                }
                else if (name is "data")
                {
                    // The data directory itself (with the settings, the snippets, the favorites ...), also when
                    // the data files are in a subfolder of it
                    ChangeDirectory(System.IO.Path.GetFullPath(FileIo.GetDataDirectoryName()), null);
                }
                else if (name is "h" or "help")
                {
                    Tools.WriteEditorHelp(false, Tools.HelpTopic.Files);
                }
                else if (name == "cd" && command != null)
                {
                    // Go to a directory by path (a relative path starts from the directory shown)
                    var path = CommandLine.Argument(command);
                    if (path.Length > 0 && ActivePane.Directory.Length > 0)
                    {
                        path = System.IO.Path.GetFullPath(path, ActivePane.Directory);
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
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Files);
            }
            else if (key.KeyChar == '/')
            {
                // Only show the names containing a text
                ActivePane.Filter = CommandLine.Read('/', false) ?? string.Empty;
                LoadItems(null);
            }
            else if (key.KeyChar == 'G')
            {
                ActivePane.Index = ActivePane.Items.Count - 1;
                redraw = false;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') ActivePane.Index = 0;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Esc removes the marks, and then a filter, before it closes the browser
                if (key.Key == ConsoleKey.Escape && ActivePane.Marks.Count > 0)
                {
                    ActivePane.Marks.Clear();
                }
                else if (key.Key == ConsoleKey.Escape && ActivePane.Filter.Length > 0)
                {
                    ActivePane.Filter = string.Empty;
                    LoadItems(null);
                }
                else
                {
                    break;
                }
            }
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                ActivePane.Index++;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                ActivePane.Index--;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.PageDown || Keyboard.IsControl(key, ConsoleKey.D) ||
                     Keyboard.IsControl(key, ConsoleKey.F))
            {
                ActivePane.Index += PageSize;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.PageUp || Keyboard.IsControl(key, ConsoleKey.U) ||
                     Keyboard.IsControl(key, ConsoleKey.B))
            {
                ActivePane.Index -= PageSize;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.Home)
            {
                ActivePane.Index = 0;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.End)
            {
                ActivePane.Index = ActivePane.Items.Count - 1;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.Backspace || key.KeyChar == '-')
            {
                GoToParent();
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.Enter)
            {
                // (while the place to copy or move to is selected, only directories are opened)
                if (ActivePane.Items.Count > 0 &&
                    (_pending == null || ActivePane.Items[ActivePane.Index].IsDirectory) &&
                    Open(ActivePane.Items[ActivePane.Index]))
                {
                    // A file was picked, or a data file was selected: continue on the login page
                    break;
                }
            }
            else if (_mode == BrowseMode.PickDirectory && key.Key is ConsoleKey.S or ConsoleKey.Spacebar)
            {
                // Select the directory shown
                if (ActivePane.Directory.Length > 0)
                {
                    _picked = ActivePane.Directory;
                    break;
                }
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.Tab)
            {
                // The focus goes to the other pane
                if (IsDual) _active = 1 - _active;
                redraw = false;
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.T)
            {
                TogglePanes();
            }
            else if (_pending != null)
            {
                // The other keys do nothing until the place to copy or move to is selected
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.Spacebar)
            {
                ToggleMark();
                redraw = false;
            }
            else if (_mode == BrowseMode.Edit && key.Key == ConsoleKey.N)
            {
                NewFile(string.Empty);
            }
            else if (key.Key == ConsoleKey.R)
            {
                LoadItems(ActivePane.Items.Count > 0 ? ActivePane.Items[ActivePane.Index].Name : null);
            }
            else if (_mode == BrowseMode.Edit && HandleFileKey(key) == KeyResult.Close)
            {
                // A data file was selected from the favorites
                break;
            }

            // Follow the size of the terminal window, and draw again if the page could not be drawn
            if (Init.ApplyAutoSize()) redraw = true;
            // What was done in one pane can have changed what the other shows
            if (redraw && IsDual) Reload(OtherPane);
            Draw(redraw || hadMessage);
            if (Tools.TakeRedrawRequest()) Draw(true);
        }

        if (_mode == BrowseMode.Edit)
        {
            // Remember the directories and the pane for next time
            if (Panes[0].Directory.Length > 0) StartDirectory = Panes[0].Directory;
            if (Panes[1].Directory.Length > 0) StartDirectory2 = Panes[1].Directory;
            StartPane = _active;
            FileIo.SaveSettings();
        }

        return _picked;
    }

    // Show one pane or two
    private static void TogglePanes()
    {
        DualPane = !DualPane;
        // A single pane shows the one that had the focus
        Reload(OtherPane);
    }

    // Read the content of the directory of a pane again, keeping the selection
    private static void Reload(Pane pane)
    {
        var selected = pane.Items.Count > 0 && pane.Index < pane.Items.Count ? pane.Items[pane.Index] : null;
        var index = pane.Index;
        var top = pane.Top;
        var message = _message;
        if (!LoadItems(pane, null))
        {
            // The directory is gone (it can have been moved or deleted from the other pane)
            pane.Directory = DataDirectory();
            pane.Filter = string.Empty;
            LoadItems(pane, null);
            _message = message;
            return;
        }

        var found = selected == null ? -1 : pane.Items.FindIndex(item => item.Name == selected.Name);
        pane.Index = found >= 0 ? found : Math.Min(index, Math.Max(0, pane.Items.Count - 1));
        pane.Top = top;
    }

    private static string DataDirectory()
    {
        return System.IO.Path.GetFullPath(FileIo.GetDataAndSubFolder());
    }

    #region Directories

    private static void ChangeDirectory(string directory, string? select)
    {
        var previous = ActivePane.Directory;
        ActivePane.Directory = directory;
        ActivePane.Filter = string.Empty;
        if (!LoadItems(select))
        {
            // Stay where we were if the directory cannot be read
            ActivePane.Directory = previous;
            var message = _message;
            LoadItems(null);
            _message = message;
        }
    }

    private static void GoToParent()
    {
        if (ActivePane.Directory.Length == 0) return;
        var parent = Directory.GetParent(ActivePane.Directory)?.FullName;
        var name = System.IO.Path.GetFileName(ActivePane.Directory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (parent != null)
        {
            ChangeDirectory(parent, name);
        }
        else if (OperatingSystem.IsWindows())
        {
            // Above the root of a drive is the list of drives
            ChangeDirectory(string.Empty, ActivePane.Directory);
        }
    }

    // Read the content of the directory of the pane with the focus
    private static bool LoadItems(string? select)
    {
        return LoadItems(ActivePane, select);
    }

    // Read the content of the directory of a pane: directories first, then files, both sorted by name
    private static bool LoadItems(Pane pane, string? select)
    {
        var items = new List<Item>();
        try
        {
            if (pane.Directory.Length == 0)
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady) items.Add(new Item(drive.Name, drive.Name, true, 0));
                }
            }
            else
            {
                items.Add(new Item(ParentName, string.Empty, true, 0));
                var info = new DirectoryInfo(pane.Directory);
                foreach (var directory in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (IsShown(directory, pane.Filter)) items.Add(new Item(directory.Name, directory.FullName, true, 0));
                }

                if (_mode != BrowseMode.PickDirectory)
                {
                    foreach (var file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (IsShown(file, pane.Filter)) items.Add(new Item(file.Name, file.FullName, false, file.Length));
                    }
                }
            }
        }
        catch (Exception e)
        {
            _message = e.Message;
            return false;
        }

        pane.Items = items;
        // The marks are kept while the same directory is shown (also with a filter). The ones gone are dropped
        if (pane.MarksDirectory != pane.Directory) pane.Marks.Clear();
        pane.Marks.RemoveWhere(path => !File.Exists(path) && !Directory.Exists(path));
        pane.MarksDirectory = pane.Directory;
        pane.Index = Math.Max(0, select == null ? 0 : items.FindIndex(i => i.Name == select || i.Path == select));
        pane.Top = 0;
        return true;
    }

    private static bool IsShown(FileSystemInfo info, string filter)
    {
        if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return false;
        return filter.Length == 0 || info.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
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
            if (StartMenu.Only == StartMenu.OnlyMode.Editor)
            {
                // Text Editor only: data files are not opened
                _message = DialogText.DataFileNotOpened;
                return false;
            }

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
            var selection = ChoiceBox.Pick(DialogText.CaptionsFiles,
                Lang.F("This file is not UTF-8. It is read as {0}.", file.EncodingName), DialogText.NotUtf8Note,
                new[] { DialogText.ConvertReplace, DialogText.ConvertCopy, DialogText.ConvertCancel },
                new[] { "A", "B", "Esc" }, 0, false);
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
        EventLog.Add(DialogText.LogFile + "Open: " + file.Path);
        AddToHistory(file.Path);
        Mode.SetMode(Mode.SelectedMode.Edit);
        try
        {
            var cursorIndex = _openAtIndex;
            while (true)
            {
                Tools.ClearScreen();
                Actions.DrawEditFileMenu();
                var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                    Init.LineCount - 5, _file?.Text ?? string.Empty, cursorIndex, false, false, false, TextArea.SaveAsFile, modified);
                textArea.ClearUndoRedoCache();
                // ":snip edit": the file of snippets, and then this file again (as it is on disk)
                if (TextArea.TakeSnippetsRequest() is not { } line) break;
                cursorIndex = textArea.GetCursorIndex();
                EditSnippets(line);
                if (!File.Exists(file.Path)) break;
                _file = TextFile.Load(file.Path);
                modified = false;
            }
        }
        catch (Exception e) when (e is not NavigationException)
        {
            _message = e.Message;
        }
        finally
        {
            Mode.SetMode(Mode.SelectedMode.Login);
            _file = null;
        }
    }

    /// <summary>
    /// Open the file of snippets in the editor (at a line), from the editor of a file or an entry (":snip edit").
    /// The file or entry being edited is kept, and the data file stays open
    /// </summary>
    internal static void EditSnippets(int line)
    {
        var outer = _file;
        var mode = Mode.CurrentMode;
        try
        {
            _file = TextFile.Load(Snippets.FilePath);
            EventLog.Add(DialogText.LogFile + "Open: " + _file.Path);
            Mode.SetMode(Mode.SelectedMode.Edit);
            Tools.ClearScreen();
            Actions.DrawEditFileMenu();
            var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                Init.LineCount - 5, _file.Text, Snippets.IndexOfLine(_file.Text, line), false, false, false,
                TextArea.SaveAsFile);
            textArea.ClearUndoRedoCache();
            TextArea.TakeSnippetsRequest();
        }
        catch (Exception e) when (e is not NavigationException)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.CaptionsSnippets, e.Message, string.Empty);
        }
        finally
        {
            _file = outer;
            Mode.SetMode(mode);
        }
    }

    /// <summary>
    /// The name of the file being edited (for the title of the editor)
    /// </summary>
    internal static string CurrentFileName => _file == null ? string.Empty : System.IO.Path.GetFileName(_file.Path);

    /// <summary>
    /// The full path of the file being edited (empty if none)
    /// </summary>
    internal static string CurrentFilePath => _file == null ? string.Empty : System.IO.Path.GetFullPath(_file.Path);

    /// <summary>
    /// The type and encoding of the file being edited, and line endings of the file being edited, for the bottom line of the editor:
    /// "HTML UTF-8 (BOM) CRLF"
    /// </summary>
    internal static string CurrentFileLabel
    {
        get
        {
            if (_file == null) return string.Empty;
            var extension = System.IO.Path.GetExtension(_file.Path);
            var type = extension.Length > 1 && extension.Length < System.IO.Path.GetFileName(_file.Path).Length
                ? Tools.TruncateString(extension[1..].ToUpperInvariant(), 10, true) + " "
                : string.Empty;
            return type + _file.EncodingLabel + " " + _file.LineEndingLabel;
        }
    }

    /// <summary>
    /// Read the file being edited from disk again (it can have been changed by another program)
    /// </summary>
    /// <param name="text">The text of the file</param>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string ReloadCurrentFile(out string text)
    {
        text = string.Empty;
        if (_file == null) return DialogText.FileNotFound;
        try
        {
            if (!File.Exists(_file.Path)) return DialogText.FileNotFound;
            _file = TextFile.Load(_file.Path);
        }
        catch (Exception e)
        {
            return e.Message;
        }

        EventLog.Add(DialogText.LogFile + "Reload: " + _file.Path);
        text = _file.Text;
        return DialogText.Success;
    }

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
        if (ActivePane.Directory.Length == 0) return;
        name = name.Trim();
        if (name.Length == 0)
        {
            var asked = AskName(DialogText.NewFile, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var path = System.IO.Path.Combine(ActivePane.Directory, name);
        var problem = Tools.FileNameProblem(name) ??
                      (File.Exists(path) || Directory.Exists(path) ? DialogText.NameExists : null);
        if (problem != null)
        {
            _message = problem;
            LogOperation("New file", path, _message);
            return;
        }

        try
        {
            File.WriteAllBytes(path, Array.Empty<byte>());
        }
        catch (Exception e)
        {
            _message = e.Message;
            LogOperation("New file", path, _message);
            return;
        }

        LogOperation("New file", path, DialogText.Success);

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
                if (!ChoiceBox.Confirm(DialogText.CaptionsFiles, DialogText.CopyDataFile, DialogText.CopyDataFileNote,
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

    private static int _shownActive = -1; // The pane that had the focus when the browser was drawn last
    private static bool _shownDual;

    // The columns of a pane: where it starts and how wide it is. Two panes share the window with a
    // separator between them
    private static (int Left, int Width) PaneArea(int number)
    {
        if (!IsDual) return (0, Init.LineWidth);
        var half = (Init.LineWidth - 1) / 2;
        return number == 0 ? (0, half) : (half + 1, Init.LineWidth - half - 2);
    }

    /// <summary>
    /// Draw the file browser
    /// </summary>
    /// <param name="all">False if only the selection has moved, or the focus has gone to the other pane:
    /// then only the selection pointers, the pane labels and the status are drawn (the whole screen is
    /// still drawn if the list has to scroll)</param>
    private static void Draw(bool all)
    {
        var shown = IsDual ? Panes : new[] { ActivePane };
        foreach (var pane in shown)
        {
            // Keep the selection within the list and the view
            pane.Index = Math.Clamp(pane.Index, 0, Math.Max(0, pane.Items.Count - 1));
            if (pane.Index < pane.Top) pane.Top = pane.Index;
            if (pane.Index >= pane.Top + PageSize) pane.Top = pane.Index - PageSize + 1;
        }

        Tools.HideCursor();
        if (!all && _shownDual == IsDual && shown.All(pane => pane.Top == pane.ShownTop))
        {
            if (IsDual && _shownActive != _active)
            {
                // The focus has moved to the other pane
                for (var number = 0; number < Panes.Length; number++) DrawPaneHeader(number);
            }

            // Move the selection pointers
            for (var number = 0; number < shown.Length; number++)
            {
                var pane = shown[number];
                DrawPointer(number, pane.ShownIndex, false);
                DrawPointer(number, pane.Index, true);
                pane.ShownIndex = pane.Index;
            }

            _shownActive = _active;
            DrawStatus();
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        // While the place to copy or move to is selected, the label stands out
        Menu.WriteModeLabel(_caption, string.Empty, 0, _pending != null);
        Menu.WriteNameInTopBar(false);
        for (var number = 0; number < shown.Length; number++)
        {
            var pane = shown[number];
            DrawPaneHeader(number);
            for (var row = 0; row < PageSize && pane.Top + row < pane.Items.Count; row++)
            {
                DrawItem(number, pane, pane.Top + row);
            }

            pane.ShownTop = pane.Top;
            pane.ShownIndex = pane.Index;
        }

        if (IsDual)
        {
            // The line between the panes
            var column = PaneArea(1).Left - 1;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(1, column);
            Console.Write('|');
            for (var row = 0; row < PageSize; row++)
            {
                Tools.SetCursorEx(ListTop + row, column);
                Console.Write('|');
            }
        }

        _shownActive = _active;
        _shownDual = IsDual;
        DrawStatus();
    }

    // The pane that is drawn as a number (a single pane is always the first)
    private static Pane ShownPane(int number) => IsDual ? Panes[number] : ActivePane;

    private static bool HasFocus(int number) => !IsDual || number == _active;

    // The two lines above the list of a pane: the directory, and a divider that tells which pane has the focus
    private static void DrawPaneHeader(int number)
    {
        var pane = ShownPane(number);
        var (left, width) = PaneArea(number);
        // The directory (the end of a long path is shown)
        var room = width - 2;
        var path = pane.Directory.Length == 0 ? DialogText.Drives : pane.Directory;
        if (path.Length > room) path = "..." + path[^(room - 3)..];
        Tools.SetCursorEx(1, left);
        ConsoleColors.SetColors(HasFocus(number)
            ? ConsoleColors.ColorPreset.HeaderColor
            : ConsoleColors.ColorPreset.Divider);
        Console.Write(" " + path.PadRight(room));
        Tools.SetCursorEx(2, left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', IsDual && number == 0 ? width : Math.Min(width, Init.LineWidth - left)));
        if (!IsDual) return;
        if (number == 0) Console.Write('+');
        // The label of the pane: highlighted on the one with the focus (in the attention color while the place to
        // copy or move to is selected, as the label at the top)
        Tools.SetCursorEx(2, left + 2);
        if (HasFocus(number))
        {
            ConsoleColors.SetColors(_pending != null
                ? ConsoleColors.ColorPreset.AttentionInverted
                : ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(" " + Lang.F("PANE {0}", number + 1) + " ");
        }
        else
        {
            Console.Write(" " + Lang.F("PANE {0}", number + 1).ToLowerInvariant() + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // A row of the list of a pane: the name, the type (if the pane is wide enough for it) and the size
    private static void DrawItem(int number, Pane pane, int index)
    {
        var item = pane.Items[index];
        var row = index - pane.Top;
        var (left, width) = PaneArea(number);
        DrawPointer(number, index, index == pane.Index);
        var isDataFile = !item.IsDirectory &&
                         item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase);
        var showType = width >= MinWidthForType;
        var type = ItemType(item);
        // In a narrow pane a directory is marked where the size would be
        var size = item.IsDirectory ? (showType ? string.Empty : DialogText.DirectoryMark)
            : FileSizeFormatter.FormatSize(item.Size);
        var sizeEnd = left + width - 1;
        var typeColumn = sizeEnd - 1 - SizeColumnWidth - TypeColumnWidth;
        var nameEnd = showType ? typeColumn - 1 : sizeEnd - SizeColumnWidth - 1;
        var name = Tools.TruncateString(item.Name + (item.IsDirectory && item.Name != ParentName ? "/" : ""),
            Math.Max(4, nameEnd - (left + 3) - 2), true);
        var nameColors = item.IsDirectory ? ConsoleColors.ColorPreset.HeaderColor :
            isDataFile ? ConsoleColors.ColorPreset.Favorites : ConsoleColors.ColorPreset.Normal;
        if (item.Path.Length > 0 && pane.Marks.Contains(item.Path))
        {
            // Marked
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write("+");
            ConsoleColors.SetColors(nameColors);
            Console.Write(name);
        }
        else
        {
            ConsoleColors.SetColors(nameColors);
            Console.Write(" " + name);
        }
        if (_mode == BrowseMode.Edit && item.Path.Length > 0 && FileFavorites.Contains(item.Path))
        {
            // A favorite
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
            Console.Write(" *");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (showType)
        {
            Tools.SetCursorEx(ListTop + row, typeColumn);
            Console.Write(type);
        }

        Tools.SetCursorEx(ListTop + row, sizeEnd - size.Length);
        Console.Write(size);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private const int MinWidthForType = 50; // The type column is left out in a pane that is narrower

    private const int TypeColumnWidth = 11; // The width of "[Data file]"
    private const int SizeColumnWidth = 11; // The width of "1,023 bytes"

    // What is shown in the type column: a directory, a data file, or the extension of the file ("TXT")
    private static string ItemType(Item item)
    {
        if (item.IsDirectory) return DialogText.DirectoryMark;
        if (item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase))
        {
            return DialogText.DataFileMark;
        }

        var extension = System.IO.Path.GetExtension(item.Name);
        // A name like ".gitignore" is not an extension
        if (extension.Length < 2 || extension.Length == item.Name.Length) return string.Empty;
        return Tools.TruncateString(extension[1..].ToUpperInvariant(), TypeColumnWidth, true);
    }

    // Draw (or remove) the selection pointer in front of an item of a pane, leaving the cursor after it.
    // In the pane without the focus the pointer is not highlighted
    private static void DrawPointer(int number, int index, bool selected)
    {
        var pane = ShownPane(number);
        var row = index - pane.Top;
        if (row < 0 || row >= PageSize || index >= pane.Items.Count) return;
        Tools.SetCursorEx(ListTop + row, PaneArea(number).Left);
        ConsoleColors.SetColors(!selected ? ConsoleColors.ColorPreset.Normal :
            HasFocus(number) ? ConsoleColors.ColorPreset.SelectorInverted : ConsoleColors.ColorPreset.Divider);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The two bottom lines: the number of the selected item of each pane, and a message or the keys
    private static void DrawStatus()
    {
        var count = IsDual ? Panes.Length : 1;
        for (var number = 0; number < count; number++)
        {
            var pane = ShownPane(number);
            var (left, width) = PaneArea(number);
            Tools.SetCursorEx(Init.LineCount - 2, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(new string('-', IsDual && number == 0 ? width : Math.Min(width, Init.LineWidth - left)));
            if (IsDual && number == 0) Console.Write('+');
            var status = " " + (pane.Items.Count == 0 ? 0 : pane.Index + 1) + "/" + pane.Items.Count + " ";
            if (pane.Filter.Length > 0) status += "/" + pane.Filter + " ";
            if (pane.Marks.Count > 0) status = " " + Lang.F("{0} marked", pane.Marks.Count) + " -" + status;
            status = Tools.TruncateString(status, Math.Max(4, width - 6), true);
            Tools.SetCursorEx(Init.LineCount - 2, left + width - status.Length - 2);
            Console.Write(status);
        }

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        if (_message.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(_message, Init.LineWidth - 4, true) + " ");
        }
        else
        {
            Menu.WriteKeyHints(_mode switch
            {
                BrowseMode.PickFile => DialogText.FilePickKeys,
                BrowseMode.PickDirectory => DialogText.DirectoryPickKeys,
                _ when _pending != null => (IsDual ? DialogText.FileTargetDualKeys : DialogText.FileTargetKeys)
                    .Replace("{0}", _pending.IsMove ? DialogText.FileMoveHere : DialogText.FileCopyHere),
                // What can be done with the marked files and directories
                _ when ActivePane.Marks.Count > 0 => DialogText.FileMarkedKeys,
                _ => IsDual ? DialogText.FileBrowserDualKeys : DialogText.FileBrowserKeys
            });
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    #endregion
}
