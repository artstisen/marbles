namespace Marbles;

/// <summary>
/// Coloring of the text in the editor by what it is (Markdown, HTML, CSS, JavaScript, JSON): the command
/// ":syntax" and the styles of the lines to draw. The styles themselves are found by the highlighter classes
/// (see SyntaxHighlighter).
/// </summary>
internal sealed partial class EditorEngine
{
    private const int MaxCachedSyntaxLines = 5000;

    private SyntaxType _syntax;

    /// <summary>
    /// The kind of text the editor colors (None: no colors)
    /// </summary>
    internal SyntaxType Syntax
    {
        get => _syntax;
        set
        {
            if (value == _syntax) return;
            _syntax = value;
            // The states of the lines are those of the kind of text
            _knownStates = 1;
        }
    }

    /// <summary>
    /// The kind of text used when the colors are turned on without telling which
    /// </summary>
    internal SyntaxType SyntaxDefault { get; set; } = SyntaxType.Markdown;

    /// <summary>
    /// True if the text is colored as Markdown
    /// </summary>
    internal bool Markdown
    {
        get => Syntax == SyntaxType.Markdown;
        set => Syntax = value ? SyntaxType.Markdown : SyntaxType.None;
    }

    // The state each line starts in (see SyntaxHighlighter). The states are found when lines are drawn, from
    // the top and down, so only the first ones are known: when a line is changed, the states of the lines
    // after it are found again
    private readonly List<int> _lineStates = new() { 0 };
    private int _knownStates = 1; // The number of lines the state is known for (the first is always 0)
    private bool _watchingLines;

    // The styles and the state after it of the lines looked at so far (by the text and state of the line)
    private readonly Dictionary<(SyntaxType, string, int), (SyntaxStyle[]? Styles, int Next)> _syntaxCache = new();

    // The command ":syntax": turn the colors on or off, or color the text as one of the kinds
    // (":syntax html", ":syntax on", ":syntax off")
    private void SetSyntax(string argument)
    {
        if (SingleLine) return;
        switch (argument)
        {
            case "":
                Syntax = Syntax == SyntaxType.None ? SyntaxDefault : SyntaxType.None;
                break;
            case "on":
                Syntax = SyntaxDefault;
                break;
            case "off":
                Syntax = SyntaxType.None;
                break;
            default:
                var type = SyntaxHighlighter.ParseType(argument);
                if (type == SyntaxType.None)
                {
                    SetError(DialogText.SyntaxUnknown + argument);
                    return;
                }

                Syntax = type;
                SyntaxDefault = type;
                break;
        }

        Message = Syntax == SyntaxType.None
            ? DialogText.SyntaxOff
            : DialogText.SyntaxOn + SyntaxHighlighter.Name(Syntax);
        // The host remembers if files are to be colored
        _request = EditorRequest.SyntaxChanged;
    }

    private (SyntaxStyle[]? Styles, int Next) HighlightLine(int line)
    {
        var key = (_syntax, Buffer[line], _lineStates[line]);
        if (_syntaxCache.TryGetValue(key, out var result)) return result;
        if (_syntaxCache.Count >= MaxCachedSyntaxLines) _syntaxCache.Clear();
        var styles = SyntaxHighlighter.Highlight(key.Item1, key.Item2, key.Item3, out var next);
        result = (styles, next);
        _syntaxCache[key] = result;
        return result;
    }

    // Lines were changed: the states from the first of them are found again
    private void ForgetLineStates(int first, int removed, int inserted)
    {
        _knownStates = Math.Min(_knownStates, first + 1);
    }

    /// <summary>
    /// The style of each character of a line (null if the line has none, or the colors are off)
    /// </summary>
    internal SyntaxStyle[]? SyntaxStyles(int line)
    {
        if (_syntax == SyntaxType.None || line >= Buffer.LineCount) return null;
        if (!_watchingLines)
        {
            // When lines are changed, the states from the first of them are found again
            _watchingLines = true;
            Buffer.LinesReplaced += ForgetLineStates;
            _knownStates = 1;
        }

        while (_lineStates.Count < Buffer.LineCount) _lineStates.Add(0);
        // Find the states down to the line: each line gives the state of the next
        while (_knownStates <= line)
        {
            _lineStates[_knownStates] = HighlightLine(_knownStates - 1).Next;
            _knownStates++;
        }

        return HighlightLine(line).Styles;
    }
}
