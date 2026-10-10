namespace Marbles;

class ConsoleColors
{
    #region Properties: Default console colors
    // Standard layout
    private const ConsoleColor DefaultBackgroundColor = ConsoleColor.Black;
    private const ConsoleColor DefaultForegroundColor = ConsoleColor.Gray;
    private const ConsoleColor DefaultHeaderColor = ConsoleColor.Cyan;
    private const ConsoleColor DefaultDividerColor = ConsoleColor.DarkGray;
    private const ConsoleColor DefaultFavoritesColor = ConsoleColor.Green;
    private const ConsoleColor DefaultPreviewColor = ConsoleColor.Yellow;
    private const ConsoleColor DefaultEditorColor = ConsoleColor.Green;
    // Used for unbound keys etc.
    private const ConsoleColor DefaultAttentionBackgroundColor = ConsoleColor.Yellow;
    private const ConsoleColor DefaultAttentionForegroundColor = ConsoleColor.Black;
    // Used by Yes/no dialog when saving etc.
    private const ConsoleColor DefaultWarningBackgroundColor = ConsoleColor.Red;
    private const ConsoleColor DefaultWarningForegroundColor = ConsoleColor.White;
    // Used by mode labels, search result selector and file name display during login
    private const ConsoleColor DefaultHighlightBackgroundColor = ConsoleColor.Cyan;
    private const ConsoleColor DefaultHighlightForegroundColor = ConsoleColor.Black;
    #endregion

    #region Properties: Current console colors

    // The color scheme has a background of its own. Without one (the default), text is shown on the background
    // of the terminal (which can be transparent), and the background color is taken to be black
    private static bool CurrentBackgroundSet { get; set; }
    private static ConsoleColor CurrentBackgroundColor { get; set; }
    private static ConsoleColor CurrentForegroundColor { get; set; }
    private static ConsoleColor CurrentHeaderColor { get; set; } // Foreground color
    private static ConsoleColor CurrentDividerColor { get; set; } // Foreground color
    private static ConsoleColor CurrentFavoritesColor { get; set; } // Foreground color
    private static ConsoleColor CurrentPreviewTitleColor { get; set; } // Foreground color
    private static ConsoleColor CurrentEditorTitleColor { get; set; } // Foreground color
    private static ConsoleColor CurrentAttentionBackgroundColor { get; set; }
    private static ConsoleColor CurrentAttentionForegroundColor { get; set; }
    private static ConsoleColor CurrentWarningBackgroundColor { get; set; }
    private static ConsoleColor CurrentWarningForegroundColor { get; set; }
    private static ConsoleColor CurrentHighlightBackgroundColor { get; set; }
    private static ConsoleColor CurrentHighlightForegroundColor { get; set; }

    #endregion

    #region Properties: Preview console colors
    private static bool PreviewBackgroundSet { get; set; }
    private static ConsoleColor PreviewBackgroundColor { get; set; }
    private static ConsoleColor PreviewForegroundColor { get; set; }
    private static ConsoleColor PreviewHeaderColor { get; set; } // Foreground color
    private static ConsoleColor PreviewDividerColor { get; set; } // Foreground color
    private static ConsoleColor PreviewFavoritesColor { get; set; } // Foreground color
    private static ConsoleColor PreviewPreviewTitleColor { get; set; } // Foreground color
    private static ConsoleColor PreviewEditorTitleColor { get; set; } // Foreground color
    private static ConsoleColor PreviewAttentionBackgroundColor { get; set; }
    private static ConsoleColor PreviewAttentionForegroundColor { get; set; }
    private static ConsoleColor PreviewWarningBackgroundColor { get; set; }
    private static ConsoleColor PreviewWarningForegroundColor { get; set; }
    private static ConsoleColor PreviewHighlightBackgroundColor { get; set; }
    private static ConsoleColor PreviewHighlightForegroundColor { get; set; }

    #endregion

    #region Background

    /// <summary>
    /// The background color of the color scheme, or null to use the background of the terminal
    /// (setting "background")
    /// </summary>
    internal static ConsoleColor? Background
    {
        get => CurrentBackgroundSet ? CurrentBackgroundColor : null;
        set
        {
            CurrentBackgroundSet = value != null;
            CurrentBackgroundColor = value ?? DefaultBackgroundColor;
        }
    }

    /// <summary>
    /// The background color of the color scheme being edited, or null to use the background of the terminal
    /// </summary>
    internal static ConsoleColor? PreviewBackground
    {
        get => PreviewBackgroundSet ? PreviewBackgroundColor : null;
        set
        {
            PreviewBackgroundSet = value != null;
            PreviewBackgroundColor = value ?? DefaultBackgroundColor;
        }
    }

    /// <summary>
    /// The background color from its number in the settings file (null, or a number that is not a color:
    /// the background of the terminal)
    /// </summary>
    internal static void SetBackgroundFromIndex(int? colorIndex)
    {
        Background = colorIndex is >= 0 and < 16 ? GetColorFromIndex(colorIndex.Value) : null;
    }

    // The colors of text on the normal background: the background of the color scheme, or that of the terminal
    private static void SetNormalBackground(bool preview)
    {
        Console.ResetColor();
        if (preview ? PreviewBackgroundSet : CurrentBackgroundSet)
        {
            Console.BackgroundColor = preview ? PreviewBackgroundColor : CurrentBackgroundColor;
        }
    }

    #endregion

    #region Color index conversion and reset
    private static void ResetColors()
    {
        // Normal colors
        CurrentBackgroundColor = DefaultBackgroundColor;
        CurrentForegroundColor = DefaultForegroundColor;
        // Individual foreground colors
        CurrentHeaderColor = DefaultHeaderColor;
        CurrentDividerColor = DefaultDividerColor;
        CurrentFavoritesColor = DefaultFavoritesColor;
        CurrentPreviewTitleColor = DefaultPreviewColor;
        CurrentEditorTitleColor = DefaultEditorColor;
        // Inverted colors
        CurrentAttentionBackgroundColor = DefaultAttentionBackgroundColor;
        CurrentAttentionForegroundColor = DefaultAttentionForegroundColor;
        CurrentWarningBackgroundColor = DefaultWarningBackgroundColor;
        CurrentWarningForegroundColor = DefaultWarningForegroundColor;
        CurrentHighlightBackgroundColor = DefaultHighlightBackgroundColor;
        CurrentHighlightForegroundColor = DefaultHighlightForegroundColor;
    }

    private static void MarblesQuestColorTheme()
    {
        // Normal colors
        CurrentBackgroundColor = GetColorFromIndex(0);
        CurrentForegroundColor = GetColorFromIndex(14);
        // Individual foreground colors
        CurrentHeaderColor = GetColorFromIndex(4);
        CurrentDividerColor = GetColorFromIndex(6);
        CurrentFavoritesColor = GetColorFromIndex(8);
        CurrentPreviewTitleColor = GetColorFromIndex(8);
        CurrentEditorTitleColor = GetColorFromIndex(14);
        // Inverted colors
        CurrentAttentionBackgroundColor = GetColorFromIndex(12);
        CurrentAttentionForegroundColor = GetColorFromIndex(0);
        CurrentWarningBackgroundColor = GetColorFromIndex(10);
        CurrentWarningForegroundColor = GetColorFromIndex(15);
        CurrentHighlightBackgroundColor = GetColorFromIndex(5);
        CurrentHighlightForegroundColor = GetColorFromIndex(4);
    }
        
    private static void NightWriterColorTheme()
    {
        // Normal colors
        CurrentBackgroundColor = GetColorFromIndex(0);
        CurrentForegroundColor = GetColorFromIndex(4);
        // Individual foreground colors
        CurrentHeaderColor = GetColorFromIndex(13);
        CurrentDividerColor = GetColorFromIndex(8);
        CurrentFavoritesColor = GetColorFromIndex(8);
        CurrentPreviewTitleColor = GetColorFromIndex(8);
        CurrentEditorTitleColor = GetColorFromIndex(14);
        // Inverted colors
        CurrentAttentionBackgroundColor = GetColorFromIndex(8);
        CurrentAttentionForegroundColor = GetColorFromIndex(15);
        CurrentWarningBackgroundColor = GetColorFromIndex(10);
        CurrentWarningForegroundColor = GetColorFromIndex(15);
        CurrentHighlightBackgroundColor = GetColorFromIndex(7);
        CurrentHighlightForegroundColor = GetColorFromIndex(4);
    }
    
    private static void GreenMonochromeColorTheme()
    {
        // Normal colors
        CurrentBackgroundColor = GetColorFromIndex(0);
        CurrentForegroundColor = GetColorFromIndex(14);
        // Individual foreground colors
        CurrentHeaderColor = GetColorFromIndex(14);
        CurrentDividerColor = GetColorFromIndex(13);
        CurrentFavoritesColor = GetColorFromIndex(14);
        CurrentPreviewTitleColor = GetColorFromIndex(14);
        CurrentEditorTitleColor = GetColorFromIndex(14);
        // Inverted colors
        CurrentAttentionBackgroundColor = GetColorFromIndex(14);
        CurrentAttentionForegroundColor = GetColorFromIndex(13);
        CurrentWarningBackgroundColor = GetColorFromIndex(14);
        CurrentWarningForegroundColor = GetColorFromIndex(13);
        CurrentHighlightBackgroundColor = GetColorFromIndex(13);
        CurrentHighlightForegroundColor = GetColorFromIndex(14);
    }
    
    private static void YellowMonochromeColorTheme()
    {
        // Normal colors
        CurrentBackgroundColor = GetColorFromIndex(0);
        CurrentForegroundColor = GetColorFromIndex(12);
        // Individual foreground colors
        CurrentHeaderColor = GetColorFromIndex(11);
        CurrentDividerColor = GetColorFromIndex(11);
        CurrentFavoritesColor = GetColorFromIndex(12);
        CurrentPreviewTitleColor = GetColorFromIndex(12);
        CurrentEditorTitleColor = GetColorFromIndex(12);
        // Inverted colors
        CurrentAttentionBackgroundColor = GetColorFromIndex(12);
        CurrentAttentionForegroundColor = GetColorFromIndex(11);
        CurrentWarningBackgroundColor = GetColorFromIndex(12);
        CurrentWarningForegroundColor = GetColorFromIndex(11);
        CurrentHighlightBackgroundColor = GetColorFromIndex(11);
        CurrentHighlightForegroundColor = GetColorFromIndex(12);
    }
    
    private static void IceWarpColorTheme()
    {
        // Normal colors
        CurrentBackgroundColor = GetColorFromIndex(0);
        CurrentForegroundColor = GetColorFromIndex(15);
        // Individual foreground colors
        CurrentHeaderColor = GetColorFromIndex(4);
        CurrentDividerColor = GetColorFromIndex(6);
        CurrentFavoritesColor = GetColorFromIndex(8);
        CurrentPreviewTitleColor = GetColorFromIndex(12);
        CurrentEditorTitleColor = GetColorFromIndex(14);
        // Inverted colors
        CurrentAttentionBackgroundColor = GetColorFromIndex(12);
        CurrentAttentionForegroundColor = GetColorFromIndex(0);
        CurrentWarningBackgroundColor = GetColorFromIndex(10);
        CurrentWarningForegroundColor = GetColorFromIndex(15);
        CurrentHighlightBackgroundColor = GetColorFromIndex(5);
        CurrentHighlightForegroundColor = GetColorFromIndex(4);
    }

    private static void WaterSport()
    {
        // Normal colors
        CurrentBackgroundColor = GetColorFromIndex(0);
        CurrentForegroundColor = GetColorFromIndex(3);
        // Individual foreground colors
        CurrentHeaderColor = GetColorFromIndex(14);
        CurrentDividerColor = GetColorFromIndex(3);
        CurrentFavoritesColor = GetColorFromIndex(12);
        CurrentPreviewTitleColor = GetColorFromIndex(15);
        CurrentEditorTitleColor = GetColorFromIndex(10);
        // Inverted colors
        CurrentAttentionBackgroundColor = GetColorFromIndex(3);
        CurrentAttentionForegroundColor = GetColorFromIndex(15);
        CurrentWarningBackgroundColor = GetColorFromIndex(7);
        CurrentWarningForegroundColor = GetColorFromIndex(15);
        CurrentHighlightBackgroundColor = GetColorFromIndex(13);
        CurrentHighlightForegroundColor = GetColorFromIndex(15);
    }

    // Set current color properties from index (used when loading settings)
    internal static void SetColorFromIndex(int propertyIndex, int colorIndex)
    {
        switch (propertyIndex)
        {
            case 0:
                CurrentBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 1:
                CurrentForegroundColor = GetColorFromIndex(colorIndex);
                break;
            case 2:
                CurrentHeaderColor = GetColorFromIndex(colorIndex);
                break;
            case 3:
                CurrentDividerColor = GetColorFromIndex(colorIndex);
                break;
            case 4:
                CurrentFavoritesColor = GetColorFromIndex(colorIndex);
                break;
            case 5:
                CurrentPreviewTitleColor = GetColorFromIndex(colorIndex);
                break;
            case 6:
                CurrentEditorTitleColor = GetColorFromIndex(colorIndex);
                break;
            case 7:
                CurrentAttentionBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 8:
                CurrentAttentionForegroundColor = GetColorFromIndex(colorIndex);
                break;
            case 9:
                CurrentWarningBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 10:
                CurrentWarningForegroundColor = GetColorFromIndex(colorIndex);
                break;
            case 11:
                CurrentHighlightBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 12:
                CurrentHighlightForegroundColor = GetColorFromIndex(colorIndex);
                break;
        }
    }

    internal static ConsoleColor GetCurrentColorFromIndex(int propertyIndex)
    {
        switch (propertyIndex)
        {
            case 0:
                return CurrentBackgroundColor;
            case 1:
                return CurrentForegroundColor;
            case 2:
                return CurrentHeaderColor;
            case 3:
                return CurrentDividerColor;
            case 4:
                return CurrentFavoritesColor;
            case 5:
                return CurrentPreviewTitleColor;
            case 6:
                return CurrentEditorTitleColor;
            case 7:
                return CurrentAttentionBackgroundColor;
            case 8:
                return CurrentAttentionForegroundColor;
            case 9:
                return CurrentWarningBackgroundColor;
            case 10:
                return CurrentWarningForegroundColor;
            case 11:
                return CurrentHighlightBackgroundColor;
            case 12:
                return CurrentHighlightForegroundColor;
            default: return CurrentWarningForegroundColor;
        }
    }

    private static ConsoleColor GetPreviewColorFromIndex(int propertyIndex)
    {
        switch (propertyIndex)
        {
            case 0:
                return PreviewBackgroundColor;
            case 1:
                return PreviewForegroundColor;
            case 2:
                return PreviewHeaderColor;
            case 3:
                return PreviewDividerColor;
            case 4:
                return PreviewFavoritesColor;
            case 5:
                return PreviewPreviewTitleColor;
            case 6:
                return PreviewEditorTitleColor;
            case 7:
                return PreviewHighlightBackgroundColor;
            case 8:
                return PreviewHighlightForegroundColor;
            case 9:
                return PreviewAttentionBackgroundColor;
            case 10:
                return PreviewAttentionForegroundColor;
            case 11:
                return PreviewWarningBackgroundColor;
            case 12:
                return PreviewWarningForegroundColor;
            default: return PreviewWarningForegroundColor;
        }
    }

    // Get color index used by the SetColorFromIndex method
    private static ConsoleColor GetColorFromIndex(int colorIndex)
    {
        switch (colorIndex)
        {
            case 0:
                return ConsoleColor.Black;
            case 1:
                return ConsoleColor.DarkGray;
            case 2:
                return ConsoleColor.Gray;
            case 3:
                return ConsoleColor.DarkCyan;
            case 4:
                return ConsoleColor.Cyan;
            case 5:
                return ConsoleColor.DarkBlue;
            case 6:
                return ConsoleColor.Blue;
            case 7:
                return ConsoleColor.DarkMagenta;
            case 8:
                return ConsoleColor.Magenta;
            case 9:
                return ConsoleColor.DarkRed;
            case 10:
                return ConsoleColor.Red;
            case 11:
                return ConsoleColor.DarkYellow;
            case 12:
                return ConsoleColor.Yellow;
            case 13:
                return ConsoleColor.DarkGreen;
            case 14:
                return ConsoleColor.Green;
            case 15:
                return ConsoleColor.White;
            default: return ConsoleColor.Red;
        }
    }

    // Get index from color (used when saving settings)
    internal static int GetIndexFromColor(ConsoleColor color)
    {
        switch (color)
        {
            case ConsoleColor.Black:
                return 0;
            case ConsoleColor.DarkGray:
                return 1;
            case ConsoleColor.Gray:
                return 2;
            case ConsoleColor.DarkCyan:
                return 3;
            case ConsoleColor.Cyan:
                return 4;
            case ConsoleColor.DarkBlue:
                return 5;
            case ConsoleColor.Blue:
                return 6;
            case ConsoleColor.DarkMagenta:
                return 7;
            case ConsoleColor.Magenta:
                return 8;
            case ConsoleColor.DarkRed:
                return 9;
            case ConsoleColor.Red:
                return 10;
            case ConsoleColor.DarkYellow:
                return 11;
            case ConsoleColor.Yellow:
                return 12;
            case ConsoleColor.DarkGreen:
                return 13;
            case ConsoleColor.Green:
                return 14;
            case ConsoleColor.White:
                return 15;
            default: return 15;
        }
    }
    #endregion

    #region Color presets (used through out the application)
    public enum ColorPreset
    {
        Reset,
        Normal,
        NormalInverted,
        HeaderColor,
        Divider,
        Favorites,
        PreviewTitle,
        EditorTitle,
        HighlightInverted,
        AttentionInverted,
        WarningInverted,
        MarblesQuest,
        NightWriter,
        YellowMonochrome,
        GreenMonochrome,
        IceWarp,
        WaterSport
    }

    // Used for saving the last selected color preset
    private static int _selectedColorPreset;

    public static void DefaultColorPresetIndex()
    {
        _selectedColorPreset = 0;
    }

    internal static void SetColorPresetIndex(int index)
    {
        _selectedColorPreset = index;
    }

    internal static int GetColorPresetIndex()
    {
        return _selectedColorPreset;
    }

    /// <summary>
    /// Write text in a color on the normal background (the normal text color is used if the color is that
    /// of the background)
    /// </summary>
    internal static void SetTextColor(ConsoleColor color)
    {
        SetColors(ColorPreset.Normal);
        if (Monochrome)
        {
            // No colors: text with a color of its own is shown brighter
            Console.ForegroundColor = ConsoleColor.White;
        }
        else if (!IsBackgroundColor(color))
        {
            Console.ForegroundColor = color;
        }
    }

    internal static bool IsBackgroundColor(ConsoleColor color) => color == CurrentBackgroundColor;

    /// <summary>
    /// True to show everything in black and white, whatever the colors of the color scheme are
    /// (setting "monochrome"). The color scheme is kept, and is used again when this is turned off
    /// </summary>
    internal static bool Monochrome { get; set; }

    // The colors used when everything is shown in black and white
    private static bool SetMonochromeColors(ColorPreset preset)
    {
        switch (preset)
        {
            case ColorPreset.Normal:
            case ColorPreset.Divider:
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Gray;
                return true;
            case ColorPreset.HeaderColor:
            case ColorPreset.Favorites:
            case ColorPreset.PreviewTitle:
            case ColorPreset.EditorTitle:
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.White;
                return true;
            case ColorPreset.NormalInverted:
            case ColorPreset.HighlightInverted:
            case ColorPreset.AttentionInverted:
            case ColorPreset.WarningInverted:
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                return true;
            default:
                // The color presets set the colors of the scheme
                return false;
        }
    }

    public static void SetColors(ColorPreset preset)
    {
        if (Monochrome && SetMonochromeColors(preset)) return;
        // The color presets use the background of the terminal
        if (preset == ColorPreset.Reset || preset >= ColorPreset.MarblesQuest) Background = null;
        if (preset == ColorPreset.Reset)
        {
            ResetColors();
        }
        else if (preset == ColorPreset.MarblesQuest)
        {
            MarblesQuestColorTheme();
        }
        else if (preset == ColorPreset.NightWriter)
        {
            NightWriterColorTheme();
        }
        else if (preset == ColorPreset.YellowMonochrome)
        {
            YellowMonochromeColorTheme();
        }
        else if (preset == ColorPreset.GreenMonochrome)
        {
            GreenMonochromeColorTheme();
        }
        else if (preset == ColorPreset.IceWarp)
        {
            IceWarpColorTheme();
        }
        else if (preset == ColorPreset.WaterSport)
        {
            WaterSport();
        }
        else if (preset == ColorPreset.Normal)
        {
            SetNormalBackground(false);
            Console.ForegroundColor = CurrentForegroundColor;
        }
        else if (preset == ColorPreset.NormalInverted)
        {
            Console.BackgroundColor = CurrentForegroundColor;
            Console.ForegroundColor = CurrentBackgroundColor;
        }
        else if (preset == ColorPreset.HeaderColor)
        {
            SetNormalBackground(false);
            Console.ForegroundColor = CurrentHeaderColor;
        }
        else if (preset == ColorPreset.Divider)
        {
            SetNormalBackground(false);
            Console.ForegroundColor = CurrentDividerColor;
        }
        else if (preset == ColorPreset.Favorites)
        {
            SetNormalBackground(false);
            Console.ForegroundColor = CurrentFavoritesColor;
        }
        else if (preset == ColorPreset.PreviewTitle)
        {
            SetNormalBackground(false);
            Console.ForegroundColor = CurrentPreviewTitleColor;
        }
        else if (preset == ColorPreset.EditorTitle)
        {
            SetNormalBackground(false);
            Console.ForegroundColor = CurrentEditorTitleColor;
        }
        else if (preset == ColorPreset.HighlightInverted)
        {
            Console.BackgroundColor = CurrentHighlightBackgroundColor;
            Console.ForegroundColor = CurrentHighlightForegroundColor;
        }
        else if (preset == ColorPreset.AttentionInverted)
        {
            Console.BackgroundColor = CurrentAttentionBackgroundColor;
            Console.ForegroundColor = CurrentAttentionForegroundColor;
        }
        else if (preset == ColorPreset.WarningInverted)
        {
            Console.BackgroundColor = CurrentWarningBackgroundColor;
            Console.ForegroundColor = CurrentWarningForegroundColor;
        }

    }

    public static void SetColorsFromPreview(ColorPreset preset)
    {
        if (preset == ColorPreset.Reset)
        {
            ResetColors();
        }
        else if (preset == ColorPreset.Normal)
        {
            SetNormalBackground(true);
            Console.ForegroundColor = PreviewForegroundColor;
        }
        else if (preset == ColorPreset.NormalInverted)
        {
            Console.BackgroundColor = PreviewForegroundColor;
            Console.ForegroundColor = PreviewBackgroundColor;
        }
        else if (preset == ColorPreset.HeaderColor)
        {
            SetNormalBackground(true);
            Console.ForegroundColor = PreviewHeaderColor;
        }
        else if (preset == ColorPreset.Divider)
        {
            SetNormalBackground(true);
            Console.ForegroundColor = PreviewDividerColor;
        }
        else if (preset == ColorPreset.Favorites)
        {
            SetNormalBackground(true);
            Console.ForegroundColor = PreviewFavoritesColor;
        }
        else if (preset == ColorPreset.PreviewTitle)
        {
            SetNormalBackground(true);
            Console.ForegroundColor = PreviewPreviewTitleColor;
        }
        else if (preset == ColorPreset.EditorTitle)
        {
            SetNormalBackground(true);
            Console.ForegroundColor = PreviewEditorTitleColor;
        }
        else if (preset == ColorPreset.HighlightInverted)
        {
            Console.BackgroundColor = PreviewHighlightBackgroundColor;
            Console.ForegroundColor = PreviewHighlightForegroundColor;
        }
        else if (preset == ColorPreset.AttentionInverted)
        {
            Console.BackgroundColor = PreviewAttentionBackgroundColor;
            Console.ForegroundColor = PreviewAttentionForegroundColor;
        }
        else if (preset == ColorPreset.WarningInverted)
        {
            Console.BackgroundColor = PreviewWarningBackgroundColor;
            Console.ForegroundColor = PreviewWarningForegroundColor;
        }

    }
    #endregion

    #region Change color settings with preview and save
    private static int _previewColorIndex;


    /// <summary>
    /// The color of a property in the color scheme being edited
    /// </summary>
    internal static ConsoleColor GetPreviewColor(int propertyIndex)
    {
        return GetPreviewColorFromIndex(propertyIndex);
    }

    /// <summary>
    /// Set the color of a property in the color scheme being edited
    /// </summary>
    internal static void SetPreviewColor(int propertyIndex, ConsoleColor color)
    {
        SetPreviewColorFromIndex(propertyIndex, GetIndexFromColor(color));
    }

    /// <summary>
    /// False if a color cannot be used for a property, as the text would not be visible on its background
    /// </summary>
    internal static bool IsPreviewColorAllowed(int propertyIndex, ConsoleColor color)
    {
        if (propertyIndex == 0)
        {
            // The background cannot have the color of the text shown on it
            for (var text = 1; text <= 6; text++)
            {
                if (GetPreviewColorFromIndex(text) == color) return false;
            }

            return true;
        }

        _previewColorIndex = GetIndexFromColor(color);
        return !IsBackgroundColorMatchingForegroundColor(propertyIndex);
    }

    private static bool IsBackgroundColorMatchingForegroundColor(int propertyIndex)
    {
        // This method assures that if you change the background color it wont hit a matching foreground color
        // and vice versa, so that the text wont render invisible. The same is the case with the highlight colors.

        // HeaderColor: back = 0, text = 1 (special
        // HighlightInverted: back = 8, text = 7
        // AttentionInverted: back = 10, text = 9
        // WarningInverted: back = 12, text = 11

        if ((propertyIndex != 0 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(0)) ||
             propertyIndex == 0 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(1))) ||
            (propertyIndex == 8 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(7)) ||
             propertyIndex == 7 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(8))) ||
            (propertyIndex == 10 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(9)) ||
             propertyIndex == 9 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(10))) ||
            (propertyIndex == 12 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(11)) ||
             propertyIndex == 11 && _previewColorIndex == GetIndexFromColor(GetPreviewColorFromIndex(12))))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static void SetPreviewColorFromIndex(int propertyIndex, int colorIndex)
    {
        switch (propertyIndex)
        {
            case 0:
                PreviewBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 1:
                PreviewForegroundColor = GetColorFromIndex(colorIndex);
                break;
            case 2:
                PreviewHeaderColor = GetColorFromIndex(colorIndex);
                break;
            case 3:
                PreviewDividerColor = GetColorFromIndex(colorIndex);
                break;
            case 4:
                PreviewFavoritesColor = GetColorFromIndex(colorIndex);
                break;
            case 5:
                PreviewPreviewTitleColor = GetColorFromIndex(colorIndex);
                break;
            case 6:
                PreviewEditorTitleColor = GetColorFromIndex(colorIndex);
                break;
            case 7:
                PreviewHighlightBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 8:
                PreviewHighlightForegroundColor = GetColorFromIndex(colorIndex);
                break;
            case 9:
                PreviewAttentionBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 10:
                PreviewAttentionForegroundColor = GetColorFromIndex(colorIndex);
                break;
            case 11:
                PreviewWarningBackgroundColor = GetColorFromIndex(colorIndex);
                break;
            case 12:
                PreviewWarningForegroundColor = GetColorFromIndex(colorIndex);
                break;
        }
    }

    // Must be initialized to hold the current color preset or else saving will make
    // all colors default black.
    internal static void InitializePreviewColors()
    {
        // Normal colors
        PreviewBackgroundSet = CurrentBackgroundSet;
        PreviewBackgroundColor = CurrentBackgroundColor;
        PreviewForegroundColor = CurrentForegroundColor;
        // Individual foreground colors
        PreviewHeaderColor = CurrentHeaderColor;
        PreviewDividerColor = CurrentDividerColor;
        PreviewFavoritesColor = CurrentFavoritesColor;
        PreviewPreviewTitleColor = CurrentPreviewTitleColor;
        PreviewEditorTitleColor = CurrentEditorTitleColor;
        // Inverted colors
        PreviewHighlightBackgroundColor = CurrentHighlightBackgroundColor;
        PreviewHighlightForegroundColor = CurrentHighlightForegroundColor;
        PreviewAttentionBackgroundColor = CurrentAttentionBackgroundColor;
        PreviewAttentionForegroundColor = CurrentAttentionForegroundColor;
        PreviewWarningBackgroundColor = CurrentWarningBackgroundColor;
        PreviewWarningForegroundColor = CurrentWarningForegroundColor;
    }

    internal static void SavePreviewToCurrentColors()
    {
        // Normal colors
        CurrentBackgroundSet = PreviewBackgroundSet;
        CurrentBackgroundColor = PreviewBackgroundColor;
        CurrentForegroundColor = PreviewForegroundColor;
        // Individual foreground colors
        CurrentHeaderColor = PreviewHeaderColor;
        CurrentDividerColor = PreviewDividerColor;
        CurrentFavoritesColor = PreviewFavoritesColor;
        CurrentPreviewTitleColor = PreviewPreviewTitleColor;
        CurrentEditorTitleColor = PreviewEditorTitleColor;
        // Inverted colors
        CurrentHighlightBackgroundColor = PreviewHighlightBackgroundColor;
        CurrentHighlightForegroundColor = PreviewHighlightForegroundColor;
        CurrentAttentionBackgroundColor = PreviewAttentionBackgroundColor;
        CurrentAttentionForegroundColor = PreviewAttentionForegroundColor;
        CurrentWarningBackgroundColor = PreviewWarningBackgroundColor;
        CurrentWarningForegroundColor = PreviewWarningForegroundColor;
        // Save settings
        FileIo.SaveSettings();
    }


    #endregion
}