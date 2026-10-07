namespace Marbles;

/// <summary>
/// Thrown when a new screen has been drawn and is waiting for input. It ends whatever led to the screen
/// and is caught by the main loop (see Program), which reads the next key for the screen.
/// </summary>
internal sealed class NavigationException : Exception
{
}

/// <summary>
/// The screens of the application (login page, main menu, lists of entries, entry view) lead to each other:
/// a key pressed in the main menu shows a list, where a key shows an entry, and so on. Instead of the
/// screens calling each other ever deeper, a screen that has been drawn calls Navigation.Wait. This returns
/// control to the main loop, which then passes the keys to the screen of the current mode.
/// </summary>
internal static class Navigation
{
    /// <summary>
    /// Tells the login page to draw itself completely the next time it is shown (false = only refresh
    /// the file and directory selection)
    /// </summary>
    internal static bool LoginFullRedraw { get; set; } = true;

    /// <summary>
    /// The screen of the current mode is drawn and ready: return to the main loop to wait for the next key.
    /// Nothing after a call to this method is executed.
    /// </summary>
    internal static void Wait()
    {
        throw new NavigationException();
    }
}
