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

    internal enum AnimationSpeed
    {
        Slow,
        Fast,
        VeryFast,
        Off
    }

    /// <summary>
    /// How fast the logo is drawn with a sweep from left to right when arriving at the login page, or that it
    /// is not (setting "logoanim": slow, fast, veryfast or off)
    /// </summary>
    internal static AnimationSpeed Animation { get; set; } = AnimationSpeed.Fast;

    internal static bool AnimationEnabled => Animation != AnimationSpeed.Off;

    /// <summary>
    /// How long the sweep of the whole logo takes, in milliseconds. The sweep follows the clock, as a short wait
    /// is not equally short everywhere (on Windows it is at least about 15 milliseconds)
    /// </summary>
    internal static int AnimationDuration(AnimationSpeed speed)
    {
        return speed switch
        {
            AnimationSpeed.Slow => 2000,
            AnimationSpeed.VeryFast => 300,
            AnimationSpeed.Off => 0,
            _ => 800
        };
    }

    /// <summary>
    /// When a column of the logo is done (from 0), in milliseconds from the start of a sweep of a duration
    /// </summary>
    internal static long ColumnTime(int column, int duration) => (long)duration * (column + 1) / LogoArt.Width;

    /// <summary>
    /// The name of a speed in the settings file
    /// </summary>
    internal static string SettingName(AnimationSpeed speed)
    {
        return speed switch
        {
            AnimationSpeed.Slow => "slow",
            AnimationSpeed.VeryFast => "veryfast",
            AnimationSpeed.Off => "off",
            _ => "fast"
        };
    }

    internal static AnimationSpeed ParseAnimation(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "slow" => AnimationSpeed.Slow,
            "veryfast" => AnimationSpeed.VeryFast,
            "off" or "false" => AnimationSpeed.Off,
            _ => AnimationSpeed.Fast
        };
    }

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
            var duration = AnimationDuration(Animation);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var column = 0; column < LogoArt.Width; column++)
            {
                if (Console.KeyAvailable) return;
                for (var row = 0; row < LogoArt.Rows; row++)
                {
                    if (logo[row, column] == ' ') continue;
                    SetLogoColor(logo.ColorAt(row, column));
                    Tools.SetCursorEx(row + 1, LogoMargin + 1 + column);
                    Console.Write(logo[row, column]);
                }

                // Wait until it is time for the next column
                while (watch.ElapsedMilliseconds < ColumnTime(column, duration))
                {
                    if (Console.KeyAvailable) return;
                    Thread.Sleep(1);
                }
            }
        }
        catch
        {
            // The animation is only decoration: the logo is drawn in full afterwards
        }
    }
}
