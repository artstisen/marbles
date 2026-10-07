namespace marbles;

internal static class AsciiStyles
{
    private const char TopCorner1 = '.';
    private const char TopCorner2 = '+';
    private const char BottomCorner1 = '\'';
    private const char BottomCorner2 = '+';
    private static int _currentAsciiStyle;

    internal static void SetAsciiStyle(int index)
    {
        _currentAsciiStyle = index;
    }

    internal static int GetAsciiStyle()
    {
        return _currentAsciiStyle;
    }

    internal static void ResetAsciiStyle()
    {
        _currentAsciiStyle = 0;
    }

    internal static char GetTopCorner()
    {
        return _currentAsciiStyle switch
        {
            0 => TopCorner1,
            1 => TopCorner2,
            _ => TopCorner1
        };
    }

    internal static char GetBottomCorner()
    {
        return _currentAsciiStyle switch
        {
            0 => BottomCorner1,
            1 => BottomCorner2,
            _ => BottomCorner1
        };
    }
}