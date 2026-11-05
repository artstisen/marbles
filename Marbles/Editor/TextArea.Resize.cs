namespace Marbles;

/// <summary>
/// The editor follows the size of the terminal window while it waits for a key: when the size has stayed the same
/// for a moment (a window dragged larger passes many sizes), the editor takes the dimensions now in use (they follow
/// the window with automatic size, else the user is asked when the window is too small) and is drawn again. Keys
/// that are waiting (typing, pasting) are read at once. The wait between the questions is that of the pages
/// (Keyboard.WaitForInput), which follow the window the same way. Ctrl+C (cancelled in Init) is told about here: it
/// does not close the editor. Only the editor of a note, of a file or of the settings follows the window: a peek and
/// a field of a box are drawn again by the page they are on
/// </summary>
public partial class TextArea
{
    // The dimensions the editor is laid out for (Init.LineWidth, Init.LineCount), and the size of the window then
    private int _lineWidth;
    private int _lineCount;
    private (int Width, int Height) _windowSize;

    // The editor of a note, of a file or of the settings, on the whole screen
    private bool FollowsWindow => _peek == null && !_singleLine && !_readOnly;

    // When the editor opens: the dimensions it is laid out for (a Ctrl+C from before is forgotten). With automatic
    // size, the window the dimensions were taken from: a window resized before the editor opened (while Marbles
    // started) is followed too
    private void StartFollowingWindow()
    {
        _lineWidth = Init.LineWidth;
        _lineCount = Init.LineCount;
        _windowSize = Init.AutoSize
            ? (Init.LineWidth + 1, Init.LineCount + 1)
            : Keyboard.WindowSize((Init.LineWidth + 1, Init.LineCount + 1));
        Tools.TakeCancelKey();
    }

    // The size of the terminal window (the last one seen, if it is not known)
    private (int Width, int Height) WindowSize() => Keyboard.WindowSize(_windowSize);

    // The key after Esc, when the two came at once (see ReadKey)
    private ConsoleKeyInfo? _keyAfterEscape;

    // The next key. On Linux and macOS a key typed right after Esc (Esc j, fast, or while the screen is drawn)
    // comes from the terminal as one key with Alt: it is Esc and then the key, as in Vim (AltGr never sets Alt
    // there, so no character typed with AltGr is split)
    private ConsoleKeyInfo ReadKey()
    {
        if (_keyAfterEscape is { } after)
        {
            _keyAfterEscape = null;
            return after;
        }

        // A key handed back by a page that closed before it could use it
        if (Keyboard.TakePending() is { } pending) return pending;

        var key = ReadTerminalKey();
        if (OperatingSystem.IsWindows() || !IsEscapeAndKey(key)) return key;
        _keyAfterEscape = new ConsoleKeyInfo(key.KeyChar, key.Key, (key.Modifiers & ConsoleModifiers.Shift) != 0,
            false, false);
        return new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false);
    }

    /// <summary>
    /// True for a key with Alt (and not Ctrl) that types a character: on Linux and macOS, Esc and that key
    /// </summary>
    internal static bool IsEscapeAndKey(ConsoleKeyInfo key) =>
        (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == ConsoleModifiers.Alt &&
        key.KeyChar != '\0' && !char.IsControl(key.KeyChar);

    // The next key from the terminal. Meanwhile the editor follows the size of the window, and tells how to close
    // it after Ctrl+C
    private ConsoleKeyInfo ReadTerminalKey()
    {
        if (!FollowsWindow) return Console.ReadKey(true);
        var seen = _windowSize; // The new size, and when it was seen first
        var seenAt = 0L;
        while (true)
        {
            try
            {
                if (Console.KeyAvailable) break;
            }
            catch
            {
                // Keys cannot be asked about (input from a file): wait for one
                break;
            }

            if (Tools.TakeCancelKey())
            {
                _engine.SetMessage(DialogText.CancelKeyHint, false);
                Draw();
            }

            // Back after Ctrl+Z: the shell wrote on the screen
            if (Tools.TakeContinued()) RedrawAll();

            var size = WindowSize();
            if (size == _windowSize)
            {
                seenAt = 0;
            }
            else if (seenAt == 0 || size != seen)
            {
                seen = size;
                seenAt = Environment.TickCount64;
            }
            else if (Environment.TickCount64 - seenAt >= Keyboard.SettleMilliseconds)
            {
                seenAt = 0;
                FitToWindow();
            }

            Keyboard.WaitForInput();
        }

        return Console.ReadKey(true);
    }

    // Ctrl+C read as a key (not as the signal)
    private static bool IsCancelKey(ConsoleKeyInfo key) =>
        key.KeyChar == '\u0003' ||
        (key.Key == ConsoleKey.C && (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == ConsoleModifiers.Control);

    // The window was resized (or the text could not be drawn in it): the editor takes the dimensions now in use,
    // and is drawn again, also when they stay the same (the terminal may have moved or cleared what was shown)
    private void FitToWindow()
    {
        _windowSize = WindowSize();
        if (Init.AutoSize) Init.ApplyAutoSize();
        else if (Tools.IsWindowTooSmall()) Tools.WindowTooSmall();
        var widthChange = Init.LineWidth - _lineWidth;
        var heightChange = Init.LineCount - _lineCount;
        _lineWidth = Init.LineWidth;
        _lineCount = Init.LineCount;
        if (widthChange != 0 || heightChange != 0) Resize(widthChange, heightChange);
        RedrawAll();
        // (What could not be drawn is drawn now)
        Tools.TakeRedrawRequest();
    }

    // The text area is made wider or narrower, and higher or lower. The text is wrapped in the new width (the line
    // numbers are found again), and the cursor stays in view. A split keeps its panes on their sides, each with
    // half the width as when it was split
    private void Resize(int widthChange, int heightChange)
    {
        _height = Math.Max(1, _height + heightChange);
        _shownRows = new string?[_height];
        _shownCells = new int[_height];
        _engine.SetHeight(_height);
        if (_otherPane == null)
        {
            SetPlace(_left, Math.Max(2, _width + widthChange));
            return;
        }

        _wholeWidth = Math.Max(5, _wholeWidth + widthChange);
        var leftWidth = (_wholeWidth - 1) / 2;
        var rightWidth = _wholeWidth - leftWidth - 1;
        var rightLeft = _wholeLeft + leftWidth + 1;
        var focusAtLeft = _left < _otherPane.Left;
        SetPlace(focusAtLeft ? _wholeLeft : rightLeft, focusAtLeft ? leftWidth : rightWidth);
        _otherPane.Left = focusAtLeft ? rightLeft : _wholeLeft;
        _otherPane.Width = focusAtLeft ? rightWidth : leftWidth;
        _otherPane.TextWidth = _otherPane.Width - 1;
        _otherPane.Gutter = -1;
        _otherPane.ShownRows = new string?[_height];
        _otherPane.ShownCells = new int[_height];
        _otherPane.Engine.SetHeight(_height);
    }
}
