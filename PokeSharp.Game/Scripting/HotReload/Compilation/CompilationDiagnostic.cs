namespace PokeSharp.Game.Scripting.HotReload.Compilation;

/// <summary>
///     Detailed compilation diagnostic information for error reporting and rollback decisions.
/// </summary>
public class CompilationDiagnostic
{
    public DiagnosticSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
    public string? Code { get; init; }
    public string? FilePath { get; init; }

    public override string ToString()
    {
        return $"[{Severity}] Line {Line}, Col {Column}: {Message} ({Code})";
    }
}
