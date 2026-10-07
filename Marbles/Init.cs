using System.Text;

namespace marbles;

internal static class Init
{
    #region Output dimensions
    // Min/Max line values
    // Standard line count (row count)
    internal const byte MinLineCount = 23; // Default
    private const byte MaxLineCount = 255; // Default
    // Standard line width (column count)
    internal const byte MinLineWidth = 80; // Default
    private const byte MaxLineWidth = 255; // Default
    internal static byte LineWidth { get; private set; }
    internal static byte LineCount { get; private set; }

    // Set "artificial buffer size" according to general guidelines (80x23)
    // -the actual console buffer is not touched but must be set manually by the user (cross platform compat)
    // -this buffer is used to calculate the output width and height across the entire application.
    internal static void SetDefaultOutputDimensions()
    {
        LineWidth = MinLineWidth;
        LineCount = MinLineCount;
    }

  
    internal static void SetLineWidth(byte width)
    {
        LineWidth = width is >= MinLineWidth and <= MaxLineWidth ? width : MinLineWidth;

        // if (width >= DefaultLineWidth && width <= LineWidthPreset4)
        // {
        //     LineWidth = width;
        // }
        // else
        // {
        //     LineWidth = DefaultLineWidth;
        // }
    }

    internal static void SetLineCount(byte count)
    {
        LineCount = count is >= MinLineCount and <= MaxLineCount ? count : MinLineCount;
        // if (count >= DefaultLineCount && count <= LineCountPreset5)
        // {
        //     LineCount = count;
        // }
        // else
        // {
        //     LineCount = DefaultLineCount;
        // }
    }

    internal static void IncreaseLineCount()
    {
        if (LineCount + 1 <= MaxLineCount)
        {
            LineCount += 1;
        }
    }

    internal static void DecreaseLineCount()
    {
        if (LineCount - 1 >= MinLineCount)
        {
            LineCount -= 1;
        }
    }
    
    internal static void IncreaseLineWidth()
    {
        if (LineWidth + 1 <= MaxLineWidth)
        {
            LineWidth += 1;
        }
    }

    internal static void DecreaseLineWidth()
    {
        if (LineWidth - 1 >= MinLineWidth)
        {
            LineWidth -= 1;
        }
    }

    // internal static void SetOutputLineWidthFromPreset(int presetIndex)
    // {
    //     switch (presetIndex)
    //     {
    //         // Set line width
    //         case >= 1 and <= 5:
    //             switch (presetIndex)
    //             {
    //                 case 1:
    //                     SetLineWidth(DefaultLineWidth);
    //                     break;
    //                 case 2:
    //                     SetLineWidth(LineWidthPreset1);
    //                     break;
    //                 case 3:
    //                     SetLineWidth(LineWidthPreset2);
    //                     break;
    //                 case 4:
    //                     SetLineWidth(LineWidthPreset3);
    //                     break;
    //                 case 5:
    //                     SetLineWidth(LineWidthPreset4);
    //                     break;
    //             }
    //
    //             break;
    //         // Reset dimensions
    //         case 0:
    //             SetDefaultOutputDimensions();
    //             break;
    //     }
    // }

    // internal static void SetOutputLineCountFromPreset(int presetIndex)
    // {
    //     switch (presetIndex)
    //     {
    //         // Set line count
    //         case >= 1 and <= 6:
    //             switch (presetIndex)
    //             {
    //                 case 1:
    //                     SetLineCount(DefaultLineCount);
    //                     break;
    //                 case 2:
    //                     SetLineCount(LineCountPreset1);
    //                     break;
    //                 case 3:
    //                     SetLineCount(LineCountPreset2);
    //                     break;
    //                 case 4:
    //                     SetLineCount(LineCountPreset3);
    //                     break;
    //                 case 5:
    //                     SetLineCount(LineCountPreset4);
    //                     break;
    //                 case 6:
    //                     SetLineCount(LineCountPreset5);
    //                     break;
    //             }
    //
    //             break;
    //         // Reset dimensions
    //         case 0:
    //             SetDefaultOutputDimensions();
    //             break;
    //     }
    // }


    private const char TempOutputCornerChar = '+';

    internal static bool TestOutputDimensions()
    {
        try
        {
            Console.Clear();
            // Top left corner
            Tools.CursorTopLeft();
            Console.Write(TempOutputCornerChar);

            // Top right corner
            Tools.CursorTopLeft();
            Tools.CursorRight(LineWidth - 1);
            Console.Write(TempOutputCornerChar);

            // Bottom left corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Console.Write(TempOutputCornerChar);

            // Bottom right corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Tools.CursorRight(LineWidth - 1);
            Console.Write(TempOutputCornerChar);

            return true;
        }
        catch
        {
            SetDefaultOutputDimensions();  // Must be set first to display the message correctly
            Menu.StatusMessage(DialogText.ErrorBufferSize, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
            FileIo.SaveSettings();
            return false;
        }
    }


    internal static void DisplayOutputDimensions()
    {
        try
        {
            Console.Clear();
            // Top left corner
            Tools.CursorTopLeft();
            Console.Write(TempOutputCornerChar + "--");
            Tools.CursorReturn();
            Tools.CursorDown();
            Console.Write("|");

            // Top right corner
            Tools.CursorTopLeft();
            Tools.CursorRight(LineWidth - 3);
            Console.Write("--" + TempOutputCornerChar);
            Tools.CursorReturn();
            Tools.CursorDown();
            Tools.CursorRight(LineWidth - 1);
            Console.Write("|");

            // Bottom left corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Console.Write(TempOutputCornerChar + "--");
            Tools.CursorReturn();
            Tools.CursorUp(1);
            Console.Write("|");

            // Bottom right corner
            Tools.CursorTopLeft();
            Tools.CursorDown(LineCount - 1);
            Tools.CursorRight(LineWidth - 3);
            Console.Write("--" + TempOutputCornerChar);
            Tools.CursorReturn();
            Tools.CursorRight(LineWidth - 1);
            Tools.CursorUp(1);
            Console.Write("|");

            // Display center message
            Tools.CursorTopLeft();
            Tools.CursorDown(2);
            Tools.CursorRight(3);
            Console.Write(LineWidth + "x" + LineCount);
        }
        catch
        {
            DimensionsExceptionCaught = true;
            SetDefaultOutputDimensions(); // Must be set first to display the message correctly
            Menu.StatusMessage(DialogText.ErrorBufferSize, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
            FileIo.SaveSettings();
        }
    }

    internal static string GetOutputDimensions()
    {
        return "(" + LineWidth + "x" + LineCount + ")";
    }

    #endregion


    #region Console init sequence


    internal static bool DimensionsExceptionCaught { get; set; }
    internal static bool DataDirectoryCreated { get; set; }
    internal static bool UsingDefaultSettings { get; set; }
    private static List<string> _initLog = new();
    public static void RunInitialization()
    {
        // Hide the cursor
        Tools.HideCursor();
        DimensionsExceptionCaught = false;
        DataDirectoryCreated = false;
        UsingDefaultSettings = false;
        Console.Clear();
        Console.WriteLine();
        WriteProgressBar(1); // progress
        Log(DialogText.ApplicationName + " " + DialogText.ApplicationVersion, true);
        WriteProgressBar(1); // progress
        Log(DialogText.InitInitializing, true);
        WriteProgressBar(2); // progress
        Keyboard.ResetTriggerKey();
        WriteProgressBar(3); // progress
        Mode.SetMode(Mode.SelectedMode.Login); // Mode decides everything, if logged in/authorized, if searching etc.
        FileActions.CurrentFileType = FileActions.FileType.Normal; // Set file type
        WriteProgressBar(4); // progress
        // Set title
        Console.Title = DialogText.ApplicationName;
        WriteProgressBar(5); // progress
        // Set default file name
        FileIo.SetDefaultFileName();
        WriteProgressBar(6); // progress
        Log(DialogText.InitSetEncoding, true);
        WriteProgressBar(7); // progress
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Log(DialogText.InitSetDefaults, true);
        Entries.ResetRecallId();
        AsciiStyles.ResetAsciiStyle();
        Vignette.SetDefaultVignetteState();
        Logo.ResetLogoIndex();
        Logo.ResetLogoText();
        TextArea.InitCommandMemoryIndex();
        WriteProgressBar(8); // progress
        // Set default colors
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
        WriteProgressBar(9); // progress
        //Console.TreatControlCAsInput = true; // Prevent console from exiting when pressing CTRL+C // DOES AFFECT COPY/PASTE HENCE COMMENTED OUT
        // Set default buffer-size
        Log(DialogText.InitDimensions, true);
        WriteProgressBar(10); // progress
        SetDefaultOutputDimensions();
        WriteProgressBar(11); // progress
        // Check default data folder path (writing permissions etc.)
        Log(DialogText.InitDirectoryCheck, true);
        WriteProgressBar(12); // progress
        if (!FileIo.CheckDataDirectory())
        {
            Log(DialogText.InitDirectoryCheckError + FileIo.DirectoryCheckErrorMessage, true);
            Keyboard.AwaitAnyKey();
        }
        else
        {
            if (DataDirectoryCreated)
            {
                Log(DialogText.InitOk + " -" + DialogText.DirectoryCreated, true);
            }
            else
            {
                Log(DialogText.InitOk, true);
            }
        }
        WriteProgressBar(13); // progress
        Log(DialogText.InitLoadingSettings, true);
        WriteProgressBar(14); // progress
        // Load settings
        if (!FileIo.LoadSettings())
        {
            //Log(FileIo.DirectoryCheckErrorMessage + ", " + FileIo.LoadSettingsErrorMessage);
            Log(DialogText.InitFailed, true);
            Log(DialogText.InitLoadingSettingsError, true);
            Log("(reverting to default)", true);
            FileIo.SaveSettings(); // Save a fresh settings file
            Keyboard.AwaitAnyKey();
        }
        else
        {
            if (UsingDefaultSettings)
            {
                Log(DialogText.InitOk + " (" + DialogText.DefaultSettings + ")", true);
                FileIo.SaveSettings(); // Save a fresh settings file
            }
            else
            {
                Log(DialogText.InitOk, true);
            }
        }
        FileBrowser.ResolveSubFolderIndex(); // Update subfolder index for selection
        WriteProgressBar(15); // progress
        // Update output dimensions if set by user
        Log(DialogText.InitUpdateDimensions, true);
        WriteProgressBar(16); // progress
        Log("Target: " + LineWidth + "x" + LineCount, true);
        WriteProgressBar(17); // progress
        if (TestOutputDimensions() && !DimensionsExceptionCaught)
        {
            Log(DialogText.InitOk, true);
        }
        else
        {
            Log(DialogText.InitFailed, true);
        }
        WriteAllInitOutput();
        WriteProgressBar(18); // progress
        Log(DialogText.InitCompleted, true);
    }

    private static void InitializeLog()
    {
        // Truncate log if more than 500 lines
        if (_initLog.Count <= 500) return;
        // Remove the first item
        _initLog.RemoveAt(0);
        // Indicate that the log was truncated
        _initLog[0] = "(Truncated)";
    }

    internal static void Log(string text, bool writeOutput)
    {
        InitializeLog();
        _initLog.Add( Tools.TruncateString(LogTs() + text, 75, true));
        if (writeOutput)
        {
            Console.WriteLine(Tools.TruncateString(text, 75, true));
        }
    }
    private static string LogTs()
    {
        return "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
    }
    

    private static void WriteAllInitOutput()
    {
        if (_initLog.Count > 0)
        {
            Tools.ClearScreen();
            for (int i = 0; i < _initLog.Count; i++)
            {
                Console.WriteLine(_initLog[i]);
            }
        }
        else
        {
            Tools.ClearScreen();
            Console.WriteLine(DialogText.InitNoData);
            Keyboard.AwaitAnyKey();
        }
    }


    private static List<List<string>> _logPages = new();
    internal static void WriteLog()
    {
        if (_initLog.Count > 0)
        {
            // Split log into pages to fit output line count
            _logPages = Tools.SplitIntoChunks(_initLog, (LineCount - 3));

            for (int i = 0; i < _logPages.Count; i++)
            {
                Tools.ClearScreen();
                Menu.WriteModeLabel(DialogText.LogReview, string.Empty, 0);
                Tools.CursorDown(2);
                for (int j = 0; j < _logPages[i].Count; j++)
                {
                    if (_logPages[i][j].Contains(DialogText.ApplicationName + " " + DialogText.ApplicationVersion)) // DialogText.ApplicationName + " " + DialogText.ApplicationVersion
                    {
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                    }

                    Console.WriteLine(_logPages[i][j]);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }
                Tools.CursorTopLeft();
                Tools.CursorDown(LineCount - 1);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(" Page " + (i + 1) + " of " + _logPages.Count + " ");
                if (_logPages.Count == 1 || (i + 1) == _logPages.Count)
                {
                    Keyboard.AwaitAnyKey();
                }
                else if (Keyboard.AwaitAnyKeyOrEscape())
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Tools.ClearScreen();
                    break;
                }
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
            }
        }
        else
        {
            Tools.ClearScreen();
            Console.WriteLine(DialogText.InitNoData);
            Keyboard.AwaitAnyKey();
        }
    }

    private static void WriteProgressBar(int value)
    {
        Tools.SaveCursorPosition(0);
        Tools.CursorTopLeft();
        Console.Write(Tools.GetProgressBar(value, 18));
        Tools.LoadCursorPosition(0);
    }

    #endregion
}