namespace Marbles;

class Program
{
    private static void Main(string[] args)
    {
        Init.RunInitialization();
        MainLoop();
    }

    private static void MainLoop()
    {
        Menu.DrawTopMenu(true, true, false);
    }
}