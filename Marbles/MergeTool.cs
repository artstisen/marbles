namespace Marbles;

internal class MergeTool
{
    private List<Entry> _sourceEntries1;
    private List<Entry> _sourceEntries2;
    private List<Entry> _targetEntries;
    private List<string> _filesList;
    private LoadStatus _load;
    private int _fileIndex;
    private int _result;
    private string _selectedFileName;
    private string _sourceFileName1;
    private string _sourceFileName2;
    public MergeTool()
    {
        _filesList = new List<string>();
        _load = new LoadStatus();
        _selectedFileName = string.Empty;
        _sourceEntries1 = new List<Entry>();
        _sourceEntries2 = new List<Entry>();
        _sourceFileName1 = string.Empty;
        _sourceFileName2 = string.Empty;
        _targetEntries = new List<Entry>();
    }

    internal void MergeExport()
    {
        var selection = ChoiceBox.Pick(DialogText.CaptionsMergeExport, DialogText.SelectOption, string.Empty,
            new[] { DialogText.MergeChoice, DialogText.ExportChoice, DialogText.ChoiceCancel },
            new[] { "A", "B", "Esc" }, 0, false);
        switch (selection)
        {
            case 0:
                Merge();
                break;
            case 1:
                Export();
                break;
            case 2:
                return;
        }
    }



    // There are no data files to merge or export (the caption tells which)
    private void NoFiles(string caption)
    {
        ChoiceBox.Message(caption, DialogText.NoFiles, string.Empty);
        MergeExport();
    }

    private void Merge()
    {
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsMerge, string.Empty, 0);
        Tools.CursorDown(1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.CursorDown();
        Console.WriteLine(" " + DialogText.CaptionsInstructions);
        Tools.CursorDown(1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        WriteParagraph(Lang.T("This tool will merge two existing Marbles data files into a new file. The source " +
                              "files must be located within the Marbles \"data\" directory or selected subdirectory. " +
                              "You will be asked to select each file and type their passwords for decryption. The new " +
                              "target file will be saved with encryption in the Marbles \"data\" directory with a file " +
                              "name and password of your choosing. The original source files remain unchanged after " +
                              "the merge."));
        Tools.CursorDown(1);
        // There will be created new ID's for each of the merged entries this discarding old entry IDs
        // All other data will be kept. Clear all merge memory on completion/cancel
        Tools.CursorDown(1);
        Tools.CursorRight(1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (Keyboard.AwaitAnyKeyOrEscape())
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            return;
        }
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        _selectedFileName = string.Empty;
        _sourceFileName1 = string.Empty;
        _sourceFileName2 = string.Empty;
        _sourceEntries1 = new List<Entry>();
        _sourceEntries2 = new List<Entry>();
        _targetEntries = new List<Entry>();
        _fileIndex = 0;
        RunMergeSteps();
        DoCleanUp();
    }



    private void RunMergeSteps()
    {
        // Read the filenames into memory
        RefreshFilesList();
        if (_filesList.Count > 0)
        {
            // ---------- FILE 1 -----------
            TryAgain1:
            WriteStepInformation(" " + Lang.T("Step 1: Select source file 1"), DialogText.CaptionsMerge);
            Tools.CursorDown(1);
            _result = PageFiles();
            //  Act on result
            if (_result == 1)
            {
                _sourceFileName1 = _selectedFileName;
                FileIo.SetFileName(_sourceFileName1);
                // Enter password and load entries into memory
                WriteStepInformation(" " + Lang.T("Step 1: Enter password for source file 1"), DialogText.CaptionsMerge);
                Tools.CursorReturn();
                Tools.CursorDown(1);
                Console.Write(" " + DialogText.PasswordColon + " ");
                Encryptor.SetKey(Tools.ReadMaskedSimple(false));
                // Try loading, and retry if failing
                _sourceEntries1 = new List<Entry>();
                Entries.ClearAllEntries(false);
                _load = new LoadStatus();
                _load = FileIo.LoadEntries();
                if (_load.Success)
                {
                    for (int i = 0; i < Entries.GetEntries().Count; i++)
                    {
                        _sourceEntries1.Add(Entries.CloneEntry(Entries.GetEntries()[i], true));
                    }

                    if (_sourceEntries1.Count == 0)
                    {
                        // Goto retry
                        WriteStepAlert(DialogText.FileNoEntries);
                        goto TryAgain1;
                    }
                }
                else
                {
                    // Goto retry
                    WriteStepAlert(_load.StatusMessage);
                    goto TryAgain1;
                }


                //  ---------- FILE 2 -----------
                TryAgain2:
                WriteStepInformation(" " + Lang.T("Step 2: Select source file 2"), DialogText.CaptionsMerge);
                Tools.CursorDown(1);
                _result = PageFiles();
                if (_result == 1)
                {
                    _sourceFileName2 = _selectedFileName;
                    if (_sourceFileName1 != _sourceFileName2)
                    {
                        FileIo.SetFileName(_sourceFileName2);
                        // Enter password and load entries into memory
                        WriteStepInformation(" " + Lang.T("Step 2: Enter password for source file 2"), DialogText.CaptionsMerge);
                        Tools.CursorReturn();
                        Tools.CursorDown(1);
                        Console.Write(" " + DialogText.PasswordColon + " ");
                        Encryptor.SetKey(Tools.ReadMaskedSimple(false));
                        // Try loading, and retry if failing
                        _sourceEntries2 = new List<Entry>();
                        Entries.ClearAllEntries(false);
                        _load = new LoadStatus();
                        _load = FileIo.LoadEntries();
                        if (_load.Success)
                        {
                            for (int i = 0; i < Entries.GetEntries().Count; i++)
                            {
                                _sourceEntries2.Add(Entries.CloneEntry(Entries.GetEntries()[i], true));
                            }

                            if (_sourceEntries2.Count == 0)
                            {
                                // Goto retry
                                WriteStepAlert(DialogText.FileNoEntries);
                                goto TryAgain2;
                            }
                        }
                        else
                        {
                            // Goto retry
                            WriteStepAlert(_load.StatusMessage);
                            goto TryAgain2;
                        }
                        var merging = true;
                        do
                        {
                            //  ---------- FILE 3 -----------
                            WriteStepInformation(" " + Lang.T("Step 3: Enter target file name"), DialogText.CaptionsMerge);
                            Tools.CursorReturn();
                            Tools.CursorDown(1);
                            Console.Write(" " + DialogText.NameColon + " ");
                            // Enter file name (Esc: back to the choice of merge or export)
                            var typedName = Tools.ReadLineOrEscape(32);
                            if (typedName == null)
                            {
                                Tools.RemovePrivileges(false);
                                MergeExport();
                                return;
                            }

                            _selectedFileName = typedName;
                            // Check file name
                            if (Tools.IsValidFileName(_selectedFileName))
                            {
                                // Add file name extension
                                _selectedFileName = _selectedFileName + FileIo.GetFileNameExtension();
                                // Check if the file exists
                                if (!File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), _selectedFileName)))
                                {
                                    // Set as current filename
                                    FileIo.SetFileName(_selectedFileName);
                                    TryAgain3:
                                    WriteStepInformation(" " + Lang.T("Step 3: Enter a new password for the target file"), DialogText.CaptionsMerge);
                                    Tools.CursorReturn();
                                    Tools.CursorDown(1);
                                    Console.Write(" " + DialogText.EnterNewPasswordColon + " ");
                                    var newPass = Tools.ReadMaskedSimple(false);
                                    string newPassRepeat;
                                    if (!Encryptor.IsValidPasswordLength(newPass))
                                    {
                                        WriteStepAlert(DialogText.PasswordLength);
                                        goto TryAgain3;
                                    }
                                    else
                                    {
                                        Tools.CursorReturn();
                                        Tools.CursorDown();
                                        Console.Write(" " + DialogText.RepeatNewPasswordColon + " ");
                                        newPassRepeat = Tools.ReadMaskedSimple(false);
                                    }

                                    // Compare and repeat process from the beginning if the two passwords doesnt match
                                    if (newPass != newPassRepeat)
                                    {
                                        WriteStepAlert(DialogText.PasswordMismatch);
                                        goto TryAgain3;
                                    }
                                    else
                                    {
                                        // Submit new password
                                        Encryptor.SetKey(newPass);
                                        // Do merge
                                        _targetEntries = new List<Entry>();
                                        // Clone entries into new list
                                        foreach (var t in _sourceEntries1)
                                        {
                                            _targetEntries.Add(Entries.CloneEntry(t, false));
                                        }
                                        foreach (var t in _sourceEntries2)
                                        {
                                            _targetEntries.Add(Entries.CloneEntry(t, false));
                                        }

                                        // Sort the list
                                        try
                                        {
                                            // Sort by DateCreated desc
                                            _targetEntries.Sort((x, y) =>
                                                DateTime.Compare(Convert.ToDateTime(y.DateCreated),
                                                    Convert.ToDateTime(x.DateCreated)));
                                        }
                                        catch
                                        {
                                            WriteStepAlert(DialogText.ErrorSortingList);
                                        }

                                        // Add collected entries to internal collection
                                        Entries.SetAllEntries(_targetEntries);
                                        // Save the file
                                        Entries.SaveEntriesToFile();
                                        merging = false;
                                        Init.Log("Merge (S): " + _sourceFileName1, false);
                                        Init.Log("Merge (S): " + _sourceFileName2, false);
                                        Init.Log("Merge (T): -> " + _selectedFileName, false);
                                        // The summary in a box on top of the page
                                        ChoiceBox.ShowRows(DialogText.MergeCompleted, new List<(string Label, string Value)>
                                        {
                                            (Lang.T("Source read 1:"), _sourceEntries1.Count + " (" + _sourceFileName1 + ")"),
                                            (Lang.T("Source read 2:"), _sourceEntries2.Count + " (" + _sourceFileName2 + ")"),
                                            (Lang.T("Target write:"), _targetEntries.Count.ToString()),
                                            (Lang.T("File name:"), _selectedFileName),
                                            (Lang.T("File size:"), FileIo.GetFileSize(true))
                                        }, 64);
                                        Tools.RemovePrivileges(false);
                                        MergeExport();
                                    }
                                }
                                else
                                {
                                    WriteStepAlert(DialogText.InvalidFileName);
                                }
                            }
                            else
                            {
                                WriteStepAlert(DialogText.InvalidFileName);
                            }

                        }
                        while (merging);

                    }
                    else
                    {
                        WriteStepAlert(Lang.T("The specified filenames are identical"));
                        goto TryAgain2;
                    }
                }
                else if (_result == 0)
                {
                    MergeExport();
                }
            }
            else if (_result == 0)
            {
                MergeExport();
            }
        }
        else
        {
            NoFiles(DialogText.CaptionsMerge);
        }
    }

    private void Export()
    {
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsExport, string.Empty, 0);
        Tools.CursorDown(1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.CursorDown();
        Console.WriteLine(" " + DialogText.CaptionsInstructions);
        Tools.CursorDown(1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        WriteParagraph(Lang.T("This tool will export an existing Marbles data file as XML. You will be asked to " +
                              "enter the password for decryption. The exported file will be saved in the Marbles " +
                              "\"data\" directory (or selected subdirectory) with a file name of your choosing."));
        Tools.CursorDown(1);
        Console.Write(" ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(" " + Lang.T("ATTENTION:") + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(" " + Lang.T("Entries are exported as readable text."));
        Tools.CursorReturn();
        Tools.CursorDown(2);
        Tools.CursorRight(1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (Keyboard.AwaitAnyKeyOrEscape())
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            return;
        }
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        RefreshFilesList();
        if (_filesList.Count > 0)
        {
            TryExportAgain1:
            WriteStepInformation(" " + Lang.T("Step 1: Select file"), DialogText.CaptionsExport);
            Tools.CursorDown(1);
            _result = PageFiles();
            //  Act on result
            if (_result == 1)
            {
                _sourceFileName1 = _selectedFileName;
                FileIo.SetFileName(_sourceFileName1);
                // Enter password and load entries into memory
                WriteStepInformation(" " + Lang.T("Step 1: Enter password"), DialogText.CaptionsExport);
                Tools.CursorReturn();
                Tools.CursorDown(1);
                Console.Write(" " + DialogText.PasswordColon + " ");
                Encryptor.SetKey(Tools.ReadMaskedSimple(false));
                // Try loading, and retry if failing
                _sourceEntries1 = new List<Entry>();
                _load = new LoadStatus();
                _load = FileIo.LoadEntries();
                if (_load.Success)
                {
                    // Add the entries to the collection
                    for (int i = 0; i < Entries.GetEntries().Count; i++)
                    {
                        _sourceEntries1.Add(Entries.CloneEntry(Entries.GetEntries()[i], false));
                    }
                    // Return if zero
                    if (_sourceEntries1.Count == 0)
                    {
                        // Goto retry
                        WriteStepAlert(DialogText.FileNoEntries);
                        goto TryExportAgain1;
                    }
                    // Enter new file name for the exported file
                    // Check that the file name is valid and that the file
                    // does not exist. (test.xml <- add the extension automatically)
                    TryExportAgain2:
                    WriteStepInformation(" " + Lang.T("Step 2: Enter export file name"), DialogText.CaptionsExport);
                    Tools.CursorReturn();
                    Tools.CursorDown(1);
                    Console.Write(" " + DialogText.NameColon + " ");
                    // Enter file name (Esc: back to the choice of merge or export)
                    var typedName = Tools.ReadLineOrEscape(32);
                    if (typedName == null)
                    {
                        Tools.RemovePrivileges(false);
                        MergeExport();
                        return;
                    }

                    _selectedFileName = typedName;
                    // Check file name
                    if (Tools.IsValidFileName(_selectedFileName))
                    {
                        // Add file name extension
                        _selectedFileName = _selectedFileName + ".xml";
                        // Check if the file exists
                        if (!File.Exists(Path.Combine(FileIo.GetDataAndSubFolder(), _selectedFileName)))
                        {
                            // Export as XML
                            string stringXml = FileIo.GetEntriesAsXmlForExport(_sourceEntries1).ToString();

                            try
                            {
                                if (Directory.Exists(FileIo.GetDataAndSubFolder()))
                                {
                                    File.WriteAllText(Path.Combine(FileIo.GetDataAndSubFolder(), _selectedFileName), stringXml);
                                    // Exit routine
                                    Init.Log("Export (S): " + _sourceFileName1, false);
                                    Init.Log("Export (T): -> " + _selectedFileName, false);
                                    // The summary in a box on top of the page
                                    ChoiceBox.ShowRows(DialogText.ExportCompleted, new List<(string Label, string Value)>
                                    {
                                        (Lang.T("Target write:"), _sourceEntries1.Count.ToString()),
                                        (Lang.T("File name:"), _selectedFileName)
                                    }, 64);
                                    // Clear locals
                                    Tools.RemovePrivileges(false);
                                    MergeExport();
                                }
                                else
                                {
                                    WriteStepAlert(DialogText.StatusUnableToSave);
                                }
                            }
                            catch (Exception e) when (e is not NavigationException)
                            {
                                WriteStepAlert(e.Message);
                                Tools.RemovePrivileges(false);
                            }
                        }
                        else
                        {
                            WriteStepAlert(DialogText.InvalidFileName);
                            goto TryExportAgain2;
                        }
                    }
                    else
                    {
                        WriteStepAlert(DialogText.InvalidFileName);
                        goto TryExportAgain2;
                    }
                }
                else
                {
                    // Goto retry
                    WriteStepAlert(_load.StatusMessage);
                    goto TryExportAgain1;
                }
            }
            else if (_result == 0)
            {
                MergeExport();
            }
        }
        else
        {
            NoFiles(DialogText.CaptionsExport);
        }
    }


    private void WriteStepAlert(string text)
    {
        ChoiceBox.Message(DialogText.CaptionsMergeExport, text, string.Empty);
    }

    private void WriteStepInformation(string text, string modelabel)
    {
        Tools.ClearScreen();
        Menu.WriteModeLabel(modelabel, string.Empty, 0);
        Tools.CursorTopLeft();
        Tools.CursorDown(1);
        Console.WriteLine(text);
    }

    private void DoCleanUp()
    {
        _selectedFileName = DialogText.Cleared;
        _sourceFileName1 = DialogText.Cleared;
        _sourceFileName2 = DialogText.Cleared;
        _sourceEntries1.Clear();
        _sourceEntries2.Clear();
        _targetEntries.Clear();
    }

    #region File browser
    private void DisplaySelectedFile(int currentIndex, int totalCount)
    {
        Console.Write(new String(' ', Init.LineWidth - 2));
        Tools.CursorReturn();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(" <- " + (currentIndex + 1) + "/" + totalCount + " -> ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Console.Write(" " + Tools.TruncateString(_selectedFileName, 40, true) + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.CursorReturn();
    }


    private int PageFiles()
    {
        if (_filesList.Count > 0 && _fileIndex >= 0 && _fileIndex < _filesList.Count)
        {
            _selectedFileName = _filesList[_fileIndex];
            DisplaySelectedFile(_fileIndex, _filesList.Count);
            do
            {
                var c = Keyboard.ReadNavigationKey();
                if (c.Key == ConsoleKey.LeftArrow || (c.Key == ConsoleKey.Tab && c.Modifiers == ConsoleModifiers.Shift))
                {
                    // Previous file
                    if (_fileIndex > 0)
                    {
                        _fileIndex -= 1;
                    }
                    else
                    {
                        _fileIndex = _filesList.Count - 1;
                    }
                    _selectedFileName = _filesList[_fileIndex];
                    DisplaySelectedFile(_fileIndex, _filesList.Count);
                }
                else if (c.Key == ConsoleKey.RightArrow || c.Key == ConsoleKey.Tab)
                {
                    // Next file
                    if (_fileIndex + 1 < _filesList.Count)
                    {
                        _fileIndex += 1;
                    }
                    else
                    {
                        _fileIndex = 0;
                    }
                    _selectedFileName = _filesList[_fileIndex];
                    DisplaySelectedFile(_fileIndex, _filesList.Count);
                }
                else if (c.Key == ConsoleKey.Enter)
                {
                    return 1;
                }
                else if (c.Key == ConsoleKey.Escape)
                {
                    return 0;
                }
            } while (true);
        }
        else
        {
            _selectedFileName = DialogText.NoFiles;
            DisplaySelectedFile(0, 0);
            return 0;
        }
    }


    private void RefreshFilesList()
    {
        _filesList.Clear();
        try
        {
            if (FileIo.CheckCurrentDirectory())
            {
                _filesList.AddRange(Directory.EnumerateFiles(FileIo.GetDataAndSubFolder(), "*" + FileIo.GetFileNameExtension()));
                // Sort the list alphabetically
                if (_filesList.Count > 0)
                {
                    // Isolate file names and sort the list
                    for (int i = 0; i < _filesList.Count; i++)
                    {
                        _filesList[i] = Path.GetFileName(_filesList[i]);
                    }
                    _filesList.Sort(string.Compare);
                }
            }
        }
        catch
        {
            _filesList.Clear();
        }
    }
    #endregion

    // A paragraph wrapped to the width of the window
    private static void WriteParagraph(string text)
    {
        foreach (var line in TextMetrics.Wrap(text, Init.LineWidth - 2))
        {
            Console.WriteLine(" " + line);
        }
    }
}
