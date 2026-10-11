namespace Marbles;

/// <summary>
/// The color scheme editor: a page of its own, opened from the Configuration page and with ":scheme" in the main
/// menu. A preset is chosen at the top, and the colors of each group below it are picked from a list. The page
/// shows the colors being edited, also as a small screen at the bottom, and nothing is used until it is saved.
/// </summary>
internal static class SchemeEditor
{
    // A group of the color scheme: its name, the style it is shown in, and where its colors are kept among the
    // colors being edited (the text, and for the inverted styles the background; -1 if there is none)
    private sealed record Group(string Name, ConsoleColors.ColorPreset Style, int Color, int Background);

    private static Group[] Groups => new[]
    {
        new Group(Lang.T("Text"), ConsoleColors.ColorPreset.Normal, 1, -1),
        new Group(Lang.T("Logo and labels"), ConsoleColors.ColorPreset.HeaderColor, 2, -1),
        new Group(Lang.T("Dividers"), ConsoleColors.ColorPreset.Divider, 3, -1),
        new Group(Lang.T("Favorites"), ConsoleColors.ColorPreset.Favorites, 4, -1),
        new Group(Lang.T("Preview title"), ConsoleColors.ColorPreset.PreviewTitle, 5, -1),
        new Group(Lang.T("Editor title"), ConsoleColors.ColorPreset.EditorTitle, 6, -1),
        new Group(Lang.T("Highlight"), ConsoleColors.ColorPreset.HighlightInverted, 8, 7),
        new Group(Lang.T("Attention"), ConsoleColors.ColorPreset.AttentionInverted, 10, 9),
        new Group(Lang.T("Warning"), ConsoleColors.ColorPreset.WarningInverted, 12, 11),
        // The background of the whole screen (or the one of the terminal)
        new Group(Lang.T("Background"), ConsoleColors.ColorPreset.Normal, -1, 0)
    };

    private const int PresetRow = 2;
    private const int FirstGroupRow = 4;
    private const int PickerLeft = 52;
    private static int PreviewTop => FirstGroupRow + Groups.Length + 1;

    private static int _preset; // The preset the colors are based on
    private static bool _changed;

    /// <summary>
    /// Edit the color scheme until it is saved or closed. The page it was opened from must be drawn again afterwards
    /// </summary>
    internal static void Show()
    {
        _preset = ConsoleColors.GetColorPresetIndex();
        _changed = false;
        ConsoleColors.InitializePreviewColors();
        var selected = 1; // 0 is the preset, 1 and on the groups
        var redraw = true;
        while (true)
        {
            // Moving the selection only moves the pointer (the page is drawn when something else has changed)
            if (Init.ApplyAutoSize()) redraw = true;
            if (redraw)
            {
                Draw(selected);
            }
            else
            {
                MovePointer(selected);
            }

            redraw = true;
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            var count = Groups.Length + 1;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Changes are only kept when saved
                if (_changed && ChoiceBox.Confirm(DialogText.CaptionsColorScheme, DialogText.SaveChangesQuestion,
                        string.Empty, false))
                {
                    Save();
                }

                break;
            }

            if (key.Key == ConsoleKey.S)
            {
                Save();
                break;
            }

            if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.ColorScheme);
            }
            else if (navigation == ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % count;
                redraw = false;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                selected = (selected + count - 1) % count;
                redraw = false;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') selected = 0;
                redraw = false;
            }
            else if (key.KeyChar == 'G')
            {
                selected = count - 1;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.E)
            {
                // Back to the colors of the preset
                UsePreset(_preset);
            }
            else if (selected == 0 && navigation is ConsoleKey.RightArrow or ConsoleKey.LeftArrow ||
                     selected == 0 && key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            {
                var presets = Actions.ColorPresetCount;
                UsePreset((_preset + (navigation == ConsoleKey.LeftArrow ? presets - 1 : 1)) % presets);
            }
            else if (selected > 0 && (navigation == ConsoleKey.RightArrow || key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar))
            {
                Pick(Groups[selected - 1], false);
            }
            else if (selected > 0 && key.Key == ConsoleKey.T)
            {
                Pick(Groups[selected - 1], true);
            }
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    // Start over from the colors of a preset
    private static void UsePreset(int preset)
    {
        _preset = preset;
        ConsoleColors.PreviewPreset(Actions.ColorPresetAt(preset));
        _changed = true;
    }

    private static void Save()
    {
        ConsoleColors.SavePreviewToCurrentColors();
        ConsoleColors.SetColorPresetIndex(_preset);
        FileIo.SaveSettings();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // Pick a color of a group from the list of colors: its color (the text color, or the background of an inverted
    // style), or with "textColor" the text of an inverted style
    private static void Pick(Group group, bool textColor)
    {
        var isInverted = group.Color >= 0 && group.Background >= 0;
        if (textColor && !isInverted) return;
        var index = isInverted && !textColor ? group.Background : group.Color >= 0 ? group.Color : group.Background;
        if (index == 0)
        {
            // The background of the screen: [Default] is the background of the terminal. The colors are shown with
            // the text on them
            if (ColorPicker.Pick(DialogText.BackgroundCaption, PickerLeft, true, ConsoleColors.PreviewBackground,
                    color => ConsoleColors.IsPreviewColorAllowed(0, color), color =>
                    {
                        Console.BackgroundColor = color;
                        Console.ForegroundColor = ConsoleColors.GetPreviewColor(1);
                    }, out var background))
            {
                ConsoleColors.PreviewBackground = background;
                _changed = true;
            }

            return;
        }

        Action<ConsoleColor>? sample = null;
        if (isInverted)
        {
            // The text is shown on the background it will have
            sample = color =>
            {
                Console.BackgroundColor = textColor ? ConsoleColors.GetPreviewColor(group.Background) : color;
                Console.ForegroundColor = textColor ? color : ConsoleColors.GetPreviewColor(group.Color);
            };
        }

        if (ColorPicker.Pick(textColor ? DialogText.TextColorCaption : DialogText.ColorCaption, PickerLeft, false,
                ConsoleColors.GetPreviewColor(index), color => ConsoleColors.IsPreviewColorAllowed(index, color),
                sample, out var picked) && picked != null)
        {
            ConsoleColors.SetPreviewColor(index, picked.Value);
            _changed = true;
        }
    }

    private static void Draw(int selected)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsColorScheme, string.Empty, 0);
        Menu.WriteNameInTopBar(false);

        // The preset the colors are based on
        Tools.SetCursorEx(PresetRow, 3);
        Console.Write(Tools.FitLine(Lang.F("Preset: {0} of {1} ({2})", _preset + 1, Actions.ColorPresetCount,
            Actions.ColorPresetName(_preset)) + "  " + Lang.T("(h/l: the previous/next)"), 3));

        // The groups: name, sample and colors
        var groups = Groups;
        var nameWidth = groups.Max(g => TextMetrics.WidthOf(g.Name)) + 2;
        var sample = " " + Lang.T("Sample") + " ";
        for (var i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            Tools.SetCursorEx(FirstGroupRow + i, 3);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(group.Name + new string(' ', nameWidth - TextMetrics.WidthOf(group.Name)));
            ConsoleColors.SetColorsFromPreview(group.Style);
            Console.Write(sample);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(Tools.FitLine("  " + ColorsOf(group), Console.CursorLeft));
        }

        DrawPreview();
        if (ConsoleColors.Monochrome)
        {
            Tools.SetCursorEx(Init.LineCount - 2, 1);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(Tools.FitLine(DialogText.MonochromeIsOn, 1));
        }

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Menu.WriteKeyHints(DialogText.SchemeEditorKeys);

        // The selection
        _shownSelected = -1;
        MovePointer(selected);
    }

    private static int _shownSelected = -1; // The row with the pointer on the screen (-1: none)

    // The row of the preset (0) or of a group (1 and on)
    private static int RowOf(int selected) => selected == 0 ? PresetRow : FirstGroupRow + selected - 1;

    // Move the selection pointer to a row (only the pointer is drawn)
    private static void MovePointer(int selected)
    {
        if (_shownSelected >= 0 && _shownSelected != selected)
        {
            Tools.SetCursorEx(RowOf(_shownSelected), 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write("  ");
        }

        Tools.SetCursorEx(RowOf(selected), 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Console.Write("->");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        _shownSelected = selected;
        Tools.SetCursorEx(Init.LineCount - 1, 0);
    }

    // The names of the colors of a group
    private static string ColorsOf(Group group)
    {
        if (group.Color < 0)
        {
            return ConsoleColors.PreviewBackground is { } background
                ? ColorPicker.Name(background)
                : DialogText.ColorDefault;
        }

        var text = ColorPicker.Name(ConsoleColors.GetPreviewColor(group.Color));
        return group.Background < 0
            ? text
            : Lang.F("{0}, background {1}", text, ColorPicker.Name(ConsoleColors.GetPreviewColor(group.Background)));
    }

    // A small screen in the colors being edited, on their background
    private static void DrawPreview()
    {
        var top = PreviewTop;
        var width = Init.LineWidth - 2;
        if (top + 5 > Init.LineCount - 2) return;
        for (var row = 0; row < 6; row++)
        {
            Tools.SetCursorEx(top + row, 1);
            ConsoleColors.SetColorsFromPreview(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', width));
        }

        void Part(ConsoleColors.ColorPreset style, string text)
        {
            ConsoleColors.SetColorsFromPreview(style);
            Console.Write(text);
            ConsoleColors.SetColorsFromPreview(ConsoleColors.ColorPreset.Normal);
        }

        Tools.SetCursorEx(top + 1, 2);
        Part(ConsoleColors.ColorPreset.HeaderColor, DialogText.ApplicationName);
        Part(ConsoleColors.ColorPreset.Normal, "  ");
        Part(ConsoleColors.ColorPreset.Divider, "--- " + Lang.T("Dividers") + " ---");
        Part(ConsoleColors.ColorPreset.Normal, "  ");
        Part(ConsoleColors.ColorPreset.EditorTitle, Lang.T("Editor title"));
        Tools.SetCursorEx(top + 2, 2);
        Part(ConsoleColors.ColorPreset.HighlightInverted, "->");
        Part(ConsoleColors.ColorPreset.Normal, " " + Lang.T("Text") + "  ");
        Part(ConsoleColors.ColorPreset.Favorites, Lang.T("Favorites"));
        Part(ConsoleColors.ColorPreset.Normal, "  ");
        Part(ConsoleColors.ColorPreset.PreviewTitle, Lang.T("Preview title"));
        Tools.SetCursorEx(top + 3, 2);
        Part(ConsoleColors.ColorPreset.HighlightInverted, " " + Lang.T("Highlight") + " ");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Part(ConsoleColors.ColorPreset.AttentionInverted, " " + Lang.T("Attention") + " ");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Part(ConsoleColors.ColorPreset.WarningInverted, " " + Lang.T("Warning") + " ");
        Tools.SetCursorEx(top + 4, 2);
        Part(ConsoleColors.ColorPreset.NormalInverted, " " + Lang.T("Key") + " ");
        Part(ConsoleColors.ColorPreset.Normal, " " + Lang.T("Text on the background"));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
