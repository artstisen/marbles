namespace Marbles;

/// <summary>
/// Finds the parts of a line of HTML to color: tags, the names and values of attributes, comments and
/// character entities (like &amp;amp;). What is between &lt;style&gt; and &lt;/style&gt; is read as CSS,
/// and what is between &lt;script&gt; and &lt;/script&gt; as JavaScript.
/// XML is read the same way, without the CSS and JavaScript, and with its CDATA sections.
/// PHP is HTML with code between &lt;?php (or &lt;?=) and ?&gt;, read as PHP (CStyleHighlighter.Php), also over
/// several lines; a block of PHP inside the value of an attribute or in a script or style is read on its line.
/// </summary>
internal static class HtmlHighlighter
{
    // Where a line starts
    private const int InText = 0;
    private const int InComment = 1;
    private const int InTag = 2; // After the name of a tag: among its attributes
    private const int InScript = 3;
    private const int InStyle = 4;
    private const int InCData = 5; // <![CDATA[ text that is not read as tags ]]>
    private const int InPhp = 6; // <?php code ?>

    // The state is kept in one number: where the line starts (3 bits), what the tag it is in starts when it
    // ends (2 bits: nothing, script or style), and the state of the JavaScript, CSS or PHP it is in (the rest)
    private const int ModeBits = 3;
    private const int PendingBits = 2;
    private const int PendingScript = 1;
    private const int PendingStyle = 2;

    /// <summary>
    /// Find the styles of a line
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="state">The state the line starts in (0 at the start of a document)</param>
    /// <param name="styles">The styles of the line, to fill in</param>
    /// <param name="xml">True for XML: script and style are tags like the others</param>
    /// <param name="php">True for PHP: the code between &lt;?php and ?&gt; is read as PHP</param>
    /// <returns>The state after the line</returns>
    internal static int Scan(string line, int state, SyntaxStyle[] styles, bool xml = false, bool php = false)
    {
        var mode = state & ((1 << ModeBits) - 1);
        var pending = (state >> ModeBits) & ((1 << PendingBits) - 1);
        var inner = state >> (ModeBits + PendingBits);
        var end = line.Length;
        var i = 0;
        while (i < end)
        {
            switch (mode)
            {
                case InComment:
                {
                    var close = line.IndexOf("-->", i, StringComparison.Ordinal);
                    var stop = close < 0 ? end : close + 3;
                    JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Comment);
                    if (close >= 0) mode = InText;
                    i = stop;
                    break;
                }
                case InScript:
                case InStyle:
                {
                    // To the tag that ends it
                    var close = line.IndexOf(mode == InScript ? "</script" : "</style", i,
                        StringComparison.OrdinalIgnoreCase);
                    var stop = close < 0 ? end : close;
                    inner = mode == InScript
                        ? JavaScriptHighlighter.Scan(line, i, stop, inner, styles)
                        : CssHighlighter.Scan(line, i, stop, inner, styles);
                    if (php) ColorPhpBlocks(line, i, stop, styles);
                    if (close >= 0)
                    {
                        mode = InText;
                        inner = 0;
                    }

                    i = stop;
                    break;
                }
                case InCData:
                {
                    // The text is left as it is, to the mark that ends the section
                    var close = line.IndexOf("]]>", i, StringComparison.Ordinal);
                    if (close >= 0)
                    {
                        JavaScriptHighlighter.Fill(styles, close, close + 3, SyntaxStyle.Keyword);
                        mode = InText;
                    }

                    i = close < 0 ? end : close + 3;
                    break;
                }
                case InPhp:
                {
                    // To "?>", which goes back to the HTML
                    inner = CStyleHighlighter.Scan(CStyleHighlighter.Php, line, i, end, inner, styles, out var stop);
                    if (stop < end)
                    {
                        JavaScriptHighlighter.Fill(styles, stop, stop + 2, SyntaxStyle.Keyword);
                        mode = InText;
                        inner = 0;
                        stop += 2;
                    }

                    i = stop;
                    break;
                }
                case InTag:
                    i = ScanTag(line, i, styles, ref mode, ref pending, php);
                    break;
                default:
                    if (php && PhpOpening(line, i) is var opening and > 0)
                    {
                        JavaScriptHighlighter.Fill(styles, i, i + opening, SyntaxStyle.Keyword);
                        mode = InPhp;
                        inner = 0;
                        i += opening;
                        break;
                    }

                    i = ScanText(line, i, styles, ref mode, ref pending);
                    if (xml) pending = 0;
                    break;
            }
        }

        return mode | (pending << ModeBits) | (inner << (ModeBits + PendingBits));
    }

    // Text outside the tags: to the next tag, comment or entity
    private static int ScanText(string line, int i, SyntaxStyle[] styles, ref int mode, ref int pending)
    {
        var c = line[i];
        if (c == '<')
        {
            if (string.CompareOrdinal(line, i, "<!--", 0, 4) == 0)
            {
                JavaScriptHighlighter.Fill(styles, i, i + 4, SyntaxStyle.Comment);
                mode = InComment;
                return i + 4;
            }

            const string cdata = "<![CDATA[";
            if (string.CompareOrdinal(line, i, cdata, 0, cdata.Length) == 0)
            {
                JavaScriptHighlighter.Fill(styles, i, i + cdata.Length, SyntaxStyle.Keyword);
                mode = InCData;
                return i + cdata.Length;
            }

            var next = i + 1 < line.Length ? line[i + 1] : '\0';
            if (next == '!')
            {
                // <!DOCTYPE html> and the like
                var close = line.IndexOf('>', i);
                var stop = close < 0 ? line.Length : close + 1;
                JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Keyword);
                return stop;
            }

            // (<?xml version="1.0"?> is read as a tag that ends with "?>")
            var name = next is '/' or '?' ? i + 2 : i + 1;
            if (name < line.Length && char.IsAsciiLetter(line[name]))
            {
                var stop = name;
                while (stop < line.Length && (char.IsLetterOrDigit(line[stop]) || line[stop] is '-' or ':')) stop++;
                JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Keyword);
                var tag = line[name..stop];
                // The content of these two is not HTML
                pending = next == '/' ? 0 :
                    tag.Equals("script", StringComparison.OrdinalIgnoreCase) ? PendingScript :
                    tag.Equals("style", StringComparison.OrdinalIgnoreCase) ? PendingStyle : 0;
                mode = InTag;
                return stop;
            }
        }
        else if (c == '&')
        {
            // &amp; &#169;
            var stop = i + 1;
            while (stop < line.Length && (char.IsLetterOrDigit(line[stop]) || line[stop] == '#')) stop++;
            if (stop > i + 1 && stop < line.Length && line[stop] == ';')
            {
                JavaScriptHighlighter.Fill(styles, i, stop + 1, SyntaxStyle.Number);
                return stop + 1;
            }
        }

        return i + 1;
    }

    // The mark that starts a block of PHP at a place: "<?php", "<?=" or "<?" with a blank space after it (not
    // "<?xml"), and its length; 0 if there is none
    private static int PhpOpening(string line, int i)
    {
        if (i + 1 >= line.Length || line[i] != '<' || line[i + 1] != '?') return 0;
        if (string.Compare(line, i, "<?php", 0, 5, StringComparison.OrdinalIgnoreCase) == 0) return 5;
        if (i + 2 < line.Length && line[i + 2] == '=') return 3;
        return i + 2 == line.Length || char.IsWhiteSpace(line[i + 2]) ? 2 : 0;
    }

    // The blocks of PHP in a part of a line read as something else (the value of an attribute, a script or a
    // style): each from its mark to "?>" on the line, colored on top
    private static void ColorPhpBlocks(string line, int from, int to, SyntaxStyle[] styles)
    {
        var i = from;
        while (i < to)
        {
            var start = line.IndexOf("<?", i, to - i, StringComparison.Ordinal);
            if (start < 0) return;
            var opening = PhpOpening(line, start);
            if (opening == 0)
            {
                i = start + 2;
                continue;
            }

            // Read on its own, and put in place of what the part had there
            var block = new SyntaxStyle[line.Length];
            JavaScriptHighlighter.Fill(block, start, start + opening, SyntaxStyle.Keyword);
            CStyleHighlighter.Scan(CStyleHighlighter.Php, line, start + opening, line.Length, 0, block, out var stop);
            var blockEnd = Math.Min(line.Length, stop + 2);
            JavaScriptHighlighter.Fill(block, stop, blockEnd, SyntaxStyle.Keyword);
            Array.Copy(block, start, styles, start, blockEnd - start);
            i = blockEnd;
        }
    }

    // Inside a tag: the attributes, to the ">" that ends the tag
    private static int ScanTag(string line, int i, SyntaxStyle[] styles, ref int mode, ref int pending,
        bool php = false)
    {
        var c = line[i];
        if (char.IsWhiteSpace(c)) return i + 1;
        if (c == '>' || (c is '/' or '?' && i + 1 < line.Length && line[i + 1] == '>'))
        {
            var stop = c == '>' ? i + 1 : i + 2;
            JavaScriptHighlighter.Fill(styles, i, stop, SyntaxStyle.Keyword);
            // <script> and <style> start JavaScript and CSS (if they are not ended at once)
            mode = c != '>' ? InText : pending == PendingScript ? InScript : pending == PendingStyle ? InStyle : InText;
            pending = 0;
            return stop;
        }

        if (c == '=')
        {
            // The value of an attribute: in quotes, or to the next blank space
            var value = i + 1;
            while (value < line.Length && line[value] == ' ') value++;
            if (value >= line.Length) return value;
            int stop;
            if (line[value] is '"' or '\'')
            {
                stop = line.IndexOf(line[value], value + 1);
                stop = stop < 0 ? line.Length : stop + 1;
            }
            else
            {
                stop = value;
                while (stop < line.Length && !char.IsWhiteSpace(line[stop]) && line[stop] != '>') stop++;
            }

            JavaScriptHighlighter.Fill(styles, value, stop, SyntaxStyle.String);
            if (php) ColorPhpBlocks(line, value, stop, styles);
            return stop;
        }

        // The name of an attribute
        var end = i;
        while (end < line.Length && !char.IsWhiteSpace(line[end]) && line[end] is not ('=' or '>' or '/' or '?'))
        {
            end++;
        }

        if (end == i) return i + 1;
        JavaScriptHighlighter.Fill(styles, i, end, SyntaxStyle.Name);
        return end;
    }
}
