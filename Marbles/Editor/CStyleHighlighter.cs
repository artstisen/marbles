namespace Marbles;

/// <summary>
/// What a language written in the style of C is colored by: its keywords, and the kinds of strings and lines it
/// has besides those of C (// and /* */ comments, strings in ' and ", numbers)
/// </summary>
internal sealed class CStyleLanguage
{
    internal HashSet<string> Keywords { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// `Template strings`, also over several lines (JavaScript)
    /// </summary>
    internal bool TemplateStrings { get; init; }

    /// <summary>
    /// @"Verbatim strings" and """raw strings""", also over several lines, $"strings" and @names (C#)
    /// </summary>
    internal bool VerbatimStrings { get; init; }

    /// <summary>
    /// Lines of the preprocessor (#region, #if, #include ...), colored as names
    /// </summary>
    internal bool Preprocessor { get; init; }

    /// <summary>
    /// "$" is a part of names (JavaScript)
    /// </summary>
    internal bool DollarInNames { get; init; }
}

/// <summary>
/// Finds the parts of a line of a language written in the style of C to color: comments, strings, keywords and
/// numbers, as each language (CStyleLanguage) has them. It is a simple reading, not a parser: the code inside a
/// string (as in $"{x}" or `${x}`) is colored as the string, and names are only colored when they are keywords.
/// A line starts in a state, which tells what it is inside of that began on an earlier line (a comment, a string)
/// </summary>
internal static class CStyleHighlighter
{
    // The state a line starts in: inside something that began on an earlier line
    internal const int Normal = 0;
    internal const int InComment = 1; // A /* comment */
    internal const int InTemplate = 2; // A `template string` (JavaScript)
    internal const int InVerbatim = 3; // A @"verbatim string" (C#)
    internal const int InRaw = 4; // A """raw string""" (C#)

    internal static readonly CStyleLanguage JavaScript = new()
    {
        Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "async", "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete",
            "do", "else", "export", "extends", "finally", "for", "from", "function", "get", "if", "import", "in",
            "instanceof", "let", "new", "of", "return", "set", "static", "super", "switch", "this", "throw", "try",
            "typeof", "var", "void", "while", "with", "yield",
            "true", "false", "null", "undefined", "NaN", "Infinity"
        },
        TemplateStrings = true,
        DollarInNames = true
    };

    internal static readonly CStyleLanguage CSharp = new()
    {
        Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
            "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out",
            "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
            "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile",
            "while",
            // The words that are keywords where they are used
            "add", "and", "async", "await", "dynamic", "file", "get", "global", "init", "nameof", "nint", "not",
            "nuint", "or", "partial", "record", "remove", "required", "scoped", "set", "value", "var", "when",
            "where", "with", "yield"
        },
        VerbatimStrings = true,
        Preprocessor = true
    };

    /// <summary>
    /// Find the styles of a part of a line
    /// </summary>
    /// <param name="language">The language</param>
    /// <param name="line">The line</param>
    /// <param name="start">The first character to look at</param>
    /// <param name="end">The character after the last</param>
    /// <param name="state">The state the part starts in</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    /// <returns>The state after the part</returns>
    internal static int Scan(CStyleLanguage language, string line, int start, int end, int state,
        SyntaxStyle[] styles)
    {
        var i = start;
        if (state == Normal && language.Preprocessor)
        {
            // A line of the preprocessor: all of it
            var first = start;
            while (first < end && char.IsWhiteSpace(line[first])) first++;
            if (first < end && line[first] == '#')
            {
                Fill(styles, first, end, SyntaxStyle.Name);
                return state;
            }
        }

        while (i < end)
        {
            if (state != Normal)
            {
                // Inside a comment or a string that began before: up to its end, or the end of the part
                var (stop, closed) = state switch
                {
                    InComment => EndOf(line, i, end, "*/"),
                    InTemplate => (JavaScriptHighlighter.StringEnd(line, i, end, '`', out var template), template),
                    InVerbatim => (VerbatimEnd(line, i, end, out var verbatim), verbatim),
                    _ => EndOf(line, i, end, "\"\"\"")
                };
                Fill(styles, i, stop, state == InComment ? SyntaxStyle.Comment : SyntaxStyle.String);
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
            else if (language.VerbatimStrings && c is '@' or '$' && StringPrefixEnd(line, i, end) is var quote &&
                     quote < end && line[quote] == '"')
            {
                // $"text", @"text", $@"text" and $"""text"""
                if (IsRawStart(line, quote, end))
                {
                    Fill(styles, i, quote + 3, SyntaxStyle.String);
                    state = InRaw;
                    i = quote + 3;
                }
                else if (line.AsSpan(i, quote - i).Contains('@'))
                {
                    Fill(styles, i, quote + 1, SyntaxStyle.String);
                    state = InVerbatim;
                    i = quote + 1;
                }
                else
                {
                    var stop = JavaScriptHighlighter.StringEnd(line, quote + 1, end, '"', out _);
                    Fill(styles, i, stop, SyntaxStyle.String);
                    i = stop;
                }
            }
            else if (language.VerbatimStrings && c == '@' && IsNameStart(language, next))
            {
                // @class: a name, not the keyword
                i = NameEnd(language, line, i + 1, end);
            }
            else if (language.VerbatimStrings && IsRawStart(line, i, end))
            {
                Fill(styles, i, i + 3, SyntaxStyle.String);
                state = InRaw;
                i += 3;
            }
            else if (c is '"' or '\'')
            {
                // A string ends at its quote, or at the end of the line
                var stop = JavaScriptHighlighter.StringEnd(line, i + 1, end, c, out _);
                Fill(styles, i, stop, SyntaxStyle.String);
                i = stop;
            }
            else if (language.TemplateStrings && c == '`')
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
            else if (IsNameStart(language, c))
            {
                var stop = NameEnd(language, line, i + 1, end);
                // (a word after a dot is the name of a member, as in "object.default")
                if ((i == start || line[i - 1] != '.') && language.Keywords.Contains(line[i..stop]))
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

    private static bool IsNameStart(CStyleLanguage language, char c) =>
        char.IsLetter(c) || c == '_' || (c == '$' && language.DollarInNames);

    private static int NameEnd(CStyleLanguage language, string line, int from, int end)
    {
        while (from < end && (IsNameStart(language, line[from]) || char.IsAsciiDigit(line[from]))) from++;
        return from;
    }

    // Where "$", "@" (or both) in front of a string end: at the quote, if there is one
    private static int StringPrefixEnd(string line, int i, int end)
    {
        var stop = i;
        while (stop < end && stop - i < 3 && line[stop] is '@' or '$') stop++;
        return stop;
    }

    private static bool IsRawStart(string line, int i, int end) =>
        i + 2 < end && line[i] == '"' && line[i + 1] == '"' && line[i + 2] == '"';

    // Where a comment or a raw string ends: after its closing mark, or at the end
    private static (int Stop, bool Closed) EndOf(string line, int from, int end, string mark)
    {
        var close = line.IndexOf(mark, from, end - from, StringComparison.Ordinal);
        return close < 0 ? (end, false) : (close + mark.Length, true);
    }

    // Where a verbatim string ends: after its closing quote ("" is a quote in it), or at the end
    private static int VerbatimEnd(string line, int from, int end, out bool closed)
    {
        closed = false;
        var i = from;
        while (i < end)
        {
            if (line[i] == '"')
            {
                if (i + 1 < end && line[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                closed = true;
                return i + 1;
            }

            i++;
        }

        return end;
    }

    private static void Fill(SyntaxStyle[] styles, int from, int to, SyntaxStyle style) =>
        JavaScriptHighlighter.Fill(styles, from, to, style);
}
