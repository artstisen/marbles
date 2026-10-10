namespace Marbles;

internal static class DialogText
{
    // Application information
    public const string ApplicationName = "Marbles";
    public const string ApplicationCopyright = "Copyright 2026 Lars Brandt Stisen";
    public const string ApplicationBy = "by Lars Brandt Stisen (c)2026";
    public const string ApplicationVersion = "v1.5.3"; // For display. Right click the Marbles project icon to set assembly version

    //                                                                                \n <- Max line length
    //-----------------------------------------------------------------------------
    public const string About = "Marbles is written in C# and uses UTF-8 console I/O encoding and AES-256\n" +
                                "data encryption. Your notes and settings are stored in the \"data\" directory\n" +
                                "which is located alongside the Marbles program files. You can copy the data\n" +
                                "directory or the entire Marbles directory to any backup/portable/cloud medium\n" +
                                "if needed, or enable automatic backup to a secondary location." +
                                "\n\n" +
                                //-----------------------------------------------------------------------------
                                "CAUTION: It is not possible to recover your notes if you forget your\n" + 
                                "password.\n" +
                                "This application is cross-platform software that can run on different\n" +
                                "systems. Due to the difference between terminal programs and their\n" +
                                "tradition of using specific keyboard shortcuts for built-in functions,\n" +
                                "Marbles is operated with plain keys and commands in the style of Vim.\n" +
                                "Press ? for help.";

    //                                                                                \n <- Max line length
    // --------------------------------------------------------------------------------
    public const string MultipleInstances =
        "TIP: Run multiple instances of Marbles using folder duplicates with different\n" +
        "names. Change color settings or use vignettes to distinguish between\n" +
        "instances. Running multiple instances from the same folder can overwrite\n" +
        "notes and application settings when sharing the same pool of data.\n";


    // Mode labels and Caption
    public const string LogReview = "Log review";
    public const string CaptionsMergeExport = "Merge/Export";
    public const string CaptionsMerge = "Merge";
    public const string CaptionsLogin = "Login";
    public const string NewPassword = "New password";
    public const string CaptionsPassword = "Password";
    public const string TitleResetPassword = "Reset password";
    public const string TitleSearch = "Search";
    public const string CaptionsSettings = "Settings";
    public const string CaptionsOptions = "Options";
    public const string CaptionsMoreOptions = "More options";
    public const string CaptionsColorScheme = "Edit color scheme";
    public const string CaptionsExport = "Export";
    public const string CaptionsImport = "Import";
    public const string CaptionsBackup = "Backup";
    public const string CaptionsInstructions = "Instructions";
    public const string CaptionsPreview = "PREVIEW";
    public const string CaptionsInfo = "INFO";
    public const string CaptionsRecall = "RECALL";
    public const string CaptionsClear = "CLEAR";
    public const string CaptionsEdit = "EDIT";
    public const string CaptionsTop20 = "TOP 20";
    public const string CaptionsFavorites = "FAVORITES";
    public const string CaptionsSession = "SESSION HISTORY";
    public const string CaptionsSearch = "Search";
    public const string CaptionsAll = "ALL";
    public const string EditTitle = "Edit title";
    public const string CaptionsDelete = "Delete";
    public const string CaptionsDuplicate = "Duplicate";
    public const string CaptionsRevert = "Revert";
    public const string CaptionsRestore = "Restore";
    public const string CaptionsStringMemory = "String memory";
    public const string CaptionsRanking = "Move pinned";
    public const string MovePinnedKeys = "MOVE | j/k Move the entry up/down | Esc Done";
    public const string CaptionsColorize = "Set color";
    public const string PinColorCaption = "Pinned color";
    public const string ColorDefault = "[Default]";

    // The find-and-replace box of the editor
    public const string ReplaceCaption = "Replace";
    public const string ReplaceFind = "Find:";
    public const string ReplaceWith = "Replace with:";
    public const string ReplaceMatchCase = "Match case";
    public const string ReplaceRegex = "Regular expression";
    public const string ReplaceWhere = "Where:";
    public const string ReplaceWholeText = "Whole text";
    public const string ReplaceCurrentLine = "Current line";
    public const string ReplaceSelectedLines = "Selected lines";
    public const string ReplaceRun = "Replace all";
    public const string ReplaceKeys = "Enter/Space Edit or change | j/k Select | Esc Cancel";
    public const string ColorCaption = "Color";
    public const string TextColorCaption = "Text color";
    public const string BackgroundCaption = "Background";
    public const string BackgroundDefault = "default"; // The setting for the background of the terminal
    public const string Favorites = "Favorites";
    public const string Session = "Session";
    public const string New = "New";
    public const string NewFile = "New file";
    public const string RenameFile = "Rename file";
    public const string CopyFile = "Copy file";
    public const string MoveFile = "Move file";
    public const string DeleteFile = "Delete file";
    public const string MergeFiles = "Merge/Export";
    public const string Transfer = "Transfer";
    public const string FileInfo = "Info";
    public const string Save = "Save";
    public const string Restart = "Restart";
    public const string Clear = "Clear";
    public const string Log = "Log";
    public const string Refresh = "Refresh";
    public const string Exit = "Exit";
    public const string Title = "Title";
    public const string Select = "Select";
    public const string SortColon = "Sort:";
    public const string TabBrackets = "(Tab)";
    public const string SortOptionDateChanged = "Date changed";
    public const string FavoritesSortColumn = "Date changed/Custom";
    public const string CaptionsKeyError = "Keyboard";

    // Mode texts
    public const string EnterQueryForSearch = "Enter query and press Enter";
    public const string EditTitleAndPressEnter = "Change title and press Enter";
    public const string EnterTitleAndPressEnter = "Enter title and press Enter";
    public const string EnterFileNameAndPressEnter = "Enter file name and press Enter";
    public const string ChangeFileNameAndPressEnter = "Change file name and press Enter";
    public const string EnterPathAndPressEnter = "Enter absolute directory path and press Enter";

    public const string Escape = "Esc";
    public const string Spacebar = "Spacebar";

    // Indicators/Misc. texts
    public const string DocumentScroll = " (Press j/k to scroll, Spacebar/b for page down/up) ";
    public const string TitleCopyText = " (copy)";
    public const string InitNoData = "No data";
    public const string CheckedSetting = "[x]";
    public const string UnCheckedSetting = "[ ]";
    public const string NotAvailable = "N/A";
    public const string SavIndicator = "Unsaved";

    public const string ExtExample = "[Example]";
    public const string PaginationLeftRight = "<- ->";
    public const string BrowseUpDown = "Up/Down";
    public const string ExtSelectDataFile = "File";
    public const string ExtSelectDirectory = "Directory";
    public const string HitAnyKeyToContinue = "[Press any key to continue]";
    public const string HitAnyKeyOrEscape = "[Press a key to continue/Esc to exit]";
    public const string PressEscToExitP = "(Press Esc to exit)";
    public const string PressEscToExitS = "[Press Esc to exit]";
    public const string PressEscSpaceToExit = "[Press Esc or Spacebar to cancel]";

    // Yes/No dialogs
    private const string YesNo = "[Y] Yes  [N] No";
    private const string YesNoCancel = "[Y] Yes  [N] No  [Esc] Cancel";
    public const string YesContinue = "[E] Yes (continue editing)";
    public const string SaveQuestion = "Save changes?";
    public const string SaveChoiceYes = "Y    Yes, save and close";
    public const string SaveChoiceNo = "N    No, close without saving";
    public const string SaveChoiceContinue = "E    Save and continue editing";
    public const string SaveChoiceCancel = "Esc  Cancel (continue editing)";
    private const string YesPinnedNo = "[Y] Yes  [P] Pinned  [N] No";
    private const string YesNoPinned = "[Y] Yes  [N] No  [P] Pin";

    // -------------------------------------------------------------------
    //public const string NewFileYn = "Create a new file? " + YesNo;
    public const string RenameFileYn = "Rename selected file? " + YesNo;
    public const string CopyFileYn = "Copy selected file? " + YesNo;
    public const string MoveFileYn = "Move selected file? " + YesNo;
    public const string DeleteFileYn = "Delete selected file? " + YesNo;
    public const string CreateTransfer = "Prepare transfer? " + YesNo;

    public const string SaveChangesYnCancel = "Save changes? " + YesNoCancel;
    public const string SaveChangesYn = "Save changes? " + YesNo;
    public const string ExitYn = "Exit? " + YesNo;
    public const string RestartYn = "Restart Marbles? " + YesNo;
    public const string ReplaceFileYn = "The file exists. Replace it? " + YesNo;
    public const string DeleteEntryYn = "Delete entry? " + YesNo;
    public const string DeleteEntryYnConfirm = "Confirm deletion: " + YesNo;
    public const string DuplicateEntryYn = "Duplicate entry? " + YesNo;
    public const string ClearTextYn = "Clear text? " + YesNo;
    public const string RevertEditYn = "Revert to previous edit? " + YesNo;
    public const string RestoreDeletedYn = "Undo last deletion? " + YesNo;
    public const string ResetPassword = "Reset password? " + YesNo;
    public const string ResetOutputDimensions = "Reset " + ApplicationName + " output dimensions? " + YesNo;
    public const string FitOutputDimensions = "Follow the size of the terminal window (automatic size)? " + YesNo;
    public const string EnlargeWindow = "Make the terminal window larger and press a key";
    public const string ResetSettings = "Revert to default settings? " + YesNo;
    public const string RemoveFavoriteYn = "Remove from favorites? " + YesNo;
    public const string RemoveFavoriteOrPinYn = "Remove from favorites? " + YesNoPinned;
    public const string AddFavoriteYn = "Add to favorites? " + YesPinnedNo;
    public const string ImportYn = "Import a text file as a new entry? " + YesNo;
    public const string BackupYn = "Set backup directory? " + YesNo;
    public const string BackupDisableYn = "Disable backup? " + YesNo;
    public const string DiscardTransferCollection = "Discard transfer? " + YesNo;
    public const string ClearStringMemory = "Clear string memory? " + YesNo;

    // Selection dialogs
    public const string MergeExportA = "Press [A] to merge";
    public const string MergeExportB = "Press [B] to export";
    public const string MergeExportC = "Press [Esc] to cancel";
    public const string NewFileA = "[A] Standard data file (default)";
    public const string NewFileB = "[B] Calendar";
    public const string NewFileC = "[Esc] Cancel";
    public const string TransferA = "[Enter] Add to current transfer";
    public const string TransferB = "[A] Complete transfer";
    public const string TransferC = "[B] Discard transfer";
    public const string TransferD = "[Esc] Cancel";

    public const string ImportYnNote = "The file is selected in the file browser.";
    public const string ExportedTo = "Saved as ";
    public const string SelectOption = "Select option...";
    public const string SelectFileOption = "Select file preset";
    public const string SelectTransferOption = "Select transfer option";
    public const string SelectTransferOptionEsc = "Select transfer option " + PressEscToExitP;
    public const string CanNotBeUndone = "This action can not be undone!";
    public const string FileMoveInstructions = "Move file: Select target directory and press Enter...";
    public const string PinnedEntryPosition = "Only pinned entries can change position";
    public const string PinnedEntryColors = "Only pinned entries can change color";

    // Input/Output/Information
    public const string TitleColon = "Title:";
    public const string NameColon = "Name:";
    public const string FileColon = "File:";
    public const string KeyColon = "Key:";
    public const string PathColon = "Path: ";
    public const string SearchColon = "Search:";
    public const string DateCreatedColon = "Created: ";
    public const string DateChangedColon = "Changed: ";
    public const string EnterNewPasswordColon = "Create new password:";
    public const string RepeatNewPasswordColon = "Repeat new password:";
    public const string EnterCurrentPasswordColon = "Enter current password:";
    public const string PasswordColon = "Password:";

    public const string CurrentBackupPathColon = "Current path: ";

    public const string AttentionColon = "Attention: ";

    // Log
    public const string InitInitializing = "Initializing...";
    public const string InitCompleted = "Initialization complete.";
    public const string InitSetEncoding = "Set I/O encoding...";
    public const string InitSetDefaults = "Set default values...";
    public const string InitDirectoryCheck = "File directory check...";
    public const string InitOk = "[OK]";
    public const string InitFailed = "[FAILED]";
    public const string InitLoadingSettings = "Loading settings...";
    public const string InitLoadingSettingsError = "Unable to interpret " + FileIo.SettingsFileName;
    public const string InitDimensions = "Initializing output variables...";
    public const string InitUpdateDimensions = "Updating output variables...";
    public const string InitDirectoryCheckError = "Directory check failed: ";

    public const string DirectoryCreated = "Directory created";
    public const string DefaultSettings = "Default settings";

    // Merge tool
    public const string MergeCompleted = "Merge completed";
    public const string ExportCompleted = "Export completed";
    public const string Summary = "Summary";

    // Status messages (styled)
    public const string NoEntrySelected = "No entry selected";
    public const string PasswordLength = "Password must be between 8 and 128 characters long";
    public const string PasswordMismatch = "The two passwords do not match";
    public const string InvalidPasswordDot = "Invalid password."; // Must have period.
    public const string InvalidPassword = "Invalid password";
    public const string CapsLockNotice = "Make sure Caps Lock is not enabled.";
    public const string PasswordCreated = "Password created";
    public const string InvalidFileName = "Invalid file name";
    public const string NoRecallHistoryFound = "No recall history found";
    public const string NoBackupFound = "No edit history to revert to";
    public const string NoDeletionBackupFound = "No restore data available";
    public const string UnableToCompleteRestore = "Unable to complete restore";
    public const string EntryRestored = "Edit reverted";
    public const string DeletionRestored = "Deletion restored";
    public const string NoEntriesFound = "No entries found";
    public const string NoFavoritesFound = "No favorites found";
    public const string NoSessionData = "No session data";
    public const string NoEntriesSelected = "No entries selected";
    public const string EntryDeleted = "Entry deleted";
    public const string FileDeleted = "File deleted";
    public const string BackupDisabled = "Backup disabled";
    public const string BackupEnabled = "Backup enabled";
    public const string Saving = "Saving...";
    public const string FileNoEntries = "File contains no entries";
    public const string InvalidBackupPath = "Invalid backup path";

    // Un-styled status messages
    public const string LoadComplete = "Load complete.";
    public const string Loading = "Loading...";
    public const string Reading = "Reading...";

    // File browser
    public const string CaptionsFiles = "Files";
    public const string BrowseFilesSelectMode = "Files/Select mode";

    // The page shown at startup
    public const string StartSelectMode = "Select mode";
    public const string StartNotes = "Note Management (encrypted notes)";
    public const string StartEditor = "Text Editor/File Management";
    public const string StartConfiguration = "Configuration";
    public const string StartExit = "Exit";
    public const string QuickGuide = "Quick guide";
    public const string QuickGuideKeys = "| h/l Page | j/k Line | ? All keys | Esc Close";
    public const string CopyDeleteFile = "Copy/Delete file";
    public const string StartConfigSelect = "Show choices on startup";
    public const string StartConfigNotes = "Start in Note Management";
    public const string StartConfigEditor = "Start in Text Editor (file browser)";
    public const string StartKeys = "j/k 1-5 Select | Enter Open | ? Help | Q Exit";
    public const string StartConfigKeys = "j/k 1-9 Select | Enter/Space Change | h/l Previous/Next | Esc Back";
    public const string ConfigMonochrome = "Black and white (no colors)";
    public const string ConfigColorScheme = "Color scheme: ";
    public const string ConfigLogo = "Logo: ";
    public const string ConfigOf = " of ";
    public const string ConfigAutoSize = "Automatic size (follow the terminal window)";
    public const string ConfigSize = "Size: ";
    public const string ConfigSizeNote = " (Enter: Change)";
    public const string ConfigLineNumbers = "Line numbers in the editor";
    public const string ConfigReset = "Reset all settings";
    public const string ConfigRestart = "Restart Marbles";
    public const string ConfigViewLog = "View log";

    public const string EditorReloaded = "Read from the file again";
    public const string EditorReloadUnsaved = "Unsaved changes (:e! reads the file anyway)";
    public const string EditorReloadFilesOnly = "Only text files can be read again";
    public const string LineNumbersOn = "Line numbers on";
    public const string LineNumbersOff = "Line numbers off";
    public const string SyntaxOn = "Colors on: ";
    public const string SyntaxOff = "Colors off";
    public const string SyntaxUnknown = "Use md, html, css, js, json or xml, not: ";

    // The log
    public const string LogLines = " lines";
    public const string LogLine = " line";
    public const string LogOf = " of ";
    public const string LogFiltered = " containing: ";
    public const string LogRemoved = " older lines removed";
    public const string LogKeys = "| h/l Page | j/k Line | gg G First/Last | /text Filter | Esc Close";
    public const string LogCommand = "Command";
    public const string LogEditorCommand = "Editor";
    public const string LogFilesCommand = "Files";
    public const string LogFile = "File: ";
    public const string MonochromeIsOn = "Black and white is on: turn it off (:bw) to use the color scheme";
    public const string Drives = "Drives";
    public const string DirectoryMark = "<DIR>";
    public const string DataFileMark = "[Data file]";
    public const string FileBrowserKeys = "j/k Select | l Open | h Up | / Filter | F Faves | ? Help | Esc Close";

    public const string FileBrowserDualKeys = "Tab Pane | l Open | h Up | / Filter | F Faves | ? Help | Esc Close";
    public const string PaneLabel = "PANE ";
    public const string FileTargetDualKeys = "Tab Pane | l Open | h Up | / Filter | S {0} | Esc Cancel";
    public const string FileTargetKeys = "j/k Select | l Open | h Up | / Filter | S {0} | Esc Cancel";
    public const string FileCopyHere = "Copy here";
    public const string FileMoveHere = "Move here";
    public const string FileSelectTarget = "Go to the directory and press S (Esc cancels)";

    // File management in the file browser
    public const string NewDirectory = "New directory";
    public const string DeleteDirectory = "Delete directory";
    public const string DeleteNamedYn = "Delete \"{0}\"? " + YesNo;
    public const string DeleteFileNote = "The file cannot be recovered";
    public const string DeleteDataFileNote = "A Marbles data file with your notes. It cannot be recovered";
    public const string DeleteEmptyDirectoryNote = "The directory is empty";
    public const string DeleteDirectoryNote = "With all its content: ";
    public const string DeleteDirectoryFiles = " files and ";
    public const string DeleteDirectoryDirectories = " directories. They cannot be recovered";
    public const string FileItemDeleted = "Deleted: ";
    public const string FileCopyTo = "Copy to: ";
    public const string FileMoveTo = "Move to: ";
    public const string FileCopied = "Copied to ";
    public const string FileMoved = "Moved to ";
    public const string FileCopying = "Copying (Esc: Cancel): ";
    public const string FileCanceled = "Canceled";
    public const string FileIntoItself = "A directory cannot be copied or moved into itself";
    public const string FileAlreadyThere = "It is already in that directory";
    public const string FileUsedByMarbles = "Used by Marbles: it cannot be renamed, moved or deleted here";

    // Information about a file in the file browser
    public const string InfoCaptionFile = "File info";
    public const string InfoCaptionDirectory = "Directory info";
    public const string InfoName = "Name:";
    public const string InfoType = "Type:";
    public const string InfoSize = "Size:";
    public const string InfoContent = "Content:";
    public const string InfoFiles = " files, ";
    public const string InfoDirectories = " directories";
    public const string InfoCreated = "Created:";
    public const string InfoChanged = "Changed:";
    public const string InfoFavorite = "Favorite:";
    public const string InfoFavoriteYes = "Yes";
    public const string InfoPath = "Path:";
    public const string InfoTypeDirectory = "Directory";
    public const string InfoTypeDataFile = "Marbles data file (encrypted notes)";
    public const string InfoTypeFile = " file";
    public const string InfoTypeNoExtension = "File";
    public const string InfoTypeLink = ", link to ";
    public const string InfoTypeReadOnly = ", read-only";

    // Searching for files in the file browser
    public const string SearchCaption = "Search files";
    public const string SearchResultCaption = "Files found";
    public const string HelpSectionSearch = "File browser search";
    public const string SearchIn = "In: ";
    public const string SearchName = "Name:";
    public const string SearchText = "Text in files:";
    public const string SearchSubfolders = "This directory and its subfolders";
    public const string SearchThisDirectory = "This directory only";
    public const string SearchRun = "Search";
    public const string SearchNothing = "Write a name or a text to search for";
    public const string SearchInvalidPattern = "Not a regular expression: ";
    public const string SearchProgress = "Searching (Esc: Stop), {0} found: ";
    public const string SearchFound = " files found";
    public const string SearchFoundOne = " file found";
    public const string SearchFoundIn = " in ";
    public const string SearchCapped = " (the search stops at this number)";
    public const string SearchStopped = " (stopped)";
    public const string SearchUnreadable = " directories could not be read";
    public const string SearchResultKeys = "Enter Open | B Show | I Info | P Fave | S Search | / Filter | Esc Close";

    // The favorites of the file browser
    public const string CaptionsFileFavorites = "Favorite files";
    public const string HelpSectionFavorites = "File browser favorites";
    public const string FavoritesHeader = "Files and directories marked as favorites (P in the file browser)";
    public const string FavoritesEmpty = "No favorites yet: press P on a file or directory in the file browser";
    public const string FavoritesExpanded = "All folders are open";
    public const string FavoritesCollapsed = "All folders are closed";
    public const string FavoritesNoFolders = "There are no folders";
    public const string FavoritesKeys = "Enter Open | B Show | A Folder | V Move | C Color | P Remove | Esc Close";
    public const string FavoriteAdded = "Added to the favorites";
    public const string FavoriteRemoved = "Removed from the favorites";
    public const string FavoriteMissing = "(missing)";
    public const string FavoriteMoveFiltered = "Remove the filter (Esc) to move favorites and add folders";
    public const string FavoriteNewFolder = "New folder";
    public const string FavoriteNoFolder = "(No folder)";
    public const string FavoriteMoveOne = "Move to folder";
    public const string FavoriteMoveMany = "Move {0} favorites to folder";
    public const string FavoriteMovedOne = "Moved to: ";
    public const string FavoriteMovedMany = "{0} favorites moved to: ";
    public const string FavoriteMarked = " marked";
    public const string FavoriteMarkFolder = "Only favorites can be marked and moved to a folder (not folders)";
    public const string FavoritesMarkedKeys = "Space Mark | M To folder | D Remove | Esc Clear the marks";
    public const string FavoriteRemoveCaption = "Remove favorites";
    public const string FavoriteRemoveManyYn = "Remove the {0} marked favorites from the list? " + YesNo;
    public const string FavoriteRemoveNote = "The files stay where they are";
    public const string FavoriteRemovedMany = "{0} removed from the favorites (u: Undo)";
    public const string FavoriteRenameFolder = "Rename folder";
    public const string FavoriteDeleteFolder = "Remove folder";
    public const string FavoriteDeleteFolderNote = "The {0} favorites in it are removed from the list too (the files stay)";
    public const string FavoriteFolderRemoved = "Removed the folder: ";
    public const string FavoriteFolderName = "The name cannot be used for a folder";
    public const string FavoriteFolderCount = " favorites";
    public const string FavoriteFolderOne = " favorite";
    public const string FavoriteDeleteFolderNoteOne = "The favorite in it is removed from the list too (the file stays)";
    public const string FavoriteNothingToUndo = "Nothing to undo";
    public const string FavoriteRestored = "Put back in the favorites";
    public const string FavoriteRenameFile = "Only folders are renamed here. B shows the file, where X renames it";
    public const string HelpSectionFiles = "File browser";
    public const string HelpKeys = "| h/l Page | j/k Line | /text Search | n/N Next/Previous | Esc Close";
    public const string HelpKeysTopic = "| h/l Page | j/k Line | / Search | n/N Next | ? All keys | Esc Close";
    public const string HelpKeysAll = "| h/l Page | j/k Line | / Search | n/N Next | ? This page | Esc Close";
    public const string HelpSectionStart = "Start page";
    public const string HelpSectionLogin = "Login page";
    public const string HelpSectionMenus = "Menus and lists";
    public const string HelpSectionEditor = "Editor";
    public const string HelpNotFound = "Not found: ";
    public const string NotEncrypted = "[Not encrypted]";
    public const string FilePickKeys = "j/k Select | l/Enter Open/Select file | h Up | / Filter | Esc Cancel";
    public const string DirectoryPickKeys = "j/k Select | l/Enter Open | h Up | S Select this directory | Esc Cancel";
    public const string PreviewCaption = "Last edited (j/k, Enter: Open)";
    public const string FileTooLarge = "The file is too large to open (the limit is 10 MB)";
    public const string FileNotText = "This is not a text file";
    public const string DirectoryNotFound = "Directory not found";
    public const string NotUtf8 = "This file is not UTF-8. It is read as ";
    public const string NotUtf8Note = "Files are saved as UTF-8.";
    public const string ConvertReplace = "[A] Convert to UTF-8 and replace the file when saving";
    public const string ConvertCopy = "[B] Convert to UTF-8 and save as a copy (the file is left as it is)";
    public const string ConvertCancel = "[Esc] Cancel";
    public const string CopyDataFileYn = "Copy the data file to the data directory? " + YesNo;
    public const string CopyDataFileNote = "Data files are opened from the Marbles data directory.";

    // Spell checking
    public const string SpellFileInvalid = "Not a spell file that can be used (a UTF-8 .spl file is needed)";
    public const string SpellFileNotFound = "Spell file not found: ";
    public const string SpellNoLanguage = "No language given";
    public const string SpellNoWord = "No word to add";
    public const string SpellOff = "Spell checking is off (turn it on with :spell)";
    public const string SpellOn = "Spell checking: ";
    public const string SpellTurnedOff = "Spell checking off";
    public const string SpellNoMore = "No misspelled words found";
    public const string SpellNoSuggestions = "No suggestions for: ";
    public const string SpellWordAdded = "Added to the word list: ";
    public const string SpellSuggestions = "Spelling";
    public const string SpellCount = " misspelled";
    public const string SpellAddWord = "Add to word list";
    public const string SpellStatus = "SPELL";

    // Command line
    public const string NotACommand = "Not a command: ";
    public const string CommandPrompt = "COMMAND:"; // Shown in front of a command being typed
    public const string SelectFileDirectory = "File/Directory";

    // Editor status line
    public const string EditorModeNormal = "-- NORMAL --";
    public const string EditorModeInsert = "-- INSERT --";
    public const string EditorModeVisual = "-- VISUAL --";
    public const string EditorModeVisualLine = "-- V-LINE --";
    public const string EditorModeVisualBlock = "-- V-BLOCK --";
    public const string EditorMemory = "mem";
    public const string EditorSaved = "Saved";
    public const string EditorNoChanges = "No changes to save";

    // Legacy data file conversion
    public const string LegacyFileConverted = "Data file converted to the current file format";
    public const string LegacyFileCopyColon = "A copy of the original data file was kept as: ";
    public const string LegacyFileNotConverted = "Unable to convert the data file to the current file format:";

    // Error messages
    public const string ErrorDuplicatingEntry = "Error: Unable to duplicate entry.";
    public const string FileNotFound = "File not found.";
    public const string NoFiles = "No files in directory.";
    public const string EmptyDirectory = "(0)";
    public const string NoDataFiles = "[No data files]";
    public const string ErrorReadingFile = "Error reading file.";
    public const string ErrorDataDamaged = "The content of the data file is damaged and cannot be read.";
    public const string ErrorSortingList = "Error sorting list...";
    public const string StatusUnableToSave = "Error: Unable to save changes.";
    public const string ErrorUnableToCompleteRequest = "Error: Unable to complete request.";
    public const string ErrorMovingFile = "Unable to move file. Action aborted.";
    public const string ErrorFileAlreadyExist = "A file with the same name already exists in the target location.";
    public const string SelectTargetDirectory = "Please select a valid target directory...";
    public const string StatusUnableToSaveBackup = "Error: Unable to save backup (path invalid). Backup disabled.";
    public const string ErrorBufferSize = "Attention: Unable to use specified output dimensions. Reverting to default.";
    public const string ErrorClientSize = "Current terminal dimensions are unsupported.";
    public const string ErrorUnsupportedAction = "Action unsupported.";


    // Technical (internal identifiers) DO NOT CHANGE
    public const string NoData = "nodata";
    public const string InvalidData = "invaliddata";
    public const string Success = "ok";
    public const string Cleared = "cleared";
    public const string BTrue = "true";
    public const string BFalse = "false";
    public const string CancelCommand = "#cancelcommand";

}