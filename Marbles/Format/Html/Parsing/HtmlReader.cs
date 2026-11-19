// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Html/Parsing/HtmlReader.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Collections.Frozen;
using System.Globalization;

namespace Marbles.Format.Html.Parsing;

/// <summary>A parse error, with the offset in the source where it was found.</summary>
public sealed record HtmlParseError(int Offset, string Code, string Message);

public sealed class HtmlParseResult(HElement root, List<HtmlParseError> errors, int tooDeepAt = -1)
{
    /// <summary>The document; its children are the top-level nodes.</summary>
    public HElement Root { get; } = root;

    public List<HtmlParseError> Errors { get; } = errors;

    /// <summary>
    /// The offset of the first element nested deeper than <see cref="HtmlReader.MaxDepth"/>, or -1. The rules and the
    /// formatter walk the tree recursively, so such a tree is not checked or formatted (it would overflow the stack)
    /// </summary>
    public int TooDeepAt { get; } = tooDeepAt;
}

/// <summary>
/// Reads HTML into an <see cref="HElement"/> tree and reports parse errors as the HTML standard
/// defines them (a selection: doctype, tags, attributes, comments, character references, open and
/// stray elements). It never fails: it knows the end tags that may be left out (<c>li</c>, <c>p</c>,
/// <c>td</c> …), keeps end tags without a start tag, and records the source text of everything, so the
/// formatter can write it back.
/// </summary>
public sealed class HtmlReader
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    /// <summary>Start tags that close an open <c>p</c>.</summary>
    private static readonly FrozenSet<string> ClosesParagraph = new[]
    {
        "address", "article", "aside", "blockquote", "center", "details", "dialog", "dir", "div", "dl",
        "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header",
        "hgroup", "hr", "main", "menu", "nav", "ol", "p", "pre", "search", "section", "table", "ul",
        "listing", "xmp", "plaintext", "li", "dd", "dt", "summary",
    }.ToFrozenSet(Ci);

    /// <summary>
    /// The elements that may be in head. Another start tag, or text, ends a head whose end tag is left out (the
    /// content then goes in the body, whose start tag may be left out too)
    /// </summary>
    private static readonly FrozenSet<string> HeadContent = new[]
    {
        "base", "basefont", "bgsound", "link", "meta", "noframes", "noscript", "script", "style", "template", "title",
    }.ToFrozenSet(Ci);

    /// <summary>Named character references that the parser also takes without a semicolon (a common subset).</summary>
    private static readonly FrozenSet<string> LegacyReferences = new[]
    {
        "amp", "lt", "gt", "quot", "nbsp", "copy", "reg", "deg", "plusmn", "micro", "para", "middot", "laquo",
        "raquo", "frac12", "frac14", "frac34", "times", "divide", "sect", "cent", "pound", "yen", "iexcl",
        "iquest", "shy", "uml", "acute", "cedil", "ordf", "ordm", "sup1", "sup2", "sup3", "not", "macr",
        "brvbar", "curren", "AMP", "LT", "GT", "QUOT", "COPY", "REG",
    }.ToFrozenSet(StringComparer.Ordinal);

    private readonly string _s;
    private readonly List<HElement> _stack = [];
    private readonly List<HtmlParseError> _errors = [];
    private readonly HashSet<string> _seenStructural = new(Ci);

    // After </body> or </html>: content there is moved into the body by browsers, and is told once
    private bool _afterBody;
    private bool _toldAfterBody;
    private bool _seenDoctype;
    private bool _seenContent;
    private int _pos;
    private int _tooDeepAt = -1;

    /// <summary>How deep elements may be nested to be checked and formatted (as deep as browsers build them).</summary>
    public const int MaxDepth = 512;

    private HtmlReader(string source)
    {
        _s = source;
        _stack.Add(new HElement("#root") { ContentStart = 0 });
    }

    public static HtmlParseResult Read(string source)
    {
        var reader = new HtmlReader(source);
        reader.Run();
        return new HtmlParseResult(reader._stack[0], reader._errors, reader._tooDeepAt);
    }

    private HElement Current => _stack[^1];

    // In SVG or MathML, but not in an HTML integration point (svg foreignObject, desc, title; MathML annotation-xml),
    // whose content is HTML again
    private bool InForeignContent => Current.IsForeign && !HtmlNames.IsIntegrationPoint(Current);

    // HTML's whitespace: space, tab, line feed, form feed, carriage return. Not a no-break space or other Unicode space,
    // which are characters of a name or an unquoted value
    private static bool IsHtmlSpace(char c) => c is ' ' or '\t' or '\n' or '\f' or '\r';

    private void Error(int offset, string code, string message) =>
        _errors.Add(new HtmlParseError(offset, "html.parse." + code, message));

    private void Run()
    {
        while (_pos < _s.Length)
        {
            if (_s[_pos] == '<' && IsMarkupStart(_pos))
            {
                if (char.IsAsciiLetter(_s[_pos + 1])) AfterBody(_pos);
                ReadMarkup();
                continue;
            }

            var start = _pos;
            _pos++;
            while (_pos < _s.Length && !(_s[_pos] == '<' && IsMarkupStart(_pos))) _pos++;
            var text = _s[start.._pos];
            if (Current.Lower == "head" && !Current.IsForeign && text.AsSpan().IndexOfAnyExcept(" \t\n\r\f") is var solid and >= 0)
            {
                // Text ends a head whose end tag is left out: the whitespace before it stays in the head
                Current.Children.Add(new HText(text[..solid]) { Start = start });
                CloseHead(start + solid);
                start += solid;
                text = text[solid..];
            }

            Current.Children.Add(new HText(text) { Start = start });
            CheckText(text, start);
        }

        foreach (var open in _stack) open.ContentEnd = _s.Length;
        ReportUnclosed(1, _s.Length, "eof-in-element", "End of file seen and there were open elements.");
    }

    private void CheckText(string text, int start)
    {
        var firstSolid = text.AsSpan().IndexOfAnyExcept(" \t\n\r\f");
        if (firstSolid >= 0)
        {
            AfterBody(start + firstSolid);
            NoteContent(start + firstSolid, "Non-space characters found without seeing a doctype first. Expected “<!DOCTYPE html>”.");
            if (Current.Lower is "table" or "tbody" or "thead" or "tfoot" or "tr")
                Error(start + firstSolid, "text-in-table", "Misplaced non-space characters inside a table.");
        }
        CheckCharacterReferences(text, start, attribute: false);

        // "</" that does not start an end tag (it is not followed by a letter).
        for (var i = text.IndexOf("</", StringComparison.Ordinal); i >= 0; i = text.IndexOf("</", i + 2, StringComparison.Ordinal))
        {
            Error(start + i, "bogus-end-tag", i + 2 < text.Length && text[i + 2] == '>'
                ? "Saw “</>”. Probable causes: Unescaped “<” (escape as “&lt;”) or mistyped end tag."
                : "Garbage after “</”. Probable cause: Unescaped “<” (escape as “&lt;”).");
        }
    }

    /// <summary>Reports a missing doctype the first time content appears.</summary>
    private void NoteContent(int offset, string message)
    {
        if (!_seenDoctype && !_seenContent) Error(offset, "missing-doctype", message);
        _seenContent = true;
    }

    private bool IsMarkupStart(int p)
    {
        if (p + 1 >= _s.Length) return false;
        var c = _s[p + 1];
        if (char.IsAsciiLetter(c) || c is '!' or '?') return true;
        return c == '/' && p + 2 < _s.Length && char.IsAsciiLetter(_s[p + 2]);
    }

    private void ReadMarkup()
    {
        var start = _pos;
        if (At("<!--"))
        {
            ReadComment(start);
            return;
        }
        if (At("<![CDATA["))
        {
            if (!InForeignContent) Error(start, "cdata-in-html", "Saw “<![CDATA[” in HTML content. CDATA sections are only allowed in SVG and MathML.");
            var end = _s.IndexOf("]]>", _pos, StringComparison.Ordinal);
            _pos = end < 0 ? _s.Length : end + 3;
            Current.Children.Add(new HVerbatim(_s[start.._pos], false) { Start = start });
            return;
        }
        if (_s[_pos + 1] is '!' or '?')
        {
            var end = _s.IndexOf('>', _pos);
            _pos = end < 0 ? _s.Length : end + 1;
            var raw = _s[start.._pos];
            var isDoctype = raw.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase);
            if (isDoctype) CheckDoctype(raw, start);
            else if (raw[1] == '?') Error(start, "processing-instruction", "Saw “<?”. Probable cause: Attempt to use an XML processing instruction in HTML. (XML processing instructions are not supported in HTML.)");
            else Error(start, "bogus-comment", "Bogus comment: “<!” must start “<!--” or “<!DOCTYPE”.");
            Current.Children.Add(new HVerbatim(raw, isDoctype) { Start = start });
            return;
        }
        if (_s[_pos + 1] == '/')
        {
            ReadEndTag();
            return;
        }
        ReadStartTag();
    }

    private void ReadComment(int start)
    {
        if (At("<!-->") || At("<!--->"))
        {
            Error(start, "abrupt-comment", "Premature end of comment. Use “-->” to end a comment properly.");
            _pos += At("<!-->") ? 5 : 6;
            Current.Children.Add(new HVerbatim(_s[start.._pos], false) { Start = start });
            return;
        }

        var close = _s.IndexOf("-->", _pos + 4, StringComparison.Ordinal);
        var searchEnd = close < 0 ? _s.Length : close + 3;
        var bangClose = _s.IndexOf("--!>", _pos + 4, searchEnd - (_pos + 4), StringComparison.Ordinal);
        if (bangClose >= 0 && (close < 0 || bangClose < close))
        {
            Error(bangClose, "comment-bang", "Comment ended with “--!>”. Use “-->” to end a comment.");
            _pos = bangClose + 4;
        }
        else if (close < 0)
        {
            Error(start, "eof-in-comment", "End of file inside comment: the comment is never closed with “-->”.");
            _pos = _s.Length;
        }
        else
        {
            _pos = close + 3;
        }

        var raw = _s[start.._pos];
        var nested = raw.IndexOf("<!--", 4, StringComparison.Ordinal);
        if (nested >= 0 && nested + 4 < raw.Length && raw[nested + 4] == '>') nested = -1;
        if (nested >= 0) Error(start + nested, "nested-comment", "Saw “<!--” within a comment. Probable cause: Nested comment (not allowed).");
        Current.Children.Add(new HVerbatim(raw, false) { Start = start });
    }

    private void CheckDoctype(string raw, int start)
    {
        if (_seenDoctype || _seenContent)
        {
            Error(start, "stray-doctype", "Stray doctype: a doctype must come first, and only once.");
            return;
        }
        _seenDoctype = true;

        // (a doctype at the end of the file can be without its ">")
        var words = raw[2..(raw.EndsWith('>') ? ^1 : ^0)].Split((char[])[' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries);
        var ok = words.Length >= 2 && words[1].Equals("html", StringComparison.OrdinalIgnoreCase) &&
                 (words.Length == 2 ||
                  (words.Length == 4 && words[2].Equals("system", StringComparison.OrdinalIgnoreCase) &&
                   words[3].Trim('"', '\'').Equals("about:legacy-compat", StringComparison.Ordinal)));
        if (!ok) Error(start, "obsolete-doctype", "Obsolete doctype. Expected “<!DOCTYPE html>”.");
    }

    private bool At(string text) => string.CompareOrdinal(_s, _pos, text, 0, text.Length) == 0;

    private void ReadEndTag()
    {
        var start = _pos;
        _pos += 2;
        var nameStart = _pos;
        while (_pos < _s.Length && !IsHtmlSpace(_s[_pos]) && _s[_pos] is not ('>' or '/')) _pos++;
        var name = _s[nameStart.._pos];
        var end = _s.IndexOf('>', _pos);
        if (end < 0) Error(start, "eof-in-tag", "End of file seen inside an end tag.");
        else CheckEndTagRest(start, name, _s[_pos..end]);
        _pos = end < 0 ? _s.Length : end + 1;
        var raw = _s[start.._pos];

        var lower = name.ToLowerInvariant();
        if (lower == "br")
        {
            Error(start, "br-end-tag", "End tag “br”. Use “<br>” for a line break.");
            Current.Children.Add(new HStrayEndTag(raw) { Start = start });
            return;
        }

        for (var k = _stack.Count - 1; k > 0; k--)
        {
            if (_stack[k].Lower != lower) continue;
            if (lower is "body" or "html" && !_stack[k].IsForeign) _afterBody = true;
            PopTo(k, start, $"End tag “{name}” seen, but there were open elements.");
            var element = _stack[k];
            element.EndTag = raw;
            element.ContentEnd = start;
            _stack.RemoveAt(k);
            return;
        }

        Error(start, "stray-end-tag", lower == "p"
            ? "No “p” element in scope but a “p” end tag seen."
            : $"Stray end tag “{name}”.");
        Current.Children.Add(new HStrayEndTag(raw) { Start = start });
    }

    /// <summary>Closes the elements above index <paramref name="k"/>, which have no end tag.</summary>
    private void PopTo(int k, int at, string message)
    {
        ReportUnclosed(k + 1, at, "unclosed-elements", message);
        while (_stack.Count - 1 > k)
        {
            _stack[^1].ContentEnd = at;
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    /// <summary>Reports the open elements from index <paramref name="from"/> up that need an end tag.</summary>
    private void ReportUnclosed(int from, int at, string code, string message)
    {
        var unclosed = _stack.Skip(from).Where(e => !HtmlNames.OptionalEndTag.Contains(e.Lower)).ToList();
        if (unclosed.Count == 0) return;
        Error(at, code, message);
        foreach (var e in unclosed)
            Error(e.Start, "unclosed-element", $"Unclosed element “{e.Name}”.");
    }

    private void ReadStartTag()
    {
        var start = _pos;
        _pos++;
        var nameStart = _pos;
        while (_pos < _s.Length && !IsHtmlSpace(_s[_pos]) && _s[_pos] is not ('>' or '/')) _pos++;
        var name = _s[nameStart.._pos];
        var lower = name.ToLowerInvariant();
        var foreign = InForeignContent || lower is "svg" or "math";
        var element = new HElement(name) { Start = start, IsForeign = foreign };

        NoteContent(start, "Start tag seen without seeing a doctype first. Expected “<!DOCTYPE html>”.");

        var names = new HashSet<string>(Ci);
        var closed = false;
        var first = true;
        while (_pos < _s.Length)
        {
            var hadSpace = SkipWhitespace();
            if (_pos >= _s.Length) break;
            var c = _s[_pos];
            if (c == '>')
            {
                _pos++;
                closed = true;
                break;
            }
            if (c == '/' && _pos + 1 < _s.Length && _s[_pos + 1] == '>')
            {
                element.SelfClosing = true;
                element.SpaceBeforeSlash = hadSpace;
                _pos += 2;
                closed = true;
                break;
            }
            if (c == '/')
            {
                _pos++;
                continue;
            }

            if (!hadSpace && !first)
                Error(_pos, "no-space-between-attributes", "No space between attributes.");
            first = false;

            var attrStart = _pos;
            _pos++;
            while (_pos < _s.Length && !IsHtmlSpace(_s[_pos]) && _s[_pos] is not ('>' or '/' or '=')) _pos++;
            var attrName = _s[attrStart.._pos];
            if (attrName.IndexOfAny(['"', '\'', '<']) >= 0)
                Error(attrStart, "bad-attribute-name", $"Quote or “<” in attribute name “{attrName}”. Probable cause: Missing “=” or a missing space.");

            string? value = null;
            var valueStart = -1;
            var save = _pos;
            SkipWhitespace();
            if (_pos < _s.Length && _s[_pos] == '=')
            {
                _pos++;
                SkipWhitespace();
                valueStart = _pos;
                value = ReadAttributeValue(valueStart);
                if (value.Length == 0)
                    Error(attrStart, "missing-attribute-value", $"Attribute value missing: “{attrName}=” has no value. Write {attrName}=\"…\", or leave out the “=”.");
                CheckCharacterReferences(value, valueStart, attribute: true);
            }
            else
            {
                _pos = save;
            }

            if (!names.Add(attrName))
                Error(attrStart, "duplicate-attribute", $"Duplicate attribute “{attrName}”.");
            element.Attributes.Add(new HAttribute(attrName, value, attrStart, valueStart));
        }

        if (!closed) Error(start, "eof-in-tag", $"End of file seen inside the start tag “{name}”.");

        if (element.SelfClosing && !element.IsVoid && !foreign)
            Error(start, "self-closing-non-void",
                $"Self-closing syntax (“/>”) used on a non-void HTML element “{name}”. Ignoring the slash and treating as a start tag.");

        if (lower is "html" or "head" or "body" && !_seenStructural.Add(lower))
            Error(start, "duplicate-structure", $"Start tag “{name}” seen but an element of the same type was already open.");
        if (lower is "a" or "form" or "button" && !foreign && _stack.Any(e => e.Lower == lower && !e.IsForeign))
            Error(start, "nested-element", $"Start tag “{name}” seen but an element of the same type was already open.");

        element.ContentStart = _pos;
        CloseImplied(lower, start);
        Current.Children.Add(element);

        if (element.SelfClosing || element.IsVoid) return;

        if (lower == "plaintext")
        {
            element.RawContent = _s[_pos..];
            _pos = _s.Length;
            element.ContentEnd = _pos;
            return;
        }
        if (HtmlNames.RawText.Contains(lower) && !(lower == "title" && InForeignContent))
        {
            ReadRawContent(element);
            return;
        }

        _stack.Add(element);
        if (_stack.Count - 1 > MaxDepth && _tooDeepAt < 0) _tooDeepAt = element.Start;
    }

    private string ReadAttributeValue(int start)
    {
        if (_pos >= _s.Length) return "";
        var quote = _s[_pos];
        if (quote is '"' or '\'')
        {
            var end = _s.IndexOf(quote, _pos + 1);
            if (end < 0) Error(start, "eof-in-attribute", "End of file inside an attribute value: the quote is never closed.");
            _pos = end < 0 ? _s.Length : end + 1;
            return _s[start.._pos];
        }
        while (_pos < _s.Length && !IsHtmlSpace(_s[_pos]) && _s[_pos] != '>') _pos++;
        var value = _s[start.._pos];
        var bad = value.IndexOfAny(['"', '\'', '<', '=', '`']);
        if (bad >= 0)
            Error(start + bad, "bad-unquoted-value", $"“{value[bad]}” in an unquoted attribute value. Probable cause: Missing quotes around the value.");
        return value;
    }

    // A start tag or text after </body> or </html> (a script added after the page, a second page pasted in): browsers
    // put it in the body, but it is a parse error
    private void AfterBody(int at)
    {
        if (!_afterBody || _toldAfterBody) return;
        _toldAfterBody = true;
        Error(at, "content-after-body",
            "Content after the end of the “body” (or “html”): browsers move it into the body. Move it before “</body>”.");
    }

    private bool SkipWhitespace()
    {
        var start = _pos;
        while (_pos < _s.Length && IsHtmlSpace(_s[_pos])) _pos++;
        return _pos > start;
    }

    /// <summary>Reads the text of a script, style, textarea … up to its end tag.</summary>
    private void ReadRawContent(HElement element)
    {
        var end = FindEndTag(element.Lower, _pos);
        if (end < 0)
        {
            Error(element.Start, "eof-in-element", $"End of file seen when looking for the end tag of “{element.Name}”.");
            element.RawContent = _s[_pos..];
            element.ContentEnd = _s.Length;
            _pos = _s.Length;
            return;
        }

        element.RawContent = _s[_pos..end];
        element.ContentEnd = end;
        if (element.Lower is "textarea" or "title")
            CheckCharacterReferences(element.RawContent, _pos, attribute: false);
        var close = _s.IndexOf('>', end);
        if (close < 0) Error(end, "eof-in-tag", "End of file seen inside an end tag.");
        else CheckEndTagRest(end, element.Name, _s[(end + 2 + element.Lower.Length)..close]);
        _pos = close < 0 ? _s.Length : close + 1;
        element.EndTag = _s[end.._pos];
    }

    /// <summary>
    /// What follows the name of an end tag, up to its "&gt;": only blanks (or a "/"). Anything else is taken as
    /// attributes of the end tag, also by browsers: "&lt;/title" with its "&gt;" left out takes the next tag into it
    /// ("&lt;/title\n&lt;meta charset=…&gt;"), and that tag is lost
    /// </summary>
    private void CheckEndTagRest(int start, string name, string rest)
    {
        if (string.IsNullOrWhiteSpace(rest) || rest.Trim() == "/") return;
        Error(start, "end-tag-attributes", rest.Contains('<')
            ? $"End tag “{name}” had attributes: “<” seen in it. Probable cause: Missing “>” immediately before, so the tag after it is part of the end tag."
            : $"End tag “{name}” had attributes.");
    }

    private int FindEndTag(string name, int from)
    {
        var p = from;
        while (true)
        {
            var i = _s.IndexOf("</", p, StringComparison.Ordinal);
            if (i < 0) return -1;
            var after = i + 2 + name.Length;
            if (after <= _s.Length &&
                string.Compare(_s, i + 2, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                (after == _s.Length || IsHtmlSpace(_s[after]) || _s[after] is '>' or '/'))
                return i;
            p = i + 2;
        }
    }

    /// <summary>
    /// Checks <c>&amp;…</c> in text and attribute values: numeric references must be complete and stand for
    /// an allowed character, and known names written without “;” are reported. Unknown names that end with
    /// “;” are not reported, as the list of names here is not complete.
    /// </summary>
    private void CheckCharacterReferences(string text, int offset, bool attribute)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '&') continue;
            var j = i + 1;
            if (j < text.Length && text[j] == '#')
            {
                var k = j + 1;
                var hex = k < text.Length && text[k] is 'x' or 'X';
                if (hex) k++;
                var digitsStart = k;
                while (k < text.Length && (hex ? char.IsAsciiHexDigit(text[k]) : char.IsAsciiDigit(text[k]))) k++;
                if (k == digitsStart)
                {
                    Error(offset + i, "bad-reference", "“&#” did not start a character reference. (“&” probably should have been escaped as “&amp;”.)");
                    continue;
                }
                if (k >= text.Length || text[k] != ';')
                    Error(offset + i, "unterminated-reference", "Character reference was not terminated by a semicolon.");

                var digits = text[digitsStart..k];
                var ok = int.TryParse(digits, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out var code);
                if (!ok || code == 0 || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
                    Error(offset + i, "bad-reference-codepoint", $"Character reference “{text[i..Math.Min(k + 1, text.Length)]}” does not stand for a valid character.");
                else if (code is >= 0x80 and <= 0x9F or (< 0x20 and not (0x09 or 0x0A or 0x0C or 0x0D)) or 0x7F)
                    Error(offset + i, "control-reference", $"Character reference “{text[i..Math.Min(k + 1, text.Length)]}” expands to a control character.");
                i = k;
                continue;
            }

            var n = j;
            while (n < text.Length && char.IsAsciiLetterOrDigit(text[n])) n++;
            if (n == j) continue;
            var name = text[j..n];
            if (n < text.Length && text[n] == ';') continue;
            if (attribute && n < text.Length && (text[n] == '=' || char.IsAsciiLetterOrDigit(text[n]))) continue;
            if (LegacyReferences.Contains(name))
                Error(offset + i, "unterminated-reference",
                    $"Named character reference “&{name}” was not terminated by a semicolon. (Or “&” should have been escaped as “&amp;”.)");
        }
    }

    /// <summary>Closes elements whose end tag may be left out, as a new start tag implies (simplified from the HTML standard).</summary>
    private void CloseImplied(string name, int at)
    {
        switch (name)
        {
            case "li":
                CloseUpTo(["li"], ["ul", "ol", "menu"], at);
                break;
            case "dt" or "dd":
                CloseUpTo(["dt", "dd"], ["dl"], at);
                break;
            case "tr":
                CloseUpTo(["tr", "colgroup", "caption"], ["table", "thead", "tbody", "tfoot"], at);
                break;
            case "colgroup":
                CloseUpTo(["colgroup", "caption"], ["table"], at);
                break;
            case "col":
                CloseUpTo(["caption"], ["table", "colgroup"], at);
                break;
            case "td" or "th":
                CloseUpTo(["td", "th"], ["tr", "table"], at);
                break;
            case "thead" or "tbody" or "tfoot":
                CloseUpTo(["thead", "tbody", "tfoot", "colgroup", "caption"], ["table"], at);
                break;
            case "option":
                CloseUpTo(["option"], ["select", "datalist", "optgroup"], at);
                break;
            case "optgroup":
                CloseUpTo(["option", "optgroup"], ["select"], at);
                break;
            case "rt" or "rp":
                CloseUpTo(["rt", "rp"], ["ruby"], at);
                break;
            case "body":
                CloseUpTo(["head"], ["html"], at);
                break;
            default:
                // Any other start tag ends a head whose end tag is left out
                if (!HeadContent.Contains(name) && Current.Lower == "head" && !Current.IsForeign) CloseHead(at);
                break;
        }

        if (ClosesParagraph.Contains(name))
        {
            for (var k = _stack.Count - 1; k > 0; k--)
            {
                var lower = _stack[k].Lower;
                if (lower == "p")
                {
                    PopTo(k - 1, at, "End tag “p” implied, but there were open elements.");
                    return;
                }
                if (HtmlNames.Block.Contains(lower) || HtmlNames.Container.Contains(lower) || lower == "button") return;
            }
        }
    }

    // Ends the head (its end tag may be left out)
    private void CloseHead(int at)
    {
        Current.ContentEnd = at;
        _stack.RemoveAt(_stack.Count - 1);
    }

    private void CloseUpTo(string[] targets, string[] boundaries, int at)
    {
        for (var k = _stack.Count - 1; k > 0; k--)
        {
            var lower = _stack[k].Lower;
            if (targets.Contains(lower))
            {
                PopTo(k - 1, at, $"End tag “{_stack[k].Name}” implied, but there were open elements.");
                return;
            }
            if (boundaries.Contains(lower)) return;
        }
    }
}
