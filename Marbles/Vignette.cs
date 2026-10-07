namespace marbles;

internal static class Vignette
{
    #region Vignette check/display
    private const byte VignetteHeight = 15;
    private const byte VignetteWidth = 42; // 42 will equal 40 in content char count
    private const string VignetteExText = "Your text here...";
    private const string VignetteExTitle = "VIGNETTE";
    private const string VignetteName = "[#vignette]";
    private const string VignetteText = VignetteExTitle + " " + Entries.NewLineCharString + Entries.NewLineCharString + VignetteExText;
    private static bool _vignetteEnabled = true;
    private static string _vignetteText = string.Empty;
                                    
    public static void DisplayVignette()
    {
        if (!_vignetteEnabled) return;
        // Write vignette text to output
        if (_vignetteText.Length <= 0) return;
        SaveCursorPosition();
        // Show in read only editor
        var dummy = new TextArea(Init.LineWidth - (VignetteWidth + 1), Init.LineCount - (VignetteHeight + 2), VignetteWidth, VignetteHeight, _vignetteText, 0, false, true, false, 0);
        LoadCursorPosition();
    }

    // Limit the text used in the vignette to the visible char count
    // to optimize memory and system performance
    private static string LimitVignetteText(string input)
    {
        const int limit = (VignetteWidth * VignetteHeight);
        if (!Tools.IsValid(input)) return string.Empty;
        return input.Length > limit ? input[..limit] : input;
    }

    // Call this when saving an entry and upon login
    public static void CheckVignette()
    {
        // Grab the vignette from the current entry
        if (Entries.CurrentEntryExists() && Tools.TrimToLower(Entries.CurrentEntry.Title).Contains(Tools.TrimToLower(VignetteName)))
        {
            _vignetteText = LimitVignetteText(Entries.CurrentEntry.Text);
            _vignetteEnabled = true;
        }
        // Else grab it from the entries collection
        else if (Entries.HasEntries())
        {
            _vignetteText = "";
            if (Entries.HasEntries())
            {
                int entriesCount = Entries.GetEntriesCount();
                // The default vignette will normally be the first entry in the datafile
                // hence making the search quick...
                for (int i = 0; i < entriesCount; i++)
                {
                    if (Tools.TrimToLower(Entries.GetEntries()[i].Title).Contains(Tools.TrimToLower(VignetteName)))
                    {
                        _vignetteText = LimitVignetteText(Entries.GetEntries()[i].Text);
                        _vignetteEnabled = true;
                        break;
                    }
                }
            }
        }
        else
        {
            _vignetteEnabled = false;
        }
    }
    #endregion

    #region Cursor position memory

    private static int _tempCursorMemoryTop;
    private static int _tempCursorMemoryLeft;
    private static void SaveCursorPosition()
    {
        _tempCursorMemoryTop = Console.CursorTop;
        _tempCursorMemoryLeft = Console.CursorLeft;
    }

    private static void LoadCursorPosition()
    {
        Tools.SetCursorEx(_tempCursorMemoryTop, _tempCursorMemoryLeft);
    }
    #endregion

    #region Default vignette creation
    private static bool CreateVignettes { get; set; }

    // Determines if vignettes are added when creating a new data file
    public static void SetVignetteSate(bool state)
    {
        CreateVignettes = state;
    }
    // Do not vignettes per default
    public static void SetDefaultVignetteState()
    {
        CreateVignettes = false;
    }

    public static string GetVignetteState()
    {
        return CreateVignettes ? DialogText.BTrue : DialogText.BFalse;
    }
    internal static void CreateVignette(bool forced)
    {
        if (!CreateVignettes && !forced) return;
        // Calendar related
        if (FileActions.CurrentFileType == FileActions.FileType.Calendar)
        {
            Entries.NewEntry("Calendar info " + VignetteName, Entries.NewLineCharString + "Press F to view calendar" +
                                                              Entries.NewLineCharString + new string('-', 24) +
                                                              Entries.NewLineCharString + "Press [" + Keyboard.CmpTrig() + "1-3] to change" +
                                                              Entries.NewLineCharString + "entry color...");
        }
        else
        {
            // Create standard vignette
            Entries.NewEntry(VignetteExTitle + " " + VignetteName, VignetteText); 
            // Set the entry to be pinned with highlight
            if (Entries.CurrentEntryExists())
            {
                Entries.CurrentEntry.Options = Entries.FavoritesTag.ToString();
                Entries.UpdateEntry(Entries.CurrentEntry, true);
            }
        }
        CheckVignette();
    }

    #endregion
}