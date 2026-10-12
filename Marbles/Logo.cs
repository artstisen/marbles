namespace Marbles;

/// <summary>
/// The logo of the start and login pages: "Marbles" in the style of block letters, in ASCII.
/// The first line holds the version number, and the name of the data file is shown at the top right
/// </summary>
internal static class Logo
{
    /// <summary>
    /// The logo in use (setting "logo": its name). The built-in logo is "Marbles"
    /// </summary>
    internal static LogoArt Current { get; set; } = LogoArt.BuiltIn;

    // The lines drawn: a blank first line (the version number is written on it) and the rows of the logo, from
    // the column after the margin
    private static string[] Lines => new[] { " " }.Concat(Current.AllRows.Select(row => " " + row)).ToArray();

    private const int LogoMargin = 1; // The column the logo starts in
    private const int VersionMargin = 2; // The column of the version number
    private const int AnimationColumnDelay = 3; // Milliseconds between the columns of the sweep

    /// <summary>
    /// Draw the logo with a sweep from left to right when arriving at the login page (setting "logoanim")
    /// </summary>
    internal static bool AnimationEnabled { get; set; } = true;

    private static bool _animateNext;

    /// <summary>
    /// Ask for the logo to be animated the next time the login page is drawn
    /// </summary>
    internal static void AnimateNext()
    {
        _animateNext = true;
    }

    /// <summary>
    /// True (once) if the logo is to be animated now
    /// </summary>
    internal static bool TakeAnimation()
    {
        var animate = _animateNext && AnimationEnabled;
        _animateNext = false;
        return animate;
    }

    internal static void DrawLogo(bool showVersionNumber)
    {
        DrawLogo(showVersionNumber, false);
    }

    /// <param name="showVersionNumber">Show the version number on the first line</param>
    /// <param name="animate">Sweep the logo in from the left</param>
    /// <param name="showDataFile">Show the name of the selected data file at the top right (not on the start page)</param>
    internal static void DrawLogo(bool showVersionNumber, bool animate, bool showDataFile = true)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        if (animate)
        {
            SweepLogo();
        }

        // Write the lines (after a sweep this makes sure the logo is complete)
        var lines = Lines;
        for (var i = 0; i < lines.Length; i++)
        {
            SetRowColor(i);
            Tools.SetCursorEx(i, LogoMargin);
            Console.Write(lines[i]);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);

        if (showVersionNumber)
        {
            Tools.SetCursorEx(0, VersionMargin);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(DialogText.ApplicationVersion);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        }

        if (showDataFile) Menu.WriteDataFileName();
    }

    // The color of a line: a row of the logo with a color of its own, or the color of the logo
    private static void SetRowColor(int line)
    {
        if (line > 0 && Current.RowColor(line - 1) is { } color) ConsoleColors.SetTextColor(color);
        else ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
    }

    // Reveal the logo column by column from left to right. A key press ends the sweep
    private static void SweepLogo()
    {
        try
        {
            var lines = Lines;
            var width = lines.Max(line => line.Length);
            for (var column = 0; column < width; column++)
            {
                if (Console.KeyAvailable) return;
                var drawn = false;
                for (var i = 0; i < lines.Length; i++)
                {
                    if (column >= lines[i].Length || lines[i][column] == ' ') continue;
                    SetRowColor(i);
                    Tools.SetCursorEx(i, LogoMargin + column);
                    Console.Write(lines[i][column]);
                    drawn = true;
                }

                if (drawn) Thread.Sleep(AnimationColumnDelay);
            }
        }
        catch
        {
            // The animation is only decoration: the logo is drawn in full afterwards
        }
    }
}
