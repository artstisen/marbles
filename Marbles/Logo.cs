namespace Marbles;

internal static class Logo
{
    private static readonly string[] LogoLines = new[]
    {
        @"    __  ___              __     __            ",
        @"   /  |/  /____ _ _____ / /_   / /___   _____ ",
        @"  / /|_/ // __ `// ___// __ \ / // _ \ / ___/ ",
        @" / /  / // /_/ // /   / /_/ // //  __/(__  )  ",
        @"/_/  /_/ \__,_//_/   /_.___//_/ \___//____/   ",
        @"    _   __        __             ",
        @"   / | / /____   / /_ ___   _____",
        @"  /  |/ // __ \ / __// _ \ / ___/",
        @" / /|  // /_/ // /_ /  __/(__  ) ",
        @"/_/ |_/ \____/ \__/ \___//____/  ",
        @"-   -  ---  ----  ----  --    ----- -----",
        @"-- -- -- -- -- -- -- -- --    --    --   ",
        @"----- ----- ----  ----  --    ----- -----",
        @"-- -- -- -- -- -- -- -- --    --       --",
        @"-- -- -- -- -- -- ----- ----- ----- -----",
        @"  ,~__~.,  ",
        @" ((')(') ) ",
        @".| ---u  |.",
        @"/(__---__)\",
        @" ~*     *~ ",
        @"    ,~__~.,    ",
        @"   ((O)(o) )   ",
        @"}=-| ~--~  |-={",
        @"   (__---__)   ",
        @"   ~*     *~   ",
        @"  _____  ",
        @" /\  _ \ ",
        @"/  \__\_\",
        @"\/ /  __/",
        @" \/__/_/ ",
        @" ",
        @" +----------------+",
        @" | --DEBUG/TEST-- |",
        @" +----------------+",
        @" ",
        @" ",
        @" ",
        @"Your custom text from " + FileIo.SettingsFileName + "...",
        @" ",
        @" "
    };

    private static int _logoIndex;

    public static void SetLogoIndex(int index)
    {
        if (index is >= 0 and <= 7)
            _logoIndex = index;
        else
        {
            ResetLogoIndex();
        }
    }

    public static int GetLogoIndex()
    {
        return _logoIndex;
    }

    public static void ResetLogoIndex()
    {
        _logoIndex = 0;
    }

    private static string _logoText = string.Empty;

    public static void SetLogoText(string input)
    {
        if (Tools.IsValid(input))
        {
            _logoText = Tools.TruncateString(input, 48, true);
        }
    }

    private static bool HasLogoText()
    {
        if (Tools.IsValid(_logoText))
        {
            return true;
        }

        return false;
    }

    public static string GetLogoText()
    {
        return _logoText;
    }

    public static void ResetLogoText()
    {
        _logoText = string.Empty;
    }

    private static int _startIndex;
    private static int _logoMargin;
    private static int _versionMargin;

    private const int AnimationColumnDelay = 6; // Milliseconds between the columns of the sweep

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

    internal static void DrawLogo(bool showVersionNumber, bool animate)
    {
        // Resolve start index
        _startIndex = @_logoIndex switch
        {
            0 => 0,
            1 => 5,
            2 => 10,
            3 => 15,
            4 => 20,
            5 => 25,
            6 => 30,
            7 => 35,
            _ => 0
        };
        // Resolve left margin
        _logoMargin = _logoIndex switch
        {
            0 => 4,
            1 => 4,
            2 => 1,
            3 => 3,
            4 => 2,
            5 => 3,
            6 => 3,
            7 => 2,
            _ => 0
        };

        // The five lines of the logo (the third and fourth are replaced if using custom logo text)
        var lines = new string[5];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = LogoLines[_startIndex + i];
        }

        if (_logoIndex == 7 && HasLogoText())
        {
            lines[2] = GetLogoText();
            lines[3] = " ";
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        if (animate)
        {
            SweepLogo(lines);
        }

        // Write the lines (after a sweep this makes sure the logo is complete)
        for (var i = 0; i < lines.Length; i++)
        {
            Tools.SetCursorEx(i, _logoMargin);
            Console.Write(lines[i]);
        }

        // Write version number
        if (showVersionNumber)
        {
            // Resolve margin for the version number
            _versionMargin = _logoIndex switch
            {
                0 => 43,
                1 => 31,
                2 => 45,
                3 => 32,
                4 => 32,
                5 => 31,
                6 => 1,
                7 => 1,
                _ => 0
            };
            Tools.CursorTopLeft();
            Tools.CursorRight(_versionMargin);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(DialogText.ApplicationVersion);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        }

        Menu.WriteDataFileName();
    }

    // Reveal the logo column by column from left to right. A key press ends the sweep
    private static void SweepLogo(string[] lines)
    {
        try
        {
            var width = lines.Max(line => line.Length);
            for (var column = 0; column < width; column++)
            {
                if (Console.KeyAvailable) return;
                var drawn = false;
                for (var i = 0; i < lines.Length; i++)
                {
                    if (column >= lines[i].Length || lines[i][column] == ' ') continue;
                    Tools.SetCursorEx(i, _logoMargin + column);
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
