namespace Marbles;

// File management in the file browser: new directory, rename, copy, duplicate, move and delete, working on
// the selected file or directory, or on the marked ones (Space). The operations themselves are found in the
// class FileOperations.
internal static partial class FileExplorer
{
    private enum KeyResult
    {
        NotHandled,
        Handled,
        Close // The browser must close (a data file was selected)
    }

    // A key for managing files (only when the browser is opened for editing files)
    private static KeyResult HandleFileKey(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return KeyResult.NotHandled;
        switch (key.Key)
        {
            case ConsoleKey.A:
                MakeDirectory(string.Empty);
                break;
            case ConsoleKey.I:
                ShowInfo();
                break;
            case ConsoleKey.X:
                RenameSelected(string.Empty);
                break;
            case ConsoleKey.C:
                CopySelected();
                break;
            case ConsoleKey.Y:
                DuplicateSelected();
                break;
            case ConsoleKey.M:
                MoveSelected();
                break;
            case ConsoleKey.D:
                DeleteSelected();
                break;
            case ConsoleKey.P:
                ToggleFavorite();
                break;
            case ConsoleKey.F:
                return BrowseFavorites();
            case ConsoleKey.S:
                return SearchFiles();
            default:
                return KeyResult.NotHandled;
        }

        return KeyResult.Handled;
    }

    // A command for managing files
    private static KeyResult RunFileCommand(string name, string argument)
    {
        switch (name)
        {
            case "new":
                NewFile(argument);
                break;
            case "info":
                ShowInfo();
                break;
            case "mkdir":
                MakeDirectory(argument);
                break;
            case "rename":
                RenameSelected(argument);
                break;
            case "copy":
                CopySelected();
                break;
            case "duplicate":
                DuplicateSelected();
                break;
            case "move":
                MoveSelected();
                break;
            case "delete":
                DeleteSelected();
                break;
            case "fav":
            case "favorite":
                ToggleFavorite();
                break;
            case "favs":
            case "favorites":
                return BrowseFavorites();
            case "panes":
                TogglePanes();
                break;
            case "find":
                return SearchFiles();
            default:
                return KeyResult.NotHandled;
        }

        return KeyResult.Handled;
    }

    // The selected file or directory, if it is one that can be managed (not ".." or a drive)
    private static Item? SelectedItem()
    {
        if (ActivePane.Directory.Length == 0 || ActivePane.Items.Count == 0) return null;
        var item = ActivePane.Items[ActivePane.Index];
        return item.Name == ParentName ? null : item;
    }

    private static bool IsDataFile(Item item)
    {
        return !item.IsDirectory &&
               item.Name.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase);
    }

    // Why a file or directory cannot be renamed, moved or deleted here (null if it can): it is used by Marbles
    private static string? Protection(Item item)
    {
        var dataDirectory = System.IO.Path.GetFullPath(FileIo.GetDataDirectoryName());
        if (item.IsDirectory)
        {
            // The data directory, and the directories it is in
            return FileOperations.SamePath(item.Path, dataDirectory) ||
                   FileOperations.IsInside(dataDirectory, item.Path)
                ? DialogText.FileUsedByMarbles
                : null;
        }

        if (FileOperations.SamePath(item.Path, System.IO.Path.Combine(dataDirectory, FileIo.SettingsFileName)) ||
            FileOperations.SamePath(item.Path, FileFavorites.FilePath))
        {
            return DialogText.FileUsedByMarbles;
        }

        // The data file that is logged in to
        if (Mode.CurrentMode != Mode.SelectedMode.Login &&
            FileOperations.SamePath(item.Path,
                System.IO.Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName())))
        {
            return DialogText.FileUsedByMarbles;
        }

        return null;
    }

    private static bool IsProtected(Item item)
    {
        var protection = Protection(item);
        if (protection == null) return false;
        _message = protection;
        return true;
    }

    // True (with the reason as the message) if one of the files or directories cannot be moved or deleted
    private static bool AnyProtected(List<Item> items)
    {
        foreach (var item in items)
        {
            var protection = Protection(item);
            if (protection == null) continue;
            _message = items.Count == 1 ? protection : item.Name + ": " + protection;
            return true;
        }

        return false;
    }

    #region Marks

    // Mark (or unmark) the selected file or directory and go to the next
    private static void ToggleMark()
    {
        var item = SelectedItem();
        if (item == null) return;
        var pane = ActivePane;
        if (!pane.Marks.Remove(item.Path)) pane.Marks.Add(item.Path);
        DrawItem(IsDual ? _active : 0, pane, pane.Index);
        if (pane.Index < pane.Items.Count - 1) pane.Index++;
    }

    // The files and directories an operation works on: the marked ones (directories first), or the selected one
    // if none are marked
    private static List<Item> MarkedOrSelected()
    {
        var pane = ActivePane;
        if (pane.Marks.Count == 0)
        {
            var item = SelectedItem();
            return item == null ? new List<Item>() : new List<Item> { item };
        }

        // A marked item can be hidden by a filter
        return pane.Marks.Select(path => pane.Items.FirstOrDefault(item => item.Path == path) ?? ItemOf(path))
            .OrderBy(item => !item.IsDirectory)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Item ItemOf(string path)
    {
        var isDirectory = Directory.Exists(path);
        return new Item(System.IO.Path.GetFileName(path), path, isDirectory,
            isDirectory ? 0 : new FileInfo(path).Length);
    }

    // What went wrong with one of the files or directories: with its name if there are several
    private static string Failure(List<Item> items, Item item, string result)
    {
        return items.Count == 1 ? result : item.Name + ": " + result;
    }

    #endregion

    // Ask for a name in a box on top of the list. Returns null if canceled
    private static string? AskName(string caption, string name)
    {
        return ChoiceBox.AskText(caption, DialogText.TypeNameAndPressEnter, DialogText.NameColon, name);
    }

    #region Selecting where to copy or move to

    // A file or directory that is being copied or moved: the user is selecting the directory to put it in.
    // This is done in the panes as they are: any directory can be gone to, in either pane, and "S" selects
    // the directory shown in the pane with the focus
    private sealed record PendingOperation(List<Item> Items, bool IsMove, int Pane);

    private static PendingOperation? _pending;

    private static void StartSelectingTarget(List<Item> items, bool isMove)
    {
        _pending = new PendingOperation(items, isMove, _active);
        if (items.Count == 1)
        {
            _caption = (isMove ? DialogText.FileMoveTo : DialogText.FileCopyTo) + items[0].Name;
        }
        else
        {
            _caption = isMove ? Lang.F("Move to: {0} marked", items.Count) : Lang.F("Copy to: {0} marked", items.Count);
        }

        // With two panes the other pane is the place to start: "S" at once copies or moves to it
        if (IsDual) _active = 1 - _active;
    }

    private static void StopSelectingTarget()
    {
        if (_pending == null) return;
        _active = _pending.Pane;
        _pending = null;
        _caption = DialogText.CaptionsFiles;
    }

    // Copy or move to the directory shown in the pane with the focus
    private static void CompleteSelectingTarget()
    {
        if (_pending == null) return;
        var target = ActivePane.Directory;
        if (target.Length == 0)
        {
            // The list of drives is not a directory
            _message = DialogText.DirectoryNotFound;
            return;
        }

        var (items, isMove, _) = _pending;
        // Back to the pane the files are in
        StopSelectingTarget();
        Draw(true);
        // One after the other, until one fails (or the copying is canceled). Those done are no longer marked
        string? failed = null;
        Item? failedItem = null;
        var done = 0;
        var path = string.Empty;
        var favoritesChanged = false;
        foreach (var item in items)
        {
            var result = isMove
                ? FileOperations.Move(item.Path, target, out path, ShowProgress)
                : FileOperations.Copy(item.Path, target, out path, ShowProgress);
            LogOperation(isMove ? "Move" : "Copy", item.Path + " -> " + target, result);
            if (result != DialogText.Success)
            {
                failed = Failure(items, item, result);
                failedItem = item;
                break;
            }

            done++;
            ActivePane.Marks.Remove(item.Path);
            if (isMove && FileFavorites.PathChanged(item.Path, path)) favoritesChanged = true;
        }

        string message;
        if (items.Count == 1)
        {
            message = (isMove ? DialogText.FileMoved : DialogText.FileCopied) + ShortPath(target);
        }
        else
        {
            message = isMove
                ? Lang.F("{0} moved to {1}", done, ShortPath(target))
                : Lang.F("{0} copied to {1}", done, ShortPath(target));
        }

        // A copy in the same directory is selected. Else the original stays selected (or the place it was in)
        var inPlace = !isMove && done > 0 && FileOperations.SamePath(target, ActivePane.Directory);
        Finish(failed ?? DialogText.Success, inPlace ? path : failedItem?.Path ?? (isMove ? null : items[0].Path),
            message);
        if (favoritesChanged) SaveFavorites();
    }

    #endregion

    // Show the file being copied on the bottom line. Esc cancels
    private static bool ShowProgress(string name)
    {
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Console.Write(" " + Tools.TruncateString(DialogText.FileCopying + name, Init.LineWidth - 4, true) + " ");
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
            // Without a keyboard to read there is nothing to cancel with
        }

        return true;
    }

    // Show the directory again after an operation, with an item selected or with the error message
    private static void Finish(string result, string? select, string message)
    {
        var index = ActivePane.Index;
        LoadItems(select);
        if (select != null && ActivePane.Filter.Length > 0 && !ActivePane.Items.Any(item => item.Path == select))
        {
            // The item no longer matches the filter (it has a new name): show all
            ActivePane.Filter = string.Empty;
            LoadItems(select);
        }

        if (select == null) ActivePane.Index = index;
        _message = result == DialogText.Success ? message : result;
    }

    // The end of a long path, for a message on the bottom line
    private static string ShortPath(string path)
    {
        var room = Math.Max(10, Init.LineWidth - 20);
        return path.Length <= room ? path : "..." + path[^(room - 3)..];
    }

    // Add a file operation to the log, with what went wrong if it failed
    private static void LogOperation(string operation, string detail, string result)
    {
        EventLog.Add(DialogText.LogFile + operation + ": " + detail +
                     (result == DialogText.Success ? string.Empty : " - FAILED: " + result));
    }

    private static void SaveFavorites()
    {
        var result = FileFavorites.Save();
        if (result != DialogText.Success) _message = result;
    }

    private static void MakeDirectory(string name)
    {
        if (ActivePane.Directory.Length == 0) return;
        if (name.Length == 0)
        {
            var asked = AskName(DialogText.NewDirectory, string.Empty);
            if (asked == null) return;
            name = asked;
        }

        var result = FileOperations.CreateDirectory(ActivePane.Directory, name, out var path);
        LogOperation("New directory", System.IO.Path.Combine(ActivePane.Directory, name), result);
        Finish(result, result == DialogText.Success ? path : null, string.Empty);
    }

    private static void RenameSelected(string name)
    {
        var item = SelectedItem();
        if (item == null || IsProtected(item)) return;
        if (name.Length == 0)
        {
            var asked = AskName(DialogText.RenameFile, item.Name);
            if (asked == null) return;
            name = asked;
        }

        var result = FileOperations.Rename(item.Path, name, out var path);
        LogOperation("Rename", item.Path + " -> " + name, result);
        Finish(result, result == DialogText.Success ? path : item.Path, string.Empty);
        if (result == DialogText.Success && FileFavorites.PathChanged(item.Path, path)) SaveFavorites();
    }

    private static void CopySelected()
    {
        var items = MarkedOrSelected();
        if (items.Count > 0) StartSelectingTarget(items, false);
    }

    private static void DuplicateSelected()
    {
        var items = MarkedOrSelected();
        if (items.Count == 0) return;
        string? failed = null;
        var select = items[0].Path;
        var done = 0;
        foreach (var item in items)
        {
            var result = FileOperations.Copy(item.Path, ActivePane.Directory, out var path, ShowProgress);
            LogOperation("Duplicate", item.Path + (path.Length > 0 ? " -> " + System.IO.Path.GetFileName(path) : ""),
                result);
            if (result != DialogText.Success)
            {
                failed = Failure(items, item, result);
                select = item.Path;
                break;
            }

            done++;
            ActivePane.Marks.Remove(item.Path);
            select = path;
        }

        Finish(failed ?? DialogText.Success, select, items.Count == 1 ? string.Empty : Lang.F("{0} duplicated", done));
    }

    private static void MoveSelected()
    {
        var items = MarkedOrSelected();
        if (items.Count > 0 && !AnyProtected(items)) StartSelectingTarget(items, true);
    }

    private static void DeleteSelected()
    {
        var items = MarkedOrSelected();
        if (items.Count == 0 || AnyProtected(items)) return;
        if (!ConfirmDelete(items)) return;

        string? failed = null;
        Item? failedItem = null;
        var done = 0;
        var favoritesChanged = false;
        foreach (var item in items)
        {
            var result = FileOperations.Delete(item.Path);
            LogOperation(item.IsDirectory ? "Delete directory" : "Delete", item.Path, result);
            if (result != DialogText.Success)
            {
                failed = Failure(items, item, result);
                failedItem = item;
                break;
            }

            done++;
            if (FileFavorites.PathDeleted(item.Path)) favoritesChanged = true;
        }

        Finish(failed ?? DialogText.Success, failedItem?.Path,
            items.Count == 1 ? DialogText.FileItemDeleted + items[0].Name : Lang.F("{0} deleted", done));
        if (favoritesChanged) SaveFavorites();
    }

    // Ask before deleting, in a box on top of the list, telling what is deleted
    private static bool ConfirmDelete(List<Item> items)
    {
        if (items.Count == 1)
        {
            var item = items[0];
            string note;
            if (item.IsDirectory)
            {
                var (files, directories) = FileOperations.Count(item.Path);
                note = files + directories == 0
                    ? DialogText.DeleteEmptyDirectoryNote
                    : Lang.F("With all its content: {0} files and {1} directories. They cannot be recovered", files,
                        directories);
            }
            else
            {
                note = IsDataFile(item) ? DialogText.DeleteDataFileNote : DialogText.DeleteFileNote;
            }

            return ChoiceBox.Confirm(item.IsDirectory ? DialogText.DeleteDirectory : DialogText.DeleteFile,
                DialogText.DeleteNamed.Replace("{0}", Tools.TruncateString(item.Name, 40, true)), note, true);
        }

        // All the files and directories, with the content of the directories
        int allFiles = 0, allDirectories = 0;
        foreach (var item in items)
        {
            if (!item.IsDirectory)
            {
                allFiles++;
                continue;
            }

            var (files, directories) = FileOperations.Count(item.Path);
            allFiles += files;
            allDirectories += directories + 1;
        }

        var note2 = Lang.F("In all {0} files and {1} directories. They cannot be recovered.", allFiles, allDirectories);
        if (items.Any(IsDataFile)) note2 += " " + DialogText.DeleteDataFilesNote;
        return ChoiceBox.Confirm(DialogText.DeleteMarkedCaption,
            Lang.F("Delete the {0} marked files and directories?", items.Count), note2, true);
    }

    // Show information about the selected file or directory in a box on top of the list, until a key is pressed
    private static void ShowInfo()
    {
        var item = SelectedItem();
        if (item != null) ShowInfo(item);
    }

    private static void ShowInfo(Item item)
    {
        var rows = new List<(string Label, string Value)>();
        try
        {
            FileSystemInfo info = item.IsDirectory ? new DirectoryInfo(item.Path) : new FileInfo(item.Path);
            rows.Add((DialogText.InfoName, item.Name));
            rows.Add((DialogText.InfoType, FileType(item, info)));
            if (item.IsDirectory)
            {
                var (files, directories) = FileOperations.Count(item.Path);
                rows.Add((DialogText.InfoContent, Lang.F("{0} files, {1} directories", files, directories)));
            }
            else
            {
                rows.Add((DialogText.InfoSize, FileSizeFormatter.FormatSize(item.Size) +
                                               (item.Size >= 1024 ? " (" + Lang.F("{0} bytes", item.Size.ToString("N0")) + ")" : "")));
            }

            rows.Add((DialogText.InfoCreated, info.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")));
            rows.Add((DialogText.InfoChanged, info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
            if (FileFavorites.Contains(item.Path)) rows.Add((DialogText.InfoFavorite, DialogText.InfoFavoriteYes));
            // A long path continues on the next rows
            var width = Init.LineWidth - 8 - InfoLabelWidth;
            var path = item.Path;
            for (var label = DialogText.InfoPath; path.Length > 0; label = string.Empty)
            {
                var part = path.Length <= width ? path : path[..width];
                rows.Add((label, part));
                path = path[part.Length..];
            }
        }
        catch (Exception e)
        {
            _message = e.Message;
            return;
        }

        var inner = Init.LineWidth - 6;
        var top = Math.Max(ListTop, (Init.LineCount - rows.Count - 3) / 2);
        const int left = 2;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(top, left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        for (var i = 0; i < rows.Count; i++)
        {
            Tools.SetCursorEx(top + 1 + i, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write("| " + rows[i].Label.PadRight(InfoLabelWidth));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(Tools.TruncateString(rows[i].Value, inner - InfoLabelWidth - 2, true)
                .PadRight(inner - InfoLabelWidth - 1));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(top + rows.Count + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(top, left + 2);
        Console.Write(" " + (item.IsDirectory ? DialogText.InfoCaptionDirectory : DialogText.InfoCaptionFile) + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.ReadKey(true);
    }

    private const int InfoLabelWidth = 10;

    // What kind of file it is, as far as the name tells
    private static string FileType(Item item, FileSystemInfo info)
    {
        var type = item.IsDirectory ? DialogText.InfoTypeDirectory :
            IsDataFile(item) ? DialogText.InfoTypeDataFile :
            info.Extension.Length > 1 ? Lang.F("{0} file", info.Extension[1..].ToUpperInvariant()) :
            DialogText.InfoTypeNoExtension;
        if (info.LinkTarget != null) type += Lang.F(", link to {0}", info.LinkTarget);
        if ((info.Attributes & FileAttributes.ReadOnly) != 0 && !item.IsDirectory) type += DialogText.InfoTypeReadOnly;
        return type;
    }

    private static void ToggleFavorite()
    {
        var items = MarkedOrSelected();
        if (items.Count == 0) return;
        if (items.Count == 1)
        {
            _message = FileFavorites.Toggle(items[0].Path) ? DialogText.FavoriteAdded : DialogText.FavoriteRemoved;
        }
        else
        {
            // All become favorites, or none if they all are
            var add = !items.All(item => FileFavorites.Contains(item.Path));
            foreach (var item in items.Where(item => FileFavorites.Contains(item.Path) != add))
            {
                FileFavorites.Toggle(item.Path);
            }

            _message = add
                ? Lang.F("{0} added to the favorites", items.Count)
                : Lang.F("{0} removed from the favorites", items.Count);
            ActivePane.Marks.Clear();
        }

        SaveFavorites();
    }
}
