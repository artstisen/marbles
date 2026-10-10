namespace Marbles;

/// <summary>
/// The logo of the start and login pages: "Marbles" in the style of block letters, in ASCII.
/// The first line holds the version number, and the name of the data file is shown at the top right
/// </summary>
internal static class Logo
{
    private static readonly string[] Lines =
    {
        @" ",
        @" #_  .  _#""""""""""""##_""""""""""""##_""""""""""""##_ ### ....""""""""""""###""""""""""""###",
        @" ###_ _### ### _#%# ### _##"" ### _##"" #%#  ___ ###_ """""" #%# ___ ",
        @" #%# "" #%# #%#"" #%# #%#"" #%# #%#"" #%# #%#  #%# #%#  ___  """""" #%#",
        @" ### . ### ###  ### ###  ### ###__##"" ""##__### ""##__### ###__##"""
    };

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
        for (var i = 0; i < Lines.Length; i++)
        {
            Tools.SetCursorEx(i, LogoMargin);
            Console.Write(Lines[i]);
        }

        if (showVersionNumber)
        {
            Tools.SetCursorEx(0, VersionMargin);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(DialogText.ApplicationVersion);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        }

        if (showDataFile) Menu.WriteDataFileName();
    }

    // Reveal the logo column by column from left to right. A key press ends the sweep
    private static void SweepLogo()
    {
        try
        {
            var width = Lines.Max(line => line.Length);
            for (var column = 0; column < width; column++)
            {
                if (Console.KeyAvailable) return;
                var drawn = false;
                for (var i = 0; i < Lines.Length; i++)
                {
                    if (column >= Lines[i].Length || Lines[i][column] == ' ') continue;
                    Tools.SetCursorEx(i, LogoMargin + column);
                    Console.Write(Lines[i][column]);
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
