using System.Text.RegularExpressions;

namespace Marbles;

/// <summary>
/// Search for files in a directory (and the directories in it): by name, and by a text in the files.
/// The class knows nothing about the console: the file browser shows the box to fill in and the result.
///
/// A name is found if it contains the text given. "*" and "?" stand for any characters and any one
/// character, and then the whole name must match ("*.md"). As a regular expression the name, and a line of
/// the file, must have a match. Hidden files and directories are not searched, links to directories are not
/// followed, and only text files that the editor can open are searched for text (not Marbles data files).
/// </summary>
internal static class FileSearch
{
    /// <summary>
    /// The largest number of files found (the search stops there)
    /// </summary>
    internal const int MaxResults = 5000;

    private const int MaxShownLineLength = 200;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    internal sealed class Options
    {
        internal string Name = string.Empty; // Empty: all names
        internal string Text = string.Empty; // Empty: the content of the files is not searched
        internal bool IsRegex;
        internal bool MatchCase;
        internal bool Subfolders = true;
    }

    internal sealed class Result
    {
        internal string Path = string.Empty;
        internal long Size;
        internal bool IsDirectory;
        internal int Line = -1; // The first line with the text (from 0), when the content was searched
        internal string LineText = string.Empty;
    }

    internal sealed class Outcome
    {
        internal List<Result> Results = new();
        internal bool Stopped; // Stopped by the user
        internal bool Capped; // Stopped at the largest number of files
        internal int Unreadable; // Directories that could not be read
    }

    /// <summary>
    /// Check what is to be searched for
    /// </summary>
    /// <returns>DialogText.Success, or what is wrong</returns>
    internal static string Validate(Options options)
    {
        if (options.Name.Trim().Length == 0 && options.Text.Length == 0) return DialogText.SearchNothing;
        if (!options.IsRegex) return DialogText.Success;
        try
        {
            if (options.Name.Trim().Length > 0) _ = new Regex(options.Name.Trim());
            if (options.Text.Length > 0) _ = new Regex(options.Text);
        }
        catch (ArgumentException e)
        {
            return DialogText.SearchInvalidPattern + e.Message;
        }

        return DialogText.Success;
    }

    /// <summary>
    /// The pattern a name must match: what was written as a regular expression, or made from a text
    /// with "*" and "?" (null: all names)
    /// </summary>
    internal static Regex? NamePattern(Options options)
    {
        var name = options.Name.Trim();
        if (name.Length == 0) return null;
        var regexOptions = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        if (options.IsRegex) return new Regex(name, regexOptions, MatchTimeout);
        var pattern = Regex.Escape(name);
        if (name.Contains('*') || name.Contains('?'))
        {
            // With wildcards the whole name must match
            pattern = "^" + pattern.Replace("\\*", ".*").Replace("\\?", ".") + "$";
        }

        return new Regex(pattern, regexOptions, MatchTimeout);
    }

    private static Regex? TextPattern(Options options)
    {
        if (options.Text.Length == 0) return null;
        var regexOptions = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        return new Regex(options.IsRegex ? options.Text : Regex.Escape(options.Text), regexOptions, MatchTimeout);
    }

    private static bool IsMatch(Regex? pattern, string text)
    {
        if (pattern == null) return true;
        try
        {
            return pattern.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Search a directory
    /// </summary>
    /// <param name="root">The directory</param>
    /// <param name="options">What to search for (it must be valid)</param>
    /// <param name="progress">Called with each directory before it is searched, and the number of files
    /// found so far. Returns false to stop</param>
    internal static Outcome Search(string root, Options options, Func<string, int, bool>? progress = null)
    {
        var outcome = new Outcome();
        var namePattern = NamePattern(options);
        var textPattern = TextPattern(options);
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            if (progress != null && !progress(directory, outcome.Results.Count))
            {
                outcome.Stopped = true;
                break;
            }

            List<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(directory).EnumerateFileSystemInfos()
                    .Where(entry => (entry.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch
            {
                outcome.Unreadable++;
                continue;
            }

            var subDirectories = new List<string>();
            foreach (var entry in entries)
            {
                var isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;
                // A link to a directory is not followed, so the search cannot go in circles
                if (isDirectory && options.Subfolders && (entry.Attributes & FileAttributes.ReparsePoint) == 0)
                {
                    subDirectories.Add(entry.FullName);
                }

                if (!IsMatch(namePattern, entry.Name)) continue;
                var result = new Result { Path = entry.FullName, IsDirectory = isDirectory };
                if (textPattern != null)
                {
                    // Only files have text
                    if (isDirectory || !FindText(entry.FullName, textPattern, result)) continue;
                }

                if (!isDirectory) result.Size = ((FileInfo)entry).Length;
                outcome.Results.Add(result);
                if (outcome.Results.Count >= MaxResults)
                {
                    outcome.Capped = true;
                    return outcome;
                }
            }

            // The directories are searched in the order of their names
            for (var i = subDirectories.Count - 1; i >= 0; i--) directories.Push(subDirectories[i]);
        }

        return outcome;
    }

    // Find the first line of a text file with a match. False if there is none, or the file is not a text file
    // that can be opened (too large, not text, a Marbles data file)
    private static bool FindText(string path, Regex pattern, Result result)
    {
        if (path.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase)) return false;
        string text;
        try
        {
            text = TextFile.Load(path).Text;
        }
        catch
        {
            return false;
        }

        var lineNumber = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (IsMatch(pattern, line))
            {
                result.Line = lineNumber;
                result.LineText = line.Trim();
                if (result.LineText.Length > MaxShownLineLength) result.LineText = result.LineText[..MaxShownLineLength];
                return true;
            }

            lineNumber++;
        }

        return false;
    }
}
