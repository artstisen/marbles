namespace Marbles;

/// <summary>
/// What a character of a colored text is part of. The first ones are those of Markdown, and the last ones
/// those of code (HTML, CSS, JavaScript and JSON)
/// </summary>
internal enum SyntaxStyle
{
    None,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Bold,
    Italic,
    Code, // `code` in a line
    CodeBlock, // A line of a block of code (between lines starting with ```)
    Quote,
    ListMarker,
    Link, // [text]
    LinkUrl, // (address), <address> and plain web addresses
    Rule, // A line like "---"
    Comment,
    String, // Text in quotes, and the values of attributes
    Keyword, // The words of a language, the tags of HTML, the selectors of CSS
    Name, // The names of attributes and properties
    Number, // Numbers, also with units, colors and character entities
    DiffFile, // The lines of a comparison that name the files ("---", "+++")
    DiffPart, // "@@ -12,7 +12,8 @@": where a part of a comparison is
    DiffRemoved, // A line removed ("-")
    DiffAdded // A line added ("+")
}

/// <summary>
/// Finds the parts of a line of Markdown that are to be colored in the editor: headings, bold, italic, code,
/// quotes, list markers, links and rules. It looks at one line at a time and knows nothing about the editor
/// or the console. The only thing a line depends on is whether it is inside a block of code.
///
/// This is a simple reading of Markdown and not a complete one: emphasis inside emphasis, emphasis over
/// several lines and tables are not found.
/// </summary>
internal static class MarkdownHighlighter
{
    /// <summary>
    /// True if the name of a file says it is a Markdown file
    /// </summary>
    internal static bool IsMarkdownFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".md" or ".markdown" or ".mdown" or ".mkd";
    }

    /// <summary>
    /// True if a line starts or ends a block of code: it starts with ``` or ~~~
    /// </summary>
    internal static bool IsFence(string line)
    {
        var start = Indent(line);
        if (start > 3 || start + 3 > line.Length) return false;
        var c = line[start];
        return c is '`' or '~' && line[start + 1] == c && line[start + 2] == c;
    }

    // The number of blank spaces a line starts with
    private static int Indent(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] == ' ') i++;
        return i;
    }

    /// <summary>
    /// Find the styles of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="inCodeBlock">True if the line is inside a block of code</param>
    /// <returns>The style of each character, or null if no character has a style</returns>
    internal static SyntaxStyle[]? Highlight(string line, bool inCodeBlock)
    {
        if (line.Length == 0) return null;
        var styles = new SyntaxStyle[line.Length];
        if (inCodeBlock || IsFence(line))
        {
            Array.Fill(styles, SyntaxStyle.CodeBlock);
            return styles;
        }

        var indent = Indent(line);
        if (indent == line.Length) return null;

        // Heading: one to six "#" and a blank space
        if (indent <= 3 && line[indent] == '#')
        {
            var level = 0;
            while (indent + level < line.Length && line[indent + level] == '#') level++;
            if (level <= 6 && (indent + level == line.Length || line[indent + level] == ' '))
            {
                Array.Fill(styles, SyntaxStyle.Heading1 + (level - 1));
                return styles;
            }
        }

        if (indent <= 3 && IsRule(line))
        {
            Array.Fill(styles, SyntaxStyle.Rule);
            return styles;
        }

        if (indent <= 3 && line[indent] == '>')
        {
            Array.Fill(styles, SyntaxStyle.Quote);
            return styles;
        }

        // The marker of a list item: "-", "*", "+" or a number, followed by a blank space
        var textStart = indent;
        var markerEnd = ListMarkerEnd(line, indent);
        if (markerEnd > indent)
        {
            for (var i = indent; i < markerEnd; i++) styles[i] = SyntaxStyle.ListMarker;
            textStart = markerEnd;
        }

        HighlightInline(line, textStart, styles);
        return Array.Exists(styles, style => style != SyntaxStyle.None) ? styles : null;
    }

    // A line of three or more "-", "*" or "_" (blank spaces between them are allowed)
    private static bool IsRule(string line)
    {
        var marker = '\0';
        var count = 0;
        foreach (var c in line)
        {
            if (c == ' ') continue;
            if (c is not ('-' or '*' or '_') || (marker != '\0' && c != marker)) return false;
            marker = c;
            count++;
        }

        return count >= 3;
    }

    // Where the marker of a list item ends (the position itself if there is no marker)
    private static int ListMarkerEnd(string line, int start)
    {
        var i = start;
        if (line[i] is '-' or '*' or '+')
        {
            i++;
        }
        else
        {
            while (i < line.Length && char.IsAsciiDigit(line[i]) && i - start < 9) i++;
            if (i == start || i >= line.Length || line[i] is not ('.' or ')')) return start;
            i++;
        }

        return i < line.Length && line[i] == ' ' ? i : start;
    }

    // Find code, bold, italic and links in the text of a line
    private static void HighlightInline(string line, int start, SyntaxStyle[] styles)
    {
        var i = start;
        while (i < line.Length)
        {
            var c = line[i];
            if (c == '\\')
            {
                // The next character is just a character
                i += 2;
            }
            else if (c == '`')
            {
                i = CodeSpan(line, i, styles);
            }
            else if (c is '*' or '_')
            {
                i = Emphasis(line, i, styles);
            }
            else if (c == '[' || (c == '!' && i + 1 < line.Length && line[i + 1] == '['))
            {
                i = Link(line, i, styles);
            }
            else if (c == '<')
            {
                // <https://example.com>
                var close = line.IndexOf('>', i);
                if (close > i && IsWebAddress(line, i + 1) && line.IndexOf(' ', i, close - i) < 0)
                {
                    Fill(styles, i, close + 1, SyntaxStyle.LinkUrl);
                    i = close + 1;
                }
                else
                {
                    i++;
                }
            }
            else if (c == 'h' && IsWebAddress(line, i) && (i == 0 || !char.IsLetterOrDigit(line[i - 1])))
            {
                // A plain web address: to the next blank space
                var end = i;
                while (end < line.Length && !char.IsWhiteSpace(line[end]) && line[end] is not (')' or '>')) end++;
                Fill(styles, i, end, SyntaxStyle.LinkUrl);
                i = end;
            }
            else
            {
                i++;
            }
        }
    }

    private static bool IsWebAddress(string line, int at)
    {
        return string.CompareOrdinal(line, at, "http://", 0, 7) == 0 ||
               string.CompareOrdinal(line, at, "https://", 0, 8) == 0;
    }

    private static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style)
    {
        for (var i = from; i < to && i < styles.Length; i++) styles[i] = style;
    }

    // The number of times a character is repeated from a position
    private static int RunLength(string line, int at)
    {
        var end = at;
        while (end < line.Length && line[end] == line[at]) end++;
        return end - at;
    }

    // `code`: from a run of backticks to the next run of the same length. Returns where to continue
    private static int CodeSpan(string line, int at, SyntaxStyle[] styles)
    {
        var length = RunLength(line, at);
        for (var i = at + length; i < line.Length; i++)
        {
            if (line[i] != '`') continue;
            var run = RunLength(line, i);
            if (run == length)
            {
                Fill(styles, at, i + run, SyntaxStyle.Code);
                return i + run;
            }

            i += run - 1;
        }

        return at + length;
    }

    // *italic*, **bold** and ***both*** (and the same with "_"). Returns where to continue
    private static int Emphasis(string line, int at, SyntaxStyle[] styles)
    {
        var marker = line[at];
        var run = RunLength(line, at);
        var textStart = at + run;
        // The text must follow directly. "_" inside a word (as in "my_file_name") is not emphasis
        if (textStart >= line.Length || char.IsWhiteSpace(line[textStart]) ||
            (marker == '_' && at > 0 && char.IsLetterOrDigit(line[at - 1])))
        {
            return textStart;
        }

        for (var length = Math.Min(run, 3); length >= 1; length--)
        {
            var close = ClosingRun(line, textStart, marker, length);
            if (close < 0) continue;
            // Markers that are not part of the emphasis are left as they are
            Fill(styles, at + run - length, close + length, length >= 2 ? SyntaxStyle.Bold : SyntaxStyle.Italic);
            return close + length;
        }

        return textStart;
    }

    // Where the emphasis started with a number of markers ends: at markers that follow the text directly.
    // One marker is closed by one marker, two (or three) by at least as many. -1 if it is not closed
    private static int ClosingRun(string line, int from, char marker, int length)
    {
        for (var i = from + 1; i < line.Length; i++)
        {
            if (line[i] == '\\')
            {
                i++;
                continue;
            }

            if (line[i] != marker) continue;
            var run = RunLength(line, i);
            var end = i + run;
            var fits = length == 1 ? run == 1 : run >= length;
            if (fits && !char.IsWhiteSpace(line[i - 1]) &&
                (marker != '_' || end >= line.Length || !char.IsLetterOrDigit(line[end])))
            {
                // With more markers than needed the last ones close it
                return length == 1 ? i : end - length;
            }

            i = end - 1;
        }

        return -1;
    }

    // [text](address) and ![text](address). Returns where to continue
    private static int Link(string line, int at, SyntaxStyle[] styles)
    {
        var open = line[at] == '!' ? at + 1 : at;
        var close = line.IndexOf(']', open + 1);
        if (close < 0) return open + 1;
        Fill(styles, at, close + 1, SyntaxStyle.Link);
        if (close + 1 < line.Length && line[close + 1] == '(')
        {
            var end = line.IndexOf(')', close + 2);
            if (end > 0)
            {
                Fill(styles, close + 1, end + 1, SyntaxStyle.LinkUrl);
                return end + 1;
            }
        }
        else if (close + 1 < line.Length && line[close + 1] == '[')
        {
            // [text][reference]
            var end = line.IndexOf(']', close + 2);
            if (end > 0)
            {
                Fill(styles, close + 1, end + 1, SyntaxStyle.LinkUrl);
                return end + 1;
            }
        }
        else if (close == open + 2 && line[open + 1] is ' ' or 'x' or 'X')
        {
            // The box of a task: "[ ]" and "[x]"
            Fill(styles, at, close + 1, SyntaxStyle.ListMarker);
        }

        return close + 1;
    }
}
