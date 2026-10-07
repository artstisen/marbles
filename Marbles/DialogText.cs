namespace Marbles;

internal static class DialogText
{
    // Application information
    public const string ApplicationName = "Marbles";
    public const string ApplicationCopyright = "Copyright 2026 LBS";
    public const string ApplicationBy = "by LBS (c)2026";
    public const string ApplicationVersion = "v1.1.0"; // For display. Right click the Marbles project icon to set assembly version

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
    public const string WelcomeTo = "Welcome to " + ApplicationName;
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
    public const string CaptionsContentSearch = "Content search";
    public const string CaptionsEnterNumber = "Enter number";
    public const string CaptionsAll = "ALL";
    public const string EditTitle = "Edit title";
    public const string CaptionsDelete = "Delete";
    public const string CaptionsDuplicate = "Duplicate";
    public const string CaptionsRevert = "Revert";
    public const string CaptionsRestore = "Restore";
    public const string CaptionsStringMemory = "String memory";
    public const string CaptionsRanking = "Arrange pinned";
    public const string CaptionsColorize = "Set color";
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
    public const string SessionSortColumn = "";
    public const string CaptionsKeyError = "Keyboard";

    // Mode texts
    public const string EnterQueryForSearch = "Enter query and press Enter";
    public const string EditTitleAndPressEnter = "Change title and press Enter";
    public const string EnterTitleAndPressEnter = "Enter title and press Enter";
    public const string EnterFileNameAndPressEnter = "Enter file name and press Enter";
    public const string ChangeFileNameAndPressEnter = "Change file name and press Enter";
    public const string EnterPathAndPressEnter = "Enter absolute directory path and press Enter";
    public const string PressTheDesiredKey = "Press the desired key";

    // Keyboard
    public const string CtrlPlus = "CTRL+";
    public const string ShiftPlus = "SHIFT+";
    public const string CtrlShift = "CTRL+SHIFT+";
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

    public const string MemoryIndicatorLineCopy = "String memory:";

    //public const string MemoryIndicatorRepeat = "Repeat:";
    //public const string MemoryIndicatorRepeat = "CTRL+K:";
    public const string ExtExample = "[Example]";
    public const string PaginationLeftRight = "<- ->";
    public const string BrowseUpDown = "Up/Down";
    public const string ExtSelectDataFile = "File";
    public const string ExtSelectDirectory = "Directory";
    public const string HitAnyKeyToContinue = "[Press any key to continue]";
    public const string HitAnyKeyOrEscape = "[Press a key to continue/Esc to exit]";
    public const string PressEscToExitP = "(Press Esc to exit)";
    public const string PressEscToExitS = "[Press Esc to exit]";
    public const string PressEscToExit = "Press Esc to exit";
    public const string PressStoRetryEscToExit = "Press S to retry/Esc to exit";
    public const string PressEscSpaceToExit = "[Press Esc or Spacebar to cancel]";

    // Yes/No dialogs
    private const string YesNo = "[Y] Yes  [N] No";
    private const string YesNoCancel = "[Y] Yes  [N] No  [Esc] Cancel";
    public const string YesContinue = "[E] Yes (continue editing)";
    private const string YesPinnedNo = "[Y] Yes  [P] Pinned  [N] No";
    private const string YesNoPinned = "[Y] Yes  [N] No  [P] Pin";

    // -------------------------------------------------------------------
    //public const string NewFileYn = "Create a new file? " + YesNo;
    public const string RenameFileYn = "Rename selected file? " + YesNo;
    public const string CopyFileYn = "Copy selected file? " + YesNo;
    public const string MoveFileYn = "Move selected file? " + YesNo;
    public const string DeleteFileYn = "Delete selected file? " + YesNo;
    public const string CreateTransfer = "Prepare transfer? " + YesNo;
    //public const string ToggleRanking = "Enable ranking? " + YesNo;

    //public const string SaveChangesYN = "Save changes? " + YesNo;
    public const string SaveChangesYnCancel = "Save changes? " + YesNoCancel;
    public const string SaveChangesYn = "Save changes? " + YesNo;
    public const string ExitYn = "Exit? " + YesNo;
    public const string RestartYn = "Restart Marbles? " + YesNo;
    public const string EditorDisableUndoCache = "Disable undo cache? " + YesNo;
    public const string ExportYn = "Export as .txt? " + YesNo;
    public const string DeleteEntryYn = "Delete entry? " + YesNo;
    public const string DeleteEntryYnConfirm = "Confirm deletion: " + YesNo;
    public const string DuplicateEntryYn = "Duplicate entry? " + YesNo;
    public const string ClearTextYn = "Clear text? " + YesNo;
    public const string RevertEditYn = "Revert to previous edit? " + YesNo;
    public const string RestoreDeletedYn = "Undo last deletion? " + YesNo;
    public const string ResetPassword = "Reset password? " + YesNo;
    public const string ResetOutputDimensions = "Reset " + ApplicationName + " output dimensions? " + YesNo;
    public const string ResetSettings = "Revert to default settings? " + YesNo;
    public const string RemoveFavoriteYn = "Remove from favorites? " + YesNo;
    public const string RemoveFavoriteOrPinYn = "Remove from favorites? " + YesNoPinned;
    public const string AddFavoriteYn = "Add to favorites? " + YesPinnedNo;
    public const string ImportYn = "Import .txt? " + YesNo;
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

    // Additional dialog texts
    public const string EditorDisableUndoCacheNote = "This operation will clear the current undo cache";
    public const string ImportYnNote = "Entry text is imported from ";
    public const string ExportYnNote = "Entry text is exported to ";
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

    //public const string TotalCountColon = "Total count: ";
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

    //public const string InitClearingFields = "Clearing program memory...";
    public const string DirectoryCreated = "Directory created";
    public const string DefaultSettings = "Default settings";

    // Merge tool
    public const string MergeCompleted = "Merge completed";
    public const string ExportCompleted = "Export completed";
    public const string Summary = "Summary";

    // Status messages (styled)
    public const string NoEntrySelected = "No entry selected";
    public const string PasswordLength = "Password must be between 8 and 32 characters long";
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
    public const string UnableToFind = "Unable to find";
    public const string EntryDeleted = "Entry deleted";
    public const string FileDeleted = "File deleted";
    public const string BackupDisabled = "Backup disabled";
    public const string BackupEnabled = "Backup enabled";
    public const string Saving = "Saving...";
    public const string FileNoEntries = "File contains no entries";
    public const string InvalidBackupPath = "Invalid backup path";
    public const string EditorScrollIndicator = "Scroll indicator: ";
    public const string EditorUndoCache = "Undo cache: ";
    public const string StatusDisabled = "Disabled";
    public const string StatusEnabled = "Enabled";

    // Un-styled status messages
    public const string LoadComplete = "Load complete.";
    public const string Loading = "Loading...";
    public const string Reading = "Reading...";

    // Command line
    public const string NotACommand = "Not a command: ";
    public const string SelectFileDirectory = "File/Directory";

    // Editor status line
    public const string EditorModeNormal = " NORMAL ";
    public const string EditorModeInsert = " INSERT ";
    public const string EditorModeVisual = " VISUAL ";
    public const string EditorModeVisualLine = " V-LINE ";
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