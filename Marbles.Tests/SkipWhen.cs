namespace Marbles.Tests;

/// <summary>
/// A test that can only run on Windows: elsewhere it is shown as skipped (not as passed)
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Only on Windows";
    }
}

/// <summary>
/// A test that can only run on Linux and macOS: on Windows it is shown as skipped (not as passed)
/// </summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "Only on Linux and macOS";
    }
}

/// <summary>
/// A test that makes symbolic links: where they cannot be made (Windows without Developer Mode) it is shown as
/// skipped (not as passed)
/// </summary>
public sealed class LinksFactAttribute : FactAttribute
{
    private static readonly bool CanMakeLinks = TryMakeLink();

    public LinksFactAttribute()
    {
        if (!CanMakeLinks) Skip = "Symbolic links cannot be made here (Windows: turn on Developer Mode)";
    }

    private static bool TryMakeLink()
    {
        var directory = Path.Combine(Path.GetTempPath(), "marbles-links-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, "target.txt");
            File.WriteAllText(target, "x");
            File.CreateSymbolicLink(Path.Combine(directory, "link.txt"), target);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
        finally
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // Left in the temporary directory
            }
        }
    }
}
