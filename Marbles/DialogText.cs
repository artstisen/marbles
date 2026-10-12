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
    public static string About => Lang.T(
        "Marbles is written in C# and uses UTF-8 console I/O encoding and AES-256\n" +
        "data encryption. Your notes and settings are stored in the \"data\" directory\n" +
        "which is located alongside the Marbles program files. You can copy the data\n" +
        "directory or the entire Marbles directory to any backup/portable/cloud medium\n" +
        "if needed, or enable automatic backup to a secondary location.\n\n" +
        "CAUTION: It is not possible to recover your notes if you forget your\n" +
        "password.\n" +
        "This application is cross-platform software that can run on different\n" +
        "systems. Due to the difference between terminal programs and their\n" +
        "tradition of using specific keyboard shortcuts for built-in functions,\n" +
        "Marbles is operated with plain keys and commands in the style of Vim.\n" +
        "Press ? for help.");

    //                                                                                \n <- Max line length
    // --------------------------------------------------------------------------------
    public static string MultipleInstances => Lang.T("TIP: Run multiple instances of Marbles using folder duplicates with different\n" +
        "names. Change color settings to distinguish between instances. Running\n" +
        "multiple instances from the same folder can overwrite notes and application\n" +
        "settings when sharing the same pool of data.\n");


    // Mode labels and Caption
    public static string LogReview => Lang.T("Log review");
    public static string CaptionsMergeExport => Lang.T("Merge/Export");
    public static string CaptionsMerge => Lang.T("Merge");
    public static string CaptionsLogin => Lang.T("Login");
    public static string NewPassword => Lang.T("New password");
    public static string CaptionsPassword => Lang.T("Password");
    public static string TitleResetPassword => Lang.T("Reset password");
    public static string TitleSearch => Lang.T("Search");
    public static string CaptionsSettings => Lang.T("Settings");
    public static string CaptionsOptions => Lang.T("Options");
    public static string CaptionsMoreOptions => Lang.T("More options");
    public static string CaptionsColorScheme => Lang.T("Edit color scheme");
    public static string CaptionsMenu => Lang.T("Menu");
    public static string LeaveKeys => Lang.T("j/k 1-3 Select | Enter Open | Esc Back");
    public static string MenuKeys => Lang.T("j/k Select | l/Enter Open | Tab Box | h Back | ? Help | X Logout");
    // The keys of a list of entries (All, Top 20, Favorites, Session history, Search)
    public static string ListKeys => Lang.T("Enter Open | h/l Page | gg/G First/Last | ? Help | Esc Back");
    public static string HelpSectionMainMenu => Lang.T("Main menu");
    public static string SchemeEditorKeys => Lang.T("j/k Select | l Pick | T Text | E Reset | S Save | Tab Preview | Esc Close");
    public static string SchemePreviewKeys => Lang.T("Tab Colors | S Save | ? Help | Esc Close");
    public static string HelpSectionColorScheme => Lang.T("Color scheme editor");
    public static string CaptionsExport => Lang.T("Export");
    public static string CaptionsImport => Lang.T("Import");
    public static string CaptionsBackup => Lang.T("Backup");
    public static string CaptionsInstructions => Lang.T("Instructions");
    public static string InspectInfoCaption => Lang.T("Entry info");
    public static string CaptionsInfo => Lang.T("INFO");
    public static string CaptionsRecall => Lang.T("RECALL");
    public static string CaptionsClear => Lang.T("CLEAR");
    public static string CaptionsEdit => Lang.T("EDIT");
    // The mode of the editor, on the divider above the text (EDIT for Insert mode)
    public static string DividerNavigate => Lang.T("NAVIGATE");
    public static string DividerSelect => Lang.T("SELECT");
    public static string DividerCommand => Lang.T("COMMAND");
    public static string CaptionsEditor => Lang.T("Editor");
    public static string CaptionsTop20 => Lang.T("TOP 20");
    public static string CaptionsFavorites => Lang.T("FAVORITES");
    public static string CaptionsSession => Lang.T("SESSION HISTORY");
    public static string CaptionsSearch => Lang.T("Search");
    public static string CaptionsAll => Lang.T("ALL");
    public static string EditTitle => Lang.T("Edit title");
    public static string CaptionsDelete => Lang.T("Delete");
    public static string CaptionsDuplicate => Lang.T("Duplicate");
    public static string CaptionsRevert => Lang.T("Revert");
    public static string CaptionsRestore => Lang.T("Restore");
    public static string CaptionsStringMemory => Lang.T("String memory");
    public static string CaptionsRanking => Lang.T("Move pinned");
    public static string MovePinnedKeys => Lang.T("MOVE | j/k Move the entry up/down | Esc Done");
    public static string CaptionsColorize => Lang.T("Set color");
    public static string PinColorCaption => Lang.T("Pinned color");
    public static string ColorDefault => Lang.T("[Default]");

    // The find-and-replace box of the editor
    public static string ReplaceCaption => Lang.T("Replace");
    public static string ReplaceFind => Lang.T("Find:");
    public static string ReplaceWith => Lang.T("Replace with:");
    public static string ReplaceMatchCase => Lang.T("Match case");
    public static string ReplaceRegex => Lang.T("Regular expression");
    public static string ReplaceWhere => Lang.T("Where:");
    public static string ReplaceWholeText => Lang.T("Whole text");
    public static string ReplaceCurrentLine => Lang.T("Current line");
    public static string ReplaceSelectedLines => Lang.T("Selected lines");
    public static string ReplaceRun => Lang.T("Replace all");
    public static string ReplaceKeys => Lang.T("Enter/Space Edit or change | j/k Select | Esc Cancel");
    public static string ColorCaption => Lang.T("Color");
    public static string TextColorCaption => Lang.T("Text color");
    public static string BackgroundCaption => Lang.T("Background");
    public const string BackgroundDefault = "default"; // The setting for the background of the terminal
    public static string Favorites => Lang.T("Favorites");
    public static string Session => Lang.T("Session");
    public static string New => Lang.T("New");
    public static string NewFile => Lang.T("New file");
    public static string RenameFile => Lang.T("Rename file");
    public static string CopyFile => Lang.T("Copy file");
    public static string MoveFile => Lang.T("Move file");
    public static string DeleteFile => Lang.T("Delete file");
    public static string MergeFiles => Lang.T("Merge/Export");
    public static string Transfer => Lang.T("Transfer");
    public static string FileInfo => Lang.T("Info");
    public static string Save => Lang.T("Save");
    public static string Restart => Lang.T("Restart");
    public static string Clear => Lang.T("Clear");
    public static string Log => Lang.T("Log");
    public static string Refresh => Lang.T("Refresh");
    public static string Exit => Lang.T("Exit");
    public static string Title => Lang.T("Title");
    public static string Select => Lang.T("Select");
    public static string SortColon => Lang.T("Sort:");
    public const string TabBrackets = "(Tab)";
    public static string SortOptionDateChanged => Lang.T("Date changed");
    public static string FavoritesSortColumn => Lang.T("Date changed/Custom");
    public static string CaptionsKeyError => Lang.T("Keyboard");

    // Mode texts
    public static string EnterQueryForSearch => Lang.T("Enter query and press Enter");
    public static string EditTitleAndPressEnter => Lang.T("Change title and press Enter");
    public static string EnterTitleAndPressEnter => Lang.T("Enter title and press Enter");
    public static string EnterFileNameAndPressEnter => Lang.T("Enter file name and press Enter");
    public static string ChangeFileNameAndPressEnter => Lang.T("Change file name and press Enter");
    public static string EnterPathAndPressEnter => Lang.T("Enter absolute directory path and press Enter");

    public const string Escape = "Esc";
    public const string Spacebar = "Spacebar";

    // Indicators/Misc. texts
    public static string TitleCopyText => Lang.T(" (copy)");
    public static string InitNoData => Lang.T("No data");
    public const string CheckedSetting = "[x]";
    public const string UnCheckedSetting = "[ ]";
    public static string NotAvailable => Lang.T("N/A");
    public static string SavIndicator => " " + Lang.T("Unsaved") + " ";

    public const string PaginationLeftRight = "<- ->";
    public static string BrowseUpDown => Lang.T("Up/Down");
    public static string ExtSelectDataFile => Lang.T("File");
    public static string ExtSelectDirectory => Lang.T("Directory");
    public static string HitAnyKeyToContinue => Lang.T("[Press any key to continue]");
    public static string HitAnyKeyOrEscape => Lang.T("[Press a key to continue/Esc to exit]");
    public static string PressEscToExitP => Lang.T("(Press Esc to exit)");
    public static string PressEscSpaceToExit => Lang.T("[Press Esc or Spacebar to cancel]");

    // Yes/No dialogs
    private static string YesNo => Lang.T("[Y] Yes  [N] No");
    public static string YesContinue => Lang.T("[E] Yes (continue editing)");
    public static string SaveQuestion => Lang.T("Save changes?");
    public static string SaveChoiceYes => Lang.T("Y    Yes, save and close");
    public static string SaveChoiceNo => Lang.T("N    No, close without saving");
    public static string SaveChoiceContinue => Lang.T("E    Save and continue editing");
    public static string SaveChoiceCancel => Lang.T("Esc  Cancel (continue editing)");

    // -------------------------------------------------------------------
    //public static string NewFileYn => Lang.T("Create a new file?") + " " + YesNo;
    public static string RenameFileYn => Lang.T("Rename selected file?");
    public static string CopyFileYn => Lang.T("Copy selected file?");
    public static string MoveFileYn => Lang.T("Move selected file?");
    public static string DeleteFileYn => Lang.T("Delete selected file?");

    public static string SaveChangesYn => Lang.T("Save changes?");
    public static string ReplaceFileYn => Lang.T("The file exists. Replace it?");
    public static string DeleteEntryYn => Lang.T("Delete entry?");
    public static string DeleteEntryYnConfirm => Lang.T("Confirm deletion:");
    public static string DuplicateEntryYn => Lang.T("Duplicate entry?");
    public static string ClearTextYn => Lang.T("Clear text?");
    public static string RevertEditYn => Lang.T("Revert to previous edit?");
    public static string RestoreDeletedYn => Lang.T("Undo last deletion?");
    public static string FitOutputDimensions => Lang.T("Follow the size of the terminal window (automatic size)?") + " " + YesNo;
    public static string EnlargeWindow => Lang.T("Make the terminal window larger and press a key");
    // The same questions in a box on top of the page (ChoiceBox), which shows the choices itself
    public static string ExitQuestion => Lang.T("Exit?");
    public static string RestartQuestion => Lang.T("Restart Marbles?");
    public static string ResetSettingsQuestion => Lang.T("Revert to default settings?");
    public static string SaveChangesQuestion => Lang.T("Save changes?");
    public static string ResetPasswordQuestion => Lang.T("Reset password?");
    public static string BackupQuestion => Lang.T("Set backup directory?");
    public static string BackupDisableQuestion => Lang.T("Disable backup?");
    public static string ImportQuestion => Lang.T("Import a text file as a new entry?");
    public static string CreateTransferQuestion => Lang.T("Prepare transfer?");
    public static string DiscardTransferQuestion => Lang.T("Discard transfer?");
    // The choices of a transfer (in a box, with their keys in front)
    public static string TransferAdd => Lang.T("Add to current transfer");
    public static string TransferComplete => Lang.T("Complete transfer");
    public static string TransferDiscard => Lang.T("Discard transfer");
    public static string ChoiceCancel => Lang.T("Cancel");
    // The boxes for picking a color scheme and a language
    public static string PickSchemeCaption => Lang.T("Color scheme");
    public static string PickSchemeQuestion => Lang.T("Select a color scheme");
    public static string PickSchemeEditor => Lang.T("Edit the color scheme...");
    public static string PickLanguageCaption => Lang.T("Language");
    public static string PickLanguageQuestion => Lang.T("Select the language");
    // The choices when an entry is made a favorite, or is a favorite already (in a box, with keys in front)
    public static string FavoriteChoicePinned => Lang.T("Yes, pinned (at the top of the favorites)");
    public static string FavoriteChoicePin => Lang.T("No, pin it (at the top of the favorites)");
    public static string FavoriteChoiceUnpin => Lang.T("No, unpin it (keep it as a favorite)");
    // The kinds of new data files, and merge or export
    public static string NewFileStandard => Lang.T("Standard data file (default)");
    public static string NewFileCalendar => Lang.T("Calendar");
    public static string MergeChoice => Lang.T("Merge two data files into a new one");
    public static string ExportChoice => Lang.T("Export a data file to a new one");
    public static string RemoveFavoriteYn => Lang.T("Remove from favorites?");
    public static string RemoveFavoriteOrPinYn => Lang.T("Remove from favorites?");
    public static string AddFavoriteYn => Lang.T("Add to favorites?");
    public static string ClearStringMemory => Lang.T("Clear string memory?");

    // Selection dialogs

    public static string ImportYnNote => Lang.T("The file is selected in the file browser.");
    public static string ExportedTo => Lang.T("Saved as ");
    public static string SelectOption => Lang.T("Select option...");
    public static string SelectFileOption => Lang.T("Select file preset");
    public static string SelectTransferOption => Lang.T("Select transfer option");
    public static string CanNotBeUndone => Lang.T("This action can not be undone!");
    public static string FileMoveInstructions => Lang.T("Move file: Select target directory and press Enter...");
    public static string PinnedEntryPosition => Lang.T("Only pinned entries can change position");
    public static string PinnedEntryColors => Lang.T("Only pinned entries can change color");

    // Input/Output/Information
    public static string TitleColon => Lang.T("Title:");
    public static string NameColon => Lang.T("Name:");
    public static string FileColon => Lang.T("File:");
    public static string KeyColon => Lang.T("Key:");
    public static string PathColon => Lang.T("Path: ");
    public static string SearchColon => Lang.T("Search:");
    public static string DateCreatedColon => Lang.T("Created: ");
    public static string DateChangedColon => Lang.T("Changed: ");
    public static string EnterNewPasswordColon => Lang.T("Create new password:");
    public static string RepeatNewPasswordColon => Lang.T("Repeat new password:");
    public static string EnterCurrentPasswordColon => Lang.T("Enter current password:");
    public static string PasswordColon => Lang.T("Password:");

    public static string CurrentBackupPathColon => Lang.T("Current path: ");

    public static string AttentionColon => Lang.T("Attention: ");

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
    public static string MergeCompleted => Lang.T("Merge completed");
    public static string ExportCompleted => Lang.T("Export completed");
    public static string Summary => Lang.T("Summary");

    // Status messages (styled)
    public static string NoEntrySelected => Lang.T("No entry selected");
    public static string PasswordLength => Lang.T("Password must be between 8 and 128 characters long");
    public static string PasswordMismatch => Lang.T("The two passwords do not match");
    public static string InvalidPasswordDot => Lang.T("Invalid password."); // Must have period.
    public static string CapsLockNotice => Lang.T("Make sure Caps Lock is not enabled.");
    public static string PasswordCreated => Lang.T("Password created");
    public static string InvalidFileName => Lang.T("Invalid file name");
    // What is wrong with a new name of a file or directory (Tools.FileNameProblem)
    public static string NameExists => Lang.T("A file or directory with that name already exists");
    public static string DataFileExists => Lang.T("A data file with that name already exists");
    public static string NameControlCharacters => Lang.T("A name cannot contain control characters");
    public static string NameEnd => Lang.T("A name cannot end with a dot or a space");
    public static string NameTooLong => Lang.T("A name can be at most 255 characters long");
    public static string NameCharacter(char c) => Lang.F("A name cannot contain the character {0}", c.ToString());
    public static string NameReserved(string name) => Lang.F("{0} is a name kept by Windows and cannot be used", name);
    public static string NoRecallHistoryFound => Lang.T("No recall history found");
    public static string NoBackupFound => Lang.T("No edit history to revert to");
    public static string NoDeletionBackupFound => Lang.T("No restore data available");
    public static string UnableToCompleteRestore => Lang.T("Unable to complete restore");
    public static string EntryRestored => Lang.T("Edit reverted");
    public static string DeletionRestored => Lang.T("Deletion restored");
    public static string NoEntriesFound => Lang.T("No entries found");
    public static string NoFavoritesFound => Lang.T("No favorites found");
    public static string NoSessionData => Lang.T("No session data");
    public static string NoEntriesSelected => Lang.T("No entries selected");
    public static string EntryDeleted => Lang.T("Entry deleted");
    public static string FileDeleted => Lang.T("File deleted");
    public static string BackupDisabled => Lang.T("Backup disabled");
    public static string BackupEnabled => Lang.T("Backup enabled");
    public static string Saving => Lang.T("Saving...");
    public static string FileNoEntries => Lang.T("File contains no entries");
    public static string InvalidBackupPath => Lang.T("Invalid backup path");

    // Un-styled status messages
    public static string LoadComplete => Lang.T("Load complete.");
    public static string Loading => Lang.T("Loading...");
    public static string Reading => Lang.T("Reading...");

    // File browser
    public static string CaptionsFiles => Lang.T("Files");
    public static string BrowseFilesSelectMode => Lang.T("Files/Select mode");

    // The page shown at startup
    public static string StartSelectMode => Lang.T("Select mode");
    public static string StartModeTab => Lang.T("Mode");
    public static string StartNotes => Lang.T("Note Management (encrypted notes)");
    public static string StartEditor => Lang.T("Text Editor/File Management");
    public static string StartConfiguration => Lang.T("Configuration");
    public static string StartExit => Lang.T("Exit");
    public static string QuickGuide => Lang.T("Quick guide");
    public static string QuickGuideKeys => Lang.T("| h/l Page | j/k Line | ? All keys | Esc Close");
    public static string CopyDeleteFile => Lang.T("Copy/Delete file");
    public static string StartConfigSelect => Lang.T("Show choices on startup");
    public static string StartConfigNotes => Lang.T("Start in Note Management");
    public static string StartConfigEditor => Lang.T("Start in Text Editor (file browser)");
    public static string StartKeys => Lang.T("j/k 1-4 Select | Enter Open | Tab Configuration | ? Help | Q Exit");
    public static string StartConfigKeys => Lang.T("j/k Select | Enter/Space Change | h/l Previous/Next | Tab Page | Esc Back");
    public static string ConfigMonochrome => Lang.T("Black and white (no colors)");
    public static string ConfigAutoSize => Lang.T("Automatic size (follow the terminal window)");
    public static string ConfigSize => Lang.T("Size: ");
    public static string ConfigSizeNote => Lang.T(" (Enter: Change)");
    public static string ConfigLineNumbers => Lang.T("Line numbers in the editor");
    public static string ConfigReset => Lang.T("Reset all settings");
    public static string ConfigRestart => Lang.T("Restart Marbles");
    public static string ConfigViewLog => Lang.T("View log");
    public static string ConfigLanguage => Lang.T("Language: ");
    public static string LanguageUnknown => Lang.T("Languages: ");

    public static string EditorReloaded => Lang.T("Read from the file again");
    public static string EditorReloadUnsaved => Lang.T("Unsaved changes (:e! reads the file anyway)");
    public static string EditorReloadFilesOnly => Lang.T("Only text files can be read again");
    public static string LineNumbersOn => Lang.T("Line numbers on");
    public static string LineNumbersOff => Lang.T("Line numbers off");
    public static string WrapOn => Lang.T("Long lines are wrapped");
    public static string WrapOff => Lang.T("Long lines are not wrapped (zh zl: scroll)");
    public static string SyntaxOn => Lang.T("Colors on: ");
    public static string SyntaxOff => Lang.T("Colors off");
    public static string SyntaxUnknown => Lang.T("Use md, html, css, js, json or xml, not: ");

    // The log
    public static string LogKeys => Lang.T("| h/l Page | j/k Line | gg G First/Last | /text Filter | Esc Close");
    public const string LogCommand = "Command";
    public const string LogEditorCommand = "Editor";
    public const string LogFilesCommand = "Files";
    public const string LogFile = "File: ";
    public static string MonochromeIsOn => Lang.T("Black and white is on: the colors are used when it is turned off");
    public static string Drives => Lang.T("Drives");
    public const string DirectoryMark = "<DIR>";
    public static string DataFileMark => Lang.T("[Data file]");
    public static string FileBrowserKeys => Lang.T("j/k Select | l Open | F Faves | O History | ? Help | Esc Close");

    public static string FileBrowserDualKeys => Lang.T("Tab Pane | l Open | F Faves | O History | ? Help | Esc Close");
    public static string FileTargetDualKeys => Lang.T("Tab Pane | l Open | h Up | / Filter | S {0} | Esc Cancel");
    public static string FileTargetKeys => Lang.T("j/k Select | l Open | h Up | / Filter | S {0} | Esc Cancel");
    public static string FileCopyHere => Lang.T("Copy here");
    public static string FileMoveHere => Lang.T("Move here");
    public static string FileSelectTarget => Lang.T("Go to the directory and press S (Esc cancels)");

    // File management in the file browser
    public static string NewDirectory => Lang.T("New directory");
    public static string DeleteDirectory => Lang.T("Delete directory");
    public static string DeleteNamed => Lang.T("Delete \"{0}\"?");
    public static string DeleteMarkedCaption => Lang.T("Delete files and directories");
    public static string DeleteDataFilesNote => Lang.T("Marbles data files with your notes are among them.");
    public static string FileMarkedKeys => Lang.T("Space Mark | Y Duplicate | C Copy | M Move | P Fave | D Delete | Esc Unmark");
    // The keys of a box of choices (ChoiceBox)
    public static string ChoiceKeys => Lang.T("j/k Select | Enter Choose | Esc Cancel");
    public static string AskTextKeys => Lang.T("Enter OK | Esc Cancel");
    // The clipboard: yanked text sent to the system clipboard (OSC 52) and/or the paste bin file
    public static string CaptionsClipboard => Lang.T("Clipboard");
    public static string HelpSectionClipboard => Lang.T("Clipboard and paste bin");
    public static string ClipboardKeys => Lang.T("j/k Select | Enter/Space Change | h/l Choice | ? Help | Esc Back");
    public static string ClipboardNote => Lang.T("The system clipboard is reached through the terminal (OSC 52): it works in Windows Terminal, iTerm2, kitty, WezTerm and tmux, also over SSH, but not in the console of a Raspberry Pi or the Terminal of macOS. There the paste bin file can be used: other programs can read it, and :pbread puts its text in the editor.");
    public static string ClipboardAllOff => Lang.T("The clipboard is off (:clipboard)");
    public static string ClipboardNotesOff => Lang.T("Not allowed from notes (:clipboard)");
    public static string CopiedToClipboard => Lang.T("Copied to the clipboard");
    public static string CopiedToPasteBin => Lang.T("Copied to the paste bin");
    public static string CopiedToBoth => Lang.T("Copied (clipboard, paste bin)");
    public static string PasteBinWritten => Lang.T("Written to the paste bin");
    public static string PasteBinAdded => Lang.T("Added to the paste bin");
    public static string PasteBinInserted => Lang.T("Inserted from the paste bin");
    public static string PasteBinCleared => Lang.T("The paste bin is empty now");
    public static string PasteBinEmpty => Lang.T("The paste bin is empty");
    public static string NoFilePath => Lang.T("A note has no file path");

    // A list to pick from (the string memory: ":reglist")
    public static string PickListNoMatch => Lang.T("Nothing matches the filter");
    public static string StringMemoryKeys => Lang.T("j/k Select | Enter Insert | / Filter | Esc Close");

    // Snippets (":snip" in the editor)
    public static string CaptionsSnippets => Lang.T("Snippets");
    public static string SnippetKeys =>
        Lang.T("Enter Insert | / Filter | S Sort | A Add | R Rename | D Delete | E Edit");
    public static string SnippetFilterKeys => Lang.T("Tab Next | Enter OK | Esc Clear");
    public static string SnippetSortFile => Lang.T("Sort: as in the file");
    public static string SnippetSortTitle => Lang.T("Sort: by title");
    public static string SnippetSortUsed => Lang.T("Sort: last used first");
    public static string SnippetNone => Lang.T("No snippets yet (A: add one)");
    public static string SnippetNoMatch => Lang.T("No titles match the filter");
    public static string SnippetCreate => Lang.T("There is no file of snippets. Make one?");
    public static string SnippetAdd => Lang.T("Add a snippet");
    public static string SnippetAddPrompt => Lang.T("Type the title of the snippet and press Enter");
    public static string SnippetRename => Lang.T("Rename the snippet");
    public static string SnippetRenamePrompt => Lang.T("Type the new title and press Enter");
    public static string SnippetTitleTaken => Lang.T("A snippet has that title already");
    public static string SnippetDeleteNote => Lang.T("It is taken out of the file of snippets.");
    public static string SnippetFromNote => Lang.T("Keep text from a note as a snippet?");
    public static string SnippetFromNoteNote =>
        Lang.T("The file of snippets is not encrypted: the text can be read by anyone with access to it.");
    public static string SnippetAdded => Lang.T("Snippet added");
    public static string SnippetsFileOpen => Lang.T("This is the file of snippets");
    public static string SnippetsNotHere => Lang.T("Not while the settings are edited");
    public static string SnippetSwitchNote => Lang.T("The file of snippets is opened, and this text again when it is closed.");
    public static string MatchNeedsTwoPanes => Lang.T("Only with two panes (T shows them)");
    public static string PickNumberKeys => Lang.T("h/l Change | Enter OK | Esc Cancel");
    public static string TypeNameAndPressEnter => Lang.T("Type the name and press Enter");
    public static string ChoiceYes => Lang.T("Yes");
    public static string ChoiceNo => Lang.T("No");
    public static string DeleteFileNote => Lang.T("The file cannot be recovered");
    public static string DeleteDataFileNote => Lang.T("A Marbles data file with your notes. It cannot be recovered");
    public static string DeleteEmptyDirectoryNote => Lang.T("The directory is empty");
    public static string FileItemDeleted => Lang.T("Deleted: ");
    public static string FileCopyTo => Lang.T("Copy to: ");
    public static string FileMoveTo => Lang.T("Move to: ");
    public static string FileCopied => Lang.T("Copied to ");
    public static string FileMoved => Lang.T("Moved to ");
    public static string FileCopying => Lang.T("Copying (Esc: Cancel): ");
    public static string FileCanceled => Lang.T("Canceled");
    public static string FileIntoItself => Lang.T("A directory cannot be copied or moved into itself");
    public static string FileAlreadyThere => Lang.T("It is already in that directory");
    public static string FileUsedByMarbles => Lang.T("Used by Marbles: it cannot be renamed, moved or deleted here");

    // Information about a file in the file browser
    public static string InfoCaptionFile => Lang.T("File info");
    public static string InfoCaptionDirectory => Lang.T("Directory info");
    public static string InfoName => Lang.T("Name:");
    public static string InfoType => Lang.T("Type:");
    public static string InfoSize => Lang.T("Size:");
    public static string InfoContent => Lang.T("Content:");
    public static string InfoCreated => Lang.T("Created:");
    public static string InfoChanged => Lang.T("Changed:");
    public static string InfoFavorite => Lang.T("Favorite:");
    public static string InfoFavoriteYes => Lang.T("Yes");
    public static string InfoPath => Lang.T("Path:");
    // The info box of a data file (in the main menu and on the login page)
    public static string InfoCaptionDataFile => Lang.T("Data file info");
    public static string InfoEntries => Lang.T("Entries:");
    public static string InfoBackup => Lang.T("Backup:");
    public static string InfoDirectory => Lang.T("Directory:");
    public static string InfoTypeDirectory => Lang.T("Directory");
    public static string InfoTypeDataFile => Lang.T("Marbles data file (encrypted notes)");
    public static string InfoTypeNoExtension => Lang.T("File");
    public static string InfoTypeReadOnly => Lang.T(", read-only");

    // Searching for files in the file browser
    public static string SearchCaption => Lang.T("Search files");
    public static string SearchResultCaption => Lang.T("Files found");
    public static string HelpSectionSearch => Lang.T("File browser search");
    public static string SearchIn => Lang.T("In: ");
    public static string SearchName => Lang.T("Name:");
    public static string SearchText => Lang.T("Text in files:");
    public static string SearchSubfolders => Lang.T("This directory and its subfolders");
    public static string SearchThisDirectory => Lang.T("This directory only");
    public static string SearchRun => Lang.T("Search");
    public static string SearchNothing => Lang.T("Write a name or a text to search for");
    public static string SearchInvalidPattern => Lang.T("Not a regular expression: ");
    public static string SearchProgress => Lang.T("Searching (Esc: Stop), {0} found: ");
    public static string SearchCapped => Lang.T(" (the search stops at this number)");
    public static string SearchStopped => Lang.T(" (stopped)");
    public static string SearchResultKeys => Lang.T("Enter Open | B Show | I Info | P Fave | S Search | / Filter | Esc Close");

    // The favorites of the file browser
    public static string CaptionsFileFavorites => Lang.T("Favorite files");
    public static string HelpSectionFavorites => Lang.T("File browser favorites");
    // The history of the file browser (the files opened from it)
    public static string HelpSectionHistory => Lang.T("File history");
    public static string CaptionsFileHistory => Lang.T("File history");
    public static string HistoryEmpty => Lang.T("No files yet: the files opened from the file browser are listed here");
    public static string HistoryKeys => Lang.T("Enter Open | B Show | P Fave | D Remove | X Clear | / Filter | Esc Close");
    public static string HistoryClearCaption => Lang.T("Clear file history");
    public static string HistoryClearNote => Lang.T("The files themselves are not changed");
    public static string HistoryCleared => Lang.T("The file history is cleared");
    public static string FavoritesHeader => Lang.T("Files and directories marked as favorites (P in the file browser)");
    public static string FavoritesEmpty => Lang.T("No favorites yet: press P on a file or directory in the file browser");
    public static string FavoritesExpanded => Lang.T("All folders are open");
    public static string FavoritesCollapsed => Lang.T("All folders are closed");
    public static string FavoritesNoFolders => Lang.T("There are no folders");
    public static string FavoritesKeys => Lang.T("Enter Open | B Show | A Folder | V Move | C Color | P Remove | Esc Close");
    public static string FavoriteAdded => Lang.T("Added to the favorites");
    public static string FavoriteRemoved => Lang.T("Removed from the favorites");
    public static string FavoriteMissing => Lang.T("(missing)");
    public static string FavoriteMoveFiltered => Lang.T("Remove the filter (Esc) to move favorites and add folders");
    public static string FavoriteNewFolder => Lang.T("New folder");
    public static string FavoriteNoFolder => Lang.T("(No folder)");
    public static string FavoriteMoveOne => Lang.T("Move to folder");
    public static string FavoriteMoveMany => Lang.T("Move {0} favorites to folder");
    public static string FavoriteMovedOne => Lang.T("Moved to: ");
    public static string FavoriteMovedMany => Lang.T("{0} favorites moved to: ");
    public static string FavoriteMarkFolder => Lang.T("Only favorites can be marked and moved to a folder (not folders)");
    public static string FavoritesMarkedKeys => Lang.T("Space Mark | M To folder | T/E Top/End | D Remove | Esc Clear the marks");
    public static string FavoriteRemoveCaption => Lang.T("Remove favorites");
    public static string FavoriteRemoveMany => Lang.T("Remove the {0} marked favorites from the list?");
    public static string FavoriteRemoveNote => Lang.T("The files stay where they are");
    public static string FavoriteRemovedMany => Lang.T("{0} removed from the favorites (u: Undo)");
    public static string FavoriteRenameFolder => Lang.T("Rename folder");
    public static string FavoriteDeleteFolder => Lang.T("Remove folder");
    public static string FavoriteDeleteFolderNote => Lang.T("The {0} favorites in it are removed from the list too (the files stay)");
    public static string FavoriteFolderRemoved => Lang.T("Removed the folder: ");
    public static string FavoriteFolderName => Lang.T("The name cannot be used for a folder");
    public static string FavoriteDeleteFolderNoteOne => Lang.T("The favorite in it is removed from the list too (the file stays)");
    public static string FavoriteNothingToUndo => Lang.T("Nothing to undo");
    public static string FavoriteRestored => Lang.T("Put back in the favorites");
    public static string FavoriteRenameFile => Lang.T("Only folders are renamed here. B shows the file, where X renames it");
    public static string HelpSectionFiles => Lang.T("File browser");
    public static string HelpKeys => Lang.T("| h/l Page | j/k Line | /text Search | n/N Next/Previous | Esc Close");
    public static string HelpKeysTopic => Lang.T("| h/l Page | j/k Line | / Search | n/N Next | ? All keys | Esc Close");
    public static string HelpKeysAll => Lang.T("| h/l Page | j/k Line | / Search | n/N Next | ? This page | Esc Close");
    public static string HelpSectionStart => Lang.T("Start page");
    public static string HelpSectionLogin => Lang.T("Login page");
    public static string HelpSectionMenus => Lang.T("Lists of entries");
    public static string HelpSectionEditor => Lang.T("Editor");
    public static string HelpNotFound => Lang.T("Not found: ");
    public static string NotEncrypted => Lang.T("[Not encrypted]");
    public static string FilePickKeys => Lang.T("j/k Select | l/Enter Open/Select file | h Up | / Filter | Esc Cancel");
    public static string DirectoryPickKeys => Lang.T("j/k Select | l/Enter Open | h Up | S Select this directory | Esc Cancel");
    public static string FileTooLarge => Lang.T("The file is too large to open (the limit is 10 MB)");
    public static string FileNotText => Lang.T("This is not a text file");
    public static string DirectoryNotFound => Lang.T("Directory not found");
    public static string NotUtf8Note => Lang.T("Files are saved as UTF-8.");
    public static string ConvertReplace => Lang.T("Convert to UTF-8 and replace the file when saving");
    public static string ConvertCopy => Lang.T("Convert to UTF-8 and save a copy (the file stays as it is)");
    public static string ConvertCancel => Lang.T("Cancel");
    public static string CopyDataFile => Lang.T("Copy the data file to the data directory?");
    public static string CopyDataFileNote => Lang.T("Data files are opened from the Marbles data directory.");

    // Spell checking
    public static string SpellFileInvalid => Lang.T("Not a spell file that can be used (a UTF-8 .spl file is needed)");
    public static string SpellFileNotFound => Lang.T("Spell file not found: ");
    public static string SpellNoLanguage => Lang.T("No language given");
    public static string SpellNoWord => Lang.T("No word to add");
    public static string SpellOff => Lang.T("Spell checking is off (turn it on with :spell)");
    public static string SpellOn => Lang.T("Spell checking: ");
    public static string SpellTurnedOff => Lang.T("Spell checking off");
    public static string SpellNoMore => Lang.T("No misspelled words found");
    public static string SpellNoSuggestions => Lang.T("No suggestions for: ");
    public static string SpellWordAdded => Lang.T("Added to the word list: ");
    public static string SpellSuggestions => Lang.T("Spelling");
    public static string SpellAddWord => Lang.T("Add to word list");
    public const string SpellStatus = "SPELL";

    // Command line
    public static string NotACommand => Lang.T("Not a command: ");
    public const string CommandPrompt = "COMMAND:"; // Shown in front of a command being typed
    public static string SelectFileDirectory => Lang.T("File/Directory");

    // Editor status line
    public const string EditorModeNormal = "-- NORMAL --";
    public const string EditorModeInsert = "-- INSERT --";
    public const string EditorModeVisual = "-- VISUAL --";
    public const string EditorModeVisualLine = "-- V-LINE --";
    public const string EditorModeVisualBlock = "-- V-BLOCK --";
    public const string EditorMemory = "mem";
    public static string EditorSaved => Lang.T("Saved");
    public static string EditorNoChanges => Lang.T("No changes to save");

    // Legacy data file conversion
    public static string LegacyFileConverted => Lang.T("Data file converted to the current file format");
    public static string LegacyFileCopyColon => Lang.T("A copy of the original data file was kept as: ");
    public static string LegacyFileNotConverted => Lang.T("Unable to convert the data file to the current file format:");

    // Error messages
    public static string ErrorDuplicatingEntry => Lang.T("Error: Unable to duplicate entry.");
    public static string FileNotFound => Lang.T("File not found.");
    public static string NoFiles => Lang.T("No files in directory.");
    public const string EmptyDirectory = "(0)";
    public static string NoDataFiles => Lang.T("[No data files]");
    public static string ErrorReadingFile => Lang.T("Error reading file.");
    public static string ErrorDataDamaged => Lang.T("The content of the data file is damaged and cannot be read.");
    public static string ErrorSortingList => Lang.T("Error sorting list...");
    public static string StatusUnableToSave => Lang.T("Error: Unable to save changes.");
    public static string ErrorUnableToCompleteRequest => Lang.T("Error: Unable to complete request.");
    public static string ErrorMovingFile => Lang.T("Unable to move file. Action aborted.");
    public static string ErrorFileAlreadyExist => Lang.T("A file with the same name already exists in the target location.");
    public static string SelectTargetDirectory => Lang.T("Please select a valid target directory...");
    public static string StatusUnableToSaveBackup => Lang.T("Error: Unable to save backup (path invalid). Backup disabled.");
    public static string ErrorBufferSize => Lang.T("Attention: Unable to use specified output dimensions. Reverting to default.");
    public static string ErrorClientSize => Lang.T("Current terminal dimensions are unsupported.");
    public static string ErrorUnsupportedAction => Lang.T("Action unsupported.");


    // Technical (internal identifiers) DO NOT CHANGE
    public const string NoData = "nodata";
    public const string InvalidData = "invaliddata";
    public const string Success = "ok";
    public const string Cleared = "cleared";
    public const string BTrue = "true";
    public const string BFalse = "false";
    public const string CancelCommand = "#cancelcommand";

}