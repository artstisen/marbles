using System.Text.RegularExpressions;

namespace marbles;

internal static class Keyboard
{
    #region Properties
    private static ConsoleKeyInfo _c;
    private const ConsoleKey DefaultPrimaryTrigger = ConsoleKey.W;
    private static ConsoleKey _primaryTrigger;
    private const string AwaitingKey = "Awaiting keystroke...";
    private static string _awaitingKeyNote = string.Empty;
    private const string UnboundKeyColon = "Unbound key: ";
    // Additional keys (default keys for CTRL+S (save) etc.)
    internal static ConsoleKey KbSave = ConsoleKey.E;
    internal static ConsoleKey KbSaveExit = ConsoleKey.Q;
    internal static ConsoleKey KbRepeat = ConsoleKey.K;
    internal static ConsoleKey KbNxtword = ConsoleKey.L;
    internal static ConsoleKey KbPreword = ConsoleKey.N;
    internal static ConsoleKey KbSelect = ConsoleKey.O;
    #endregion
    
    #region additional keys

    public static void ResetAdditionalKeys()
    {
        SetAdditionalKeyDefault(AdditionalKeys.SaveKey);
        SetAdditionalKeyDefault(AdditionalKeys.SaveExitKey);
        SetAdditionalKeyDefault(AdditionalKeys.RepeatKey);
        SetAdditionalKeyDefault(AdditionalKeys.NextWordKey);
        SetAdditionalKeyDefault(AdditionalKeys.PreviousWordKey);
        SetAdditionalKeyDefault(AdditionalKeys.SelectWordKey);
    }

    public enum AdditionalKeys
    {
        SaveKey,
        SaveExitKey,
        RepeatKey,
        NextWordKey,
        PreviousWordKey,
        SelectWordKey
    }

    public static void SetAdditionalKeyFromString(string s, AdditionalKeys key)
    {
        try
        {
            if (!string.IsNullOrEmpty(s) && Tools.IsAzChar(s) && IsValidTriggerKey(ConsoleKey.X, s))
            {
                // Try to convert the string character to ConsoleKey
                if (Enum.TryParse(s, out ConsoleKey ck))
                {
                    
                    switch (key)
                    {
                        case AdditionalKeys.SaveKey:
                            KbSave = ck;
                            break;
                        case AdditionalKeys.SaveExitKey:
                            KbSaveExit = ck;
                            break;
                        case AdditionalKeys.RepeatKey:
                            KbRepeat = ck;
                            break;
                        case AdditionalKeys.NextWordKey:
                            KbNxtword = ck;
                            break;
                        case AdditionalKeys.PreviousWordKey:
                            KbPreword = ck;
                            break;
                        case AdditionalKeys.SelectWordKey:
                            KbSelect = ck;
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    SetAdditionalKeyDefault(key);
                }
            }
            else
            {
                SetAdditionalKeyDefault(key);
            }
        }
        catch
        {
            SetAdditionalKeyDefault(key);
        }
    }

    public static void SetAdditionalKeyDefault(AdditionalKeys key)
    {
        switch (key)
        {
            case AdditionalKeys.SaveKey:
                KbSave = ConsoleKey.E;
                break;
            case AdditionalKeys.SaveExitKey:
                KbSaveExit = ConsoleKey.Q;
                break;
            case AdditionalKeys.RepeatKey:
                KbRepeat = ConsoleKey.K;
                break;
            case AdditionalKeys.NextWordKey:
                KbNxtword = ConsoleKey.L;
                break;
            case AdditionalKeys.PreviousWordKey:
                KbPreword = ConsoleKey.N;
                break;
            case AdditionalKeys.SelectWordKey:
                KbSelect = ConsoleKey.O;
                break;
            default:
                break;
        }
    }

    #endregion

    #region Second key trigger
    private static void SetPrimaryTriggerToDefault()
    {
        _primaryTrigger = DefaultPrimaryTrigger;
    }

    // Get compound trigger
    public static string CmpTrig()
    {
        return DialogText.CtrlPlus + _primaryTrigger + ", ";
    }

    public static ConsoleKey GetTriggerKey()
    {
        return _primaryTrigger;
    }

    public static string GetTriggerAsString()
    {
        return _primaryTrigger.ToString();
    }

    public static bool SetTriggerKey(ConsoleKey key)
    {
        if (IsValidTriggerKey(key, string.Empty))
        {
            _primaryTrigger = key;
            return true;
        }
        else
        {
            Menu.StatusMessage(DialogText.InvalidTriggerKey, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
            return false;
        }
    }

    public static void SetTriggerKeyFromString(string s)
    {
        try
        {
            if (!string.IsNullOrEmpty(s) && Tools.IsAzChar(s) && IsValidTriggerKey(ConsoleKey.X, s))
            {
                // Try to convert the string character to ConsoleKey
                if (Enum.TryParse(s, out ConsoleKey ck))
                {
                    _primaryTrigger = ck;
                }
                else
                {
                    SetPrimaryTriggerToDefault();
                }
            }
            else
            {
                SetPrimaryTriggerToDefault();
            }
        }
        catch
        {
            SetPrimaryTriggerToDefault();
        }
    }

    public static void ResetTriggerKey()
    {
        SetPrimaryTriggerToDefault();
    }

    // Determine if a trigger key is valid
    private static bool IsValidTriggerKey(ConsoleKey key, string keyString)
    {
        var k = !string.IsNullOrEmpty(keyString) ? keyString.ToUpper() : key.ToString().ToUpper();
        // Is latin character (case insensitive)
        return Regex.IsMatch(k, @"(?i)[a-z]");
        // Old code (because all keys kan be mapped the restriction is no longer necessary):
        // Reserved/invalid keys are: C, V, G, S, E, D, X, 1-0
        //return k is "A" or "B" or "F" or "H" or "I" or "J" or "K" or "L" or "M" or "N" or "O" or "P" or "Q" or "R" or "T" or "U" or "W" or "Y" or "Z";
    }



    public static bool IsPrimaryShortcutTrigger(ConsoleKeyInfo cKeyInfo, string note)
    {
        Tools.HideCursor(); // force hide to override the cursor visibility used in the text editor
        if ((ConsoleModifiers.Control & cKeyInfo.Modifiers) == ConsoleModifiers.Control && cKeyInfo.Key == _primaryTrigger)
        {
            _awaitingKeyNote = "";
            if (!string.IsNullOrEmpty(note))
            {
                _awaitingKeyNote = "(" + note + ")";
            }
            else
            {
                _awaitingKeyNote = string.Empty;
            }

            ClearTopIndicatorArea();
            // Write top indicator
            // "Awaiting second key"
            Tools.SaveCursorPosition(0);
            Tools.CursorTopLeft();
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(" " + AwaitingKey + " " + _awaitingKeyNote);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.LoadCursorPosition(0);
            return true;
        }
        return false;
    }

    public static void ClearTopIndicatorArea()
    {
        Tools.CursorTopLeft();
        Tools.SaveCursorPosition(3);
        Console.Write(new String(' ', (AwaitingKey.Length + _awaitingKeyNote.Length) + 5));
        Tools.LoadCursorPosition(3);
    }

    public static void DisplayUnboundKey(ConsoleKeyInfo cKeyInfo)
    {
        if (cKeyInfo.Key is ConsoleKey.Escape or ConsoleKey.Enter) return;
        Tools.CursorTopLeft();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(" " + UnboundKeyColon + cKeyInfo.KeyChar.ToString().ToUpper() + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(" ");
        AwaitAnyKey();
    }
    #endregion

    #region Await keypress
    public static void AwaitAnyKey()
    {
        Console.Write(DialogText.HitAnyKeyToContinue);
        _c = Console.ReadKey(true);
    }


    public static bool AwaitAnyKeyOrEscape()
    {
        Console.Write(DialogText.HitAnyKeyOrEscape);
        _c = Console.ReadKey(true);
        if (_c.Key == ConsoleKey.Escape)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static ConsoleKeyInfo AwaitAnyKeyCenterScreen()
    {
        Tools.WriteCenterScreen(DialogText.HitAnyKeyToContinue);
        _c = Console.ReadKey(true);
        return _c;
    }
    
    public static ConsoleKeyInfo AwaitAnyKeyWithInstructions(string[] text)
    {
        Tools.WriteCenterScreen(text);
        _c = Console.ReadKey(true);
        return _c;
    }
    #endregion
}

class Shortcut
{
    private string _key;
    private string _description;
    private string _divider;
    private bool _isHeadline;
    private const char Margin = ' ';
    public Shortcut(string key, string description, bool isHeadline)
    {
        _key = key;
        _description = description;
        _divider = new string('.', (20 - key.Length));
        _isHeadline = isHeadline;
    }

    public Shortcut(string key, string description, bool isHeadline, int width)
    {
        _key = key;
        _description = description;
        _divider = new string('.', (width - key.Length));
        _isHeadline = isHeadline;
    }

    public int GetLength()
    {
        return _key.Length + _description.Length + _divider.Length;
    }

    public string GetDescription()
    {
        return _description;
    }

    public void WriteShortcut(bool newLine, bool useMargin)
    {
        if (_isHeadline)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            if (useMargin)
            {
                Console.Write(new string(Margin, 1) + _description);
            }
            else
            {
                Console.Write(_description);
            }
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        else
        {
            if (useMargin)
            {
                Console.Write(new string(Margin, 4) + _key + " ");
            }
            else
            {
                Console.Write(_key + " ");
            }
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(_divider);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + _description);
        }
        if (newLine)
        {
            Console.Write("\n");
        }
    }

}