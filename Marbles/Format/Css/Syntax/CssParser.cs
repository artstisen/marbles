// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Css/Syntax/CssParser.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
namespace Marbles.Format.Css.Syntax;

/// <summary>Something in a style sheet or a block: a rule, an at-rule, a declaration or a comment.</summary>
public abstract class CssItem
{
    /// <summary>Offset of the first character.</summary>
    public int Start { get; init; }

    /// <summary>True when the source has an empty line between this item and the one before.</summary>
    public bool BlankLineBefore { get; set; }

    /// <summary>True when the item starts on the same line as the previous item ends (a trailing comment).</summary>
    public bool SameLineAsPrevious { get; set; }
}

public sealed class CssComment(CssToken token) : CssItem
{
    public CssToken Token { get; } = token;
}

/// <summary>A rule with a selector: <c>a:hover { color: red }</c>.</summary>
public sealed class CssStyleRule(List<CssToken> prelude, CssBlock block) : CssItem
{
    public List<CssToken> Prelude { get; } = prelude;
    public CssBlock Block { get; } = block;
}

/// <summary><c>@media screen { ... }</c> or <c>@import url(a.css);</c>.</summary>
public sealed class CssAtRule(CssToken nameToken, List<CssToken> prelude, CssBlock? block) : CssItem
{
    public CssToken NameToken { get; } = nameToken;
    public string Name => NameToken.Value.ToLowerInvariant();
    public List<CssToken> Prelude { get; } = prelude;

    /// <summary>Null for a statement at-rule (ending with <c>;</c>).</summary>
    public CssBlock? Block { get; } = block;
}

/// <summary><c>color: red !important</c>.</summary>
public sealed class CssDeclaration(CssToken nameToken, List<CssToken> value, bool important) : CssItem
{
    public CssToken NameToken { get; } = nameToken;

    /// <summary>The name as written (custom properties are case-sensitive).</summary>
    public string Name => NameToken.Value;

    /// <summary>The value without surrounding whitespace and without <c>!important</c>.</summary>
    public List<CssToken> Value { get; } = value;

    public bool Important { get; } = important;

    /// <summary>
    /// All the tokens of the declaration as written, from the name to the end of the value (comments and
    /// <c>!important</c> included, the <c>;</c> not), without whitespace around them: the formatter writes these
    /// </summary>
    public List<CssToken> Tokens { get; init; } = [];

    /// <summary>The place of the <c>:</c> in <see cref="Tokens"/>.</summary>
    public int ColonIndex { get; init; }

    public bool IsCustomProperty => Name.StartsWith("--", StringComparison.Ordinal);
}

/// <summary>Text that could not be read as a declaration or rule. Kept as it is.</summary>
public sealed class CssBogus(List<CssToken> tokens) : CssItem
{
    public List<CssToken> Tokens { get; } = tokens;

    /// <summary>True when it ended with a <c>;</c> (not part of <see cref="Tokens"/>).</summary>
    public bool HadSemicolon { get; init; }
}

public sealed class CssBlock(CssToken open)
{
    public CssToken Open { get; } = open;
    public List<CssItem> Items { get; } = [];
}

public sealed class CssStylesheet
{
    public required string Source { get; init; }
    public required List<CssToken> Tokens { get; init; }
    public List<CssItem> Items { get; } = [];
    public List<CssSyntaxError> Errors { get; } = [];

    /// <summary>True when a block is never closed or a <c>}</c> has no block: the formatter refuses such files.</summary>
    public bool HasStructuralErrors { get; set; }
}

/// <summary>
/// Parser following the structure of CSS Syntax Level 3, section 5, including nested rules
/// (CSS Nesting). Comments are kept as items so the formatter can write them back.
/// </summary>
public sealed class CssParser
{
    private readonly List<CssToken> _t;
    private readonly CssStylesheet _sheet;
    private int _i;

    private CssParser(CssStylesheet sheet)
    {
        _sheet = sheet;
        _t = sheet.Tokens;
    }

    /// <summary>
    /// How deep blocks, parentheses, brackets and functions may be nested. The parser and the checks recurse, so a
    /// file nested deeper (made by a mistake or on purpose) is not read further, instead of overflowing the stack
    /// </summary>
    public const int MaxDepth = 256;

    public static CssStylesheet Parse(string source)
    {
        var tokens = CssTokenizer.Tokenize(source, out var tokenErrors);
        var sheet = new CssStylesheet { Source = source, Tokens = tokens };
        sheet.Errors.AddRange(tokenErrors);
        if (!TooDeep(sheet)) new CssParser(sheet).ParseRuleList(sheet.Items, topLevel: true);
        return sheet;
    }

    // True (with an error that the formatter refuses) when the tokens are nested deeper than MaxDepth
    private static bool TooDeep(CssStylesheet sheet)
    {
        var depth = 0;
        foreach (var token in sheet.Tokens)
        {
            if (token.Type is CssTokenType.LeftCurly or CssTokenType.LeftParen or CssTokenType.LeftSquare or CssTokenType.Function)
            {
                if (++depth <= MaxDepth) continue;
                sheet.Errors.Add(new CssSyntaxError(token.Start, "css.parse.too-deep",
                    $"Blocks, parentheses or functions are nested more than {MaxDepth} deep: the rest of the file is not checked."));
                sheet.HasStructuralErrors = true;
                return true;
            }

            if (token.Type is CssTokenType.RightCurly or CssTokenType.RightParen or CssTokenType.RightSquare && depth > 0)
                depth--;
        }

        return false;
    }

    private CssToken Current => _t[_i];

    private void Error(CssToken at, string code, string message, bool structural = false)
    {
        _sheet.Errors.Add(new CssSyntaxError(at.Start, code, message));
        if (structural) _sheet.HasStructuralErrors = true;
    }

    /// <summary>Skips whitespace, and tells whether it held an empty line or any line break.</summary>
    private (bool BlankLine, bool LineBreak) SkipWhitespace()
    {
        var newlines = 0;
        while (Current.Type == CssTokenType.Whitespace)
        {
            newlines += CountNewlines(Current.Text(_sheet.Source));
            _i++;
        }
        return (newlines >= 2, newlines >= 1);
    }

    private static int CountNewlines(string text)
    {
        var count = 0;
        for (var k = 0; k < text.Length; k++)
        {
            if (text[k] == '\r' && k + 1 < text.Length && text[k + 1] == '\n') k++;
            if (text[k] is '\n' or '\r' or '\f') count++;
        }
        return count;
    }

    private static void Add(List<CssItem> items, CssItem item, (bool BlankLine, bool LineBreak) space)
    {
        item.BlankLineBefore = items.Count > 0 && space.BlankLine;
        item.SameLineAsPrevious = items.Count > 0 && !space.LineBreak;
        items.Add(item);
    }

    private void ParseRuleList(List<CssItem> items, bool topLevel)
    {
        while (true)
        {
            var space = SkipWhitespace();
            var token = Current;
            switch (token.Type)
            {
                case CssTokenType.EndOfFile:
                    return;
                case CssTokenType.Comment:
                    _i++;
                    Add(items, new CssComment(token) { Start = token.Start }, space);
                    break;
                case CssTokenType.Cdo or CssTokenType.Cdc when topLevel:
                    _i++;
                    break;
                case CssTokenType.RightCurly:
                    Error(token, "css.parse.unexpected-close", "Unexpected “}”: there is no block to close.", structural: true);
                    _i++;
                    Add(items, new CssBogus([token]) { Start = token.Start }, space);
                    break;
                case CssTokenType.AtKeyword:
                    Add(items, ParseAtRule(), space);
                    break;
                default:
                    Add(items, ParseQualifiedRule(), space);
                    break;
            }
        }
    }

    private CssAtRule ParseAtRule()
    {
        var name = Current;
        _i++;
        var prelude = new List<CssToken>();
        while (true)
        {
            var token = Current;
            if (token.Type == CssTokenType.EndOfFile)
                return new CssAtRule(name, Trim(prelude), null) { Start = name.Start };
            if (token.Type == CssTokenType.Semicolon)
            {
                _i++;
                return new CssAtRule(name, Trim(prelude), null) { Start = name.Start };
            }
            if (token.Type == CssTokenType.RightCurly)
            {
                // The end of the enclosing block; the statement ends without ';'.
                return new CssAtRule(name, Trim(prelude), null) { Start = name.Start };
            }
            if (token.Type == CssTokenType.LeftCurly)
            {
                var block = ParseBlock();
                return new CssAtRule(name, Trim(prelude), block) { Start = name.Start };
            }
            prelude.AddRange(ConsumeComponent());
        }
    }

    private CssItem ParseQualifiedRule()
    {
        var start = Current;
        var prelude = new List<CssToken>();
        while (true)
        {
            var token = Current;
            if (token.Type == CssTokenType.EndOfFile)
            {
                Error(start, "css.parse.missing-block", "Selector has no “{ … }” block.");
                return new CssBogus(prelude) { Start = start.Start };
            }
            if (token.Type == CssTokenType.LeftCurly)
            {
                var block = ParseBlock();
                return new CssStyleRule(Trim(prelude), block) { Start = start.Start };
            }
            if (token.Type == CssTokenType.RightCurly)
            {
                Error(start, "css.parse.missing-block", "Selector has no “{ … }” block.");
                return new CssBogus(Trim(prelude)) { Start = start.Start };
            }
            prelude.AddRange(ConsumeComponent());
        }
    }

    /// <summary>
    /// Parses the value of an HTML <c>style</c> attribute: declarations, as inside a <c>{ … }</c> block, without the
    /// braces. The items are in <see cref="CssStylesheet.Items"/>
    /// </summary>
    public static CssStylesheet ParseDeclarationList(string source)
    {
        var tokens = CssTokenizer.Tokenize(source, out var tokenErrors);
        var sheet = new CssStylesheet { Source = source, Tokens = tokens };
        sheet.Errors.AddRange(tokenErrors);
        if (!TooDeep(sheet)) new CssParser(sheet).ParseBlockItems(sheet.Items, null);
        return sheet;
    }

    /// <summary>Parses a <c>{ … }</c> block holding declarations, nested rules and at-rules.</summary>
    private CssBlock ParseBlock()
    {
        var block = new CssBlock(Current);
        _i++;
        ParseBlockItems(block.Items, block.Open);
        return block;
    }

    /// <summary>
    /// The declarations, nested rules and at-rules of a block, to its <c>}</c>, or to the end of the text when there is
    /// no opening brace (a style attribute)
    /// </summary>
    private void ParseBlockItems(List<CssItem> items, CssToken? open)
    {
        while (true)
        {
            var space = SkipWhitespace();
            var token = Current;
            switch (token.Type)
            {
                case CssTokenType.EndOfFile:
                    if (open != null)
                        Error(open, "css.parse.unclosed-block", "Block is never closed (missing “}”).", structural: true);
                    return;
                case CssTokenType.RightCurly when open == null:
                    Error(token, "css.parse.unexpected-close", "Unexpected “}”: there is no block to close.", structural: true);
                    _i++;
                    break;
                case CssTokenType.RightCurly:
                    _i++;
                    return;
                case CssTokenType.Semicolon:
                    _i++;
                    break;
                case CssTokenType.Comment:
                    _i++;
                    Add(items, new CssComment(token) { Start = token.Start }, space);
                    break;
                case CssTokenType.AtKeyword:
                    Add(items, ParseAtRule(), space);
                    break;
                default:
                    Add(items, IsNestedRule() ? ParseQualifiedRule() : ParseDeclaration(), space);
                    break;
            }
        }
    }

    /// <summary>Looks ahead: a nested rule reaches a <c>{</c> before a <c>;</c> or <c>}</c>.</summary>
    private bool IsNestedRule()
    {
        var token = Current;
        if (token.Type == CssTokenType.Ident && token.Value.StartsWith("--", StringComparison.Ordinal))
        {
            var k = _i + 1;
            while (_t[k].IsTrivia) k++;
            if (_t[k].Type == CssTokenType.Colon) return false;
        }

        var depth = 0;
        for (var k = _i; k < _t.Count; k++)
        {
            switch (_t[k].Type)
            {
                case CssTokenType.Function or CssTokenType.LeftParen or CssTokenType.LeftSquare:
                    depth++;
                    break;
                case CssTokenType.RightParen or CssTokenType.RightSquare:
                    if (depth > 0) depth--;
                    break;
                case CssTokenType.LeftCurly when depth == 0:
                    return true;
                case CssTokenType.Semicolon or CssTokenType.RightCurly when depth == 0:
                    return false;
                case CssTokenType.EndOfFile:
                    return false;
            }
        }
        return false;
    }

    private CssItem ParseDeclaration()
    {
        var start = Current;
        var tokens = new List<CssToken>();
        var custom = start.Type == CssTokenType.Ident && start.Value.StartsWith("--", StringComparison.Ordinal);
        while (Current.Type is not (CssTokenType.Semicolon or CssTokenType.RightCurly or CssTokenType.EndOfFile))
            tokens.AddRange(ConsumeComponent(allowBraces: custom));
        var semicolon = Current.Type == CssTokenType.Semicolon;
        if (semicolon) _i++;
        tokens = Trim(tokens);

        if (start.Type != CssTokenType.Ident)
        {
            Error(start, "css.parse.invalid-declaration", $"“{Shorten(start.Text(_sheet.Source))}” is not a valid declaration: expected a property name.");
            return new CssBogus(tokens) { Start = start.Start, HadSemicolon = semicolon };
        }

        var k = 1;
        while (k < tokens.Count && tokens[k].IsTrivia) k++;
        if (k >= tokens.Count || tokens[k].Type != CssTokenType.Colon)
        {
            Error(start, "css.parse.missing-colon", $"Expected “:” after the property name “{start.Value}”.");
            return new CssBogus(tokens) { Start = start.Start, HadSemicolon = semicolon };
        }

        var value = Trim(tokens.GetRange(k + 1, tokens.Count - k - 1));
        var important = false;

        var last = value.Count - 1;
        while (last >= 0 && value[last].IsTrivia) last--;
        if (last >= 0 && value[last].IsIdent("important"))
        {
            var bang = last - 1;
            while (bang >= 0 && value[bang].IsTrivia) bang--;
            if (bang >= 0 && value[bang].IsDelim('!'))
            {
                important = true;
                value = Trim(value.GetRange(0, bang));
            }
        }

        return new CssDeclaration(start, value, important) { Start = start.Start, Tokens = tokens, ColonIndex = k };
    }

    /// <summary>
    /// Consumes one component value: a single token, or a whole function or ( ) / [ ] block with its contents.
    /// A { } block is only taken whole when <paramref name="allowBraces"/> is set (values of custom properties).
    /// </summary>
    private List<CssToken> ConsumeComponent(bool allowBraces = false)
    {
        var token = Current;
        _i++;
        var result = new List<CssToken> { token };

        var close = token.Type switch
        {
            CssTokenType.Function or CssTokenType.LeftParen => CssTokenType.RightParen,
            CssTokenType.LeftSquare => CssTokenType.RightSquare,
            CssTokenType.LeftCurly when allowBraces => CssTokenType.RightCurly,
            _ => (CssTokenType?)null,
        };
        if (close is null) return result;

        while (true)
        {
            var next = Current;
            if (next.Type == CssTokenType.EndOfFile)
            {
                var what = close == CssTokenType.RightSquare ? "]" : close == CssTokenType.RightCurly ? "}" : ")";
                Error(token, "css.parse.unclosed-parenthesis", $"“{Shorten(token.Text(_sheet.Source))}” is never closed (missing “{what}”).", structural: true);
                return result;
            }
            if (next.Type == close)
            {
                _i++;
                result.Add(next);
                return result;
            }
            // A '}' or ';' that is not ours ends a broken value inside a block; leave it for the caller.
            if (!allowBraces && next.Type is CssTokenType.RightCurly or CssTokenType.Semicolon)
            {
                var what = close == CssTokenType.RightSquare ? "]" : ")";
                Error(token, "css.parse.unclosed-parenthesis", $"“{Shorten(token.Text(_sheet.Source))}” is not closed (missing “{what}”).", structural: true);
                return result;
            }
            result.AddRange(ConsumeComponent(allowBraces));
        }
    }

    private static List<CssToken> Trim(List<CssToken> tokens)
    {
        var start = 0;
        var end = tokens.Count;
        while (start < end && tokens[start].Type == CssTokenType.Whitespace) start++;
        while (end > start && tokens[end - 1].Type == CssTokenType.Whitespace) end--;
        return tokens.GetRange(start, end - start);
    }

    private static string Shorten(string text) => text.Length > 30 ? text[..30] + "…" : text;
}
