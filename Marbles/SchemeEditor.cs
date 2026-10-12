namespace Marbles;

/// <summary>
/// The color scheme editor: a page of its own, opened from the Configuration page and with ":scheme" in the main
/// menu. It is a frame with two tabs (TabFrame): Colors, where a preset is chosen at the top and the colors of each
/// group below it are picked from a list, and Preview, a small screen in the colors being edited (also a frame
/// with tabs). Nothing is used until it is saved.
/// </summary>
internal static class SchemeEditor
{
    // A group of the color scheme: its name, the style it is shown in, and where its colors are kept among the
    // colors being edited (the text, and for the inverted styles the background; -1 if there is none). The tabs
    // have colors of their own, which by default are those of the frames (Tab)
    private sealed record Group(string Name, ConsoleColors.ColorPreset Style, int Color, int Background,
        bool Tab = false);

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
        new Group(Lang.T("Tabs"), ConsoleColors.ColorPreset.Divider, -1, -1, true),
        // The background of the whole screen (or the one of the terminal)
        new Group(Lang.T("Background"), ConsoleColors.ColorPreset.Normal, -1, 0)
    };

    private const int TabRow = 2; // Below the top bar
    private const int PickerLeft = 52;

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
        var frame = new TabFrame(TabRow, () =>
            {
                Menu.WriteModeLabel(DialogText.CaptionsColorScheme, string.Empty, 0);
                Menu.WriteNameInTopBar(false);
            },
            new TabItem(() => Lang.T("Colors"), new ColorsPage()),
            new TabItem(() => Lang.T("Preview"), new PreviewPage()));
        frame.Run();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    // Esc or Q: changes are only kept when saved
    private static TabResult Close()
    {
        if (_changed && ChoiceBox.Confirm(DialogText.CaptionsColorScheme, DialogText.SaveChangesQuestion,
                string.Empty, false))
        {
            Save();
        }

        return TabResult.Close;
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

    /// <summary>
    /// The Colors tab: the preset at the top, and the groups of colors below it
    /// </summary>
    private sealed class ColorsPage : ITabPage
    {
        private int _selected = 1; // 0 is the preset, 1 and on the groups
        private int _shown = -1; // The row with the pointer on the screen (-1: none)

        public string Title => Lang.T("Colors");
        public string Keys => DialogText.SchemeEditorKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.ColorScheme;

        // The row of the preset (0) or of a group (1 and on)
        private static int RowOf(TabArea area, int selected) => area.Top + (selected == 0 ? 0 : selected + 1);

        public void Draw(TabArea area, bool full)
        {
            if (full)
            {
                // The preset the colors are based on
                Tools.SetCursorEx(area.Top, area.Left + 3);
                Console.Write(Tools.TruncateString(Lang.F("Preset: {0} of {1} ({2})", _preset + 1,
                    Actions.ColorPresetCount, Actions.ColorPresetName(_preset)) + "  " +
                    Lang.T("(h/l: the previous/next)"), area.Width - 3, true));

                // The groups: name, sample and colors
                var groups = Groups;
                var nameWidth = groups.Max(g => TextMetrics.WidthOf(g.Name)) + 2;
                var sample = " " + Lang.T("Sample") + " ";
                for (var i = 0; i < groups.Length; i++)
                {
                    var group = groups[i];
                    Tools.SetCursorEx(RowOf(area, i + 1), area.Left + 3);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(group.Name + new string(' ', nameWidth - TextMetrics.WidthOf(group.Name)));
                    if (group.Tab) ConsoleColors.SetTabColors(true);
                    else ConsoleColors.SetColorsFromPreview(group.Style);
                    Console.Write(sample);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    var room = area.Left + area.Width - Console.CursorLeft;
                    Console.Write(Tools.TruncateString("  " + ColorsOf(group), Math.Max(0, room), true));
                }

                if (ConsoleColors.Monochrome && area.Height > groups.Length + 3)
                {
                    Tools.SetCursorEx(area.Top + area.Height - 1, area.Left + 3);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    Console.Write(Tools.TruncateString(DialogText.MonochromeIsOn, area.Width - 3, true));
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }

                _shown = -1;
            }

            // The pointer
            if (_shown >= 0 && _shown != _selected)
            {
                Tools.SetCursorEx(RowOf(area, _shown), area.Left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write("  ");
            }

            Tools.SetCursorEx(RowOf(area, _selected), area.Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write("->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            _shown = _selected;
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var navigation = Keyboard.ToNavigationKey(key).Key;
            var count = Groups.Length + 1;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            if (key.Key == ConsoleKey.S)
            {
                Save();
                return TabResult.Close;
            }

            if (navigation == ConsoleKey.DownArrow)
            {
                _selected = (_selected + 1) % count;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _selected = (_selected + count - 1) % count;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _selected = 0;
            }
            else if (key.KeyChar == 'G')
            {
                _selected = count - 1;
            }
            else if (key.Key == ConsoleKey.E)
            {
                // Back to the colors of the preset
                UsePreset(_preset);
                return TabResult.Redraw;
            }
            else if (_selected == 0 && navigation is ConsoleKey.RightArrow or ConsoleKey.LeftArrow ||
                     _selected == 0 && key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            {
                var presets = Actions.ColorPresetCount;
                UsePreset((_preset + (navigation == ConsoleKey.LeftArrow ? presets - 1 : 1)) % presets);
                return TabResult.Redraw;
            }
            else if (_selected > 0 &&
                     (navigation == ConsoleKey.RightArrow || key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar))
            {
                Pick(Groups[_selected - 1], false);
                return TabResult.Redraw;
            }
            else if (_selected > 0 && key.Key == ConsoleKey.T)
            {
                Pick(Groups[_selected - 1], true);
                return TabResult.Redraw;
            }

            return TabResult.None;
        }
    }

    /// <summary>
    /// The Preview tab: a small screen in the colors being edited, with a frame with tabs
    /// </summary>
    private sealed class PreviewPage : ITabPage
    {
        public string Title => Lang.T("Preview");
        public string Keys => DialogText.SchemePreviewKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.ColorScheme;

        public void Draw(TabArea area, bool full)
        {
            if (full) DrawPreview(area);
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            if (key.Key != ConsoleKey.S) return TabResult.None;
            Save();
            return TabResult.Close;
        }
    }

    // Pick a color of a group from the list of colors: its color (the text color, or the background of an inverted
    // style), or with "textColor" the text of an inverted style
    private static void Pick(Group group, bool textColor)
    {
        if (group.Tab)
        {
            PickTabColor(textColor);
            return;
        }

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

    // The colors of the tab shown: its background (l, [Default]: the color of the frames) or its text (T,
    // [Default]: the background of the screen). The text cannot have the color of the background
    private static void PickTabColor(bool textColor)
    {
        var (background, text) = ConsoleColors.TabColors(true);
        var current = textColor ? ConsoleColors.PreviewTabTextColor : ConsoleColors.PreviewTabColor;
        if (!ColorPicker.Pick(textColor ? DialogText.TextColorCaption : DialogText.ColorCaption, PickerLeft, true,
                current, color => color != (textColor ? background : text), color =>
                {
                    Console.BackgroundColor = textColor ? background : color;
                    Console.ForegroundColor = textColor ? color : text;
                }, out var picked))
        {
            return;
        }

        if (textColor) ConsoleColors.PreviewTabTextColor = picked;
        else ConsoleColors.PreviewTabColor = picked;
        _changed = true;
    }

    // The names of the colors of a group
    private static string ColorsOf(Group group)
    {
        if (group.Tab)
        {
            // By default the colors of the dividers inverted
            if (ConsoleColors.PreviewTabColor == null && ConsoleColors.PreviewTabTextColor == null)
            {
                return Lang.T("[Default]: the dividers inverted");
            }

            return Lang.F("{0}, background {1}",
                ConsoleColors.PreviewTabTextColor is { } tabText ? ColorPicker.Name(tabText) : DialogText.ColorDefault,
                ConsoleColors.PreviewTabColor is { } tabColor ? ColorPicker.Name(tabColor) : DialogText.ColorDefault);
        }

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

    // A small screen in the colors being edited, on their background: the colors of the groups, and a frame with
    // tabs (the tab and sub-tab shown in the colors of the tabs)
    private static void DrawPreview(TabArea area)
    {
        var left = area.Left;
        var width = area.Width;
        for (var row = 0; row < area.Height; row++)
        {
            Tools.SetCursorEx(area.Top + row, left);
            ConsoleColors.SetColorsFromPreview(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', width));
        }

        void Part(ConsoleColors.ColorPreset style, string text)
        {
            ConsoleColors.SetColorsFromPreview(style);
            Console.Write(text);
            ConsoleColors.SetColorsFromPreview(ConsoleColors.ColorPreset.Normal);
        }

        void Tab(string text)
        {
            ConsoleColors.SetTabColors(true);
            Console.Write(text);
            ConsoleColors.SetColorsFromPreview(ConsoleColors.ColorPreset.Normal);
        }

        var top = area.Top;
        Tools.SetCursorEx(top + 1, left + 1);
        Part(ConsoleColors.ColorPreset.HeaderColor, DialogText.ApplicationName);
        Part(ConsoleColors.ColorPreset.Normal, "  ");
        Part(ConsoleColors.ColorPreset.Divider, "--- " + Lang.T("Dividers") + " ---");
        Part(ConsoleColors.ColorPreset.Normal, "  ");
        Part(ConsoleColors.ColorPreset.EditorTitle, Lang.T("Editor title"));
        Tools.SetCursorEx(top + 2, left + 1);
        Part(ConsoleColors.ColorPreset.HighlightInverted, "->");
        Part(ConsoleColors.ColorPreset.Normal, " " + Lang.T("Text") + "  ");
        Part(ConsoleColors.ColorPreset.Favorites, Lang.T("Favorites"));
        Part(ConsoleColors.ColorPreset.Normal, "  ");
        Part(ConsoleColors.ColorPreset.PreviewTitle, Lang.T("Preview title"));
        Tools.SetCursorEx(top + 3, left + 1);
        Part(ConsoleColors.ColorPreset.HighlightInverted, " " + Lang.T("Highlight") + " ");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Part(ConsoleColors.ColorPreset.AttentionInverted, " " + Lang.T("Attention") + " ");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Part(ConsoleColors.ColorPreset.WarningInverted, " " + Lang.T("Warning") + " ");
        Tools.SetCursorEx(top + 4, left + 1);
        Part(ConsoleColors.ColorPreset.NormalInverted, " " + Lang.T("Key") + " ");
        Part(ConsoleColors.ColorPreset.Normal, " " + Lang.T("Text on the background"));

        // A frame with tabs, as the start page and this editor
        if (area.Height < 12) return;
        var names = new[] { Lang.F("Tab {0}", 1), Lang.F("Tab {0}", 2) };
        var frameWidth = Math.Min(48, width - 2);
        var (tops, labels, starts) = TabLayout.Tabs(names, AsciiStyles.GetTopCorner());
        var x = left + 1;
        Tools.SetCursorEx(top + 6, x);
        Part(ConsoleColors.ColorPreset.Divider, tops);
        Tools.SetCursorEx(top + 7, x);
        Part(ConsoleColors.ColorPreset.Divider, labels);
        Tools.SetCursorEx(top + 7, x + starts[0] - 1);
        Tab(" " + names[0] + " ");
        Tools.SetCursorEx(top + 7, x + starts[1] - 1);
        Part(ConsoleColors.ColorPreset.Normal, " " + names[1] + " ");
        Tools.SetCursorEx(top + 8, x);
        Part(ConsoleColors.ColorPreset.Divider,
            TabLayout.Edge(names, 0, frameWidth, AsciiStyles.GetTopCorner(), AsciiStyles.GetBottomCorner()));
        Tools.SetCursorEx(top + 9, x);
        Part(ConsoleColors.ColorPreset.Divider, "|");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Tab(" " + Lang.F("Page {0}", 1) + " ");
        Part(ConsoleColors.ColorPreset.Normal, "  " + Lang.F("Page {0}", 2) + " ");
        Tools.SetCursorEx(top + 9, x + frameWidth - 1);
        Part(ConsoleColors.ColorPreset.Divider, "|");
        Tools.SetCursorEx(top + 10, x);
        Part(ConsoleColors.ColorPreset.Divider,
            AsciiStyles.GetBottomCorner() + new string('-', frameWidth - 2) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
