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

        // Write the lines (after a sweep this makes sure the logo is complete): a blank first line (the version
        // number is written on it) and the rows of the logo from the column after the margin, each as its parts
        // of one color
        Tools.SetCursorEx(0, LogoMargin);
        Console.Write(' ');
        var logo = Current;
        for (var row = 0; row < LogoArt.Rows; row++)
        {
            Tools.SetCursorEx(row + 1, LogoMargin);
            Console.Write(' ');
            foreach (var run in logo.Runs(row))
            {
                SetLogoColor(run.Color);
                Console.Write(run.Text);
            }
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

    /// <summary>
    /// The color of a part of a logo: the one of its cells or its row, or the color of the logo
    /// </summary>
    internal static void SetLogoColor(ConsoleColor? color)
    {
        if (color is { } own) ConsoleColors.SetTextColor(own);
        else ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
    }

    // Reveal the logo column by column from left to right. A key press ends the sweep
    private static void SweepLogo()
    {
        try
        {
            var logo = Current;
            for (var column = 0; column < LogoArt.Width; column++)
            {
                if (Console.KeyAvailable) return;
                var drawn = false;
                for (var row = 0; row < LogoArt.Rows; row++)
                {
                    if (logo[row, column] == ' ') continue;
                    SetLogoColor(logo.ColorAt(row, column));
                    Tools.SetCursorEx(row + 1, LogoMargin + 1 + column);
                    Console.Write(logo[row, column]);
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
