namespace Marbles;

/// <summary>
/// What a page of a TabFrame wants after a key
/// </summary>
internal enum TabResult
{
    None, // Only the pointer (or what the page draws by itself) has changed
    Redraw, // Draw the whole frame and page again (something was shown on top, or a setting changed)
    Close, // The frame is done: TabFrame.Run returns
    FirstTab // Go to the first page (as Esc in a page of settings)
}

/// <summary>
/// The place inside the frame where a page draws itself (in console columns and rows)
/// </summary>
internal readonly record struct TabArea(int Top, int Left, int Width, int Height);

/// <summary>
/// A page shown in a TabFrame
/// </summary>
internal interface ITabPage
{
    /// <summary>
    /// The name of the page, shown as a sub-tab when its tab has several pages (looked up when drawn)
    /// </summary>
    string Title { get; }

    /// <summary>
    /// The keys of the page, on the bottom line
    /// </summary>
    string Keys { get; }

    /// <summary>
    /// The section of the help shown with "?"
    /// </summary>
    Tools.HelpTopic Help { get; }

    /// <summary>
    /// Draw the page in the area: all of it (full), or only what has changed (the pointer)
    /// </summary>
    void Draw(TabArea area, bool full);

    TabResult HandleKey(ConsoleKeyInfo key);
}

/// <summary>
/// A tab of a TabFrame: its name, and its pages (with more than one they are shown as sub-tabs)
/// </summary>
internal sealed class TabItem
{
    internal TabItem(Func<string> title, params ITabPage[] pages)
    {
        Title = title;
        Pages = pages;
    }

    internal Func<string> Title { get; }
    internal IReadOnlyList<ITabPage> Pages { get; }
}

/// <summary>
/// A frame in the style of the boxes, with tabs on its top (and sub-tabs in a row inside it), where one page is
/// shown at a time. The tab and sub-tab shown have the colors of the tabs (by default the frame color inverted). Tab and Shift+Tab go through all the pages in order (the sub-tabs of a tab too), "?" shows
/// the help of the page, and the other keys go to the page. The frame goes from its top row to the row above
/// the bottom line, where the keys of the page are shown. The layout is found by TabLayout.
/// </summary>
internal sealed class TabFrame
{
    private readonly int _top;
    private readonly Action? _drawAbove;
    private readonly IReadOnlyList<TabItem> _tabs;

    // All the pages in order, and the tab of each
    private readonly List<(int Tab, ITabPage Page)> _pages = new();

    /// <param name="top">The row of the tops of the tabs (the frame starts two rows below)</param>
    /// <param name="drawAbove">Draws what is above the frame (a logo), after the screen is cleared</param>
    /// <param name="tabs">The tabs</param>
    internal TabFrame(int top, Action? drawAbove, params TabItem[] tabs)
    {
        _top = top;
        _drawAbove = drawAbove;
        _tabs = tabs;
        for (var tab = 0; tab < tabs.Length; tab++)
        {
            foreach (var page in tabs[tab].Pages) _pages.Add((tab, page));
        }
    }

    /// <summary>
    /// The number of the page shown (of all the pages, in order)
    /// </summary>
    internal int Selected { get; set; }

    internal ITabPage Page => _pages[Selected].Page;

    private TabArea _area;

    /// <summary>
    /// Show the frame until a page closes it
    /// </summary>
    internal void Run()
    {
        var full = true;
        while (true)
        {
            Selected = Math.Clamp(Selected, 0, _pages.Count - 1);
            var page = Page;
            if (full)
            {
                Draw();
            }
            else
            {
                page.Draw(_area, false);
            }

            full = false;
            // The page is drawn again if it could not be drawn
            if (Tools.TakeRedrawRequest())
            {
                full = true;
                continue;
            }

            Tools.SetCursorEx(Init.LineCount - 1, 0);
            var key = Console.ReadKey(true);
            // The terminal window has changed size
            if (Init.AutoSize && Init.ApplyAutoSize())
            {
                full = true;
                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                Selected = TabLayout.Next(Selected, _pages.Count, (key.Modifiers & ConsoleModifiers.Shift) != 0);
                full = true;
                continue;
            }

            if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, page.Help);
                full = true;
                continue;
            }

            switch (page.HandleKey(key))
            {
                case TabResult.Close:
                    return;
                case TabResult.Redraw:
                    full = true;
                    break;
                case TabResult.FirstTab:
                    Selected = 0;
                    full = true;
                    break;
            }
        }
    }

    // Draw all: what is above, the tabs, the frame, the sub-tabs, the page and its keys
    private void Draw()
    {
        Init.ApplyAutoSize();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Tools.HideCursor();
        _drawAbove?.Invoke();
        var (tab, page) = _pages[Selected];
        var width = Init.LineWidth - 2; // From column 1, with a corner at each end
        var right = width; // The column of the right side
        var bottom = Init.LineCount - 2;
        var names = TabLayout.Fit(_tabs.Select(item => item.Title()).ToList(), width);
        var (tops, labels, starts) = TabLayout.Tabs(names, AsciiStyles.GetTopCorner());

        // The tabs, the name of the tab shown highlighted
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_top, 1);
        Console.Write(tops);
        Tools.SetCursorEx(_top + 1, 1);
        Console.Write(labels);
        for (var i = 0; i < names.Count; i++)
        {
            Tools.SetCursorEx(_top + 1, 1 + starts[i] - 1);
            // The name of the tab shown in the colors of the frame, inverted
            if (i == tab) ConsoleColors.SetTabColors();
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + names[i] + " ");
        }

        // The frame
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_top + 2, 1);
        Console.Write(TabLayout.Edge(names, tab, width, AsciiStyles.GetTopCorner(), AsciiStyles.GetBottomCorner()));
        for (var row = _top + 3; row < bottom; row++)
        {
            Tools.SetCursorEx(row, 1);
            Console.Write('|');
            Tools.SetCursorEx(row, right);
            Console.Write('|');
        }

        Tools.SetCursorEx(bottom, 1);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', width - 2) + AsciiStyles.GetBottomCorner());

        // The sub-tabs of a tab with several pages, in a row with a line below it
        var contentTop = _top + 4;
        var pages = _tabs[tab].Pages;
        if (pages.Count > 1)
        {
            var subNames = pages.Select(item => item.Title).ToList();
            var subStarts = TabLayout.SubTabStarts(subNames);
            for (var i = 0; i < subNames.Count; i++)
            {
                Tools.SetCursorEx(_top + 3, 3 + subStarts[i] - 1);
                if (ReferenceEquals(pages[i], page)) ConsoleColors.SetTabColors();
                else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" " + subNames[i] + " ");
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(_top + 4, 3);
            Console.Write(new string('-', Math.Max(0, width - 4)));
            contentTop = _top + 5;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        _area = new TabArea(contentTop, 3, Math.Max(0, width - 4), Math.Max(0, bottom - contentTop));
        page.Draw(_area, true);

        // The keys of the page
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(" " + Tools.TruncateString(page.Keys, Init.LineWidth - 2, false));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
