namespace Marbles;

public static class FileActions
{
    #region Show file info

    /// <summary>
    /// Show information about the data file in a box on top of the page (the same box as in the file browser),
    /// with the number of entries and the backup directory as well
    /// </summary>
    /// <param name="showEntryCount">True when logged in (the entries can be counted)</param>
    internal static void ShowDataFileInfo(bool showEntryCount)
    {
        FileFavorites.Load();
        var rows = new List<(string Label, string Value)>();
        var path = FileIo.GetCurrentFolderAndFileName();
        if (File.Exists(path))
        {
            var full = Path.GetFullPath(path);
            try
            {
                rows = FileExplorer.InfoRows(Path.GetFileName(full), full, false, new FileInfo(full).Length);
            }
            catch (Exception e)
            {
                rows.Add((DialogText.InfoPath, full));
                rows.Add((DialogText.InfoType, e.Message));
            }

            // After the size
            if (showEntryCount) rows.Insert(Math.Min(3, rows.Count), (DialogText.InfoEntries, Entries.GetEntriesCount().ToString()));
        }
        else
        {
            rows.Add((DialogText.InfoDirectory, Path.GetFullPath(FileIo.GetDataAndSubFolder())));
            rows.Add((DialogText.InfoName, DialogText.NoDataFiles));
        }

        rows.Add((DialogText.InfoBackup, FileIo.GetBackupPathForDisplay(false)));
        ChoiceBox.ShowRows(DialogText.InfoCaptionDataFile, rows);
    }

    #endregion

    #region New file

    // A data file made at the first start (it gets the welcome text), or one made by the user
    internal enum FileType
    {
        Normal,
        NormalUserCreate
    }

    internal static FileType CurrentFileType { get; set; }

    internal static bool NewDataFilePrompt()
    {
        return NewDataFile(FileType.NormalUserCreate);
    }

    private static bool NewDataFile(FileType type)
    {
        CurrentFileType = type;
        // The name in a box on top of the page
        var asked = ChoiceBox.AskText(DialogText.NewFile, DialogText.EnterFileNameAndPressEnter, DialogText.NameColon,
            string.Empty);
        if (asked == null)
        {
            return false;
        }

        string fileName = asked;
        fileName = Tools.TruncateString(fileName, 32, false);
        // Check file name
        var problem = Tools.FileNameProblem(fileName);
        if (problem == null)
        {
            // Add file name extension
            fileName += FileIo.GetFileNameExtension();
            // Check if the file exists
            if (!File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), fileName)))
            {
                Tools.CreateNewDataFile = true;
                FileIo.SetFileName(fileName);
            }
            else
            {
                Menu.StatusMessage(DialogText.DataFileExists, true, true, true, DialogText.NewFile, string.Empty,
                    false);
                return NewDataFile(type);
            }
        }
        else
        {
            Menu.StatusMessage(problem, true, true, true, DialogText.NewFile, string.Empty, false);
            return NewDataFile(type);
        }

        return true;
    }

    #endregion

    #region Rename file

    private static string _oldFileName = string.Empty;
    internal static bool RenameCurrentDataFile()
    {
        if (!FileIo.IsCurrentFileValid()) return false;

        _oldFileName = FileIo.GetFileName();
        if (Menu.YesNoPrompt(DialogText.RenameFile, DialogText.RenameFileYn,
                DialogText.FileColon + " " + FileIo.GetFileName(),
                false))
        {
            // The name in a box on top of the page
            var asked = ChoiceBox.AskText(DialogText.RenameFile, DialogText.ChangeFileNameAndPressEnter,
                DialogText.NameColon, FileIo.GetFileNameWithoutExtension());
            if (asked == null)
            {
                return false;
            }

            string newFileName = asked;
            newFileName = Tools.TruncateString(newFileName, 32, false);
            // Check file name
            var problem = Tools.FileNameProblem(newFileName);
            if (problem == null)
            {
                // Add file name extension
                newFileName += FileIo.GetFileNameExtension();
                // Check if the file exists
                if (!File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), newFileName)))
                {
                    // Move the current file to a new location (This method is not advised as it can have some
                    // access restrictions and doesnt work over networks.
                    //File.Move(GetFileName(), newFileName);
                    // Instead we copy the file to a file with a new name, and delete the old one.
                    try
                    {
                        File.Copy(Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName()),
                            Path.Combine(FileIo.GetDataAndSubFolder(), newFileName),
                            false);
                        // If the new copy exists...
                        if (File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), newFileName)))
                        {
                            // Delete the old file
                            File.Delete(Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName()));
                            // And set the new filename as the current
                            FileIo.SetFileName(newFileName);
                            Init.Log("Rename file: " + _oldFileName + " -> " + FileIo.GetFileName(), false);
                            PlacesFollow(Path.Combine(FileIo.GetDataAndSubFolder(), _oldFileName),
                                Path.Combine(FileIo.GetDataAndSubFolder(), newFileName));
                            return true;
                        }
                    }
                    catch (Exception e) when (e is not NavigationException)
                    {
                        Menu.StatusMessage(DialogText.ErrorUnableToCompleteRequest, true, true, true,
                            DialogText.RenameFile, string.Empty, false);
                        return RenameCurrentDataFile();
                    }
                }
                else
                {
                    Menu.StatusMessage(DialogText.DataFileExists, true, true, true, DialogText.RenameFile,
                        string.Empty, false);
                    return RenameCurrentDataFile();
                }
            }
            else
            {
                Menu.StatusMessage(problem, true, true, true, DialogText.RenameFile, string.Empty,
                    false);
                return RenameCurrentDataFile();
            }
        }

        return false;
    }

    #endregion

    #region Copy file (duplicate)

    internal static bool CopyCurrentDataFile()
    {
        if (!FileIo.IsCurrentFileValid()) return false;
        _oldFileName = FileIo.GetFileName();
        if (Menu.YesNoPrompt(DialogText.CopyFile, DialogText.CopyFileYn,
                DialogText.FileColon + " " + FileIo.GetFileName(),
                false))
        {
            // The name in a box on top of the page
            var asked = ChoiceBox.AskText(DialogText.CopyFile, DialogText.ChangeFileNameAndPressEnter,
                DialogText.NameColon, FileIo.GetFileNameWithoutExtension() + " (copy)");
            if (asked == null)
            {
                return false;
            }

            string newFileName = asked;
            newFileName = Tools.TruncateString(newFileName, 32, false);
            // Check file name
            var problem = Tools.FileNameProblem(newFileName);
            if (problem == null)
            {
                // Add file name extension
                newFileName += FileIo.GetFileNameExtension();
                // Check if the file exists
                if (!File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), newFileName)))
                {
                    try
                    {
                        // Copy the file
                        File.Copy(Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName()),
                            Path.Combine(FileIo.GetDataAndSubFolder(), newFileName),
                            false);
                        // If the new copy exists...
                        if (File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), newFileName)))
                        {
                            // And set the new filename as the current
                            FileIo.SetFileName(newFileName);
                            Init.Log("Copy file: " + _oldFileName + " -> " + FileIo.GetFileName(), false);
                            return true;
                        }
                    }
                    catch (Exception e) when (e is not NavigationException)
                    {
                        Menu.StatusMessage(DialogText.ErrorUnableToCompleteRequest, true, true, true,
                            DialogText.CopyFile, string.Empty, false);
                        CopyCurrentDataFile();
                    }
                }
                else
                {
                    Menu.StatusMessage(DialogText.DataFileExists, true, true, true, DialogText.CopyFile, string.Empty,
                        false);
                    CopyCurrentDataFile();
                }
            }
            else
            {
                Menu.StatusMessage(problem, true, true, true, DialogText.CopyFile, string.Empty,
                    false);
                CopyCurrentDataFile();
            }
        }

        return false;
    }

    #endregion

    #region Move file

    private static bool _lockForFileMove;

    public static bool IsLockedForMove()
    {
        return _lockForFileMove;
    }

    private static string _saveSubfolder = string.Empty;
    private static string _saveFileName = string.Empty;

    private static void SaveSnapshot()
    {
        _saveSubfolder = FileIo.GetSubFolder();
        _saveFileName = FileIo.GetFileName();
    }

    private static bool IsSnapShotValid()
    {
        // Validate saved directory
        if (Tools.IsValid(_saveSubfolder) &&
            Directory.Exists(Path.Combine(FileIo.GetDataDirectoryName(), _saveSubfolder)))
        {
            return File.Exists(Path.Combine(Path.Combine(FileIo.GetDataDirectoryName(), _saveSubfolder),
                _saveFileName));
        }

        // Else return data directory
        return File.Exists(Path.Combine(FileIo.GetDataDirectoryName(), _saveFileName));
    }

    private static string _currentPath = string.Empty;
    private static string _newPath = string.Empty;

    public static void SetLockForMove(bool state, bool restore)
    {
        _lockForFileMove = state;
        // If locked for move set paths
        if (_lockForFileMove)
        {
            // Save current path
            SaveSnapshot();
            _currentPath = FileIo.GetDataAndSubFolder();
        }
        else
        {
            // Restore snapshot when aborting lock
            if (restore && IsSnapShotValid())
            {
                if (Tools.IsValid(_saveSubfolder))
                {
                    FileIo.SetSubFolder(_saveSubfolder);
                    FileBrowser.ResolveSubFolderIndex();
                }
                else
                {
                    FileIo.ResetSubFolder();
                    FileBrowser.ResolveSubFolderIndex();
                }

                FileIo.SetFileName(_saveFileName);
            }

            // Clear path
            _currentPath = string.Empty;
            _newPath = string.Empty;
        }
    }

    public static bool IsMoveDirectoryValid()
    {
        if (Tools.IsValid(_currentPath) && Tools.IsValid(_newPath) && _currentPath != _newPath &&
            Directory.Exists(_newPath))
        {
            return true;
        }

        return false;
    }

    public static void SetNewMovePath()
    {
        if (IsLockedForMove())
        {
            _newPath = FileIo.GetDataAndSubFolder();
        }
    }

    // The bookmarks and the jumps (Recent) of a data file follow it when it is renamed or moved
    private static void PlacesFollow(string oldPath, string newPath)
    {
        try
        {
            oldPath = Path.GetFullPath(oldPath);
            newPath = Path.GetFullPath(newPath);
            Bookmarks.Follow(oldPath, newPath);
            JumpHistory.Load();
            if (JumpHistory.PathChanged(oldPath, newPath)) JumpHistory.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The file is renamed or moved: the lists are not what matters
        }
    }

    internal static void MoveCurrentDataFile()
    {
        // Return if paths are empty
        if ((!Tools.IsValid(_currentPath) || !Tools.IsValid(_newPath)) || (_currentPath == _newPath))
        {
            // Inform that the target path must be different than the source path
            Menu.StatusMessage(DialogText.SelectTargetDirectory, true, true, true,
                DialogText.MoveFile, string.Empty, false);
            return;
        }

        // Only move if locked for move and if paths are different
        if (IsLockedForMove() && (_currentPath != _newPath) && Menu.YesNoPrompt(DialogText.MoveFile,
                DialogText.MoveFileYn,
                DialogText.FileColon + " " + FileIo.GetFileName(),
                false))
        {
            Tools.ClearScreen();
            try
            {
                var currentPathAndFileName = Path.Combine(_currentPath, FileIo.GetFileName());
                var targetPathAndFileName = Path.Combine(_newPath, FileIo.GetFileName());

                // Check that another file with the same name does not exist (ToLower())
                if (File.Exists(targetPathAndFileName))
                {
                    // Inform that the file wasn't copied: another directory can be picked (still locked for move)
                    Menu.StatusMessage(DialogText.ErrorFileAlreadyExist, true, true, true,
                        DialogText.MoveFile, string.Empty, false);
                    return;
                }

                // Copy the file (never over another file: one made since the check above stops the move)
                File.Copy(currentPathAndFileName, targetPathAndFileName, false);
                // If the new copy exists...
                if (File.Exists(targetPathAndFileName))
                {
                    // ...delete the old file to complete the move.
                    File.Delete(currentPathAndFileName);
                    // Log the action
                    Init.Log("Move: " + FileIo.GetFileName(), false);
                    Init.Log("(From: " + _currentPath + ")", false);
                    Init.Log("(To: " + _newPath + ")", false);
                    PlacesFollow(currentPathAndFileName, targetPathAndFileName);
                    // Remove lock and return to login
                    SetLockForMove(false, false);
                }
                else
                {
                    // Inform that the file wasn't copied and return to start
                    Menu.StatusMessage(DialogText.ErrorMovingFile, true, true, true,
                        DialogText.MoveFile, string.Empty, false);
                    MoveCurrentDataFile();
                    return;
                }
            }
            catch (Exception e) when (e is not NavigationException)
            {
                Menu.StatusMessage(DialogText.ErrorUnableToCompleteRequest, true, true, true,
                    DialogText.MoveFile, string.Empty, false);
                MoveCurrentDataFile();
                return;
            }
        }
    }

    #endregion

    #region Delete file

    internal static bool DeleteCurrentDataFile()
    {
        if (!FileIo.IsCurrentFileValid()) return false;
        _oldFileName = FileIo.GetFileName();
        if (Menu.YesNoPrompt(DialogText.DeleteFile, DialogText.DeleteFileYn,
                DialogText.FileColon + " " + FileIo.GetFileName(),
                true))
        {
            // Reconfirm
            if (Menu.YesNoPrompt(DialogText.DeleteFile, DialogText.DeleteEntryYnConfirm,
                    DialogText.NameColon + " " + FileIo.GetFileName() + Entries.NewLineCharString + " " +
                    DialogText.CanNotBeUndone, true))
            {
                // Check if the file exists
                if (File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName())))
                {
                    try
                    {
                        // Delete the file
                        File.Delete(Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName()));
                        // Check that the file is deleted
                        if (!File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), FileIo.GetFileName())))
                        {
                            Menu.StatusMessage(DialogText.FileDeleted, true, true, true, DialogText.DeleteFile,
                                string.Empty, false);
                            // And set the filename to the first in the list
                            if (!string.IsNullOrWhiteSpace(FileBrowser.GetFirstDataFileName()))
                            {
                                FileIo.SetFileName(FileBrowser.GetFirstDataFileName());
                            }
                            else
                            {
                                // Or set default name
                                FileIo.SetDefaultFileName();
                            }
                            Init.Log("Delete file: " + _oldFileName, false);
                            return true;
                        }
                    }
                    catch (Exception e) when (e is not NavigationException)
                    {
                        Menu.StatusMessage(DialogText.ErrorUnableToCompleteRequest, true, true, true,
                            DialogText.DeleteFile, string.Empty, false);
                        DeleteCurrentDataFile();
                    }
                }
                else
                {
                    Menu.StatusMessage(DialogText.InvalidFileName, true, true, true, DialogText.DeleteFile,
                        string.Empty, false);
                    DeleteCurrentDataFile();
                }
            }
        }

        return false;
    }

    #endregion
}