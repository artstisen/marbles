namespace Marbles;

class Program
{
    private static void Main(string[] args)
    {
        Init.RunInitialization();
        StartMenu.Start();
        MainLoop();
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
                    Menu.DrawTopMenu(true, true, false);
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