namespace Marbles;

/// <summary>
/// Finds the parts of a line of CSS to color: comments, selectors, the names of properties, strings, numbers
/// (with units, and colors like #fff) and the words that start with "@" or "!".
/// What is a selector and what is a property is told by what follows it on the line: "{" after a selector,
/// ";" or "}" after a property and its value.
/// </summary>
internal static class CssHighlighter
{
    // The state a line starts in: inside a comment (the lowest bit), and how many "{" are open (the rest)
    private const int InComment = 1;
    private const int MaxDepth = 100;

    /// <summary>
    /// Find the styles of a part of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="start">The first character to look at</param>
    /// <param name="end">The character after the last</param>
    /// <param name="state">The state the part starts in (0 at the start of a style sheet)</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    /// <returns>The state after the part</returns>
    internal static int Scan(string line, int start, int end, int state, SyntaxStyle[] styles)
    {
        var inComment = (state & InComment) != 0;
        var depth = state >> 1;
        var i = start;
        while (i < end)
        {
            if (inComment)
            {
                var close = line.IndexOf("*/", i, end - i, StringComparison.Ordinal);
                var stop = close < 0 ? end : close + 2;
                JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Comment);
                inComment = close < 0;
                i = stop;
                continue;
            }

            var c = line[i];
            if (c == '/' && i + 1 < end && line[i + 1] == '*')
            {
                JavaScriptHighlighter.Fill(styles, i, i + 2, SyntaxStyle.Comment);
                inComment = true;
                i += 2;
            }
            else if (c == '{')
            {
                if (depth < MaxDepth) depth++;
                i++;
            }
            else if (c == '}')
            {
                if (depth > 0) depth--;
                i++;
            }
            else if (c == ';' || char.IsWhiteSpace(c))
            {
                i++;
            }
            else
            {
                // A selector, or a property with its value: to the next "{", ";" or "}"
                var stop = StatementEnd(line, i, end);
                var terminator = stop < end ? line[stop] : '\0';
                var colon = line.IndexOf(':', i, stop - i);
                if (c == '@')
                {
                    // @media, @import ...
                    var word = i + 1;
                    while (word < stop && (char.IsLetterOrDigit(line[word]) || line[word] == '-')) word++;
                    JavaScriptHighlighter.Fill(styles, i, word, SyntaxStyle.Keyword);
                    ScanValue(line, word, stop, styles);
                }
                else if (terminator == '{' || (terminator != ';' && terminator != '}' && (depth == 0 || colon < 0)))
                {
                    // Followed by "{", or the start of one that continues on the next line
                    var last = stop;
                    while (last > i && char.IsWhiteSpace(line[last - 1])) last--;
                    JavaScriptHighlighter.Fill(styles, i, last, SyntaxStyle.Keyword);
                }
                else if (colon >= 0)
                {
                    var last = colon;
                    while (last > i && char.IsWhiteSpace(line[last - 1])) last--;
                    JavaScriptHighlighter.Fill(styles, i, last, SyntaxStyle.Name);
                    ScanValue(line, colon + 1, stop, styles);
                }
                else
                {
                    // More of a value that started earlier
                    ScanValue(line, i, stop, styles);
                }

                i = stop;
            }
        }

        return (depth << 1) | (inComment ? InComment : 0);
    }

    // Where a selector or a property with its value ends: at "{", ";" or "}", or where a comment starts
    private static int StatementEnd(string line, int from, int end)
    {
        var i = from;
        while (i < end)
        {
            var c = line[i];
            if (c is '{' or ';' or '}') return i;
            if (c == '/' && i + 1 < end && line[i + 1] == '*') return i;
            if (c is '"' or '\'')
            {
                i = JavaScriptHighlighter.StringEnd(line, i + 1, end, c, out _);
                continue;
            }

            i++;
        }

        return end;
    }

    // The value of a property: strings, numbers with their units, colors and "!important"
    private static void ScanValue(string line, int from, int end, SyntaxStyle[] styles)
    {
        var i = from;
        while (i < end)
        {
            var c = line[i];
            var next = i + 1 < end ? line[i + 1] : '\0';
            if (c is '"' or '\'')
            {
                var stop = JavaScriptHighlighter.StringEnd(line, i + 1, end, c, out _);
                JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.String);
                i = stop;
            }
            else if (char.IsAsciiDigit(c) || (c is '.' or '-' or '+' && char.IsAsciiDigit(next)) ||
                     (c == '#' && char.IsAsciiHexDigit(next)))
            {
                // 12px, 1.5em, 50%, -3, #fff (not a digit that is part of a word)
                var isWordPart = i > from && (char.IsLetterOrDigit(line[i - 1]) || line[i - 1] is '-' or '_');
                var stop = i + 1;
                while (stop < end && (char.IsLetterOrDigit(line[stop]) || line[stop] is '.' or '%')) stop++;
                if (!isWordPart) JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Number);
                i = stop;
            }
            else if (c == '!')
            {
                var stop = i + 1;
                while (stop < end && char.IsLetter(line[stop])) stop++;
                JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Keyword);
                i = stop;
            }
            else
            {
                i++;
            }
        }
    }
}
