namespace Marbles;

/// <summary>
/// Coloring of Markdown in the editor: the command ":syntax" and the styles of the lines to draw.
/// The styles themselves are found by the MarkdownHighlighter class.
/// </summary>
internal sealed partial class EditorEngine
{
    private const int MaxCachedMarkdownLines = 5000;

    /// <summary>
    /// True to color the text as Markdown
    /// </summary>
    internal bool Markdown { get; set; }

    // Tells for each line if it is inside a block of code. Found again when the text has changed
    private bool[] _inCodeBlock = Array.Empty<bool>();
    private int _codeBlockVersion = -1;

    // The styles of the lines looked at so far (by the text of the line, and if it is in a block of code)
    private readonly Dictionary<(string, bool), MarkdownStyle[]?> _markdownStyles = new();

    // The command ":syntax" (or ":syntax on" and ":syntax off"): turn the colors on or off
    private void SetMarkdown(string argument)
    {
        if (SingleLine) return;
        switch (argument)
        {
            case "":
                Markdown = !Markdown;
                break;
            case "on":
                Markdown = true;
                break;
            case "off":
                Markdown = false;
                break;
            default:
                SetError("Not an editor command: syntax " + argument);
                return;
        }

        Message = Markdown ? DialogText.MarkdownOn : DialogText.MarkdownOff;
        // The host remembers the choice for Markdown files
        _request = EditorRequest.SyntaxChanged;
    }

    /// <summary>
    /// The style of each character of a line (null if the line has none, or the colors are off)
    /// </summary>
    internal MarkdownStyle[]? MarkdownStyles(int line)
    {
        if (!Markdown) return null;
        if (_codeBlockVersion != Buffer.Version || _inCodeBlock.Length != Buffer.LineCount)
        {
            // A line starting with ``` starts a block of code, and the next one ends it
            _inCodeBlock = new bool[Buffer.LineCount];
            var inBlock = false;
            for (var i = 0; i < _inCodeBlock.Length; i++)
            {
                _inCodeBlock[i] = inBlock;
                if (MarkdownHighlighter.IsFence(Buffer[i])) inBlock = !inBlock;
            }

            _codeBlockVersion = Buffer.Version;
        }

        var key = (Buffer[line], _inCodeBlock[line]);
        if (_markdownStyles.TryGetValue(key, out var styles)) return styles;
        if (_markdownStyles.Count >= MaxCachedMarkdownLines) _markdownStyles.Clear();
        styles = MarkdownHighlighter.Highlight(key.Item1, key.Item2);
        _markdownStyles[key] = styles;
        return styles;
    }
}
