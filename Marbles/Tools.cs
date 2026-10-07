using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

internal static class Tools
{
    public static string TruncateString(string s, int length, bool dots)
    {
        if (!IsValid(s)) return string.Empty;
        if (s.Length >= length)
        {
            return dots ? string.Concat(s.AsSpan(0, (length - 3)), "...") : s[..length];
        }

        return s;
    }


    public static string[] SplitStringOnNewLine(string input, bool trim)
    {
        if (IsValid(input))
        {
            if (trim)
            {
                input = input.Trim();
            }

            return input.Split(
                new[] {Entries.ReturnNewLineCharString, Entries.ReturnCharString, Entries.NewLineCharString},
                StringSplitOptions.None);
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    public static List<string> SplitStringOnNewLine(string input)
    {
        if (IsValid(input))
        {
            var tempList = new List<string>();
            tempList.AddRange(input.Split(
                new[] {Entries.ReturnNewLineCharString, Entries.ReturnCharString, Entries.NewLineCharString},
                StringSplitOptions.None));

            return tempList;
        }
        else
        {
            return new List<string>();
        }
    }

    // This will split a string on several parameters and only fetch the first instance which will be limited to 256 chars
    public static string ConstrainTitle(string input)
    {
        if (IsValid(input))
        {
            input = input.Trim();
            input = input.Split(
                new[] {Entries.ReturnNewLineCharString, Entries.ReturnCharString, Entries.NewLineCharString},
                StringSplitOptions.None)[0];
            if (input.Length > 256)
            {
                input = input[..255];
            }

            return input;
        }
        else
        {
            return string.Empty;
        }
    }

    public static string RemoveNewLineChars(string input)
    {
        if (IsValid(input))
        {
            input = input.Replace("\n", string.Empty);
            input = input.Replace("\r", string.Empty);
            input = input.Replace("\r\n", string.Empty);
        }

        return input;
    }

    public static string TrimToLower(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        input = input.Trim();
        input = input.ToLower();
        return input;
    }

    // Not null or empty
    public static bool IsValid(string input)
    {
        return !string.IsNullOrEmpty(input);
    }

    public static bool IsValidFileName(string input)
    {
        if (!IsValid(input)) return false;
        var invalidChars = Path.GetInvalidFileNameChars();
        return invalidChars.All(c => !input.Contains(c));
    }

    public static bool IsValidPath(string input)
    {
        if (!IsValid(input)) return false;
        var invalidChars = Path.GetInvalidPathChars();
        return invalidChars.All(c => !input.Contains(c));
    }

    public static bool IsValidXml(string input)
    {
        if (IsValid(input) && input.Contains(FileIo.XmlDoctype))
        {
            return true;
        }
        return false;
    }


    public static bool IsNumeric(string s)
    {
        return Regex.IsMatch(s, @"^\d+$");
    }


    internal static int WordCount(string text)
    {
        int wordCount = 0, index = 0;

        // skip whitespace until first word
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        while (index < text.Length)
        {
            // check if current char is part of a word
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
                index++;

            wordCount++;

            // skip whitespace until next word
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        return wordCount;
    }

    internal static int CharacterCount(string text)
    {
        int charCount = 0, index = 0;

        // skip whitespace until first word
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        while (index < text.Length)
        {
            //check if current char is part of a word
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
                charCount++;
            }

            // skip whitespace until next word
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        return charCount;
    }

    internal static int LineCount(string text)
    {
        if (!string.IsNullOrEmpty(text) && text.Contains(Entries.NewLineCharString))
        {
            return SplitStringOnNewLine(text, false).Length;
        }

        return 0;
    }


    private static string _tempCenterScreenText = string.Empty;

    public static void WriteCenterScreen(string text)
    {
        _tempCenterScreenText = text;
        Tools.CursorTopLeft();
        Tools.CursorRight((Init.LineWidth / 2) - (_tempCenterScreenText.Length / 2));
        Tools.CursorDown(Init.LineCount / 2);
        Console.Write(_tempCenterScreenText);
    }

    public static void WriteCenterScreen(string[] text)
    {
        var lineWidth = Init.LineWidth / 2;
        var lineCount = Init.LineCount / 2;
        lineCount -= (text.Length / 2);
        CursorTopLeft();
        CursorDown(lineCount);
        for (int i = 0; i < text.Length; i++)
        {
            CursorRight((lineWidth) - (text[i].Length / 2));
            Console.Write(text[i]);
            CursorTopLeft();
            lineCount += 1;
            CursorDown(lineCount);
        }
    }


    // Mask password
    private const int MaxMaskedDisplay = 32; // The number of "*" shown (the width of the login field)
    private static ConsoleKeyInfo _keyInfo;

    // Remove the last "*" from the password field
    private static void EraseMaskedChar()
    {
        // Go back, write blank, go back -same as "\b \b", but cross platform compatible
        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
        Console.Write(" ");
        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
    }
    private static bool _exitLoop;
    private static string _pass = string.Empty;

    private static string ReadMasked(bool enableShortcuts)
    {
        _pass = string.Empty;
        _exitLoop = false;
        // Open the options menu again after a file or directory was selected from it
        if (enableShortcuts && _reopenLoginOptions && !FileActions.IsLockedForMove())
        {
            ShowLoginOptions();
        }

        do
        {
            ShowCursor();
            _keyInfo = Console.ReadKey(intercept: true);
            HideCursor();
            var key = _keyInfo.Key;

            // Esc in an empty password field opens the options menu
            if (enableShortcuts && key == ConsoleKey.Escape && !FileActions.IsLockedForMove() && _pass.Length == 0)
            {
                ShowLoginOptions();
            }
            else if (key == ConsoleKey.Enter)
            {
                if (FileActions.IsLockedForMove())
                {
                    FileActions.MoveCurrentDataFile();
                    LoginRoutine(true);
                }
                else
                {
                    _exitLoop = true;
                }
            }
            else if (key == ConsoleKey.Tab && _keyInfo.Modifiers == ConsoleModifiers.Shift)
            {
                if (!FileActions.IsLockedForMove())
                {
                    // Previous datafile (SHIFT+TAB)
                    FileBrowser.SelectPreviousDataFile();
                }
            }
            else if (enableShortcuts && key == ConsoleKey.LeftArrow)
            {
                if (!FileActions.IsLockedForMove())
                {
                    // Previous datafile
                    FileBrowser.SelectPreviousDataFile();
                }
            }
            else if (enableShortcuts && key == ConsoleKey.RightArrow || key == ConsoleKey.Tab)
            {
                if (!FileActions.IsLockedForMove())
                {
                    // Next datafile
                    FileBrowser.SelectNextDataFile();
                }
            }
            else if (enableShortcuts && key == ConsoleKey.UpArrow)
            {
                // Next sub directory
                FileBrowser.NextSubfolder();
            }
            else if (enableShortcuts && key == ConsoleKey.DownArrow)
            {
                // Previous sub directory
                FileBrowser.PreviousSubfolder();
            }
            else if (enableShortcuts && key == ConsoleKey.Spacebar)
            {
                // Lock for move
                if (IsValid(FileIo.GetFileName()) && FileBrowser.HasSubFolders() && !FileActions.IsLockedForMove())
                {
                    FileActions.SetLockForMove(true, false);
                }
                else if(FileActions.IsLockedForMove())
                {
                    FileActions.SetLockForMove(false, true);
                }

                LoginRoutine(false);
            }
            else if (enableShortcuts && key == ConsoleKey.Escape)
            {
                if (FileActions.IsLockedForMove())
                {
                    FileActions.SetLockForMove(false, true);
                    LoginRoutine(false);
                }
                // Return to root/data directory
                else if (FileIo.GetSubFolder().Length > 0)
                {
                    // Remove subfolder at select first file in the root/data directory
                    FileBrowser.ResetSubfolderAndIndex();
                    FileBrowser.ResolveDefaultDataFile();
                    LoginRoutine(false);
                }
                else if(_pass.Length > 0)
                {
                    for (int i = 0; i < Math.Min(_pass.Length, MaxMaskedDisplay); i++)
                    {
                        EraseMaskedChar();
                    }
                    // Clear password variable
                    _pass = string.Empty;
                }
            }
            else if (key == ConsoleKey.Backspace && _pass.Length > 0)
            {
                if (_pass.Length <= MaxMaskedDisplay) EraseMaskedChar();
                _pass = _pass[0..^1];
            }
            else if (!FileActions.IsLockedForMove() && !char.IsControl(_keyInfo.KeyChar) && key != ConsoleKey.Spacebar &&
                     _pass.Length < Encryptor.KeyMaxLength)
            {
                // A long password is not shown in its full length
                if (_pass.Length < MaxMaskedDisplay) Console.Write("*");
                _pass += _keyInfo.KeyChar;
            }
        } while (!_exitLoop);

        return _pass;
    }

    private static bool _reopenLoginOptions;

    // The options of the login page: shown in a menu, where the next key selects an option
    private static void ShowLoginOptions()
    {
        _reopenLoginOptions = false;
        var tempShortcuts = new List<Shortcut>
        {
            new("I", DialogText.FileInfo, false, 13),
            new("N", DialogText.NewFile, false, 13),
            new("X", DialogText.RenameFile, false, 13),
            new("C", DialogText.CopyFile, false, 13),
            new("D", DialogText.DeleteFile, false, 13),
            new("M", DialogText.MergeFiles, false, 13),
            new("F", DialogText.BrowseFiles, false, 13),
            new(string.Empty, "-", true, 13),
            new("h l / j k", DialogText.SelectFileDirectory, false, 13),
            new("R/Y", DialogText.Refresh + "/" + DialogText.Restart, false, 13),
            new("G/S", DialogText.Log + "/" + FileIo.SettingsFileName, false, 13),
            new("Q", DialogText.Exit, false, 13)
        };
        Menu.FloatingMenu(DialogText.CaptionsOptions, tempShortcuts, 9, 14, false);

        // Get second key
        _keyInfo = Console.ReadKey(true);
        if (_keyInfo.Key == ConsoleKey.R)
        {
            // Refresh
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.Y)
        {
            // Restart
            Restart();
        }
        else if (_keyInfo.Key == ConsoleKey.I)
        {
            // File info
            FileActions.ShowDataFileInfo(false);
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.N)
        {
            // New data file
            if (FileActions.NewDataFilePrompt())
            {
                Init.Log("New file: " + FileIo.GetFileName(), false);
            }

            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.X)
        {
            // Rename file
            FileActions.RenameCurrentDataFile();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.C)
        {
            // Copy file
            FileActions.CopyCurrentDataFile();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.D)
        {
            // Delete file
            FileActions.DeleteCurrentDataFile();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.M)
        {
            // Merge files
            MergeTool merge = new MergeTool();
            merge.MergeExport();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.F)
        {
            // Browse and edit text files
            FileExplorer.Run();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.G)
        {
            // Log
            Init.WriteLog();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.S)
        {
            // Settings
            Mode.SetMode(Mode.SelectedMode.Edit);
            ClearScreen();
            Actions.DrawEditSettingsMenu();
            TextArea textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                Init.LineCount - 5, FileIo.ReadSettingsFile(), 0, false, false, false, 1);
            // Cleanup
            textArea.ClearUndoRedoCache();
            // Restart if settings where saved to reload with new settings
            if (textArea.SaveWasInvoked)
            {
                ColdRestart();
            }
            else
            {
                LoginRoutine(true);
            }
        }
        else if (_keyInfo.Key == ConsoleKey.Q)
        {
            // Exit directly
            ExitApplication();
        }
        else if (_keyInfo.Key == ConsoleKey.H)
        {
            // Previous datafile (and keep the options open)
            _reopenLoginOptions = true;
            FileBrowser.SelectPreviousDataFile();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.L)
        {
            // Next datafile
            _reopenLoginOptions = true;
            FileBrowser.SelectNextDataFile();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.K)
        {
            // Next sub directory
            _reopenLoginOptions = true;
            FileBrowser.NextSubfolder();
            LoginRoutine(true);
        }
        else if (_keyInfo.Key == ConsoleKey.J)
        {
            // Previous sub directory
            _reopenLoginOptions = true;
            FileBrowser.PreviousSubfolder();
            LoginRoutine(true);
        }
        else
        {
            // Close the options
            LoginRoutine(true);
        }
    }

    internal static string ReadMaskedSimple(bool enableCancelCommand)
    {
        _pass = string.Empty;
        _exitLoop = false;

        do
        {
            ShowCursor();
            _keyInfo = Console.ReadKey(intercept: true);
            HideCursor();
            var key = _keyInfo.Key;

            // Handle all trigger keys first
            if (key == ConsoleKey.Enter)
            {
                _exitLoop = true;
            }
            else if (enableCancelCommand && key == ConsoleKey.Escape)
            {
                // Indicate cancel
                _pass = DialogText.CancelCommand;
                _exitLoop = true;
            }
            else if (key == ConsoleKey.Backspace && _pass.Length > 0)
            {
                if (_pass.Length <= MaxMaskedDisplay) EraseMaskedChar();
                _pass = _pass[0..^1];
            }
            else if (!char.IsControl(_keyInfo.KeyChar) && key != ConsoleKey.Spacebar &&
                     _pass.Length < Encryptor.KeyMaxLength)
            {
                if (_pass.Length < MaxMaskedDisplay) Console.Write("*");
                _pass += _keyInfo.KeyChar;
            }
        } while (!_exitLoop);

        return _pass;
    }


    #region String memory (Internal clipboard)

    private const int MaxLineMemoryCount = 64;
    private static List<string> _stringMemory = new();
    private static int _currentLineMemoryIndex;

    internal static void NextLineMemoryIndex()
    {
        if (_stringMemory.Count <= 0) return;
        if (_currentLineMemoryIndex + 1 < _stringMemory.Count)
        {
            _currentLineMemoryIndex += 1;
        }
        else
        {
            _currentLineMemoryIndex = 0;
        }
    }

    internal static void PreviousLineMemoryIndex()
    {
        if (_stringMemory.Count > 0)
        {
            if (_currentLineMemoryIndex - 1 >= 0)
            {
                _currentLineMemoryIndex -= 1;
            }
            else
            {
                _currentLineMemoryIndex = _stringMemory.Count - 1;
            }
        }
    }

    internal static void ClearLineMemory()
    {
        if (_stringMemory.Count > 0)
        {
            for (int i = 0; i < _stringMemory.Count; i++)
            {
                _stringMemory[i] = DialogText.Cleared;
            }
        }

        _stringMemory.Clear();
        _currentLineMemoryIndex = 0;
    }

    /// <summary>
    /// Add a string to the string memory and make it the current one.
    /// A string already in memory is not added again, but becomes the current one.
    /// </summary>
    internal static void StoreInLineMemory(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var index = _stringMemory.IndexOf(text);
        if (index >= 0)
        {
            _currentLineMemoryIndex = index;
            return;
        }

        _stringMemory.Add(text);
        LimitLineMemory();
        _currentLineMemoryIndex = _stringMemory.Count - 1;
    }

    /// <summary>
    /// The number of the current string and the total count, like "2/5"
    /// </summary>
    internal static string GetLineMemoryPosition()
    {
        return (_currentLineMemoryIndex + 1) + "/" + _stringMemory.Count;
    }

    private static void LimitLineMemory()
    {
        if (_stringMemory.Count > MaxLineMemoryCount)
        {
            _stringMemory.RemoveAt(0);
        }
    }

    internal static bool HasLineMemory()
    {
        if (_stringMemory.Count > 0)
        {
            return true;
        }

        return false;
    }

    internal static string GetCurrentLineFromLineMemory()
    {
        if (_stringMemory.Count > 0 && _currentLineMemoryIndex >= 0 && _currentLineMemoryIndex < _stringMemory.Count)
        {
            return _stringMemory[_currentLineMemoryIndex];
        }
        else
        {
            return string.Empty;
        }
    }

    internal static List<string> GetAllLinesFromLineMemory()
    {
        return _stringMemory;
    }

    #endregion


    internal static int GetTitleEditWidth()
    {
        return Init.LineWidth - 15;
    }

    internal static int GetTitlePreviewWidth()
    {
        return Init.LineWidth - 20;
    }

    internal static int GetTitleListWidth()
    {
        return Init.LineWidth - 30;
    }


    internal static void Restart()
    {
        if (Menu.YesNoPrompt(DialogText.Restart, DialogText.RestartYn, string.Empty, true))
        {
            Init.Log(" *** " + DialogText.Restart + " *** ", false);
            RemovePrivileges(true);
            ClearEditorHelpData();
            // Clear transfer entry
            Entries.ClearTransfer(false);
            Init.RunInitialization();
            Menu.DrawTopMenu(true, true, false);
        }
        else
        {
            Menu.DrawTopMenu(false, true, false);
        }
    }

    internal static void ColdRestart()
    {
        Init.Log(" *** " + DialogText.Restart + " *** ", false);
        RemovePrivileges(true);
        ClearEditorHelpData();
        // Clear transfer entry
        Entries.ClearTransfer(false);
        Init.RunInitialization();
        Menu.DrawTopMenu(true, true, false);
    }

    internal static void FatalError(string errorMsg)
    {
        Console.Clear();
        Console.WriteLine("");
        Menu.WriteModeLabel(DialogText.CaptionsKeyError, DialogText.ErrorUnsupportedAction, 0);
        Menu.WriteNameInTopBar(true);
        CursorDown(2);
        Keyboard.AwaitAnyKey();
        // Start over
        ColdRestart();
    }

    internal static void ActionUnsupported(string errorMsg)
    {
        Console.Clear();
        Console.WriteLine("");
        Menu.WriteModeLabel(DialogText.CaptionsKeyError, DialogText.ErrorUnsupportedAction, 0);
        Menu.WriteNameInTopBar(true);
        CursorDown(2);
        Keyboard.AwaitAnyKey();
    }


    #region Login/Password

    // There is only a "LoginRoutine" not a "logout" as this method provides both
    private static LoadStatus _load = new();
    internal static bool CreateNewDataFile;

    /// <summary>
    /// Go to the login page (this logs out of the current data file)
    /// </summary>
    /// <param name="fullRedraw">False to only refresh the file and directory selection</param>
    internal static void LoginRoutine(bool fullRedraw)
    {
        Navigation.LoginFullRedraw = fullRedraw;
        // Sweep the logo when arriving at the login page from elsewhere (logging out)
        if (Mode.CurrentMode != Mode.SelectedMode.Login) Logo.AnimateNext();
        Mode.SetMode(Mode.SelectedMode.Login);
        Navigation.Wait();
    }

    /// <summary>
    /// Show the login page and read the password. Called by the main loop when in Login mode.
    /// </summary>
    internal static void ShowLogin(bool fullRedraw)
    {
        // Follow the size of the terminal window (if automatic dimensions are on)
        if (Init.ApplyAutoSize()) fullRedraw = true;
        if (fullRedraw)
        {
            ClearScreen();
        }

        Mode.SetMode(Mode.SelectedMode.Login);
        RemovePrivileges(fullRedraw);

        // Start by checking if the data file exists
        // If the current file is not available try to select the next available
        if (!FileActions.IsLockedForMove() && (CreateNewDataFile || !FileBrowser.ResolveDefaultDataFile()))
        {
            CreateNewDataFile = false;
            // Calendar related
            // Only display welcome text if normal filetype
            if (FileActions.CurrentFileType == FileActions.FileType.Normal)
            {
                DisplayWelcomeText();
            }

            NewPasswordRoutine(false);
        }
        else
        {
            if (fullRedraw)
            {
                // Draw logo
                Logo.DrawLogo(true, Logo.TakeAnimation());
                CursorTopLeftDown(5);
                // Draw login menu
                CursorReturn();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                CursorDown(1);
                CursorRight(2);
                Console.Write(AsciiStyles.GetTopCorner() + new string('-', 34) + AsciiStyles.GetTopCorner());
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
                CursorReturn();
                CursorRight(4);
                Console.Write(" " + DialogText.CaptionsLogin + " ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                CursorReturn();
                CursorDown();
                CursorRight(2);
                Console.Write("|" + new string(' ', 34) + "|");
                CursorReturn();
                CursorDown();
                CursorRight(2);
                Console.Write(AsciiStyles.GetBottomCorner() + new string('-', 34) + AsciiStyles.GetBottomCorner());
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                CursorReturn();
                CursorDown(2);
            }
            else
            {
                // Clear password field when browsing files
                CursorTopLeftDown(7);
                CursorRight(3);
                Console.Write(new string(' ', 32));
            }

            // File browser
            CursorTopLeftDown(10);
            // Clear the bottom menu lines for file- and directory names if not a full redraw
            if (!fullRedraw)
            {
                Menu.WriteDataFileName();
                for (int i = 0; i < 6; i++)
                {
                    Console.Write(new string(' ', Init.LineWidth));
                    if (i < 5)
                    {
                        CursorReturn();
                        CursorDown();
                    }
                }

                CursorReturn();
                CursorUp(5);
            }


            // Write file name
            if (FileActions.IsLockedForMove())
            {
                // Write file selection
                Menu.WriteMenuItem(DialogText.PaginationLeftRight,
                    DialogText.ExtSelectDataFile + " --> ", false);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(" " + TruncateString(FileIo.GetFileName(), 35, true) + " ");
                // Write file size
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write(" Move ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" -->");
            }
            else
            {
                // Write file selection
                Menu.WriteMenuItem(DialogText.PaginationLeftRight,
                    DialogText.ExtSelectDataFile + " " + FileBrowser.GetDataFileCountForDisplay() + " ", false);
                if (FileBrowser.FilesInDirectory() > 0)
                {
                    // Write file name
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                    Console.Write(" " + TruncateString(FileIo.GetFileName(), 35, true) + " ");
                    // Write file size
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(" (" + FileIo.GetFileSize(false) + ")");
                }
                else
                {
                    // Empty directory
                    FileIo.SetNoFileName();
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(" " + TruncateString(DialogText.NoDataFiles, 35, true) + " ");
                }
            }

            // Ensure normal colors
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            CursorReturn();
            CursorDown();

            // Directory selection
            FileBrowser.RefreshSubfoldersList();
            if (!FileBrowser.HasSubFolders()) Menu.DimMenuItem = true;
            Menu.WriteMenuItem(DialogText.BrowseUpDown,
                DialogText.ExtSelectDirectory + ": ",
                false); // Removed the GetSubfolderCount() as it clutters the scene
            
            if (!FileBrowser.HasSubFolders()) ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            else
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
            }
            Console.Write(TruncateString(FileIo.GetDataAndSubFolderForDisplay(), 35, true));
            if (FileBrowser.FilesInDirectory() == 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" " + DialogText.EmptyDirectory);
            }
            // Move indicator
            if (FileActions.IsLockedForMove())
            {
                if (!FileActions.IsMoveDirectoryValid())
                {
                    Console.Write(" ");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.WarningInverted);
                    Console.Write(" X ");
                }
                else if (FileActions.IsMoveDirectoryValid())
                {
                    Console.Write(" ");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                    Console.Write(" <-- Enter ");
                }
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            if (FileActions.IsLockedForMove())
            {
                // Options disabled
                CursorReturn();
                CursorDown(2);
                Console.Write(new string(' ', 2) + DialogText.FileMoveInstructions);
                CursorReturn();
                CursorDown();
                Console.Write(new string(' ', 2) + DialogText.PressEscSpaceToExit);
            }
            else
            {
                CursorReturn();
                CursorDown();
                
                // Additional menu items
                if (!FileBrowser.HasSubFolders()) Menu.DimMenuItem = true;
                Menu.WriteMenuItem(DialogText.Spacebar, DialogText.MoveFile, true);
                Menu.WriteMenuItem(DialogText.Escape, DialogText.Clear + "/" + DialogText.CaptionsMoreOptions, true);
            }

            // Place cursor at login box
            CursorTopLeft();
            CursorDown(7);
            CursorRight(4);
            // Login
            // Else ask for current password
            Encryptor.SetKey(ReadMasked(true)); // Set password
            _pass = string.Empty; // Clear password stored from the masked reader
            ClearScreen();
            // Indicate loading
            // Try to load file
            Console.WriteLine(DialogText.Loading);
            _load = new LoadStatus();
            _load = FileIo.LoadEntries();
            if (_load.Success)
            {
                Init.Log("Login OK: " + FileIo.GetFolderAndFileNameWithSizeForDisplay(), false);
                // Convert data files from older versions
                if (_load.IsLegacyFormat)
                {
                    ConvertLegacyDataFile();
                }

                // Save settings to update the current loaded data file name
                FileIo.SaveSettings();
                Vignette.CheckVignette();
                Mode.SetMode(Mode.SelectedMode.Manage);
                Menu.DisplayWelcomeMessage = true;
                Menu.DrawTopMenu(true, true, false);
            }
            else if (!_load.Success)
            {
                Init.Log("Login FAILED: "  + FileIo.GetFolderAndFileNameWithSizeForDisplay(), false);
                if (_load.StatusMessage == DialogText.FileNotFound)
                {
                    Menu.StatusMessage(_load.StatusMessage, true, true, true, DialogText.CaptionsLogin, string.Empty,
                        false);
                    LoginRoutine(true);
                }
                else
                {
                    if (_load.StatusMessage.Contains(DialogText.InvalidPasswordDot))
                    {
                        ClearScreen();
                        Menu.WriteModeLabel(DialogText.CaptionsLogin, string.Empty, 0);
                        CursorDown(2);
                        Menu.StatusMessage(_load.StatusMessage, true, false, false, string.Empty, string.Empty, false);
                        LoginRoutine(true);
                    }
                    else
                    {
                        Menu.StatusMessage(_load.StatusMessage, true, true, false, string.Empty, string.Empty, false);
                        LoginRoutine(true);
                    }
                }
            }
        }

        // If load successful
        Mode.SetMode(Mode.SelectedMode.Manage);
        Menu.DrawTopMenu(true, true, false);
    }


    internal static void ClearScreen()
    {
        Console.Clear();
    }

    // Remove privileges but keep transfer entry
    internal static void RemovePrivileges(bool clearScreen)
    {
        if (clearScreen)
        {
            ClearScreen();
        }

        ClearLineMemory();
        Entries.ClearAllEntries(true);
        Entries.ResetCurrentEntry();
        Entries.ResetRecallId();
        Entries.ResetBackupEntry();
        Entries.ResetDeletedEntry();
        Entries.ClearTempRankedEntries();
        Entries.ClearSessionEntryIdList();
        Encryptor.RemoveKey();
        Actions.ClearSearchQuery();
    }


    public static void ExitApplicationQuestion()
    {
        if (Menu.YesNoPrompt(DialogText.Exit, DialogText.ExitYn, string.Empty, true))
        {
            ExitApplication();
        }
        else
        {
            // If NO return to the current mode
            switch (Mode.CurrentMode)
            {
                case Mode.SelectedMode.Login:
                    LoginRoutine(true);
                    break;
                case Mode.SelectedMode.Manage:
                    Menu.DrawTopMenu(true, true, false);
                    break;
                case Mode.SelectedMode.View:
                    Mode.SetMode(Mode.SelectedMode.Manage);
                    Menu.DrawTopMenu(true, true, false);
                    break;
                case Mode.SelectedMode.Search:
                    Mode.SetMode(Mode.SelectedMode.Manage);
                    Menu.DrawTopMenu(true, true, false);
                    break;
                default:
                    LoginRoutine(true);
                    break;
            }
        }
    }

    private static void ExitApplication()
    {
        RemovePrivileges(false);
        ShowCursor(); // Remember to show cursor again for continuous work in the console
        // Exit the application reverting to the original console colors
        Console.ResetColor();
        ClearScreen();
        Console.WriteLine("*** " + DialogText.ApplicationName + " " + DialogText.Exit.ToLower() + " ***");
        Environment.Exit(0);
    }


    // Save a data file from an older version in the current file format (the entries are already loaded)
    private static void ConvertLegacyDataFile()
    {
        var status = FileIo.ConvertLegacyFile();
        if (status == DialogText.Success)
        {
            Init.Log("Converted: " + FileIo.GetFileName() + " (copy: " + FileIo.LegacyCopyFileName + ")", false);
            Menu.StatusMessage(DialogText.LegacyFileConverted, true, true, true, DialogText.CaptionsLogin,
                DialogText.LegacyFileCopyColon + FileIo.LegacyCopyFileName, false);
        }
        else
        {
            Init.Log("Convert FAILED: " + FileIo.GetFileName(), false);
            Menu.StatusMessage(status, true, true, true, DialogText.CaptionsLogin,
                DialogText.LegacyFileNotConverted, false);
        }
    }

    private static string _newPass = string.Empty;
    private static string _newPassRepeat = string.Empty;

    private static void NewPasswordRoutine(bool isPasswordReset)
    {
        ClearScreen();
        Mode.SetMode(Mode.SelectedMode.Login);
        _newPass = string.Empty;
        _newPassRepeat = string.Empty;

        // If not ask for a new password
        Menu.WriteModeLabel(DialogText.NewPassword, string.Empty, 0);
        CursorDown(2);
        Console.Write(" " + DialogText.EnterNewPasswordColon + " ");
        _newPass = ReadMasked(false);
        if (!Encryptor.IsValidPasswordLength(_newPass))
        {
            Menu.StatusMessage(DialogText.PasswordLength, true, true, true, DialogText.CaptionsPassword, string.Empty,
                false);
            NewPasswordRoutine(isPasswordReset);
        }
        else
        {
            CursorReturn();
            CursorDown();
            Console.Write(" " + DialogText.RepeatNewPasswordColon + " ");
            _newPassRepeat = ReadMasked(false);
        }

        // Compare and repeat process from the beginning if the two passwords doesnt match
        if (_newPass != _newPassRepeat)
        {
            Menu.StatusMessage(DialogText.PasswordMismatch, true, true, true, DialogText.CaptionsPassword, string.Empty,
                false);
            NewPasswordRoutine(isPasswordReset);
        }
        else
        {
            // Submit new password
            Encryptor.SetKey(_newPass);
            // Create vignette entries
            if (!isPasswordReset)
            {
                // Calendar related
                if (FileActions.CurrentFileType == FileActions.FileType.Calendar)
                {
                    // Force create vignette to show calendar instructions
                    Vignette.CreateVignette(true);
                    // Assume that year was already set
                    IsoDateTime.CreateYearCalendar();
                }
                else
                {
                    // Create vignette if enabled
                    Vignette.CreateVignette(false);
                }
            }

            // Return to normal filetype
            FileActions.CurrentFileType = FileActions.FileType.Normal;
            // Save data file
            Entries.SaveEntriesToFile();
            Menu.StatusMessage(DialogText.PasswordCreated, true, true, true, DialogText.CaptionsPassword, string.Empty,
                false);
            // Clear locals
            _pass = string.Empty;
            _newPass = string.Empty;
            _newPassRepeat = string.Empty;
            Mode.SetMode(Mode.SelectedMode.Login);
            Menu.DrawTopMenu(true, true, false);
        }
    }


    private static string _tempResetPassword = string.Empty;

    internal static void ResetPasswordRoutine()
    {
        // Question: Reset password?
        if (Menu.YesNoPrompt(DialogText.CaptionsPassword, DialogText.ResetPassword, string.Empty, false))
        {
            Tools.ClearScreen();
            Mode.SetMode(Mode.SelectedMode.Login);
            Menu.WriteModeLabel(DialogText.TitleResetPassword, string.Empty, 0);
            Tools.CursorDown(2);
            _tempResetPassword = string.Empty;
            Console.Write(" " + DialogText.EnterCurrentPasswordColon + " ");
            _tempResetPassword = ReadMasked(false);
            // Compare passwords
            if (Encryptor.IsCurrentKey(_tempResetPassword))
            {
                NewPasswordRoutine(true); // Continue to new password routine
            }
            else
            {
                Menu.StatusMessage(DialogText.InvalidPassword, true, true, true, DialogText.CaptionsPassword,
                    string.Empty, false);
                ResetPasswordRoutine();
            }
        }
        else
        {
            Tools.ClearScreen();
            Mode.SetMode(Mode.SelectedMode.Manage);
            Menu.DrawTopMenu(false, true, false);
        }
    }

    #endregion

    // JaggedRule
    private static bool _tempJagged;
    private static StringBuilder _tempJaggedLine = new();

    internal static string WriteJaggedLine(int length)
    {
        if (length > 1)
        {
            _tempJagged = false;
            _tempJaggedLine = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                if (_tempJagged)
                {
                    _tempJagged = false;
                    _tempJaggedLine.Append("=");
                }
                else
                {
                    _tempJagged = true;
                    _tempJaggedLine.Append("-");
                }
            }

            return _tempJaggedLine.ToString();
        }
        else
        {
            return "-";
        }
    }


    public static List<List<T>> SplitIntoChunks<T>(List<T> list, int chunkSize)
    {
        if (chunkSize <= 0) return new List<List<T>>();
        var retVal = new List<List<T>>();
        var index = 0;
        while (index < list.Count)
        {
            var count = list.Count - index > chunkSize ? chunkSize : list.Count - index;
            retVal.Add(list.GetRange(index, count));
            index += chunkSize;
        }

        return retVal;
    }

    // Shortcuts
    //private static string tempPageNumber = "Page 1/2";
    private static List<Shortcut> _shortcutList = new();


    // Clear data to enable rewriting it with new values for shortcuts
    public static void ClearEditorHelpData()
    {
        _shortcutList.Clear();
    }

    // Start by "collecting" shortcuts in a list
    // and the print them out with pagination
    public static void WriteEditorHelp(bool returnToEditorMenu, string section = "")
    {
        // Only fill the lists once
        if (_shortcutList.Count == 0)
        {
            _shortcutList = new List<Shortcut>();
            // Start by collecting all shortcuts
            _shortcutList.Add(new Shortcut(string.Empty, "Keyboard Functions", true));
            _shortcutList.Add(new Shortcut(string.Empty, "Menus and lists", true));
            _shortcutList.Add(new Shortcut("h l", "Previous/Next page (as Left/Right)", false));
            _shortcutList.Add(new Shortcut("j k", "Select in lists/Scroll text (as Down/Up)", false));
            _shortcutList.Add(new Shortcut("P", "Main menu: Preview of the last edited entries", false));
            _shortcutList.Add(new Shortcut("gg G", "First/Last (page, entry or text)", false));
            _shortcutList.Add(new Shortcut("/", "Search", false));
            _shortcutList.Add(new Shortcut("?", "Show this help", false));
            _shortcutList.Add(new Shortcut(":", "Command line (commands are shown in the menus)", false));
            _shortcutList.Add(new Shortcut(":q", "Close/Return (in the main menu: Exit Marbles)", false));
            _shortcutList.Add(new Shortcut(":qa", "Exit Marbles", false));
            _shortcutList.Add(new Shortcut(":logout", "Logout", false));
            _shortcutList.Add(new Shortcut("Esc", "Return/Cancel (login page: Options)", false));
            _shortcutList.Add(new Shortcut("Esc F", "Login page: Browse and edit text files", false));
            _shortcutList.Add(new Shortcut(":browse", "Browse files (opening a file logs out)", false));
            _shortcutList.Add(new Shortcut(string.Empty, DialogText.HelpSectionFiles, true));
            _shortcutList.Add(new Shortcut("j k", "Select (as Down/Up)", false));
            _shortcutList.Add(new Shortcut("gg G", "First/Last", false));
            _shortcutList.Add(new Shortcut("PgUp PgDn", "One page up/down", false));
            _shortcutList.Add(new Shortcut("l Enter", "Open the directory or file", false));
            _shortcutList.Add(new Shortcut("h - Backspace", "Go to the directory above", false));
            _shortcutList.Add(new Shortcut("/text", "Only show names containing \"text\" (Esc: all)", false));
            _shortcutList.Add(new Shortcut("N", "Create a new file and open it", false));
            _shortcutList.Add(new Shortcut("R", "Refresh", false));
            _shortcutList.Add(new Shortcut(":cd path", "Go to a directory", false));
            _shortcutList.Add(new Shortcut(":home", "Go to the Marbles data directory", false));
            _shortcutList.Add(new Shortcut("Esc Q :q", "Close the file browser", false));
            _shortcutList.Add(new Shortcut("  Text files", "Opened in the editor. They are NOT encrypted", false));
            _shortcutList.Add(new Shortcut("  Saving", "As UTF-8, with the line endings of the file", false));
            _shortcutList.Add(new Shortcut("  Not UTF-8", "Can be converted (replace file or save copy)", false));
            _shortcutList.Add(new Shortcut("  Data files", "Opening one selects it on the login page", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Editor", true));
            _shortcutList.Add(new Shortcut(string.Empty, "Modes", true));
            _shortcutList.Add(new Shortcut("Esc", "Normal mode (navigate and give commands)", false));
            _shortcutList.Add(new Shortcut("i a", "Insert mode before/after the cursor", false));
            _shortcutList.Add(new Shortcut("I A", "Insert mode at line start/end", false));
            _shortcutList.Add(new Shortcut("o O", "Insert mode on a new line below/above", false));
            _shortcutList.Add(new Shortcut("v V", "Visual mode (select text/lines)", false));
            _shortcutList.Add(new Shortcut(":", "Command line (type a command, press Enter)", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Save and close", true));
            _shortcutList.Add(new Shortcut(":w", "Save", false));
            _shortcutList.Add(new Shortcut(":wq :x ZZ", "Save and close editor", false));
            _shortcutList.Add(new Shortcut(":q", "Close editor (asks to save changes)", false));
            _shortcutList.Add(new Shortcut(":q! ZQ", "Close editor without saving", false));
            _shortcutList.Add(new Shortcut(":help", "Show this help", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Moving the cursor (Normal and Visual mode)", true));
            _shortcutList.Add(new Shortcut("h j k l", "Left/Down/Up/Right (as the arrow keys)", false));
            _shortcutList.Add(new Shortcut("w b e", "Next word/Previous word/End of word", false));
            _shortcutList.Add(new Shortcut("W B E", "As above, words separated by blank space", false));
            _shortcutList.Add(new Shortcut("0 _ $", "Line start/First character/Line end", false));
            _shortcutList.Add(new Shortcut("gg G", "Document top/Document end", false));
            _shortcutList.Add(new Shortcut("5G :5", "Go to line 5", false));
            _shortcutList.Add(new Shortcut("{ }", "Previous/Next blank line", false));
            _shortcutList.Add(new Shortcut("f. t.", "To/Before next \".\" on line (F T: backwards)", false));
            _shortcutList.Add(new Shortcut("; ,", "Repeat f/t forwards/backwards", false));
            _shortcutList.Add(new Shortcut("PgUp PgDn", "Move one page up/down", false));
            _shortcutList.Add(new Shortcut("CTRL+U CTRL+D", "Move half a page up/down", false));
            _shortcutList.Add(new Shortcut("zt zz zb", "Scroll cursor row to top/center/bottom", false));
            _shortcutList.Add(new Shortcut("5j 3w ...", "A number repeats a movement or command", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Search", true));
            _shortcutList.Add(new Shortcut("/text ?text", "Search forwards/backwards", false));
            _shortcutList.Add(new Shortcut("/\\d+", "Regular expressions can be used", false));
            _shortcutList.Add(new Shortcut("/Text", "Uppercase letters make the search match case", false));
            _shortcutList.Add(new Shortcut("n N", "Next/Previous match", false));
            _shortcutList.Add(new Shortcut("*", "Search for the word under the cursor", false));
            _shortcutList.Add(new Shortcut("Esc :noh", "Remove search highlighting", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Replace (command line)", true));
            _shortcutList.Add(new Shortcut(":s/old/new/", "Replace first \"old\" on the line with \"new\"", false));
            _shortcutList.Add(new Shortcut(":s/old/new/g", "Replace all on the line", false));
            _shortcutList.Add(new Shortcut(":%s/old/new/g", "Replace all in the document", false));
            _shortcutList.Add(new Shortcut(":2,9s/old/new/g", "Replace all on line 2 to 9", false));
            _shortcutList.Add(new Shortcut("V ... :s/old/new/g", "Replace all on the selected lines", false));
            _shortcutList.Add(new Shortcut("  & \\1 \\n", "In \"new\": the match/group 1/line break", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Editing (Normal mode)", true));
            _shortcutList.Add(new Shortcut("x X", "Delete character under/before the cursor", false));
            _shortcutList.Add(new Shortcut("r.", "Replace character with \".\"", false));
            _shortcutList.Add(new Shortcut("d c y", "Delete/Change/Yank (copy) + movement:", false));
            _shortcutList.Add(new Shortcut("  dw d$ dG", "Delete word/to line end/to document end", false));
            _shortcutList.Add(new Shortcut("  cw ciw ci\"", "Change word/whole word/text in quotes", false));
            _shortcutList.Add(new Shortcut("  yy dd cc", "Yank/Delete/Change line (3dd: 3 lines)", false));
            _shortcutList.Add(new Shortcut("D C Y", "Delete/Change to line end, Yank line", false));
            _shortcutList.Add(new Shortcut("p P", "Paste after/before the cursor", false));
            _shortcutList.Add(new Shortcut("J", "Join line with the next", false));
            _shortcutList.Add(new Shortcut(">> <<", "Indent/Unindent line", false));
            _shortcutList.Add(new Shortcut("~", "Switch case of character", false));
            _shortcutList.Add(new Shortcut("gUU guu", "Line to UPPERCASE/lowercase", false));
            _shortcutList.Add(new Shortcut("gUw guw", "Word to UPPERCASE/lowercase", false));
            _shortcutList.Add(new Shortcut("u", "Undo (" + TextBuffer.MaxUndoSteps + " steps)", false));
            _shortcutList.Add(new Shortcut("U CTRL+R", "Redo", false));
            _shortcutList.Add(new Shortcut(".", "Repeat last change", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Visual mode (select with the movement keys)", true));
            _shortcutList.Add(new Shortcut("d y c", "Delete/Yank/Change selection", false));
            _shortcutList.Add(new Shortcut("p", "Replace selection with pasted text", false));
            _shortcutList.Add(new Shortcut("U u ~", "Selection to UPPERCASE/lowercase/switch", false));
            _shortcutList.Add(new Shortcut("> <", "Indent/Unindent the selected lines", false));
            _shortcutList.Add(new Shortcut("J K", "V-LINE: Move the selected lines down/up", false));
            _shortcutList.Add(new Shortcut("J", "VISUAL: Join the selected lines", false));
            _shortcutList.Add(new Shortcut("iw i\" i(", "Select word/text in quotes/in brackets", false));
            _shortcutList.Add(new Shortcut("o", "Move cursor to other end of selection", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Insert mode", true));
            _shortcutList.Add(new Shortcut("Tab", "Insert blank space x " + EditorEngine.TabWidth, false));
            _shortcutList.Add(new Shortcut("CTRL+W", "Delete word before the cursor", false));
            _shortcutList.Add(new Shortcut("CTRL+U", "Delete line before the cursor", false));
            _shortcutList.Add(new Shortcut("CTRL+R", "Paste current memory string", false));
            _shortcutList.Add(new Shortcut(string.Empty, "String Memory (limit: " + MaxLineMemoryCount + " strings)", true));
            _shortcutList.Add(new Shortcut("y d c", "Yanked/deleted text is added to memory", false));
            _shortcutList.Add(new Shortcut("CTRL+P CTRL+N", "Select previous/next memory string", false));
            _shortcutList.Add(new Shortcut(":reg", "Show current memory string", false));
            _shortcutList.Add(new Shortcut(":putall", "Insert all memory strings", false));
            _shortcutList.Add(new Shortcut(":regclear", "Clear string memory", false));
            _shortcutList.Add(new Shortcut(string.Empty, "Other commands", true));
            _shortcutList.Add(new Shortcut(":ts", "Insert ISO timestamp (YYYY-MM-DD HH:MM:SS)", false));
            _shortcutList.Add(new Shortcut(":cal", "Insert calendar...", false));
            _shortcutList.Add(new Shortcut(":pw", "Insert generated strong password", false));
            _shortcutList.Add(new Shortcut(":trim", "Trim blank space (line start/end)", false));
            _shortcutList.Add(new Shortcut(":%y :%d :2,9d", "Yank all/Delete all/Delete line 2 to 9", false));
            _shortcutList.Add(new Shortcut(":clear", "Clear all", false));
        }

        ShowHelp(section);
        if (returnToEditorMenu)
        {
            Actions.DrawEditMenu();
        }
    }

    // The help viewer: the list of keys shown in pages, which can be browsed both ways and searched
    private static void ShowHelp(string section)
    {
        var top = 0;
        var search = string.Empty;
        var match = -1;
        var message = string.Empty;
        // Start at a section if asked for
        if (section.Length > 0)
        {
            top = Math.Max(0, _shortcutList.FindIndex(s => s.IsHeadline && s.GetDescription() == section));
        }

        HideCursor();
        while (true)
        {
            var pageSize = Init.LineCount - 1;
            var maxTop = Math.Max(0, _shortcutList.Count - pageSize);
            top = Math.Clamp(top, 0, maxTop);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            ClearScreen();
            for (var row = 0; row < pageSize && top + row < _shortcutList.Count; row++)
            {
                var item = _shortcutList[top + row];
                SetCursorEx(row, 0);
                item.WriteShortcut(false, true);
                // Mark the lines matching the search
                if (search.Length > 0 && item.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    SetCursorEx(row, 0);
                    ConsoleColors.SetColors(top + row == match
                        ? ConsoleColors.ColorPreset.HighlightInverted
                        : ConsoleColors.ColorPreset.Divider);
                    Console.Write(">");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }
            }

            // Page number and keys
            var pages = (_shortcutList.Count + pageSize - 1) / pageSize;
            var page = top >= maxTop ? pages : top / pageSize + 1;
            SetCursorEx(Init.LineCount - 1, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" Page " + page + " of " + pages + " ");
            if (message.Length > 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write(" " + message + " ");
            }
            else
            {
                Console.Write(DialogText.HelpKeys);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            message = string.Empty;

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                break;
            }

            if (key.KeyChar == '/')
            {
                // Search: go to the first matching line from the top of the page
                var text = CommandLine.Read('/', false);
                if (text == null) continue;
                search = text;
                match = FindHelpLine(search, top - 1, true);
                if (match < 0)
                {
                    message = DialogText.HelpNotFound + search;
                    search = string.Empty;
                }
            }
            else if (key.KeyChar is 'n' or 'N' && search.Length > 0)
            {
                match = FindHelpLine(search, match, key.KeyChar == 'n');
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                top = maxTop;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') top = 0;
            }
            else if (key.Key == ConsoleKey.Home)
            {
                top = 0;
            }
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                top++;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                top--;
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.PageDown or ConsoleKey.Spacebar
                     or ConsoleKey.Enter)
            {
                top += pageSize;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.PageUp or ConsoleKey.B)
            {
                top -= pageSize;
            }

            // Show the current match, with a few lines above it
            if (match >= 0 && key.KeyChar is '/' or 'n' or 'N')
            {
                top = match - 2;
            }
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        ClearScreen();
    }

    // The next (or previous) line of the help containing a text, wrapping around. -1 if there is none
    private static int FindHelpLine(string search, int from, bool forward)
    {
        var count = _shortcutList.Count;
        for (var i = 1; i <= count; i++)
        {
            var index = ((from + (forward ? i : -i)) % count + count) % count;
            if (_shortcutList[index].Text.Contains(search, StringComparison.OrdinalIgnoreCase)) return index;
        }

        return -1;
    }

    private static void DisplayWelcomeText()
    {
        Tools.ClearScreen();
        Logo.DrawLogo(true);
        Tools.CursorTopLeftDown(6);
        //  automatically). You can copy the data directory or the entire Marbles directory\n <- Max line length
        // --------------------------------------------------------------------------------
        string welcomeText = " Welcome to " + DialogText.ApplicationName + "\n\n" +
                             " Your notes and settings are stored in the \"data\" directory, located alongside\n" +
                             " the Marbles program files (the program automatically creates this directory).\n" +
                             " You can copy the data directory or the entire Marbles directory to a backup,\n" +
                             " portable or cloud media if necessary, or enable automatic backup to a\n" +
                             " secondary location. Marbles uses AES-256 file encryption.\n\n" +
                             " CAUTION: It is not possible to recover your notes if you forget your password.";

        int tempPreviewLineCounter = 0;
        string[] tempOutput = Tools.SplitStringOnNewLine(welcomeText, false);
        for (int i = 0; i < tempOutput.Length; i++)
        {
            // count lines
            tempPreviewLineCounter += 1;
            // Insert line of text
            Console.WriteLine(tempOutput[i]);

            // Write long document indicator and exit loop
            if (tempPreviewLineCounter >= 13)
            {
                break;
            }
        }

        CursorDown(2);
        CursorRight();
        Keyboard.AwaitAnyKey();
    }

    public static void DisplayDateTime()
    {
        string title = "Timestamp";
        string output = " " + IsoDateTime.GetDateTimeNow() + " ";
        string output2 = " " + IsoDateTime.GetDateTimeNowCompressed(string.Empty) + " ";
        string output3 = " Week " + IsoDateTime.GetIso8601WeekNumber(DateTime.Now);
        int boxHeight = 6;
        int boxWidth = output.Length + 4;
        SaveCursorPosition(5);
        CursorTopLeft();
        // Place cursor according to box size
        CursorDown((Init.LineCount / 2) - (boxHeight / 2));
        CursorRight((Init.LineWidth / 2) - (boxWidth / 2));
        // Draw rectangle
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        for (int i = 0; i < boxHeight; i++)
        {
            Console.Write(new string(' ', boxWidth));
            CursorLeft(boxWidth);
            CursorDown(); 
        }
        CursorUp(6);
        CursorRight(1);
        Console.Write(title);
        CursorLeft(title.Length);
        CursorDown(2);
        Console.Write(output);
        CursorLeft(output.Length);
        CursorDown();
        Console.Write(output2);
        CursorLeft(output2.Length);
        CursorDown();
        Console.Write(output3);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        LoadCursorPosition(5);
    }


    #region Cursor

    public static void ShowCursor()
    {
        try
        {
            Console.CursorVisible = true;
        }
        catch
        {
            // Do nothing
        }
    }

    public static void HideCursor()
    {
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // Do nothing
        }
    }

    private static int _rowPosition;
    private static int _columnPosition;

    private static void InitializeCursor()
    {
        _rowPosition = Console.CursorTop;
        _columnPosition = Console.CursorLeft;
    }

    public static void CursorTopLeft()
    {
        _rowPosition = 0;
        _columnPosition = 0;
        Refresh();
    }

    public static void CursorTopLeftDown(int linesDown)
    {
        CursorTopLeft();
        CursorDown(linesDown);
    }

    public static void SetCursorEx(int top, int left)
    {
        _rowPosition = top;
        _columnPosition = left;
        Refresh();
    }

    private static void Refresh()
    {
        try
        {
            Console.SetCursorPosition(_columnPosition, _rowPosition);
        }
        catch
        {
            Init.DimensionsExceptionCaught = true;
            ClearScreen();
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + DialogText.AttentionColon);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.WriteLine(" " + DialogText.ErrorClientSize);
            Console.WriteLine("(Dimensions must be at least " + Init.LineWidth + "x" + Init.LineCount + ")");
            Console.WriteLine(DialogText.ResetOutputDimensions);
            _keyInfo = Console.ReadKey(true);
            if (_keyInfo.Key == ConsoleKey.Y)
            {
                Init.SetDefaultOutputDimensions();
            }

            ClearScreen();
        }
    }


    public static void CursorReturn()
    {
        InitializeCursor();

        if (_columnPosition > 0)
        {
            _columnPosition = 0;
            Refresh();
        }
    }


    public static void CursorLeft()
    {
        InitializeCursor();

        if (_columnPosition > 0)
        {
            _columnPosition--;
            Refresh();
        }
        else
        {
            CursorUp(true);
        }
    }


    public static void CursorLeft(int count)
    {
        if (count > 0)
        {
            InitializeCursor();

            for (int i = 0; i < count; i++)
            {
                if (_columnPosition > 0)
                {
                    _columnPosition--;
                    Refresh();
                }
                else
                {
                    CursorUp(true);
                    return;
                }
            }
        }
    }

    public static void CursorRight()
    {
        InitializeCursor();

        if (_columnPosition < Init.LineWidth - 1)
        {
            _columnPosition++;
            Refresh();
        }
        else
        {
            CursorDown(true);
        }
    }

    public static void CursorRight(int count)
    {
        if (count > 0)
        {
            InitializeCursor();

            for (int i = 0; i < count; i++)
            {
                if (_columnPosition < Init.LineWidth - 1)
                {
                    _columnPosition++;
                    Refresh();
                }
                else
                {
                    CursorDown(true);
                    return;
                }
            }
        }
    }

    public static void CursorUp(int rows)
    {
        InitializeCursor();

        if (_rowPosition > rows)
        {
            _rowPosition -= rows;
            Refresh();
        }
    }

    public static void CursorUp(bool returnCursor)
    {
        InitializeCursor();

        if (_rowPosition > 0)
        {
            _rowPosition--;
            Refresh();

            if (returnCursor)
                CursorReturn();
            else if (_columnPosition > 0)
            {
                CursorLeft();
            }
        }
    }

    public static void CursorDown()
    {
        InitializeCursor();

        if (_rowPosition < Init.LineCount - 1)
        {
            _rowPosition++;
            Refresh();
        }
    }

    public static void CursorDown(int rows)
    {
        InitializeCursor();

        if (_rowPosition < Init.LineCount - 1)
        {
            _rowPosition += rows;
            Refresh();
        }
    }

    public static void CursorDown(bool returnCursor)
    {
        InitializeCursor();

        if (_rowPosition < Init.LineCount - 1)
        {
            _rowPosition++;
            Refresh();

            if (returnCursor)
                CursorReturn();
            else if (_columnPosition > 0)
            {
                CursorLeft();
            }
        }
    }

    #endregion

    #region Cursor memory

    private static List<CursorMemory> _cursorMemory = new();

    private static void InitializeCursorMemory()
    {
        if (_cursorMemory.Count != 0) return;
        _cursorMemory = new List<CursorMemory>();
        for (int i = 0; i < 10; i++)
        {
            _cursorMemory.Add(new CursorMemory(0, 0));
        }
    }

    /// <summary>
    /// This method has 10 memory banks for storing cursor positions
    /// </summary>
    /// <param name="memoryBank"></param>
    public static void SaveCursorPosition(int memoryBank)
    {
        InitializeCursorMemory();
        if (memoryBank >= 0 && memoryBank <= 9)
        {
            _cursorMemory[memoryBank].Left = Console.CursorLeft;
            _cursorMemory[memoryBank].Top = Console.CursorTop;
        }
    }

    /// <summary>
    /// This method has 10 memory banks for storing cursor positions
    /// </summary>
    /// <param name="memoryBank"></param>
    public static void LoadCursorPosition(int memoryBank)
    {
        InitializeCursorMemory();
        if (memoryBank >= 0 && memoryBank <= 9)
        {
            SetCursorEx(_cursorMemory[memoryBank].Top, _cursorMemory[memoryBank].Left);
        }
    }

    #endregion


    #region Progress bar

    private const decimal ProgressbarWidth = 40;
    private const char ProgressBarChar = '#';

    /// <summary>
    /// Will return "[####################....................] 50%"
    /// </summary>
    /// <param name="currentValue"></param>
    /// <param name="maximumValue"></param>
    /// <returns></returns>
    internal static string GetProgressBar(decimal currentValue, decimal maximumValue)
    {
        try
        {
            // Calculate progress bar and insert the value into the progressBarText
            var progressBarProgress = new string(ProgressBarChar,
                Convert.ToInt32(currentValue / (maximumValue / ProgressbarWidth)));
            // Add the calculated content to the text output
            var progressBarText = "[" + progressBarProgress +
                                  new string('.', (int) ProgressbarWidth - progressBarProgress.Length) + "] " +
                                  ((currentValue / maximumValue) * 100).ToString("0") + "%";
            // Write the progress bar
            return progressBarText;
        }
        catch
        {
            return DialogText.NotAvailable;
        }
    }

    #endregion

}

internal class CursorMemory
{
    public int Left { get; set; }
    public int Top { get; set; }

    public CursorMemory(int left, int top)
    {
        Left = left;
        Top = top;
    }
}


#region Password Generator

#endregion