// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Core/Diagnostic.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
namespace Marbles.Format.Core;

/// <summary>How serious a diagnostic is.</summary>
public enum Severity
{
    Info,
    Warning,
    Error,
}

/// <summary>A 1-based position in a source file. Zero means "unknown".</summary>
public readonly record struct SourcePosition(int Line, int Column)
{
    public override string ToString() => Line > 0 ? $"{Line}:{Column}" : "?";
}

/// <summary>A single finding reported by a validator.</summary>
/// <param name="File">Path of the file the finding belongs to.</param>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Code">Stable, machine-readable identifier, e.g. <c>html.img-missing-alt</c>.</param>
/// <param name="Message">Human-readable description.</param>
/// <param name="Start">Where the problem starts.</param>
/// <param name="End">Where the problem ends (may equal <paramref name="Start"/>).</param>
/// <param name="Engine">Which engine produced the finding: <c>native</c> or <c>vnu</c>.</param>
public sealed record Diagnostic(
    string File,
    Severity Severity,
    string Code,
    string Message,
    SourcePosition Start,
    SourcePosition End,
    string Engine = "native");
