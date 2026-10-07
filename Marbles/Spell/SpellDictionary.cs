using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

/// <summary>
/// A spelling dictionary read from a Vim/Neovim spell file (.spl, UTF-8, file format version 50).
///
/// The file holds the words as two trees of bytes: one with the words in lowercase ("fold case"), where
/// each word tells how it may be capitalized, and one with the words that must be written exactly as they
/// are ("keep case"). Languages that join words (like Danish) also have rules for which words may be
/// combined into compound words.
///
/// The reading and the checking of words follow the description of the format and the function
/// find_word() in the Vim source (spellfile.c and spell.c). Not supported: postponed prefixes, regions
/// (words of all regions are accepted), syllable counting and the NOBREAK option.
/// </summary>
internal sealed class SpellDictionary
{
    #region File format constants

    private const string FileId = "VIMspell";
    private const int FileVersion = 50;

    // Sections
    private const int SectionMidWord = 2;
    private const int SectionRep = 4;
    private const int SectionCompound = 8;
    private const int SectionEnd = 255;

    // Bytes with a special meaning in the trees
    private const int ByteNoFlags = 0; // End of word without flags
    private const int ByteIndex = 1; // The child node is shared: its index follows
    private const int ByteFlags = 2; // End of word, flags follow
    private const int ByteFlags2 = 3; // End of word, two bytes of flags follow
    private const int SharedMask = 0x8000000;

    // Flags of a word
    private const int FlagRegion = 0x01; // A region byte follows
    private const int FlagOneCap = 0x02; // The word must start with a capital letter
    private const int FlagAllCap = 0x04; // The word must be in capital letters
    private const int FlagBanned = 0x10; // The word is wrong, also when it is found by other means
    private const int FlagAffix = 0x20; // An affix ID (used as the compound flag of the word) follows
    private const int FlagFixCap = 0x40; // Keep-case word: capital letters are not allowed
    private const int FlagKeepCap = 0x80; // Keep-case word
    private const int FlagNeedCompound = 0x0200; // The word is only valid as part of a compound word
    private const int FlagNoSuggest = 0x0400; // The word is not to be suggested
    private const int FlagCompoundRoot = 0x0800; // Already a compound word: counts as two
    private const int FlagNoCompoundBefore = 0x1000; // No word may be joined in front of the word
    private const int FlagNoCompoundAfter = 0x2000; // No word may be joined after the word

    private const int MaxWordLength = 254;

    #endregion

    /// <summary>
    /// A tree of words. Each node is a list of siblings sorted by byte: "Bytes" holds the number of siblings
    /// followed by their bytes, and "Indexes" the index of the child node of each sibling. A sibling with
    /// the byte 0 is the end of a word, where "Indexes" holds the flags of the word.
    /// </summary>
    private sealed class Tree
    {
        internal byte[] Bytes = Array.Empty<byte>();
        internal int[] Indexes = Array.Empty<int>();
        internal bool IsEmpty => Bytes.Length == 0;
    }

    private enum CapType
    {
        Lower, // word
        OneCap, // Word
        AllCap, // WORD
        KeepCap // wOrd
    }

    private Tree _foldTree = new();
    private Tree _keepTree = new();

    // Compound words
    private Regex? _compoundRules; // What sequences of compound flags make a valid compound word
    private readonly HashSet<int> _compoundStartFlags = new(); // The flags a compound word can start with
    private readonly HashSet<int> _compoundAllFlags = new();
    private int _compoundMax = MaxWordLength; // The maximum number of words in a compound word
    private int _compoundMinLength; // The minimum length of each word in a compound word

    private readonly List<(string From, string To)> _replacements = new(); // Common mistakes (for suggestions)

    /// <summary>
    /// The characters that are part of a word when they are between letters (like the apostrophe in "don't")
    /// </summary>
    internal string MidWordChars { get; private set; } = string.Empty;

    private SpellDictionary()
    {
    }

    #region Reading the file

    private byte[] _data = Array.Empty<byte>();
    private int _pos;

    /// <summary>
    /// Read a spell file
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not a spell file that can be used</exception>
    internal static SpellDictionary Load(string path)
    {
        return Read(File.ReadAllBytes(path));
    }

    internal static SpellDictionary Read(byte[] data)
    {
        var dictionary = new SpellDictionary { _data = data };
        try
        {
            dictionary.ReadFile();
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException(DialogText.SpellFileInvalid);
        }

        dictionary._data = Array.Empty<byte>();
        return dictionary;
    }

    private int ReadByte() => _data[_pos++];

    private int ReadNumber(int byteCount)
    {
        var number = 0;
        while (byteCount-- > 0)
        {
            number = (number << 8) | _data[_pos++];
        }

        return number;
    }

    private void ReadFile()
    {
        if (_data.Length < 10 || Encoding.ASCII.GetString(_data, 0, FileId.Length) != FileId ||
            _data[FileId.Length] != FileVersion)
        {
            throw new InvalidDataException(DialogText.SpellFileInvalid);
        }

        _pos = FileId.Length + 1;
        // Sections: <id> <flags> <length> <content>
        while (true)
        {
            var id = ReadByte();
            if (id == SectionEnd) break;
            ReadByte(); // Flags
            var length = ReadNumber(4);
            var end = _pos + length;
            switch (id)
            {
                case SectionMidWord:
                    MidWordChars = Encoding.UTF8.GetString(_data, _pos, length);
                    break;
                case SectionRep:
                    for (var count = ReadNumber(2); count > 0; count--)
                    {
                        var fromLength = ReadByte();
                        var from = Encoding.UTF8.GetString(_data, _pos, fromLength);
                        _pos += fromLength;
                        var toLength = ReadByte();
                        var to = Encoding.UTF8.GetString(_data, _pos, toLength);
                        _pos += toLength;
                        _replacements.Add((from, to));
                    }

                    break;
                case SectionCompound:
                    ReadCompoundSection(end);
                    break;
            }

            _pos = end;
        }

        _foldTree = ReadTree();
        _keepTree = ReadTree();
        // The third tree (prefixes) is not used
    }

    // <compmax> <compminlen> <compsylmax> <compoptions> <comppatcount> <comppattern> ... <compflags>
    private void ReadCompoundSection(int end)
    {
        var max = ReadByte();
        _compoundMax = max < 2 ? MaxWordLength : max;
        _compoundMinLength = ReadByte();
        // With a maximum number of syllables the number of words is only limited for words with more
        // syllables. Syllables are not counted here, so the number of words is not limited then
        if (ReadByte() != 0) _compoundMax = MaxWordLength;
        ReadNumber(2); // Options
        for (var count = ReadNumber(2); count > 0; count--)
        {
            _pos += ReadByte(); // Patterns (CHECKCOMPOUNDPATTERN)
        }

        // The rules: items separated by "/", where each item is a sequence of compound flags (single bytes)
        // that can have the wildcards "?", "*" and "+" and character classes like "[abc]"
        var pattern = new StringBuilder("^(?:");
        var atStart = 1; // 1 = at the start of an item, 2 = in a character class at the start of an item
        while (_pos < end)
        {
            var c = ReadByte();
            if (!"?*+[]/".Contains((char)c)) _compoundAllFlags.Add(c);
            if (atStart != 0)
            {
                if (c == '[')
                {
                    atStart = 2;
                }
                else if (c == ']')
                {
                    atStart = 0;
                }
                else
                {
                    _compoundStartFlags.Add(c);
                    if (atStart == 1) atStart = 0;
                }
            }

            if (c == '/')
            {
                pattern.Append('|');
                atStart = 1;
            }
            else if ("?*+[]".Contains((char)c))
            {
                pattern.Append((char)c);
            }
            else
            {
                pattern.Append(Regex.Escape(((char)c).ToString()));
            }
        }

        pattern.Append(")$");
        _compoundRules = new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
    }

    // <nodecount> <nodedata> ...
    private Tree ReadTree()
    {
        var tree = new Tree();
        var count = ReadNumber(4);
        if (count <= 0) return tree;
        tree.Bytes = new byte[count];
        tree.Indexes = new int[count];
        if (ReadNode(tree, 0) < 0) throw new InvalidDataException(DialogText.SpellFileInvalid);
        return tree;
    }

    // Read a node and the nodes below it. Returns the index following the nodes read
    private int ReadNode(Tree tree, int startIndex)
    {
        var index = startIndex;
        var length = ReadByte();
        tree.Bytes[index++] = (byte)length;
        // The siblings
        for (var i = 1; i <= length; i++)
        {
            var c = ReadByte();
            if (c <= ByteFlags2)
            {
                if (c == ByteNoFlags)
                {
                    tree.Indexes[index] = 0;
                }
                else if (c != ByteIndex)
                {
                    // End of word with flags, optionally followed by a region and an affix ID
                    var flags = ReadByte();
                    if (c == ByteFlags2) flags |= ReadByte() << 8;
                    if ((flags & FlagRegion) != 0) flags |= ReadByte() << 16;
                    if ((flags & FlagAffix) != 0) flags |= ReadByte() << 24;
                    tree.Indexes[index] = flags;
                    c = 0;
                }
                else
                {
                    // A node that was read before is used again
                    tree.Indexes[index] = ReadNumber(3) | SharedMask;
                    c = ReadByte();
                }
            }

            tree.Bytes[index++] = (byte)c;
        }

        // The children of the siblings follow, in the same order
        for (var i = 1; i <= length; i++)
        {
            if (tree.Bytes[startIndex + i] == 0) continue;
            if ((tree.Indexes[startIndex + i] & SharedMask) != 0)
            {
                tree.Indexes[startIndex + i] &= ~SharedMask;
            }
            else
            {
                tree.Indexes[startIndex + i] = index;
                index = ReadNode(tree, index);
            }
        }

        return index;
    }

    #endregion

    #region Checking a word

    /// <summary>
    /// Check the spelling of a word
    /// </summary>
    /// <returns>True if the word is good, false if it is not in the dictionary, and null if the dictionary
    /// says that the word is wrong (which overrules other dictionaries)</returns>
    internal bool? Check(string word)
    {
        if (word.Length == 0 || word.Length > MaxWordLength) return false;
        var folded = word.ToLowerInvariant();
        if (folded.Length != word.Length) return false;
        if (IsBanned(word, folded)) return null;
        var state = new MatchState();
        return MatchInTree(_foldTree, true, word, folded, 0, new List<int>(), 0, state) ||
               MatchInTree(_keepTree, false, word, folded, 0, new List<int>(), 0, state);
    }

    private bool IsBanned(string word, string folded)
    {
        var caps = GetCapType(word, 0, word.Length);
        var banned = false;
        Walk(_foldTree, Encoding.UTF8.GetBytes(folded), (end, flags) =>
        {
            if (end == folded.Length && (flags & FlagBanned) != 0 && caps != CapType.KeepCap &&
                IsValidCase(caps, flags))
            {
                banned = true;
            }

            return banned;
        });
        if (banned) return true;
        Walk(_keepTree, Encoding.UTF8.GetBytes(word), (end, flags) =>
        {
            if (end == word.Length && (flags & FlagBanned) != 0) banned = true;
            return banned;
        });
        return banned;
    }

    /// <summary>
    /// Follow a word down a tree and call a function for every place where a word of the tree ends
    /// </summary>
    /// <param name="tree">The tree</param>
    /// <param name="bytes">The word as UTF-8</param>
    /// <param name="onWordEnd">Called with the number of characters matched and the flags of the word found.
    /// Returns true to stop</param>
    private static void Walk(Tree tree, byte[] bytes, Func<int, int, bool> onWordEnd)
    {
        if (tree.IsEmpty) return;
        var node = 0;
        var chars = 0;
        for (var pos = 0;; pos++)
        {
            var count = tree.Bytes[node];
            var sibling = node + 1;
            var last = node + count;
            // The words ending here come first. Only use them where a character ends
            var atCharEnd = pos == bytes.Length || (bytes[pos] & 0xC0) != 0x80;
            while (sibling <= last && tree.Bytes[sibling] == 0)
            {
                if (atCharEnd && onWordEnd(chars, tree.Indexes[sibling])) return;
                sibling++;
            }

            if (pos == bytes.Length) return;
            // Find the sibling with the next byte of the word (the siblings are sorted)
            var b = bytes[pos];
            var low = sibling;
            var high = last;
            var found = -1;
            while (low <= high)
            {
                var middle = (low + high) / 2;
                if (tree.Bytes[middle] == b)
                {
                    found = middle;
                    break;
                }

                if (tree.Bytes[middle] < b)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            if (found < 0) return;
            node = tree.Indexes[found];
            if ((b & 0xC0) != 0x80) chars++; // A new character starts with this byte
        }
    }

    /// <summary>
    /// The state of a search for a word: the position where the last search for a following word of a
    /// compound word started. As in Vim it is not set back when that search fails, which means that of the
    /// words that can start a compound word only the longest one is tried.
    /// </summary>
    private sealed class MatchState
    {
        internal int CompoundOffset;
    }

    /// <summary>
    /// Find the part of a word from a position and to its end in a tree: as a single word, or as
    /// words joined to a compound word.
    /// </summary>
    /// <param name="tree">The tree to find the first word in</param>
    /// <param name="isFoldTree">Is it the tree with the words in lowercase?</param>
    /// <param name="word">The word as written</param>
    /// <param name="folded">The word in lowercase</param>
    /// <param name="start">The position where the part starts (0 = the whole word)</param>
    /// <param name="compoundFlags">The compound flags of the words found before the position</param>
    /// <param name="compoundExtra">The number of extra words to count for the compound word</param>
    /// <param name="state">The state of the search</param>
    private bool MatchInTree(Tree tree, bool isFoldTree, string word, string folded, int start,
        List<int> compoundFlags, int compoundExtra, MatchState state)
    {
        var text = isFoldTree ? folded : word;
        var ends = new List<(int Chars, int Flags)>();
        Walk(tree, Encoding.UTF8.GetBytes(text.Substring(start)), (chars, flags) =>
        {
            if (chars > 0) ends.Add((chars, flags));
            return false;
        });

        // The longest word first. Words of the same length are tried in the order of the file
        for (var last = ends.Count - 1; last >= 0;)
        {
            var first = last;
            while (first > 0 && ends[first - 1].Chars == ends[last].Chars) first--;
            for (var i = first; i <= last; i++)
            {
                if (MatchEnd(isFoldTree, word, folded, start, ends[i].Chars, ends[i].Flags, compoundFlags,
                        compoundExtra, state))
                {
                    return true;
                }
            }

            last = first - 1;
        }

        return false;
    }

    // Is a word found in a tree, at a position of the word to check, the end of it or the start of a compound?
    private bool MatchEnd(bool isFoldTree, string word, string folded, int start, int chars, int flags,
        List<int> compoundFlags, int compoundExtra, MatchState state)
    {
        var inCompound = start > 0;
        var isEnd = start + chars == word.Length;
        // In the fold-case tree the word tells how it can be capitalized
        var caps = GetCapType(word, start, start + chars);
        if (isFoldTree && !inCompound && (caps == CapType.KeepCap || !IsValidCase(caps, flags))) return false;
        if ((flags & FlagBanned) != 0) return false;
        if (!inCompound && isEnd)
        {
            // The whole word
            return (flags & FlagNeedCompound) == 0;
        }

        // A word that is, or will be, part of a compound word
        var compoundFlag = (flags >> 24) & 0xFF;
        if (_compoundRules == null || compoundFlag == 0) return false;
        if (start + chars - state.CompoundOffset < _compoundMinLength) return false;
        if (!isEnd && compoundFlags.Count + compoundExtra + 2 > _compoundMax) return false;
        if (inCompound && (flags & FlagNoCompoundBefore) != 0) return false;
        if (!isEnd && (flags & FlagNoCompoundAfter) != 0) return false;
        if (!(compoundFlags.Count == 0 ? _compoundStartFlags : _compoundAllFlags).Contains(compoundFlag))
        {
            return false;
        }

        if (inCompound && isFoldTree)
        {
            // The capitalization of a word joined to another: it can be in capitals (if the first is),
            // but not start with a capital letter
            if (caps == CapType.KeepCap || (caps == CapType.AllCap && (flags & FlagFixCap) != 0)) return false;
            if (caps != CapType.AllCap)
            {
                var afterWordChar = char.IsLetterOrDigit(word[start - 1]);
                if (afterWordChar ? caps == CapType.OneCap : (flags & FlagOneCap) != 0 && caps != CapType.OneCap)
                {
                    return false;
                }
            }
        }

        compoundFlags.Add(compoundFlag);
        bool result;
        if (isEnd)
        {
            // The flags of all the words must match one of the rules
            var sequence = new string(compoundFlags.Select(flag => (char)flag).ToArray());
            result = _compoundRules.IsMatch(sequence);
        }
        else
        {
            // Find the rest of the word
            var extra = compoundExtra + ((flags & FlagCompoundRoot) != 0 ? 1 : 0);
            var next = start + chars;
            state.CompoundOffset = next;
            result = MatchInTree(_foldTree, true, word, folded, next, compoundFlags, extra, state);
            if (!result)
            {
                state.CompoundOffset = next;
                result = MatchInTree(_keepTree, false, word, folded, next, compoundFlags, extra, state);
            }
        }

        compoundFlags.RemoveAt(compoundFlags.Count - 1);
        return result;
    }

    /// <summary>
    /// Find the longest word of the dictionary that starts at a position of a line, also when it has
    /// characters that are not letters: words like "f.eks." and "Jean-Luc"
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="start">The position where a word starts</param>
    /// <param name="minEnd">The end must be after this position</param>
    /// <param name="isWordEnd">Tells if a word can end at a position of the line</param>
    /// <returns>The position where the word found ends, or 0 if none is found</returns>
    internal int MatchBeyond(string line, int start, int minEnd, Func<int, bool> isWordEnd)
    {
        var length = Math.Min(line.Length - start, MaxWordLength);
        var text = line.Substring(start, length);
        var folded = text.ToLowerInvariant();
        if (folded.Length != text.Length) return 0;
        var best = 0;
        Walk(_foldTree, Encoding.UTF8.GetBytes(folded), (chars, flags) =>
        {
            var end = start + chars;
            if (end <= minEnd || end <= best || (flags & (FlagBanned | FlagNeedCompound)) != 0) return false;
            var caps = GetCapType(line, start, end);
            if (caps != CapType.KeepCap && IsValidCase(caps, flags) && isWordEnd(end)) best = end;
            return false;
        });
        Walk(_keepTree, Encoding.UTF8.GetBytes(text), (chars, flags) =>
        {
            var end = start + chars;
            if (end > minEnd && end > best && (flags & (FlagBanned | FlagNeedCompound)) == 0 && isWordEnd(end))
            {
                best = end;
            }

            return false;
        });
        return best;
    }

    // How a word is capitalized
    private static CapType GetCapType(string word, int start, int end)
    {
        // Find the first letter
        var i = start;
        while (i < end && !char.IsLetter(word[i])) i++;
        if (i >= end) return CapType.Lower;
        var firstCap = char.IsUpper(word[i]);
        var allCap = firstCap;
        var pastSecond = false;
        for (i++; i < end; i++)
        {
            if (!char.IsLetter(word[i])) continue;
            if (!char.IsUpper(word[i]))
            {
                // UUl -> keep case
                if (pastSecond && allCap) return CapType.KeepCap;
                allCap = false;
            }
            else if (!allCap)
            {
                // UlU -> keep case
                return CapType.KeepCap;
            }

            pastSecond = true;
        }

        return allCap ? CapType.AllCap : firstCap ? CapType.OneCap : CapType.Lower;
    }

    // Is a word, capitalized in a way, valid for a word of the fold-case tree with these flags?
    private static bool IsValidCase(CapType caps, int flags)
    {
        return (caps == CapType.AllCap && (flags & FlagFixCap) == 0) ||
               ((flags & (FlagAllCap | FlagKeepCap)) == 0 &&
                ((flags & FlagOneCap) == 0 || caps == CapType.OneCap));
    }

    #endregion

    #region Suggestions

    /// <summary>
    /// Find words in the dictionary that are close to a (misspelled) word
    /// </summary>
    /// <param name="word">The word</param>
    /// <param name="maxEdits">The number of changes (a letter added, removed, replaced, or two letters swapped)
    /// a word can be away</param>
    /// <param name="found">Called with a word and its score (lower is better)</param>
    internal void Suggest(string word, int maxEdits, Action<string, int> found)
    {
        if (word.Length == 0 || word.Length > 40) return;
        var folded = word.ToLowerInvariant();

        // Common mistakes of the language score better than other changes
        foreach (var (from, to) in _replacements)
        {
            for (var index = folded.IndexOf(from, StringComparison.Ordinal);
                 index >= 0;
                 index = folded.IndexOf(from, index + 1, StringComparison.Ordinal))
            {
                var candidate = folded.Substring(0, index) + to + folded.Substring(index + from.Length);
                if (Check(candidate) == true) found(candidate, ScorePerEdit / 2);
            }
        }

        SuggestFromTree(_foldTree, true, folded, maxEdits, found);
        SuggestFromTree(_keepTree, false, folded, maxEdits, found);
    }

    internal const int ScorePerEdit = 10;

    // Walk the tree, keeping to the words within a number of edits of the target. For each word walked the
    // edit distance to the target is updated one character at a time (one row of the distance table each)
    private void SuggestFromTree(Tree tree, bool isFoldTree, string target, int maxEdits,
        Action<string, int> found)
    {
        if (tree.IsEmpty) return;
        var maxDepth = target.Length + maxEdits + 1;
        var rows = new int[maxDepth + 2][];
        for (var i = 0; i < rows.Length; i++) rows[i] = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++) rows[0][j] = j;
        var text = new char[maxDepth + 2];
        SuggestFromNode(0, 0, 0, 0);

        // "codePoint" and "remaining" hold a character of several bytes that is being read
        void SuggestFromNode(int node, int depth, int codePoint, int remaining)
        {
            var count = tree.Bytes[node];
            var row = rows[depth];
            for (var sibling = node + 1; sibling <= node + count; sibling++)
            {
                var b = tree.Bytes[sibling];
                if (b == 0)
                {
                    // A word ends here
                    var flags = tree.Indexes[sibling];
                    if (remaining == 0 && depth > 0 && row[target.Length] <= maxEdits &&
                        (flags & (FlagBanned | FlagNoSuggest | FlagNeedCompound)) == 0)
                    {
                        var suggestion = new string(text, 0, depth);
                        if (isFoldTree)
                        {
                            if ((flags & FlagAllCap) != 0)
                            {
                                suggestion = suggestion.ToUpperInvariant();
                            }
                            else if ((flags & FlagOneCap) != 0)
                            {
                                suggestion = char.ToUpperInvariant(suggestion[0]) + suggestion.Substring(1);
                            }
                        }

                        found(suggestion, row[target.Length] * ScorePerEdit);
                    }

                    continue;
                }

                // Collect the bytes of a character (UTF-8)
                int value32, left;
                if (remaining > 0)
                {
                    value32 = (codePoint << 6) | (b & 0x3F);
                    left = remaining - 1;
                }
                else if (b < 0x80)
                {
                    value32 = b;
                    left = 0;
                }
                else if (b >= 0xF0)
                {
                    value32 = b & 0x07;
                    left = 3;
                }
                else if (b >= 0xE0)
                {
                    value32 = b & 0x0F;
                    left = 2;
                }
                else
                {
                    value32 = b & 0x1F;
                    left = 1;
                }

                if (left > 0)
                {
                    SuggestFromNode(tree.Indexes[sibling], depth, value32, left);
                    continue;
                }

                if (value32 > 0xFFFF || depth >= maxDepth)
                {
                    continue; // Characters outside the basic plane are not used in suggestions
                }

                var c = (char)value32;
                var compare = isFoldTree ? c : char.ToLowerInvariant(c);
                text[depth] = c;
                // The next row of the distance table
                var next = rows[depth + 1];
                next[0] = row[0] + 1;
                var best = next[0];
                for (var j = 1; j <= target.Length; j++)
                {
                    var cost = target[j - 1] == compare ? 0 : 1;
                    var value = Math.Min(Math.Min(row[j] + 1, next[j - 1] + 1), row[j - 1] + cost);
                    // Two letters swapped
                    if (depth > 0 && j > 1 && compare == target[j - 2] &&
                        (isFoldTree ? text[depth - 1] : char.ToLowerInvariant(text[depth - 1])) == target[j - 1])
                    {
                        value = Math.Min(value, rows[depth - 1][j - 2] + 1);
                    }

                    next[j] = value;
                    if (value < best) best = value;
                }

                if (best <= maxEdits)
                {
                    SuggestFromNode(tree.Indexes[sibling], depth + 1, 0, 0);
                }
            }
        }
    }

    #endregion
}
