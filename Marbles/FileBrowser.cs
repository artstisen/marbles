namespace Marbles;

public class FileBrowser
{
    // This class is all about iterating content for the
    // file browsing in Marbles.
    // Setting values related to filenames and paths are
    // handled through the FileIo class. This class is just a browser.

    #region Directories

    private static List<string> _subFolders = new();

    // Returns a list of all subfolders within the data directory
    // containing .mbls files. Nested folders are not included
    internal static void RefreshSubfoldersList()
    {
        _subFolders.Clear();
        try
        {
            // If the data directory is valid and contains sub directories
            if (FileIo.CheckDataDirectory() && Directory.GetDirectories(FileIo.GetDataDirectoryName()).Length > 0)
            {
                DirectoryInfo di = new DirectoryInfo(FileIo.GetDataDirectoryName());
                DirectoryInfo[] directories =
                    di.GetDirectories("*", SearchOption.TopDirectoryOnly);

                // Iterate directories and add directories that contains .mbls files
                foreach (DirectoryInfo dir in directories)
                {
                    _subFolders.Add(dir.Name);
                }

                // Sort list
                if (_subFolders.Count > 0)
                {
                    _subFolders.Sort(string.CompareOrdinal);
                }
            }
            else
            {
                // Clear current subfolder string when there are no subfolders
                FileIo.ResetSubFolder();
            }
        }
        catch
        {
            // do nothing
        }
    }

    internal static bool HasSubFolders()
    {
        return _subFolders.Count > 0;
    }

    internal static void ResetSubfolderAndIndex()
    {
        FileIo.ResetSubFolder();
        _subFolderIndex = -1;
    }

    private static bool IsCurrentPathDataRoot()
    {
        return !Tools.IsValid(FileIo.GetSubFolder());
    }

    private static bool IsCurrentSubfolderValid()
    {
        if (_subFolders.Count > 0 && Tools.IsValid(FileIo.GetSubFolder()))
        {
            foreach (string dir in _subFolders)
            {
                if (FileIo.GetSubFolder() == dir)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // internal static string GetSubfolderCount()
    // {
    //     // Refresh folders
    //     CheckSubfolders();
    //     if (_subFolderIndex >= 0)
    //     {
    //         return (_subFolderIndex + 1) + "/" + _subFolders.Count + " ";
    //     }
    //     else
    //     {
    //         return string.Empty;
    //     }
    // }

    private static int _subFolderIndex = -1;

    // Used from init: Match current subfolder to get the index (used when loading settings to match index for selection)
    internal static void ResolveSubFolderIndex()
    {
        RefreshSubfoldersList();
        _subFolderIndex = -1;
        if (HasSubFolders())
        {
            for (int i = 0; i < _subFolders.Count; i++)
            {
                if (_subFolders[i] == FileIo.GetSubFolder())
                {
                    _subFolderIndex = i;
                }
            }
        }
    }   

    // Directory pagination
    internal static void NextSubfolder()
    {
        // Next subfolder or back to first
        if (_subFolders.Count > 0 && (_subFolderIndex + 1 < _subFolders.Count))
        {
            _subFolderIndex += 1;
            FileIo.SetSubFolder(_subFolders[_subFolderIndex]);
        }
        else if (_subFolders.Count > 0 && (_subFolderIndex + 1 == _subFolders.Count))
        {
            _subFolderIndex = -1;
            FileIo.ResetSubFolder();
        }
        else if (_subFolders.Count > 0 && _subFolderIndex == -1)
        {
            _subFolderIndex = 0;
            FileIo.SetSubFolder(_subFolders[_subFolderIndex]);
        }

        FileActions.SetNewMovePath();
        Tools.LoginRoutine(false);
    }


    internal static void PreviousSubfolder()
    {
        // Previous subfolder or last
        if (_subFolders.Count > 0 && _subFolderIndex > 0 && (_subFolderIndex - 1 >= 0))
        {
            _subFolderIndex -= 1;
            FileIo.SetSubFolder(_subFolders[_subFolderIndex]);
        }
        else if (_subFolders.Count > 0 && _subFolderIndex == 0)
        {
            _subFolderIndex = -1;
            FileIo.ResetSubFolder();
        }
        else if (_subFolders.Count > 0 && _subFolderIndex == -1)
        {
            _subFolderIndex = _subFolders.Count - 1;
            FileIo.SetSubFolder(_subFolders[_subFolderIndex]);
        }

        FileActions.SetNewMovePath();
        Tools.LoginRoutine(false);
    }

    #endregion


    #region Files

    private static int _currentDataFileIndex;

    internal static void SelectNextDataFile()
    {
        if (GetDataFileCount() <= 1) return;
        // Jump to start if next index is higher than the list count
        if (_currentDataFileIndex + 1 > _dataFilesList.Count - 1)
        {
            _currentDataFileIndex = 0;
            SetDataFileName(_dataFilesList[_currentDataFileIndex]);
            Tools.LoginRoutine(false);
        }
        else
        {
            _currentDataFileIndex += 1;
            SetDataFileName(_dataFilesList[_currentDataFileIndex]);
            Tools.LoginRoutine(false);
        }
    }

    internal static void SelectPreviousDataFile()
    {
        if (GetDataFileCount() <= 1) return;
        // Jump to end of the list if the next index is lower than zero
        if (_currentDataFileIndex - 1 < 0)
        {
            _currentDataFileIndex = _dataFilesList.Count - 1;
            SetDataFileName(_dataFilesList[_currentDataFileIndex]);
            Tools.LoginRoutine(false);
        }
        else
        {
            _currentDataFileIndex -= 1;
            SetDataFileName(_dataFilesList[_currentDataFileIndex]);
            Tools.LoginRoutine(false);
        }
    }

    private static void SetDataFileName(string name)
    {
        // Extract the file name from the directory path and set as current file name
        FileIo.SetFileName(name);
    }


    private static int _tempDataFileTotalCount;

    internal static string GetDataFileCountForDisplay()
    {
        _tempDataFileTotalCount = GetDataFileCount(); // Will populate the _dataFilesList
        if (_tempDataFileTotalCount > 0)
        {
            for (int i = 0; i < _dataFilesList.Count; i++)
            {
                // Compare case insensitive
                if (_dataFilesList[i].ToLower() == FileIo.GetFileName().ToLower())
                {
                    // Update local file name to show correct casing (windows uses case insensitive
                    // filenames where linux uses case sensitive.
                    SetDataFileName(_dataFilesList[i]);
                    _currentDataFileIndex = i;
                    return (_currentDataFileIndex + 1) + "/" + _tempDataFileTotalCount;
                }
            }

            return DialogText.NotAvailable;
        }
        else
        {
            return DialogText.NotAvailable;
        }
    }
    
    


    private static List<string> _dataFilesList = new();

    internal static int FilesInDirectory()
    {
        return Directory.EnumerateFiles(FileIo.GetDataAndSubFolder(), "*" + FileIo.GetFileNameExtension()).Count();
    }

    private static int GetDataFileCount()
    {
        _dataFilesList.Clear();
        try
        {
            if (FileIo.CheckCurrentDirectory())
            {
                _dataFilesList.AddRange(Directory.EnumerateFiles(FileIo.GetDataAndSubFolder(),
                    "*" + FileIo.GetFileNameExtension()));
                // Sort the list alphabetically
                if (_dataFilesList.Count > 0)
                {
                    // Isolate file names and sort the list
                    for (int i = 0; i < _dataFilesList.Count; i++)
                    {
                        _dataFilesList[i] = Path.GetFileName(_dataFilesList[i]);
                    }

                    _dataFilesList.Sort(string.CompareOrdinal);
                }
            }
        }
        catch
        {
            _dataFilesList.Clear();
        }

        return _dataFilesList.Count;
    }

    internal static string GetFirstDataFileName()
    {
        GetDataFileCount();
        return _dataFilesList.Count > 0 ? _dataFilesList[0] : string.Empty;
    }


    internal static bool ResolveDefaultDataFile()
    {
        // If the current selected data file does not exist
        // solve it by checking the files and folders to select 
        // another file if possible or create the default data file
        if (!FileIo.IsCurrentFileValid())
        {
            // Refresh subfolder list
            RefreshSubfoldersList();
            // Reset sub folder if invalid (this will default to the root/data folder)
            if (!IsCurrentSubfolderValid())
            {
                ResetSubfolderAndIndex();
            }

            // Get the first name of the current list
            if (!string.IsNullOrWhiteSpace(GetFirstDataFileName()))
            {
                FileIo.SetFileName(GetFirstDataFileName());
                return true;
            }

            // If standing in the root/data directory with no subfolders set default name
            if (IsCurrentPathDataRoot() && !HasSubFolders())
            {
                // Or set default name
                FileIo.SetDefaultFileName();
                return false;
            }
            
            // Set no file
            FileIo.SetNoFileName();
            return true;
            
        }

        return true;
    }

    #endregion
}