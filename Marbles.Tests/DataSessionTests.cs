namespace Marbles.Tests;

/// <summary>
/// Staying logged into the data file (setting "stayloggedin"): the session is kept while files are edited and pages
/// shown, until the user logs out
/// </summary>
[Collection("Static state")]
public class DataSessionTests : IDisposable
{
    private readonly bool _saved = DataSession.StayLoggedIn;
    private readonly Mode.SelectedMode _savedMode = Mode.CurrentMode;

    public DataSessionTests()
    {
        Encryptor.RemoveKey();
        Tools.ClearLineMemory();
    }

    public void Dispose()
    {
        DataSession.StayLoggedIn = _saved;
        Encryptor.RemoveKey();
        Tools.ClearLineMemory();
        Mode.SetMode(_savedMode);
    }

    [Fact]
    public void TheSession_IsKept_OnlyWhenStayingAndLoggedIn()
    {
        DataSession.StayLoggedIn = false;
        Assert.False(DataSession.Keep);
        Encryptor.SetKey("test1234");
        Assert.True(DataSession.IsLoggedIn);
        Assert.False(DataSession.Keep);
        DataSession.StayLoggedIn = true;
        Assert.True(DataSession.Keep);
        Encryptor.RemoveKey();
        Assert.False(DataSession.IsLoggedIn);
        Assert.False(DataSession.Keep);
    }

    [Fact]
    public void LoggingOut_ClearsTheKey_AndTheStringMemory_AndShowsTheLoginPage()
    {
        DataSession.StayLoggedIn = true;
        Encryptor.SetKey("test1234");
        Mode.SetMode(Mode.SelectedMode.Manage);
        // Text yanked in a note stays while staying logged in (it can be put in a file)
        Tools.StoreInLineMemory("from a note");
        Assert.True(Tools.HasLineMemory());

        DataSession.LogOut();
        Assert.False(DataSession.IsLoggedIn);
        Assert.False(Encryptor.HasKey);
        Assert.False(Tools.HasLineMemory());
        Assert.Equal(Mode.SelectedMode.Login, Mode.CurrentMode);
        // The setting stays on: the next login is kept too
        Assert.True(DataSession.StayLoggedIn);
    }

    [Fact]
    public void TheEditorOfAFile_TellsItIsNotEncrypted_WhileLoggedIn()
    {
        Encryptor.SetKey("test1234");
        Assert.True(FileExplorer.FromDataFile);
        Encryptor.RemoveKey();
        Assert.Equal(FileExplorer.SessionEnded, FileExplorer.FromDataFile);
    }
}
