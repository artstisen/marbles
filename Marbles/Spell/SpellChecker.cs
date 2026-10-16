using System.Text;

namespace Marbles;

/// <summary>
/// Spell checking with one or more languages. Each language is a Vim/Neovim spell file
/// (like "da.utf-8.spl") in the directory "spell", next to the data directory. Words added by the user are
/// kept in a text file with one word per line (like "da.utf-8.add", as used by Neovim).
///
/// The class finds the words of a line of text, tells which are not in the dictionaries, gives suggestions
/// and adds words. It knows nothing about the editor or the console.
/// </summary>
internal sealed class SpellChecker
{
    /// <summary>
    /// The directory with the spell files: "spell" next to the data directory. It can be set to another (for testing)
    /// </summary>
    internal static string DirectoryName
    {
        get => _directoryName ?? Path.Combine(FileIo.DataParentDirectory, "spell");
        set => _directoryName = value;
    }

    private static string? _directoryName;

    private const string FileNameEnd = ".utf-8.spl";
    private const string AddFileNameEnd = ".utf-8.add";
    private const string DefaultMidWordChars = "";
    private const int MinWordLength = 2; // Single letters are not checked

    #region The active spell checker

    /// <summary>
    /// The spell checker in use, or null when spell checking is off
    /// </summary>
    internal static SpellChecker? Current { get; private set; }

    /// <summary>
    /// The languages used when spell checking is turned on, separated by commas (setting "spelllang")
    /// </summary>
    internal static string Languages { get; set; } = DefaultLanguages;

    internal const string DefaultLanguages = "en";

    /// <summary>
    /// The languages there is a spell file for in the directory of the spell files (in alphabetical order)
    /// </summary>
    internal static List<string> AvailableLanguages()
    {
        try
        {
            if (!Directory.Exists(DirectoryName)) return new List<string>();
            return Directory.GetFiles(DirectoryName, "*" + FileNameEnd)
                .Select(file => Path.GetFileName(file)[..^FileNameEnd.Length].ToLowerInvariant())
                // Not the files of added words ("en.utf-8.add.spl")
                .Where(name => name.Length > 0 && !name.Contains('.') && Tools.IsValidFileName(name))
                .Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// The languages in use with one of them turned on or off, in the order of the ones there is a spell file
    /// for. The last language in use cannot be turned off
    /// </summary>
    internal static string ToggleLanguage(IReadOnlyList<string> available, string languages, string language)
    {
        var used = languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => name.ToLowerInvariant()).Where(available.Contains).ToList();
        if (used.Contains(language))
        {
            if (used.Count > 1) used.Remove(language);
        }
        else
        {
            used.Add(language);
        }

        return string.Join(",", available.Where(used.Contains));
    }

    /// <summary>
    /// Turn spell checking on
    /// </summary>
    /// <param name="languages">The languages to use, or empty to use the setting</param>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string TurnOn(string languages)
    {
        if (languages.Trim().Length == 0) languages = Languages;
        var checker = Load(languages, DirectoryName, out var error);
        if (checker == null) return error;
        Current = checker;
        Languages = checker.LanguageNames;
        return DialogText.Success;
    }

    internal static void TurnOff()
    {
        Current = null;
    }

    #endregion

    private readonly List<SpellDictionary> _dictionaries = new();
    private readonly HashSet<string> _addedWords = new(StringComparer.Ordinal);
    private readonly HashSet<string> _wrongWords = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _checked = new(StringComparer.Ordinal); // Words checked before
    private string _midWordChars = DefaultMidWordChars;
    private string _addFilePath = string.Empty;

    /// <summary>
    /// The languages of the spell checker, separated by commas
    /// </summary>
    internal string LanguageNames { get; private set; } = string.Empty;

    private SpellChecker()
    {
    }

    /// <summary>
    /// Create a spell checker for one or more languages
    /// </summary>
    /// <param name="languages">The names of the languages, separated by commas ("en" or "da,en")</param>
    /// <param name="directory">The directory with the spell files</param>
    /// <param name="error">Why the spell checker could not be created</param>
    internal static SpellChecker? Load(string languages, string directory, out string error)
    {
        error = string.Empty;
        var checker = new SpellChecker();
        var names = new List<string>();
        foreach (var part in languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = part.ToLowerInvariant();
            if (names.Contains(name)) continue;
            if (!Tools.IsValidFileName(name))
            {
                error = DialogText.SpellFileNotFound(name);
                return null;
            }

            var path = Path.Combine(directory, name + FileNameEnd);
            if (!File.Exists(path))
            {
                error = DialogText.SpellFileNotFound(Path.Combine(directory, name + FileNameEnd));
                return null;
            }

            try
            {
                checker._dictionaries.Add(SpellDictionary.Load(path));
            }
            catch (Exception e)
            {
                error = name + FileNameEnd + ": " + e.Message;
                return null;
            }

            names.Add(name);
            checker.ReadAddedWords(Path.Combine(directory, name + AddFileNameEnd));
        }

        if (names.Count == 0)
        {
            error = DialogText.SpellNoLanguage;
            return null;
        }

        // Words are added to the list of the first language
        checker._addFilePath = Path.Combine(directory, names[0] + AddFileNameEnd);
        checker.LanguageNames = string.Join(",", names);
        var midWordChars = string.Concat(checker._dictionaries.Select(d => d.MidWordChars)).Distinct().ToArray();
        if (midWordChars.Length > 0) checker._midWordChars = new string(midWordChars);
        return checker;
    }

    #region Words added by the user

    // The file has one word per line. A word followed by "/!" is a wrong word, lines starting with "#" are
    // comments, and other things after a "/" are ignored
    private void ReadAddedWords(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var slash = line.IndexOf('/');
                // As the words checked: with the plain apostrophe
                var word = Normalize(slash < 0 ? line : line.Substring(0, slash));
                if (word.Length == 0) continue;
                if (slash >= 0 && line.Substring(slash).Contains('!'))
                {
                    _wrongWords.Add(word);
                }
                else
                {
                    _addedWords.Add(word);
                }
            }
        }
        catch
        {
            // A word list that cannot be read is not used
        }
    }

    /// <summary>
    /// Add a word to the word list of the user
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal string AddWord(string word)
    {
        // Kept with the plain apostrophe, as the words checked ("O’Brien" is "O'Brien")
        word = Normalize(word.Trim());
        if (word.Length == 0 || word.Any(char.IsWhiteSpace) || word.Contains('/')) return DialogText.SpellNoWord;
        if (_addedWords.Contains(word)) return DialogText.Success;
        try
        {
            // Make sure the word starts on a line of its own
            var needsNewLine = false;
            if (File.Exists(_addFilePath))
            {
                var content = File.ReadAllBytes(_addFilePath);
                needsNewLine = content.Length > 0 && content[^1] != (byte)'\n';
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_addFilePath))!);
            }

            File.AppendAllText(_addFilePath, (needsNewLine ? "\n" : string.Empty) + word + "\n",
                new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            return e.Message;
        }

        _addedWords.Add(word);
        _checked.Clear();
        return DialogText.Success;
    }

    // A word of the list written in lowercase is also good when capitalized or in capital letters
    private bool IsAddedWord(string word)
    {
        if (_addedWords.Contains(word)) return true;
        var lower = word.ToLowerInvariant();
        if (lower == word || !_addedWords.Contains(lower)) return false;
        return word == word.ToUpperInvariant() ||
               word == char.ToUpperInvariant(lower[0]) + lower.Substring(1);
    }

    #endregion

    #region Checking

    /// <summary>
    /// True if a word is spelled correctly in one of the languages
    /// </summary>
    internal bool IsCorrect(string word)
    {
        word = Normalize(word);
        if (_checked.TryGetValue(word, out var known)) return known;
        var correct = CheckWord(word);
        if (_checked.Count > 50000) _checked.Clear();
        _checked[word] = correct;
        return correct;
    }

    private bool CheckWord(string word)
    {
        if (_wrongWords.Contains(word)) return false;
        if (IsAddedWord(word)) return true;
        var correct = false;
        foreach (var dictionary in _dictionaries)
        {
            var result = dictionary.Check(word);
            if (result == null) return false; // Marked as wrong in a dictionary
            if (result == true) correct = true;
        }

        return correct;
    }

    // The typographic apostrophe is the same as the plain one
    private static string Normalize(string word)
    {
        return word.Contains('’') ? word.Replace('’', '\'') : word;
    }

    private bool IsMidWordChar(char c)
    {
        return _midWordChars.Contains(c) || (c == '’' && _midWordChars.Contains('\''));
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetter(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark;
    }

    /// <summary>
    /// The words of a line: the start and end (not included) of each word that is to be checked.
    /// Not included are single letters, words next to digits or underscores (like "x86" and "my_name"), and
    /// words in web addresses, e-mail addresses and file paths.
    /// </summary>
    internal List<(int Start, int End)> FindWords(string line)
    {
        return FindTokens(line).Where(token => token.Check).Select(token => (token.Start, token.End)).ToList();
    }

    // Can a word end at a position of a line?
    private bool IsWordEnd(string line, int pos)
    {
        if (pos >= line.Length) return true;
        if (IsWordChar(line[pos]) || char.IsDigit(line[pos])) return false;
        return !(IsMidWordChar(line[pos]) && pos + 1 < line.Length && IsWordChar(line[pos + 1]));
    }

    // All the words of a line, with a mark on the ones that are to be checked
    private List<(int Start, int End, bool Check)> FindTokens(string line)
    {
        var words = new List<(int, int, bool)>();
        var lastChunk = (-1, -1, false);
        var i = 0;
        while (i < line.Length)
        {
            if (!IsWordChar(line[i]))
            {
                i++;
                continue;
            }

            // A word: letters, and characters like "'" when they are between letters
            var start = i;
            while (i < line.Length &&
                   (IsWordChar(line[i]) ||
                    (IsMidWordChar(line[i]) && i + 1 < line.Length && IsWordChar(line[i + 1]))))
            {
                i++;
            }

            var end = i;
            var check = end - start >= MinWordLength &&
                        !(start > 0 && (char.IsDigit(line[start - 1]) || line[start - 1] == '_')) &&
                        !(end < line.Length && (char.IsDigit(line[end]) || line[end] == '_')) &&
                        !IsInAddress(line, start, end, ref lastChunk);
            words.Add((start, end, check));
        }

        return words;
    }

    // Is the word part of something that looks like a web address, an e-mail address or a file path? The part
    // of the line between blank spaces is kept in "last": the next words in it are not looked at again (a line
    // with a long address of many words would else take a time growing with the square of its length)
    private static bool IsInAddress(string line, int start, int end, ref (int Start, int End, bool Address) last)
    {
        if (start >= last.Start && end <= last.End) return last.Address && !(last.Start == start && last.End == end);
        var chunkStart = start;
        while (chunkStart > 0 && !char.IsWhiteSpace(line[chunkStart - 1])) chunkStart--;
        var chunkEnd = end;
        while (chunkEnd < line.Length && !char.IsWhiteSpace(line[chunkEnd])) chunkEnd++;
        var chunk = line.AsSpan(chunkStart, chunkEnd - chunkStart);
        var address = chunk.Contains("://".AsSpan(), StringComparison.Ordinal) || chunk.Contains('@') ||
                      chunk.Contains('\\') || chunk.StartsWith("www.".AsSpan(), StringComparison.OrdinalIgnoreCase);
        last = (chunkStart, chunkEnd, address);
        // A word that is all of the part is a word
        return address && chunkEnd - chunkStart != end - start;
    }

    /// <summary>
    /// The misspelled words of a line: the start and end (not included) of each
    /// </summary>
    internal List<(int Start, int End)> FindMisspelled(string line)
    {
        var misspelled = new List<(int, int)>();
        var normalized = Normalize(line);
        var skipTo = 0;
        foreach (var (start, end, check) in FindTokens(line))
        {
            if (start < skipTo) continue;
            if (end < line.Length && !char.IsWhiteSpace(line[end]))
            {
                // The word can be the start of a longer one in a dictionary, like "f.eks."
                var longer = 0;
                foreach (var dictionary in _dictionaries)
                {
                    longer = Math.Max(longer,
                        dictionary.MatchBeyond(normalized, start, end, pos => IsWordEnd(normalized, pos)));
                }

                if (longer > end)
                {
                    skipTo = longer;
                    continue;
                }
            }

            if (check && !IsCorrect(line.Substring(start, end - start))) misspelled.Add((start, end));
        }

        return misspelled;
    }

    /// <summary>
    /// The word at (or just before) a position in a line, or null if there is none
    /// </summary>
    internal (int Start, int End)? WordAt(string line, int col)
    {
        foreach (var (start, end) in FindWords(line))
        {
            if (col >= start && col < end) return (start, end);
        }

        return null;
    }

    #endregion

    #region Suggestions

    /// <summary>
    /// Suggest spellings for a word: the best first
    /// </summary>
    internal List<string> Suggest(string word, int maxCount)
    {
        word = Normalize(word);
        var scores = new Dictionary<string, int>(StringComparer.Ordinal);
        var lower = word.ToLowerInvariant();
        var isAllCap = word.Length > 1 && word == word.ToUpperInvariant() && word != lower;
        var isOneCap = !isAllCap && char.IsUpper(word[0]);
        var maxEdits = word.Length <= 3 ? 1 : 2;

        void Add(string suggestion, int score)
        {
            // Capitalize the suggestion like the word
            if (isAllCap)
            {
                suggestion = suggestion.ToUpperInvariant();
            }
            else if (isOneCap && char.IsLower(suggestion[0]))
            {
                suggestion = char.ToUpperInvariant(suggestion[0]) + suggestion.Substring(1);
            }

            if (suggestion == word) return;
            var suggestionLower = suggestion.ToLowerInvariant();
            // Words starting with the same letter are more likely
            if (suggestionLower[0] != lower[0]) score += 5;
            score += Math.Abs(suggestion.Length - word.Length);
            // Names and abbreviations are less likely than plain words
            var plain = isAllCap ? suggestion
                : isOneCap ? char.ToUpperInvariant(suggestionLower[0]) + suggestionLower.Substring(1)
                : suggestionLower;
            if (suggestion != plain) score += 8;
            // Common mistakes: a letter that is to be doubled (or not), and letters in the wrong order
            if (WithoutDoubles(suggestionLower) == WithoutDoubles(lower) ||
                WithoutAccents(suggestionLower) == WithoutAccents(lower))
            {
                score -= 4;
            }
            else if (suggestionLower.Length == lower.Length &&
                     suggestionLower.Order().SequenceEqual(lower.Order()))
            {
                score -= 3;
            }

            if (!scores.TryGetValue(suggestion, out var best) || score < best) scores[suggestion] = score;
        }

        foreach (var dictionary in _dictionaries)
        {
            dictionary.Suggest(word, maxEdits, Add);
        }

        // The words added by the user that are close
        foreach (var added in _addedWords)
        {
            var distance = EditDistance(added.ToLowerInvariant(), lower, maxEdits);
            if (distance <= maxEdits) Add(added, distance * SpellDictionary.ScorePerEdit);
        }

        return scores.OrderBy(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(maxCount).Select(pair => pair.Key).ToList();
    }

    // A word with letters that are repeated removed ("spelling" -> "speling")
    private static string WithoutDoubles(string word)
    {
        var result = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            if (result.Length == 0 || result[^1] != c) result.Append(c);
        }

        return result.ToString();
    }

    // A word with the letters that are often written without their accent replaced ("prøve" -> "prove")
    private static string WithoutAccents(string word)
    {
        const string accented = "øöóòôåäáàâæéèêëüúùûíìîïñç";
        const string plain = "oooooaaaaaaeeeeuuuuiiiinc";
        var result = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            var index = accented.IndexOf(c);
            result.Append(index < 0 ? c : plain[index]);
        }

        return result.ToString();
    }

    // The number of letters to add, remove or replace to get from one word to another (up to a maximum)
    private static int EditDistance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    #endregion
}
