namespace Marbles;

/// <summary>
/// Spell checking in the editor: the commands ":spell" and ":nospell", the misspelled words to mark, jumping
/// between them ("]s" and "[s"), suggestions ("z=") and adding a word to the word list ("zg").
/// The checking itself is done by the SpellChecker class.
/// </summary>
internal sealed partial class EditorEngine
{
    internal const int MaxSuggestions = 9;
    private const int MaxCachedLines = 5000;
    private static readonly List<(int Start, int End)> NoWords = new();

    private SpellChecker? _spell;

    // The misspelled words of the lines checked so far (by the text of the line)
    private readonly Dictionary<string, List<(int Start, int End)>> _misspelled = new(StringComparer.Ordinal);

    /// <summary>
    /// Are misspelled words marked?
    /// </summary>
    internal bool IsSpellOn => _spell != null;

    /// <summary>
    /// The languages used for spell checking (empty when it is off)
    /// </summary>
    internal string SpellLanguages => _spell?.LanguageNames ?? string.Empty;

    // The command ":spell": check the text with the languages given (like "da" or "da,en") or the ones
    // used the last time
    private void TurnSpellOn(string languages)
    {
        if (SingleLine) return;
        var before = SpellChecker.Languages;
        var result = SpellChecker.TurnOn(languages);
        if (result != DialogText.Success)
        {
            SetError(result);
            return;
        }

        _spell = SpellChecker.Current;
        _misspelled.Clear();
        var count = 0;
        for (var line = 0; line < Buffer.LineCount; line++)
        {
            count += CheckLine(line).Count;
        }

        Message = DialogText.SpellOn + SpellLanguages + " - " + Lang.F("{0} misspelled", count);
        // The languages are kept in the settings
        if (SpellChecker.Languages != before) _request = EditorRequest.SaveSettings;
    }

    private void TurnSpellOff()
    {
        _spell = null;
        _misspelled.Clear();
        Message = DialogText.SpellTurnedOff;
    }

    private List<(int Start, int End)> CheckLine(int line)
    {
        var text = Buffer[line];
        if (_misspelled.TryGetValue(text, out var words)) return words;
        if (_misspelled.Count >= MaxCachedLines) _misspelled.Clear();
        words = _spell!.FindMisspelled(text);
        _misspelled[text] = words;
        return words;
    }

    /// <summary>
    /// The misspelled words of a line (start and end char index) when spell checking is on
    /// </summary>
    internal List<(int Start, int End)> Misspelled(int line)
    {
        if (_spell == null) return NoWords;
        var words = CheckLine(line);
        // A word is not marked while it is being written
        if (Mode == EditorMode.Insert && line == Cursor.Line && words.Count > 0)
        {
            return words.Where(word => Cursor.Col < word.Start || Cursor.Col > word.End).ToList();
        }

        return words;
    }

    private bool RequireSpell()
    {
        if (_spell == null) SetError(DialogText.SpellOff);
        return _spell != null;
    }

    // "]s" and "[s": move the cursor to the next or previous misspelled word (continuing from the other end
    // of the text when the end is reached)
    private void JumpToMisspelled(bool forward, int count)
    {
        if (!RequireSpell()) return;
        var position = Cursor;
        var found = false;
        for (; count > 0; count--)
        {
            var next = FindMisspelled(position, forward);
            if (next == null) break;
            position = next.Value;
            found = true;
        }

        if (!found)
        {
            Message = DialogText.SpellNoMore;
            return;
        }

        Cursor = position;
        EnsureCursorVisible();
    }

    private TextPos? FindMisspelled(TextPos from, bool forward)
    {
        var lineCount = Buffer.LineCount;
        // The line of the cursor is looked at twice: first the part after the cursor, at last the part before
        for (var i = 0; i <= lineCount; i++)
        {
            var line = ((from.Line + (forward ? i : -i)) % lineCount + lineCount) % lineCount;
            var words = CheckLine(line);
            if (forward)
            {
                foreach (var word in words)
                {
                    if (i == 0 ? word.Start > from.Col : i < lineCount || word.Start <= from.Col)
                    {
                        return new TextPos(line, word.Start);
                    }
                }
            }
            else
            {
                for (var w = words.Count - 1; w >= 0; w--)
                {
                    if (i == 0 ? words[w].Start < from.Col : i < lineCount || words[w].Start >= from.Col)
                    {
                        return new TextPos(line, words[w].Start);
                    }
                }
            }
        }

        return null;
    }

    private (int Start, int End)? SpellWordAtCursor()
    {
        return _spell?.WordAt(CurrentLine, Cursor.Col);
    }

    /// <summary>
    /// The word at the cursor when spell checking is on (or empty)
    /// </summary>
    internal string SpellWord()
    {
        var word = SpellWordAtCursor();
        return word == null ? string.Empty : CurrentLine.Substring(word.Value.Start, word.Value.End - word.Value.Start);
    }

    /// <summary>
    /// Suggestions for the spelling of the word at the cursor: the best first
    /// </summary>
    internal List<string> SpellSuggestions()
    {
        var word = SpellWord();
        return word.Length == 0 ? new List<string>() : _spell!.Suggest(word, MaxSuggestions);
    }

    // "z=": ask the host to show suggestions for the word at the cursor
    private void RequestSuggestions()
    {
        if (!RequireSpell()) return;
        if (SpellWord().Length == 0)
        {
            SetError(DialogText.SpellNoWord);
            return;
        }

        _request = EditorRequest.SpellSuggest;
    }

    /// <summary>
    /// Replace the word at the cursor (as one change that can be undone)
    /// </summary>
    internal void ReplaceSpellWord(string replacement)
    {
        var word = SpellWordAtCursor();
        if (word == null || replacement.Length == 0) return;
        _stepCursor = Cursor;
        var from = new TextPos(Cursor.Line, word.Value.Start);
        Delete(from, new TextPos(Cursor.Line, word.Value.End));
        Insert(from, replacement);
        Buffer.EndStep(from);
        Cursor = from;
        ClampCursor();
        EnsureCursorVisible();
    }

    /// <summary>
    /// "zg": add the word at the cursor to the word list of the user, so it is no longer marked
    /// </summary>
    internal void AddSpellWord()
    {
        if (IsPlayingMacro)
        {
            SetError(Lang.T("Words are not added to the word list by a macro"));
            return;
        }

        if (!RequireSpell()) return;
        var word = SpellWord();
        if (word.Length == 0)
        {
            SetError(DialogText.SpellNoWord);
            return;
        }

        var result = _spell!.AddWord(word);
        if (result != DialogText.Success)
        {
            SetError(result);
            return;
        }

        _misspelled.Clear();
        Message = DialogText.SpellWordAdded + word;
    }
}
