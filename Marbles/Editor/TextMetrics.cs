using System.Globalization;
using System.Text;

namespace Marbles;

/// <summary>
/// Helpers for working with text as it is seen on screen: a "text element" is what the user perceives
/// as one character (it can be several chars, like a letter with combining marks or an emoji), and its
/// width is the number of terminal cells it occupies.
/// </summary>
internal static class TextMetrics
{
    // Chars below this value are never combined with the char before them
    private const char FirstCombiningChar = '̀';

    /// <summary>
    /// The length (in chars) of the text element starting at index
    /// </summary>
    internal static int ElementLength(string s, int index)
    {
        if (s[index] < FirstCombiningChar && (index + 1 == s.Length || s[index + 1] < FirstCombiningChar))
        {
            return 1;
        }

        return StringInfo.GetNextTextElementLength(s, index);
    }

    /// <summary>
    /// The length (in chars) of the text element ending just before index
    /// </summary>
    internal static int PreviousElementLength(string s, int index)
    {
        if (index <= 0) return 0;
        if (s[index - 1] < FirstCombiningChar) return 1;
        // Go back to a char that is known to start an element and walk forward from there
        var start = index - 1;
        while (start > 0 && s[start] >= FirstCombiningChar)
        {
            start--;
        }

        while (start < index)
        {
            var length = ElementLength(s, start);
            if (start + length >= index) return index - start;
            start += length;
        }

        return 1;
    }

    /// <summary>
    /// The index of the first char of the text element containing index
    /// </summary>
    internal static int ElementStart(string s, int index)
    {
        if (index <= 0 || index >= s.Length) return Math.Max(0, Math.Min(index, s.Length));
        return index + 1 - PreviousElementLength(s, index + 1);
    }

    /// <summary>
    /// The number of terminal cells used by the text element at index
    /// </summary>
    internal static int Width(string s, int index, int length)
    {
        var c = s[index];
        if (c < FirstCombiningChar) return 1; // Control characters are displayed as a blank space
        if (!Rune.TryGetRuneAt(s, index, out var rune)) return 1;
        // An emoji variation selector makes the element wide
        if (length > 1 && s.AsSpan(index, length).Contains('️')) return 2;
        return RuneWidth(rune);
    }

    /// <summary>
    /// The number of terminal cells used by the chars from start and up to (not including) end
    /// </summary>
    internal static int WidthOfRange(string s, int start, int end)
    {
        var cells = 0;
        while (start < end)
        {
            var length = ElementLength(s, start);
            cells += Width(s, start, length);
            start += length;
        }

        return cells;
    }

    internal static int WidthOf(string s)
    {
        return WidthOfRange(s, 0, s.Length);
    }

    private static int RuneWidth(Rune rune)
    {
        var value = rune.Value;
        if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format) return 0; // Zero width space, joiners etc.
        if (value < 0x1100) return 1;
        if (value <= 0x115F || // Hangul Jamo
            value is >= 0x2E80 and <= 0x303E || // CJK radicals and punctuation
            value is >= 0x3041 and <= 0x33FF || // Hiragana, Katakana, CJK symbols
            value is >= 0x3400 and <= 0x4DBF || // CJK extension A
            value is >= 0x4E00 and <= 0x9FFF || // CJK unified ideographs
            value is >= 0xA000 and <= 0xA4CF || // Yi
            value is >= 0xAC00 and <= 0xD7A3 || // Hangul syllables
            value is >= 0xF900 and <= 0xFAFF || // CJK compatibility ideographs
            value is >= 0xFE30 and <= 0xFE4F || // CJK compatibility forms
            value is >= 0xFF00 and <= 0xFF60 || // Fullwidth forms
            value is >= 0xFFE0 and <= 0xFFE6 ||
            value is >= 0x1F300 and <= 0x1F64F || // Emoji
            value is >= 0x1F680 and <= 0x1F6FF ||
            value is >= 0x1F900 and <= 0x1FAFF ||
            value is >= 0x20000 and <= 0x3FFFD) // CJK extensions
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// The text to write to the terminal for a part of a line (control characters are shown as blank spaces)
    /// </summary>
    internal static string Printable(string s, int start, int end)
    {
        var needsReplacing = false;
        for (var i = start; i < end; i++)
        {
            if (!char.IsControl(s[i])) continue;
            needsReplacing = true;
            break;
        }

        if (!needsReplacing) return s.Substring(start, end - start);
        var builder = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            builder.Append(char.IsControl(s[i]) ? ' ' : s[i]);
        }

        return builder.ToString();
    }

    // Break a text into lines no wider than a number of cells, between words
    internal static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' '))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && TextMetrics.WidthOf(candidate) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        lines.Add(line);
        return lines;
    }
}
