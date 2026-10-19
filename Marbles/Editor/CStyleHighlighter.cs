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

    /// <summary>
    /// The rules of PHP: $variables colored as names, # comments and #[attributes], strings in ' and " over several
    /// lines, heredoc and nowdoc (&lt;&lt;&lt;EOT), "->" before a member, and "?&gt;" ending the code (the HTML
    /// around it is read by HtmlHighlighter)
    /// </summary>
    internal bool Php { get; init; }
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
    internal const int InSingle = 5; // A 'string' over several lines (PHP)
    internal const int InDouble = 6; // A "string" over several lines (PHP)
    internal const int InHeredoc = 7; // A heredoc or nowdoc, to the line with its marker (PHP)

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

    internal static readonly CStyleLanguage Php = new()
    {
        // (the words of PHP are the same in any letter case)
        Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "abstract", "and", "array", "as", "break", "callable", "case", "catch", "class", "clone", "const",
            "continue", "declare", "default", "do", "echo", "else", "elseif", "empty", "enddeclare", "endfor",
            "endforeach", "endif", "endswitch", "endwhile", "enum", "eval", "exit", "die", "extends", "final",
            "finally", "fn", "for", "foreach", "function", "global", "goto", "if", "implements", "include",
            "include_once", "instanceof", "insteadof", "interface", "isset", "list", "match", "namespace", "new",
            "or", "print", "private", "protected", "public", "readonly", "require", "require_once", "return",
            "static", "switch", "throw", "trait", "try", "unset", "use", "var", "while", "xor", "yield",
            "true", "false", "null", "self", "parent",
            "int", "float", "bool", "string", "void", "mixed", "never", "object", "iterable",
            "__CLASS__", "__DIR__", "__FILE__", "__FUNCTION__", "__LINE__", "__METHOD__", "__NAMESPACE__",
            "__TRAIT__", "__COMPILER_HALT_OFFSET__"
        },
        Php = true
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
        SyntaxStyle[] styles) => Scan(language, line, start, end, state, styles, out _);

    /// <param name="stop">Where the code ended: at "?&gt;" in PHP, else at the end of the part</param>
    internal static int Scan(CStyleLanguage language, string line, int start, int end, int state,
        SyntaxStyle[] styles, out int stop)
    {
        stop = end;
        var i = start;
        if (state == InHeredoc)
        {
            // To the line that has only the marker (as "EOT;"): the code goes on after it
            var marker = HeredocMarkerEnd(line, start, end);
            if (marker < 0)
            {
                Fill(styles, start, end, SyntaxStyle.String);
                return state;
            }

            Fill(styles, start, marker, SyntaxStyle.String);
            state = Normal;
            i = marker;
        }

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
                var (to, closed) = state switch
                {
                    InComment => EndOf(line, i, end, "*/"),
                    InTemplate => (JavaScriptHighlighter.StringEnd(line, i, end, '`', out var template), template),
                    InVerbatim => (VerbatimEnd(line, i, end, out var verbatim), verbatim),
                    InSingle => (JavaScriptHighlighter.StringEnd(line, i, end, '\'', out var single), single),
                    InDouble => (JavaScriptHighlighter.StringEnd(line, i, end, '"', out var quoted), quoted),
                    _ => EndOf(line, i, end, "\"\"\"")
                };
                Fill(styles, i, to, state == InComment ? SyntaxStyle.Comment : SyntaxStyle.String);
                if (closed) state = Normal;
                i = to;
                continue;
            }

            var c = line[i];
            var next = i + 1 < end ? line[i + 1] : '\0';
            if (language.Php && c == '?' && next == '>')
            {
                // The end of the code: the HTML goes on
                stop = i;
                return state;
            }

            if ((c == '/' && next == '/') || (language.Php && c == '#' && next != '['))
            {
                // To the end of the line (in PHP, or to "?>")
                var close = language.Php ? line.IndexOf("?>", i, end - i, StringComparison.Ordinal) : -1;
                Fill(styles, i, close < 0 ? end : close, SyntaxStyle.Comment);
                if (close < 0) return state;
                stop = close;
                return state;
            }

            if (language.Php && c == '#')
            {
                // #[Attribute]
                var close = line.IndexOf(']', i, end - i);
                var to = close < 0 ? end : close + 1;
                Fill(styles, i, to, SyntaxStyle.Name);
                i = to;
                continue;
            }

            if (language.Php && c == '$' && IsNameStart(language, next))
            {
                // $variable
                var to = NameEnd(language, line, i + 1, end);
                Fill(styles, i, to, SyntaxStyle.Name);
                i = to;
                continue;
            }

            if (language.Php && c == '<' && string.CompareOrdinal(line, i, "<<<", 0, 3) == 0 &&
                HeredocStart(line, i + 3, end) is var after and > 0)
            {
                // <<<EOT, <<<"EOT" and <<<'EOT': the text follows on the next lines
                Fill(styles, i, end, SyntaxStyle.String);
                return InHeredoc;
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
                    var to = JavaScriptHighlighter.StringEnd(line, quote + 1, end, '"', out _);
                    Fill(styles, i, to, SyntaxStyle.String);
                    i = to;
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
                var to = JavaScriptHighlighter.StringEnd(line, i + 1, end, c, out var closed);
                Fill(styles, i, to, SyntaxStyle.String);
                // In PHP a string goes on to the next line
                if (!closed && language.Php) state = c == '"' ? InDouble : InSingle;
                i = to;
            }
            else if (language.TemplateStrings && c == '`')
            {
                styles[i] = SyntaxStyle.String;
                state = InTemplate;
                i++;
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(next)))
            {
                var to = i + 1;
                while (to < end && (char.IsLetterOrDigit(line[to]) || line[to] is '.' or '_')) to++;
                Fill(styles, i, to, SyntaxStyle.Number);
                i = to;
            }
            else if (IsNameStart(language, c))
            {
                var to = NameEnd(language, line, i + 1, end);
                // (a word after a dot is the name of a member, as in "object.default", and in PHP after "->")
                var member = i > start && (line[i - 1] == '.' ||
                                           (language.Php && line[i - 1] == '>' && i - 2 >= start && line[i - 2] == '-'));
                if (!member && language.Keywords.Contains(line[i..to]))
                {
                    Fill(styles, i, to, SyntaxStyle.Keyword);
                }

                i = to;
            }
            else
            {
                i++;
            }
        }

        return state;
    }

    // After "<<<": the marker of a heredoc ("EOT", "\"EOT\"" or "'EOT'"), and where it ends, or -1 if there is none
    private static int HeredocStart(string line, int from, int end)
    {
        var i = from;
        while (i < end && line[i] == ' ') i++;
        var quote = i < end && line[i] is '"' or '\'' ? line[i] : '\0';
        if (quote != '\0') i++;
        var name = i;
        while (i < end && (char.IsAsciiLetterOrDigit(line[i]) || line[i] == '_')) i++;
        if (i == name || char.IsAsciiDigit(line[name])) return -1;
        if (quote != '\0' && (i >= end || line[i] != quote)) return -1;
        return quote != '\0' ? i + 1 : i;
    }

    // The line that ends a heredoc: only its marker (a word in capitals, as "EOT" or "SQL"), maybe followed by ";",
    // ",", ")" or "]" and more code. Where the marker ends, or -1 if this is a line of the text. (The marker is not
    // known here, as the state of a line is a number: a line like that is taken to be it)
    private static int HeredocMarkerEnd(string line, int start, int end)
    {
        var i = start;
        while (i < end && char.IsWhiteSpace(line[i])) i++;
        var name = i;
        while (i < end && (char.IsAsciiLetterUpper(line[i]) || char.IsAsciiDigit(line[i]) || line[i] == '_')) i++;
        if (i == name || !char.IsAsciiLetterUpper(line[name])) return -1;
        return i == end || line[i] is ';' or ',' or ')' or ']' ? i : -1;
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
