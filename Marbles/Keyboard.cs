namespace Marbles;

internal static class Keyboard
{
    private static ConsoleKeyInfo _c;

    #region Navigation keys

    /// <summary>
    /// Read a key where h, j, k and l work as the Left, Down, Up and Right arrow keys.
    /// Used where these letters have no other meaning (selecting from lists and changing values).
    /// </summary>
    public static ConsoleKeyInfo ReadNavigationKey()
    {
        return ToNavigationKey(Console.ReadKey(true));
    }

    public static ConsoleKeyInfo ToNavigationKey(ConsoleKeyInfo key)
    {
        var arrow = key.KeyChar switch
        {
            'h' => ConsoleKey.LeftArrow,
            'j' => ConsoleKey.DownArrow,
            'k' => ConsoleKey.UpArrow,
            'l' => ConsoleKey.RightArrow,
            _ => ConsoleKey.None
        };
        return arrow == ConsoleKey.None ? key : new ConsoleKeyInfo('\0', arrow, false, false, false);
    }

    /// <summary>
    /// True if the key is CTRL + a letter (and not a character typed with AltGr, which is reported as CTRL+ALT)
    /// </summary>
    public static bool IsControl(ConsoleKeyInfo key, ConsoleKey letter)
    {
        return key.Key == letter && (key.Modifiers & ConsoleModifiers.Control) != 0 &&
               (key.Modifiers & ConsoleModifiers.Alt) == 0;
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

    public bool IsHeadline => _isHeadline;

    /// <summary>
    /// The key and description as one text (for searching)
    /// </summary>
    public string Text => _key + " " + _description;

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