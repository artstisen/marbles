namespace Marbles;

/// <summary>
/// The find-and-replace box of the editor (the command ":replace"): an alternative to writing a ":s" command.
/// The box is shown on top of the text. What was entered is remembered until Marbles is closed.
/// </summary>
internal static class ReplaceDialog
{
    internal enum Scope
    {
        WholeText,
        CurrentLine,
        SelectedLines
    }

    internal static string Find { get; private set; } = string.Empty;
    internal static string Replacement { get; private set; } = string.Empty;
    internal static bool MatchCase { get; private set; }
    internal static bool IsRegex { get; private set; }

    private const int RowFind = 0;
    private const int RowReplacement = 1;
    private const int RowMatchCase = 2;
    private const int RowRegex = 3;
    private const int RowScope = 4;
    private const int RowRun = 5;
    private const int RowCount = 6;

    private const int InnerWidth = 58; // Between the borders
    private const int LabelWidth = 15;
    private const int FieldWidth = InnerWidth - LabelWidth - 7;
    private const int MaxTextLength = 200;

    /// <summary>
    /// The width and height of the box
    /// </summary>
    internal const int Width = InnerWidth + 2;

    internal const int Height = RowCount + 4;

    private static int _top;
    private static int _left;
    private static Scope _scope;
    private static bool _hasSelection;

    /// <summary>
    /// Show the box and let the user fill it in
    /// </summary>
    /// <param name="top">The row of the box</param>
    /// <param name="left">The column of the box</param>
    /// <param name="hasSelection">True if lines were selected when the box was asked for</param>
    /// <param name="find">The text to find, if it is given by a selection in the text (else empty)</param>
    /// <param name="scope">Where to replace</param>
    /// <returns>True to replace, false if the box was closed</returns>
    internal static bool Show(int top, int left, bool hasSelection, string find, out Scope scope)
    {
        _top = top;
        _left = left;
        _hasSelection = hasSelection;
        _scope = hasSelection ? Scope.SelectedLines : Scope.WholeText;
        scope = _scope;
        Tools.HideCursor();
        DrawFrame();
        var selected = RowFind;
        // Start by writing what to find, or the replacement if a word was selected in the text
        var editNext = Find.Length == 0;
        if (find.Length > 0 && find.Length <= MaxTextLength)
        {
            Find = find;
            selected = RowReplacement;
            editNext = true;
        }
        while (true)
        {
            for (var row = 0; row < RowCount; row++)
            {
                DrawRow(row, row == selected);
            }

            if (editNext)
            {
                editNext = false;
                if (EditField(selected))
                {
                    // Continue with the next field, and then to "Replace"
                    editNext = selected == RowFind;
                    selected = selected == RowFind ? RowReplacement : RowRun;
                }

                continue;
            }

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return false;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow or ConsoleKey.Tab)
            {
                selected = (selected + 1) % RowCount;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + RowCount - 1) % RowCount;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow or
                     ConsoleKey.I)
            {
                switch (selected)
                {
                    case RowFind:
                    case RowReplacement:
                        editNext = true;
                        break;
                    case RowMatchCase:
                        MatchCase = !MatchCase;
                        break;
                    case RowRegex:
                        IsRegex = !IsRegex;
                        break;
                    case RowScope:
                        _scope = NextScope(_scope);
                        break;
                    default:
                        if (key.Key != ConsoleKey.Enter) break;
                        if (Find.Length == 0)
                        {
                            selected = RowFind;
                            editNext = true;
                            break;
                        }

                        scope = _scope;
                        return true;
                }
            }
        }
    }

    private static Scope NextScope(Scope scope)
    {
        return scope switch
        {
            Scope.WholeText => Scope.CurrentLine,
            Scope.CurrentLine when _hasSelection => Scope.SelectedLines,
            _ => Scope.WholeText
        };
    }

    private static void DrawFrame()
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_top, _left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', InnerWidth) + AsciiStyles.GetTopCorner());
        for (var row = 1; row < Height - 1; row++)
        {
            Tools.SetCursorEx(_top + row, _left);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', InnerWidth));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(_top + Height - 1, _left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', InnerWidth) + AsciiStyles.GetBottomCorner());
        Tools.SetCursorEx(_top + Height - 2, _left + 2);
        Console.Write(DialogText.ReplaceKeys);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(_top, _left + 2);
        Console.Write(" " + DialogText.ReplaceCaption + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static string FieldText(int row) => row == RowFind ? Find : Replacement;

    // The end of a text that is too long for the field is shown
    private static string Shown(string text)
    {
        return text.Length <= FieldWidth ? text.PadRight(FieldWidth) : text[^FieldWidth..];
    }

    private static void DrawRow(int row, bool selected)
    {
        Tools.SetCursorEx(_top + 1 + row, _left + 2);
        if (selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        var text = row switch
        {
            RowFind => DialogText.ReplaceFind.PadRight(LabelWidth) + "[" + Shown(Find) + "]",
            RowReplacement => DialogText.ReplaceWith.PadRight(LabelWidth) + "[" + Shown(Replacement) + "]",
            RowMatchCase => Check(MatchCase) + DialogText.ReplaceMatchCase,
            RowRegex => Check(IsRegex) + DialogText.ReplaceRegex,
            RowScope => DialogText.ReplaceWhere.PadRight(LabelWidth) + _scope switch
            {
                Scope.CurrentLine => DialogText.ReplaceCurrentLine,
                Scope.SelectedLines => DialogText.ReplaceSelectedLines,
                _ => DialogText.ReplaceWholeText
            },
            _ => DialogText.ReplaceRun
        };
        Console.Write(" " + text.PadRight(InnerWidth - 5));
    }

    private static string Check(bool isChecked) => isChecked ? "[x] " : "[ ] ";

    // Let the user write the text of a field. Enter accepts the text, Esc leaves it as it was
    private static bool EditField(int row)
    {
        var original = FieldText(row);
        var text = original;
        var column = _left + 2 + 3 + LabelWidth + 1;
        try
        {
            while (true)
            {
                Tools.SetCursorEx(_top + 1 + row, column);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
                Console.Write(Shown(text));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.SetCursorEx(_top + 1 + row, column + Math.Min(text.Length, FieldWidth - 1));
                Tools.ShowCursor();
                var key = Console.ReadKey(true);
                Tools.HideCursor();
                if (key.Key == ConsoleKey.Escape)
                {
                    text = original;
                    return false;
                }

                if (key.Key is ConsoleKey.Enter or ConsoleKey.Tab or ConsoleKey.DownArrow) return true;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (text.Length > 0) text = text[..^1];
                }
                else if (Keyboard.IsControl(key, ConsoleKey.U))
                {
                    text = string.Empty;
                }
                else if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0' && text.Length < MaxTextLength)
                {
                    text += key.KeyChar;
                }
            }
        }
        finally
        {
            if (row == RowFind)
            {
                Find = text;
            }
            else
            {
                Replacement = text;
            }
        }
    }
}
