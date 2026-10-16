using System.Globalization;
using System.Reflection;
using System.Text;

namespace Marbles;

/// <summary>
/// The language of the texts shown. A text is written in English in the code and looked up with T (or F for a
/// text with {0} placeholders). The other languages are language files, where each text is given in English and
/// translated:
///   en: "File deleted"
///   da: "Fil slettet"
/// A text that is not in the file of the language is shown in English. Commands, keys, the names of the editor
/// modes, the settings file and the log are always English.
///
/// Danish and German are built into the program (Marbles/Lang/xx.txt). The other languages are files in a folder
/// "lang": next to the program (published with it, from Marbles/Lang/External), or in the data directory. A file
/// there can also replace a built-in one (to change a translation), and a new language is added by putting its file
/// there: it is found by its name, "pl.txt", and named by its first line ("# Marbles: Polski (pl)"). A file in the
/// data directory is used before one next to the program, which is used before the one built in.
/// </summary>
internal static class Lang
{
    internal const string English = "en";

    /// <summary>
    /// The languages built into the program, in the order they are offered: code and name (in the language itself)
    /// </summary>
    internal static readonly (string Code, string Name)[] Languages =
    {
        (English, "English"),
        ("da", "Dansk"),
        ("de", "Deutsch")
    };

    /// <summary>
    /// The name of the folders with language files (next to the program, and in the data directory)
    /// </summary>
    internal const string FolderName = "lang";

    /// <summary>
    /// Where a language comes from
    /// </summary>
    internal enum Source
    {
        BuiltIn,
        ProgramFolder, // "lang" next to the program
        DataFolder // "lang" in the data directory
    }

    /// <summary>
    /// A language that can be used: its code, its name (in the language itself), where its file is, and how much of
    /// it is translated (null for a built-in file, which is complete). Replaced: a file in a folder is used instead
    /// of the one built in
    /// </summary>
    internal sealed record Language(string Code, string Name, Source From, int? Percent = null, bool Replaced = false);

    /// <summary>
    /// The folders with language files, the one used first first: "lang" in the data directory, and next to the
    /// program. They can be set to others (for testing)
    /// </summary>
    internal static string[]? FoldersOverride { get; set; }

    private static IEnumerable<(string Path, Source From)> Folders()
    {
        if (FoldersOverride != null)
        {
            return FoldersOverride.Select((path, i) => (path, i == 0 ? Source.DataFolder : Source.ProgramFolder));
        }

        return new[]
        {
            (Path.Combine(FileIo.GetDataDirectoryName(), FolderName), Source.DataFolder),
            (Path.Combine(AppContext.BaseDirectory, FolderName), Source.ProgramFolder)
        };
    }

    // A file name that is a language code: "pl", "fi", "pt-br"
    private static readonly System.Text.RegularExpressions.Regex CodePattern =
        new(@"^[a-z]{2,3}(-[a-z0-9]{2,8})?$");

    /// <summary>
    /// The code of the language that was set (in the settings) but could not be used, as its file was not found.
    /// English is used, and the setting is kept (it is written to the settings file again), so the language comes
    /// back when its file is there again. Null when the language set is used
    /// </summary>
    internal static string? Missing { get; set; }

    // Texts looked up by a name made while the program runs, listed here for the language files: the colors
    // (ColorPicker), and the months and short names of the days (IsoDateTime)
    internal static readonly string[] Names =
    {
        "Black", "Dark Blue", "Dark Green", "Dark Cyan", "Dark Red", "Dark Magenta", "Dark Yellow", "Gray",
        "Dark Gray", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "White",
        "January", "February", "March", "April", "May", "June", "July", "August", "September", "October",
        "November", "December",
        "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"
    };

    private static Dictionary<string, string> _texts = new(StringComparer.Ordinal);

    /// <summary>
    /// The code of the language in use ("en", "da" ...)
    /// </summary>
    internal static string Current { get; private set; } = English;

    /// <summary>
    /// The name of the language in use, in the language itself
    /// </summary>
    internal static string CurrentName => Available.FirstOrDefault(language => language.Code == Current)?.Name ?? Current;

    private static Language[]? _available;

    /// <summary>
    /// The languages that can be used: English, the built-in ones, and those with a file in a folder "lang" (in the
    /// order of their names). They are found once; Refresh finds them again
    /// </summary>
    internal static Language[] Available => _available ??= Discover();

    /// <summary>
    /// Find the languages again (a file may have been added to a folder "lang" since)
    /// </summary>
    internal static void Refresh()
    {
        _available = null;
        _reference = null;
    }

    private static Language[] Discover()
    {
        var found = new List<Language>();
        foreach (var (code, name) in Languages)
        {
            if (code == English)
            {
                found.Add(new Language(code, name, Source.BuiltIn));
                continue;
            }

            // A file in a folder replaces the one built in
            var file = FindFile(code);
            found.Add(file == null
                ? new Language(code, name, Source.BuiltIn)
                : new Language(code, name, file.Value.From, PercentOf(ReadText(file.Value.Path), code), true));
        }

        var added = new List<Language>();
        foreach (var (folder, from) in Folders())
        {
            string[] files;
            try
            {
                if (!Directory.Exists(folder)) continue;
                files = Directory.GetFiles(folder, "*.txt");
            }
            catch
            {
                continue;
            }

            foreach (var path in files)
            {
                var code = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!CodePattern.IsMatch(code) || code == English) continue;
                if (found.Exists(language => language.Code == code) || added.Exists(language => language.Code == code))
                {
                    continue;
                }

                var content = ReadText(path);
                if (content == null) continue;
                var percent = PercentOf(content, code);
                // A file without translations is not a language
                if (Parse(content, code).Count == 0) continue;
                added.Add(new Language(code, NameIn(content, code), from, percent));
            }
        }

        found.AddRange(added.OrderBy(language => language.Name, StringComparer.CurrentCultureIgnoreCase));
        return found.ToArray();
    }

    // The file of a language in a folder "lang" (the data directory first), or null
    private static (string Path, Source From)? FindFile(string code)
    {
        foreach (var (folder, from) in Folders())
        {
            var path = Path.Combine(folder, code + ".txt");
            try
            {
                if (File.Exists(path)) return (path, from);
                // The code is in lowercase, the name of the file can be in other letters ("PL.txt", "pt-BR.txt"):
                // where file names are case-sensitive (Linux) it is found by comparing the names
                if (!Directory.Exists(folder)) continue;
                var named = Directory.EnumerateFiles(folder, "*.txt").FirstOrDefault(file =>
                    string.Equals(Path.GetFileNameWithoutExtension(file), code, StringComparison.OrdinalIgnoreCase));
                if (named != null) return (named, from);
            }
            catch
            {
                // A folder that cannot be read has no files
            }
        }

        return null;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch
        {
            return null;
        }
    }

    // The name of a language, from the first line of its file ("# Marbles: Polski (pl)" or "# name: Polski"), or
    // its code
    private static string NameIn(string content, string code)
    {
        foreach (var raw in content.Split('\n').Take(10))
        {
            var line = raw.TrimEnd('\r');
            var made = System.Text.RegularExpressions.Regex.Match(line, @"^#\s*Marbles:\s*(.+?)\s*\(" +
                System.Text.RegularExpressions.Regex.Escape(code) + @"\)\s*$");
            if (made.Success) return made.Groups[1].Value;
            var named = System.Text.RegularExpressions.Regex.Match(line, @"^#\s*name:\s*(.+?)\s*$");
            if (named.Success) return named.Groups[1].Value;
        }

        return code;
    }

    // The texts of this version of Marbles: those of the built-in Danish file, which has them all
    private static HashSet<string>? _reference;

    // How much of the texts of this version is translated in a file (0 to 100)
    private static int PercentOf(string? content, string code)
    {
        if (content == null) return 0;
        _reference ??= new HashSet<string>(EnglishTexts(ReadBuiltIn("da") ?? string.Empty), StringComparer.Ordinal);
        if (_reference.Count == 0) return 100;
        var texts = Parse(content, code);
        var translated = _reference.Count(texts.ContainsKey);
        return translated * 100 / _reference.Count;
    }

    // All the English texts of a language file (also those not translated)
    private static IEnumerable<string> EnglishTexts(string content)
    {
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!line.StartsWith(English + ":", StringComparison.Ordinal)) continue;
            if (Unquote(line[(English.Length + 1)..]) is { } text) yield return text;
        }
    }

    /// <summary>
    /// The code of the language after (or before) the one in use
    /// </summary>
    internal static string Next(bool forward)
    {
        var index = Array.FindIndex(Available, language => language.Code == Current);
        var count = Available.Length;
        return Available[(index + (forward ? 1 : count - 1)) % count].Code;
    }

    /// <summary>
    /// The text in the language in use
    /// </summary>
    /// <param name="english">The text in English</param>
    internal static string T(string english)
    {
        return _texts.TryGetValue(english, out var text) ? text : english;
    }

    /// <summary>
    /// A text with placeholders ({0}, {1} ...) in the language in use, filled in
    /// </summary>
    /// <param name="english">The text in English</param>
    /// <param name="values">What goes in the placeholders</param>
    internal static string F(string english, params object[] values)
    {
        try
        {
            return string.Format(CultureInfo.InvariantCulture, T(english), values);
        }
        catch (FormatException)
        {
            // A translation with broken placeholders: show the English text
            return string.Format(CultureInfo.InvariantCulture, english, values);
        }
    }

    /// <summary>
    /// Use a language. An unknown code (or one without a file) gives English
    /// </summary>
    /// <returns>True if the language is used</returns>
    internal static bool Set(string code)
    {
        code = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (code == English || Available.All(language => language.Code != code))
        {
            Use(English, new Dictionary<string, string>(StringComparer.Ordinal));
            if (code == English) Missing = null;
            return code == English;
        }

        var content = ReadFile(code);
        if (content == null)
        {
            Use(English, new Dictionary<string, string>(StringComparer.Ordinal));
            return false;
        }

        Use(code, Parse(content, code));
        Missing = null;
        return true;
    }

    private static void Use(string code, Dictionary<string, string> texts)
    {
        Current = code;
        _texts = texts;
        // Texts kept by the pages are made again in the language
        Tools.ClearEditorHelpData();
    }

    /// <summary>
    /// The language file to use: one in a folder "lang" (the data directory first), else the one built into the
    /// program. Null if there is none
    /// </summary>
    internal static string? ReadFile(string code)
    {
        if (FindFile(code) is { } file && ReadText(file.Path) is { } text) return text;
        return ReadBuiltIn(code);
    }

    /// <summary>
    /// The language file built into the program, or null if there is none
    /// </summary>
    internal static string? ReadBuiltIn(string code)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Marbles.Lang." + code + ".txt");
        if (stream == null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Read a language file: pairs of lines, the English text ("en:") followed by the translation ("da:").
    /// The texts are in quotes, and "\n" in them is a line break. Lines starting with "#" are comments
    /// </summary>
    internal static Dictionary<string, string> Parse(string content, string code)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        string? english = null;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(English + ":", StringComparison.Ordinal))
            {
                english = Unquote(line[(English.Length + 1)..]);
            }
            else if (english != null && line.StartsWith(code + ":", StringComparison.Ordinal))
            {
                var text = Unquote(line[(code.Length + 1)..]);
                if (text != null && text.Length > 0) texts[english] = text;
                english = null;
            }
        }

        return texts;
    }

    // The text between the first and the last quote of a value, with "\n" as a line break (null if not quoted)
    private static string? Unquote(string value)
    {
        var first = value.IndexOf('"');
        var last = value.LastIndexOf('"');
        if (first < 0 || last <= first) return null;
        return value.Substring(first + 1, last - first - 1).Replace("\\n", "\n");
    }

    /// <summary>
    /// A text as it is written in a language file (in quotes, a line break as "\n")
    /// </summary>
    internal static string Quote(string text)
    {
        return "\"" + text.Replace("\n", "\\n") + "\"";
    }
}
