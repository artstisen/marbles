using System.Text.RegularExpressions;

namespace Marbles;

internal static class Entries
{
    internal static Entry CurrentEntry = new();
    internal static Entry BackupEntry = new();
    internal static Entry DeletedEntry = new();
    private static List<Entry> _transferEntry = new();
    private static string _transferKey = string.Empty;
    internal static string RecallEntryId = string.Empty;
    private static List<Entry> _entryList = new();
    private static List<string> _sessionEntryIdList = new();
    

    internal const string ReturnCharString = "\r"; // The return character used for program logic (DO NOT CHANGE)
    internal const string NewLineCharString = "\n"; // The newline character used for content text (DO NOT CHANGE)

    internal const string
        ReturnNewLineCharString = "\r\n"; // The return character used for program logic (DO NOT CHANGE)

    internal const char NewLineChar = '\n'; // The newline character used for content text (DO NOT CHANGE)

    internal static bool HasEntries()
    {
        return _entryList.Count > 0;
    }

    // Options tags
    internal const char FavoritesTag = 'f';
    internal const char PinnedTag = 'p';
    internal const char ReadOnlyTag = 'r'; // The entry cannot be edited, renamed, reverted or deleted

    /// <summary>
    /// True if the entry is read only (a tag in its options, set with ":readonly" in the lists)
    /// </summary>
    internal static bool IsReadOnly(Entry entry) =>
        !string.IsNullOrEmpty(entry.Options) && entry.Options != DialogText.Cleared &&
        entry.Options.Contains(ReadOnlyTag);

    /// <summary>
    /// Make an entry read only, or not any more
    /// </summary>
    internal static void SetReadOnly(Entry entry, bool readOnly)
    {
        var options = new string((entry.Options ?? string.Empty).Where(c => c != ReadOnlyTag).ToArray());
        entry.Options = readOnly ? options + ReadOnlyTag : options;
    }
    internal const char DefaultRank = '0';
    private const string DefaultEndRank = "99999";

#region Ranking
    private static List<Entry> _rankedEntries = new();

    private static int _rankedCounter;

    /// <summary>
    /// Will set a sort ranking on pinned entries
    /// </summary>
    /// <param name="topMost">The entry to be sorted top most</param>
    internal static void SetRanking(Entry topMost)
    {
        if (_entryList.Count > 0)
        {
            // Collect pinned entries in a new list
            _rankedEntries.Clear();
            for (int i = 0; i < _entryList.Count; i++)
            {
                // Remove any old rankings and add the new ranking, except for the selected entry
                if (_entryList[i].Options.Contains(PinnedTag))
                {
                    _rankedEntries.Add(CloneEntry(_entryList[i], true));
                }
            }

            // Return if there was no or only one pinned entries collected
            if (_rankedEntries.Count <= 1) return;

            // Sort them according to current ranking to get the right order for the updated ranking
            _rankedEntries.Sort((x, y) => ExtractRank(x.Options).CompareTo(ExtractRank(y.Options)));

            // Set new ranking
            _rankedCounter = 1;
            for (int i = 0; i < _rankedEntries.Count; i++)
            {
                RemovePinnedRanking(_rankedEntries[i]);
                _rankedEntries[i].Options += _rankedCounter.ToString();
                _rankedCounter += 1;
            }

            // Add the options values back to the _entryList
            for (int i = 0; i < _entryList.Count; i++)
            {
                for (int j = 0; j < _rankedEntries.Count; j++)
                {
                    if (_rankedEntries[j].Id == _entryList[i].Id)
                    {
                        _entryList[i].Options = _rankedEntries[j].Options;
                    }
                }
            }

            // Set the topmost ranking for the selected entry
            // If the topmost is already at the top, move it to the bottom
            if (topMost.Id == _rankedEntries[0].Id)
            {
                RemovePinnedRanking(topMost);
                topMost.Options += DefaultEndRank;
            }
            else
            {
                RemovePinnedRanking(topMost);
                topMost.Options += DefaultRank;
            }
        }
    }

    private static string _rankPattern = @"\d+";

    /// <summary>
    /// Set the order of the pinned entries
    /// </summary>
    /// <param name="ids">The IDs of the pinned entries in the new order</param>
    internal static void SetPinnedOrder(List<string> ids)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            var entry = _entryList.Find(e => e.Id == ids[i]);
            if (entry == null) continue;
            RemovePinnedRanking(entry);
            entry.Options += (i + 1).ToString();
        }
    }

    internal static void RemovePinnedRanking(Entry e)
    {
        // If string matches the rank pattern (contains the numeric rank value)
        if (Regex.IsMatch(e.Options, _rankPattern))
        {
            e.Options = Regex.Replace(e.Options, _rankPattern, string.Empty, RegexOptions.None,
                TimeSpan.FromSeconds(0.5));
        }
    }

    /// Extract (return) the rank value from an options string
    internal static int ExtractRank(string input)
    {
        if (!string.IsNullOrEmpty(input) && Regex.IsMatch(input, _rankPattern))
        {
            try
            {
                return Convert.ToInt32(Regex.Match(input, _rankPattern).Value);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    // Cleanup when logging out
    internal static void ClearTempRankedEntries()
    {
        if (_rankedEntries.Count > 0)
        {
            _rankedEntries.Clear();
        }
    }

    #endregion

    
    #region Session IDs


    internal static void RegisterSessionId(string id)
    {
        if (Tools.IsValid(id) && !SessionIdExist(id))
        {
            _sessionEntryIdList.Add(id);
        }
    }

    private static bool SessionIdExist(string id)
    {
        for (int i = 0; i < _sessionEntryIdList.Count; i++)
        {
            if (_sessionEntryIdList[i] == id)
            {
                return true;
            }
        }
        return false;
    }

    // Remove ID if deleting entry
    private static void RemoveEntrySessionId(string id)
    {
        if (_sessionEntryIdList.Count > 0)
        {
            for (int i = 0; i < _sessionEntryIdList.Count; i++)
            {
                if (_sessionEntryIdList[i] == id)
                {
                    _sessionEntryIdList.RemoveAt(i);
                    return;
                }
            }
        }
    }
    
    // Clear list when ending session (logging out)
    internal static void ClearSessionEntryIdList()
    {
        if (_sessionEntryIdList.Count > 0)
        {
            _sessionEntryIdList.Clear();
        }
    }

    // Get session ID list
    internal static List<string> GetSessionIdList()
    {
        if (_sessionEntryIdList.Count > 0)
        {
            return _sessionEntryIdList;
        }

        return new List<string>();
    }
    
    internal static string GetSessionCountAsString()
    {
        return _entryList.Count > 0 ? "(" + _sessionEntryIdList.Count + ")" : "(0)";
    }

    #endregion
    
    
    internal static string GetFavoritesCount()
    {
        if (_entryList.Count > 0)
        {
            return "(" + _entryList.FindAll(e => e.Options.Contains(FavoritesTag)).Count + ")";
        }

        return "(0)";
    }

    internal static string GetEntriesCountAsString()
    {
        return _entryList.Count > 0 ? "(" + _entryList.Count + ")" : "(0)";
    }


    internal static int GetEntriesCount()
    {
        return _entryList.Count > 0 ? _entryList.Count : 0;
    }

    internal static List<Entry> GetEntries()
    {
        return _entryList;
    }


    internal static void ClearAllEntries(bool overwriteValues)
    {
        // The texts are strings, which .NET cannot overwrite: the entries are emptied so nothing that is kept
        // (an entry referred to elsewhere) still holds the notes, and the strings are let go
        if (_entryList.Count > 0)
        {
            if (overwriteValues)
            {
                for (int i = 0; i < _entryList.Count; i++)
                {
                    _entryList[i].Title = DialogText.Cleared;
                    _entryList[i].Text = DialogText.Cleared;
                    _entryList[i].CursorIndex = 0;
                    _entryList[i].Id = DialogText.Cleared;
                    _entryList[i].DateCreated = DialogText.Cleared;
                    _entryList[i].DateChanged = DialogText.Cleared;
                    _entryList[i].Options = DialogText.Cleared;
                }
            }
        }

        _entryList.Clear();
    }

    internal static void SetAllEntries(List<Entry> loadedEntries)
    {
        ClearAllEntries(true);
        foreach (var e in loadedEntries)
        {
            _newEntry = new Entry
            {
                Id = e.Id,
                DateCreated = e.DateCreated,
                DateChanged = e.DateChanged,
                Title = e.Title,
                Text = e.Text,
                CursorIndex = e.CursorIndex,
                Options = e.Options
            };

            _entryList.Add(_newEntry);
        }
    }

    // TEMP translate old options characters
    // Works by: Load data file and save it. Dont. load it again with this option turned on
    //private static bool _translateOldOptions = false; // <--- Set to true to translate

    private static Entry _newEntry = new();

    internal static void NewEntry(string title, string text)
    {
        _newEntry = new Entry(title, text);
        _entryList.Add(_newEntry);
        RecallEntryId = _newEntry.Id; // Save recall ID
        SetAsCurrentEntry(_newEntry.Id); // Set as current entry
    }

    internal static string DuplicateEntry(Entry originalEntry)
    {
        // Duplicate the entry
        _newEntry = new Entry(string.Empty, string.Empty)
        {
            Title = originalEntry.Title + DialogText.TitleCopyText,
            Text = originalEntry.Text,
            CursorIndex = originalEntry.CursorIndex,
            Options = originalEntry.Options
        };
        // A copy can be edited
        SetReadOnly(_newEntry, false);
        _entryList.Add(_newEntry);
        return _newEntry.Id;
    }

    internal static Entry CloneEntry(Entry e, bool cloneId)
    {
        _newEntry = new Entry()
        {
            Id = e.Id,
            DateCreated = e.DateCreated,
            DateChanged = e.DateChanged,
            Title = e.Title,
            Text = e.Text,
            CursorIndex = e.CursorIndex,
            Options = e.Options
        };

        // If not cloning ID create a new one
        if (!cloneId)
        {
            _newEntry.Id = new Entry(string.Empty, string.Empty).Id;
        }

        return _newEntry;
    }

    /// <summary>
    /// Add a copy of an entry
    /// </summary>
    /// <param name="e">The entry</param>
    /// <param name="newIdIfTaken">True to give the copy an ID of its own when an entry with its ID is there already
    /// (an entry transferred into the data file it came from, or twice): entries with the same ID would be changed
    /// and deleted together</param>
    public static void AddEntryToEntries(Entry e, bool newIdIfTaken = false)
    {
        var id = e.Id;
        while (newIdIfTaken && EntryExists(id)) id = new Entry(string.Empty, string.Empty).Id;
        _newEntry = new Entry(string.Empty, string.Empty)
        {
            Id = id,
            DateCreated = e.DateCreated,
            DateChanged = e.DateChanged,
            Title = e.Title,
            Text = e.Text,
            CursorIndex = e.CursorIndex,
            Options = e.Options
        };
        _entryList.Add(_newEntry);
    }

    private static Entry? GetEntryById(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length <= 0) return null;
        if (_entryList.Count <= 0) return null;
        foreach (var t in _entryList)
        {
            if (t.Id == id)
            {
                return t;
            }
        }

        return null;
    }

    public static bool SelectEntryById(string id)
    {
        if (EntryExists(id))
        {
            SetAsCurrentEntry(id);
            return true;
        }
        else
        {
            return false;
        }
    }

    public static bool EntryExists(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length <= 0) return false;
        if (_entryList.Count <= 0) return false;
        foreach (var t in _entryList)
        {
            if (t.Id == id)
            {
                return true;
            }
        }

        return false;
    }


    private static Entry? _tempEntry;

    private static void SetAsCurrentEntry(string id)
    {
        if (!Tools.IsValid(id) || !EntryExists(id)) return;
        _tempEntry = GetEntryById(id);
        if (_tempEntry == null) return;
        CurrentEntry = new Entry(string.Empty, string.Empty)
        {
            Id = _tempEntry.Id,
            DateCreated = _tempEntry.DateCreated,
            DateChanged = _tempEntry.DateChanged,
            Title = _tempEntry.Title,
            Text = _tempEntry.Text,
            CursorIndex = _tempEntry.CursorIndex,
            Options = _tempEntry.Options
        };
        RecallEntryId = CurrentEntry.Id;
    }

    internal static void SetAsCurrentEntry(Entry e)
    {
        _tempEntry = e;
        CurrentEntry = new Entry(string.Empty, string.Empty)
        {
            Id = _tempEntry.Id,
            DateCreated = _tempEntry.DateCreated,
            DateChanged = _tempEntry.DateChanged,
            Title = _tempEntry.Title,
            Text = _tempEntry.Text,
            CursorIndex = _tempEntry.CursorIndex,
            Options = _tempEntry.Options
        };
        RecallEntryId = CurrentEntry.Id;
    }


    private static bool IsCurrentEntry(Entry e)
    {
        return (!string.IsNullOrEmpty(CurrentEntry.Id) && e.Id == CurrentEntry.Id);
    }

    public static bool CurrentEntryExists()
    {
        return (EntryExists(CurrentEntry.Id));
    }


    private static bool _changesSubmitted;

    /// <summary>
    /// Submit entry values back to the entry collection
    /// </summary>
    /// <param name="changedEntry">The changed entry</param>
    /// <param name="setDateChanged">Specify if the changed date should be updated</param>
    /// <returns></returns>
    public static bool UpdateEntry(Entry changedEntry, bool setDateChanged)
    {
        _changesSubmitted = false;
        if (string.IsNullOrEmpty(changedEntry.Id) || !HasEntries()) return _changesSubmitted;
        for (int i = 0; i < _entryList.Count; i++)
        {
            if (_entryList[i].Id == changedEntry.Id)
            {
                _entryList[i].Title = changedEntry.Title;
                _entryList[i].Text = changedEntry.Text;
                _entryList[i].CursorIndex = changedEntry.CursorIndex;
                if (setDateChanged)
                {
                    _entryList[i].DateChanged = IsoDateTime.GetDateTimeNow(); // Manual formatted to force ISO date/Time
                }
                else
                {
                    _entryList[i].DateChanged = changedEntry.DateChanged;
                }

                _entryList[i].Options = changedEntry.Options;
                _changesSubmitted = true;
                // Refresh all values in current entry
                SetAsCurrentEntry(_entryList[i]);
            }
        }

        return _changesSubmitted;
    }


    private static bool _entryDeleted;

    public static bool DeleteEntry(Entry entry)
    {
        _entryDeleted = false;
        if (string.IsNullOrEmpty(entry.Id) || !HasEntries()) return _entryDeleted;
        for (int i = 0; i < _entryList.Count; i++)
        {
            if (_entryList[i].Id == entry.Id)
            {
                // Clear any relations
                if (entry.Id == RecallEntryId)
                {
                    ResetRecallId();
                }
                
                // Remove from session ID's
                RemoveEntrySessionId(entry.Id);
                
                // Clear current entry
                if (IsCurrentEntry(entry))
                {
                    ResetCurrentEntry();
                }

                // Remove the entry
                _entryList.RemoveAt(i);
                _entryDeleted = true;
            }
        }

        return _entryDeleted;
    }

    public static void ResetRecallId()
    {
        RecallEntryId = string.Empty;
    }

    public static void ResetCurrentEntry()
    {
        CurrentEntry.Id = string.Empty;
        CurrentEntry.DateCreated = string.Empty;
        CurrentEntry.DateChanged = string.Empty;
        CurrentEntry.Title = string.Empty;
        CurrentEntry.Text = string.Empty;
        CurrentEntry.CursorIndex = 0;
        CurrentEntry.Options = string.Empty;
    }

    public static void ResetBackupEntry()
    {
        BackupEntry.Id = string.Empty;
        BackupEntry.DateCreated = string.Empty;
        BackupEntry.DateChanged = string.Empty;
        BackupEntry.Title = string.Empty;
        BackupEntry.Text = string.Empty;
        BackupEntry.CursorIndex = 0;
        BackupEntry.Options = string.Empty;
    }

    public static void ResetDeletedEntry()
    {
        DeletedEntry.Id = string.Empty;
        DeletedEntry.DateCreated = string.Empty;
        DeletedEntry.DateChanged = string.Empty;
        DeletedEntry.Title = string.Empty;
        DeletedEntry.Text = string.Empty;
        DeletedEntry.CursorIndex = 0;
        DeletedEntry.Options = string.Empty;
    }



    /// <summary>
    /// Save the entries to the data file (and the backup), and tell about an error
    /// </summary>
    /// <returns>True if the data file was written (also when only the backup failed)</returns>
    internal static bool SaveEntriesToFile()
    {
        // "Saving..." on the bottom line: the page stays (a message can follow on top of it)
        Tools.HideCursor();
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(" " + DialogText.Saving);
        // Saved by another window since it was loaded here: its changes are only replaced when asked to
        if (FileIo.ChangedElsewhere() &&
            ChoiceBox.Pick(DialogText.Save, DialogText.ChangedInAnotherWindow, DialogText.ChangedInAnotherWindowNote,
                new[] { DialogText.SaveAnyway, DialogText.DoNotSave }, new[] { "S", "N" }, 1, true) != 0)
        {
            Tools.ClearScreen();
            return false;
        }

        var result = FileIo.SaveEntries();
        ShowSaveErrors(result);
        return result.Saved;
    }

    /// <summary>
    /// Tell that the data file or its backup could not be saved (nothing when all went well)
    /// </summary>
    internal static void ShowSaveErrors(FileIo.SaveResult result)
    {
        // Block flow on write errors
        if (!result.Saved)
        {
            Menu.StatusMessage(result.Error, true, true, true, DialogText.Save, string.Empty, false);
            Tools.ClearScreen();
        }
        else if (result.BackupError.Length > 0)
        {
            Menu.StatusMessage(result.BackupError, true, true, true, DialogText.CaptionsBackup, string.Empty, false);
            Tools.ClearScreen();
        }
    }

    #region Transfer entry

    // Transfer saved entry to current collection
    internal static void TransferEntry()
    {
        // If transfer exists ask with options
        if (IsValidTransfer())
        {
            if (CurrentEntryExists())
            {
                // Add, complete, discard or cancel
                var selection = ChoiceBox.Pick(DialogText.Transfer, DialogText.SelectTransferOption,
                    Lang.T("Adding: ") + Tools.TruncateString(CurrentEntry.Title, 60, true),
                    new[]
                    {
                        DialogText.TransferAdd, DialogText.TransferComplete + " " + GetTransferState(),
                        DialogText.TransferDiscard, DialogText.ChoiceCancel
                    },
                    new[] { "Enter", "A", "B", "Esc" }, 0, false);
                switch (selection)
                {
                    case 0:
                        // Collect entry
                        CreateTransfer();
                        break;
                    case 1:
                        // Ask for key and insert
                        InsertTransfer();
                        break;
                    case 2:
                        // Clear transfer
                        ClearTransfer(true);
                        break;
                    default:
                        // Cancel
                        return;
                }
            }
            else
            {
                // Complete, discard or cancel
                var selection = ChoiceBox.Pick(DialogText.Transfer, DialogText.SelectTransferOption, string.Empty,
                    new[]
                    {
                        DialogText.TransferComplete + " " + GetTransferState(), DialogText.TransferDiscard,
                        DialogText.ChoiceCancel
                    },
                    new[] { "A", "B", "Esc" }, 0, false);
                switch (selection)
                {
                    case 0:
                        // Ask for key and insert
                        InsertTransfer();
                        break;
                    case 1:
                        // Clear transfer
                        ClearTransfer(true);
                        break;
                    default:
                        // Cancel
                        return;
                }
            }
        }
        else
        {
            if (CurrentEntryExists())
            {
                if (ChoiceBox.Confirm(DialogText.Transfer, DialogText.CreateTransferQuestion,
                        Lang.T("This feature will store a protected collection of cloned entries in memory\n" +
                               "for transfer between data files within the current session. The original\n" +
                               "entries will remain untouched.").Replace("\n", " ") +
                        " " + Lang.T("Adding: ") + Tools.TruncateString(CurrentEntry.Title, 60, true), false))
                {
                    // Create transfer key
                    CreateTransfer();
                }
            }
            else
            {
                ChoiceBox.Message(DialogText.Transfer, Lang.T("Please select an entry to transfer..."), string.Empty);
            }
        }
    }

    private static void CreateTransfer()
    {
        if (CurrentEntryExists())
        {
            // Ask for transfer key to save entry for transfer
            if (!Tools.IsValid((_transferKey)))
            {
                var key = ChoiceBox.AskSecret(DialogText.Transfer,
                    Lang.T("Create a security key (password) to protect the transfer..."), DialogText.KeyColon);
                _transferKey = key ?? DialogText.CancelCommand;
                if (key == null)
                {
                    // Clear key
                    _transferKey = string.Empty;
                    TransferEntry();
                    return;
                }
            }

            // Validate key
            if (Tools.IsValid(_transferKey) && _transferKey.Length >= 4 && _transferKey.Length <= 16)
            {
                // Ensure not already added
                if (!TransferEntryExists(CurrentEntry))
                {
                    _transferEntry.Add(CloneEntry(CurrentEntry, true));
                    ChoiceBox.Message(DialogText.Transfer, Lang.T("Entry collected for transfer") + " " + GetTransferState(),
                        string.Empty);
                }
                else
                {
                    ChoiceBox.Message(DialogText.Transfer, Lang.T("Canceled: Entry already added"), string.Empty);
                }
            }
            else
            {
                ChoiceBox.Message(DialogText.Transfer, Lang.T("Transfer key must be between 4 and 16 characters long"),
                    string.Empty);
                _transferKey = string.Empty;
                TransferEntry();
            }
        }
    }

    private static bool TransferEntryExists(Entry e)
    {
        if (_transferEntry.Count > 0)
        {
            foreach (var te in _transferEntry)
            {
                if (te.Id == e.Id)
                    return true;
            }
        }

        return false;
    }

    private static void InsertTransfer()
    {
        if (IsValidTransfer())
        {
            // Ask for key and add transfer to current collection
            var input = ChoiceBox.AskSecret(DialogText.Transfer, Lang.T("Enter key to complete the transfer..."),
                DialogText.KeyColon);
            if (input == null)
            {
                // Just return without clearing key
                // because it is just a cancel of the transfer
                TransferEntry();
                return;
            }

            if (input == _transferKey)
            {
                foreach (var e in _transferEntry)
                {
                    AddEntryToEntries(e, true);
                }

                SaveEntriesToFile();
                ChoiceBox.Message(DialogText.Transfer, Lang.T("Transfer complete"), string.Empty);
            }
            else
            {
                // Invalid key...
                ChoiceBox.Message(DialogText.Transfer, Lang.T("Invalid key. Please try again..."), string.Empty);
                TransferEntry();
            }
        }
    }

    internal static void ClearTransfer(bool confirm)
    {
        if (confirm && !ChoiceBox.Confirm(DialogText.Transfer, DialogText.DiscardTransferQuestion, string.Empty,
                true))
        {
            return;
        }

        if (_transferEntry.Count > 0)
        {
            _transferEntry.Clear();
        }

        _transferKey = string.Empty;
    }

    private static bool IsValidTransfer()
    {
        return _transferEntry.Count > 0 && Tools.IsValid(_transferKey);
    }

    internal static string GetTransferState()
    {
        return "(" + _transferEntry.Count + ")";
    }

    #endregion
}

internal class Entry
{
    private static Random _rand = new();
    internal string Id { get; set; }
    internal string Title { get; set; }
    internal string Text { get; set; }
    internal int CursorIndex { get; set; }
    internal string DateCreated { get; set; }
    internal string DateChanged { get; set; }
    internal string Options { get; set; }

    /// <summary>
    /// Create a new entry initialized with ID and dates
    /// </summary>
    /// <param name="title"></param>
    /// <param name="text"></param>
    public Entry(string title, string text)
    {
        // The ID is a combination of ticks + a 4-numeric random value
        Id = DateTime.Now.Ticks.ToString() + _rand.Next(1000, 9999);
        Title = title;
        Text = text;
        CursorIndex = 0;
        DateCreated = IsoDateTime.GetDateTimeNow();
        DateChanged = DateCreated;
        Options = string.Empty;
    }

    /// <summary>
    /// Create a blank entry (used for new "empty" instances without ID or timestamps)
    /// </summary>
    public Entry()
    {
        Id = string.Empty;
        Title = string.Empty;
        Text = string.Empty;
        CursorIndex = 0;
        DateCreated = string.Empty;
        DateChanged = string.Empty;
        Options = string.Empty;
    }
}