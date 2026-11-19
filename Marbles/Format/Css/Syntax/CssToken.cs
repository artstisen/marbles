// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Css/Syntax/CssToken.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
namespace Marbles.Format.Css.Syntax;

/// <summary>Token types from CSS Syntax Level 3 (https://www.w3.org/TR/css-syntax-3/#tokenization), plus comments.</summary>
public enum CssTokenType
{
    Ident,
    Function,
    AtKeyword,
    Hash,
    String,
    BadString,
    Url,
    BadUrl,
    Delim,
    Number,
    Percentage,
    Dimension,
    Whitespace,
    Comment,
    Cdo,
    Cdc,
    Colon,
    Semicolon,
    Comma,
    LeftSquare,
    RightSquare,
    LeftParen,
    RightParen,
    LeftCurly,
    RightCurly,
    EndOfFile,
}

/// <summary>A token with its place in the source.</summary>
/// <param name="Type">The kind of token.</param>
/// <param name="Start">Offset of the first character in the source.</param>
/// <param name="End">Offset just after the last character.</param>
/// <param name="Value">
/// Ident, function, at-keyword and hash names (without <c>(</c>, <c>@</c> or <c>#</c>), string and url
/// contents, the character of a delim, and the unit of a dimension.
/// </param>
public sealed record CssToken(CssTokenType Type, int Start, int End, string Value)
{
    /// <summary>The number of Number, Percentage and Dimension tokens.</summary>
    public double Number { get; init; }

    /// <summary>True when a number was written without a fraction or exponent.</summary>
    public bool IsInteger { get; init; }

    /// <summary>True for a hash whose name is a valid identifier (an ID selector).</summary>
    public bool IsIdHash { get; init; }

    public bool IsDelim(char c) => Type == CssTokenType.Delim && Value.Length == 1 && Value[0] == c;

    public bool IsIdent(string name) => Type == CssTokenType.Ident && Value.Equals(name, StringComparison.OrdinalIgnoreCase);

    public bool IsTrivia => Type is CssTokenType.Whitespace or CssTokenType.Comment;

    public string Text(string source) => source[Start..End];
}
