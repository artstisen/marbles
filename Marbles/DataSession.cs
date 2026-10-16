namespace Marbles;

/// <summary>
/// The session of the data file that is logged in to. Normally it ends when Marbles goes from the notes to something
/// else (a text file opened in the editor, a jump to a file, a directory or a page of Configuration): the notes are
/// cleared from memory, and the password is asked for again.
///
/// With "Stay logged into data file (Session)" on (setting "stayloggedin"), the session is kept until the user
/// logs out (Logout in the main menu, ":logout" in the file browser), chooses another data file, or exits: text files
/// are edited and pages are shown with the data file open, and the main menu of the data file is shown again
/// afterwards. The string memory is kept too, so text can be yanked in a note and put in a file (and back).
/// </summary>
internal static class DataSession
{
    /// <summary>
    /// Stay logged into the data file until logging out (setting "stayloggedin")
    /// </summary>
    internal static bool StayLoggedIn { get; set; }

    /// <summary>
    /// A data file is logged in to (its notes are in memory)
    /// </summary>
    internal static bool IsLoggedIn => Encryptor.HasKey;

    /// <summary>
    /// Leaving the notes for files or pages keeps the data file open
    /// </summary>
    internal static bool Keep => StayLoggedIn && IsLoggedIn;

    /// <summary>
    /// End the session: the notes are cleared from memory, and the login page is shown next
    /// </summary>
    internal static void LogOut()
    {
        if (!IsLoggedIn) return;
        EventLog.Add(DialogText.LogFile + "Logout");
        Tools.RemovePrivileges(false);
        Mode.SetMode(Mode.SelectedMode.Login);
    }

    /// <summary>
    /// Back in the main menu of the data file (the session was kept). It does not return: the menu waits for its keys
    /// </summary>
    internal static void ReturnToMainMenu()
    {
        Mode.SetMode(Mode.SelectedMode.Manage);
        Menu.DrawTopMenu(true, true, false);
    }

    /// <summary>
    /// The start page (a page of Configuration, or the Mode tab) shown while staying logged in: afterwards the main
    /// menu of the data file, or the login page if a data file was chosen there (that logs out)
    /// </summary>
    /// <param name="configurationPage">The page of Configuration (0 to 3), or -1 for the Mode tab</param>
    internal static void ShowStartPage(int configurationPage)
    {
        StartMenu.Open(configurationPage);
        FileExplorer.DataFileSelected = false;
        StartMenu.Run();
        if (FileExplorer.DataFileSelected || !IsLoggedIn)
        {
            LogOut();
            Tools.LoginRoutine(true);
            return;
        }

        ReturnToMainMenu();
    }
}
