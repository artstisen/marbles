using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

/// <summary>
/// The macros of the editor: what is done in the editor is recorded (":record"), saved as a file in the folder
/// "macros" of the data directory, and played again (":play name", ":macro" to pick one).
///
/// A macro is a text file of lines that can be read and edited by hand, one step on each line:
///   # a comment
///   insert "text"            (also: append, insert at line start, append at line end, new line below/above)
///   down 3                   (left, right, up, down, word left, word right, word end: with a count or not)
///   line start, line end, first character, top, bottom, go to line 12
///   delete character, delete word, delete line, delete to line end, copy line, paste, paste before, join lines
///   undo, redo, find "text", find back "text", find next, find previous
///   command s/old/new/g      (a command of the editor, as typed after ":")
///   play other 2             (another macro, twice)
///   keys ciw&lt;Esc&gt;            (keys as typed, for what has no name: &lt;Esc&gt; &lt;CR&gt; &lt;BS&gt; &lt;Tab&gt; &lt;C-r&gt; &lt;lt&gt; ...)
/// In a text in quotes "\n" is a new line, "\t" a tab, "\"" a quote and "\\" a "\".
///
/// A macro works on the text being edited only: what would save, close or leave it (":w", ":q", Ctrl+O ...) stops
/// the macro with a message. The language is English in all languages (as the commands of the editor).
///
/// MacroLanguage and MacroFiles do not touch the console (the recording and playing are in EditorEngine.Macros);
/// MacroBoxes shows the boxes of the editor (TextArea) for them.
/// </summary>
internal static class MacroLanguage
{
    #region Keys written as text

    private static readonly (string Name, ConsoleKeyInfo Key)[] NamedKeys =
    {
        ("Esc", new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false)),
        ("CR", new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)),
        ("BS", new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false)),
        ("Tab", new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false)),
        ("S-Tab", new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false)),
        ("Del", new ConsoleKeyInfo('\0', ConsoleKey.Delete, false, false, false)),
        ("Left", new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, false)),
        ("Right", new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, false)),
        ("Up", new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false)),
        ("Down", new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false)),
        ("Home", new ConsoleKeyInfo('\0', ConsoleKey.Home, false, false, false)),
        ("End", new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false)),
        ("PgUp", new ConsoleKeyInfo('\0', ConsoleKey.PageUp, false, false, false)),
        ("PgDn", new ConsoleKeyInfo('\0', ConsoleKey.PageDown, false, false, false)),
        ("Space", new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false)),
        ("C-Left", new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, false, false, true)),
        ("C-Right", new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, false, false, true)),
        ("lt", new ConsoleKeyInfo('<', 0, false, false, false))
    };

    // A key typed as a character: with the key of the letter or digit, as the console gives it
    private static ConsoleKeyInfo CharKey(char c)
    {
        var key = c switch
        {
            >= 'a' and <= 'z' => ConsoleKey.A + (c - 'a'),
            >= 'A' and <= 'Z' => ConsoleKey.A + (c - 'A'),
            >= '0' and <= '9' => ConsoleKey.D0 + (c - '0'),
            ' ' => ConsoleKey.Spacebar,
            _ => (ConsoleKey)0
        };
        return new ConsoleKeyInfo(c, key, c is >= 'A' and <= 'Z', false, false);
    }

    /// <summary>
    /// The keys of a text written as in Vim documentation: "ciw", "&lt;Esc&gt;", "&lt;C-r&gt;", "&lt;lt&gt;" for "&lt;"
    /// </summary>
    internal static List<ConsoleKeyInfo> ParseKeys(string text)
    {
        var keys = new List<ConsoleKeyInfo>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                var end = text.IndexOf('>', i);
                var name = end > 0 ? text.Substring(i + 1, end - i - 1) : string.Empty;
                var named = NamedKeys.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (named.Name != null)
                {
                    keys.Add(named.Key);
                    i = end;
                    continue;
                }

                if (name.Length == 3 && name.StartsWith("C-", StringComparison.OrdinalIgnoreCase) &&
                    char.IsAsciiLetter(name[2]))
                {
                    // CTRL + letter
                    var letter = char.ToUpperInvariant(name[2]);
                    keys.Add(new ConsoleKeyInfo((char)(letter - 'A' + 1), ConsoleKey.A + (letter - 'A'), false, false,
                        true));
                    i = end;
                    continue;
                }
            }

            keys.Add(CharKey(text[i]));
        }

        return keys;
    }

    /// <summary>
    /// A key written as text, or null if it cannot be (a function key)
    /// </summary>
    internal static string? FormatKey(ConsoleKeyInfo key)
    {
        // A character typed is text, also with AltGr (which comes as Ctrl+Alt: €, @, ł ...)
        var isText = key.KeyChar != '\0' && !char.IsControl(key.KeyChar);
        if (!isText && (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            if (key.Key == ConsoleKey.LeftArrow) return "<C-Left>";
            if (key.Key == ConsoleKey.RightArrow) return "<C-Right>";
        }

        if (!isText && (key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key is >= ConsoleKey.A and <= ConsoleKey.Z)
        {
            return "<C-" + (char)('a' + (key.Key - ConsoleKey.A)) + ">";
        }

        switch (key.Key)
        {
            case ConsoleKey.Escape: return "<Esc>";
            case ConsoleKey.Enter: return "<CR>";
            case ConsoleKey.Backspace: return "<BS>";
            case ConsoleKey.Tab: return (key.Modifiers & ConsoleModifiers.Shift) != 0 ? "<S-Tab>" : "<Tab>";
            case ConsoleKey.Delete: return "<Del>";
            case ConsoleKey.LeftArrow: return "<Left>";
            case ConsoleKey.RightArrow: return "<Right>";
            case ConsoleKey.UpArrow: return "<Up>";
            case ConsoleKey.DownArrow: return "<Down>";
            case ConsoleKey.Home: return "<Home>";
            case ConsoleKey.End: return "<End>";
            case ConsoleKey.PageUp: return "<PgUp>";
            case ConsoleKey.PageDown: return "<PgDn>";
        }

        if (key.KeyChar == '\u001b') return "<Esc>";
        if (key.KeyChar is '\r' or '\n') return "<CR>";
        if (key.KeyChar == '<') return "<lt>";
        if (key.KeyChar == ' ') return "<Space>";
        return key.KeyChar != '\0' && !char.IsControl(key.KeyChar) ? key.KeyChar.ToString() : null;
    }

    private static string FormatKeys(IEnumerable<ConsoleKeyInfo> keys) =>
        string.Concat(keys.Select(FormatKey).Where(text => text != null));

    #endregion

    #region Texts in quotes

    /// <summary>
    /// A text in quotes, with "\n" for a new line, "\t" for a tab, "\"" for a quote and "\\" for a "\"
    /// </summary>
    internal static string Quote(string text)
    {
        var result = new StringBuilder("\"");
        foreach (var c in text)
        {
            result.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => c.ToString()
            });
        }

        return result.Append('"').ToString();
    }

    // The text of a text in quotes (the whole of what is given), or null if it is not one
    private static string? Unquote(string quoted)
    {
        quoted = quoted.Trim();
        if (quoted.Length < 2 || quoted[0] != '"' || quoted[^1] != '"') return null;
        var result = new StringBuilder();
        for (var i = 1; i < quoted.Length - 1; i++)
        {
            var c = quoted[i];
            if (c == '\\' && i + 1 < quoted.Length - 1)
            {
                var next = quoted[++i];
                result.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    _ => next
                });
                continue;
            }

            if (c == '"') return null; // A quote inside must be written \"
            result.Append(c);
        }

        return result.ToString();
    }

    #endregion

    #region The steps of the language

    // Steps without a text: their name, and the keys they are (a count can be given before the keys)
    private static readonly (string Name, string Keys, bool Count)[] Simple =
    {
        ("left", "h", true), ("right", "l", true), ("up", "k", true), ("down", "j", true),
        ("word right", "w", true), ("word left", "b", true), ("word end", "e", true),
        ("line start", "0", false), ("line end", "$", false), ("first character", "^", false),
        ("top", "gg", false), ("bottom", "G", false),
        ("delete character", "x", true), ("delete word", "dw", true), ("delete line", "dd", true),
        ("delete to line end", "D", false), ("copy line", "yy", true), ("paste", "p", true),
        ("paste before", "P", true), ("join lines", "J", true), ("undo", "u", true), ("redo", "<C-r>", true),
        ("find next", "n", true), ("find previous", "N", true)
    };

    // Steps with a text: their name, and the key that starts typing the text (the text ends with Esc)
    private static readonly (string Name, string Key)[] Typing =
    {
        ("insert", "i"), ("append", "a"), ("insert at line start", "I"), ("append at line end", "A"),
        ("new line below", "o"), ("new line above", "O")
    };

    /// <summary>
    /// The words of the steps, for completing them while a macro is edited (as the completions of the editor)
    /// </summary>
    internal static IEnumerable<(string Word, string Text)> Words()
    {
        foreach (var (name, _, count) in Simple) yield return (name, count ? name + " $0" : name);
        foreach (var (name, _) in Typing) yield return (name, name + " \"$0\"");
        yield return ("go to line", "go to line $0");
        yield return ("find", "find \"$0\"");
        yield return ("find back", "find back \"$0\"");
        yield return ("command", "command $0");
        yield return ("play", "play $0");
        yield return ("keys", "keys $0");
    }

    /// <summary>
    /// The colors of a line of a macro: a comment, the name of the step, texts in quotes, numbers, and what follows
    /// "keys" and "command"
    /// </summary>
    internal static void Scan(string line, SyntaxStyle[] styles)
    {
        var start = line.Length - line.TrimStart().Length;
        if (start >= line.Length) return;
        if (line[start] == '#')
        {
            Array.Fill(styles, SyntaxStyle.Comment, start, line.Length - start);
            return;
        }

        // The name: the words up to a number, a quote, or what follows "keys", "command" and "play"
        var i = start;
        while (i < line.Length && line[i] != '"' && !char.IsAsciiDigit(line[i]))
        {
            var wordEnd = i;
            while (wordEnd < line.Length && char.IsAsciiLetter(line[wordEnd])) wordEnd++;
            if (wordEnd == i) break;
            var word = line[i..wordEnd].ToLowerInvariant();
            Array.Fill(styles, SyntaxStyle.Keyword, i, wordEnd - i);
            i = wordEnd;
            if (word is "keys" or "command" or "play")
            {
                // What follows is as typed (keys and commands), or the name of a macro
                if (i < line.Length) Array.Fill(styles, SyntaxStyle.Name, i, line.Length - i);
                return;
            }

            while (i < line.Length && line[i] == ' ') i++;
        }

        for (; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                var end = i + 1;
                while (end < line.Length && line[end] != '"') end += line[end] == '\\' ? 2 : 1;
                end = Math.Min(end, line.Length - 1);
                Array.Fill(styles, SyntaxStyle.String, i, end - i + 1);
                i = end;
            }
            else if (char.IsAsciiDigit(line[i]))
            {
                styles[i] = SyntaxStyle.Number;
            }
        }
    }

    #endregion

    #region Recorded keys to lines

    /// <summary>
    /// The lines of a macro for what was recorded: the keys of each command, from NORMAL mode back to it. Each is
    /// written as a step with a name where it has one, else as "keys"
    /// </summary>
    internal static List<string> ToLines(IEnumerable<ConsoleKeyInfo[]> commands)
    {
        var lines = new List<string>();
        foreach (var keys in commands)
        {
            var line = ToLine(keys);
            if (line == null) continue;
            // The same movement again: one line with a count ("down", "down" is "down 2")
            if (lines.Count > 0 && Merge(lines[^1], line) is { } merged)
            {
                lines[^1] = merged;
                continue;
            }

            lines.Add(line);
        }

        return lines;
    }

    // Two movements of the same kind as one, or null
    private static string? Merge(string before, string line)
    {
        static (string Name, int Count) Split(string text)
        {
            var match = Regex.Match(text, @"^([a-z ]+?)(?: (\d+))?$");
            return match.Success
                ? (match.Groups[1].Value, match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1)
                : (string.Empty, 0);
        }

        var a = Split(before);
        var b = Split(line);
        if (a.Name.Length == 0 || a.Name != b.Name) return null;
        if (a.Name is not ("left" or "right" or "up" or "down" or "word right" or "word left" or "word end")) return null;
        return a.Name + " " + (a.Count + b.Count);
    }

    // The line of one command, or null if there is nothing to keep (a command line left with Esc)
    private static string? ToLine(ConsoleKeyInfo[] keys)
    {
        var text = FormatKeys(keys);
        // Esc alone does nothing to the text (it takes away the highlight of a search)
        if (text.Length == 0 || text == "<Esc>") return null;
        // A count in front ("3j")
        var countMatch = Regex.Match(text, "^([1-9][0-9]*)(.*)$");
        var count = countMatch.Success ? int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var rest = countMatch.Success ? countMatch.Groups[2].Value : text;

        // A command line, or a search: its text and Enter (left with Esc: nothing was done)
        if (count == 0 && rest.Length > 0 && rest[0] is ':' or '/' or '?')
        {
            if (!rest.EndsWith("<CR>", StringComparison.Ordinal)) return null;
            var typed = Typed(keys.Skip(1).Take(keys.Length - 2));
            if (typed == null) return "keys " + text;
            if (rest[0] == ':')
            {
                var play = Regex.Match(typed, @"^play +(.+)$");
                return play.Success ? "play " + play.Groups[1].Value.Trim() : "command " + typed;
            }

            return (rest[0] == '/' ? "find " : "find back ") + Quote(typed);
        }

        // Typing a text: the key that starts it, the text, and Esc
        if (count == 0 && keys.Length >= 2 && Typing.FirstOrDefault(item => item.Key == FormatKey(keys[0])) is
                { Name: not null } typing && keys[^1].Key == ConsoleKey.Escape &&
            Typed(keys.Skip(1).Take(keys.Length - 2), true) is { } inserted)
        {
            return typing.Name + " " + Quote(inserted);
        }

        // A step with a name
        if (rest == "G" && count > 0) return "go to line " + count;
        foreach (var (name, simpleKeys, withCount) in Simple)
        {
            if (rest != simpleKeys || (count > 0 && !withCount)) continue;
            return count > 1 ? name + " " + count : name;
        }

        return "keys " + text;
    }

    // The text of keys typed as text (Enter and Tab are a new line and a tab where newLines is true), or null if
    // they are not only that
    private static string? Typed(IEnumerable<ConsoleKeyInfo> keys, bool newLines = false)
    {
        var text = new StringBuilder();
        foreach (var key in keys)
        {
            if (newLines && key.Key == ConsoleKey.Enter) text.Append('\n');
            else if (newLines && key.Key == ConsoleKey.Tab && key.Modifiers == 0) text.Append('\t');
            else if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar)) text.Append(key.KeyChar); // (also AltGr)
            else return null;
        }

        return text.ToString();
    }

    #endregion

    #region Lines to keys

    /// <summary>
    /// A step of a macro: the keys it is (or another macro to play), and the line it is on (from 1)
    /// </summary>
    internal sealed record Step(int Line, List<ConsoleKeyInfo> Keys, string? Play = null, int Times = 1);

    /// <summary>
    /// The steps of a macro, or the message about the first line that is not understood
    /// </summary>
    internal static List<Step> Compile(string text, out string error)
    {
        error = string.Empty;
        var steps = new List<Step>();
        var lines = TextBuffer.Normalize(text).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var step = CompileLine(line, i + 1);
            if (step == null)
            {
                error = Lang.F("Line {0} of the macro is not understood: {1}", i + 1, Tools.TruncateString(line, 40, true));
                return new List<Step>();
            }

            steps.Add(step);
        }

        return steps;
    }

    private static Step? CompileLine(string line, int number)
    {
        // keys ...: as typed
        if (line.StartsWith("keys ", StringComparison.OrdinalIgnoreCase))
        {
            return new Step(number, ParseKeys(line[5..].TrimStart()));
        }

        // command ...: a command line
        if (line.StartsWith("command ", StringComparison.OrdinalIgnoreCase))
        {
            // (one that does not fit on the command line would be cut short, and another command given)
            if (line[8..].Trim().Length > EditorEngine.MaxCommandLength) return null;
            var keys = new List<ConsoleKeyInfo> { CharKey(':') };
            keys.AddRange(line[8..].Trim().Select(CharKey));
            keys.Add(NamedKeys[1].Key);
            return new Step(number, keys);
        }

        // play name [times]
        var play = Regex.Match(line, @"^play +(.+?)(?: +(\d+))?$", RegexOptions.IgnoreCase);
        if (play.Success)
        {
            var times = play.Groups[2].Success ? int.Parse(play.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
            return new Step(number, new List<ConsoleKeyInfo>(), play.Groups[1].Value.Trim(), Math.Clamp(times, 1, 1000));
        }

        // find "text", find back "text"
        var find = Regex.Match(line, "^find( back)? +(\".*\")$", RegexOptions.IgnoreCase);
        if (find.Success && Unquote(find.Groups[2].Value) is { } search && search.Length <= EditorEngine.MaxCommandLength)
        {
            var keys = new List<ConsoleKeyInfo> { CharKey(find.Groups[1].Success ? '?' : '/') };
            keys.AddRange(search.Select(CharKey));
            keys.Add(NamedKeys[1].Key);
            return new Step(number, keys);
        }

        // go to line N
        var goTo = Regex.Match(line, @"^go to line +(\d+)$", RegexOptions.IgnoreCase);
        if (goTo.Success) return new Step(number, ParseKeys(goTo.Groups[1].Value + "G"));

        // insert "text" ...: the longest name first ("insert at line start" before "insert")
        foreach (var (name, key) in Typing.OrderByDescending(item => item.Name.Length))
        {
            if (!line.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)) continue;
            if (Unquote(line[name.Length..]) is not { } inserted) return null;
            var keys = ParseKeys(key);
            foreach (var c in inserted)
            {
                keys.Add(c switch
                {
                    '\n' => NamedKeys[1].Key,
                    '\t' => NamedKeys[3].Key,
                    _ => CharKey(c)
                });
            }

            keys.Add(NamedKeys[0].Key);
            return new Step(number, keys);
        }

        // A step with a name, and a count or not
        var simple = Regex.Match(line, @"^([a-z ]+?)(?: +(\d+))?$", RegexOptions.IgnoreCase);
        if (!simple.Success) return null;
        var stepName = simple.Groups[1].Value.Trim().ToLowerInvariant();
        foreach (var (name, simpleKeys, withCount) in Simple)
        {
            if (name != stepName) continue;
            if (simple.Groups[2].Success && !withCount) return null;
            var count = simple.Groups[2].Success ? simple.Groups[2].Value : string.Empty;
            return new Step(number, ParseKeys(count + simpleKeys));
        }

        return null;
    }

    #endregion
}

/// <summary>
/// The files of the macros: one file for each macro ("name.macro") in the folder "macros" of the data directory
/// </summary>
internal static class MacroFiles
{
    internal const string FolderName = "macros";
    internal const string Extension = ".macro";

    /// <summary>
    /// The folder of the macros. It can be set to another folder (for testing)
    /// </summary>
    internal static string? FolderOverride { get; set; }

    internal static string Folder => FolderOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FolderName);

    internal static string PathOf(string name) => Path.Combine(Folder, name + Extension);

    /// <summary>
    /// A name that can be used for a macro: a name of a file, without blank space at its ends
    /// </summary>
    internal static bool IsValidName(string name)
    {
        return NameProblem(name) == null;
    }

    /// <summary>
    /// Why a name cannot be used for a macro, or null if it can
    /// </summary>
    internal static string? NameProblem(string name)
    {
        if (name.Length == 0 || name != name.Trim() || name.Length > 60) return DialogText.InvalidFileName;
        // ":play copy 2" plays "copy" twice
        if (Regex.IsMatch(name, @"\s\d+$")) return Lang.T("A name of a macro cannot end with a number");
        return Tools.FileNameProblem(name);
    }

    /// <summary>
    /// The names of the macros, in the order of the alphabet
    /// </summary>
    internal static List<string> Names()
    {
        try
        {
            return Directory.Exists(Folder)
                ? Directory.GetFiles(Folder, "*" + Extension).Select(Path.GetFileNameWithoutExtension).OfType<string>()
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// The text of a macro, or null if there is none with the name
    /// </summary>
    internal static string? Load(string name)
    {
        try
        {
            var path = PathOf(name);
            return IsValidName(name) && File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static bool Exists(string name) => IsValidName(name) && File.Exists(PathOf(name));

    /// <summary>
    /// Save the text of a macro as it was edited (with a new line at its end)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string SaveText(string name, string text)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            text = TextBuffer.Normalize(text).TrimEnd('\n') + "\n";
            FileIo.WriteAllBytesSafe(PathOf(name), new UTF8Encoding(false).GetBytes(text));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Save a macro: a comment at the top, and its lines
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save(string name, IEnumerable<string> lines)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var text = new StringBuilder();
            // English: the file is a macro, in the language of macros
            text.Append("# Macro: ").Append(name).Append('\n');
            text.Append("# Recorded ").Append(IsoDateTime.GetDateTimeNow()).Append(". One step on each line; see the guide to the editor\n");
            foreach (var line in lines) text.Append(line).Append('\n');
            FileIo.WriteAllBytesSafe(PathOf(name), new UTF8Encoding(false).GetBytes(text.ToString()));
            return DialogText.Success;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return e.Message;
        }
    }
}

/// <summary>
/// The boxes of the macros in the editor: recording starts (with a warning in a note of a data file), the question
/// when it stops (where the steps can be edited before they are saved), the list of macros (":macro"), and the box
/// a macro is edited in. A macro is written to its file only when it is saved, as before
/// </summary>
internal static class MacroBoxes
{
    /// <summary>
    /// ":record": start recording, after a warning in a note of a data file (what is typed is saved unencrypted)
    /// </summary>
    internal static void StartRecording(EditorEngine engine, bool inNote)
    {
        if (engine.IsRecording)
        {
            engine.ShowMessage(Lang.T("A macro is being recorded already"), false);
            return;
        }

        if (inNote && !ChoiceBox.Confirm(Lang.T("Record a macro"),
                Lang.T("The text you type is saved unencrypted in the file of the macro."),
                Lang.T("Record a macro of this note anyway?"), true))
        {
            return;
        }

        engine.StartRecording();
        engine.ShowMessage(Lang.T("Recording a macro: Esc in NORMAL mode stops it"), false);
    }

    /// <summary>
    /// The Esc that would close the editor, while recording: go on, stop and save, stop and edit the steps before
    /// saving them, or stop and discard
    /// </summary>
    /// <param name="engine">The editor that is recording</param>
    /// <param name="inNote">The editor has a note of a data file (yanks in the box of the macro follow the setting
    /// for notes)</param>
    /// <param name="drawBehind">Draws the editor behind the boxes again</param>
    internal static void Stop(EditorEngine engine, bool inNote, Action drawBehind)
    {
        while (true)
        {
            var choice = ChoiceBox.Pick(Lang.T("Record a macro"), Lang.T("Stop recording the macro?"), string.Empty,
                new[]
                {
                    Lang.T("Resume recording"), Lang.T("Stop and save"), Lang.T("Stop and edit"),
                    Lang.T("Stop and discard")
                },
                new[] { "R", "S", "E", "D" }, 0, false);
            if (choice == 3)
            {
                engine.StopRecording();
                engine.ShowMessage(Lang.T("The macro is discarded"), false);
                return;
            }

            if (choice == 2)
            {
                // The steps in the box first: saved (and named) from there, or back to this question
                if (EditRecorded(engine, inNote, drawBehind)) return;
                drawBehind();
                continue;
            }

            if (choice != 1) return; // Recording goes on
            // Esc: recording goes on (nothing is lost)
            var name = AskName();
            if (name == null) return;
            var status = MacroFiles.Save(name, engine.StopRecording());
            engine.ShowMessage(status == DialogText.Success ? Lang.F("Macro saved: {0} (:play {0})", name) : status,
                status != DialogText.Success);
            return;
        }
    }

    // The name of a macro to save, asked until it is one that can be used (one that is there is replaced after
    // asking), or null (Esc)
    private static string? AskName()
    {
        while (true)
        {
            var name = ChoiceBox.AskText(Lang.T("Save the macro"), DialogText.TypeNameAndPressEnter,
                DialogText.NameColon, string.Empty);
            if (name == null) return null;
            name = name.Trim();
            if (MacroFiles.NameProblem(name) is { } problem)
            {
                ChoiceBox.Message(Lang.T("Save the macro"), problem, name);
                continue;
            }

            if (MacroFiles.Exists(name) && !ChoiceBox.Confirm(Lang.T("Save the macro"),
                    Lang.F("There is a macro named {0}. Replace it?", name), string.Empty, true))
            {
                continue;
            }

            return name;
        }
    }

    // "Stop and edit": the steps recorded so far in the box, to be changed before the macro is saved. The first save
    // asks for the name, and stops recording (later ones save under that name); closing without saving goes back
    // to the question (still recording). Returns true when the macro was saved. Nothing is written before it is
    // saved, as with "Stop and save"
    private static bool EditRecorded(EditorEngine engine, bool inNote, Action drawBehind)
    {
        string? saved = null;
        EditInBox(() => saved == null ? Lang.T("New macro (named when saved)") : Lang.F("Macro: {0}", saved),
            string.Join("\n", engine.RecordedLines()), true, inNote, text =>
            {
                if (Check(text) is { } problem) return problem;
                var name = saved ?? AskName();
                if (name == null) return Lang.T("Not saved");
                var lines = TextBuffer.Normalize(text).TrimEnd('\n').Split('\n');
                var status = MacroFiles.Save(name, lines);
                if (status != DialogText.Success) return status;
                if (saved == null) engine.StopRecording();
                saved = name;
                return null;
            }, drawBehind);
        if (saved == null) return false;
        engine.ShowMessage(Lang.F("Macro saved: {0} (:play {0})", saved), false);
        return true;
    }

    /// <summary>
    /// Why the text of a macro cannot be saved (the first line that is not understood), or null when it can
    /// </summary>
    internal static string? Check(string text)
    {
        MacroLanguage.Compile(text, out var error);
        return error.Length > 0 ? error : null;
    }

    /// <summary>
    /// A macro in the box of the macros, to change with the keys of the editor: ":w" saves it (when every line is
    /// understood), Esc and ":q" close it (asking about changes that are not saved)
    /// </summary>
    /// <param name="label">What is edited (the name of the macro), above the text: asked for each time the box is
    /// drawn, as a macro is named when it is saved</param>
    /// <param name="text">The text of the macro</param>
    /// <param name="modified">The text can be saved without changing it (steps recorded)</param>
    /// <param name="inNote">Opened from a note of a data file: yanks follow the setting for notes</param>
    /// <param name="save">Saves the text: null when saved, or why not</param>
    /// <param name="drawBehind">Draws the editor behind the box again</param>
    private static void EditInBox(Func<string> label, string text, bool modified, bool inNote,
        Func<string, string?> save, Action drawBehind)
    {
        // (on the editor alone: the box of the list or the question is gone)
        drawBehind();
        var (left, top, width, height) = DrawEditBox(label);
        // The state of the page behind (its label "Unsaved") is as it was when the box closes
        var changed = DocumentInfo.HasChanged;
        try
        {
            _ = new TextArea(left, top, width, height, text, 0, false, false, false, TextArea.SaveAsFile, modified,
                new TextArea.PeekOptions(SyntaxType.Macro, () =>
                {
                    drawBehind();
                    DrawEditBox(label);
                })
                {
                    SaveText = save,
                    FromNote = inNote,
                    Hint = Lang.T("Edit the macro: :w saves it (checked first), Esc closes"),
                    ReadOnlyText = Lang.T("Not done in the box of the macro")
                });
        }
        finally
        {
            DocumentInfo.HasChanged = changed;
        }
    }

    // The box of a macro being edited, as big as the list of the macros: what is edited at the top, and the place of
    // the text below it
    private static (int Left, int Top, int Width, int Height) DrawEditBox(Func<string> label)
    {
        using var box = ConsoleColors.BoxScope();
        ChoiceBox.Forget();
        var (top, left, inner, height) = ChoiceBox.DrawFrame(Lang.T("Edit macro"),
            Init.LineWidth - 6, Math.Max(8, Init.LineCount - 5));
        Tools.SetCursorEx(top + 2, left + 3);
        Console.Write(Tools.TruncateString(label(), inner - 4, true));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(top + 3, left + 3);
        Console.Write(new string('-', inner - 4));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // From the row below the line to the last row but one, below the label (a blank column at the right)
        return (left + 3, top + 4, inner - 3, Math.Max(1, height - 4));
    }

    /// <summary>
    /// ":macro": the macros in a list, with the text of the selected one below it; Enter plays it, E edits it
    /// </summary>
    /// <param name="engine">The editor the macro is played in</param>
    /// <param name="inNote">The editor has a note of a data file (yanks in the box follow the setting for notes)</param>
    /// <param name="drawBehind">Draws the editor behind the box again</param>
    internal static void Pick(EditorEngine engine, bool inNote, Action drawBehind)
    {
        var selected = 0;
        while (true)
        {
            var names = MacroFiles.Names();
            if (names.Count == 0)
            {
                engine.ShowMessage(Lang.T("No macros yet: :record starts recording one"), false);
                return;
            }

            var edit = -1;
            var picked = PickListBox.Show(Lang.T("Macros"), names, i => MacroFiles.Load(names[i]) ?? string.Empty,
                selected, Lang.T("j/k Select | Enter Play | E Edit | / Filter | Esc Close"), (key, i) =>
                {
                    if (key.Key != ConsoleKey.E || (key.Modifiers & ConsoleModifiers.Control) != 0) return false;
                    edit = i;
                    return true;
                });
            if (picked == PickListBox.Reload && edit >= 0)
            {
                selected = edit;
                EditMacro(engine, names[edit], inNote, drawBehind);
                drawBehind();
                continue;
            }

            if (picked < 0) return;
            // While recording: recorded as "play name"
            engine.RecordPlay(names[picked]);
            engine.PlayMacro(names[picked], 1);
            return;
        }
    }

    // E in the list of macros: the macro in the box, to change and save under its name
    private static void EditMacro(EditorEngine engine, string name, bool inNote, Action drawBehind)
    {
        ChoiceBox.Forget();
        if (engine.IsRecording)
        {
            // The keys typed in the box would not be the macro's: "Stop and edit" does it
            ChoiceBox.Message(Lang.T("Edit macro"), Lang.T("Not while recording: Esc stops it, then Stop and edit"),
                string.Empty);
            return;
        }

        // A macro that cannot be read (gone, or kept from it) is not edited as an empty text, which ":w" would save
        // in its place
        if (MacroFiles.Load(name) is not { } macro)
        {
            ChoiceBox.Message(Lang.T("Edit macro"), Lang.T("The macro could not be read"), name);
            return;
        }

        EditInBox(() => Lang.F("Macro: {0}", name), macro, false, inNote,
            text => Check(text) ?? (MacroFiles.SaveText(name, text) is var status &&
                                    status != DialogText.Success ? status : null), drawBehind);
    }
}