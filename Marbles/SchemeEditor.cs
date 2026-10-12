namespace Marbles;

/// <summary>
/// The color schemes: a page of their own, opened from the Configuration page and with ":scheme" in the main
/// menu. It is a frame with three tabs (TabFrame): Schemes, where a scheme is selected and used (the built-in
/// ones and the ones saved by the user), Colors, where the colors of the scheme are picked (on two pages), and
/// Preview, a small screen in the colors being edited. Edited colors are only used when they are saved, as a
/// scheme of the user's own (a file in the directory "colorschemes"), which is then the scheme in use.
/// </summary>
internal static class SchemeEditor
{
    // A group of colors: its name, its color (for the inverted ones the background) and the text on it, and the
    // style its sample is shown in
    private sealed record Group(string Name, ColorItem Color, ColorItem? Text, ConsoleColors.ColorPreset Style);

    // The groups on the two pages of the Colors tab
    private static Group[] GroupsOf(int page) => page == 0
        ? new[]
        {
            new Group(Lang.T("Text"), ColorItem.Text, null, ConsoleColors.ColorPreset.Normal),
            new Group(Lang.T("Logo and labels"), ColorItem.Logo, null, ConsoleColors.ColorPreset.HeaderColor),
            new Group(Lang.T("Dividers"), ColorItem.Dividers, null, ConsoleColors.ColorPreset.Divider),
            new Group(Lang.T("Favorites"), ColorItem.Favorites, null, ConsoleColors.ColorPreset.Favorites),
            new Group(Lang.T("Preview title"), ColorItem.PreviewTitle, null, ConsoleColors.ColorPreset.PreviewTitle),
            new Group(Lang.T("Editor title"), ColorItem.EditorTitle, null, ConsoleColors.ColorPreset.EditorTitle),
            // The background of the whole screen (or the one of the terminal)
            new Group(Lang.T("Background"), ColorItem.Background, null, ConsoleColors.ColorPreset.Normal)
        }
        : new[]
        {
            new Group(Lang.T("Highlight"), ColorItem.Highlight, ColorItem.HighlightText,
                ConsoleColors.ColorPreset.HighlightInverted),
            new Group(Lang.T("Attention"), ColorItem.Attention, ColorItem.AttentionText,
                ConsoleColors.ColorPreset.AttentionInverted),
            new Group(Lang.T("Warning"), ColorItem.Warning, ColorItem.WarningText,
                ConsoleColors.ColorPreset.WarningInverted),
            new Group(Lang.T("Tab frames"), ColorItem.TabFrames, null, ConsoleColors.ColorPreset.TabFrame),
            new Group(Lang.T("Box borders"), ColorItem.BoxBorders, null, ConsoleColors.ColorPreset.BoxBorder),
            new Group(Lang.T("Tab highlight"), ColorItem.Tab, ColorItem.TabText, ConsoleColors.ColorPreset.Normal),
            new Group(Lang.T("Tab focus"), ColorItem.TabShown, null, ConsoleColors.ColorPreset.TabShown),
            new Group(Lang.T("Other tabs"), ColorItem.OtherTabs, null, ConsoleColors.ColorPreset.OtherTabs)
        };

    private const int TabRow = 2; // Below the top bar
    private const int PickerLeft = 52;

    private static bool _changed; // The colors being edited are not saved
    private static bool _selectInUse; // The list of schemes selects the one in use when it is drawn again

    private static ColorScheme Preview => ConsoleColors.Preview;

    /// <summary>
    /// Select and edit the color schemes until the page is closed. The page it was opened from must be drawn
    /// again afterwards
    /// </summary>
    internal static void Show()
    {
        ConsoleColors.Preview = ConsoleColors.Current.Clone();
        _changed = false;
        var frame = new TabFrame(TabRow, () =>
            {
                Menu.WriteModeLabel(DialogText.CaptionsColorScheme, string.Empty, 0);
                Menu.WriteNameInTopBar(false);
            },
            new TabItem(() => Lang.T("Schemes"), new SchemesPage()),
            new TabItem(() => Lang.T("Colors"), new ColorsPage(0), new ColorsPage(1)),
            new TabItem(() => Lang.T("Preview"), new PreviewPage()));
        frame.Run();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    /// <summary>
    /// Use a color scheme by its name (":scheme Name")
    /// </summary>
    /// <returns>False if there is no scheme with the name</returns>
    internal static bool UseByName(string name)
    {
        var scheme = ColorSchemes.Load(name);
        if (scheme == null) return false;
        Use(scheme);
        return true;
    }

    // Use a scheme: it is shown at once, and its name is saved in the settings
    private static void Use(ColorScheme scheme)
    {
        ConsoleColors.Use(scheme);
        FileIo.SaveSettings();
        _selectInUse = true;
    }

    // Esc or Q: changes that are not saved are asked about
    private static TabResult Close()
    {
        if (!_changed) return TabResult.Close;
        switch (Menu.YesNoCancelPrompt(DialogText.CaptionsColorScheme, DialogText.SaveChangesQuestion, string.Empty,
                    false))
        {
            case 1:
                return Save() ? TabResult.Close : TabResult.Redraw;
            case 2:
                return TabResult.Close;
            default:
                return TabResult.Redraw;
        }
    }

    // Save the colors being edited as a scheme of the user's own (the name is asked for), and use it
    private static bool Save()
    {
        var suggested = ColorSchemes.IsBuiltIn(Preview.Name) ? string.Empty : Preview.Name;
        while (true)
        {
            ChoiceBox.Forget();
            var name = ChoiceBox.AskText(DialogText.SchemeSaveCaption, DialogText.SchemeSavePrompt,
                DialogText.NameColon, suggested);
            if (name == null) return false;
            suggested = name;
            if (!ColorSchemes.IsValidName(name))
            {
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.SchemeSaveCaption,
                    ColorSchemes.IsBuiltIn(name) ? DialogText.SchemeNameBuiltIn : DialogText.SchemeNameInvalid,
                    string.Empty);
                continue;
            }

            // Another scheme of the user's own with the name is replaced, if the user wants that
            if (ColorSchemes.Exists(name) && !name.Equals(Preview.Name, StringComparison.OrdinalIgnoreCase))
            {
                ChoiceBox.Forget();
                if (!ChoiceBox.Confirm(DialogText.SchemeSaveCaption,
                        Lang.F("Replace the color scheme \"{0}\"?", name), string.Empty, true))
                {
                    continue;
                }
            }

            Preview.Name = name.Trim();
            var status = ColorSchemes.Save(Preview);
            if (status != DialogText.Success)
            {
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.SchemeSaveCaption, status, string.Empty);
                return false;
            }

            Use(Preview);
            _changed = false;
            return true;
        }
    }

    /// <summary>
    /// The Schemes tab: the built-in schemes and the ones of the user's own, the one in use marked
    /// </summary>
    private sealed class SchemesPage : ITabPage
    {
        private int _selected = -1;
        private int _scroll;
        private int _shown = -1;

        public string Title => Lang.T("Schemes");
        public string Keys => DialogText.SchemeListKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.ColorScheme;

        private static List<string> Names => ColorSchemes.AllNames();

        // The rows of the list (the last row of the page tells where the schemes are kept)
        private static int Rows(TabArea area) => Math.Max(1, area.Height - 2);

        public void Draw(TabArea area, bool full)
        {
            var names = Names;
            if (_selectInUse)
            {
                _selected = -1;
                _selectInUse = false;
            }

            if (_selected < 0)
            {
                // The scheme in use is selected from the start
                _selected = Math.Max(0, names.FindIndex(name =>
                    name.Equals(ConsoleColors.Current.Name, StringComparison.OrdinalIgnoreCase)));
            }

            _selected = Math.Clamp(_selected, 0, Math.Max(0, names.Count - 1));
            var rows = Rows(area);
            var scroll = Math.Clamp(_scroll, Math.Max(0, _selected - rows + 1), _selected);
            if (scroll != _scroll || full)
            {
                _scroll = scroll;
                var builtIn = ColorScheme.BuiltIn.Count;
                for (var row = 0; row < rows; row++)
                {
                    var i = _scroll + row;
                    Tools.SetCursorEx(area.Top + row, area.Left);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(new string(' ', area.Width));
                    if (i >= names.Count) continue;
                    Tools.SetCursorEx(area.Top + row, area.Left + 3);
                    var inUse = names[i].Equals(ConsoleColors.Current.Name, StringComparison.OrdinalIgnoreCase);
                    Console.Write((inUse ? "[x] " : "[ ] ") + ColorSchemes.DisplayName(names[i]));
                    if (i < builtIn)
                    {
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                        Console.Write("  " + Lang.T("(built-in)"));
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    }
                }

                // Where the schemes of the user's own are kept
                Tools.SetCursorEx(area.Top + area.Height - 1, area.Left + 3);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(Tools.TruncateString(Lang.F("Your own schemes are saved in {0}",
                    Path.Combine(FileIo.GetDataDirectoryName(), ColorSchemes.DirectoryName)), area.Width - 3, true));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                _shown = -1;
            }

            // The pointer
            if (_shown >= 0 && _shown != _selected)
            {
                Tools.SetCursorEx(area.Top + _shown - _scroll, area.Left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write("  ");
            }

            Tools.SetCursorEx(area.Top + _selected - _scroll, area.Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write("->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            _shown = _selected;
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var names = Names;
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            if (key.Key == ConsoleKey.S)
            {
                if (_changed) Save();
                return TabResult.Redraw;
            }

            if (navigation == ConsoleKey.DownArrow)
            {
                _selected = Math.Min(_selected + 1, names.Count - 1);
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _selected = Math.Max(_selected - 1, 0);
            }
            else if (key.KeyChar == 'G')
            {
                _selected = names.Count - 1;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _selected = 0;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar || navigation == ConsoleKey.RightArrow)
            {
                return UseSelected(names[_selected]);
            }
            else if (key.Key == ConsoleKey.D)
            {
                return Delete(names[_selected]);
            }

            return TabResult.None;
        }

        // Use the selected scheme (its colors are then the ones edited). Changes that are not saved are given up
        // if the user wants that
        private static TabResult UseSelected(string name)
        {
            if (_changed)
            {
                ChoiceBox.Forget();
                if (!ChoiceBox.Confirm(DialogText.CaptionsColorScheme, DialogText.SchemeDiscardQuestion, string.Empty,
                        true))
                {
                    return TabResult.Redraw;
                }
            }

            var scheme = ColorSchemes.Load(name);
            if (scheme == null) return TabResult.Redraw;
            Use(scheme);
            ConsoleColors.Preview = scheme.Clone();
            _changed = false;
            return TabResult.Redraw;
        }

        // Delete a scheme of the user's own (the built-in ones cannot be deleted)
        private TabResult Delete(string name)
        {
            ChoiceBox.Forget();
            if (ColorSchemes.IsBuiltIn(name))
            {
                ChoiceBox.Message(DialogText.CaptionsColorScheme, DialogText.SchemeBuiltInDelete, string.Empty);
                return TabResult.Redraw;
            }

            if (!ChoiceBox.Confirm(DialogText.CaptionsColorScheme, Lang.F("Delete the color scheme \"{0}\"?", name),
                    DialogText.SchemeDeleteNote, true))
            {
                return TabResult.Redraw;
            }

            var status = ColorSchemes.Delete(name);
            if (status != DialogText.Success)
            {
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.CaptionsColorScheme, status, string.Empty);
                return TabResult.Redraw;
            }

            // The scheme in use was deleted: the default is used
            if (name.Equals(ConsoleColors.Current.Name, StringComparison.OrdinalIgnoreCase))
            {
                Use(ColorScheme.Default);
                if (!_changed) ConsoleColors.Preview = ColorScheme.Default;
            }

            _selected = Math.Max(0, _selected - 1);
            return TabResult.Redraw;
        }
    }

    /// <summary>
    /// A page of the Colors tab: the name of the scheme being edited at the top, and the groups of colors below it
    /// </summary>
    private sealed class ColorsPage : ITabPage
    {
        private readonly int _page;
        private int _selected;
        private int _shown = -1;

        internal ColorsPage(int page)
        {
            _page = page;
        }

        public string Title => Lang.F("Page {0}", _page + 1);
        public string Keys => DialogText.SchemeEditorKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.ColorScheme;

        private Group[] Groups => GroupsOf(_page);

        // The rows that can be selected: the groups, and on the second page the highlight of the tabs (on/off)
        private int Count => Groups.Length + (_page == 1 ? 1 : 0);
        private bool OnHighlight => _page == 1 && _selected == Groups.Length;

        private static int RowOf(TabArea area, int selected) => area.Top + 2 + selected;

        public void Draw(TabArea area, bool full)
        {
            if (full)
            {
                // The scheme being edited
                Tools.SetCursorEx(area.Top, area.Left + 3);
                Console.Write(Tools.TruncateString(
                    Lang.F("Color scheme: {0}", ColorSchemes.DisplayName(Preview.Name)) +
                    (_changed ? "  " + Lang.T("(changed, S: save)") : string.Empty), area.Width - 3, true));

                // The groups: name, sample and colors
                var groups = Groups;
                var nameWidth = GroupsOf(0).Concat(GroupsOf(1)).Max(g => TextMetrics.WidthOf(g.Name)) + 2;
                var sample = " " + Lang.T("Sample") + " ";
                for (var i = 0; i < groups.Length; i++)
                {
                    var group = groups[i];
                    Tools.SetCursorEx(RowOf(area, i), area.Left + 3);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(group.Name + new string(' ', nameWidth - TextMetrics.WidthOf(group.Name)));
                    if (group.Color == ColorItem.Tab) ConsoleColors.SetTabColors(true);
                    else ConsoleColors.SetColorsFromPreview(group.Style);
                    Console.Write(sample);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    var room = area.Left + area.Width - Console.CursorLeft;
                    Console.Write(Tools.TruncateString("  " + ColorsOf(group), Math.Max(0, room), true));
                }

                if (_page == 1)
                {
                    // The highlight of the tab shown, on or off
                    Tools.SetCursorEx(RowOf(area, groups.Length), area.Left + 3);
                    Console.Write((Preview.TabHighlight ? "[x] " : "[ ] ") + Lang.T("Highlight focused tabs"));
                }

                if (ConsoleColors.Monochrome && area.Height > Count + 3)
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
            var count = Count;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            if (key.Key == ConsoleKey.S)
            {
                Save();
                return TabResult.Redraw;
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
                // Back to the colors of the scheme in use
                ConsoleColors.Preview = ConsoleColors.Current.Clone();
                _changed = false;
                return TabResult.Redraw;
            }
            else if (OnHighlight && (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar ||
                                     navigation == ConsoleKey.RightArrow))
            {
                Preview.TabHighlight = !Preview.TabHighlight;
                _changed = true;
                return TabResult.Redraw;
            }
            else if (!OnHighlight &&
                     (navigation == ConsoleKey.RightArrow || key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar))
            {
                Pick(Groups[_selected].Color);
                return TabResult.Redraw;
            }
            else if (!OnHighlight && key.Key == ConsoleKey.T && Groups[_selected].Text is { } text)
            {
                Pick(text);
                return TabResult.Redraw;
            }

            return TabResult.None;
        }
    }

    /// <summary>
    /// The Preview tab: a small screen in the colors being edited, with a frame with tabs and a box
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
            return TabResult.Redraw;
        }
    }

    // Pick a color from the list of colors. The colors that would not be visible (on their background, or with
    // the text on them) cannot be picked. [Default] is there for the colors that can have it
    private static void Pick(ColorItem item)
    {
        var caption = item switch
        {
            ColorItem.Background => DialogText.BackgroundCaption,
            ColorItem.HighlightText or ColorItem.AttentionText or ColorItem.WarningText or ColorItem.TabText =>
                DialogText.TextColorCaption,
            _ => DialogText.ColorCaption
        };

        // The colors are shown as they would look
        void Sample(ConsoleColor color)
        {
            var scheme = Preview.Clone();
            scheme.Set(item, color);
            switch (item)
            {
                case ColorItem.Highlight or ColorItem.HighlightText:
                    Console.BackgroundColor = scheme.Highlight;
                    Console.ForegroundColor = scheme.HighlightText;
                    break;
                case ColorItem.Attention or ColorItem.AttentionText:
                    Console.BackgroundColor = scheme.Attention;
                    Console.ForegroundColor = scheme.AttentionText;
                    break;
                case ColorItem.Warning or ColorItem.WarningText:
                    Console.BackgroundColor = scheme.Warning;
                    Console.ForegroundColor = scheme.WarningText;
                    break;
                case ColorItem.Tab or ColorItem.TabText:
                    Console.BackgroundColor = scheme.TabColor;
                    Console.ForegroundColor = scheme.TabTextColor;
                    break;
                case ColorItem.Background:
                    Console.BackgroundColor = color;
                    Console.ForegroundColor = scheme.Text;
                    break;
                default:
                    Console.BackgroundColor = scheme.BackgroundColor;
                    Console.ForegroundColor = color;
                    break;
            }
        }

        if (!ColorPicker.Pick(caption, PickerLeft, ColorScheme.CanBeDefault(item), Preview.Get(item),
                color => Preview.Allowed(item, color), Sample, out var picked))
        {
            return;
        }

        if (picked == null && !ColorScheme.CanBeDefault(item)) return;
        Preview.Set(item, picked);
        _changed = true;
    }

    // The names of the colors of a group
    private static string ColorsOf(Group group)
    {
        string Name(ColorItem item)
        {
            if (Preview.Get(item) is { } color) return ColorPicker.Name(color);
            return item switch
            {
                ColorItem.TabFrames or ColorItem.BoxBorders => Lang.T("[Default]: the dividers"),
                ColorItem.TabShown or ColorItem.OtherTabs => Lang.T("[Default]: the text"),
                _ => DialogText.ColorDefault
            };
        }

        if (group.Color == ColorItem.Tab && Preview.Tab == null && Preview.TabText == null)
        {
            return Lang.T("[Default]: Tab focus on the tab frames");
        }

        return group.Text is { } text
            ? Lang.F("{0}, background {1}", Name(text), Name(group.Color))
            : Name(group.Color);
    }

    // A small screen in the colors being edited, on their background: the colors of the groups, a frame with
    // tabs, and a box with its title in a tab
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

        // A frame with tabs (in the color of the tab frames), as the start page and this page
        if (area.Height < 12) return;
        var names = new[] { Lang.F("Tab {0}", 1), Lang.F("Tab {0}", 2) };
        var frameWidth = Math.Min(44, width / 2 + 4);
        var (tops, labels, starts) = TabLayout.Tabs(names, AsciiStyles.GetTopCorner());
        var x = left + 1;
        Tools.SetCursorEx(top + 6, x);
        Part(ConsoleColors.ColorPreset.TabFrame, tops);
        Tools.SetCursorEx(top + 7, x);
        Part(ConsoleColors.ColorPreset.TabFrame, labels);
        Tools.SetCursorEx(top + 7, x + starts[0] - 1);
        Tab(" " + names[0] + " ");
        Tools.SetCursorEx(top + 7, x + starts[1] - 1);
        Part(ConsoleColors.ColorPreset.OtherTabs, " " + names[1] + " ");
        Tools.SetCursorEx(top + 8, x);
        Part(ConsoleColors.ColorPreset.TabFrame,
            TabLayout.Edge(names, 0, frameWidth, AsciiStyles.GetTopCorner(), AsciiStyles.GetBottomCorner()));
        Tools.SetCursorEx(top + 9, x);
        Part(ConsoleColors.ColorPreset.TabFrame, "|");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Tab(" " + Lang.F("Page {0}", 1) + " ");
        Part(ConsoleColors.ColorPreset.Normal, " ");
        Part(ConsoleColors.ColorPreset.OtherTabs, " " + Lang.F("Page {0}", 2) + " ");
        Tools.SetCursorEx(top + 9, x + frameWidth - 1);
        Part(ConsoleColors.ColorPreset.TabFrame, "|");
        Tools.SetCursorEx(top + 10, x);
        Part(ConsoleColors.ColorPreset.TabFrame,
            AsciiStyles.GetBottomCorner() + new string('-', frameWidth - 2) + AsciiStyles.GetBottomCorner());

        // A box with its title in a tab (in the color of the box borders), to the right of it
        var boxLeft = x + frameWidth + 2;
        var inner = Math.Min(24, left + width - boxLeft - 3);
        if (inner < 12) return;
        var title = Tools.TruncateString(Lang.T("Box"), inner - 4, true);
        var titleWidth = TextMetrics.WidthOf(title);
        Tools.SetCursorEx(top + 6, boxLeft);
        Part(ConsoleColors.ColorPreset.BoxBorder,
            AsciiStyles.GetTopCorner() + new string('-', titleWidth + 2) + AsciiStyles.GetTopCorner());
        Tools.SetCursorEx(top + 7, boxLeft);
        Part(ConsoleColors.ColorPreset.BoxBorder, "|");
        Tab(" " + title + " ");
        Part(ConsoleColors.ColorPreset.BoxBorder, AsciiStyles.GetBottomCorner() +
                                                 new string('-', Math.Max(0, inner - titleWidth - 3)) +
                                                 AsciiStyles.GetTopCorner());
        for (var row = 8; row <= 9; row++)
        {
            Tools.SetCursorEx(top + row, boxLeft);
            Part(ConsoleColors.ColorPreset.BoxBorder, "|");
            Part(ConsoleColors.ColorPreset.Normal, (row == 8 ? "  " + Lang.T("Text") : string.Empty).PadRight(inner));
            Part(ConsoleColors.ColorPreset.BoxBorder, "|");
        }

        Tools.SetCursorEx(top + 10, boxLeft);
        Part(ConsoleColors.ColorPreset.BoxBorder,
            AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
