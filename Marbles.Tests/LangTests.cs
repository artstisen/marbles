using System.Text.RegularExpressions;

namespace Marbles.Tests;

/// <summary>
/// The languages: the lookup of texts, and the language files compared with the texts of the code
/// </summary>
[Collection("Static state")]
public class LangTests : IDisposable
{
    public void Dispose()
    {
        Lang.Set(Lang.English);
    }

    #region The texts of the code

    private static string SourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Marbles.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "Marbles");
    }

    private static IEnumerable<string> SourceFiles()
    {
        var root = SourceDirectory();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                           !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
    }

    private static readonly Regex Call = new(@"\bLang\.[TF]\(\s*((?:""(?:[^""\\]|\\.)*""\s*(?:\+\s*)?)+)");
    private static readonly Regex Literal = new(@"""((?:[^""\\]|\\.)*)""");

    private static string Unescape(string text)
    {
        return Regex.Replace(text, @"\\(.)", m => m.Groups[1].Value switch
        {
            "n" => "\n",
            "t" => "\t",
            "r" => "\r",
            var other => other
        });
    }

    // All the English texts: what is given to Lang.T and Lang.F in the code, and the names made while running
    private static HashSet<string> SourceTexts()
    {
        var texts = new HashSet<string>(Lang.Names, StringComparer.Ordinal);
        foreach (var path in SourceFiles())
        {
            if (Path.GetFileName(path) == "Lang.cs") continue;
            foreach (Match call in Call.Matches(File.ReadAllText(path)))
            {
                texts.Add(string.Concat(Literal.Matches(call.Groups[1].Value).Select(l => Unescape(l.Groups[1].Value))));
            }
        }

        return texts;
    }

    // The pairs of a language file, in order
    private static List<(string English, string Text)> FilePairs(string code)
    {
        var content = Lang.ReadFile(code);
        Assert.NotNull(content);
        var pairs = new List<(string, string)>();
        string? english = null;
        foreach (var raw in content!.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("en:", StringComparison.Ordinal))
            {
                Assert.True(english == null, code + ": an English text without a translation line before: " + line);
                english = Value(line[3..]);
            }
            else if (line.StartsWith(code + ":", StringComparison.Ordinal))
            {
                Assert.True(english != null, code + ": a translation without an English text: " + line);
                pairs.Add((english!, Value(line[(code.Length + 1)..])));
                english = null;
            }
            else
            {
                Assert.True(line.Length == 0 || line.StartsWith('#'), code + ": a line that is not understood: " + line);
            }
        }

        return pairs;
    }

    private static string Value(string value)
    {
        value = value.Trim();
        Assert.True(value.Length >= 2 && value[0] == '"' && value[^1] == '"', "A text must be in quotes: " + value);
        return value[1..^1].Replace("\\n", "\n");
    }

    private static readonly string[] Translated = Lang.Languages.Select(l => l.Code)
        .Where(code => code != Lang.English && Lang.ReadFile(code) != null).ToArray();

    public static IEnumerable<object[]> LanguageFiles() => Translated.Select(code => new object[] { code });

    #endregion

    [Fact]
    public void TheDanishFile_IsThere()
    {
        Assert.Contains("da", Translated);
    }

    [Theory]
    [MemberData(nameof(LanguageFiles))]
    public void LanguageFile_HasTheTextsOfTheCode(string code)
    {
        var source = SourceTexts();
        var pairs = FilePairs(code);
        var english = pairs.Select(pair => pair.English).ToList();
        Assert.Equal(english.Count, english.Distinct(StringComparer.Ordinal).Count());
        // A text that is not in the code any more (or written differently)
        var unknown = english.Where(text => !source.Contains(text)).ToList();
        Assert.True(unknown.Count == 0, code + ": not texts of the code (python tools/langfile.py " + code + " brings the file up to date): " +
                                        string.Join(" | ", unknown));
        // Every text of the code is in the file
        var missing = source.Where(text => !english.Contains(text)).ToList();
        Assert.True(missing.Count == 0, code + ": texts not in the file (python tools/langfile.py " + code + "): " + string.Join(" | ", missing));
    }

    [Theory]
    [MemberData(nameof(LanguageFiles))]
    public void Translations_KeepPlaceholdersKeysAndBlankSpace(string code)
    {
        var problems = new List<string>();
        foreach (var (english, text) in FilePairs(code))
        {
            if (text.Length == 0) continue;
            string Placeholders(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Order());
            if (Placeholders(english) != Placeholders(text)) problems.Add("placeholders: " + english);
            // Blank space at the start and end is part of the layout
            if (english.Length - english.TrimStart().Length != text.Length - text.TrimStart().Length ||
                english.Length - english.TrimEnd().Length != text.Length - text.TrimEnd().Length)
            {
                problems.Add("blank space: " + english);
            }

            // Key hints: each part starts with the same key (a part without a description is a label, and "/text"
            // is something typed)
            if (english.Contains(" | "))
            {
                var englishParts = english.Split(" | ");
                var parts = text.Split(" | ");
                static string Key(string part) => part.Split(' ')[0];
                if (englishParts.Length != parts.Length ||
                    englishParts.Zip(parts).Any(p => p.First.Contains(' ') && !Key(p.First).StartsWith('/') &&
                                                     Key(p.First) != Key(p.Second)))
                {
                    problems.Add("keys: " + english);
                }
            }

            // Lines broken by hand stay within the width of the screen
            if (english.Contains('\n') && text.Split('\n').Any(line => line.Length > 79)) problems.Add("too wide: " + english);
            // Brackets with a key in front of a choice, like "[Y] Yes" or "[Esc] Cancel"
            string Keys(string s) => string.Join(",", Regex.Matches(s, @"\[(?:[A-Z]|Esc|Enter|Tab)\]").Select(m => m.Value));
            if (Keys(english) != Keys(text)) problems.Add("keys in brackets: " + english);
        }

        Assert.True(problems.Count == 0, code + ": " + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void TheDanishFile_IsComplete()
    {
        var missing = FilePairs("da").Where(pair => pair.Text.Length == 0).Select(pair => pair.English).ToList();
        Assert.True(missing.Count == 0, "Not translated: " + string.Join(" | ", missing));
    }

    [Fact]
    public void T_AndF_UseTheLanguage_AndFallBackToEnglish()
    {
        Assert.Equal("File deleted", Lang.T("File deleted"));
        Assert.True(Lang.Set("da"));
        Assert.Equal("da", Lang.Current);
        Assert.Equal("Dansk", Lang.CurrentName);
        Assert.NotEqual("File deleted", Lang.T("File deleted"));
        Assert.NotEqual("File deleted", DialogText.FileDeleted);
        // Not in the file: English
        Assert.Equal("Not a text of Marbles", Lang.T("Not a text of Marbles"));
        Assert.Contains("3", Lang.F("{0} fewer lines", 3));
        // The commands and the names of the modes do not change
        Assert.Equal("-- NORMAL --", DialogText.EditorModeNormal);
        Assert.Equal("COMMAND:", DialogText.CommandPrompt);
        // An unknown language: English
        Assert.False(Lang.Set("xx"));
        Assert.Equal(Lang.English, Lang.Current);
        Assert.Equal("File deleted", DialogText.FileDeleted);
        Assert.Equal("da", Lang.Next(true));
        Assert.Equal(Lang.Available[^1].Code, Lang.Next(false));
    }

    [Fact]
    public void Parse_ReadsPairs_AndSkipsEmptyTranslations()
    {
        var texts = Lang.Parse("# comment\nen: \"Save\"\nda: \"Gem\"\nen: \"Two\\nlines\"\nda: \"To\\nlinjer\"\n" +
                               "en: \"Empty\"\nda: \"\"\nen: \" Spaces \"\nda: \" Mellemrum \"\n", "da");
        Assert.Equal("Gem", texts["Save"]);
        Assert.Equal("To\nlinjer", texts["Two\nlines"]);
        Assert.False(texts.ContainsKey("Empty"));
        Assert.Equal(" Mellemrum ", texts[" Spaces "]);
    }

    [Fact]
    public void F_WithABrokenTranslation_ShowsTheEnglishText()
    {
        Lang.Set("da");
        // A format that is not in the file is used as it is
        Assert.Equal("a 1 b", Lang.F("a {0} b", 1));
    }

    // The text given to Lang.T or Lang.F is written in the call, where the language files are made from (a
    // name made while running is listed in Lang.Names, and the line before says so)
    [Fact]
    public void TheTextsLookedUp_AreWrittenInTheCall()
    {
        var problems = new List<string>();
        foreach (var path in SourceFiles())
        {
            if (Path.GetFileName(path) == "Lang.cs") continue;
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"\bLang\.[TF]\(\s*[^""\s]")) continue;
                if (lines[i].Contains("Lang.Names") || i > 0 && lines[i - 1].Contains("Lang.Names")) continue;
                problems.Add(Path.GetFileName(path) + ":" + (i + 1) + ": " + lines[i].Trim());
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // Text written to the screen is given to Lang.T (or Lang.F). A line that is meant to be the same in all
    // languages is marked with a comment containing "English"
    [Fact]
    public void TextOnTheScreen_IsTranslated()
    {
        var patterns = new[]
        {
            @"Console\.Write(Line)?\(\s*""[^""]*[A-Za-z]{3,}",
            @"\b(Message|_message) = ""[A-Za-z]",
            @"SetError\(""",
            @"StatusMessage\(""",
            @"WriteModeLabel\(""[A-Za-z]",
            @"WriteModeLabel\([^,]+,\s*""[A-Za-z]",
            @"new Shortcut\((?:""[^""]*""|[^,""]+),\s*""",
            @"WriteMenuItem\(""[^""]*"",\s*""[A-Za-z]",
            @"YesNoPrompt\([^,]+,\s*""[A-Za-z]"
        }.Select(p => new Regex(p)).ToArray();
        var problems = new List<string>();
        foreach (var path in SourceFiles())
        {
            var name = Path.GetFileName(path);
            if (name is "SoftwareLicense.cs" or "Lang.cs") continue;
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//") || line.Contains("English")) continue;
                var code = Regex.Replace(line, @"Lang\.[TF]\(""(?:[^""\\]|\\.)*""", "Lang.T(x");
                if (patterns.Any(p => p.IsMatch(code))) problems.Add(name + ":" + (i + 1) + ": " + line.Trim());
            }
        }

        Assert.True(problems.Count == 0, "Text that is not translated:" + Environment.NewLine +
                                         string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void ChoiceBox_WrapsTextBetweenWords()
    {
        Assert.Equal(new[] { "one two", "three four", "five" }, ChoiceBox.Wrap("one two three four five", 10));
        Assert.Equal(new[] { "short" }, ChoiceBox.Wrap("short", 10));
        Assert.Equal(new[] { "" }, ChoiceBox.Wrap("", 10));
        Assert.All(ChoiceBox.Wrap("abcdefghijklmnop qr", 8), line => Assert.True(line.Length <= 8));
    }
}
