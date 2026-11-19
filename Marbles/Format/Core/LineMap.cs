// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Core/LineMap.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
namespace Marbles.Format.Core;

/// <summary>Maps offsets in a text to 1-based line and column numbers.</summary>
public sealed class LineMap
{
    private readonly List<int> _lineStarts = [0];

    public LineMap(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            if (c is '\n' or '\r' or '\f') _lineStarts.Add(i + 1);
        }
    }

    public SourcePosition this[int offset]
    {
        get
        {
            var index = _lineStarts.BinarySearch(offset);
            if (index < 0) index = ~index - 1;
            return new SourcePosition(index + 1, offset - _lineStarts[index] + 1);
        }
    }
}
