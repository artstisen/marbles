namespace Marbles;

class Program
{
    private static void Main(string[] args)
    {
        // A path given (relative to the directory Marbles is started in, which is read before anything else)
        var currentDirectory = Directory.GetCurrentDirectory();
        Init.RunInitialization();
        if (Lang.Missing != null)
        {
            // The language set has no file (a language file in a folder "lang" that is gone): English is used
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.CaptionsSettings,
                Lang.F("The language file {0} was not found: English is used.", Lang.Missing + ".txt"),
                Lang.T("Put it in the folder lang next to Marbles (or in the data directory)."));
        }

        // Started with a file or directory ("marbles notes.md"): it is opened as a jump, without the start page
        if (args.Length > 0 && OpenArgument(args[0], currentDirectory) is { } target)
        {
            Jump.OpenAtStart(target);
        }
        else
        {
            StartMenu.Start();
        }

        MainLoop();
    }

    // The place a path given at startup leads to, or null (a message tells why, or the user did not want a file
    // that is not there to be made)
    private static JumpTarget? OpenArgument(string argument, string currentDirectory)
    {
        var result = StartArgument.Resolve(argument, currentDirectory, StartMenu.Only);
        if (result.Message.Length > 0)
        {
            Tools.ClearScreen();
            ChoiceBox.Message(DialogText.ApplicationName, result.Message, string.Empty);
            return null;
        }

        if (!result.AskToCreate) return result.Target;
        Tools.ClearScreen();
        if (!ChoiceBox.Confirm(DialogText.NewFile, Lang.F("{0} is not there. Make it?", Path.GetFileName(result.Path)),
                result.Path, false))
        {
            return null;
        }

        try
        {
            File.WriteAllText(result.Path, string.Empty);
            EventLog.Add(DialogText.LogFile + "New: " + result.Path);
            return result.Target;
        }
        catch (Exception e)
        {
            ChoiceBox.Message(DialogText.NewFile, e.Message, result.Path);
            return null;
        }
    }

    // Each round handles one key in the screen of the current mode. When a key leads to another
    // screen, the screen is drawn and control returns here (see Navigation)
    private static void MainLoop()
    {
        var starting = true;
        while (true)
        {
            try
            {
                if (starting)
                {
                    starting = false;
                    // (a place given at startup is opened instead of the first page)
                    if (Jump.Pending == null) Menu.DrawTopMenu(true, true, false);
                }

                if (Jump.Pending != null)
                {
                    // A place was picked in the Jump menu: go there
                    Jump.RunPending();
                }
                else if (Mode.CurrentMode == Mode.SelectedMode.Login)
                {
                    Tools.ShowLogin(Navigation.LoginFullRedraw);
                }
                else
                {
                    Actions.HandleKey(Console.ReadKey(true));
                }
            }
            catch (NavigationException)
            {
                // A new screen is shown: continue with its keys
            }
            catch (Exception ex)
            {
                try
                {
                    Tools.FatalError(ex.Message);
                }
                catch (NavigationException)
                {
                    // Restarted
                }
            }
        }
    }
}