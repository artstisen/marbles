namespace Marbles;

/// <summary>
/// The coloring of JavaScript and JSON. JavaScript is read by the reader of the languages written in the style of
/// C (CStyleHighlighter.JavaScript); this class reads JSON, and has what the other highlighters share (Fill,
/// StringEnd).
/// </summary>
internal static class JavaScriptHighlighter
{
    // The state a line starts in: inside something that began on an earlier line
    internal const int Normal = CStyleHighlighter.Normal;
    internal const int InComment = CStyleHighlighter.InComment; // A /* comment */
    internal const int InTemplate = CStyleHighlighter.InTemplate; // A `template string`

    /// <summary>
    /// Find the styles of a part of a line of JavaScript
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="start">The first character to look at</param>
    /// <param name="end">The character after the last</param>
    /// <param name="state">The state the part starts in</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    /// <returns>The state after the part</returns>
    internal static int Scan(string line, int start, int end, int state, SyntaxStyle[] styles) =>
        CStyleHighlighter.Scan(CStyleHighlighter.JavaScript, line, start, end, state, styles);

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
