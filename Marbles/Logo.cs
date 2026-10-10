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
        // Marbles in the style of block letters, in ASCII (four lines: the first line holds the version number)
        @" ",
        @" #_  .  _#""""""""""""##_""""""""""""##_""""""""""""##_ ### ....""""""""""""###""""""""""""###",
        @" ###_ _### ### _#%# ### _##"" ### _##"" #%#  ___ ###_ """""" #%# ___ ",
        @" #%# "" #%# #%#"" #%# #%#"" #%# #%#"" #%# #%#  #%# #%#  ___  """""" #%#",
        @" ### . ### ###  ### ###  ### ###__##"" ""##__### ""##__### ###__##""",
        // The same block letters: soft edges (~ and ,)
        @" ",
        @" #,  :  ,#~~~~~~##,~~~~~~##,~~~~~~##, ### ::::~~~~~~###~~~~~~###",
        @" ###, ,### ### ,#@# ### ,##~ ### ,##~ #@#  ,,, ###, ~~~ #@# ,,, ",
        @" #@# ~ #@# #@#~ #@# #@#~ #@# #@#~ #@# #@#  #@# #@#  ,,,  ~~~ #@#",
        @" ### : ### ###  ### ###  ### ###,,##~ ~##,,### ~##,,### ###,,##~",
        // The same block letters: heavy (M)
        @" ",
        @" M_  .  _M""""""""""""MM_""""""""""""MM_""""""""""""MM_ MMM ....""""""""""""MMM""""""""""""MMM",
        @" MMM_ _MMM MMM _M#M MMM _MM"" MMM _MM"" M#M  ___ MMM_ """""" M#M ___ ",
        @" M#M "" M#M M#M"" M#M M#M"" M#M M#M"" M#M M#M  M#M M#M  ___  """""" M#M",
        @" MMM . MMM MMM  MMM MMM  MMM MMM__MM"" ""MM__MMM ""MM__MMM MMM__MM""",
        // The same block letters: a marble in each letter (o)
        @" ",
        @" #_  .  _#^^^^^^##_^^^^^^##_^^^^^^##_ ### ....^^^^^^###^^^^^^###",
        @" ###_ _### ### _#o# ### _##^ ### _##^ #o#  ___ ###_ ^^^ #o# ___ ",
        @" #o# ^ #o# #o#^ #o# #o#^ #o# #o#^ #o# #o#  #o# #o#  ___  ^^^ #o#",
        @" ### . ### ###  ### ###  ### ###__##^ ^##__### ^##__### ###__##^",
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

    // For each preset: the left margin of the logo and the column of the version number
    private static readonly int[] LogoMargins = { 4, 1, 1, 1, 1, 4, 1, 3, 2, 3, 3, 2 };
    private static readonly int[] VersionMargins = { 43, 2, 2, 2, 2, 31, 45, 32, 32, 31, 1, 1 };

    /// <summary>
    /// The number of logo presets (five lines each)
    /// </summary>
    internal static int PresetCount => LogoMargins.Length;

    // The last preset shows a text of your own (setting "logotxt")
    private static int CustomTextPreset => PresetCount - 1;

    public static void SetLogoIndex(int index)
    {
        if (index >= 0 && index < PresetCount)
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
        // The lines of the preset, and its margin
        _startIndex = _logoIndex * 5;
        _logoMargin = LogoMargins[_logoIndex];

        // The five lines of the logo (the third and fourth are replaced if using custom logo text)
        var lines = new string[5];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = LogoLines[_startIndex + i];
        }

        if (_logoIndex == CustomTextPreset && HasLogoText())
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
            _versionMargin = VersionMargins[_logoIndex];
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
