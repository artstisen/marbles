using System.Text;

namespace Marbles;

/// <summary>
/// The completions of the editor: while text is typed in Insert mode (with ":complete" on), the word before the
/// cursor is looked up in lists of words, and what it can be completed to is shown above the text. Tab completes.
///
/// The lists are kept in the file "completions.txt" in the data directory, which is edited in the editor (":complete
/// edit"). A line starting with "===" starts a list, and the rest of the line is its title. A tag at the end of the
/// title, like "(html)" or "(css)", uses the list only when that kind of text is edited (as the tags of snippets); a
/// list without a tag is used for all text. Each line of a list is a word, or "word = text": the word is what is
/// typed, and the text what it is completed to. In the text "$0" marks where the cursor is placed, "\n" starts a new
/// line and "\\" is a "\". The text above the first title is not used.
///
/// This class does not touch the console: the suggestions are kept by the editor (EditorEngine) and drawn by
/// TextArea.
/// </summary>
internal static class Completions
{
    internal const string FileName = "completions.txt";
    private const string TitleMark = "===";

    // The most suggestions shown at a time, and the fewest characters of a word that are looked up
    internal const int MaxSuggestions = 8;
    internal const int MinWordLength = 2;

    /// <summary>
    /// A completion: the word typed, the text it is completed to, and the kind of text it is for (None: all)
    /// </summary>
    internal sealed record Item(string Word, string Text, SyntaxType Syntax)
    {
        /// <summary>
        /// How the completion is shown: the first line of its text, without the cursor mark ("..." if it has more
        /// lines)
        /// </summary>
        internal string Shown
        {
            get
            {
                var lines = Text.Replace(Snippets.CursorMark, string.Empty).Split('\n');
                var first = lines[0].Trim();
                return lines.Length > 1 ? first + " ..." : first;
            }
        }
    }

    /// <summary>
    /// Complete words while typing (setting "autocomplete", ":complete" in the editor)
    /// </summary>
    internal static bool Enabled { get; set; }

    private static readonly List<Item> List = new();

    internal static IReadOnlyList<Item> Items => List;

    /// <summary>
    /// The file with the completions. It can be set to another file than the one in the data directory (for
    /// testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    internal static bool Exists => File.Exists(FilePath);

    // When the file read was last written (it is read again when it has changed)
    private static DateTime _loadedWriteTime = DateTime.MinValue;
    private static string _loadedPath = string.Empty;

    // The file made when there is none: how it works, and lists for HTML, CSS, JavaScript, Markdown and all text
    // (English: the file is not translated)
    private const string NewFileText =
        "Completions for the editor of Marbles (:complete in the editor turns them\n" + // English
        "on and off, and :complete edit opens this file).\n" + // English
        "\n" +
        "While you type in Insert mode, the word before the cursor is looked up in\n" + // English
        "the lists below, and what it can be completed to is shown on the line above\n" + // English
        "the text. Tab completes when there is one. When there are more, Tab goes\n" + // English
        "through them (Shift+Tab back) and Enter uses the one chosen. Typing on\n" + // English
        "closes the list, and so does Esc (the next Esc leaves Insert mode).\n" + // English
        "\n" +
        "Each list starts with a line \"=== \" and its title. A tag at the end of the\n" + // English
        "title, like (html) or (css), uses the list only when that kind of text is\n" + // English
        "edited (md, html, css, js, json or xml); a list without a tag is used for\n" + // English
        "all text. Each line of a list is a word, or \"word = text\": the word is\n" + // English
        "what you type, and the text what it is completed to. In the text, $0 marks\n" + // English
        "where the cursor is placed, \\n starts a new line and \\\\ is a \\. The lines\n" + // English
        "after the first are indented as the line of the cursor. Blank lines are not\n" + // English
        "used, and neither is the text above the first title.\n" + // English
        "\n" +
        "=== HTML tags (html)\n" + // English
        "<!-- = <!-- $0 -->\n" +
        "<!DOCTYPE = <!DOCTYPE html>\n" +
        "<a = <a href=\"$0\"></a>\n" +
        "<article = <article>\\n    $0\\n</article>\n" +
        "<aside = <aside>\\n    $0\\n</aside>\n" +
        "<b = <b>$0</b>\n" +
        "<blockquote = <blockquote>$0</blockquote>\n" +
        "<body = <body>\\n    $0\\n</body>\n" +
        "<br = <br>\n" +
        "<button = <button type=\"button\">$0</button>\n" +
        "<code = <code>$0</code>\n" +
        "<div = <div>$0</div>\n" +
        "<em = <em>$0</em>\n" +
        "<footer = <footer>\\n    $0\\n</footer>\n" +
        "<form = <form action=\"$0\" method=\"post\">\\n</form>\n" +
        "<h1 = <h1>$0</h1>\n" +
        "<h2 = <h2>$0</h2>\n" +
        "<h3 = <h3>$0</h3>\n" +
        "<head = <head>\\n    $0\\n</head>\n" +
        "<header = <header>\\n    $0\\n</header>\n" +
        "<hr = <hr>\n" +
        "<i = <i>$0</i>\n" +
        "<img = <img src=\"$0\" alt=\"\">\n" +
        "<input = <input type=\"$0\" name=\"\">\n" +
        "<label = <label for=\"$0\"></label>\n" +
        "<li = <li>$0</li>\n" +
        "<link = <link rel=\"stylesheet\" href=\"$0\">\n" +
        "<main = <main>\\n    $0\\n</main>\n" +
        "<meta = <meta name=\"$0\" content=\"\">\n" +
        "<nav = <nav>\\n    $0\\n</nav>\n" +
        "<ol = <ol>\\n    <li>$0</li>\\n</ol>\n" +
        "<option = <option value=\"$0\"></option>\n" +
        "<p = <p>$0</p>\n" +
        "<pre = <pre>$0</pre>\n" +
        "<script = <script src=\"$0\"></script>\n" +
        "<section = <section>\\n    $0\\n</section>\n" +
        "<select = <select name=\"$0\">\\n</select>\n" +
        "<span = <span>$0</span>\n" +
        "<strong = <strong>$0</strong>\n" +
        "<style = <style>\\n    $0\\n</style>\n" +
        "<table = <table>\\n    <tr>\\n        <td>$0</td>\\n    </tr>\\n</table>\n" +
        "<td = <td>$0</td>\n" +
        "<textarea = <textarea name=\"$0\"></textarea>\n" +
        "<th = <th>$0</th>\n" +
        "<title = <title>$0</title>\n" +
        "<tr = <tr>$0</tr>\n" +
        "<ul = <ul>\\n    <li>$0</li>\\n</ul>\n" +
        "&nbsp = &nbsp;\n" +
        "&amp = &amp;\n" +
        "\n" +
        "=== CSS properties (css)\n" + // English
        "@media = @media (max-width: $0) {\\n}\n" +
        "align-items = align-items: $0;\n" +
        "background-color = background-color: $0;\n" +
        "border = border: $0;\n" +
        "border-radius = border-radius: $0;\n" +
        "box-sizing = box-sizing: border-box;\n" +
        "color = color: $0;\n" +
        "display = display: $0;\n" +
        "flex-direction = flex-direction: $0;\n" +
        "font-family = font-family: $0;\n" +
        "font-size = font-size: $0;\n" +
        "font-weight = font-weight: $0;\n" +
        "gap = gap: $0;\n" +
        "grid-template-columns = grid-template-columns: $0;\n" +
        "height = height: $0;\n" +
        "justify-content = justify-content: $0;\n" +
        "line-height = line-height: $0;\n" +
        "margin = margin: $0;\n" +
        "max-width = max-width: $0;\n" +
        "overflow = overflow: $0;\n" +
        "padding = padding: $0;\n" +
        "position = position: $0;\n" +
        "text-align = text-align: $0;\n" +
        "transition = transition: $0;\n" +
        "width = width: $0;\n" +
        "z-index = z-index: $0;\n" +
        "\n" +
        "=== JavaScript (js)\n" + // English
        "addEventListener = addEventListener('$0', () => {\\n});\n" +
        "console.log = console.log($0);\n" +
        "const = const $0 = ;\n" +
        "document.querySelector = document.querySelector('$0')\n" +
        "for = for (let i = 0; i < $0; i++) {\\n}\n" +
        "function = function $0() {\\n}\n" +
        "if = if ($0) {\\n}\n" +
        "return = return $0;\n" +
        "\n" +
        "=== Markdown (md)\n" + // English
        "code = ```\\n$0\\n```\n" +
        "image = ![$0]()\n" +
        "link = [$0]()\n" +
        "table = | $0 |  |\\n| --- | --- |\\n|  |  |\n" +
        "task = - [ ] $0\n" +
        "\n" +
        "=== All text\n" + // English
        "lorem = Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n" +
        "todo = TODO: $0\n" +
        "Marbles\n" +
        "JavaScript\n";

    /// <summary>
    /// Read the completions from the file (from the text the file is made with, if there is none)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Load()
    {
        try
        {
            if (!Exists)
            {
                Parse(NewFileText);
                _loadedPath = string.Empty;
                return DialogText.Success;
            }

            Parse(File.ReadAllText(FilePath, Encoding.UTF8));
            _loadedPath = FilePath;
            _loadedWriteTime = File.GetLastWriteTimeUtc(FilePath);
            return DialogText.Success;
        }
        catch (Exception e)
        {
            List.Clear();
            _loadedPath = string.Empty;
            return e.Message;
        }
    }

    /// <summary>
    /// Read the file again if it has changed since it was read (or another file is used)
    /// </summary>
    internal static void Refresh()
    {
        try
        {
            if (_loadedPath == FilePath && Exists && File.GetLastWriteTimeUtc(FilePath) == _loadedWriteTime) return;
            if (_loadedPath.Length == 0 && !Exists && List.Count > 0) return;
        }
        catch
        {
            // Read it again below
        }

        Load();
    }

    internal static void Parse(string text)
    {
        List.Clear();
        var syntax = SyntaxType.None;
        var inList = false;
        foreach (var raw in TextBuffer.Normalize(text).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(TitleMark, StringComparison.Ordinal))
            {
                inList = true;
                syntax = Snippets.Tag(line[TitleMark.Length..]);
                continue;
            }

            if (!inList || line.Length == 0) continue;
            var equals = line.IndexOf('=');
            // "word = text", or a word that is completed to itself ("=" first: a word starting with "=")
            var word = equals > 0 ? line[..equals].Trim() : line;
            var completion = equals > 0 ? Unescape(line[(equals + 1)..].Trim()) : line;
            if (word.Length == 0 || completion.Length == 0) continue;
            List.Add(new Item(word, completion, syntax));
        }
    }

    // "\n" is a new line, "\t" a tab and "\\" a "\"
    private static string Unescape(string text)
    {
        if (!text.Contains('\\')) return text;
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                switch (text[i + 1])
                {
                    case 'n':
                        result.Append('\n');
                        i++;
                        continue;
                    case 't':
                        result.Append('\t');
                        i++;
                        continue;
                    case '\\':
                        result.Append('\\');
                        i++;
                        continue;
                }
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Make the file, with the lists it starts with
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Create()
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory != null) Directory.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(NewFileText));
            return Load();
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Make the file if there is none
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string EnsureFile()
    {
        return Exists ? DialogText.Success : Create();
    }

    /// <summary>
    /// The completions of a word typed: those whose word starts with it (upper and lower case alike), the ones for
    /// the kind of text first, then those for all text. A completion that is only the word typed is left out
    /// </summary>
    internal static List<Item> Find(string typed, SyntaxType syntax)
    {
        var found = new List<Item>();
        if (typed.Length < MinWordLength) return found;
        if (syntax == SyntaxType.Macro)
        {
            // The steps of a macro (MacroLanguage), before the lists of the file
            foreach (var (word, text) in MacroLanguage.Words())
            {
                if (!word.StartsWith(typed, StringComparison.OrdinalIgnoreCase) ||
                    word.Equals(typed, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(new Item(word, text, SyntaxType.Macro));
                if (found.Count == MaxSuggestions) return found;
            }
        }

        foreach (var pass in new[] { true, false })
        {
            foreach (var item in List)
            {
                if (pass ? item.Syntax != syntax || syntax == SyntaxType.None : item.Syntax != SyntaxType.None) continue;
                if (!item.Word.StartsWith(typed, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.Text == typed) continue;
                if (found.Any(other => other.Text == item.Text)) continue;
                found.Add(item);
                if (found.Count == MaxSuggestions) return found;
            }
        }

        return found;
    }

    // The characters of a word, and those that can start one ("<div", "&nbsp", "@media", ".class", "#id", "</p")
    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.';
    private static bool IsStartChar(char c) => c is '<' or '/' or '!' or '&' or '@' or '#' or ':' or '?' or '$';

    /// <summary>
    /// The words that end at a column of a line, the longest first ("console.lo", then "lo"): where each starts.
    /// None if the column is inside a word
    /// </summary>
    internal static List<int> WordStarts(string line, int column)
    {
        var starts = new List<int>();
        if (column <= 0 || column > line.Length) return starts;
        if (column < line.Length && IsWordChar(line[column])) return starts;
        var start = column;
        while (start > 0 && IsWordChar(line[start - 1])) start--;
        var first = start;
        while (first > 0 && start - first < 2 && IsStartChar(line[first - 1])) first--;
        for (var i = first; i < column; i++)
        {
            // A word starts at the start, after a character that is not a letter or digit, or at one
            if (i == first || !char.IsLetterOrDigit(line[i - 1]) || !char.IsLetterOrDigit(line[i]))
            {
                if (column - i >= MinWordLength && char.IsLetterOrDigit(line[column - 1]) | IsStartChar(line[i]))
                {
                    starts.Add(i);
                }
            }
        }

        return starts;
    }
}
