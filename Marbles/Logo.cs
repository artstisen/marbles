namespace marbles;

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
        @" ",
        @"*** Temp/utility ***",
        @" ",
        @" ",
        @" ",
        @" ",
        @"Your custom text from " + FileIo.SettingsFileName + "...",
        @"(from login menu press " + DialogText.CtrlPlus + Keyboard.GetTriggerAsString() + ", S and set the \"logotxt\" property)",
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

    internal static void DrawLogo(bool showVersionNumber)
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

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.CursorTopLeft();
        Tools.CursorRight(_logoMargin);
        // Write first line of logo
        Console.Write(LogoLines[_startIndex]);
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(_logoMargin);
        // Write second line of logo
        Console.Write(LogoLines[_startIndex + 1]);
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(_logoMargin);
        // Write third line of logo
        // (if using custom logo text)
        if (_logoIndex == 7 && HasLogoText())
        {
            Console.Write(GetLogoText());
        }
        else
        {
            Console.Write(LogoLines[_startIndex + 2]);
        }
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(_logoMargin);
        // Write fourth line of logo
        if (_logoIndex == 7 && HasLogoText())
        {
            Console.Write(" ");
        }
        else
        {
            Console.Write(LogoLines[_startIndex + 3]);
        }
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(_logoMargin);
        // Write fifth line of logo
        Console.Write(LogoLines[_startIndex + 4]);
        // Write version number
        if (showVersionNumber)
        {
            // Resolve margin for the version number
            _versionMargin = _logoIndex switch
            {
                0 => 41,
                1 => 29,
                2 => 43,
                3 => 30,
                4 => 30,
                5 => 29,
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
}