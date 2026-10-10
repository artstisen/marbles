using System.Globalization;
using System.Reflection;
using System.Text;

namespace Marbles;

/// <summary>
/// The language of the texts shown. A text is written in English in the code and looked up with T (or F for a
/// text with {0} placeholders). The other languages are files built into the program (Marbles/Lang/xx.txt), where
/// each text is given in English and translated:
///   en: "File deleted"
///   da: "Fil slettet"
/// A text that is not in the file of the language is shown in English. Commands, keys, the names of the editor
/// modes, the settings file and the log are always English.
/// </summary>
internal static class Lang
{
    internal const string English = "en";

    /// <summary>
    /// The languages, in the order they are offered: code and name (in the language itself)
    /// </summary>
    internal static readonly (string Code, string Name)[] Languages =
    {
        (English, "English"),
        ("da", "Dansk"),
        ("de", "Deutsch"),
        ("nb", "Norsk bokmål"),
        ("sv", "Svenska"),
        ("es", "Español"),
        ("fr", "Français")
    };

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
    internal static string CurrentName => Languages.First(language => language.Code == Current).Name;

    private static (string Code, string Name)[]? _available;

    /// <summary>
    /// The languages that can be used: English, and those with a language file
    /// </summary>
    internal static (string Code, string Name)[] Available =>
        _available ??= Languages.Where(language => language.Code == English || ReadFile(language.Code) != null).ToArray();

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
            return code == English;
        }

        var content = ReadFile(code);
        if (content == null)
        {
            Use(English, new Dictionary<string, string>(StringComparer.Ordinal));
            return false;
        }

        Use(code, Parse(content, code));
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
    /// The language file built into the program, or null if there is none
    /// </summary>
    internal static string? ReadFile(string code)
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
