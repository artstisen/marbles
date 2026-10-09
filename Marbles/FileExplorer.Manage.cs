namespace Marbles;

// File management in the file browser: new directory, rename, copy, duplicate, move and delete, working on
// the selected file or directory. The operations themselves are found in the class FileOperations.
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

    // Ask for a name on a page of its own. Returns null if canceled
    private static string? AskName(string caption, string name)
    {
        Tools.ClearScreen();
        Menu.WriteModeLabel(caption, DialogText.EnterFileNameAndPressEnter + " " + DialogText.PressEscToExitP, 1);
        Tools.CursorDown(3);
        Console.Write(" " + DialogText.NameColon);
        var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, 56, 1, name, 0, name.Length > 0, false,
            false, 0);
        name = textArea.GetText().Trim();
        return textArea.Canceled || name.Length == 0 ? null : name;
    }

    // Let the user select the directory to copy or move something to. With two panes it starts in the
    // directory of the other pane, so that "S" at once copies or moves to it. With one: in the directory shown
    private static string? PickTargetDirectory(string caption)
    {
        var saved = (_mode, _caption, SessionEnded, DataFileSelected);
        var start = IsDual ? OtherPane.Directory : ActivePane.Directory;
        var target = Browse(BrowseMode.PickDirectory, caption, start.Length > 0 ? start : null);
        (_mode, _caption, SessionEnded, DataFileSelected) = saved;
        _picked = null;
        _message = string.Empty;
        // The panes are as they were: they must be drawn again
        foreach (var pane in Panes) pane.ShownTop = -1;
        return target;
    }

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
        var item = SelectedItem();
        if (item == null) return;
        var target = PickTargetDirectory(DialogText.FileCopyTo + item.Name);
        if (target == null) return;
        Draw(true);
        var result = FileOperations.Copy(item.Path, target, out var path, ShowProgress);
        LogOperation("Copy", item.Path + " -> " + target, result);
        // A copy in the same directory is selected. Else the original stays selected
        var inPlace = result == DialogText.Success && FileOperations.SamePath(target, ActivePane.Directory);
        Finish(result, inPlace ? path : item.Path, DialogText.FileCopied + ShortPath(target));
    }

    private static void DuplicateSelected()
    {
        var item = SelectedItem();
        if (item == null) return;
        var result = FileOperations.Copy(item.Path, ActivePane.Directory, out var path, ShowProgress);
        LogOperation("Duplicate", item.Path + (path.Length > 0 ? " -> " + System.IO.Path.GetFileName(path) : ""),
            result);
        Finish(result, result == DialogText.Success ? path : item.Path, string.Empty);
    }

    private static void MoveSelected()
    {
        var item = SelectedItem();
        if (item == null || IsProtected(item)) return;
        var target = PickTargetDirectory(DialogText.FileMoveTo + item.Name);
        if (target == null) return;
        Draw(true);
        var result = FileOperations.Move(item.Path, target, out var path, ShowProgress);
        LogOperation("Move", item.Path + " -> " + target, result);
        Finish(result, result == DialogText.Success ? null : item.Path, DialogText.FileMoved + ShortPath(target));
        if (result == DialogText.Success && FileFavorites.PathChanged(item.Path, path)) SaveFavorites();
    }

    private static void DeleteSelected()
    {
        var item = SelectedItem();
        if (item == null || IsProtected(item)) return;
        string note;
        if (item.IsDirectory)
        {
            var (files, directories) = FileOperations.Count(item.Path);
            note = files + directories == 0
                ? DialogText.DeleteEmptyDirectoryNote
                : DialogText.DeleteDirectoryNote + files + DialogText.DeleteDirectoryFiles + directories +
                  DialogText.DeleteDirectoryDirectories;
        }
        else
        {
            note = IsDataFile(item) ? DialogText.DeleteDataFileNote : DialogText.DeleteFileNote;
        }

        if (!Menu.YesNoPrompt(item.IsDirectory ? DialogText.DeleteDirectory : DialogText.DeleteFile,
                DialogText.DeleteNamedYn.Replace("{0}", Tools.TruncateString(item.Name, 40, true)), note, true))
        {
            return;
        }

        var result = FileOperations.Delete(item.Path);
        LogOperation(item.IsDirectory ? "Delete directory" : "Delete", item.Path, result);
        Finish(result, result == DialogText.Success ? null : item.Path, DialogText.FileItemDeleted + item.Name);
        if (result == DialogText.Success && FileFavorites.PathDeleted(item.Path)) SaveFavorites();
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
                rows.Add((DialogText.InfoContent, files + DialogText.InfoFiles + directories + DialogText.InfoDirectories));
            }
            else
            {
                rows.Add((DialogText.InfoSize, FileSizeFormatter.FormatSize(item.Size) +
                                               (item.Size >= 1024 ? " (" + item.Size.ToString("N0") + " bytes)" : "")));
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
            info.Extension.Length > 1 ? info.Extension[1..].ToUpperInvariant() + DialogText.InfoTypeFile :
            DialogText.InfoTypeNoExtension;
        if (info.LinkTarget != null) type += DialogText.InfoTypeLink + info.LinkTarget;
        if ((info.Attributes & FileAttributes.ReadOnly) != 0 && !item.IsDirectory) type += DialogText.InfoTypeReadOnly;
        return type;
    }

    private static void ToggleFavorite()
    {
        var item = SelectedItem();
        if (item == null) return;
        _message = FileFavorites.Toggle(item.Path) ? DialogText.FavoriteAdded : DialogText.FavoriteRemoved;
        SaveFavorites();
    }
}
