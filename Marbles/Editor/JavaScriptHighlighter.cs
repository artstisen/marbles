namespace Marbles;

/// <summary>
/// Finds the parts of a line of JavaScript (and JSON) to color: comments, strings, keywords and numbers.
/// It is a simple reading: regular expressions written between slashes, and the code inside template
/// strings, are not recognized.
/// </summary>
internal static class JavaScriptHighlighter
{
    // The state a line starts in: inside something that began on an earlier line
    internal const int Normal = 0;
    internal const int InComment = 1; // A /* comment */
    internal const int InTemplate = 2; // A `template string`

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "async", "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
        "do", "else", "export", "extends", "finally", "for", "from", "function", "get", "if", "import", "in",
        "instanceof", "let", "new", "of", "return", "set", "static", "super", "switch", "this", "throw", "try",
        "typeof", "var", "void", "while", "with", "yield",
        "true", "false", "null", "undefined", "NaN", "Infinity"
    };

    /// <summary>
    /// Find the styles of a part of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="start">The first character to look at</param>
    /// <param name="end">The character after the last</param>
    /// <param name="state">The state the part starts in</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    /// <returns>The state after the part</returns>
    internal static int Scan(string line, int start, int end, int state, SyntaxStyle[] styles)
    {
        var i = start;
        while (i < end)
        {
            if (state == InComment)
            {
                var close = line.IndexOf("*/", i, end - i, StringComparison.Ordinal);
                var stop = close < 0 ? end : close + 2;
                Fill(styles, i, stop, SyntaxStyle.Comment);
                if (close >= 0) state = Normal;
                i = stop;
                continue;
            }

            if (state == InTemplate)
            {
                var stop = StringEnd(line, i, end, '`', out var closed);
                Fill(styles, i, stop, SyntaxStyle.String);
                if (closed) state = Normal;
                i = stop;
                continue;
            }

            var c = line[i];
            var next = i + 1 < end ? line[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                Fill(styles, i, end, SyntaxStyle.Comment);
                return state;
            }

            if (c == '/' && next == '*')
            {
                Fill(styles, i, i + 2, SyntaxStyle.Comment);
                state = InComment;
                i += 2;
            }
            else if (c is '"' or '\'')
            {
                // A string ends at its quote, or at the end of the line
                var stop = StringEnd(line, i + 1, end, c, out _);
                Fill(styles, i, stop, SyntaxStyle.String);
                i = stop;
            }
            else if (c == '`')
            {
                styles[i] = SyntaxStyle.String;
                state = InTemplate;
                i++;
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(next)))
            {
                var stop = i + 1;
                while (stop < end && (char.IsLetterOrDigit(line[stop]) || line[stop] is '.' or '_')) stop++;
                Fill(styles, i, stop, SyntaxStyle.Number);
                i = stop;
            }
            else if (IsNameStart(c))
            {
                var stop = i + 1;
                while (stop < end && (IsNameStart(line[stop]) || char.IsAsciiDigit(line[stop]))) stop++;
                // (a word after a dot is the name of a property, as in "object.default")
                if ((i == start || line[i - 1] != '.') && Keywords.Contains(line[i..stop]))
                {
                    Fill(styles, i, stop, SyntaxStyle.Keyword);
                }

                i = stop;
            }
            else
            {
                i++;
            }
        }

        return state;
    }

    /// <summary>
    /// Find the styles of a line of JSON: names, strings, numbers and the words true, false and null
    /// </summary>
    internal static void ScanJson(string line, SyntaxStyle[] styles)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == '"')
            {
                var stop = StringEnd(line, i + 1, line.Length, '"', out _);
                // A string followed by a colon is a name
                var after = stop;
                while (after < line.Length && line[after] == ' ') after++;
                Fill(styles, i, stop,
                    after < line.Length && line[after] == ':' ? SyntaxStyle.Name : SyntaxStyle.String);
                i = stop;
            }
            else if (char.IsAsciiDigit(c) || (c == '-' && i + 1 < line.Length && char.IsAsciiDigit(line[i + 1])))
            {
                var stop = i + 1;
                while (stop < line.Length && (char.IsAsciiDigit(line[stop]) || line[stop] is '.' or 'e' or 'E' or '+' or '-'))
                {
                    stop++;
                }

                Fill(styles, i, stop, SyntaxStyle.Number);
                i = stop;
            }
            else if (char.IsAsciiLetter(c))
            {
                var stop = i + 1;
                while (stop < line.Length && char.IsAsciiLetter(line[stop])) stop++;
                if (line[i..stop] is "true" or "false" or "null") Fill(styles, i, stop, SyntaxStyle.Keyword);
                i = stop;
            }
            else
            {
                i++;
            }
        }
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c is '_' or '$';

    // Where a string ends: after the closing quote, or at the end (a backslash keeps the next character)
    internal static int StringEnd(string line, int from, int end, char quote, out bool closed)
    {
        closed = false;
        var i = from;
        while (i < end)
        {
            if (line[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (line[i] == quote)
            {
                closed = true;
                return i + 1;
            }

            i++;
        }

        return end;
    }

    internal static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style)
    {
        for (var i = from; i < to && i < styles.Length; i++) styles[i] = style;
    }
}
