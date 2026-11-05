using System.Runtime.InteropServices;

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

    #region The key of a page

    // A key to be read next, handed back by a page that closed before it could use it (a peek closed by Esc, with
    // the key typed right after it, see TextArea.ReadKey)
    private static ConsoleKeyInfo? _pending;

    /// <summary>
    /// The key a page gets when it must be drawn again (the terminal window was resized, or Marbles goes on after
    /// Ctrl+Z and "fg", and the shell wrote on the screen). It does nothing on a page: the page is drawn again
    /// </summary>
    internal static readonly ConsoleKeyInfo RedrawKey = new('\0', ConsoleKey.F24, false, false, false);

    internal static bool IsRedraw(ConsoleKeyInfo key) => key.Key == ConsoleKey.F24 && key.KeyChar == '\0';

    /// <summary>
    /// Keep a key to be read next by the page that is shown now
    /// </summary>
    internal static void PushBack(ConsoleKeyInfo key) => _pending = key;

    /// <summary>
    /// The key handed back, if there is one (it is taken)
    /// </summary>
    internal static ConsoleKeyInfo? TakePending()
    {
        var pending = _pending;
        _pending = null;
        return pending;
    }

    /// <summary>
    /// The next key of a page: a key handed back first. While waiting, the size of the terminal window is looked
    /// at: when it has another size and has kept it for a moment (a window dragged larger passes many sizes),
    /// RedrawKey comes, and the page is drawn again in the dimensions that follow the window. RedrawKey also comes
    /// when Marbles goes on after Ctrl+Z (Linux, macOS)
    /// </summary>
    internal static ConsoleKeyInfo ReadKey()
    {
        if (TakePending() is { } pending) return pending;
        var shown = WindowSize((0, 0)); // The size of the window the page was drawn in
        var seen = shown; // A new size, and when it was seen first
        var seenAt = 0L;
        while (true)
        {
            if (Tools.TakeContinued()) return RedrawKey;
            try
            {
                if (Console.KeyAvailable) return Console.ReadKey(true);
            }
            catch (InvalidOperationException)
            {
                // Keys cannot be asked about: wait for one
                return Console.ReadKey(true);
            }

            var size = WindowSize(shown);
            if (size == shown)
            {
                seenAt = 0;
            }
            else if (seenAt == 0 || size != seen)
            {
                seen = size;
                seenAt = Environment.TickCount64;
            }
            else if (Environment.TickCount64 - seenAt >= SettleMilliseconds)
            {
                return RedrawKey;
            }

            WaitForInput();
        }
    }

    /// <summary>
    /// How long the window keeps its new size before a page or the editor follows it
    /// </summary>
    internal const int SettleMilliseconds = 150;

    // How long to wait between the questions for a key and the size of the window (on Windows: at most, as a key
    // ends the wait)
    private const int KeyWaitMilliseconds = 15;
    private const int WindowsWaitMilliseconds = 100;

    /// <summary>
    /// The size of the terminal window (the one given, if it is not known)
    /// </summary>
    internal static (int Width, int Height) WindowSize((int Width, int Height) unknown)
    {
        try
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }
        catch
        {
            return unknown;
        }
    }

    /// <summary>
    /// Wait until input may have come, or a while has passed. On Windows the input of the console is waited for (a
    /// key ends the wait at once), and the size is asked for some times a second; elsewhere there is a short pause
    /// (the questions take no time there: .NET keeps the size, and is told when it changes)
    /// </summary>
    internal static void WaitForInput()
    {
        if (OperatingSystem.IsWindows())
        {
            var input = GetStdHandle(StdInputHandle);
            if (input != IntPtr.Zero && input != new IntPtr(-1) &&
                WaitForSingleObject(input, WindowsWaitMilliseconds) != WaitFailed)
            {
                return;
            }
        }

        Thread.Sleep(KeyWaitMilliseconds);
    }

    private const int StdInputHandle = -10;
    private const uint WaitFailed = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

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
        _divider = new string('.', Math.Max(1, 20 - key.Length));
        _isHeadline = isHeadline;
    }

    public Shortcut(string key, string description, bool isHeadline, int width)
    {
        _key = key;
        _description = description;
        _divider = new string('.', Math.Max(1, width - key.Length));
        _isHeadline = isHeadline;
    }

    public bool IsHeadline => _isHeadline;

    /// <summary>
    /// The keys (or commands) of the row
    /// </summary>
    public string Key => _key;

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
        // In the colors of the quick guide: the headings in the color of headings, the keys in the accent color
        if (_isHeadline)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
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
            if (useMargin) Console.Write(new string(Margin, 4));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
            Console.Write(_key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(_divider);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // A long description (in another language) is cut at the edge of the window
            Console.Write(Tools.FitLine(" " + _description, Console.CursorLeft));
        }
        if (newLine)
        {
            Console.Write("\n");
        }
    }

}