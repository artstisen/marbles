using System.Text;

namespace Marbles;

/// <summary>
/// A logo of the start and login pages: 4 rows of 63 characters, each row in a color of its own or in the color
/// of the logo (of the color scheme). The built-in logos are "Marbles" (the one used from the start),
/// "Reflections", "Adventure" and "Islands"; the ones made by the user are kept as files
/// (Logos): the 4 rows, and a line "colors=" with the color of each row ("default" for the color of the logo).
/// A cell can have a color of its own, which is used instead of the color of its row: these are 4 lines "cells="
/// after the others, with a character for each cell (the number of the color as a hex digit, "." for none).
/// It does not touch the console, so it can be tested.
/// </summary>
internal sealed class LogoArt
{
    internal const int Rows = 4;
    internal const int Width = 63;
    internal const string BuiltInName = "Marbles";
    private const string ReflectionsName = "Reflections";
    private const string IslandsName = "Islands";
    private const string AdventureName = "Adventure";

    /// <summary>
    /// The names of the built-in logos, in the order they are listed
    /// </summary>
    internal static readonly IReadOnlyList<string> BuiltInNames = new[] { BuiltInName, ReflectionsName, AdventureName, IslandsName };

    internal string Name { get; set; } = BuiltInName;

    private readonly char[][] _cells;
    private readonly ConsoleColor?[] _colors = new ConsoleColor?[Rows];
    private readonly ConsoleColor?[][] _cellColors;
    private const string ColorsKey = "colors=";
    private const string CellsKey = "cells=";
    private const char NoCellColor = '.';
    private const string HexDigits = "0123456789ABCDEF";

    internal LogoArt(string name, IReadOnlyList<string> rows)
    {
        Name = name;
        _cells = new char[Rows][];
        _cellColors = new ConsoleColor?[Rows][];
        for (var row = 0; row < Rows; row++)
        {
            _cellColors[row] = new ConsoleColor?[Width];
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

    /// <summary>
    /// The color of a row (null: the color of the logo)
    /// </summary>
    internal ConsoleColor? RowColor(int row) => _colors[row];

    internal void SetRowColor(int row, ConsoleColor? color) => _colors[row] = color;

    /// <summary>
    /// The color a cell has of its own (null: the color of its row)
    /// </summary>
    internal ConsoleColor? CellColor(int row, int column) => _cellColors[row][column];

    internal void SetCellColor(int row, int column, ConsoleColor? color) => _cellColors[row][column] = color;

    /// <summary>
    /// The color a cell is drawn in: its own, or else the one of its row (null: the color of the logo)
    /// </summary>
    internal ConsoleColor? ColorAt(int row, int column) => _cellColors[row][column] ?? _colors[row];

    internal bool HasCellColors => _cellColors.Any(row => row.Any(color => color != null));

    /// <summary>
    /// Take the colors of their own from all the cells: they have the colors of their rows again
    /// </summary>
    internal void ClearCellColors()
    {
        foreach (var row in _cellColors) Array.Fill(row, null);
    }

    /// <summary>
    /// A row as the parts that are drawn in one color, from left to right. Blank space has no color to see, so
    /// it goes with the part before it
    /// </summary>
    internal IReadOnlyList<(int Column, string Text, ConsoleColor? Color)> Runs(int row)
    {
        var runs = new List<(int Column, string Text, ConsoleColor? Color)>();
        var start = 0;
        var seen = false; // The part has a character that can be seen
        ConsoleColor? current = null;
        for (var column = 0; column < Width; column++)
        {
            if (_cells[row][column] == ' ') continue;
            var color = ColorAt(row, column);
            if (seen && color != current)
            {
                runs.Add((start, new string(_cells[row], start, column - start), current));
                start = column;
            }

            current = color;
            seen = true;
        }

        runs.Add((start, new string(_cells[row], start, Width - start), current));
        return runs;
    }

    internal LogoArt Clone()
    {
        var copy = new LogoArt(Name, AllRows);
        Array.Copy(_colors, copy._colors, Rows);
        for (var row = 0; row < Rows; row++) Array.Copy(_cellColors[row], copy._cellColors[row], Width);
        return copy;
    }

    /// <summary>
    /// The logo as text: its rows (without blank space at their ends), the colors of the rows when one has a
    /// color of its own, and the colors of the cells when one has
    /// </summary>
    internal string Serialize()
    {
        var text = string.Join("\n", AllRows.Select(row => row.TrimEnd())) + "\n";
        if (_colors.Any(color => color != null))
        {
            text += ColorsKey + string.Join(",", _colors.Select(color => color?.ToString() ?? "default")) + "\n";
        }

        if (!HasCellColors) return text;
        foreach (var row in _cellColors)
        {
            var cells = new string(row.Select(color => color is { } own ? HexDigits[(int)own] : NoCellColor).ToArray());
            text += CellsKey + cells.TrimEnd(NoCellColor) + "\n";
        }

        return text;
    }

    /// <summary>
    /// A logo from its text: the first 4 lines, each cut or filled with blank space to 63 characters, and the
    /// colors of the rows from a line "colors=" after them, and the colors of the cells from the lines "cells="
    /// </summary>
    internal static LogoArt Parse(string text, string name)
    {
        var lines = TextBuffer.Normalize(text).Split('\n');
        var logo = new LogoArt(name, lines.Take(Rows).ToList());
        var colors = lines.Skip(Rows).FirstOrDefault(line => line.StartsWith(ColorsKey, StringComparison.OrdinalIgnoreCase));
        var names = colors == null ? Array.Empty<string>() : colors[ColorsKey.Length..].Split(',');
        for (var row = 0; row < Rows && row < names.Length; row++)
        {
            if (Enum.TryParse<ConsoleColor>(names[row].Trim(), true, out var color) && Enum.IsDefined(color))
            {
                logo._colors[row] = color;
            }
        }

        // The colors of the cells: a line for each row, in the order of the rows
        var cells = lines.Skip(Rows).Where(line => line.StartsWith(CellsKey, StringComparison.OrdinalIgnoreCase))
            .Select(line => line[CellsKey.Length..]).Take(Rows).ToList();
        for (var row = 0; row < cells.Count; row++)
        {
            for (var column = 0; column < Width && column < cells[row].Length; column++)
            {
                var digit = HexDigits.IndexOf(char.ToUpperInvariant(cells[row][column]));
                if (digit >= 0) logo._cellColors[row][column] = (ConsoleColor)digit;
            }
        }

        return logo;
    }

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

    /// <summary>
    /// A built-in logo by its name (a copy), or null if there is none with the name
    /// </summary>
    internal static LogoArt? Preset(string name)
    {
        name = name.Trim();
        if (name.Equals(BuiltInName, StringComparison.OrdinalIgnoreCase)) return BuiltIn;
        if (name.Equals(ReflectionsName, StringComparison.OrdinalIgnoreCase)) return Reflections;
        if (name.Equals(IslandsName, StringComparison.OrdinalIgnoreCase)) return Islands;
        return name.Equals(AdventureName, StringComparison.OrdinalIgnoreCase) ? Adventure : null;
    }

    // The logo of Marbles with a color for each row, from the sky to the grass
    private static LogoArt Reflections
    {
        get
        {
            var logo = BuiltIn;
            logo.Name = ReflectionsName;
            logo.SetRowColor(0, ConsoleColor.Blue);
            logo.SetRowColor(1, ConsoleColor.Cyan);
            logo.SetRowColor(2, ConsoleColor.Green);
            logo.SetRowColor(3, ConsoleColor.DarkGreen);
            return logo;
        }
    }

    // Two islands with palms, one of sand and one of grass, and the sun over the water between them: a logo with
    // a color for each of its cells
    private static LogoArt Islands => Parse(string.Join("\n",
        @" . ,-\|/-,          .      .\.|./.      .      .(\|/), ,-\|/-,",
        @"   '  |  '   .  _,       --~= 0 =~--    _.     '' |' : '  |  '",
        @"    __|_o___.--'. :-.      '/'|'\'  _.-'.;'-.___o_|_;_.___|_",
        @"~~~/..;...;..;...;...\~~~~~~~~~~~~~/,,,;,,,,,;,,,;,,,;,,,,,\~~~",
        "cells=.B.222AAAA..........B......66EEE66......B......AAAAA22.AAAA222",
        "cells=...2..6..A...B..EE......666EE6E6EE666...AA.....AA662.2.A..6..2",
        "cells=....E66E6EEE6EE6E.EEE......66EEE66..AAAAAA2A2AAACA622AAAAA62",
        "cells=B996EE6EEEEEE6EEEEEEEE99B99B9EEE9B9AAAA2AAAAA2AAAAAAA2AAAAA2B99"), IslandsName);

    // A hut in the forest with smoke from its chimney and a lit window, a mountain with snow on its top to the
    // left, a stream coming down past the hut, and birds in the sky: a logo with a color for each of its cells
    private static LogoArt Adventure => Parse(string.Join("\n",
        @"       /\          ^             ~~     v          ^    ^",
        @"      /^^\  /\    /^\   ^    ___|_____     v      /^\  /^\  ^",
        @"     /    \/  \  /^^^\ /^\  /__________\  .~~~~  /^^^\/^^^\/^\",
        @"____/__________\___|____|____|[#]__|'||_~~~~~~~~~~_|____|___|__",
        "cells=.......FF..........A.............78.....8..........A....A",
        "cells=......7FF7..77....2A2...A....444844444.....8......2A2..2A2..A",
        "cells=.....7....77..7..2AAA2.2A2..CCCCCCCCCCCC..BBBB9..2AAA22AAA22A2",
        "cells=2228722222222227822622226222266E6226666299B99B99B92622226222622"), AdventureName);
}

/// <summary>
/// The logos: the built-in ones, and the ones made by the user, kept as files in the directory "logos" in the data
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
        LogoArt.BuiltInNames.Any(builtIn => builtIn.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A name a logo can be saved with: not empty, not the name of a built-in logo, and usable as a file name
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
    /// All the logos by name: the built-in ones, then the ones made by the user
    /// </summary>
    internal static List<string> AllNames() => LogoArt.BuiltInNames.Concat(CustomNames()).ToList();

    /// <summary>
    /// A logo by its name (a copy), or null if there is none with the name
    /// </summary>
    internal static LogoArt? Load(string name)
    {
        if (LogoArt.Preset(name) is { } builtIn) return builtIn;
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
    /// Give a logo made by the user another name (its file is renamed). Another logo with the name is not replaced
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Rename(string name, string newName)
    {
        if (IsBuiltIn(newName)) return DialogText.LogoNameBuiltIn;
        if (!IsValidName(name) || !IsValidName(newName)) return DialogText.LogoNameInvalid;
        try
        {
            if (!File.Exists(PathOf(name))) return DialogText.LogoNameInvalid;
            var sameLogo = name.Trim().Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!sameLogo && File.Exists(PathOf(newName))) return DialogText.LogoNameTaken;
            if (name.Trim() != newName.Trim()) File.Move(PathOf(name), PathOf(newName));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Make a copy of a logo (a built-in one, or one made by the user) as a logo of the user's own with another
    /// name. Another logo with the name is not replaced
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Duplicate(string name, string newName)
    {
        if (IsBuiltIn(newName)) return DialogText.LogoNameBuiltIn;
        if (!IsValidName(newName)) return DialogText.LogoNameInvalid;
        if (Exists(newName)) return DialogText.LogoNameTaken;
        var copy = Load(name);
        if (copy == null) return DialogText.LogoNameInvalid;
        copy.Name = newName.Trim();
        return Save(copy);
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
