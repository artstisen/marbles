using System.Text;

namespace Marbles;

/// <summary>
/// A logo of the start and login pages: 4 rows of 63 characters, drawn in the color of the logo. The built-in
/// logo is "Marbles"; the ones made by the user are kept as files (Logos). It does not touch the console, so it
/// can be tested.
/// </summary>
internal sealed class LogoArt
{
    internal const int Rows = 4;
    internal const int Width = 63;
    internal const string BuiltInName = "Marbles";

    internal string Name { get; set; } = BuiltInName;

    private readonly char[][] _cells;

    internal LogoArt(string name, IReadOnlyList<string> rows)
    {
        Name = name;
        _cells = new char[Rows][];
        for (var row = 0; row < Rows; row++)
        {
            _cells[row] = new char[Width];
            Array.Fill(_cells[row], ' ');
            if (row >= rows.Count) continue;
            var text = rows[row];
            for (var column = 0; column < Width && column < text.Length; column++)
            {
                _cells[row][column] = Usable(text[column]) ? text[column] : ' ';
            }
        }
    }

    /// <summary>
    /// A character a logo can have: one that takes one cell (ASCII is typed in the editor; a file can have others)
    /// </summary>
    internal static bool Usable(char c) => !char.IsControl(c) && !char.IsSurrogate(c) &&
                                          TextMetrics.WidthOf(c.ToString()) == 1;

    /// <summary>
    /// A character that can be typed in the logo editor: printable ASCII
    /// </summary>
    internal static bool Typeable(char c) => c is >= ' ' and <= '~';

    internal char this[int row, int column]
    {
        get => _cells[row][column];
        set => _cells[row][column] = Usable(value) ? value : ' ';
    }

    /// <summary>
    /// A row of the logo (63 characters)
    /// </summary>
    internal string Row(int row) => new(_cells[row]);

    internal IReadOnlyList<string> AllRows => Enumerable.Range(0, Rows).Select(Row).ToList();

    internal LogoArt Clone() => new(Name, AllRows);

    /// <summary>
    /// The logo as text: its rows, without blank space at their ends
    /// </summary>
    internal string Serialize() => string.Join("\n", AllRows.Select(row => row.TrimEnd())) + "\n";

    /// <summary>
    /// A logo from its text: the first 4 lines, each cut or filled with blank space to 63 characters
    /// </summary>
    internal static LogoArt Parse(string text, string name) =>
        new(name, TextBuffer.Normalize(text).Split('\n').Take(Rows).ToList());

    /// <summary>
    /// The logo of Marbles (a copy)
    /// </summary>
    internal static LogoArt BuiltIn => new(BuiltInName, new[]
    {
        @"M_     _M """"""""""MM_ """"""""""MM_ """"""""""MM_ MMM      """"""""""MMM """"""""""MMM",
        @"MMM_ _MMM MMM _M%M MMM _MM"" MMM _MM"" M%M  ___ MMM_ """""" M%M ___",
        @"M%M "" M%M M%M"" M%M M%M"" M%M M%M"" M%M M%M  M%M M%M  ___  """""" M%M",
        @"MMM   MMM MMM  MMM MMM  MMM MMM__MM"" ""MM__MMM ""MM__MMM MMM__MM"""
    });
}

/// <summary>
/// The logos: the built-in one, and the ones made by the user, kept as files in the directory "logos" in the data
/// directory (the name of the logo and ".txt")
/// </summary>
internal static class Logos
{
    internal const string DirectoryName = "logos";
    private const string Extension = ".txt";

    /// <summary>
    /// The directory of the logos. It can be set to another directory (for testing)
    /// </summary>
    internal static string? DirectoryOverride { get; set; }

    internal static string Directory =>
        DirectoryOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), DirectoryName);

    internal static bool IsBuiltIn(string name) =>
        name.Trim().Equals(LogoArt.BuiltInName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A name a logo can be saved with: not empty, not the name of the built-in logo, and usable as a file name
    /// </summary>
    internal static bool IsValidName(string name)
    {
        name = name.Trim();
        return name.Length is > 0 and <= 40 && !IsBuiltIn(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0 && name != "." && name != "..";
    }

    private static string PathOf(string name) => Path.Combine(Directory, name.Trim() + Extension);

    /// <summary>
    /// The names of the logos made by the user (in alphabetical order)
    /// </summary>
    internal static List<string> CustomNames()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return new List<string>();
            return System.IO.Directory.GetFiles(Directory, "*" + Extension)
                .Select(file => Path.GetFileNameWithoutExtension(file)!)
                .Where(name => !IsBuiltIn(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// All the logos by name: the built-in one, then the ones made by the user
    /// </summary>
    internal static List<string> AllNames() => new[] { LogoArt.BuiltInName }.Concat(CustomNames()).ToList();

    /// <summary>
    /// A logo by its name (a copy), or null if there is none with the name
    /// </summary>
    internal static LogoArt? Load(string name)
    {
        if (IsBuiltIn(name)) return LogoArt.BuiltIn;
        if (!IsValidName(name)) return null;
        try
        {
            var path = PathOf(name);
            return File.Exists(path) ? LogoArt.Parse(File.ReadAllText(path, Encoding.UTF8), name.Trim()) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static bool Exists(string name) => IsBuiltIn(name) || (IsValidName(name) && File.Exists(PathOf(name)));

    /// <summary>
    /// Save a logo made by the user (its name is the name of the file)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save(LogoArt logo)
    {
        if (!IsValidName(logo.Name)) return DialogText.LogoNameInvalid;
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            FileIo.WriteAllBytesSafe(PathOf(logo.Name), new UTF8Encoding(false).GetBytes(logo.Serialize()));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Delete a logo made by the user
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Delete(string name)
    {
        if (!IsValidName(name)) return DialogText.LogoNameInvalid;
        try
        {
            File.Delete(PathOf(name));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
