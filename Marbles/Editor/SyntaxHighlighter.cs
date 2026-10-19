namespace Marbles;

/// <summary>
/// The kinds of text the editor can color
/// </summary>
internal enum SyntaxType
{
    None,
    Markdown,
    Html,
    Css,
    JavaScript,
    Json,
    Xml,
    Diff, // A comparison (":diff"), and .diff and .patch files
    Macro, // A macro of the editor (.macro files, see MacroLanguage)
    CSharp
}

/// <summary>
/// The coloring of text in the editor by what it is: Markdown, HTML, CSS, JavaScript, JSON, XML or C#.
/// Each kind has a class of its own that finds the styles of a line (MarkdownHighlighter, HtmlHighlighter ...).
/// This class picks the one to use, and tells what kind a file is from its name.
///
/// A line is colored from its text and a state: a number that tells what the line starts inside (a comment,
/// a block of code, a tag ...). The state after the line is the state of the next line, and 0 is the state
/// of the first line.
/// </summary>
internal static class SyntaxHighlighter
{
    /// <summary>
    /// True to color files of a known kind when they are opened in the editor (setting "syntaxcolors")
    /// </summary>
    internal static bool ColorFiles { get; set; } = true;

    /// <summary>
    /// The kind of text in a file, from the extension of its name
    /// </summary>
    internal static SyntaxType TypeOfFile(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".md" or ".markdown" or ".mdown" or ".mkd" => SyntaxType.Markdown,
            ".html" or ".htm" or ".xhtml" => SyntaxType.Html,
            ".css" => SyntaxType.Css,
            ".js" or ".mjs" or ".cjs" => SyntaxType.JavaScript,
            ".cs" or ".csx" => SyntaxType.CSharp,
            ".json" => SyntaxType.Json,
            ".xml" or ".xsd" or ".xsl" or ".xslt" or ".svg" or ".xaml" or ".rss" or ".atom" or ".plist"
                or ".csproj" or ".props" or ".targets" or ".resx" => SyntaxType.Xml,
            ".diff" or ".patch" => SyntaxType.Diff,
            ".macro" => SyntaxType.Macro,
            _ => SyntaxType.None
        };
    }

    /// <summary>
    /// The kind of text given by a name in a command (":syntax html"). None if the name is not known
    /// </summary>
    internal static SyntaxType ParseType(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "md" or "markdown" => SyntaxType.Markdown,
            "html" or "htm" => SyntaxType.Html,
            "css" => SyntaxType.Css,
            "js" or "javascript" => SyntaxType.JavaScript,
            "cs" or "csharp" or "c#" => SyntaxType.CSharp,
            "json" => SyntaxType.Json,
            "xml" => SyntaxType.Xml,
            "diff" or "patch" => SyntaxType.Diff,
            "macro" => SyntaxType.Macro,
            _ => SyntaxType.None
        };
    }

    internal static string Name(SyntaxType type)
    {
        return type switch
        {
            SyntaxType.Markdown => "Markdown",
            SyntaxType.Html => "HTML",
            SyntaxType.Css => "CSS",
            SyntaxType.JavaScript => "JavaScript",
            SyntaxType.CSharp => "C#",
            SyntaxType.Json => "JSON",
            SyntaxType.Xml => "XML",
            SyntaxType.Diff => "Diff",
            SyntaxType.Macro => "Macro",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Find the styles of a line
    /// </summary>
    /// <param name="type">The kind of text</param>
    /// <param name="line">The line</param>
    /// <param name="state">The state the line starts in</param>
    /// <param name="nextState">The state after the line</param>
    /// <returns>The style of each character, or null if no character has a style</returns>
    internal static SyntaxStyle[]? Highlight(SyntaxType type, string line, int state, out int nextState)
    {
        nextState = state;
        if (type == SyntaxType.Markdown)
        {
            // The state tells if the line is inside a block of code
            if (MarkdownHighlighter.IsFence(line)) nextState = state == 0 ? 1 : 0;
            return MarkdownHighlighter.Highlight(line, state != 0);
        }

        if (type == SyntaxType.None || line.Length == 0) return null;
        var styles = new SyntaxStyle[line.Length];
        switch (type)
        {
            case SyntaxType.Html:
                nextState = HtmlHighlighter.Scan(line, state, styles);
                break;
            case SyntaxType.Css:
                nextState = CssHighlighter.Scan(line, 0, line.Length, state, styles);
                break;
            case SyntaxType.JavaScript:
                nextState = JavaScriptHighlighter.Scan(line, 0, line.Length, state, styles);
                break;
            case SyntaxType.Json:
                JavaScriptHighlighter.ScanJson(line, styles);
                break;
            case SyntaxType.CSharp:
                nextState = CStyleHighlighter.Scan(CStyleHighlighter.CSharp, line, 0, line.Length, state, styles);
                break;
            case SyntaxType.Xml:
                nextState = HtmlHighlighter.Scan(line, state, styles, true);
                break;
            case SyntaxType.Diff:
                nextState = ScanDiff(line, state, styles);
                break;
            case SyntaxType.Macro:
                MacroLanguage.Scan(line, styles);
                break;
        }

        return Array.Exists(styles, style => style != SyntaxStyle.None) ? styles : null;
    }

    // A line of a unified diff: the lines naming the files (before the first "@@"), "@@" lines, and the lines
    // removed ("-") and added ("+"). The state is 1 after the first "@@" (where "---" is a line removed)
    private static int ScanDiff(string line, int state, SyntaxStyle[] styles)
    {
        var style = SyntaxStyle.None;
        if (line.StartsWith("@@", StringComparison.Ordinal))
        {
            style = SyntaxStyle.DiffPart;
            state = 1;
        }
        else if (state == 0 && (line.StartsWith("---", StringComparison.Ordinal) ||
                                line.StartsWith("+++", StringComparison.Ordinal) ||
                                line.StartsWith("diff ", StringComparison.Ordinal) ||
                                line.StartsWith("index ", StringComparison.Ordinal)))
        {
            style = SyntaxStyle.DiffFile;
        }
        else if (line[0] == '-')
        {
            style = SyntaxStyle.DiffRemoved;
        }
        else if (line[0] == '+')
        {
            style = SyntaxStyle.DiffAdded;
        }
        else if (line.StartsWith("diff ", StringComparison.Ordinal))
        {
            // The next file of a patch
            style = SyntaxStyle.DiffFile;
            state = 0;
        }

        Array.Fill(styles, style);
        return state;
    }
}
